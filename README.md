# Apex Tweaker — Gaming & Low-Spec PC Performance Suite

A lightweight, **zero-dependency**, **100% Anti-Cheat Safe** Windows 10/11 performance & frame-rate tweaker built for both low-spec ("Potato") PCs and competitive esports rigs.

- **Single standalone executable** (~150 KB, compiles natively with Windows' built-in `.NET Framework 4.8` C# compiler — no Electron, no Python, no external runtime installs).
- **100% Reversible (`SafetyVault`)**: Captures your exact pre-tweak registry, service, and power-plan values before touching anything, with 1-click baseline rollback, point-in-time JSON snapshots, and Windows System Restore Point creation.
- **100% Anti-Cheat Safe**: Compliant with **EasyAntiCheat (EAC)**, **BattlEye**, **Riot Vanguard**, **FACEIT AC**, and **Ricochet** — zero DLL injection, zero input hooks, zero fullscreen overlay hijacking, zero third-party kernel drivers.

---

## Download & Quick Start

### Option 1 — Download Pre-Built Executable (Recommended)
1. Go to the [**Latest GitHub Release**](https://github.com/culturalize/apex-tweaker/releases/latest).
2. Download **`PCTweaker.exe`** (standalone app) or **`ApexTweaker-v1.0.0-Windows-x64.zip`** (full bundle + source + `build.bat`).
3. Double-click **`PCTweaker.exe`** to launch.
   - Standard user mode runs all `HKCU` user tweaks, game `.ini` optimizations, and working-set RAM purges.
   - Click **Run as Administrator** in the bottom-left sidebar whenever you want to apply machine-level tweaks (`HKLM`, Services, `bcdedit`, IFEO priority, or Kernel Standby Page List purges).

### Option 2 — Build From Source (2 Seconds, Zero Dependencies)
```bat
git clone https://github.com/culturalize/apex-tweaker.git
cd apex-tweaker
.\build.bat
.\PCTweaker.exe
```

---

## Key Features

### 1. Hardware-Aware Tier Detection & 1-Click Presets
Automatically profiles your CPU, true 64-bit GPU VRAM (`HardwareInformation.qwMemorySize`), HAGS status, RAM, NVMe/SATA/HDD storage, and chassis, classifying your PC into one of three tiers:
- **Tier 1 — Potato PC Rescue**: Reclaims DWM VRAM (`EnableTransparency=0`), consolidates `svchost.exe` memory overhead, freezes background UWP apps, disables GameDVR/SysMain/Search indexing spikes, and generates ultra-low Unreal Engine configs.
- **Tier 2 — Balanced Safe Gaming**: Applies every zero-downside Safe gaming optimization while keeping Windows Search and desktop visuals untouched.
- **Tier 3 — Competitive FPS & Ultra-Low Latency**: Unlocks Ultimate Performance power plan, unparks CPU cores, sets `Win32PrioritySeparation=0x26`, elevates MMCSS gaming thread & GPU priority, disables multimedia network throttling & Nagle's algorithm, and enforces true 1:1 mouse linearity.

### 2. 36 Audited Technical Tweaks (6 Subsystems)
Every tweak performs a **live real-time audit** against your PC so you see whether it is currently `APPLIED` or `DEFAULT`, along with FPS/Latency/RAM impact meters, risk badges, and an expandable **Technical Details** drawer showing the exact registry key or Windows command:
- **CPU & Power (8)**: Ultimate Performance scheme, CPU Core Parking unpark, `Win32PrioritySeparation (0x26)`, MMCSS gaming profile, EcoQoS Power Throttling off, Kernel Dynamic Tick, PCIe ASPM L0 lock, USB Selective Suspend off.
- **GPU & Display (8)**: GameDVR & AppCapture pipeline off, DXGI Exclusive Fullscreen mode, DX10/DX11 Windowed Flip Model & VRR upgrade, HAGS toggle, DWM transparency VRAM saver, desktop visual animations off, Multi-Plane Overlay (MPO) stutter fix, Game Mode ON + GameBar bloat off.
- **Memory & Storage (6)**: Dynamic `SvcHostSplitThresholdInKB` consolidation, `DisablePagingExecutive`, SysMain (Superfetch) disable, NTFS Last-Access & 8.3 short-name IOPS reduction, SSD prefetcher trace disable.
- **Network & Ping (5)**: `NetworkThrottlingIndex = 0xFFFFFFFF`, per-NIC Nagle's Algorithm disable (`TcpAckFrequency=1`, `TCPNoDelay=1`), QoS 20% bandwidth unlock, TCP RSS enable / RSC disable, Windows Update P2P seeding disable.
- **Input & Mouse (4)**: True 1:1 linear mouse aim, `MouseDataQueueSize` & `KeyboardDataQueueSize` ring-buffer tuning (`32`), StickyKeys/FilterKeys/ToggleKeys hook removal.
- **Bloat & Services (5)**: `DiagTrack` & `dmwappushservice` telemetry disable, Windows Search Indexer manual mode, UWP background app freeze, `CompatTelRunner` & CEIP scheduled task disabler, Hibernation / Fast Startup cleanup.

### 3. Live Game Booster & Native `ntdll.dll` Standby Memory Purger
- **3-Segment Physical Memory Visualizer**: Real-time breakdown of **In-Use RAM**, **Windows Standby Cache**, and **Pure Free RAM**.
- **Native RAM Purger**: Trims non-critical background working sets (`psapi.dll` `EmptyWorkingSet`) and flushes Kernel Modified & Standby Page Lists (`ntdll.dll` `NtSetSystemInformation`) to eliminate low-RAM stuttering.
- **Shader & Temp Cache Cleaner**: Safely removes stale `%TEMP%`, `%WINDIR%\Temp`, DirectX (`D3DSCache`), NVIDIA (`DXCache`/`GLCache`), and AMD (`DxCache`) cache files while skipping active locked files.
- **Explicit Session Boost Mode**: Temporarily activates High/Ultimate Performance power, closes safe desktop bloat (never touches Steam, Epic, Riot, Discord, OBS, or Anti-Cheats), pauses background indexing services, and restores everything cleanly when you finish playing.

### 4. Game Config & Engine Optimizer
- Automatically discovers installed games via Windows `GameConfigStore` and `%LOCALAPPDATA%` Unreal Engine configs (plus a **Browse Custom Game `.exe`** button).
- **1-Click Kernel High CPU/IO Priority (`IFEO PerfOptions`)**: Tells the Windows kernel to launch the game with High CPU & I/O priority without user-mode process handle tampering.
- **Per-Game DirectX High-Performance GPU Preference** (`GpuPreference=2`) & **Fullscreen Optimizations Toggle** (`DISABLEDXMAXIMIZEDWINDOWEDMODE`).
- **Unreal Engine `GameUserSettings.ini` Presets**: Apply **Potato Max-FPS** or **Competitive Clarity** scalability settings while automatically preserving a `.pctweaker.bak` backup for 1-click restoration.

### 5. Safety Vault & Rollback Center
- Saves your original pre-tweak baseline and named point-in-time JSON snapshots (including exact `TweakStates` of all 36 tweaks) under `%APPDATA%\PCTweaker\`.
- 1-click **Create Windows System Restore Point**, **Save Manual Snapshot**, **Restore Snapshot**, **Delete Snapshot**, and **Revert All Tweaks to Baseline**.

---

## Command-Line Flags (Automation & Diagnostics)
- `PCTweaker.exe` — Launches the desktop application.
- `PCTweaker.exe --audit` — Runs a headless hardware, telemetry, and 36-tweak audit and writes `latest-audit-report.txt`.
- `PCTweaker.exe --purge-ram` — Performs an immediate Working Set & Kernel Standby List RAM purge.
- `PCTweaker.exe --self-test` — Verifies hardware profiling, 36-tweak live audit, SafetyVault transactional apply/revert, point-in-time snapshot replay, native RAM purging, 3-segment memory telemetry, and game discovery.

---

## Security & License
- See [`SECURITY.md`](./SECURITY.md) for our Anti-Cheat Safety Policy and rollback guarantees.
- Released under the [MIT License](./LICENSE).
