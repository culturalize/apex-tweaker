# Security & Anti-Cheat Safety Policy

## Anti-Cheat Compliance by Design
**Apex Tweaker** is engineered to be 100% compatible with modern kernel-level anti-cheat systems — including **EasyAntiCheat (EAC)**, **BattlEye**, **Riot Vanguard**, **FACEIT AC**, and **Ricochet**:

1. **Zero Process Injection or Memory Tampering** — Never calls `WriteProcessMemory`, `CreateRemoteThread`, or injects DLLs into any game or anti-cheat binary.
2. **Zero Global Input Hooks or Overlay Hijacking** — Never registers `WH_MOUSE_LL`, `WH_KEYBOARD_LL`, or `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, and never spawns `WS_EX_TOPMOST` overlays or triggers `ChangeDisplaySettingsW` during game launch, preventing DXGI Exclusive Fullscreen swapchain stalls.
3. **Zero Vulnerable Third-Party Kernel Drivers** — Never ships or loads unsigned `.sys` kernel drivers (such as `WinRing0`).
4. **Kernel-Native Process Priority (`IFEO PerfOptions`)** — High CPU and I/O priority for games is applied through documented Windows `Image File Execution Options\<Game.exe>\PerfOptions` registry entries so the Windows kernel itself grants priority at process launch without opening runtime handles to anti-cheat-protected binaries.
5. **Protected Process Allowlist** — The Game Session Booster and Memory Purger explicitly exclude all Windows critical processes, game launchers, Discord/OBS, and anti-cheat daemons (`EasyAntiCheat*`, `BEService`, `vgc`, `vgk`, `faceit*`, `PnkBstr*`).

## Transactional Rollback Guarantee (`SafetyVault`)
Every registry value, service start mode, power scheme GUID, or game `.ini` file edited by Apex Tweaker is captured prior to modification:
- System baselines and point-in-time JSON snapshots are saved under `%APPDATA%\PCTweaker\`.
- Unreal Engine `GameUserSettings.ini` edits automatically create a `.pctweaker.bak` backup alongside the original file.
- Full source code is auditable and compiles directly from source using Windows' built-in `.NET Framework 4.8` C# compiler via `build.bat`.

## Reporting a Vulnerability
If you find a bug, regression, or game compatibility issue, please open a GitHub Issue with your Windows build number and reproduction steps.
