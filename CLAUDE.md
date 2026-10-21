# Samus RoR2 host rules

- Follow ../CLAUDE.md and ../libssbm/CLAUDE.md: reimplement from decomp; cite it;
  no Nintendo data in Git/packages. First-run extraction from the player's disc.
- Match Mario/Olimar: the C# plugin invokes native DLL extraction on a worker.
  Release packages contain only mod DLLs, docs and a checksum manifest; no
  extractor executables or setup scripts. CLI extractors stay development-only.
- Rust owns fighter behavior. C# owns Unity/RoR2 world, item/skill adaptation,
  input bindings and unit conversion. No Unity types in libssbm.
- Do not use gc2ror2-mcp (../AGENTS.md forbids it). Before inventing a shared
  mechanism, look in ../ror2-common/src (MeleeDriver, MeleeBouncingShot, MeleeHitTracker,
  MeleeSheet, EnemyCapture, NativeHit) and reuse it.
  Compile ../ror2-common/src into this plugin; no separate shared DLL.
- Fighter status and the ordered backlog live in ../libssbm/docs/first-pass-audit.md;
  update it when a fighter's behavior changes.
- `build.ps1 -Deploy` runs the native C# boundary checks first and stages nothing if one
  fails. When a libssbm change alters behavior, update the matching check in NativeCheck.
- Offline prototype. Do not claim multiplayer support or in-game verification
  from a successful build. State unfinished fidelity/integration work in README.
- Compare native ABI and every shared struct size before using a handle. Pin
  callback delegates; prevent C# exceptions from crossing unmanaged callbacks.
- Keep a runnable .NET native-boundary harness, build against the installed DLC4
  assemblies and commit each verified working increment.
- User preference: stage all future deploys in ../target/BepInEx/plugins/Melee/.
  The user moves the files into the game install. No game-close request is needed.
  Keep one staged DLL pair; do not create rollback DLL copies.
