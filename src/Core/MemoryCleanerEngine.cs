using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PCTweaker.Core
{
    public static class MemoryCleanerEngine
    {
        private static readonly HashSet<string> ProtectedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
            "winlogon", "dwm", "audiodg", "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService",
            "vgc", "vgk", "faceit", "faceitclient", "faceitservice", "PnkBstrA", "PnkBstrB"
        };

        public static MemoryPurgeResult PurgeMemory(bool trimWorkingSets = true, bool purgeStandbyList = true)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MemoryPurgeResult result = new MemoryPurgeResult();
            result.RamAvailableBeforeMb = GetAvailablePhysicalRamMb();

            int trimmedCount = 0;

            // 1. Acquire required token privileges
            NativeInterop.EnablePrivilege(NativeInterop.SE_PROFILE_SINGLE_PROCESS_NAME);
            NativeInterop.EnablePrivilege(NativeInterop.SE_INCREASE_QUOTA_NAME);

            // 2. Trim process working sets via psapi.dll EmptyWorkingSet using fast OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA)
            if (trimWorkingSets)
            {
                int currentPid = Process.GetCurrentProcess().Id;
                int currentSessionId = Process.GetCurrentProcess().SessionId;
                Process[] procs = Process.GetProcesses();
                foreach (Process p in procs)
                {
                    IntPtr hProc = IntPtr.Zero;
                    try
                    {
                        if (p.Id <= 4 || p.Id == currentPid || p.SessionId != currentSessionId) continue;
                        if (ProtectedProcessNames.Contains(p.ProcessName)) continue;

                        hProc = NativeInterop.OpenProcess(
                            NativeInterop.PROCESS_QUERY_LIMITED_INFORMATION | NativeInterop.PROCESS_SET_QUOTA,
                            false,
                            p.Id);
                        if (hProc != IntPtr.Zero && NativeInterop.EmptyWorkingSet(hProc))
                        {
                            trimmedCount++;
                        }
                    }
                    catch { }
                    finally
                    {
                        if (hProc != IntPtr.Zero)
                            NativeInterop.CloseHandle(hProc);
                        try { p.Dispose(); } catch { }
                    }
                }
            }

            // 3. Call ntdll NtSetSystemInformation for kernel memory lists (requires Admin + SeProfileSingleProcessPrivilege)
            bool standbyPurged = false;
            if (purgeStandbyList)
            {
                try
                {
                    if (trimWorkingSets)
                    {
                        int cmdWs = NativeInterop.MemoryEmptyWorkingSets;
                        NativeInterop.NtSetSystemInformation(NativeInterop.SystemMemoryListInformation, ref cmdWs, sizeof(int));
                    }

                    int cmdMod = NativeInterop.MemoryFlushModifiedList;
                    NativeInterop.NtSetSystemInformation(NativeInterop.SystemMemoryListInformation, ref cmdMod, sizeof(int));

                    int cmdLow = NativeInterop.MemoryPurgeLowPriorityStandbyList;
                    NativeInterop.NtSetSystemInformation(NativeInterop.SystemMemoryListInformation, ref cmdLow, sizeof(int));

                    int cmdStandby = NativeInterop.MemoryPurgeStandbyList;
                    uint status = NativeInterop.NtSetSystemInformation(NativeInterop.SystemMemoryListInformation, ref cmdStandby, sizeof(int));
                    standbyPurged = (status == 0);
                }
                catch { }
            }

            sw.Stop();
            result.RamAvailableAfterMb = GetAvailablePhysicalRamMb();
            result.FreedMb = Math.Max(0L, result.RamAvailableAfterMb - result.RamAvailableBeforeMb);
            result.ProcessesTrimmed = trimmedCount;
            result.StandbyListPurged = standbyPurged;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Success = true;
            result.SummaryMessage = string.Format(
                "Memory purge complete in {0} ms: Freed {1} MB physical RAM ({2} -> {3} MB available), trimmed {4} background working sets{5}.",
                result.DurationMs,
                result.FreedMb,
                result.RamAvailableBeforeMb,
                result.RamAvailableAfterMb,
                result.ProcessesTrimmed,
                standbyPurged ? ", and purged Kernel Standby Page List" : " (Run as Admin to also purge Kernel Standby List)");

            return result;
        }

        public static CacheCleanResult CleanShaderAndTempCaches(bool includeGpuShaderCaches)
        {
            CacheCleanResult res = new CacheCleanResult();
            List<string> targetDirs = new List<string>();

            string userTemp = Path.GetTempPath();
            if (!string.IsNullOrEmpty(userTemp) && Directory.Exists(userTemp))
                targetDirs.Add(userTemp);

            string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (Directory.Exists(winTemp))
                targetDirs.Add(winTemp);

            if (includeGpuShaderCaches)
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] shaderDirs = new string[]
                {
                    Path.Combine(localAppData, "D3DSCache"),
                    Path.Combine(localAppData, @"NVIDIA\DXCache"),
                    Path.Combine(localAppData, @"NVIDIA\GLCache"),
                    Path.Combine(localAppData, @"AMD\DxCache"),
                    Path.Combine(localAppData, @"AMD\GLCache")
                };

                foreach (string d in shaderDirs)
                {
                    if (Directory.Exists(d))
                        targetDirs.Add(d);
                }
            }

            long bytesFreed = 0;
            int deleted = 0;
            int skipped = 0;

            foreach (string dir in targetDirs)
            {
                CleanDirectoryContentsSafe(dir, ref bytesFreed, ref deleted, ref skipped);
            }

            res.Success = true;
            res.FilesDeleted = deleted;
            res.FilesSkippedInUse = skipped;
            res.FreedMb = Math.Round(bytesFreed / (1024.0 * 1024.0), 2);
            res.SummaryMessage = string.Format(
                "Cleaned {0} stale cache/temp files ({1:F2} MB reclaimed; {2} active locked files safely skipped).",
                deleted, res.FreedMb, skipped);
            return res;
        }

        private static void CleanDirectoryContentsSafe(string rootDir, ref long bytesFreed, ref int deleted, ref int skipped)
        {
            try
            {
                string[] files = Directory.GetFiles(rootDir);
                foreach (string file in files)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        long len = fi.Length;
                        File.Delete(file);
                        bytesFreed += len;
                        deleted++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                string[] subDirs = Directory.GetDirectories(rootDir);
                foreach (string sub in subDirs)
                {
                    CleanDirectoryContentsSafe(sub, ref bytesFreed, ref deleted, ref skipped);
                    try
                    {
                        if (Directory.GetFiles(sub).Length == 0 && Directory.GetDirectories(sub).Length == 0)
                            Directory.Delete(sub, false);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static long GetAvailablePhysicalRamMb()
        {
            try
            {
                NativeInterop.MEMORYSTATUSEX mem = new NativeInterop.MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(NativeInterop.MEMORYSTATUSEX));
                if (NativeInterop.GlobalMemoryStatusEx(ref mem))
                {
                    return (long)(mem.ullAvailPhys / (1024UL * 1024UL));
                }
            }
            catch { }
            return 0L;
        }
    }
}
