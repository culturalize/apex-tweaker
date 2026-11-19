using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PCTweaker.Core
{
    public class TweakerController
    {
        public HardwareProfile Hardware { get; private set; }
        public BackupAndRollbackEngine Vault { get; private set; }
        public List<TweakDefinition> Tweaks { get; private set; }
        public List<DetectedGame> DetectedGames { get; private set; }
        public GameBoosterEngine Booster { get; private set; }
        public SystemTelemetryEngine Telemetry { get; private set; }
        public string LastStatusMessage { get; private set; }
        public HashSet<string> PendingRebootTweakIds { get; private set; }
        public string LastRebootTweakTitle { get; private set; }

        public event Action<string> StatusMessageChanged;
        public event Action StateChanged;

        public TweakerController()
        {
            Hardware = HardwareDetector.Detect();
            Vault = new BackupAndRollbackEngine();
            Tweaks = TweakCatalog.BuildAllTweaks();
            Booster = new GameBoosterEngine();
            Telemetry = new SystemTelemetryEngine();
            DetectedGames = new List<DetectedGame>();
            PendingRebootTweakIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LastRebootTweakTitle = "";

            AuditAllTweakStates();
            RefreshDetectedGames();
            SetStatus("System ready: " + Hardware.TierDisplayName + " (" + GetAppliedTweaksCount() + " of " + Tweaks.Count + " optimizations enabled).");
        }

        private void SetStatus(string msg)
        {
            LastStatusMessage = msg ?? "";
            if (StatusMessageChanged != null)
                StatusMessageChanged(LastStatusMessage);
        }

        private void NotifyStateChanged()
        {
            if (StateChanged != null)
                StateChanged();
        }

        public void AuditAllTweakStates()
        {
            foreach (TweakDefinition t in Tweaks)
            {
                AuditSingleTweak(t);
            }
            NotifyStateChanged();
        }

        public void AuditSingleTweak(TweakDefinition tweak)
        {
            if (tweak == null || tweak.DetectFunc == null) return;
            try
            {
                TweakEvaluation eval = tweak.DetectFunc(Hardware);
                if (eval != null)
                {
                    tweak.CurrentStatus = eval.Status;
                    tweak.LiveValueSummary = eval.LiveValueSummary;
                }
            }
            catch (Exception ex)
            {
                tweak.CurrentStatus = TweakStatus.Unknown;
                tweak.LiveValueSummary = "Audit error: " + ex.Message;
            }
        }

        public TweakDefinition FindTweakById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (TweakDefinition t in Tweaks)
            {
                if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        public TweakOperationResult ApplyTweak(string tweakId)
        {
            TweakDefinition t = FindTweakById(tweakId);
            if (t == null)
                return new TweakOperationResult(false, "Tweak not found: " + tweakId);

            if (t.RequiresAdmin && !Hardware.IsRunningAsAdmin)
            {
                string msg = "'" + t.Title + "' requires Administrator privileges. Click 'Run as Administrator' to apply machine-level tweaks.";
                SetStatus(msg);
                return new TweakOperationResult(false, msg);
            }

            try
            {
                TweakOperationResult res = t.ApplyFunc(Hardware, Vault);
                AuditSingleTweak(t);
                if (res.Success && t.RequiresReboot)
                {
                    PendingRebootTweakIds.Add(t.Id);
                    LastRebootTweakTitle = t.Title;
                }
                SetStatus(res.Message);
                NotifyStateChanged();
                return res;
            }
            catch (Exception ex)
            {
                string err = "Error applying " + t.Title + ": " + ex.Message;
                SetStatus(err);
                return new TweakOperationResult(false, err);
            }
        }

        public TweakOperationResult RevertTweak(string tweakId)
        {
            TweakDefinition t = FindTweakById(tweakId);
            if (t == null)
                return new TweakOperationResult(false, "Tweak not found: " + tweakId);

            if (t.RequiresAdmin && !Hardware.IsRunningAsAdmin)
            {
                string msg = "Reverting '" + t.Title + "' requires Administrator privileges.";
                SetStatus(msg);
                return new TweakOperationResult(false, msg);
            }

            try
            {
                TweakOperationResult res = t.RevertFunc(Hardware, Vault);
                AuditSingleTweak(t);
                if (res.Success && t.RequiresReboot)
                {
                    PendingRebootTweakIds.Add(t.Id);
                    LastRebootTweakTitle = t.Title;
                }
                SetStatus(res.Message);
                NotifyStateChanged();
                return res;
            }
            catch (Exception ex)
            {
                string err = "Error reverting " + t.Title + ": " + ex.Message;
                SetStatus(err);
                return new TweakOperationResult(false, err);
            }
        }

        public TweakOperationResult ToggleTweak(string tweakId)
        {
            TweakDefinition t = FindTweakById(tweakId);
            if (t == null)
                return new TweakOperationResult(false, "Tweak not found.");

            if (t.CurrentStatus == TweakStatus.Applied)
                return RevertTweak(tweakId);
            else
                return ApplyTweak(tweakId);
        }

        public string ApplyPreset(PresetProfile preset)
        {
            Vault.CreateNamedSnapshot("Auto-Snapshot Before " + preset.ToString(), Tweaks);

            int succeeded = 0;
            int skippedAdmin = 0;
            int failed = 0;

            foreach (TweakDefinition t in Tweaks)
            {
                bool shouldApply = false;
                switch (preset)
                {
                    case PresetProfile.PotatoRescue:
                        shouldApply = t.RecommendedForPotato;
                        break;
                    case PresetProfile.CompetitiveFps:
                        shouldApply = t.RecommendedForCompetitive;
                        break;
                    case PresetProfile.BalancedSafe:
                        shouldApply = t.IncludedInBalancedSafe && t.RiskLevel == TweakRisk.Safe;
                        break;
                }

                if (!shouldApply) continue;

                if (t.RequiresAdmin && !Hardware.IsRunningAsAdmin)
                {
                    skippedAdmin++;
                    continue;
                }

                TweakOperationResult res = t.ApplyFunc(Hardware, Vault);
                AuditSingleTweak(t);
                if (res.Success)
                {
                    succeeded++;
                    if (t.RequiresReboot)
                    {
                        PendingRebootTweakIds.Add(t.Id);
                        LastRebootTweakTitle = t.Title;
                    }
                }
                else failed++;
            }

            string summary = string.Format(
                "Preset '{0}' complete: {1} tweaks applied{2}{3}.",
                preset,
                succeeded,
                skippedAdmin > 0 ? (", " + skippedAdmin + " skipped (Admin needed)") : "",
                failed > 0 ? (", " + failed + " failed") : "");

            SetStatus(summary);
            NotifyStateChanged();
            return summary;
        }

        public string RevertAllTweaks()
        {
            int reverted = 0;
            int skippedAdmin = 0;

            foreach (TweakDefinition t in Tweaks)
            {
                if (t.CurrentStatus != TweakStatus.Applied) continue;
                if (t.RequiresAdmin && !Hardware.IsRunningAsAdmin)
                {
                    skippedAdmin++;
                    continue;
                }

                TweakOperationResult res = t.RevertFunc(Hardware, Vault);
                AuditSingleTweak(t);
                if (res.Success)
                {
                    reverted++;
                    if (t.RequiresReboot)
                    {
                        PendingRebootTweakIds.Add(t.Id);
                        LastRebootTweakTitle = t.Title;
                    }
                }
            }

            string summary = string.Format(
                "Reverted {0} active tweaks back to pre-tweak baseline / Windows defaults{1}.",
                reverted,
                skippedAdmin > 0 ? (" (" + skippedAdmin + " require Admin to revert)") : "");
            SetStatus(summary);
            NotifyStateChanged();
            return summary;
        }

        public MemoryPurgeResult PurgeMemory(bool includeStandbyList)
        {
            MemoryPurgeResult res = MemoryCleanerEngine.PurgeMemory(true, includeStandbyList);
            Vault.AppendAuditLog(res.SummaryMessage);
            SetStatus(res.SummaryMessage);
            NotifyStateChanged();
            return res;
        }

        public CacheCleanResult CleanCaches(bool includeGpuShaderCaches)
        {
            CacheCleanResult res = MemoryCleanerEngine.CleanShaderAndTempCaches(includeGpuShaderCaches);
            Vault.AppendAuditLog(res.SummaryMessage);
            SetStatus(res.SummaryMessage);
            NotifyStateChanged();
            return res;
        }

        public GameBoostSessionState ToggleGameBoostSession()
        {
            GameBoostSessionState state;
            if (!Booster.SessionState.IsActive)
                state = Booster.EngageBoostSession(Vault);
            else
                state = Booster.RestoreNormalSession(Vault);

            SetStatus(state.StatusSummary);
            NotifyStateChanged();
            return state;
        }

        public void RefreshDetectedGames()
        {
            DetectedGames = GameConfigOptimizer.DiscoverInstalledGames();
            NotifyStateChanged();
        }

        public DetectedGame AddCustomGameExecutable(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
            string exeName = Path.GetFileName(exePath);
            string title = Path.GetFileNameWithoutExtension(exePath);

            DetectedGame g = new DetectedGame
            {
                GameName = title,
                ExecutableName = exeName,
                ExecutablePath = exePath,
                EngineName = "Custom DirectX Binary",
                GameUserSettingsIniPath = "",
                HasUnrealIni = false
            };
            GameConfigOptimizer.RefreshGameStatus(g);
            DetectedGames.Add(g);
            SetStatus("Added custom game binary: " + exeName);
            NotifyStateChanged();
            return g;
        }

        public bool ToggleGameHighPriority(DetectedGame game, out string message)
        {
            if (game == null)
            {
                message = "No game selected.";
                return false;
            }
            if (!Hardware.IsRunningAsAdmin)
            {
                message = "Administrator privileges are required to register IFEO PerfOptions in HKLM.";
                SetStatus(message);
                return false;
            }

            bool targetState = !game.IsIfeoHighPriorityEnabled;
            bool ok = GameBoosterEngine.SetGameIfeoHighPriority(game.ExecutableName, targetState, Vault, out message);
            GameConfigOptimizer.RefreshGameStatus(game);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public bool ToggleGameGpuPreference(DetectedGame game, out string message)
        {
            if (game == null || string.IsNullOrEmpty(game.ExecutablePath))
            {
                message = "Select or browse the game's .exe file first to register per-app GPU preference.";
                SetStatus(message);
                return false;
            }

            bool targetState = !game.IsHighPerfGpuPreferred;
            bool ok = GameConfigOptimizer.SetExecutableDirectXGpuPreference(game.ExecutablePath, targetState, Vault, out message);
            GameConfigOptimizer.RefreshGameStatus(game);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public bool ToggleGameFullscreenOptimizations(DetectedGame game, out string message)
        {
            if (game == null || string.IsNullOrEmpty(game.ExecutablePath))
            {
                message = "Select or browse the game's .exe file first to configure Fullscreen Optimizations.";
                SetStatus(message);
                return false;
            }

            bool targetState = !game.IsFullscreenOptDisabled;
            bool ok = GameConfigOptimizer.SetExecutableFullscreenOptimizations(game.ExecutablePath, targetState, Vault, out message);
            GameConfigOptimizer.RefreshGameStatus(game);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public bool ApplyUnrealConfigPreset(DetectedGame game, bool potatoMode, out string message)
        {
            bool ok = GameConfigOptimizer.ApplyUnrealScalabilityProfile(game, potatoMode, Vault, out message);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public bool RestoreUnrealConfigBackup(DetectedGame game, out string message)
        {
            bool ok = GameConfigOptimizer.RestoreUnrealConfigBackup(game, Vault, out message);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public StateSnapshot CreateManualSnapshot(string label)
        {
            StateSnapshot snap = Vault.CreateNamedSnapshot(label, Tweaks);
            SetStatus("Created safety snapshot: " + snap.Label);
            NotifyStateChanged();
            return snap;
        }

        public bool RestoreSnapshot(string snapshotId, out string message)
        {
            StateSnapshot snap = Vault.FindSnapshotById(snapshotId);
            if (snap == null)
            {
                message = "Snapshot not found: " + snapshotId;
                SetStatus(message);
                return false;
            }

            if (string.Equals(snap.SnapshotId, "baseline", StringComparison.OrdinalIgnoreCase))
            {
                int rawRestored, rawFailed;
                Vault.ReplaySnapshotBackupItems(snap, out rawRestored, out rawFailed);
                message = RevertAllTweaks();
                return true;
            }

            int itemsRestored, itemsFailed;
            Vault.ReplaySnapshotBackupItems(snap, out itemsRestored, out itemsFailed);

            int tweaksReconciled = 0;
            int skippedAdmin = 0;

            if (snap.TweakStates != null && snap.TweakStates.Count > 0)
            {
                foreach (TweakDefinition t in Tweaks)
                {
                    AuditSingleTweak(t);
                    int targetStatusInt;
                    if (!snap.TweakStates.TryGetValue(t.Id, out targetStatusInt))
                        continue;

                    TweakStatus targetStatus = (TweakStatus)targetStatusInt;
                    if (t.CurrentStatus == targetStatus)
                        continue;

                    if (t.RequiresAdmin && !Hardware.IsRunningAsAdmin)
                    {
                        skippedAdmin++;
                        continue;
                    }

                    if (targetStatus == TweakStatus.Applied)
                    {
                        if (t.ApplyFunc(Hardware, Vault).Success)
                            tweaksReconciled++;
                    }
                    else if (targetStatus == TweakStatus.Default)
                    {
                        if (t.RevertFunc(Hardware, Vault).Success)
                            tweaksReconciled++;
                    }
                    AuditSingleTweak(t);
                }
            }

            AuditAllTweakStates();
            message = string.Format(
                "Restored snapshot '{0}': {1} backup items replayed, {2} tweak states reconciled{3}.",
                snap.Label,
                itemsRestored,
                tweaksReconciled,
                skippedAdmin > 0 ? (" (" + skippedAdmin + " skipped — Admin needed)") : "");
            Vault.AppendAuditLog(message);
            SetStatus(message);
            NotifyStateChanged();
            return true;
        }

        public bool DeleteSnapshot(string snapshotId, out string message)
        {
            bool ok = Vault.DeleteSnapshot(snapshotId, out message);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public List<StateSnapshot> GetSnapshots()
        {
            return Vault.ListSnapshots();
        }

        public bool CreateSystemRestorePoint(out string message)
        {
            bool ok = Vault.CreateWindowsSystemRestorePoint("SkyTweaks Safety Restore Point", out message);
            SetStatus(message);
            NotifyStateChanged();
            return ok;
        }

        public bool IsRebootPending
        {
            get { return PendingRebootTweakIds != null && PendingRebootTweakIds.Count > 0; }
        }

        public bool IsTweakPendingReboot(string tweakId)
        {
            return PendingRebootTweakIds != null && !string.IsNullOrEmpty(tweakId) && PendingRebootTweakIds.Contains(tweakId);
        }

        public void MarkTweakPendingReboot(TweakDefinition tweak)
        {
            if (tweak == null || !tweak.RequiresReboot) return;
            PendingRebootTweakIds.Add(tweak.Id);
            LastRebootTweakTitle = tweak.Title;
            NotifyStateChanged();
        }

        public int GetAppliedRebootTweaksCount()
        {
            int c = 0;
            foreach (TweakDefinition t in Tweaks)
            {
                if (t.RequiresReboot && t.CurrentStatus == TweakStatus.Applied) c++;
            }
            return c;
        }

        public string GetPendingRebootSummary()
        {
            List<string> titles = new List<string>();
            foreach (TweakDefinition t in Tweaks)
            {
                if (PendingRebootTweakIds.Contains(t.Id))
                    titles.Add(t.Title);
            }
            if (titles.Count == 0)
            {
                foreach (TweakDefinition t in Tweaks)
                {
                    if (t.RequiresReboot && t.CurrentStatus == TweakStatus.Applied)
                        titles.Add(t.Title);
                }
            }
            if (titles.Count == 0) return "System optimization settings";
            if (titles.Count <= 2) return string.Join(", ", titles.ToArray());
            return titles[0] + ", " + titles[1] + " (+" + (titles.Count - 2) + " more)";
        }

        public bool ScheduleSystemRestart(int delaySeconds, out string message)
        {
            try
            {
                int sec = Math.Max(0, delaySeconds);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = string.Format("/r /t {0} /c \"SkyTweaks: Restarting Windows to apply performance optimizations.\"", sec),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3000);
                }
                message = string.Format("Windows restart scheduled in {0} seconds.", sec);
                Vault.AppendAuditLog(message);
                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not initiate Windows restart: " + ex.Message;
                SetStatus(message);
                return false;
            }
        }

        public TelemetrySample SampleTelemetry()
        {
            return Telemetry.CaptureSample(Hardware, Tweaks);
        }

        public int GetAppliedTweaksCount()
        {
            int c = 0;
            foreach (TweakDefinition t in Tweaks)
            {
                if (t.CurrentStatus == TweakStatus.Applied) c++;
            }
            return c;
        }

        public bool RelaunchAsAdministrator()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("Elevation cancelled or failed: " + ex.Message);
                return false;
            }
        }

        public string GenerateDiagnosticReport()
        {
            TelemetrySample tele = SampleTelemetry();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("========================================================================");
            sb.AppendLine("SKYTWEAKS — SYSTEM HARDWARE, TELEMETRY & OPTIMIZATION REPORT");
            sb.AppendLine("========================================================================");
            sb.AppendLine("Generated At     : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Admin Privileges : " + (Hardware.IsRunningAsAdmin ? "YES (Elevated)" : "NO (Standard User)"));
            sb.AppendLine("Detected Profile : " + Hardware.TierDisplayName);
            sb.AppendLine("CPU              : " + Hardware.CpuName + " (" + Hardware.PhysicalCores + "C / " + Hardware.LogicalThreads + "T @ " + Hardware.MaxClockMhz + " MHz)");
            sb.AppendLine("GPU              : " + Hardware.GpuName + " (" + Hardware.GpuVramMb + " MB VRAM, Driver " + Hardware.GpuDriverVersion + ")");
            sb.AppendLine("HAGS Enabled     : " + Hardware.IsHagsEnabled);
            sb.AppendLine("Total RAM        : " + Hardware.TotalRamMb + " MB (Used: " + tele.UsedRamMb + " MB | Standby Cache: " + tele.StandbyCacheMb + " MB | Free: " + tele.FreeRamMb + " MB | " + tele.MemoryLoadPercent + "% Load)");
            sb.AppendLine("Primary Storage  : " + Hardware.PrimaryDiskType + (Hardware.IsLaptop ? " [Laptop Chassis]" : " [Desktop Chassis]"));
            sb.AppendLine("OS Version       : " + Hardware.OsCaption + " (Build " + Hardware.OsBuild + ")");
            sb.AppendLine("Active Power Plan: " + tele.ActivePowerPlanName);
            sb.AppendLine("Timer Resolution : " + tele.TimerResCurrentMs.ToString("F3") + " ms (Min " + tele.TimerResMinMs.ToString("F3") + " / Max " + tele.TimerResMaxMs.ToString("F3") + " ms)");
            sb.AppendLine("Processes/Handles: " + tele.ProcessCount + " procs / " + tele.ThreadCount + " threads / " + tele.HandleCount + " handles");
            sb.AppendLine("Opt. Score       : " + tele.OptimizationScorePercent + "% (" + tele.AppliedTweaksCount + "/" + tele.TotalTweaksCount + " total tweaks applied)");
            sb.AppendLine("------------------------------------------------------------------------");
            sb.AppendLine("LIVE TWEAK AUDIT (" + Tweaks.Count + " TWEAKS):");
            sb.AppendLine("------------------------------------------------------------------------");

            foreach (TweakDefinition t in Tweaks)
            {
                sb.AppendLine(string.Format("[{0,-7}] {1,-32} | {2,-48} | {3}",
                    t.CurrentStatus == TweakStatus.Applied ? "APPLIED" : "DEFAULT",
                    t.Id,
                    t.Title,
                    t.LiveValueSummary));
            }

            sb.AppendLine("------------------------------------------------------------------------");
            sb.AppendLine("AUTO-DETECTED GAME CONFIGS (" + DetectedGames.Count + "):");
            sb.AppendLine("------------------------------------------------------------------------");
            foreach (DetectedGame g in DetectedGames)
            {
                sb.AppendLine(string.Format(" - {0,-26} | Engine={1,-16} | IFEO={2,-5} | GPU={3,-5} | FSO_Off={4,-5} | Exe={5}",
                    g.GameName,
                    g.EngineName,
                    g.IsIfeoHighPriorityEnabled,
                    g.IsHighPerfGpuPreferred,
                    g.IsFullscreenOptDisabled,
                    !string.IsNullOrEmpty(g.ExecutablePath) ? g.ExecutablePath : g.GameUserSettingsIniPath));
            }

            return sb.ToString();
        }
    }
}
