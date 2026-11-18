# Changelog

All notable changes to **Apex Tweaker** are documented in this file.

## [v1.0.0] — 2026-11-18

### Highlights
- **Initial Public Release** of Apex Tweaker — a zero-dependency Windows 10/11 gaming & low-spec PC optimizer compiled natively to a standalone ~150 KB x64 executable.

### Core Technical Engines (`src/Core/`)
- **64-Bit Hardware Profiler & Automatic Tier Classifier**: Detects CPU vendor/cores/threads/clock, true 64-bit GPU VRAM via Display Class registry (`HardwareInformation.qwMemorySize`), HAGS status, RAM, NVMe/SATA/HDD storage, and laptop/desktop chassis, classifying PCs into **Tier 1: Potato Rescue**, **Tier 2: Mid-Range Gaming**, or **Tier 3: Competitive Esports**.
- **36 Audited Windows & Gaming Tweaks (6 Subsystems)**: Live state detection, atomic apply, and exact baseline rollback across *CPU & Power*, *GPU & Display*, *Memory & Storage*, *Network & Ping*, *Input & Mouse*, and *Bloat & Services*.
- **Transactional SafetyVault & Point-in-Time Snapshots**: Records exact pre-modification state (including non-existent key sentinels), supports Windows System Restore Point creation, and provides full point-in-time `RestoreSnapshot` and `DeleteSnapshot` management.
- **Native `ntdll.dll` Standby List & Working Set Memory Purger**: Reclaims Windows Standby Page List and trims background working sets (`OpenProcess` + `EmptyWorkingSet` + `NtSetSystemInformation`), plus safe Temp and DirectX/NVIDIA/AMD Shader Cache cleaning.
- **Anti-Cheat-Safe Live Session Game Booster & Kernel IFEO Priority Manager**: Switches power plans, pauses safe desktop bloat/indexing services, purges standby RAM, and configures kernel-native `IFEO\PerfOptions` high CPU/I/O priority.
- **Game Config & Engine Optimizer**: Auto-discovers installed games via Windows `GameConfigStore` and `%LOCALAPPDATA%` Unreal Engine configs, links `.exe` binaries on disk, toggles DirectX High-Performance GPU Preference and Fullscreen Optimizations (`DISABLEDXMAXIMIZEDWINDOWEDMODE`), and applies **Potato Max-FPS** or **Competitive Clarity** `GameUserSettings.ini` presets with automatic `.pctweaker.bak` backups.
- **Live Kernel Timer Resolution & 3-Segment RAM Telemetry**: Sub-millisecond overhead sampling of CPU load, 3-segment RAM (`Used` / `Standby Cache` / `Free`), kernel timer resolution (`NtQueryTimerResolution`), and Optimization Score.

### User Interface (`src/UI/`)
- **Tactical Hardware Telemetry UI**: Double-buffered custom dark WinForms interface with native Windows 11 dark titlebar & scrollbars, persistent live telemetry header bar, radial optimization score gauge, 1-click preset cards, searchable/expandable tweak cards with FPS/Latency/RAM impact meters, 3-segment stacked memory visualizer, searchable game config cards, Safety Vault snapshot manager, and full diagnostic audit viewer.
