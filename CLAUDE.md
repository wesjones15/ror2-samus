# Samus RoR2 host rules

- Follow ../CLAUDE.md and ../libssbm/CLAUDE.md: reimplement from decomp; cite it;
  no Nintendo data in Git/packages. First-run extraction from the player's disc.
- Match Mario/Olimar: the C# plugin invokes native DLL extraction on a worker.
  Release packages contain only mod DLLs, docs and a checksum manifest; no
  extractor executables or setup scripts. CLI extractors stay development-only.
- Rust owns fighter behavior. C# owns Unity/RoR2 world, item/skill adaptation,
  input bindings and unit conversion. No Unity types in libssbm.
- Use gc2ror2 MCP common_library/find_recipe before inventing shared mechanisms.
  Compile ../ror2-common/src into this plugin; no separate shared DLL.
- Offline prototype. Do not claim multiplayer support or in-game verification
  from a successful build. State unfinished fidelity/integration work in README.
- Compare native ABI and every shared struct size before using a handle. Pin
  callback delegates; prevent C# exceptions from crossing unmanaged callbacks.
- Keep a runnable .NET native-boundary harness, build against the installed DLC4
  assemblies and commit each verified working increment.
- User preference: replace the installed DLL pair directly after checking the
  game is closed. Do not create DLL backups or retain rollback DLL copies.
