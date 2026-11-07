# Development

Source layout, builds and checks for `ror2-samus`. Run the commands below from the repository root. Player installation, controls and configuration are in the [README](../README.md).

## Repository relationships

| Repository | Role |
| --- | --- |
| **ror2-samus** (this repository) | Roster, fighter definitions/drivers, combat adapters, configuration, native-boundary harness and Melee packaging. |
| [libssbm](https://github.com/wesjones15/libssbm) | Native simulation, asset readers, presentation, audio, C API and replay. |
| [ror2-common](https://github.com/wesjones15/ror2-common) | Shared C# driver, input, skills/items, defense, capture, rendering, UI, audio, networking and VR; compiled into this plugin. |
| [libcommon](https://github.com/wesjones15/libcommon) | Shared Rust utilities used by the native library. |
| [ror2-characters](https://github.com/wesjones15/ror2-characters) | Related character project and player documentation. This repository's build script packages Melee. |

The [native reuse notes](https://github.com/wesjones15/libssbm/blob/main/docs/reuse.md) describe the shared interfaces and ownership across the native and C# layers.

## Build and checks

Use Windows, PowerShell 5.1, Rust supporting edition 2024, a .NET SDK and local DLC4/BepInEx assemblies. Keep `ror2-samus` and `libssbm` on `main`, with `ror2-common`, `libcommon` and the viewer's `libsms` dependency on `modern/1.5.0`, as sibling checkouts.

From this repository:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -RoR2Dir "C:\path\to\Risk of Rain 2"
```

The script builds, checks and packages the plugin under `dist/`, with progress recorded in `dist/build.log`. `-Deploy` additionally stages `Samus.dll`, `ssbm.dll` and SharedFiles beneath the workspace's `target/BepInEx/plugins/` for manual copying into the intended game/profile. The default compile path is the workspace's `2026 Mod Stuff/RiskOfRain2_DLC4` copy.

The normal build includes startup, pad, shared-host and native-boundary checks. An unchanged native tree can reuse its previous successful Rust-test stamp; `-ForceTests` reruns it. `-SkipTests` is available for iteration builds and skips Rust/native-host checks.

For a focused input check:

```powershell
dotnet run --project PadCheck -c Release
```

Run native checks from `libssbm/` with `cargo test --release`. Disc-dependent checks report skips when local assets are absent. Manual gameplay, balance, visual/audio and headset checks remain for recent changes. Current tuning and per-fighter follow-up work are recorded in the [implementation audit](https://github.com/wesjones15/libssbm/blob/main/docs/first-pass-audit.md).
