# User playtest notes, 2026-10-16

## 2026-10-17 / deployed 0.2.2 checkpoint

Both D: installed DLLs were replaced directly and SHA-256 verified against the
0.2.2 release manifest. No backups. Packaged DLL first-run extraction from a fresh
fourteen-file cache passes, followed by the native C# harness. ZIP has exactly
two DLLs, README, CHANGELOG and release.json. Commits: libssbm 5104f51,
ror2-common 06cf0c8, ror2-samus dc0c57a; MCP 106b0f6.

Delayed invisibility/Commando attacks: installed log showed `GFX bone outside rig`
in MeleeEffects.Step. Decode special source parts 0x8D/0x8E and useUnkBone;
isolate presentation errors. Generated charge/cannon/Screw callback cues also
needed physical joint IDs. All 207 disc attachment cues now pass; the C# boundary
runs sixty simulated seconds consuming 174 live attack effects, particles/models
and PCM without failure. This does not establish in-game rendering.

Grid icon is now the actual 64x56 Samus tile (menu joint 27), visually inspected.
Selection repeats up-B with effect/audio on UI time, retries pending extraction,
and consumes first-tick cues before another step. Original trophy platform/growth
entry, source sound/effect, disc jump momentum/voice, directional falls, aerial
landing, hurt/fall/bounce/stand and native article physics/impact callbacks are
connected. Held charge has its source model/generator/scale; source bomb blast
hitbox shrink/clear window runs. Procedural explosion spheres are removed.

Rust suite, shared UI harness, native C# checks, Mario/Olimar builds and 46 MCP
tests pass. Package compiles committed common sources, excluding unrelated local
ModelView performance work. MCP asset_coverage reports extraction complete but
implementation verification incomplete; every category remains honestly wired.

Requested a new D: playtest of selection, Screw Attack, charge ball, missile/bomb
explosions and continued Samus behavior after one minute. Await the user's report
and then inspect D: BepInEx/LogOutput.log. Open fidelity items include entry colour
animation 0x75, full texture/UV/TEV animation, transition blending and some particle
trail/mirror/alpha-compare semantics. Do not call the whole original task complete.

Older observations below are retained as history and superseded where fixed.

The user asked to start now and expects an imminent usage cutoff. Preserve this
checkpoint so unfinished work resumes from these observations. Do not describe
compilation or headless checks as in-game verification.

- Latest user correction: use Melee's original CSS photos directly, replacing
  the generated portrait. Shared Portrait.RequestTexture handles deferred asset
  readiness; the 3D lobby model still plays up-B. Decoder samples TIMG and TCLT
  from MnSlChr.usd; all five Samus costume photos decode. This adds a fourteenth
  required cache file and must migrate older thirteen-file caches.
- Stop making DLL backups. Replace one installed DLL pair after the game closes.
- Occasional floor embedding on spawn: apply common pod/vehicle gating,
  GroundGate/StandingGround, Unstuck and wall recovery.
- Generic Melee entry: original figurine appears and transforms into the living
  fighter, with source models, animations, sound and effects. Research the source
  entry flow and add all dependencies to the character definition/extraction gate.
- Generic movement must cover fall/fall-down, landing, momentum, sprint/run,
  original speed and jump height, and air steering, with associated source audio.
- User sees extremely slow movement, no sprint and a walk alternating two frames.
  Audit native tuning values and animation decoding against decomp; fixed-clock
  tests alone do not establish correct animation sampling or movement.
- Up-B is visibly missing its source effects. Trace source GFX dispatch through
  native cues, attachments, effect models/particles and the actual RoR2 renderer.
- Attacks can work when not embedded, but source effects, sounds, particles,
  models and animations remain incomplete. Account for every dependency through
  the generic fighter profile and coverage report; do not substitute placeholders.

Started: common vehicle gating, clear-ground spawn placement, Unstuck and WallGuard
integration in SamusDriver; original CSS portrait decoding and shared texture
registration. Version 0.2.1 was deployed to D: Steam RoR2 with both installed DLL
hashes verified, directly replacing the pair without backups. The previous DLL
backup pair created by this chat was removed. Full Rust/native-boundary checks,
all five CSS photo/palette decodes, and real fourteen-file first-run extraction
through the packaged DLL pass. Common host changes build in Mario/Olimar too.
In-game portrait/spawn verification is pending. Core movement/entry/effect fidelity
work above remains outstanding.
