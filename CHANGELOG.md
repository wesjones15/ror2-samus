# 0.2.0 prototype

- Shared Melee character-select rule: play the character's up-B animation and
  source sound cues once per selection. Samus uses Screw Attack. Portraits idle
  silently and face forward.
- Fixed-tick gait animation, disc attack-script hit windows and posed hitboxes.
- Original SSM/SEM sound decoding/mixing through the shared PCM device queue.
- Disc article models, native HSD particle snapshots and effect model/material
  animation; shared character asset profiles and particle renderer.
- ABI 3; matching C#/native DLLs, thirteen required disc files and cache migration.

Gameplay verification of these changes is pending. Projectile physics/impacts,
movement transitions and complete effects/material fidelity remain unfinished.
See README for current limits; the 0.1.0 notes below describe the original prototype.

# 0.1.0 prototype

- Samus survivor for RoR2 DLC4, compiling the existing ror2-common host library.
- Real disc costumes, meshes, textures and 208 parsed animation clips through libssbm.
- Disc-tuned jumps and movement; Xbox RT attacks / LT specials / shoulder direction modifiers.
- Directional tilts/aerials, flick-and-hold smashes with yellow flashing, Charge Shot storage,
  normal/super missiles, Screw Attack and morph-ball bombs using prototype attack schedules.
- NativeSkills charges, NativeHit damage/procs/credit, extra jumps from Hopoo Feathers,
  movement/jump stat scaling, crowd-control interruption and ballistic jump-pad flights.
- Pink, black, green and lavender costume unlocks after stage 1/2/3/4 teleporter events.
- C#-driven first-run disc extraction through ssbm.dll, version/struct checks,
  native .NET harness and package audit. Release contains DLLs, docs and checksum
  manifest only; standalone development extractors and setup scripts are excluded.

Unverified in RoR2. Sound, original hitboxes/timing/projectiles/material effects, grabs/throws,
ledge mechanics, percent/stock HUD and multiplayer are not implemented. Basic damage effects
are procedural placeholders. See README for playtest steps and known limits.
