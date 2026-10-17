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
  The user's game log confirms the plugin loads under Mono. The game/BepInEx
  assemblies were not modified to suppress this warning.
- C# NativeCheck extracted the user's disc into an ignored empty cache on a
  worker through ssbm.dll, without an external tool. A second invocation reused
  that cache with no disc argument. A missing-cache invocation passed ABI checks
  and skipped asset-dependent checks, so clean builds need no disc or cache.
- Release packaging allows only Samus.dll, ssbm.dll, README.md, CHANGELOG.md and
  release.json. The build removes the two obsolete helpers from previous staging
  output, rejects unexpected files and writes an external ZIP checksum.
- gc2ror2's 15 server/protocol/guide tests pass after the DLL-first release policy
  update, including policy checks on initialization, prompts, plans and guides.

The user reports a Steamworks startup failure with Samus enabled and successful
startup with Samus.dll disabled. Treat Samus as the trigger while investigating
the underlying fault. The prior claim of confirmed successful startup with
Samus active was an incorrect interpretation of log entries and has been
withdrawn. Survivor/portrait log entries alone do not establish successful
platform initialization. No Steamworks bypass or game-file changes were made.

RoR2 runtime, skin selection/unlocks and world/item/combat behavior require the
offline playtest matrix in README. The corrected packaging ZIP has been built;
this does not establish that the startup defect is fixed. Full audio, original attack commands and other fidelity work
listed in README remain unfinished; this is a prototype, not a final release.
