using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace PCTweaker.Core
{
    public static class GameConfigOptimizer
    {
        private static readonly string[] IgnoredExeSubstrings = new string[]
        {
            "unins", "setup", "install", "crashreport", "unitycrashhandler", "cefprocess",
            "launcher", "updater", "helper", "redist", "dxsetup", "vcredist", "easyanticheat",
            "beservice", "battleye", "explorer", "chrome", "msedge", "firefox", "discord",
            "spotify", "obs64", "steam.exe", "steamwebhelper", "epicgameslauncher", "riotclient"
        };

        public static List<DetectedGame> DiscoverInstalledGames()
        {
            List<DetectedGame> results = new List<DetectedGame>();
            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenExePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Build index of verified game executables from Windows GameConfigStore
            List<string> discoveredExePaths = ScanGameConfigStoreExecutables();

            // 2. Scan %LOCALAPPDATA% for Unreal Engine GameUserSettings.ini configs
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (Directory.Exists(localAppData))
            {
                try
                {
                    foreach (string appDir in Directory.GetDirectories(localAppData))
                    {
                        string winClientCfg = Path.Combine(appDir, @"Saved\Config\WindowsClient\GameUserSettings.ini");
                        string winNoEditorCfg = Path.Combine(appDir, @"Saved\Config\WindowsNoEditor\GameUserSettings.ini");
                        string winCfg = Path.Combine(appDir, @"Saved\Config\Windows\GameUserSettings.ini");

                        string iniPath = null;
                        if (File.Exists(winClientCfg)) iniPath = winClientCfg;
                        else if (File.Exists(winNoEditorCfg)) iniPath = winNoEditorCfg;
                        else if (File.Exists(winCfg)) iniPath = winCfg;

                        if (iniPath != null)
                        {
                            string folderName = Path.GetFileName(appDir);
                            if (seenNames.Add(folderName))
                            {
                                string exeGuess = InferUnrealExeName(folderName);
                                string matchedFullPath = FindMatchingExePath(folderName, exeGuess, discoveredExePaths);
                                if (!string.IsNullOrEmpty(matchedFullPath))
                                {
                                    exeGuess = Path.GetFileName(matchedFullPath);
                                    seenExePaths.Add(matchedFullPath);
                                }

                                DetectedGame g = new DetectedGame
                                {
                                    GameName = folderName,
                                    ExecutableName = exeGuess,
                                    ExecutablePath = matchedFullPath ?? "",
                                    EngineName = "Unreal Engine",
                                    GameUserSettingsIniPath = iniPath,
                                    HasUnrealIni = true
                                };
                                RefreshGameStatus(g);
                                results.Add(g);
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Add verified DirectX / Steam / Epic / Standalone game binaries from GameConfigStore
            foreach (string exePath in discoveredExePaths)
            {
                if (seenExePaths.Contains(exePath)) continue;
                string exeName = Path.GetFileName(exePath);
                string friendlyTitle = InferFriendlyGameTitle(exePath);
                if (seenNames.Contains(friendlyTitle)) continue;

                seenNames.Add(friendlyTitle);
                seenExePaths.Add(exePath);

                DetectedGame g = new DetectedGame
                {
                    GameName = friendlyTitle,
                    ExecutableName = exeName,
                    ExecutablePath = exePath,
                    EngineName = exeName.IndexOf("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Unreal Engine"
                        : "DirectX / Native",
                    GameUserSettingsIniPath = "",
                    HasUnrealIni = false
                };
                RefreshGameStatus(g);
                results.Add(g);
            }

            return results;
        }

        private static List<string> ScanGameConfigStoreExecutables()
        {
            List<string> list = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                using (RegistryKey children = baseKey.OpenSubKey(@"System\GameConfigStore\Children", false))
                {
                    if (children != null)
                    {
                        foreach (string childName in children.GetSubKeyNames())
                        {
                            using (RegistryKey sub = children.OpenSubKey(childName, false))
                            {
                                if (sub == null) continue;
                                string exePath = (sub.GetValue("MatchedExeFullPath") ?? "").ToString().Trim();
                                if (string.IsNullOrEmpty(exePath)) continue;
                                if (!IsCandidateGameExecutable(exePath)) continue;
                                if (File.Exists(exePath) && unique.Add(exePath))
                                {
                                    list.Add(exePath);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        private static bool IsCandidateGameExecutable(string exePath)
        {
            string lowerPath = exePath.ToLowerInvariant();
            if (!lowerPath.EndsWith(".exe")) return false;
            if (lowerPath.Contains(@"\windows\") || lowerPath.Contains(@"\system32\") || lowerPath.Contains(@"\microsoft.net\"))
                return false;

            string fileName = Path.GetFileName(lowerPath);
            foreach (string bad in IgnoredExeSubstrings)
            {
                if (fileName.Contains(bad)) return false;
            }
            return true;
        }

        private static string FindMatchingExePath(string folderName, string exeGuess, List<string> candidates)
        {
            string cleanFolder = folderName.Replace("Game", "");
            foreach (string path in candidates)
            {
                string fn = Path.GetFileName(path);
                if (string.Equals(fn, exeGuess, StringComparison.OrdinalIgnoreCase))
                    return path;
                if (!string.IsNullOrEmpty(cleanFolder) &&
                    (fn.IndexOf(cleanFolder, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     path.IndexOf(@"\" + folderName + @"\", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return path;
                }
            }
            return "";
        }

        private static string InferFriendlyGameTitle(string exePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(exePath) ?? "";
                string[] parts = dir.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (string.Equals(parts[i], "common", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(parts[i], "Epic Games", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(parts[i], "EpicGames", StringComparison.OrdinalIgnoreCase))
                    {
                        return parts[i + 1];
                    }
                }
            }
            catch { }
            return Path.GetFileNameWithoutExtension(exePath);
        }

        private static string InferUnrealExeName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return "Game-Win64-Shipping.exe";
            if (folderName.IndexOf("Fortnite", StringComparison.OrdinalIgnoreCase) >= 0)
                return "FortniteClient-Win64-Shipping.exe";
            string clean = folderName.Replace("Game", "");
            if (string.IsNullOrEmpty(clean)) clean = folderName;
            return clean + "-Win64-Shipping.exe";
        }

        public static void RefreshGameStatus(DetectedGame game)
        {
            if (game == null) return;
            game.IsIfeoHighPriorityEnabled = GameBoosterEngine.IsGameIfeoHighPriority(game.ExecutableName);
            game.HasIniBackup = !string.IsNullOrEmpty(game.GameUserSettingsIniPath) &&
                                File.Exists(game.GameUserSettingsIniPath + ".pctweaker.bak");

            if (!string.IsNullOrEmpty(game.ExecutablePath))
            {
                bool ex1, ex2;
                string gpuPref = BackupAndRollbackEngine.ReadRegistryString(
                    RegistryHive.CurrentUser,
                    @"Software\Microsoft\DirectX\UserGpuPreferences",
                    game.ExecutablePath, "", out ex1);
                game.IsHighPerfGpuPreferred = gpuPref.IndexOf("GpuPreference=2", StringComparison.OrdinalIgnoreCase) >= 0;

                string layers = BackupAndRollbackEngine.ReadRegistryString(
                    RegistryHive.CurrentUser,
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
                    game.ExecutablePath, "", out ex2);
                game.IsFullscreenOptDisabled = layers.IndexOf("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static bool ApplyUnrealScalabilityProfile(DetectedGame game, bool ultraLowPotatoMode, BackupAndRollbackEngine vault, out string message)
        {
            message = "";
            if (game == null || string.IsNullOrEmpty(game.GameUserSettingsIniPath) || !File.Exists(game.GameUserSettingsIniPath))
            {
                message = "Config file not found.";
                return false;
            }

            string iniPath = game.GameUserSettingsIniPath;
            string bakPath = iniPath + ".pctweaker.bak";

            try
            {
                if (!File.Exists(bakPath))
                {
                    File.Copy(iniPath, bakPath, false);
                }

                FileAttributes attrs = File.GetAttributes(iniPath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(iniPath, attrs & ~FileAttributes.ReadOnly);
                }

                Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                overrides["sg.ShadowQuality"] = "0";
                overrides["sg.PostProcessQuality"] = "0";
                overrides["sg.EffectsQuality"] = "0";
                overrides["sg.FoliageQuality"] = "0";
                overrides["sg.ShadingQuality"] = "0";
                overrides["sg.GlobalIlluminationQuality"] = "0";
                overrides["sg.ReflectionQuality"] = "0";
                overrides["sg.ViewDistanceQuality"] = ultraLowPotatoMode ? "0" : "2";
                overrides["sg.TextureQuality"] = ultraLowPotatoMode ? "0" : "2";
                overrides["sg.AntiAliasingQuality"] = ultraLowPotatoMode ? "0" : "1";
                overrides["sg.ResolutionQuality"] = ultraLowPotatoMode ? "85" : "100";
                overrides["bUseVSync"] = "False";
                overrides["bMotionBlur"] = "False";

                string[] lines = File.ReadAllLines(iniPath, Encoding.UTF8);
                HashSet<string> appliedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("[") || line.StartsWith(";"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string k = line.Substring(0, eq).Trim();
                    string newVal;
                    if (overrides.TryGetValue(k, out newVal))
                    {
                        lines[i] = k + "=" + newVal;
                        appliedKeys.Add(k);
                    }
                }

                List<string> outLines = new List<string>(lines);
                bool hasScalabilitySection = false;
                foreach (string l in outLines)
                {
                    if (l.Trim().Equals("[ScalabilityGroups]", StringComparison.OrdinalIgnoreCase))
                    {
                        hasScalabilitySection = true;
                        break;
                    }
                }

                if (!hasScalabilitySection)
                {
                    outLines.Add("");
                    outLines.Add("[ScalabilityGroups]");
                }

                foreach (KeyValuePair<string, string> kv in overrides)
                {
                    if (kv.Key.StartsWith("sg.", StringComparison.OrdinalIgnoreCase) && !appliedKeys.Contains(kv.Key))
                    {
                        outLines.Add(kv.Key + "=" + kv.Value);
                    }
                }

                File.WriteAllLines(iniPath, outLines.ToArray(), Encoding.UTF8);
                RefreshGameStatus(game);

                string modeLabel = ultraLowPotatoMode ? "Low-Spec Max FPS" : "Competitive Clarity";
                message = string.Format("Applied {0} scalability preset to {1} (backup saved).", modeLabel, game.GameName);
                if (vault != null) vault.AppendAuditLog(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to update config: " + ex.Message;
                return false;
            }
        }

        public static bool RestoreUnrealConfigBackup(DetectedGame game, BackupAndRollbackEngine vault, out string message)
        {
            message = "";
            if (game == null || string.IsNullOrEmpty(game.GameUserSettingsIniPath))
            {
                message = "No game config selected.";
                return false;
            }

            string iniPath = game.GameUserSettingsIniPath;
            string bakPath = iniPath + ".pctweaker.bak";
            if (!File.Exists(bakPath))
            {
                message = "No backup file (.pctweaker.bak) exists for " + game.GameName + ".";
                return false;
            }

            try
            {
                FileAttributes attrs = File.Exists(iniPath) ? File.GetAttributes(iniPath) : FileAttributes.Normal;
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(iniPath, attrs & ~FileAttributes.ReadOnly);

                File.Copy(bakPath, iniPath, true);
                RefreshGameStatus(game);
                message = "Restored original GameUserSettings.ini backup for " + game.GameName + ".";
                if (vault != null) vault.AppendAuditLog(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to restore backup: " + ex.Message;
                return false;
            }
        }

        public static bool SetExecutableDirectXGpuPreference(string exeFullPath, bool highPerfGpu, BackupAndRollbackEngine vault, out string message)
        {
            message = "";
            if (string.IsNullOrEmpty(exeFullPath))
            {
                message = "Full executable path is required to set per-app DirectX GPU preference.";
                return false;
            }

            string err;
            string subKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
            if (highPerfGpu)
            {
                bool ok = vault.SetRegistryValueWithBackup(
                    "dx_gpu_pref",
                    RegistryHive.CurrentUser,
                    subKey,
                    exeFullPath,
                    "GpuPreference=2;",
                    RegistryValueKind.String,
                    out err);
                message = ok ? ("Assigned High-Performance discrete GPU to " + Path.GetFileName(exeFullPath)) : err;
                return ok;
            }
            else
            {
                bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                    "dx_gpu_pref",
                    RegistryHive.CurrentUser,
                    subKey,
                    exeFullPath,
                    "",
                    RegistryValueKind.String,
                    true,
                    out err);
                message = ok ? ("Removed custom GPU preference for " + Path.GetFileName(exeFullPath)) : err;
                return ok;
            }
        }

        public static bool SetExecutableFullscreenOptimizations(string exeFullPath, bool disableFso, BackupAndRollbackEngine vault, out string message)
        {
            message = "";
            if (string.IsNullOrEmpty(exeFullPath))
            {
                message = "Full executable path is required to configure per-app Fullscreen Optimizations.";
                return false;
            }

            string err;
            string subKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            if (disableFso)
            {
                bool ok = vault.SetRegistryValueWithBackup(
                    "fso_override",
                    RegistryHive.CurrentUser,
                    subKey,
                    exeFullPath,
                    "~ DISABLEDXMAXIMIZEDWINDOWEDMODE",
                    RegistryValueKind.String,
                    out err);
                message = ok ? ("Disabled Fullscreen Optimizations (enforced exclusive fullscreen) for " + Path.GetFileName(exeFullPath)) : err;
                return ok;
            }
            else
            {
                bool ok = vault.RestoreRegistryValueFromBackupOrDefault(
                    "fso_override",
                    RegistryHive.CurrentUser,
                    subKey,
                    exeFullPath,
                    "",
                    RegistryValueKind.String,
                    true,
                    out err);
                message = ok ? ("Restored default Fullscreen Optimizations for " + Path.GetFileName(exeFullPath)) : err;
                return ok;
            }
        }
    }
}
