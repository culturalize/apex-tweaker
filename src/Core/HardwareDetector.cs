using System;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

namespace PCTweaker.Core
{
    public static class HardwareDetector
    {
        public static HardwareProfile Detect()
        {
            HardwareProfile profile = new HardwareProfile();
            profile.IsRunningAsAdmin = CheckIsAdmin();

            // 1. Physical Memory via GlobalMemoryStatusEx
            try
            {
                NativeInterop.MEMORYSTATUSEX mem = new NativeInterop.MEMORYSTATUSEX();
                mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeInterop.MEMORYSTATUSEX));
                if (NativeInterop.GlobalMemoryStatusEx(ref mem))
                {
                    profile.TotalRamKb = (long)(mem.ullTotalPhys / 1024UL);
                    profile.TotalRamMb = (long)(mem.ullTotalPhys / (1024UL * 1024UL));
                }
            }
            catch
            {
                profile.TotalRamMb = 8192;
                profile.TotalRamKb = 8192 * 1024;
            }

            // 2. CPU via Win32_Processor
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        profile.CpuName = (obj["Name"] ?? "Unknown CPU").ToString().Trim();
                        string mfg = (obj["Manufacturer"] ?? "").ToString();
                        if (mfg.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0)
                            profile.CpuVendor = "AMD";
                        else if (mfg.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
                            profile.CpuVendor = "Intel";
                        else
                            profile.CpuVendor = mfg;

                        profile.PhysicalCores = Convert.ToInt32(obj["NumberOfCores"] ?? Environment.ProcessorCount);
                        profile.LogicalThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? Environment.ProcessorCount);
                        profile.MaxClockMhz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                        break;
                    }
                }
            }
            catch
            {
                profile.PhysicalCores = Math.Max(1, Environment.ProcessorCount / 2);
                profile.LogicalThreads = Environment.ProcessorCount;
            }

            // 3. GPU via Registry 64-bit qwMemorySize + Win32_VideoController
            DetectBestGpu(profile);

            // 4. HAGS status
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("HwSchMode");
                        if (val != null && Convert.ToInt32(val) == 2)
                            profile.IsHagsEnabled = true;
                    }
                }
            }
            catch { }

            // 5. OS Caption & Build
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Caption, BuildNumber FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        profile.OsCaption = (obj["Caption"] ?? "Microsoft Windows").ToString().Trim();
                        profile.OsBuild = (obj["BuildNumber"] ?? Environment.OSVersion.Version.Build.ToString()).ToString().Trim();
                        break;
                    }
                }
            }
            catch { }

            // 6. Storage Type (SSD/NVMe vs HDD)
            DetectStorageType(profile);

            // 7. Laptop Battery check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        profile.IsLaptop = true;
                        break;
                    }
                }
            }
            catch { }

            // 8. Automatic System Tier Classification
            ClassifyTier(profile);

            return profile;
        }

        private static void DetectBestGpu(HardwareProfile profile)
        {
            string bestName = "Unknown GPU";
            string bestDriver = "N/A";
            long bestVramBytes = 0;

            // Check 64-bit VRAM from Display Class registry keys first
            try
            {
                using (RegistryKey classKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (classKey != null)
                    {
                        foreach (string subName in classKey.GetSubKeyNames())
                        {
                            if (!subName.StartsWith("0")) continue;
                            using (RegistryKey sub = classKey.OpenSubKey(subName))
                            {
                                if (sub == null) continue;
                                string desc = (sub.GetValue("DriverDesc") ?? "").ToString().Trim();
                                if (string.IsNullOrEmpty(desc)) continue;

                                long vram = 0;
                                object qw = sub.GetValue("HardwareInformation.qwMemorySize");
                                if (qw is long)
                                    vram = (long)qw;
                                else if (qw is byte[] && ((byte[])qw).Length >= 8)
                                    vram = BitConverter.ToInt64((byte[])qw, 0);
                                else
                                {
                                    object dw = sub.GetValue("HardwareInformation.MemorySize");
                                    if (dw is int)
                                        vram = (uint)(int)dw;
                                    else if (dw is byte[] && ((byte[])dw).Length >= 4)
                                        vram = BitConverter.ToUInt32((byte[])dw, 0);
                                }

                                bool isDiscreteCandidate = IsLikelyDiscreteGpu(desc);
                                bool currentBestIsDiscrete = IsLikelyDiscreteGpu(bestName);

                                if (bestName == "Unknown GPU" ||
                                    (isDiscreteCandidate && !currentBestIsDiscrete) ||
                                    (isDiscreteCandidate == currentBestIsDiscrete && vram > bestVramBytes))
                                {
                                    bestName = desc;
                                    bestVramBytes = vram;
                                    bestDriver = (sub.GetValue("DriverVersion") ?? "N/A").ToString();
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback or supplement from WMI Win32_VideoController
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = (obj["Name"] ?? "").ToString().Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        long wmiRam = 0;
                        if (obj["AdapterRAM"] != null)
                            wmiRam = Convert.ToInt64(obj["AdapterRAM"]);

                        string drv = (obj["DriverVersion"] ?? "N/A").ToString();

                        if (bestName == "Unknown GPU" || (IsLikelyDiscreteGpu(name) && !IsLikelyDiscreteGpu(bestName)))
                        {
                            bestName = name;
                            if (bestVramBytes <= 0) bestVramBytes = wmiRam;
                            bestDriver = drv;
                        }
                        else if (string.Equals(bestName, name, StringComparison.OrdinalIgnoreCase) && bestVramBytes <= 0)
                        {
                            bestVramBytes = wmiRam;
                        }
                    }
                }
            }
            catch { }

            profile.GpuName = bestName;
            profile.GpuDriverVersion = bestDriver;
            profile.GpuVramMb = bestVramBytes > 0 ? (bestVramBytes / (1024L * 1024L)) : 0;

            if (bestName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                bestName.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                bestName.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                bestName.IndexOf("GTX", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile.GpuVendor = "NVIDIA";
            }
            else if (bestName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     bestName.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile.GpuVendor = "AMD";
            }
            else if (bestName.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile.GpuVendor = "Intel";
            }
            else
            {
                profile.GpuVendor = "Unknown";
            }

            profile.IsIntegratedGpu = !IsLikelyDiscreteGpu(bestName) || profile.GpuVramMb <= 2048;
        }

        private static bool IsLikelyDiscreteGpu(string gpuName)
        {
            if (string.IsNullOrEmpty(gpuName)) return false;
            if (gpuName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("GTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("Radeon RX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("Arc A", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpuName.IndexOf("Arc B", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }

        private static void DetectStorageType(HardwareProfile profile)
        {
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    scope, new ObjectQuery("SELECT FriendlyName, MediaType, BusType FROM MSFT_PhysicalDisk")))
                {
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        ushort mediaType = Convert.ToUInt16(disk["MediaType"] ?? 0);
                        ushort busType = Convert.ToUInt16(disk["BusType"] ?? 0);
                        // MediaType: 3 = HDD, 4 = SSD; BusType: 17 = NVMe
                        if (busType == 17)
                        {
                            profile.PrimaryDiskType = "NVMe SSD";
                            profile.IsSsd = true;
                            return;
                        }
                        if (mediaType == 4)
                        {
                            profile.PrimaryDiskType = "SATA SSD";
                            profile.IsSsd = true;
                            return;
                        }
                        if (mediaType == 3)
                        {
                            profile.PrimaryDiskType = "Mechanical HDD";
                            profile.IsSsd = false;
                        }
                    }
                }
            }
            catch { }
        }

        private static void ClassifyTier(HardwareProfile profile)
        {
            bool lowRam = profile.TotalRamMb <= 10240; // <= 10 GB
            bool lowVram = profile.GpuVramMb > 0 && profile.GpuVramMb <= 4096;
            bool lowCores = profile.PhysicalCores <= 4;

            if (lowRam || lowVram || profile.IsIntegratedGpu || lowCores)
            {
                profile.DetectedTier = SystemTier.PotatoLowSpec;
                profile.TierDisplayName = "Tier 1: Low-Spec / Potato Rescue Profile";
                profile.TierRecommendationSummary =
                    "Hardware bottle-neck protection prioritized: Reclaim DWM VRAM, consolidate svchost RAM overhead, freeze background UWP apps, purge standby memory before launch, and use Potato GameUserSettings.ini configs.";
            }
            else if (profile.TotalRamMb >= 15000 && profile.GpuVramMb >= 7500 && profile.PhysicalCores >= 8)
            {
                profile.DetectedTier = SystemTier.CompetitiveEsports;
                profile.TierDisplayName = "Tier 3: Competitive Esports & Low-Latency Rig";
                profile.TierRecommendationSummary =
                    "Frame-pacing & input-latency prioritized: Unpark CPU cores, unlock Ultimate Performance scheme, elevate MMCSS & Win32PrioritySeparation (0x26), disable Nagle's algorithm & network throttling, and lock 1:1 mouse linearity.";
            }
            else
            {
                profile.DetectedTier = SystemTier.MidRangeGamer;
                profile.TierDisplayName = "Tier 2: Mid-Range Gaming Framerate Boost";
                profile.TierRecommendationSummary =
                    "Balanced FPS & 1% low stability prioritized: Disable GameDVR encoder shadow buffers, enable DXGI Windowed Flip Model, pause background telemetry/indexing spikes, and keep kernel pages resident.";
            }
        }

        public static bool CheckIsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
