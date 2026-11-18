# PC Tweaker for Gamers & Low-Spec PCs — Technical Architecture & UI/UX Design Plan

> **Two-Stage Execution Workflow**
> 1. **Stage 1 (Completed by Argon — Technical Core & Barebones Functional Shell)**: Complete technical system architecture, exhaustive 36-tweak catalog engineering, transactional backup/rollback vault (`SafetyVault`), native `ntdll.dll` memory/standby-list purger, anti-cheat-safe game booster, Unreal Engine config optimizer, and a **strictly barebones WinForms UI** (`src/UI/MainForm.cs`) wired to a clean `TweakerController` API (`src/Core/TweakerController.cs`).
> 2. **Stage 2 (Next — Design Model UI/UX Transformation)**: The design-specialized model should transform `src/UI/MainForm.cs` and `src/UI/Theme.cs` (and optionally add custom WinForms controls in `src/UI/`) following **Section 4** below, without breaking any technical engines in `src/Core/`. Run `build.bat` to compile `PCTweaker.exe`.

---

## 1. Engineering Philosophy & Non-Negotiable Guardrails

1. **100% Anti-Cheat Safe by Construction (EAC, BattlEye, Vanguard, FACEIT AC, Ricochet)**
   - **Zero process injection or memory tampering**: Never calls `WriteProcessMemory`, `CreateRemoteThread`, or installs global `WH_MOUSE_LL` / `WH_KEYBOARD_LL` / `SetWinEventHook` hooks.
   - **Zero exclusive-fullscreen contention**: Never spawns `WS_EX_TOPMOST` overlays or broadcasts `WM_DISPLAYCHANGE` during game bootstrap.
   - **Zero vulnerable third-party kernel drivers**: Never loads `WinRing0` or unsigned `.sys` drivers.
   - **Kernel-Native Process Priority via IFEO (`PerfOptions`)**: Priority boosts use `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\<Game.exe>\PerfOptions` (`CpuPriorityClass = 3`, `IoPriority = 3`), instructing the Windows kernel itself to spawn the game with high priority.

2. **Transactional State Backup & Instant Rollback (`SafetyVault`)**
   - Every tweak is atomic and reversible. Before modifying any registry value, Windows service startup mode, scheduled task, BCD flag, or power scheme setting, `BackupAndRollbackEngine` queries and serializes the **exact pre-tweak state** (including a `ValueExisted: false` sentinel if a registry value did not previously exist) to `%APPDATA%\PCTweaker\Snapshots\`.

3. **Hardware-Aware Tier Adaptation (Potato PC vs. Competitive Rig)**
   - `HardwareDetector` automatically classifies the host system into one of three tiers (`PotatoLowSpec`, `MidRangeGamer`, `CompetitiveEsports`) and tailors recommendations and presets accordingly.

---

## 2. Project File Map

```text
apex-tweaker/
├── PLAN.md                             # This Master Technical & UI/UX Plan
├── build.bat                           # Zero-dependency 64-bit C# compiler script (csc.exe -> PCTweaker.exe)
├── app.manifest                        # Per-Monitor DPI-Aware manifest
└── src\
    ├── Program.cs                      # Entry point (GUI + headless CLI flags: --audit, --purge-ram, --apply-preset, --revert-all)
    ├── Core\
    │   ├── Models.cs                   # Domain models: HardwareProfile, SystemTier, TweakDefinition, StateSnapshot, DetectedGame, TelemetrySample
    │   ├── NativeInterop.cs            # P/Invoke declarations: ntdll.dll, psapi.dll, kernel32.dll, advapi32.dll
    │   ├── HardwareDetector.cs         # WMI + Registry hardware profiling & automatic PC Tier Classifier
    │   ├── BackupAndRollbackEngine.cs  # Transactional SafetyVault: pre-tweak snapshots, JSON rollback & Restore Point creator
    │   ├── TweakCatalog.cs             # 36 audited tweaks across 6 categories with real live state detection, apply, and revert logic
    │   ├── MemoryCleanerEngine.cs      # Native Standby List purge (NtSetSystemInformation), Working Set trim & Shader/Temp cache cleaner
    │   ├── GameBoosterEngine.cs        # Anti-cheat-safe live session booster + IFEO kernel PerfOptions manager
    │   ├── GameConfigOptimizer.cs      # Auto-discovers installed games (UE4/UE5 GameUserSettings.ini + launchers) + Potato/Competitive configs
    │   ├── SystemTelemetryEngine.cs    # Live low-overhead CPU, RAM, Timer Resolution & Optimization Score telemetry
    │   └── TweakerController.cs        # Clean facade API connecting Core technical systems to the UI layer
    └── UI\
        ├── Theme.cs                    # Tokenized Theme palette & metrics contract ready for the Design Model
        └── MainForm.cs                 # Barebones functional WinForms UI (Stage 1) -> Target for Design Model upgrade (Stage 2)
```

---

## 3. UI/UX Design Plan for Stage 2 (Design Model Specification)

### 3.1 Visual Identity & Aesthetic Direction
- **Theme Persona**: *"Tactical Hardware Telemetry"* — high-contrast dark mode with crisp card hierarchy, zero visual clutter, plain-English explanations for beginners, and live hardware telemetry for enthusiasts.
- **Color Palette Tokens (`src/UI/Theme.cs`)**:
  - `BgCanvas`: `#0A0D14` (Deep Carbon Obsidian)
  - `BgSidebar`: `#0F131D` (Tactical Slate)
  - `BgCard`: `#151B28` (Elevated Panel Surface)
  - `BgCardHover`: `#1C2436` (Interactive Card Hover)
  - `BorderSubtle`: `#232C40` (1px Hairline Divider)
  - `BorderActive`: `#00E5FF` (Active Selection / Focus Glow)
  - `TextPrimary`: `#F3F6FC` (Crisp Alabaster)
  - `TextSecondary`: `#9BA6BC` (Muted Steel)
  - `TextMuted`: `#637089` (Dim Telemetry Label)
  - `AccentPrimary`: `#00E5FF` (Electric Cyan — Primary Actions & Active Highlights)
  - `AccentSafe`: `#10B981` (Emerald Green — `APPLIED` badge, `SAFE` risk level, High Optimization Score)
  - `AccentWarning`: `#F59E0B` (Amber Gold — `MODERATE` risk level, `REBOOT REQUIRED` pill, Potato Preset accent)
  - `AccentDanger`: `#EF4444` (Crimson Coral — `ADVANCED` risk level, High Memory Pressure)

### 3.2 Layout Architecture (`1180 x 760` Default Window, Resizable, DoubleBuffered)
1. **Left Navigation Rail (`230px` width)**:
   - Brand Header + Detected Hardware Tier Badge (`POTATO RESCUE`, `MID-RANGE GAMER`, or `COMPETITIVE ESPORTS`).
   - 6 Navigation Buttons with active indicator pill and tweak count badges:
     1. **Dashboard & Quick Boost**
     2. **System Tweaks (36)**
     3. **Game Booster & RAM Purge**
     4. **Game Config Optimizer**
     5. **Safety Vault & Rollback**
     6. **Hardware & System Audit**
   - Bottom Footer Pill showing **Administrator Mode Status** (`ELEVATED` vs `STANDARD USER`) and **`100% ANTI-CHEAT SAFE`** shield badge.

2. **Persistent Top Telemetry Bar (`76px` height)**:
   - 4 Live Metric Cards updated by `TweakerController.SampleTelemetry()`:
     - **CPU Load %** (with mini progress bar)
     - **RAM Usage (`Used / Total MB` + `Free MB`)**
     - **Kernel Timer Resolution (`Current ms`, e.g., `0.50 ms` or `1.00 ms` vs `15.62 ms`)**
     - **Optimization Score (`0% – 100%`)**
   - Quick Action Button: **`Quick Purge RAM`** (`Controller.PurgeMemory(false)`).

3. **View 1 — Dashboard & 1-Click Presets**:
   - **Hardware Summary Banner**: CPU, GPU, VRAM, RAM, Storage Type, OS Build, and recommended profile explanation.
   - **4 Hero Preset Cards**:
     - **`Potato PC Rescue (Max FPS & RAM Saver)`** -> `Controller.ApplyPreset(PresetProfile.PotatoRescue)`
     - **`Competitive FPS & Ultra-Low Latency`** -> `Controller.ApplyPreset(PresetProfile.CompetitiveFps)`
     - **`Balanced Safe Gaming`** -> `Controller.ApplyPreset(PresetProfile.BalancedSafe)`
     - **`Restore Windows Stock Baseline`** -> `Controller.RevertAllTweaks()`

4. **View 2 — System Tweaks Explorer**:
   - Category Filter Pills (`All`, `Recommended`, `CPU & Power`, `GPU & Display`, `Memory & Storage`, `Network & Ping`, `Input & Mouse`, `Bloat & Services`) + Search Box.
   - Custom Tweak Cards displaying:
     - Interactive Toggle Switch (`TweakStatus.Applied` vs `TweakStatus.Default`) calling `Controller.ToggleTweak(tweak.Id)`
     - Title & Plain-English `ShortSummary`
     - Impact Badges (`FPS`, `LATENCY`, `RAM`), `RiskLevel` pill (`SAFE`, `MODERATE`, `ADVANCED`), and `RequiresReboot` pill
     - Monospace `TechnicalDetails` line showing the exact registry key / system command.

5. **View 3 — Live Game Booster & RAM Cleaner**:
   - Big Hero Toggle Card: **`ENGAGE GAME SESSION BOOST`** / **`RESTORE NORMAL DESKTOP MODE`** (`Controller.ToggleGameBoostSession()`).
   - Native `ntdll` Memory Purger Card (`Controller.PurgeMemory(false)`) and DirectX/GPU Shader & Temp Cache Cleaner (`Controller.CleanCaches()`).

6. **View 4 — Game Config & Engine Optimizer**:
   - Auto-detected games list (`Controller.DetectedGames`) with per-game buttons:
     - Toggle **Kernel High CPU/IO Priority (IFEO)** (`Controller.ToggleGameHighPriority(game)`)
     - Toggle **High-Performance GPU & DXGI Flip Preference** (`Controller.ToggleGameGpuPreference(game)`)
     - Apply **Unreal Engine Potato Max-FPS `.ini` Preset** (`Controller.ApplyUnrealConfigPreset(game, true)`)
     - Apply **Unreal Engine Competitive Clarity `.ini` Preset** (`Controller.ApplyUnrealConfigPreset(game, false)`)
     - **Restore Original `.ini` Backup** (`Controller.RestoreUnrealConfigBackup(game)`)

7. **View 5 — Safety Vault & Rollback Center**:
   - Buttons: **Create Windows System Restore Point** (`Controller.CreateSystemRestorePoint()`), **Save Manual Snapshot** (`Controller.CreateManualSnapshot()`), **Revert All Tweaks** (`Controller.RevertAllTweaks()`), **Export Audit Log**.
   - List of saved JSON Snapshots (`Controller.GetSnapshots()`) with 1-click **Restore Selected Snapshot** (`Controller.RestoreSnapshot(id)`).

8. **View 6 — Hardware & Diagnostic Report**:
   - Complete read-only diagnostic breakdown and 1-click **Copy Diagnostic Report to Clipboard** (`Controller.GenerateDiagnosticReport()`).


---

## 4. Stage 3 UI Handoff Specification for Neon (Backend Hooks Ready in `src/Core/`)

Argon has implemented and verified all 4 backend technical additions in `src/Core/`. **Neon** should now update `src/UI/TacticalControls.cs` and `src/UI/MainForm.cs` to surface them visually:

### 4.1 View 3 (`StackedMemoryBar` in `TacticalControls.cs` & `UpdateTelemetryDisplay()` in `MainForm.cs`) — 3-Segment RAM Bar
- **New `TelemetrySample` Properties Available**:
  - `long UsedRamMb` (Active In-Use Working Sets)
  - `long StandbyCacheMb` (Windows Standby Page List / System Cache)
  - `long FreeRamMb` (Pure Zeroed / Free Physical RAM)
  - Guaranteed invariant: `UsedRamMb + StandbyCacheMb + FreeRamMb == TotalRamMb`.
- **UI Task for Neon**:
  - Add `public long StandbyCacheMb { get; set; }` and `public long FreeRamMb { get; set; }` to `StackedMemoryBar`.
  - Render **3 distinct visual segments** inside `StackedMemoryBar.OnPaint`:
    1. **In-Use RAM**: `AccentPrimary` (`#00E5FF`) / `AccentWarning` / `AccentDanger` based on load %.
    2. **Standby Cache**: `AccentIndigo` (`#6366F1` — purple/indigo tint) immediately to the right of In-Use RAM.
    3. **Pure Free RAM**: `AccentSafe` (`#10B981` — emerald tint) filling the remainder.
  - Update the bottom legend row of `StackedMemoryBar` to show all 3 color dots:
    - `● Used: X.X GB`
    - `● Standby: X.X GB`
    - `● Free: X.X GB`

### 4.2 View 4 (`GameOptimizerCard` in `TacticalControls.cs` & `RebuildGameCards()` in `MainForm.cs`) — Per-Game Fullscreen Optimizations (FSO) Toggle
- **New `TweakerController` Method Available**:
  - `bool ToggleGameFullscreenOptimizations(DetectedGame game, out string message)`
  - Reads/toggles `game.IsFullscreenOptDisabled` (`~ DISABLEDXMAXIMIZEDWINDOWEDMODE` in `HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`).
  - Also note: `GameConfigOptimizer.DiscoverInstalledGames()` automatically scans Windows `GameConfigStore` and `%LOCALAPPDATA%` Unreal Engine configs to discover installed games and auto-populate `game.ExecutablePath` on disk.
- **UI Task for Neon**:
  - Add a `BtnFso` (`TacticalButton`) to `GameOptimizerCard` next to `BtnGpuPref`:
    - When `game.IsFullscreenOptDisabled == true`: Text = `"Exclusive FSO Off: ON"`, `ButtonStyle = TacticalButtonStyle.Safe` + top-right pill badge `"FSO DISABLED"`.
    - When `game.IsFullscreenOptDisabled == false`: Text = `"Disable Fullscreen Opt"`, `ButtonStyle = TacticalButtonStyle.Secondary`.
    - Wire `FsoClicked` in `MainForm.RebuildGameCards()` to call `_controller.ToggleGameFullscreenOptimizations(game, out msg)` (prompting with `OpenFileDialog` if `game.ExecutablePath` is empty, just like `GpuPrefClicked`).

### 4.3 View 5 (`SnapshotItemCard` in `TacticalControls.cs` & `RebuildVaultCards()` in `MainForm.cs`) — Live Point-in-Time Snapshot Restore & Delete
- **New `TweakerController` Methods Available**:
  - `bool RestoreSnapshot(string snapshotId, out string message)` — replays all `BackupItem`s and reconciles all 36 `TweakStates` stored in the JSON snapshot.
  - `bool DeleteSnapshot(string snapshotId, out string message)` — deletes any non-baseline snapshot JSON file.
- **UI Task for Neon**:
  - In `MainForm.RebuildVaultCards()`, replace the placeholder message box on `card.RestoreClicked` for named snapshots with a confirmation dialog that runs `_controller.RestoreSnapshot(selectedSnap.SnapshotId, out msg)` via `RunAsyncAction`.
  - On `SnapshotItemCard`, for non-baseline snapshots (`Snapshot.SnapshotId != "baseline"`), add a compact **`Delete`** button (`TacticalButtonStyle.Danger`, `Width = 68`) to the left of `BtnRestore` wired to `_controller.DeleteSnapshot(selectedSnap.SnapshotId, out msg)`.
