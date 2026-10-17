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

The user's D: Steam installation log at 18:09 on 2026-10-16 confirms plugin
loading, survivor registration, portrait rendering, Steam profile loading and
the main menu. The user confirmed startup without error. Steam restarted and
updated before this successful launch, with Samus restored and active; no
Steamworks bypass or game-file changes were made. The earlier generic Steamworks
startup dialog's exact cause was not established. The attempted comparison did
not launch a game before restoring Samus, so it is not an A/B test result.

RoR2 runtime, skin selection/unlocks and world/item/combat behavior require the
offline playtest matrix in README. The corrected ZIP has been built; the user's
successful startup used the previously installed plugin. Full audio, original attack commands and other fidelity work
listed in README remain unfinished; this is a prototype, not a final release.
