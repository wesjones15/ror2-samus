# User playtest notes, 2026-10-16

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
registration. These edits are not yet deployed. Core movement/entry/effect fidelity
work above remains outstanding.
