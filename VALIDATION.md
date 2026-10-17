# Samus prototype verification — 2026-10-16

- Used the local gc2ror2 server over its stdio MCP protocol for guides, disc
  inspection, port planning, file inventory/extraction and shared-host recipes.
- Melee GALE01 rev 2 decomp rebuilt with local PowerPC tools. Both input and
  rebuilt main.dol hash: 08e0bf20134dfcb260699671004527b2d6bb1a45.
- 17 Rust tests pass, including synthetic ISO/CISO and corrupt DAT handling,
  fixed-step gameplay scenarios, real costumes/clip poses and eleven ABI layouts.
- `cargo clippy --workspace --all-targets --offline -- -D warnings` passes.
- Standalone viewer rendered and screenshot inspected after correcting envelope
  matrix handling. Real model, texture and Wait animation verified visually.
- C# NativeCheck calls the real release DLL through Samus/Native.cs; all eleven
  struct layouts, callbacks, movement events, five costumes, indices and textures pass.
- BepInEx plugin compiles against installed RoR2 DLC4 assemblies. One MSB3270
  metadata architecture warning remains: the installed BepInEx reference is
  I386/ILOnly with Required32Bit, while Samus is I386/ILOnly without Required32Bit.
  Compatibility with the game's Mono loader has not been verified in-game; the
  game/BepInEx assemblies were not modified to suppress this warning.
- Packaged setup/extractor ran against the user's disc into an ignored empty
  cache. All eight output files match gc2ror2 MCP extraction by SHA-256.
- Release ZIP allowlist audited: Samus.dll, ssbm.dll, ssbm-extract.exe,
  setup-assets.ps1, README.md and CHANGELOG.md only; no game/disc data.
- gc2ror2's 39 tests pass after nested decomp discovery and Samus workspace reporting.

RoR2 runtime, skin selection/unlocks and world/item/combat behavior require the
offline playtest matrix in README. The ZIP has been built, not deployed into the
game installation. Full audio, original attack commands and other fidelity work
listed in README remain unfinished; this is a prototype, not a final release.
