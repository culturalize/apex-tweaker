using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace PCTweaker.Core
{
    public class BackupAndRollbackEngine
    {
        private readonly string _vaultRootDir;
        private readonly string _snapshotsDir;
        private readonly string _baselinePath;
        private readonly string _auditLogPath;
        private readonly JavaScriptSerializer _serializer;

        private StateSnapshot _baseline;
        public List<string> SessionAuditLog { get; private set; }

        public string VaultDirectory { get { return _vaultRootDir; } }

        public BackupAndRollbackEngine()
        {
            _vaultRootDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PCTweaker");
            _snapshotsDir = Path.Combine(_vaultRootDir, "Snapshots");
            _baselinePath = Path.Combine(_vaultRootDir, "baseline-state.json");
            _auditLogPath = Path.Combine(_vaultRootDir, "audit.log");
            _serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            SessionAuditLog = new List<string>();

            EnsureDirectories();
            LoadOrInitializeBaseline();
        }

        private void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(_vaultRootDir))
                    Directory.CreateDirectory(_vaultRootDir);
                if (!Directory.Exists(_snapshotsDir))
                    Directory.CreateDirectory(_snapshotsDir);
            }
            catch { }
        }

        private void LoadOrInitializeBaseline()
        {
            try
            {
                if (File.Exists(_baselinePath))
                {
                    string json = File.ReadAllText(_baselinePath, Encoding.UTF8);
                    _baseline = _serializer.Deserialize<StateSnapshot>(json);
                }
            }
            catch { }

            if (_baseline == null)
            {
                _baseline = new StateSnapshot
                {
                    SnapshotId = "baseline",
                    Label = "Original Pre-Tweak System Baseline",
                    CreatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    Items = new List<BackupItem>()
                };
                SaveBaseline();
            }
        }

        private void SaveBaseline()
        {
            try
            {
                EnsureDirectories();
                string json = _serializer.Serialize(_baseline);
                File.WriteAllText(_baselinePath, json, Encoding.UTF8);
            }
            catch { }
        }

        public void AppendAuditLog(string entry)
        {
            string line = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), entry);
            SessionAuditLog.Insert(0, line);
            if (SessionAuditLog.Count > 300)
                SessionAuditLog.RemoveAt(SessionAuditLog.Count - 1);

            try
            {
                File.AppendAllText(_auditLogPath,
                    string.Format("[{0}] {1}{2}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), entry, Environment.NewLine),
                    Encoding.UTF8);
            }
            catch { }
        }

        public static object ReadRegistryValue64(RegistryHive hive, string subKeyPath, string valueName, out bool existed)
        {
            existed = false;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey subKey = baseKey.OpenSubKey(subKeyPath, false))
                {
                    if (subKey == null) return null;
                    object val = subKey.GetValue(valueName, null);
                    if (val != null)
                    {
                        existed = true;
                        return val;
                    }
                }
            }
            catch { }
            return null;
        }

        public static int ReadRegistryDword(RegistryHive hive, string subKeyPath, string valueName, int defaultVal, out bool existed)
        {
            object raw = ReadRegistryValue64(hive, subKeyPath, valueName, out existed);
            if (!existed || raw == null) return defaultVal;
            try
            {
                return Convert.ToInt32(raw);
            }
            catch
            {
                return defaultVal;
            }
        }

        public static string ReadRegistryString(RegistryHive hive, string subKeyPath, string valueName, string defaultVal, out bool existed)
        {
            object raw = ReadRegistryValue64(hive, subKeyPath, valueName, out existed);
            if (!existed || raw == null) return defaultVal;
            return raw.ToString();
        }

        public bool SetRegistryValueWithBackup(
            string tweakId,
            RegistryHive hive,
            string subKeyPath,
            string valueName,
            object newValue,
            RegistryValueKind kind,
            out string error)
        {
            error = null;
            string hivePrefix = GetHivePrefix(hive);
            string fullKeyPath = hivePrefix + @"\" + subKeyPath;

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                {
                    // 1. Capture pre-tweak state if not already captured in baseline
                    BackupItem existing = FindBaselineItem("Registry", fullKeyPath, valueName);
                    if (existing == null)
                    {
                        bool valExisted = false;
                        string origValStr = "";
                        string origKindStr = kind.ToString();

                        using (RegistryKey readKey = baseKey.OpenSubKey(subKeyPath, false))
                        {
                            if (readKey != null)
                            {
                                object raw = readKey.GetValue(valueName, null);
                                if (raw != null)
                                {
                                    valExisted = true;
                                    RegistryValueKind actualKind = readKey.GetValueKind(valueName);
                                    origKindStr = actualKind.ToString();
                                    origValStr = ConvertValueToString(raw);
                                }
                            }
                        }

                        _baseline.Items.Add(new BackupItem
                        {
                            TweakId = tweakId,
                            TargetType = "Registry",
                            TargetPath = fullKeyPath,
                            ValueName = valueName,
                            ValueExisted = valExisted,
                            ValueKind = origKindStr,
                            OriginalValue = origValStr,
                            AppliedValue = ConvertValueToString(newValue),
                            CapturedAtUtc = DateTime.UtcNow.ToString("o")
                        });
                        SaveBaseline();
                    }

                    // 2. Write new value
                    using (RegistryKey writeKey = baseKey.CreateSubKey(subKeyPath))
                    {
                        if (writeKey == null)
                        {
                            error = "Could not open/create key: " + fullKeyPath;
                            return false;
                        }
                        writeKey.SetValue(valueName, newValue, kind);
                    }

                    AppendAuditLog(string.Format("[{0}] Set {1}\\{2} = {3}", tweakId, fullKeyPath, valueName, ConvertValueToString(newValue)));
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                error = "Administrator privileges required to modify " + fullKeyPath;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool RestoreRegistryValueFromBackupOrDefault(
            string tweakId,
            RegistryHive hive,
            string subKeyPath,
            string valueName,
            object fallbackDefaultValue,
            RegistryValueKind fallbackKind,
            bool deleteIfNoBackup,
            out string error)
        {
            error = null;
            string hivePrefix = GetHivePrefix(hive);
            string fullKeyPath = hivePrefix + @"\" + subKeyPath;

            try
            {
                BackupItem backup = FindBaselineItem("Registry", fullKeyPath, valueName);
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey writeKey = baseKey.OpenSubKey(subKeyPath, true))
                {
                    if (backup != null)
                    {
                        if (!backup.ValueExisted)
                        {
                            if (writeKey != null)
                                writeKey.DeleteValue(valueName, false);
                            AppendAuditLog(string.Format("[{0}] Reverted {1}\\{2} (deleted non-existent original value)", tweakId, fullKeyPath, valueName));
                            return true;
                        }
                        else
                        {
                            RegistryKey targetKey = writeKey ?? baseKey.CreateSubKey(subKeyPath);
                            RegistryValueKind restoredKind = ParseValueKind(backup.ValueKind, fallbackKind);
                            object restoredObj = ParseValueFromString(backup.OriginalValue, restoredKind);
                            targetKey.SetValue(valueName, restoredObj, restoredKind);
                            if (writeKey == null && targetKey != null) targetKey.Dispose();
                            AppendAuditLog(string.Format("[{0}] Restored {1}\\{2} = {3}", tweakId, fullKeyPath, valueName, backup.OriginalValue));
                            return true;
                        }
                    }
                    else
                    {
                        if (deleteIfNoBackup)
                        {
                            if (writeKey != null)
                                writeKey.DeleteValue(valueName, false);
                            AppendAuditLog(string.Format("[{0}] Deleted {1}\\{2} (restored stock default)", tweakId, fullKeyPath, valueName));
                            return true;
                        }
                        else
                        {
                            using (RegistryKey createKey = baseKey.CreateSubKey(subKeyPath))
                            {
                                createKey.SetValue(valueName, fallbackDefaultValue, fallbackKind);
                            }
                            AppendAuditLog(string.Format("[{0}] Reset {1}\\{2} = {3} (stock default)", tweakId, fullKeyPath, valueName, ConvertValueToString(fallbackDefaultValue)));
                            return true;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                error = "Administrator privileges required to revert " + fullKeyPath;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static int GetServiceStartMode(string serviceName)
        {
            bool existed;
            return ReadRegistryDword(
                RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\" + serviceName,
                "Start",
                -1,
                out existed);
        }

        public bool SetServiceStartModeWithBackup(
            string tweakId,
            string serviceName,
            int newStartMode,
            bool stopServiceNow,
            out string error)
        {
            string subKey = @"SYSTEM\CurrentControlSet\Services\" + serviceName;
            bool existed;
            int currentStart = ReadRegistryDword(RegistryHive.LocalMachine, subKey, "Start", -1, out existed);
            if (!existed || currentStart == -1)
            {
                error = "Service '" + serviceName + "' is not present on this Windows edition.";
                return false;
            }

            if (!SetRegistryValueWithBackup(
                tweakId,
                RegistryHive.LocalMachine,
                subKey,
                "Start",
                newStartMode,
                RegistryValueKind.DWord,
                out error))
            {
                return false;
            }

            if (stopServiceNow)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(serviceName))
                    {
                        if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(4));
                        }
                    }
                }
                catch { }
            }

            return true;
        }

        public bool RestoreServiceStartMode(
            string tweakId,
            string serviceName,
            int fallbackStartMode,
            out string error)
        {
            string subKey = @"SYSTEM\CurrentControlSet\Services\" + serviceName;
            return RestoreRegistryValueFromBackupOrDefault(
                tweakId,
                RegistryHive.LocalMachine,
                subKey,
                "Start",
                fallbackStartMode,
                RegistryValueKind.DWord,
                false,
                out error);
        }

        public void RecordCustomStateBackup(string tweakId, string targetType, string targetPath, string originalValue, string appliedValue)
        {
            BackupItem existing = FindBaselineItem(targetType, targetPath, "");
            if (existing == null)
            {
                _baseline.Items.Add(new BackupItem
                {
                    TweakId = tweakId,
                    TargetType = targetType,
                    TargetPath = targetPath,
                    ValueName = "",
                    ValueExisted = true,
                    ValueKind = "String",
                    OriginalValue = originalValue ?? "",
                    AppliedValue = appliedValue ?? "",
                    CapturedAtUtc = DateTime.UtcNow.ToString("o")
                });
                SaveBaseline();
            }
            AppendAuditLog(string.Format("[{0}] {1} ({2}): {3} -> {4}", tweakId, targetType, targetPath, originalValue, appliedValue));
        }

        public string GetRecordedCustomState(string targetType, string targetPath, string fallbackDefault)
        {
            BackupItem item = FindBaselineItem(targetType, targetPath, "");
            if (item != null && !string.IsNullOrEmpty(item.OriginalValue))
                return item.OriginalValue;
            return fallbackDefault;
        }

        private BackupItem FindBaselineItem(string targetType, string targetPath, string valueName)
        {
            if (_baseline == null || _baseline.Items == null) return null;
            foreach (BackupItem item in _baseline.Items)
            {
                if (string.Equals(item.TargetType, targetType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.ValueName ?? "", valueName ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            return null;
        }

        public StateSnapshot CreateNamedSnapshot(string label, IList<TweakDefinition> currentTweaks = null)
        {
            EnsureDirectories();
            string id = "snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Dictionary<string, int> stateMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (currentTweaks != null)
            {
                foreach (TweakDefinition t in currentTweaks)
                {
                    if (t != null && !string.IsNullOrEmpty(t.Id))
                        stateMap[t.Id] = (int)t.CurrentStatus;
                }
            }

            StateSnapshot snap = new StateSnapshot
            {
                SnapshotId = id,
                Label = string.IsNullOrWhiteSpace(label) ? ("Snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")) : label,
                CreatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                Items = new List<BackupItem>(_baseline.Items),
                TweakStates = stateMap
            };

            string filePath = Path.Combine(_snapshotsDir, id + ".json");
            File.WriteAllText(filePath, _serializer.Serialize(snap), Encoding.UTF8);
            AppendAuditLog("Created safety snapshot: " + snap.Label + " (" + snap.Items.Count + " tracked items, " + stateMap.Count + " tweak states)");
            return snap;
        }

        public StateSnapshot FindSnapshotById(string snapshotId)
        {
            if (string.IsNullOrEmpty(snapshotId)) return null;
            foreach (StateSnapshot s in ListSnapshots())
            {
                if (string.Equals(s.SnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }

        public bool DeleteSnapshot(string snapshotId, out string message)
        {
            if (string.IsNullOrEmpty(snapshotId) || string.Equals(snapshotId, "baseline", StringComparison.OrdinalIgnoreCase))
            {
                message = "The primary Pre-Tweak System Baseline cannot be deleted.";
                return false;
            }

            try
            {
                string filePath = Path.Combine(_snapshotsDir, snapshotId + ".json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    message = "Deleted snapshot: " + snapshotId;
                    AppendAuditLog(message);
                    return true;
                }
                message = "Snapshot file not found: " + snapshotId;
                return false;
            }
            catch (Exception ex)
            {
                message = "Failed to delete snapshot: " + ex.Message;
                return false;
            }
        }

        public void ReplaySnapshotBackupItems(StateSnapshot snap, out int restoredCount, out int failedCount)
        {
            restoredCount = 0;
            failedCount = 0;
            if (snap == null || snap.Items == null) return;

            foreach (BackupItem item in snap.Items)
            {
                try
                {
                    if (string.Equals(item.TargetType, "Registry", StringComparison.OrdinalIgnoreCase))
                    {
                        RegistryHive hive;
                        string subKeyPath;
                        if (TrySplitFullRegistryPath(item.TargetPath, out hive, out subKeyPath))
                        {
                            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                            {
                                if (!item.ValueExisted)
                                {
                                    using (RegistryKey wk = baseKey.OpenSubKey(subKeyPath, true))
                                    {
                                        if (wk != null) wk.DeleteValue(item.ValueName, false);
                                    }
                                    restoredCount++;
                                }
                                else
                                {
                                    using (RegistryKey wk = baseKey.CreateSubKey(subKeyPath))
                                    {
                                        if (wk != null)
                                        {
                                            RegistryValueKind kind = ParseValueKind(item.ValueKind, RegistryValueKind.String);
                                            object val = ParseValueFromString(item.OriginalValue, kind);
                                            wk.SetValue(item.ValueName, val, kind);
                                            restoredCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (string.Equals(item.TargetType, "PowerPlan", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(item.OriginalValue))
                    {
                        string o; int c;
                        if (RunCommandHidden("powercfg.exe", "/setactive " + item.OriginalValue, out o, out c))
                            restoredCount++;
                    }
                }
                catch
                {
                    failedCount++;
                }
            }
        }

        private static bool TrySplitFullRegistryPath(string fullPath, out RegistryHive hive, out string subKeyPath)
        {
            hive = RegistryHive.CurrentUser;
            subKeyPath = "";
            if (string.IsNullOrEmpty(fullPath)) return false;

            int slash = fullPath.IndexOf('\\');
            if (slash <= 0 || slash >= fullPath.Length - 1) return false;

            string prefix = fullPath.Substring(0, slash).ToUpperInvariant();
            subKeyPath = fullPath.Substring(slash + 1);

            switch (prefix)
            {
                case "HKCU":
                case "HKEY_CURRENT_USER":
                    hive = RegistryHive.CurrentUser;
                    return true;
                case "HKLM":
                case "HKEY_LOCAL_MACHINE":
                    hive = RegistryHive.LocalMachine;
                    return true;
                case "HKCR":
                case "HKEY_CLASSES_ROOT":
                    hive = RegistryHive.ClassesRoot;
                    return true;
                case "HKU":
                case "HKEY_USERS":
                    hive = RegistryHive.Users;
                    return true;
                default:
                    return false;
            }
        }

        public List<StateSnapshot> ListSnapshots()
        {
            List<StateSnapshot> list = new List<StateSnapshot>();
            if (_baseline != null)
                list.Add(_baseline);

            try
            {
                if (Directory.Exists(_snapshotsDir))
                {
                    string[] files = Directory.GetFiles(_snapshotsDir, "*.json");
                    Array.Sort(files);
                    Array.Reverse(files);
                    foreach (string file in files)
                    {
                        try
                        {
                            StateSnapshot snap = _serializer.Deserialize<StateSnapshot>(File.ReadAllText(file, Encoding.UTF8));
                            if (snap != null) list.Add(snap);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return list;
        }

        public bool CreateWindowsSystemRestorePoint(string description, out string message)
        {
            try
            {
                ManagementScope scope = new ManagementScope(@"\\localhost\root\default");
                ManagementPath path = new ManagementPath("SystemRestore");
                ObjectGetOptions options = new ObjectGetOptions();
                using (ManagementClass srClass = new ManagementClass(scope, path, options))
                using (ManagementBaseObject inParams = srClass.GetMethodParameters("CreateRestorePoint"))
                {
                    inParams["Description"] = string.IsNullOrWhiteSpace(description) ? "PCTweaker Safety Restore Point" : description;
                    inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                    inParams["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE

                    ManagementBaseObject outParams = srClass.InvokeMethod("CreateRestorePoint", inParams, null);
                    uint ret = Convert.ToUInt32(outParams["ReturnValue"] ?? 1);
                    if (ret == 0)
                    {
                        message = "Windows System Restore Point created successfully.";
                        AppendAuditLog(message);
                        return true;
                    }
                    else
                    {
                        message = "SystemRestore returned code " + ret + " (ensure System Restore is enabled on drive C: and app is running as Administrator).";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                message = "Could not create Windows System Restore Point: " + ex.Message;
                return false;
            }
        }

        public static bool RunCommandHidden(string fileName, string arguments, out string stdOut, out int exitCode)
        {
            stdOut = "";
            exitCode = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    stdOut = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                    exitCode = p.ExitCode;
                    return exitCode == 0;
                }
            }
            catch (Exception ex)
            {
                stdOut = ex.Message;
                return false;
            }
        }

        private static string GetHivePrefix(RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.CurrentUser: return "HKCU";
                case RegistryHive.LocalMachine: return "HKLM";
                case RegistryHive.ClassesRoot: return "HKCR";
                case RegistryHive.Users: return "HKU";
                default: return hive.ToString();
            }
        }

        private static string ConvertValueToString(object val)
        {
            if (val == null) return "";
            if (val is byte[]) return Convert.ToBase64String((byte[])val);
            return Convert.ToString(val);
        }

        private static RegistryValueKind ParseValueKind(string kindStr, RegistryValueKind fallback)
        {
            try
            {
                return (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kindStr, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static object ParseValueFromString(string str, RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    int d;
                    return int.TryParse(str, out d) ? d : 0;
                case RegistryValueKind.QWord:
                    long q;
                    return long.TryParse(str, out q) ? q : 0L;
                case RegistryValueKind.Binary:
                    try { return Convert.FromBase64String(str); } catch { return new byte[0]; }
                default:
                    return str ?? "";
            }
        }
    }
}
