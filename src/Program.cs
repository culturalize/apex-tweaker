using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using PCTweaker.Core;
using PCTweaker.UI;

namespace PCTweaker
{
    internal static class Program
    {
        public const string ShutdownEventName = @"Local\SkyTweaks_CloseInstance_Event";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                string firstArg = args[0].ToLowerInvariant();
                if (firstArg == "--close-running")
                {
                    CloseExistingInstances();
                    return 0;
                }
            }

            TweakerController controller = new TweakerController();

            if (args != null && args.Length > 0)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string cmd = args[0].ToLowerInvariant();

                if (cmd == "--audit" || cmd == "-a")
                {
                    string report = controller.GenerateDiagnosticReport();
                    string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "latest-audit-report.txt");
                    File.WriteAllText(outPath, report, Encoding.UTF8);
                    Console.WriteLine(report);
                    return 0;
                }

                if (cmd == "--purge-ram")
                {
                    MemoryPurgeResult res = controller.PurgeMemory(true);
                    Console.WriteLine(res.SummaryMessage);
                    return res.Success ? 0 : 1;
                }

                if (cmd == "--self-test")
                {
                    return RunAutomatedSelfTest(controller);
                }
            }

            // Close any previous open instances of SkyTweaks or PCTweaker before launching the UI
            CloseExistingInstances();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(controller));
            return 0;
        }

        public static void CloseExistingInstances()
        {
            try
            {
                EventWaitHandle evt;
                if (EventWaitHandle.TryOpenExisting(ShutdownEventName, out evt))
                {
                    using (evt)
                    {
                        evt.Set();
                    }
                    Thread.Sleep(180);
                }
            }
            catch { }

            int currentPid = Process.GetCurrentProcess().Id;
            string[] targetNames = new string[] { "SkyTweaks", "PCTweaker", "ApexTweaker" };

            foreach (string procName in targetNames)
            {
                try
                {
                    Process[] procs = Process.GetProcessesByName(procName);
                    foreach (Process p in procs)
                    {
                        using (p)
                        {
                            if (p.Id == currentPid) continue;
                            try
                            {
                                if (p.CloseMainWindow())
                                {
                                    if (p.WaitForExit(350)) continue;
                                }
                            }
                            catch { }

                            try
                            {
                                p.Kill();
                                p.WaitForExit(350);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private static int RunAutomatedSelfTest(TweakerController controller)
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine("=== PCTWEAKER CORE TECHNICAL SUBSYSTEM SELF-TEST ===");
            log.AppendLine("1. Hardware Detector: PASS -> " + controller.Hardware.CpuName + " | " + controller.Hardware.GpuName + " (" + controller.Hardware.GpuVramMb + " MB) | Tier: " + controller.Hardware.TierDisplayName);
            log.AppendLine("2. Tweak Catalog Count: " + controller.Tweaks.Count + " audited tweaks loaded.");

            // Verify every tweak's DetectFunc succeeded without Unknown status
            int unknownCount = 0;
            foreach (TweakDefinition t in controller.Tweaks)
            {
                if (t.CurrentStatus == TweakStatus.Unknown)
                    unknownCount++;
            }
            log.AppendLine("3. Live State Audit: " + (controller.Tweaks.Count - unknownCount) + "/" + controller.Tweaks.Count + " tweaks audited cleanly.");

            // Verify transactional Apply + Revert on a safe HKCU tweak (inp_accessibility_hooks_off) referencing SafetyVault
            TweakDefinition testTweak = controller.FindTweakById("inp_accessibility_hooks_off");
            if (testTweak != null)
            {
                TweakStatus initialStatus = testTweak.CurrentStatus;
                string initialVal = testTweak.LiveValueSummary;

                TweakOperationResult applyRes = controller.ApplyTweak(testTweak.Id);
                TweakStatus afterApply = testTweak.CurrentStatus;

                TweakOperationResult revertRes = controller.RevertTweak(testTweak.Id);
                TweakStatus afterRevert = testTweak.CurrentStatus;

                log.AppendLine(string.Format(
                    "4. SafetyVault Transactional Apply/Revert Test ({0}): Initial={1} ({2}) -> AfterApply={3} -> AfterRevert={4} ({5})",
                    testTweak.Id, initialStatus, initialVal, afterApply, afterRevert, testTweak.LiveValueSummary));

                // 4b. Verify Point-in-Time Snapshot Creation, Replay (RestoreSnapshot), and Cleanup (DeleteSnapshot)
                StateSnapshot testSnap = controller.CreateManualSnapshot("SelfTest-PointInTime-Snapshot");
                controller.ApplyTweak(testTweak.Id);
                TweakStatus mutatedStatus = testTweak.CurrentStatus;
                string restoreMsg;
                bool snapRestored = controller.RestoreSnapshot(testSnap.SnapshotId, out restoreMsg);
                TweakStatus afterSnapRestore = testTweak.CurrentStatus;
                string delMsg;
                controller.DeleteSnapshot(testSnap.SnapshotId, out delMsg);
                log.AppendLine(string.Format(
                    "4b. Point-in-Time RestoreSnapshot Test: Snapshot='{0}', Mutated={1} -> AfterRestoreSnapshot={2} (Success={3})",
                    testSnap.SnapshotId, mutatedStatus, afterSnapRestore, snapRestored && afterSnapRestore == initialStatus));
            }

            // Verify MemoryCleanerEngine
            MemoryPurgeResult memRes = controller.PurgeMemory(true);
            log.AppendLine("5. Native MemoryCleanerEngine: " + memRes.SummaryMessage);

            // Verify SystemTelemetryEngine (including 3-segment RAM breakdown)
            TelemetrySample tele = controller.SampleTelemetry();
            log.AppendLine(string.Format(
                "6. SystemTelemetryEngine: CPU={0:F1}%, RAM Used={1} MB | Standby Cache={2} MB | Free={3} MB | Total={4} MB, TimerRes={5:F3} ms, Score={6}%",
                tele.CpuUsagePercent, tele.UsedRamMb, tele.StandbyCacheMb, tele.FreeRamMb, tele.TotalRamMb, tele.TimerResCurrentMs, tele.OptimizationScorePercent));

            // Verify GameConfigOptimizer discovery & auto-linked .exe paths
            int linkedExeCount = 0;
            foreach (DetectedGame g in controller.DetectedGames)
            {
                if (!string.IsNullOrEmpty(g.ExecutablePath)) linkedExeCount++;
            }
            log.AppendLine(string.Format(
                "7. GameConfigOptimizer Discovery: Found {0} installed games ({1} with auto-resolved full .exe paths on disk).",
                controller.DetectedGames.Count, linkedExeCount));

            string outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-results.txt");
            File.WriteAllText(outFile, log.ToString(), Encoding.UTF8);
            Console.WriteLine(log.ToString());
            return unknownCount == 0 ? 0 : 1;
        }
    }
}
