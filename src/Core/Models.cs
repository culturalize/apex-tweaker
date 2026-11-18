using System;
using System.Collections.Generic;

namespace PCTweaker.Core
{
    public enum SystemTier
    {
        PotatoLowSpec = 0,
        MidRangeGamer = 1,
        CompetitiveEsports = 2
    }

    public enum PresetProfile
    {
        PotatoRescue = 0,
        CompetitiveFps = 1,
        BalancedSafe = 2
    }

    public enum TweakCategory
    {
        CpuAndPower = 0,
        GpuAndDisplay = 1,
        MemoryAndStorage = 2,
        NetworkAndPing = 3,
        InputAndMouse = 4,
        BloatAndServices = 5
    }

    public enum TweakRisk
    {
        Safe = 0,
        Moderate = 1,
        Advanced = 2
    }

    public enum TweakStatus
    {
        Default = 0,
        Applied = 1,
        Unknown = 2
    }

    public class HardwareProfile
    {
        public string CpuName { get; set; }
        public string CpuVendor { get; set; }
        public int PhysicalCores { get; set; }
        public int LogicalThreads { get; set; }
        public int MaxClockMhz { get; set; }

        public string GpuName { get; set; }
        public string GpuVendor { get; set; }
        public long GpuVramMb { get; set; }
        public string GpuDriverVersion { get; set; }
        public bool IsIntegratedGpu { get; set; }
        public bool IsHagsEnabled { get; set; }

        public long TotalRamMb { get; set; }
        public long TotalRamKb { get; set; }
        public string PrimaryDiskType { get; set; } // "SSD / NVMe" or "HDD"
        public bool IsSsd { get; set; }
        public bool IsLaptop { get; set; }

        public string OsCaption { get; set; }
        public string OsBuild { get; set; }
        public bool IsRunningAsAdmin { get; set; }

        public SystemTier DetectedTier { get; set; }
        public string TierDisplayName { get; set; }
        public string TierRecommendationSummary { get; set; }

        public HardwareProfile()
        {
            CpuName = "Unknown Processor";
            CpuVendor = "Unknown";
            GpuName = "Unknown Graphics Adapter";
            GpuVendor = "Unknown";
            GpuDriverVersion = "N/A";
            PrimaryDiskType = "SSD / NVMe";
            IsSsd = true;
            OsCaption = "Microsoft Windows";
            OsBuild = Environment.OSVersion.Version.ToString();
            TierDisplayName = "Mid-Range Gaming PC";
            TierRecommendationSummary = "";
        }
    }

    public class TweakEvaluation
    {
        public TweakStatus Status { get; set; }
        public string LiveValueSummary { get; set; }

        public TweakEvaluation(TweakStatus status, string liveValueSummary)
        {
            Status = status;
            LiveValueSummary = liveValueSummary ?? "";
        }
    }

    public class TweakOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool RequiresReboot { get; set; }

        public TweakOperationResult(bool success, string message, bool requiresReboot = false)
        {
            Success = success;
            Message = message ?? "";
            RequiresReboot = requiresReboot;
        }
    }

    public class TweakDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public TweakCategory Category { get; set; }
        public string ShortSummary { get; set; }
        public string TechnicalDetails { get; set; }
        public string WhyItHelpsGaming { get; set; }

        public bool RecommendedForPotato { get; set; }
        public bool RecommendedForMidRange { get; set; }
        public bool RecommendedForCompetitive { get; set; }
        public bool IncludedInBalancedSafe { get; set; }

        public int ImpactFps { get; set; }      // 1..5
        public int ImpactLatency { get; set; }  // 1..5
        public int ImpactRam { get; set; }      // 1..5

        public TweakRisk RiskLevel { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool RequiresReboot { get; set; }
        public bool AntiCheatSafe { get; set; }

        public TweakStatus CurrentStatus { get; set; }
        public string LiveValueSummary { get; set; }

        public Func<HardwareProfile, TweakEvaluation> DetectFunc { get; set; }
        public Func<HardwareProfile, BackupAndRollbackEngine, TweakOperationResult> ApplyFunc { get; set; }
        public Func<HardwareProfile, BackupAndRollbackEngine, TweakOperationResult> RevertFunc { get; set; }

        public TweakDefinition()
        {
            AntiCheatSafe = true;
            CurrentStatus = TweakStatus.Default;
            LiveValueSummary = "Not audited";
        }

        public bool IsRecommendedForTier(SystemTier tier)
        {
            switch (tier)
            {
                case SystemTier.PotatoLowSpec: return RecommendedForPotato;
                case SystemTier.MidRangeGamer: return RecommendedForMidRange;
                case SystemTier.CompetitiveEsports: return RecommendedForCompetitive;
                default: return RecommendedForMidRange;
            }
        }

        public static string GetCategoryDisplayName(TweakCategory cat)
        {
            switch (cat)
            {
                case TweakCategory.CpuAndPower: return "CPU & Power";
                case TweakCategory.GpuAndDisplay: return "GPU & Display";
                case TweakCategory.MemoryAndStorage: return "Memory & Storage";
                case TweakCategory.NetworkAndPing: return "Network & Ping";
                case TweakCategory.InputAndMouse: return "Input & Mouse";
                case TweakCategory.BloatAndServices: return "Bloat & Services";
                default: return cat.ToString();
            }
        }
    }

    public class BackupItem
    {
        public string TweakId { get; set; }
        public string TargetType { get; set; } // "Registry", "Service", "ScheduledTask", "PowerPlan", "Command"
        public string TargetPath { get; set; }
        public string ValueName { get; set; }
        public bool ValueExisted { get; set; }
        public string ValueKind { get; set; }  // "DWord", "QWord", "String", "Binary"
        public string OriginalValue { get; set; }
        public string AppliedValue { get; set; }
        public string CapturedAtUtc { get; set; }
    }

    public class StateSnapshot
    {
        public string SnapshotId { get; set; }
        public string Label { get; set; }
        public string CreatedAtUtc { get; set; }
        public List<BackupItem> Items { get; set; }
        public Dictionary<string, int> TweakStates { get; set; }

        public StateSnapshot()
        {
            Items = new List<BackupItem>();
            TweakStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class MemoryPurgeResult
    {
        public bool Success { get; set; }
        public long RamAvailableBeforeMb { get; set; }
        public long RamAvailableAfterMb { get; set; }
        public long FreedMb { get; set; }
        public int ProcessesTrimmed { get; set; }
        public bool StandbyListPurged { get; set; }
        public long DurationMs { get; set; }
        public string SummaryMessage { get; set; }
    }

    public class CacheCleanResult
    {
        public bool Success { get; set; }
        public int FilesDeleted { get; set; }
        public int FilesSkippedInUse { get; set; }
        public double FreedMb { get; set; }
        public string SummaryMessage { get; set; }
    }

    public class DetectedGame
    {
        public string GameName { get; set; }
        public string ExecutableName { get; set; }
        public string ExecutablePath { get; set; }
        public string EngineName { get; set; } // "Unreal Engine", "Source / Custom", etc.
        public string GameUserSettingsIniPath { get; set; }
        public bool HasUnrealIni { get; set; }
        public bool HasIniBackup { get; set; }
        public bool IsIfeoHighPriorityEnabled { get; set; }
        public bool IsHighPerfGpuPreferred { get; set; }
        public bool IsFullscreenOptDisabled { get; set; }
    }

    public class GameBoostSessionState
    {
        public bool IsActive { get; set; }
        public DateTime EngagedAt { get; set; }
        public string PreviousPowerSchemeGuid { get; set; }
        public List<string> TerminatedProcesses { get; set; }
        public List<string> StoppedServices { get; set; }
        public long FreedRamMbOnEngage { get; set; }
        public string StatusSummary { get; set; }

        public GameBoostSessionState()
        {
            TerminatedProcesses = new List<string>();
            StoppedServices = new List<string>();
            StatusSummary = "Inactive (Desktop Default Mode)";
        }
    }

    public class TelemetrySample
    {
        public double CpuUsagePercent { get; set; }
        public long TotalRamMb { get; set; }
        public long UsedRamMb { get; set; }
        public long AvailableRamMb { get; set; }
        public long StandbyCacheMb { get; set; }
        public long FreeRamMb { get; set; }
        public int MemoryLoadPercent { get; set; }

        public double TimerResCurrentMs { get; set; }
        public double TimerResMinMs { get; set; }
        public double TimerResMaxMs { get; set; }

        public int ProcessCount { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }

        public string ActivePowerPlanName { get; set; }
        public int AppliedTweaksCount { get; set; }
        public int TotalTweaksCount { get; set; }
        public int RecommendedAppliedCount { get; set; }
        public int RecommendedTotalCount { get; set; }
        public int OptimizationScorePercent { get; set; }
    }
}
