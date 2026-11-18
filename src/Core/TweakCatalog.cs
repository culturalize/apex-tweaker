using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace PCTweaker.Core
{
    public static class TweakCatalog
    {
        public static List<TweakDefinition> BuildAllTweaks()
        {
            List<TweakDefinition> list = new List<TweakDefinition>();

            // =========================================================================
            // SUBSYSTEM A: CPU, SCHEDULER & POWER ARCHITECTURE (8 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "cpu_ultimate_power",
                Title = "Ultimate Performance Power Scheme",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Unlocks & activates Windows' hidden Ultimate Performance power scheme to eliminate CPU P-state frequency ramp delay.",
                TechnicalDetails = "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 & powercfg /setactive",
                WhyItHelpsGaming = "Prevents Windows from down-clocking CPU cores between frames during light scenes, eliminating micro-stutter when sudden firefights occur.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 4,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    string scheme = GetActivePowerSchemeInfo();
                    bool active = scheme.IndexOf("Ultimate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  scheme.IndexOf("High performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  scheme.IndexOf("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase) >= 0;
                    return new TweakEvaluation(active ? TweakStatus.Applied : TweakStatus.Default, "Active Plan: " + scheme);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string oldGuid = GetActivePowerSchemeGuid();
                    vault.RecordCustomStateBackup("cpu_ultimate_power", "PowerPlan", "ActiveSchemeGuid", oldGuid, "UltimateOrHighPerf");

                    string outStr;
                    int code;
                    // Try duplicating Ultimate Performance first
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", out outStr, out code);
                    string newGuid = ExtractGuidFromText(outStr);
                    if (!string.IsNullOrEmpty(newGuid))
                    {
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive " + newGuid, out outStr, out code);
                    }
                    else
                    {
                        // Fallback to High Performance GUID
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", out outStr, out code);
                    }
                    return new TweakOperationResult(true, "Activated Ultimate / High Performance power plan.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string origGuid = vault.GetRecordedCustomState("PowerPlan", "ActiveSchemeGuid", "381b4222-f694-41f0-9685-ff5bb260df2e");
                    string outStr;
                    int code;
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive " + origGuid, out outStr, out code);
                    return new TweakOperationResult(true, "Restored previous power plan (" + origGuid + ").");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_unpark_cores",
                Title = "Disable CPU Core Parking (100% Active Cores)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Forces Windows to keep all physical & logical CPU cores unparked during gameplay.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583 -> ValueMax = 0",
                WhyItHelpsGaming = "Waking a parked CPU core from deep C-state takes up to 15-40ms, which directly manifests as a dropped frame (1% low spike).",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 4,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                        "ValueMax", 100, out existed);
                    return new TweakEvaluation(val == 0 ? TweakStatus.Applied : TweakStatus.Default, "Core Park ValueMax = " + val + "%");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "cpu_unpark_cores",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                        "ValueMax", 0, RegistryValueKind.DWord, out err);
                    if (ok)
                    {
                        string o; int c;
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100", out o, out c);
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive SCHEME_CURRENT", out o, out c);
                    }
                    return new TweakOperationResult(ok, ok ? "CPU Core Parking disabled (100% cores active)." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "cpu_unpark_cores",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                        "ValueMax", 100, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored standard Windows CPU core parking." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_win32_priority",
                Title = "Win32PrioritySeparation Foreground Quantum Boost (0x26)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Tunes the Windows kernel thread scheduler for short, variable quanta with a 3:1 foreground-to-background boost.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl -> Win32PrioritySeparation = 38 (0x26)",
                WhyItHelpsGaming = "Gives the active game window 3x more CPU time slices than background daemons while keeping context switches fast for low input lag.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                        "Win32PrioritySeparation", 2, out existed);
                    return new TweakEvaluation(val == 38 ? TweakStatus.Applied : TweakStatus.Default, "Win32PrioritySeparation = 0x" + val.ToString("X2"));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "cpu_win32_priority",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                        "Win32PrioritySeparation", 38, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Win32PrioritySeparation set to 0x26 (38)." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "cpu_win32_priority",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                        "Win32PrioritySeparation", 2, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Win32PrioritySeparation restored to default (0x02)." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_mmcss_gaming",
                Title = "MMCSS Multimedia Class Scheduler Gaming Profile",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Reduces background multimedia CPU reservation to 10% and sets Games Tasks to High Scheduling & GPU Priority 8.",
                TechnicalDetails = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile (SystemResponsiveness=10) & Tasks\Games (GPU Priority=8, Priority=6)",
                WhyItHelpsGaming = "DirectX and audio engines register with MMCSS; setting GPU Priority 8 and Priority 6 prevents background threads from preempting render threads.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 4,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2;
                    int sysResp = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "SystemResponsiveness", 20, out ex1);
                    int gpuPrio = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                        "GPU Priority", 2, out ex2);
                    bool applied = (sysResp <= 10 && gpuPrio == 8);
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("SystemResponsiveness={0}%, GPU Priority={1}", sysResp, gpuPrio));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    string baseKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                    string gamesKey = baseKey + @"\Tasks\Games";
                    if (!vault.SetRegistryValueWithBackup("cpu_mmcss_gaming", RegistryHive.LocalMachine, baseKey, "SystemResponsiveness", 10, RegistryValueKind.DWord, out err))
                        return new TweakOperationResult(false, err);
                    vault.SetRegistryValueWithBackup("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "GPU Priority", 8, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "Priority", 6, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "Scheduling Category", "High", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "SFIO Priority", "High", RegistryValueKind.String, out err);
                    return new TweakOperationResult(true, "MMCSS Gaming Profile optimized (SystemResponsiveness=10%, GPU Priority=8).");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    string baseKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                    string gamesKey = baseKey + @"\Tasks\Games";
                    vault.RestoreRegistryValueFromBackupOrDefault("cpu_mmcss_gaming", RegistryHive.LocalMachine, baseKey, "SystemResponsiveness", 20, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "GPU Priority", 8, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "Priority", 2, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "Scheduling Category", "Medium", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("cpu_mmcss_gaming", RegistryHive.LocalMachine, gamesKey, "SFIO Priority", "Normal", RegistryValueKind.String, false, out err);
                    return new TweakOperationResult(true, "Restored default MMCSS Multimedia Scheduler settings.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_power_throttling_off",
                Title = "Disable Windows Executive Power Throttling (EcoQoS)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Prevents Windows EcoQoS from throttling clock speeds on game worker threads and audio/Discord pipelines.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling -> PowerThrottlingOff = 1",
                WhyItHelpsGaming = "Ensures background asset decompression threads and voice chat never get trapped on low-frequency efficiency states.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                        "PowerThrottlingOff", 0, out existed);
                    return new TweakEvaluation((existed && val == 1) ? TweakStatus.Applied : TweakStatus.Default,
                        "PowerThrottlingOff = " + (existed ? val.ToString() : "0 (Default)"));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "cpu_power_throttling_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                        "PowerThrottlingOff", 1, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled Executive Power Throttling." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "cpu_power_throttling_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                        "PowerThrottlingOff", 0, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Re-enabled default Windows Power Throttling." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_dynamic_tick_off",
                Title = "Disable Kernel Dynamic Tick (Consistent Interrupt Timing)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Stops the Windows kernel from stopping and restarting the system timer tick during brief CPU idle gaps.",
                TechnicalDetails = "bcdedit /set disabledynamictick yes",
                WhyItHelpsGaming = "Dynamic tick power-saving introduces timer wakeup jitter that degrades 0.1% lows and high-polling mouse consistency.",
                RecommendedForPotato = false,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 2,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    string outStr; int code;
                    if (BackupAndRollbackEngine.RunCommandHidden("bcdedit.exe", "/enum {current}", out outStr, out code))
                    {
                        bool yes = outStr.IndexOf("disabledynamictick", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   outStr.IndexOf("Yes", StringComparison.OrdinalIgnoreCase) >= 0;
                        return new TweakEvaluation(yes ? TweakStatus.Applied : TweakStatus.Default,
                            yes ? "disabledynamictick = Yes" : "disabledynamictick = No (Default)");
                    }
                    return new TweakEvaluation(TweakStatus.Default, "Requires Admin to query BCD");
                },
                ApplyFunc = (hw, vault) =>
                {
                    if (!hw.IsRunningAsAdmin)
                        return new TweakOperationResult(false, "Administrator privileges required for bcdedit.");
                    string outStr; int code;
                    bool ok = BackupAndRollbackEngine.RunCommandHidden("bcdedit.exe", "/set disabledynamictick yes", out outStr, out code);
                    if (ok) vault.RecordCustomStateBackup("cpu_dynamic_tick_off", "BCD", "disabledynamictick", "no", "yes");
                    return new TweakOperationResult(ok, ok ? "Disabled kernel dynamic tick (takes full effect on next reboot)." : outStr, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    if (!hw.IsRunningAsAdmin)
                        return new TweakOperationResult(false, "Administrator privileges required for bcdedit.");
                    string outStr; int code;
                    bool ok = BackupAndRollbackEngine.RunCommandHidden("bcdedit.exe", "/deletevalue disabledynamictick", out outStr, out code);
                    if (!ok) ok = BackupAndRollbackEngine.RunCommandHidden("bcdedit.exe", "/set disabledynamictick no", out outStr, out code);
                    return new TweakOperationResult(ok, "Restored default kernel dynamic tick behavior.", true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_pcie_aspm_off",
                Title = "Disable PCIe Link State Power Management (ASPM)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Keeps the PCIe GPU bus in active L0 state so VRAM texture transfers never wait on PCIe bus wake-up latency.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5 + powercfg ASPM 0",
                WhyItHelpsGaming = "Eliminates PCIe bus power-saving transitions that cause frametime hitching when streaming textures to the GPU.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5",
                        "Attributes", 1, out existed);
                    return new TweakEvaluation(val == 2 ? TweakStatus.Applied : TweakStatus.Default,
                        val == 2 ? "PCIe ASPM Disabled (L0 Always Active)" : "PCIe ASPM Default");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "cpu_pcie_aspm_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5",
                        "Attributes", 2, RegistryValueKind.DWord, out err);
                    string o; int c;
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0", out o, out c);
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive SCHEME_CURRENT", out o, out c);
                    return new TweakOperationResult(ok, ok ? "Disabled PCIe Link State Power Management (ASPM=0)." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "cpu_pcie_aspm_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5",
                        "Attributes", 1, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default PCIe Link State Power Management." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "cpu_usb_selective_suspend_off",
                Title = "Disable USB Selective Suspend (Zero Peripheral Dropouts)",
                Category = TweakCategory.CpuAndPower,
                ShortSummary = "Prevents Windows from putting USB root hubs into low-power suspend states while gaming.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\USB -> DisableSelectiveSuspend = 1",
                WhyItHelpsGaming = "Stops high-polling-rate gaming mice (1000Hz-8000Hz), keyboards, and USB DAC headsets from hitching or dropping polls.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\USB",
                        "DisableSelectiveSuspend", 0, out existed);
                    return new TweakEvaluation((existed && val == 1) ? TweakStatus.Applied : TweakStatus.Default,
                        "DisableSelectiveSuspend = " + (existed ? val.ToString() : "0"));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "cpu_usb_selective_suspend_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\USB",
                        "DisableSelectiveSuspend", 1, RegistryValueKind.DWord, out err);
                    string o; int c;
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0", out o, out c);
                    BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/setactive SCHEME_CURRENT", out o, out c);
                    return new TweakOperationResult(ok, ok ? "Disabled USB Selective Suspend on AC power." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "cpu_usb_selective_suspend_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\USB",
                        "DisableSelectiveSuspend", 0, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Re-enabled default USB Selective Suspend." : err);
                }
            });

            // =========================================================================
            // SUBSYSTEM B: GPU, DWM, DXGI & FRAME PIPELINE (8 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "gpu_gamedvr_off",
                Title = "Disable GameDVR & Background Frame Capture Pipeline",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Disables Windows GameDVR background video encoder ring buffers that siphon GPU compute & VRAM.",
                TechnicalDetails = @"HKCU\System\GameConfigStore (GameDVR_Enabled=0) & HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR (AppCaptureEnabled=0)",
                WhyItHelpsGaming = "Reclaims 3-8% GPU utilization and up to 250 MB of VRAM on low-end GPUs by stopping continuous background frame encoding.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 5,
                ImpactLatency = 4,
                ImpactRam = 3,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2;
                    int dvr = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1, out ex1);
                    int cap = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, out ex2);
                    bool applied = (dvr == 0 && cap == 0);
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("GameDVR_Enabled={0}, AppCaptureEnabled={1}", dvr, cap));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    vault.SetRegistryValueWithBackup("gpu_gamedvr_off", RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_gamedvr_off", RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Disabled GameDVR and AppCapture background recording pipeline.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamedvr_off", RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamedvr_off", RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(true, "Restored Windows GameDVR and AppCapture settings.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_fso_global",
                Title = "Optimize DXGI Fullscreen Exclusive Presentation Mode",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Configures GameConfigStore to honor true Exclusive Fullscreen (FSEBehaviorMode=2) without DWM proxy overhead.",
                TechnicalDetails = @"HKCU\System\GameConfigStore -> GameDVR_FSEBehaviorMode=2, GameDVR_HonorUserFSEBehaviorMode=1, GameDVR_DXGIHonorFSEWindowsCompatible=1",
                WhyItHelpsGaming = "Lets fullscreen games scan out directly to the display monitor without DWM compositor buffering, reducing display latency.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2;
                    int fse = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 0, out ex1);
                    int honor = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 0, out ex2);
                    bool applied = (fse == 2 && honor == 1);
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("FSEBehaviorMode={0}, HonorUserFSE={1}", fse, honor));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"System\GameConfigStore";
                    vault.SetRegistryValueWithBackup("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Configured DXGI Exclusive Fullscreen presentation mode.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"System\GameConfigStore";
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_FSEBehaviorMode", 0, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_HonorUserFSEBehaviorMode", 0, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_DXGIHonorFSEWindowsCompatible", 0, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_fso_global", RegistryHive.CurrentUser, key, "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(true, "Restored default Windows Fullscreen Optimizations.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_windowed_flip",
                Title = "Enable DX10/DX11 Windowed Flip Model & VRR Upgrade",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Upgrades legacy DirectX 10/11 borderless-windowed games to modern zero-copy DXGI Independent Flip presentation.",
                TechnicalDetails = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences -> DirectXUserGlobalSettings = VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;",
                WhyItHelpsGaming = "Eliminates 1-2 full frames (8-33ms) of DWM desktop compositor lag in borderless windowed games.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    string val = BackupAndRollbackEngine.ReadRegistryString(
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\DirectX\UserGpuPreferences",
                        "DirectXUserGlobalSettings", "", out existed);
                    bool applied = val.IndexOf("SwapEffectUpgradeEnable=1", StringComparison.OrdinalIgnoreCase) >= 0;
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.IsNullOrEmpty(val) ? "Default (No Flip Upgrade)" : val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "gpu_windowed_flip",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\DirectX\UserGpuPreferences",
                        "DirectXUserGlobalSettings",
                        "VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;",
                        RegistryValueKind.String,
                        out err);
                    return new TweakOperationResult(ok, ok ? "Enabled DX10/DX11 Flip Model & VRR Windowed Optimizations." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "gpu_windowed_flip",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\DirectX\UserGpuPreferences",
                        "DirectXUserGlobalSettings",
                        "",
                        RegistryValueKind.String,
                        true,
                        out err);
                    return new TweakOperationResult(ok, ok ? "Reverted Windowed Games Flip Model setting." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_hags_toggle",
                Title = "Enable Hardware-Accelerated GPU Scheduling (HAGS)",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Offloads high-frequency VRAM memory & command packet scheduling from the CPU to the GPU hardware scheduler.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers -> HwSchMode = 2",
                WhyItHelpsGaming = "Reduces CPU draw-call submission overhead on CPU-bound systems and enables NVIDIA Frame Generation / Reflex latency pipelines.",
                RecommendedForPotato = false,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 1, out existed);
                    return new TweakEvaluation(val == 2 ? TweakStatus.Applied : TweakStatus.Default,
                        val == 2 ? "HwSchMode = 2 (Enabled)" : "HwSchMode = " + val + " (Disabled)");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "gpu_hags_toggle",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 2, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Enabled Hardware-Accelerated GPU Scheduling (reboot required)." : err, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "gpu_hags_toggle",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 1, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored HwSchMode to previous setting." : err, true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_dwm_transparency_off",
                Title = "Disable DWM Acrylic & Mica Transparency (Potato VRAM Saver)",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Turns off Windows desktop glass/blur shaders that allocate multi-pass Gaussian blur buffers in VRAM.",
                TechnicalDetails = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize -> EnableTransparency = 0",
                WhyItHelpsGaming = "Frees 150-300 MB of dedicated VRAM and stops background DWM shader passes on 2GB/4GB GPUs and integrated graphics.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = false,
                IncludedInBalancedSafe = false,
                ImpactFps = 4,
                ImpactLatency = 2,
                ImpactRam = 4,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", 1, out existed);
                    return new TweakEvaluation(val == 0 ? TweakStatus.Applied : TweakStatus.Default,
                        val == 0 ? "EnableTransparency = 0 (VRAM Saved)" : "EnableTransparency = 1 (Blur Active)");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "gpu_dwm_transparency_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled DWM Acrylic/Mica transparency buffers." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "gpu_dwm_transparency_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", 1, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Re-enabled Windows desktop transparency." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_visual_fx_perf",
                Title = "Desktop Visual Effects & Window Animation Performance Mode",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Disables window minimize/maximize animations and taskbar animation overhead while preserving crisp font smoothing.",
                TechnicalDetails = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects (VisualFXSetting=2) & WindowMetrics (MinAnimate=0)",
                WhyItHelpsGaming = "Speeds up Alt-Tab transitions and removes desktop compositor animation hooks on low-spec systems.",
                RecommendedForPotato = true,
                RecommendedForMidRange = false,
                RecommendedForCompetitive = false,
                IncludedInBalancedSafe = false,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 3,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    string minAnim = BackupAndRollbackEngine.ReadRegistryString(
                        RegistryHive.CurrentUser,
                        @"Control Panel\Desktop\WindowMetrics",
                        "MinAnimate", "1", out existed);
                    return new TweakEvaluation(minAnim == "0" ? TweakStatus.Applied : TweakStatus.Default,
                        "MinAnimate = " + minAnim);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    vault.SetRegistryValueWithBackup("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Disabled window & taskbar animations while keeping ClearType font smoothing.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "1", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 1, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_visual_fx_perf", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(true, "Restored default Windows animations and visual effects.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_mpo_fix",
                Title = "Multi-Plane Overlay (MPO) Stutter & Flicker Fix",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Disables hardware DWM Multi-Plane Overlay promotion that triggers micro-stutters on dual-monitor or borderless setups.",
                TechnicalDetails = @"HKLM\SOFTWARE\Microsoft\Windows\Dwm -> OverlayTestMode = 5",
                WhyItHelpsGaming = "Prevents GPU display controller plane promotion glitches when Discord, browsers, or OBS run alongside a game.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 2,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows\Dwm",
                        "OverlayTestMode", 0, out existed);
                    return new TweakEvaluation((existed && val == 5) ? TweakStatus.Applied : TweakStatus.Default,
                        existed ? ("OverlayTestMode = " + val) : "MPO Default (Enabled)");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "gpu_mpo_fix",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows\Dwm",
                        "OverlayTestMode", 5, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Applied Multi-Plane Overlay (MPO) stutter fix (OverlayTestMode=5)." : err, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "gpu_mpo_fix",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows\Dwm",
                        "OverlayTestMode", 0, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Re-enabled default DWM Multi-Plane Overlay." : err, true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gpu_gamebar_presence_off",
                Title = "Keep Windows Game Mode ON While Disabling GameBar Overlay Bloat",
                Category = TweakCategory.GpuAndDisplay,
                ShortSummary = "Forces Windows Auto Game Mode ON for thread scheduling while disabling Xbox GameBar controller hooks & notifications.",
                TechnicalDetails = @"HKCU\Software\Microsoft\GameBar -> AutoGameModeEnabled=1, AllowAutoGameMode=1, UseNexusForGameBarEnabled=0, ShowStartupPanel=0",
                WhyItHelpsGaming = "Retains Windows Game Mode's background Windows Update suppression without running the heavy Xbox GameBar widget overlay.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 3,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2;
                    int gm = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, out ex1);
                    int nexus = BackupAndRollbackEngine.ReadRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 1, out ex2);
                    bool applied = (gm == 1 && ex2 && nexus == 0);
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("AutoGameMode={0}, GameBarOverlay={1}", gm, nexus));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"Software\Microsoft\GameBar";
                    vault.SetRegistryValueWithBackup("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "AutoGameModeEnabled", 1, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "AllowAutoGameMode", 1, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "ShowStartupPanel", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Enabled Windows Game Mode and disabled Xbox GameBar overlay triggers.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"Software\Microsoft\GameBar";
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "AutoGameModeEnabled", 1, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "AllowAutoGameMode", 1, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "UseNexusForGameBarEnabled", 1, RegistryValueKind.DWord, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("gpu_gamebar_presence_off", RegistryHive.CurrentUser, key, "ShowStartupPanel", 1, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(true, "Restored default Xbox GameBar settings.");
                }
            });

            // =========================================================================
            // SUBSYSTEM C: MEMORY, WORKING SET & STORAGE I/O (6 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "mem_svchost_consolidation",
                Title = "Consolidate svchost.exe Split Threshold to Physical RAM",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Sets SvcHostSplitThresholdInKB to your total physical RAM so Windows merges 70+ isolated svchost processes.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control -> SvcHostSplitThresholdInKB = <TotalRAM_KB + 1024000>",
                WhyItHelpsGaming = "Reclaims 250-450 MB of duplicated page-table and heap overhead across dozens of split service processes on 8GB/16GB PCs.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 2,
                ImpactRam = 5,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control",
                        "SvcHostSplitThresholdInKB", 3670016, out existed);
                    int target = (int)Math.Min(int.MaxValue, hw.TotalRamKb + 1024000L);
                    bool applied = val >= hw.TotalRamKb;
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("Threshold={0} KB (RAM={1} KB)", val, hw.TotalRamKb));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    int target = (int)Math.Min(int.MaxValue, hw.TotalRamKb + 1024000L);
                    bool ok = vault.SetRegistryValueWithBackup(
                        "mem_svchost_consolidation",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control",
                        "SvcHostSplitThresholdInKB", target, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? ("SvcHostSplitThresholdInKB set to " + target + " KB.") : err, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "mem_svchost_consolidation",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control",
                        "SvcHostSplitThresholdInKB", 3670016, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default SvcHostSplitThresholdInKB (3.5 GB)." : err, true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "mem_disable_paging_exec",
                Title = "Keep Windows Kernel & Drivers Locked in Physical RAM",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Sets DisablePagingExecutive=1 so core kernel and GPU/audio driver code is never paged out to the disk swap file.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management -> DisablePagingExecutive = 1",
                WhyItHelpsGaming = "Eliminates hard page-fault stalls when a game triggers a kernel or driver interrupt after heavy memory allocation.",
                RecommendedForPotato = false,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 4,
                ImpactRam = 2,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "DisablePagingExecutive", 0, out existed);
                    return new TweakEvaluation(val == 1 ? TweakStatus.Applied : TweakStatus.Default,
                        "DisablePagingExecutive = " + val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "mem_disable_paging_exec",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "DisablePagingExecutive", 1, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Locked kernel & driver executive pages in physical RAM." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "mem_disable_paging_exec",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                        "DisablePagingExecutive", 0, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default DisablePagingExecutive=0." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "mem_sysmain_off",
                Title = "Disable SysMain (Superfetch) Background RAM & Disk Preloader",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Stops Windows SysMain from aggressively filling Standby RAM and thrashing disk I/O with predictive app preloads.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\SysMain -> Start = 4 (Disabled)",
                WhyItHelpsGaming = "On SSDs and low-RAM systems, SysMain competes with game asset streaming and bloats the Standby Page List.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 5,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    int start = BackupAndRollbackEngine.GetServiceStartMode("SysMain");
                    return new TweakEvaluation(start == 4 ? TweakStatus.Applied : TweakStatus.Default,
                        "SysMain Start Mode = " + FormatServiceStartMode(start));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetServiceStartModeWithBackup("mem_sysmain_off", "SysMain", 4, true, out err);
                    return new TweakOperationResult(ok, ok ? "Stopped and disabled SysMain (Superfetch) service." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreServiceStartMode("mem_sysmain_off", "SysMain", 2, out err);
                    return new TweakOperationResult(ok, ok ? "Restored SysMain (Superfetch) service to Automatic." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "mem_ntfs_lastaccess_off",
                Title = "Disable NTFS Last-Access Timestamp Write IOPS",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Stops NTFS from writing a timestamp update to disk every time a game reads a texture, audio, or shader file.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\FileSystem -> NtfsDisableLastAccessUpdate = 0x80000001 (-2147483647)",
                WhyItHelpsGaming = "Eliminates thousands of unnecessary write IOPS during open-world level streaming and shader compilation.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 3,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisableLastAccessUpdate", 0, out existed);
                    bool applied = (val == 1 || val == unchecked((int)0x80000001) || val == unchecked((int)0x80000003));
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        "NtfsDisableLastAccessUpdate = 0x" + val.ToString("X8"));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "mem_ntfs_lastaccess_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisableLastAccessUpdate", unchecked((int)0x80000001), RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled NTFS Last-Access timestamp writes." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "mem_ntfs_lastaccess_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisableLastAccessUpdate", unchecked((int)0x80000002), RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default NTFS Last-Access timestamp policy." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "mem_ntfs_8dot3_off",
                Title = "Disable Legacy DOS 8.3 Short Filename Generation",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Stops NTFS from computing and storing legacy MS-DOS 8.3 alias filenames when games create shader cache files.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\FileSystem -> NtfsDisable8dot3NameCreation = 1",
                WhyItHelpsGaming = "Speeds up directory lookups and shader cache generation in folders containing tens of thousands of asset files.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 2,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisable8dot3NameCreation", 2, out existed);
                    return new TweakEvaluation(val == 1 ? TweakStatus.Applied : TweakStatus.Default,
                        "NtfsDisable8dot3NameCreation = " + val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "mem_ntfs_8dot3_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisable8dot3NameCreation", 1, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled legacy DOS 8.3 short filename creation." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "mem_ntfs_8dot3_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem",
                        "NtfsDisable8dot3NameCreation", 2, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default NTFS 8.3 filename policy." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "mem_prefetcher_game_opt",
                Title = "Disable Legacy Disk Prefetcher Trace Monitoring on SSDs",
                Category = TweakCategory.MemoryAndStorage,
                ShortSummary = "Disables .pf file trace monitoring during application startup on fast NVMe/SATA SSDs.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters -> EnablePrefetcher = 0",
                WhyItHelpsGaming = "Prevents Windows from recording and compacting prefetch trace files during the first 60 seconds of a game launch.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 2,
                ImpactLatency = 2,
                ImpactRam = 3,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher", 3, out existed);
                    return new TweakEvaluation(val == 0 ? TweakStatus.Applied : TweakStatus.Default,
                        "EnablePrefetcher = " + val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "mem_prefetcher_game_opt",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled legacy application prefetcher overhead." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "mem_prefetcher_game_opt",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                        "EnablePrefetcher", 3, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored EnablePrefetcher=3." : err);
                }
            });

            // =========================================================================
            // SUBSYSTEM D: NETWORK & PACKET LATENCY (5 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "net_throttling_off",
                Title = "Disable Multimedia Network Packet Throttling (0xFFFFFFFF)",
                Category = TweakCategory.NetworkAndPing,
                ShortSummary = "Disables Windows' built-in 10 packets/ms network throttling cap when audio/multimedia streams are active.",
                TechnicalDetails = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile -> NetworkThrottlingIndex = 0xFFFFFFFF",
                WhyItHelpsGaming = "By default, Windows throttles non-multimedia network processing to 10 packets/ms while game audio plays; removing it cuts ping jitter.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "NetworkThrottlingIndex", 10, out existed);
                    return new TweakEvaluation(val == -1 ? TweakStatus.Applied : TweakStatus.Default,
                        val == -1 ? "NetworkThrottlingIndex = 0xFFFFFFFF (Unthrottled)" : ("NetworkThrottlingIndex = " + val + " pkts/ms"));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "net_throttling_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled Multimedia Network Throttling (0xFFFFFFFF)." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "net_throttling_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                        "NetworkThrottlingIndex", 10, RegistryValueKind.DWord, false, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default NetworkThrottlingIndex (10)." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "net_nagle_off",
                Title = "Disable Nagle's Algorithm (TcpAckFrequency=1 & TCPNoDelay=1)",
                Category = TweakCategory.NetworkAndPing,
                ShortSummary = "Forces active network adapters to transmit small TCP game packets immediately without 200ms ACK batching.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{NIC_GUID} -> TcpAckFrequency=1, TCPNoDelay=1",
                WhyItHelpsGaming = "Eliminates delayed-ACK packet buffering in MMOs, ARPGs, and game matchmakers.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    int activeNics = 0;
                    int tunedNics = 0;
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        using (RegistryKey ifs = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", false))
                        {
                            if (ifs != null)
                            {
                                foreach (string guid in ifs.GetSubKeyNames())
                                {
                                    using (RegistryKey nic = ifs.OpenSubKey(guid, false))
                                    {
                                        if (nic == null) continue;
                                        object ip = nic.GetValue("DhcpIPAddress") ?? nic.GetValue("IPAddress");
                                        if (ip == null) continue;
                                        activeNics++;
                                        object ack = nic.GetValue("TcpAckFrequency");
                                        object nodelay = nic.GetValue("TCPNoDelay");
                                        if (ack != null && Convert.ToInt32(ack) == 1 && nodelay != null && Convert.ToInt32(nodelay) == 1)
                                            tunedNics++;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    bool applied = (activeNics > 0 && tunedNics == activeNics);
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("{0}/{1} active NICs unbuffered", tunedNics, activeNics));
                },
                ApplyFunc = (hw, vault) =>
                {
                    int count = 0;
                    string lastErr = null;
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        using (RegistryKey ifs = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", false))
                        {
                            if (ifs != null)
                            {
                                foreach (string guid in ifs.GetSubKeyNames())
                                {
                                    string subPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + guid;
                                    using (RegistryKey nic = ifs.OpenSubKey(guid, false))
                                    {
                                        if (nic == null) continue;
                                        object ip = nic.GetValue("DhcpIPAddress") ?? nic.GetValue("IPAddress");
                                        if (ip == null) continue;

                                        if (vault.SetRegistryValueWithBackup("net_nagle_off", RegistryHive.LocalMachine, subPath, "TcpAckFrequency", 1, RegistryValueKind.DWord, out lastErr) &&
                                            vault.SetRegistryValueWithBackup("net_nagle_off", RegistryHive.LocalMachine, subPath, "TCPNoDelay", 1, RegistryValueKind.DWord, out lastErr))
                                        {
                                            count++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { lastErr = ex.Message; }
                    if (count == 0 && !string.IsNullOrEmpty(lastErr))
                        return new TweakOperationResult(false, lastErr);
                    return new TweakOperationResult(true, "Disabled Nagle's Algorithm on " + count + " active network adapter(s).");
                },
                RevertFunc = (hw, vault) =>
                {
                    string lastErr = null;
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        using (RegistryKey ifs = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", false))
                        {
                            if (ifs != null)
                            {
                                foreach (string guid in ifs.GetSubKeyNames())
                                {
                                    string subPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + guid;
                                    vault.RestoreRegistryValueFromBackupOrDefault("net_nagle_off", RegistryHive.LocalMachine, subPath, "TcpAckFrequency", 2, RegistryValueKind.DWord, true, out lastErr);
                                    vault.RestoreRegistryValueFromBackupOrDefault("net_nagle_off", RegistryHive.LocalMachine, subPath, "TCPNoDelay", 0, RegistryValueKind.DWord, true, out lastErr);
                                }
                            }
                        }
                    }
                    catch (Exception ex) { lastErr = ex.Message; }
                    return new TweakOperationResult(true, "Restored default TCP ACK batching across all network adapters.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "net_qos_bandwidth_unlock",
                Title = "Release QoS Packet Scheduler Reserved Bandwidth (NonBestEffortLimit=0)",
                Category = TweakCategory.NetworkAndPing,
                ShortSummary = "Sets Windows Psched NonBestEffortLimit to 0% so QoS never reserves bandwidth away from games.",
                TechnicalDetails = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched -> NonBestEffortLimit = 0",
                WhyItHelpsGaming = "Prevents Windows QoS policies from throttling peak packet throughput during downloads or high-tickrate multiplayer.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 3,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\Psched",
                        "NonBestEffortLimit", 80, out existed);
                    return new TweakEvaluation((existed && val == 0) ? TweakStatus.Applied : TweakStatus.Default,
                        existed ? ("NonBestEffortLimit = " + val + "%") : "Default (20% Reserved)");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "net_qos_bandwidth_unlock",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\Psched",
                        "NonBestEffortLimit", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Unlocked 100% QoS packet scheduler bandwidth (NonBestEffortLimit=0)." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "net_qos_bandwidth_unlock",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\Psched",
                        "NonBestEffortLimit", 80, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default QoS packet scheduler reservation." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "net_tcp_rss_lowlatency",
                Title = "Enable Multi-Core NIC Receive Side Scaling (RSS) & Disable RSC Delay",
                Category = TweakCategory.NetworkAndPing,
                ShortSummary = "Spreads incoming network interrupts across CPU cores (RSS) while disabling Receive Segment Coalescing latency.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters -> EnableRSS = 1 + netsh int tcp set global rss=enabled rsc=disabled",
                WhyItHelpsGaming = "Prevents all network interrupts from bottlenecking Core 0 while avoiding RSC packet coalescing delay.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 2,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                        "EnableRSS", 0, out existed);
                    return new TweakEvaluation((existed && val == 1) ? TweakStatus.Applied : TweakStatus.Default,
                        existed && val == 1 ? "EnableRSS=1, RSC=Disabled" : "TCP Stack Default");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "net_tcp_rss_lowlatency",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                        "EnableRSS", 1, RegistryValueKind.DWord, out err);
                    if (ok)
                    {
                        string o; int c;
                        BackupAndRollbackEngine.RunCommandHidden("netsh.exe", "int tcp set global rss=enabled", out o, out c);
                        BackupAndRollbackEngine.RunCommandHidden("netsh.exe", "int tcp set global rsc=disabled", out o, out c);
                    }
                    return new TweakOperationResult(ok, ok ? "Enabled multi-core TCP RSS and disabled RSC packet coalescing." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "net_tcp_rss_lowlatency",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                        "EnableRSS", 0, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default TCP RSS/RSC configuration." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "net_delivery_opt_p2p_off",
                Title = "Disable Windows Update P2P Background Upload Leeching",
                Category = TweakCategory.NetworkAndPing,
                ShortSummary = "Stops Windows Delivery Optimization from silently using your upload bandwidth to seed updates to other PCs.",
                TechnicalDetails = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization -> DODownloadMode = 0",
                WhyItHelpsGaming = "Eliminates sudden mid-game ping spikes (20ms -> 300ms) caused by background peer-to-peer update seeding.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 5,
                ImpactRam = 2,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                        "DODownloadMode", 1, out existed);
                    return new TweakEvaluation((existed && val == 0) ? TweakStatus.Applied : TweakStatus.Default,
                        existed && val == 0 ? "DODownloadMode = 0 (P2P Off)" : "P2P Delivery Optimization Active");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "net_delivery_opt_p2p_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                        "DODownloadMode", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled Windows Update P2P Delivery Optimization." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "net_delivery_opt_p2p_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                        "DODownloadMode", 1, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Re-enabled default Windows Delivery Optimization." : err);
                }
            });

            // =========================================================================
            // SUBSYSTEM E: INPUT LATENCY & PERIPHERAL PIPELINE (4 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "inp_raw_mouse_1to1",
                Title = "True 1:1 Linear Mouse Aim (Disable Enhance Pointer Precision)",
                Category = TweakCategory.InputAndMouse,
                ShortSummary = "Zeros MouseSpeed, MouseThreshold1, and MouseThreshold2 so cursor movement is 100% proportional to physical hand speed.",
                TechnicalDetails = @"HKCU\Control Panel\Mouse -> MouseSpeed=0, MouseThreshold1=0, MouseThreshold2=0",
                WhyItHelpsGaming = "Removes Windows pointer acceleration curves that corrupt muscle memory in shooters and menus.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 5,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2, ex3;
                    string s = BackupAndRollbackEngine.ReadRegistryString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1", out ex1);
                    string t1 = BackupAndRollbackEngine.ReadRegistryString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6", out ex2);
                    string t2 = BackupAndRollbackEngine.ReadRegistryString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10", out ex3);
                    bool applied = (s == "0" && t1 == "0" && t2 == "0");
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("Speed={0}, Thresholds={1}/{2}", s, t1, t2));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"Control Panel\Mouse";
                    vault.SetRegistryValueWithBackup("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseSpeed", "0", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseThreshold1", "0", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseThreshold2", "0", RegistryValueKind.String, out err);
                    return new TweakOperationResult(true, "Enabled true 1:1 linear mouse response (Pointer Precision disabled).");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    string key = @"Control Panel\Mouse";
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseSpeed", "1", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseThreshold1", "6", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_raw_mouse_1to1", RegistryHive.CurrentUser, key, "MouseThreshold2", "10", RegistryValueKind.String, false, out err);
                    return new TweakOperationResult(true, "Restored default Windows pointer acceleration settings.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "inp_mouse_queue_size",
                Title = "Optimize Kernel Mouse Class Input Queue (MouseDataQueueSize=32)",
                Category = TweakCategory.InputAndMouse,
                ShortSummary = "Tunes mouclass kernel ring buffer size to 32 packets—tight enough for minimum queue latency while safe for 1000-4000Hz mice.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\mouclass\Parameters -> MouseDataQueueSize = 32 (0x20)",
                WhyItHelpsGaming = "Prevents excessive input packet buffering during 100% CPU load spikes without risking dropped mouse packets.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 1,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters",
                        "MouseDataQueueSize", 100, out existed);
                    return new TweakEvaluation((existed && val == 32) ? TweakStatus.Applied : TweakStatus.Default,
                        "MouseDataQueueSize = " + val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "inp_mouse_queue_size",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters",
                        "MouseDataQueueSize", 32, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Set MouseDataQueueSize to 32 packets." : err, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "inp_mouse_queue_size",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters",
                        "MouseDataQueueSize", 100, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default MouseDataQueueSize (100)." : err, true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "inp_kbd_queue_size",
                Title = "Optimize Kernel Keyboard Class Input Queue (KeyboardDataQueueSize=32)",
                Category = TweakCategory.InputAndMouse,
                ShortSummary = "Tunes kbdclass kernel ring buffer size to 32 packets for crisp counter-strafing and movement registration.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters -> KeyboardDataQueueSize = 32 (0x20)",
                WhyItHelpsGaming = "Minimizes keyboard input buffer depth when CPU cores are saturated by game physics and rendering.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 1,
                ImpactLatency = 4,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = true,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters",
                        "KeyboardDataQueueSize", 100, out existed);
                    return new TweakEvaluation((existed && val == 32) ? TweakStatus.Applied : TweakStatus.Default,
                        "KeyboardDataQueueSize = " + val);
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "inp_kbd_queue_size",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters",
                        "KeyboardDataQueueSize", 32, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Set KeyboardDataQueueSize to 32 packets." : err, true);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "inp_kbd_queue_size",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters",
                        "KeyboardDataQueueSize", 100, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Restored default KeyboardDataQueueSize (100)." : err, true);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "inp_accessibility_hooks_off",
                Title = "Disable StickyKeys, FilterKeys & ToggleKeys Shortcut Interception",
                Category = TweakCategory.InputAndMouse,
                ShortSummary = "Disables Windows accessibility hotkey listeners that monitor rapid Shift/Ctrl presses during crouch-strafing.",
                TechnicalDetails = @"HKCU\Control Panel\Accessibility\StickyKeys (Flags=506), Keyboard Response (Flags=122), ToggleKeys (Flags=58)",
                WhyItHelpsGaming = "Prevents accidental desktop focus loss from 5x Shift popups and removes key-hold timer checks.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 1,
                ImpactLatency = 3,
                ImpactRam = 1,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool ex1, ex2;
                    string sk = BackupAndRollbackEngine.ReadRegistryString(RegistryHive.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "510", out ex1);
                    string fk = BackupAndRollbackEngine.ReadRegistryString(RegistryHive.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", "126", out ex2);
                    bool applied = (sk == "506" && fk == "122");
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("StickyKeys={0}, FilterKeys={1}", sk, fk));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    vault.SetRegistryValueWithBackup("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "506", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122", RegistryValueKind.String, out err);
                    vault.SetRegistryValueWithBackup("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58", RegistryValueKind.String, out err);
                    return new TweakOperationResult(true, "Disabled StickyKeys, FilterKeys, and ToggleKeys shortcut interception.");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "510", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", "126", RegistryValueKind.String, false, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault("inp_accessibility_hooks_off", RegistryHive.CurrentUser, @"Control Panel\Accessibility\ToggleKeys", "Flags", "62", RegistryValueKind.String, false, out err);
                    return new TweakOperationResult(true, "Restored default Windows Accessibility shortcut flags.");
                }
            });

            // =========================================================================
            // SUBSYSTEM F: WINDOWS BLOAT, TELEMETRY & BACKGROUND SERVICES (5 TWEAKS)
            // =========================================================================

            list.Add(new TweakDefinition
            {
                Id = "bloat_telemetry_services_off",
                Title = "Disable Connected User Experiences & Telemetry (DiagTrack)",
                Category = TweakCategory.BloatAndServices,
                ShortSummary = "Stops and disables the DiagTrack & dmwappushservice telemetry daemons and sets AllowTelemetry=0.",
                TechnicalDetails = @"Services DiagTrack & dmwappushservice (Start=4) + HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection (AllowTelemetry=0)",
                WhyItHelpsGaming = "Eliminates continuous background ETW event tracing, disk telemetry database writes, and background CPU bursts.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 4,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    int start = BackupAndRollbackEngine.GetServiceStartMode("DiagTrack");
                    bool existed;
                    int pol = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "AllowTelemetry", 1, out existed);
                    bool applied = (start == 4 || (existed && pol == 0));
                    return new TweakEvaluation(applied ? TweakStatus.Applied : TweakStatus.Default,
                        string.Format("DiagTrack={0}, AllowTelemetry={1}", FormatServiceStartMode(start), pol));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    vault.SetServiceStartModeWithBackup("bloat_telemetry_services_off", "DiagTrack", 4, true, out err);
                    vault.SetServiceStartModeWithBackup("bloat_telemetry_services_off", "dmwappushservice", 4, true, out err);
                    bool ok = vault.SetRegistryValueWithBackup(
                        "bloat_telemetry_services_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "AllowTelemetry", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(ok, ok ? "Disabled DiagTrack, dmwappushservice, and DataCollection telemetry." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    vault.RestoreServiceStartMode("bloat_telemetry_services_off", "DiagTrack", 2, out err);
                    vault.RestoreServiceStartMode("bloat_telemetry_services_off", "dmwappushservice", 3, out err);
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "bloat_telemetry_services_off",
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "AllowTelemetry", 1, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(ok, ok ? "Restored Windows Diagnostic & Telemetry service settings." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "bloat_wsearch_manual",
                Title = "Set Windows Search Indexer (WSearch) to Manual On-Demand Mode",
                Category = TweakCategory.BloatAndServices,
                ShortSummary = "Stops SearchIndexer.exe from continuously crawling drives in the background while keeping file search available.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Services\WSearch -> Start = 3 (Manual) & Stop active daemon",
                WhyItHelpsGaming = "Prevents sudden 10-20% CPU spikes and disk queue saturation on low-spec PCs when new files are written.",
                RecommendedForPotato = true,
                RecommendedForMidRange = false,
                RecommendedForCompetitive = false,
                IncludedInBalancedSafe = false,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 4,
                RiskLevel = TweakRisk.Moderate,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    int start = BackupAndRollbackEngine.GetServiceStartMode("WSearch");
                    return new TweakEvaluation((start == 3 || start == 4) ? TweakStatus.Applied : TweakStatus.Default,
                        "WSearch Start Mode = " + FormatServiceStartMode(start));
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetServiceStartModeWithBackup("bloat_wsearch_manual", "WSearch", 3, true, out err);
                    return new TweakOperationResult(ok, ok ? "Set Windows Search Indexer to Manual and stopped background indexing." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreServiceStartMode("bloat_wsearch_manual", "WSearch", 2, out err);
                    return new TweakOperationResult(ok, ok ? "Restored Windows Search Indexer to Automatic." : err);
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "bloat_background_uwp_off",
                Title = "Freeze Background Windows Store (UWP) Apps",
                Category = TweakCategory.BloatAndServices,
                ShortSummary = "Prevents UWP Store apps (Photos, Weather, News, Mail, Xbox app helpers) from spawning in the background.",
                TechnicalDetails = @"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications -> GlobalUserDisabled = 1",
                WhyItHelpsGaming = "Frees 200-500 MB of physical RAM and eliminates background UWP timer wakeups on 4GB/8GB/16GB PCs.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 3,
                ImpactLatency = 3,
                ImpactRam = 5,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = false,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                        "GlobalUserDisabled", 0, out existed);
                    return new TweakEvaluation(val == 1 ? TweakStatus.Applied : TweakStatus.Default,
                        val == 1 ? "GlobalUserDisabled = 1 (Frozen)" : "Background UWP Apps Allowed");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    vault.SetRegistryValueWithBackup(
                        "bloat_background_uwp_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                        "GlobalUserDisabled", 1, RegistryValueKind.DWord, out err);
                    vault.SetRegistryValueWithBackup(
                        "bloat_background_uwp_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Search",
                        "BackgroundAppGlobalToggle", 0, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Frozen background UWP Store apps (GlobalUserDisabled=1).");
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    vault.RestoreRegistryValueFromBackupOrDefault(
                        "bloat_background_uwp_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                        "GlobalUserDisabled", 0, RegistryValueKind.DWord, true, out err);
                    vault.RestoreRegistryValueFromBackupOrDefault(
                        "bloat_background_uwp_off",
                        RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Search",
                        "BackgroundAppGlobalToggle", 1, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(true, "Re-enabled background UWP Store apps.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "bloat_scheduled_tasks_off",
                Title = "Disable Telemetry & Compatibility Appraiser Scheduled Tasks",
                Category = TweakCategory.BloatAndServices,
                ShortSummary = "Disables CompatTelRunner.exe, ProgramDataUpdater, CEIP Consolidator, UsbCeip, and WinSAT background tasks.",
                TechnicalDetails = @"schtasks /Change /TN '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser' /Disable",
                WhyItHelpsGaming = "CompatTelRunner.exe is notorious for spawning mid-match and pegging 1-2 CPU cores at 100% for several minutes.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = true,
                ImpactFps = 4,
                ImpactLatency = 4,
                ImpactRam = 3,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int marker = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.CurrentUser,
                        @"Software\PCTweaker\State",
                        "TelemetryTasksDisabled", 0, out existed);
                    return new TweakEvaluation(marker == 1 ? TweakStatus.Applied : TweakStatus.Default,
                        marker == 1 ? "5 Telemetry Scheduled Tasks Disabled" : "Scheduled Tasks Default");
                },
                ApplyFunc = (hw, vault) =>
                {
                    if (!hw.IsRunningAsAdmin)
                        return new TweakOperationResult(false, "Administrator privileges required to modify system scheduled tasks.");

                    string[] tasks = new string[]
                    {
                        @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                        @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                        @"\Microsoft\Windows\Maintenance\WinSAT"
                    };

                    int disabledCount = 0;
                    foreach (string tn in tasks)
                    {
                        string o; int c;
                        if (BackupAndRollbackEngine.RunCommandHidden("schtasks.exe", "/Change /TN \"" + tn + "\" /Disable", out o, out c))
                            disabledCount++;
                    }
                    string err;
                    vault.SetRegistryValueWithBackup("bloat_scheduled_tasks_off", RegistryHive.CurrentUser, @"Software\PCTweaker\State", "TelemetryTasksDisabled", 1, RegistryValueKind.DWord, out err);
                    return new TweakOperationResult(true, "Disabled " + disabledCount + " background telemetry/maintenance scheduled tasks.");
                },
                RevertFunc = (hw, vault) =>
                {
                    if (!hw.IsRunningAsAdmin)
                        return new TweakOperationResult(false, "Administrator privileges required to modify system scheduled tasks.");

                    string[] tasks = new string[]
                    {
                        @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                        @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                        @"\Microsoft\Windows\Maintenance\WinSAT"
                    };

                    foreach (string tn in tasks)
                    {
                        string o; int c;
                        BackupAndRollbackEngine.RunCommandHidden("schtasks.exe", "/Change /TN \"" + tn + "\" /Enable", out o, out c);
                    }
                    string err;
                    vault.RestoreRegistryValueFromBackupOrDefault("bloat_scheduled_tasks_off", RegistryHive.CurrentUser, @"Software\PCTweaker\State", "TelemetryTasksDisabled", 0, RegistryValueKind.DWord, true, out err);
                    return new TweakOperationResult(true, "Re-enabled default Windows maintenance & telemetry scheduled tasks.");
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "bloat_hibernation_off",
                Title = "Disable Hibernation & Fast Startup Kernel State Caching",
                Category = TweakCategory.BloatAndServices,
                ShortSummary = "Deletes hiberfil.sys to reclaim 6-16 GB of SSD space and prevents Fast Startup from keeping stale GPU driver uptime.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\Power -> HibernateEnabled = 0 (powercfg /hibernate off)",
                WhyItHelpsGaming = "Prevents weeks-old Fast Startup kernel/driver sessions that cause DPC latency drift, and frees gigabytes of SSD space.",
                RecommendedForPotato = true,
                RecommendedForMidRange = true,
                RecommendedForCompetitive = true,
                IncludedInBalancedSafe = false,
                ImpactFps = 2,
                ImpactLatency = 3,
                ImpactRam = 2,
                RiskLevel = TweakRisk.Safe,
                RequiresAdmin = true,
                RequiresReboot = false,
                DetectFunc = (hw) =>
                {
                    bool existed;
                    int val = BackupAndRollbackEngine.ReadRegistryDword(
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power",
                        "HibernateEnabled", 1, out existed);
                    return new TweakEvaluation(val == 0 ? TweakStatus.Applied : TweakStatus.Default,
                        val == 0 ? "Hibernation Disabled (SSD Space Freed)" : "Hibernation Enabled");
                },
                ApplyFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.SetRegistryValueWithBackup(
                        "bloat_hibernation_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power",
                        "HibernateEnabled", 0, RegistryValueKind.DWord, out err);
                    if (ok)
                    {
                        string o; int c;
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/hibernate off", out o, out c);
                    }
                    return new TweakOperationResult(ok, ok ? "Disabled Hibernation and reclaimed hiberfil.sys SSD space." : err);
                },
                RevertFunc = (hw, vault) =>
                {
                    string err;
                    bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                        "bloat_hibernation_off",
                        RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Power",
                        "HibernateEnabled", 1, RegistryValueKind.DWord, false, out err);
                    if (ok)
                    {
                        string o; int c;
                        BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/hibernate on", out o, out c);
                    }
                    return new TweakOperationResult(ok, ok ? "Re-enabled Windows Hibernation." : err);
                }
            });

            return list;
        }

        public static string GetActivePowerSchemeInfo()
        {
            string outStr;
            int code;
            if (BackupAndRollbackEngine.RunCommandHidden("powercfg.exe", "/getactivescheme", out outStr, out code))
            {
                return outStr.Trim();
            }
            return "Balanced (Default)";
        }

        public static string GetActivePowerSchemeGuid()
        {
            string info = GetActivePowerSchemeInfo();
            string guid = ExtractGuidFromText(info);
            return string.IsNullOrEmpty(guid) ? "381b4222-f694-41f0-9685-ff5bb260df2e" : guid;
        }

        private static string ExtractGuidFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                text,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value : null;
        }

        private static string FormatServiceStartMode(int mode)
        {
            switch (mode)
            {
                case 2: return "2 (Automatic)";
                case 3: return "3 (Manual)";
                case 4: return "4 (Disabled)";
                default: return "Not Present";
            }
        }
    }
}
