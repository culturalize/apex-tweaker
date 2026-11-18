using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace PCTweaker.Core
{
    public class GameBoosterEngine
    {
        public GameBoostSessionState SessionState { get; private set; }

        // Strictly safe non-essential desktop bloat executables that are safe to close before gaming.
        // Never includes game launchers (Steam, Epic, Riot, Battle.net, EA), Discord, OBS, or Anti-Cheats.
        private static readonly string[] SafeBloatProcessTargets = new string[]
        {
            "OneDrive",
            "WidgetService",
            "Widgets",
            "PhoneExperienceHost",
            "YourPhone",
            "GameBarPresenceWriter",
            "GameBarFTServer",
            "Cortana",
            "HxTsr",
            "CCXProcess",
            "AdobeNotificationClient",
            "AdobeIPCBroker",
            "AcrobatNotificationClient"
        };

        private static readonly string[] TemporaryBoostServices = new string[]
        {
            "WSearch",
            "SysMain",
            "DiagTrack"
        };

        public GameBoosterEngine()
        {
            SessionState = new GameBoostSessionState();
        }

        public GameBoostSessionState EngageBoostSession(BackupAndRollbackEngine vault)
        {
            SessionState.IsActive = true;
            SessionState.EngagedAt = DateTime.Now;
            SessionState.TerminatedProcesses.Clear();
            SessionState.StoppedServices.Clear();

            // 1. Save current power scheme & switch to High/Ultimate Performance
            SessionState.PreviousPowerSchemeGuid = TweakCatalog.GetActivePowerSchemeGuid();
            string outStr; int code;
            BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", out outStr, out code);

            // 2. Terminate safe background bloat processes
            foreach (string targetName in SafeBloatProcessTargets)
            {
                try
                {
                    Process[] found = Process.GetProcessesByName(targetName);
                    foreach (Process p in found)
                    {
                        try
                        {
                            p.Kill();
                            SessionState.TerminatedProcesses.Add(string.Format("{0}.exe (PID {1})", targetName, p.Id));
                        }
                        catch { }
                        finally
                        {
                            try { p.Dispose(); } catch { }
                        }
                    }
                }
                catch { }
            }

            // 3. Pause background indexing / prefetcher services during gaming session
            foreach (string svcName in TemporaryBoostServices)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(svcName))
                    {
                        if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                            SessionState.StoppedServices.Add(svcName);
                        }
                    }
                }
                catch { }
            }

            // 4. Purge Working Sets & Kernel Standby Page List
            MemoryPurgeResult memRes = MemoryCleanerEngine.PurgeMemory(true, true);
            SessionState.FreedRamMbOnEngage = memRes.FreedMb;

            SessionState.StatusSummary = string.Format(
                "GAME BOOST ACTIVE (Since {0:HH:mm:ss}) — Closed {1} bloat apps, paused {2} background services, freed {3} MB RAM.",
                SessionState.EngagedAt,
                SessionState.TerminatedProcesses.Count,
                SessionState.StoppedServices.Count,
                SessionState.FreedRamMbOnEngage);

            if (vault != null)
                vault.AppendAuditLog(SessionState.StatusSummary);

            return SessionState;
        }

        public GameBoostSessionState RestoreNormalSession(BackupAndRollbackEngine vault)
        {
            if (!string.IsNullOrEmpty(SessionState.PreviousPowerSchemeGuid))
            {
                string outStr; int code;
                BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive " + SessionState.PreviousPowerSchemeGuid, out outStr, out code);
            }

            foreach (string svcName in SessionState.StoppedServices)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(svcName))
                    {
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                        }
                    }
                }
                catch { }
            }

            SessionState.IsActive = false;
            SessionState.StatusSummary = "Normal Desktop Mode Restored (Power Plan & Background Services Reset).";
            if (vault != null)
                vault.AppendAuditLog(SessionState.StatusSummary);

            return SessionState;
        }

        public static bool IsGameIfeoHighPriority(string exeName)
        {
            if (string.IsNullOrEmpty(exeName)) return false;
            bool existed;
            int prio = BackupAndRollbackEngine.ReadRegistryDword(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exeName + @"\PerfOptions",
                "CpuPriorityClass",
                2,
                out existed);
            return existed && prio == 3;
        }

        public static bool SetGameIfeoHighPriority(string exeName, bool enableHighPriority, BackupAndRollbackEngine vault, out string message)
        {
            message = "";
            if (string.IsNullOrEmpty(exeName))
            {
                message = "Executable name cannot be empty.";
                return false;
            }

            string subKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exeName + @"\PerfOptions";
            string tweakId = "ifeo_" + exeName.ToLowerInvariant();
            string err;

            if (enableHighPriority)
            {
                bool ok1 = vault.SetRegistryValueWithBackup(tweakId, RegistryHive.LocalMachine, subKey, "CpuPriorityClass", 3, RegistryValueKind.DWord, out err);
                if (!ok1)
                {
                    message = err;
                    return false;
                }
                vault.SetRegistryValueWithBackup(tweakId, RegistryHive.LocalMachine, subKey, "IoPriority", 3, RegistryValueKind.DWord, out err);
                message = "Registered Kernel High CPU & I/O Priority (IFEO PerfOptions) for " + exeName + ".";
                return true;
            }
            else
            {
                bool ok1 = vault.RestoreRegistryValueFromBackupOrDefault(tweakId, RegistryHive.LocalMachine, subKey, "CpuPriorityClass", 2, RegistryValueKind.DWord, true, out err);
                vault.RestoreRegistryValueFromBackupOrDefault(tweakId, RegistryHive.LocalMachine, subKey, "IoPriority", 2, RegistryValueKind.DWord, true, out err);
                if (!ok1)
                {
                    message = err;
                    return false;
                }
                message = "Reverted Kernel Priority (IFEO PerfOptions) to normal for " + exeName + ".";
                return true;
            }
        }
    }
}
