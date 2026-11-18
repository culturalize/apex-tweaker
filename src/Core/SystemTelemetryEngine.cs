using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PCTweaker.Core
{
    public class SystemTelemetryEngine
    {
        private ulong _prevIdleTicks;
        private ulong _prevTotalTicks;
        private double _lastCpuPercent;
        private string _cachedPowerPlanName;
        private DateTime _lastPowerPlanCheck;

        public SystemTelemetryEngine()
        {
            _cachedPowerPlanName = "Balanced";
            _lastPowerPlanCheck = DateTime.MinValue;
            PrimeCpuCounters();
        }

        private void PrimeCpuCounters()
        {
            NativeInterop.FILETIME idle, kernel, user;
            if (NativeInterop.GetSystemTimes(out idle, out kernel, out user))
            {
                _prevIdleTicks = idle.ToUInt64();
                _prevTotalTicks = kernel.ToUInt64() + user.ToUInt64();
            }
        }

        public TelemetrySample CaptureSample(HardwareProfile hw, IList<TweakDefinition> tweaks)
        {
            TelemetrySample sample = new TelemetrySample();

            // 1. CPU Load Delta
            try
            {
                NativeInterop.FILETIME idle, kernel, user;
                if (NativeInterop.GetSystemTimes(out idle, out kernel, out user))
                {
                    ulong idleTicks = idle.ToUInt64();
                    ulong totalTicks = kernel.ToUInt64() + user.ToUInt64();

                    ulong totalDiff = totalTicks - _prevTotalTicks;
                    ulong idleDiff = idleTicks - _prevIdleTicks;

                    if (totalDiff > 0)
                    {
                        double busy = 1.0 - ((double)idleDiff / (double)totalDiff);
                        _lastCpuPercent = Math.Max(0.0, Math.Min(100.0, busy * 100.0));
                    }

                    _prevIdleTicks = idleTicks;
                    _prevTotalTicks = totalTicks;
                }
            }
            catch { }
            sample.CpuUsagePercent = Math.Round(_lastCpuPercent, 1);

            // 2. Physical Memory Status
            try
            {
                NativeInterop.MEMORYSTATUSEX mem = new NativeInterop.MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(NativeInterop.MEMORYSTATUSEX));
                if (NativeInterop.GlobalMemoryStatusEx(ref mem))
                {
                    sample.TotalRamMb = (long)(mem.ullTotalPhys / (1024UL * 1024UL));
                    sample.AvailableRamMb = (long)(mem.ullAvailPhys / (1024UL * 1024UL));
                    sample.UsedRamMb = Math.Max(0L, sample.TotalRamMb - sample.AvailableRamMb);
                    sample.MemoryLoadPercent = (int)mem.dwMemoryLoad;
                }
            }
            catch { }

            // 3. Windows Kernel Timer Resolution
            try
            {
                uint minRes, maxRes, curRes;
                if (NativeInterop.NtQueryTimerResolution(out minRes, out maxRes, out curRes) == 0)
                {
                    sample.TimerResMinMs = Math.Round(minRes / 10000.0, 3);
                    sample.TimerResMaxMs = Math.Round(maxRes / 10000.0, 3);
                    sample.TimerResCurrentMs = Math.Round(curRes / 10000.0, 3);
                }
            }
            catch { }

            // 4. Process, Thread, Handle Counts & Kernel Standby / System Cache RAM Breakdown
            try
            {
                NativeInterop.PERFORMANCE_INFORMATION perf = new NativeInterop.PERFORMANCE_INFORMATION();
                perf.cb = (uint)Marshal.SizeOf(typeof(NativeInterop.PERFORMANCE_INFORMATION));
                if (NativeInterop.GetPerformanceInfo(out perf, perf.cb))
                {
                    sample.ProcessCount = (int)perf.ProcessCount;
                    sample.ThreadCount = (int)perf.ThreadCount;
                    sample.HandleCount = (int)perf.HandleCount;

                    ulong pageSize = (ulong)perf.PageSize.ToInt64();
                    ulong cachePages = (ulong)perf.SystemCache.ToInt64();
                    long rawCacheMb = (long)((cachePages * pageSize) / (1024UL * 1024UL));

                    sample.StandbyCacheMb = Math.Max(0L, Math.Min(sample.AvailableRamMb, rawCacheMb));
                    sample.FreeRamMb = Math.Max(0L, sample.AvailableRamMb - sample.StandbyCacheMb);
                }
                else
                {
                    sample.StandbyCacheMb = sample.AvailableRamMb / 2;
                    sample.FreeRamMb = sample.AvailableRamMb - sample.StandbyCacheMb;
                }
            }
            catch
            {
                sample.StandbyCacheMb = 0;
                sample.FreeRamMb = sample.AvailableRamMb;
            }

            // 5. Active Power Plan (refresh every 15 seconds to keep sampling <1ms)
            if ((DateTime.UtcNow - _lastPowerPlanCheck).TotalSeconds > 15)
            {
                string rawPlan = TweakCatalog.GetActivePowerSchemeInfo();
                int openParen = rawPlan.IndexOf('(');
                int closeParen = rawPlan.LastIndexOf(')');
                if (openParen >= 0 && closeParen > openParen)
                    _cachedPowerPlanName = rawPlan.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                else
                    _cachedPowerPlanName = rawPlan;
                _lastPowerPlanCheck = DateTime.UtcNow;
            }
            sample.ActivePowerPlanName = _cachedPowerPlanName;

            // 6. Tweak Counts & Optimization Score
            if (tweaks != null)
            {
                SystemTier tier = hw != null ? hw.DetectedTier : SystemTier.MidRangeGamer;
                int total = tweaks.Count;
                int applied = 0;
                int recTotal = 0;
                int recApplied = 0;

                foreach (TweakDefinition t in tweaks)
                {
                    if (t.CurrentStatus == TweakStatus.Applied)
                        applied++;

                    if (t.IsRecommendedForTier(tier))
                    {
                        recTotal++;
                        if (t.CurrentStatus == TweakStatus.Applied)
                            recApplied++;
                    }
                }

                sample.TotalTweaksCount = total;
                sample.AppliedTweaksCount = applied;
                sample.RecommendedTotalCount = recTotal;
                sample.RecommendedAppliedCount = recApplied;
                sample.OptimizationScorePercent = recTotal > 0
                    ? (int)Math.Round((recApplied * 100.0) / recTotal)
                    : 0;
            }

            return sample;
        }
    }
}
