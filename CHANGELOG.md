# 0.2.6 dash movement and start prompt

- Use RoR2's vanilla interaction UI and bound-button glyph with Start as the arrival prompt.
- Move Dash Attack's source translation into collision-resolved native movement so Samus keeps the distance after returning to idle.
- Verify dash retention at 30/60/144 FPS, wall collision and native C# input; inspect mid-attack and completed poses in the viewer. Unity playtest remains pending.

# 0.2.5 presentation and controls

- Fix UV-pair transfer, source alpha/blending, texture matrices/mirror wrapping and camera-facing joints so Screw Attack and the complete Charge Shot orb render their source geometry.
- Character select plays up-B once, settles to animated idle, and replays on reselection.
- Remove the firing voice on full-charge storage; add source full-charge pulse and stored-charge HUD indicator.
- Aim ranged attacks at the live crosshair and remove Charge Shot's short offscreen lifetime for RoR2.
- Add elemental projectile hit feedback alongside melee hit feedback.
- Replace the first map's pod with a shared Interact-gated figure/platform arrival and original transformation cues.
- B grabs on the ground and Z-airs in the air. While holding an enemy, raw LS + A/B selects source throws; neutral A/B pummels. Shoulder modifiers override LS.
- Decode source grapple links and animated throw accessory instances. Enemy capture/AI restoration lives in common.
- Native, real-disc and C# boundary tests pass; Unity playtest remains pending. Full TEV/animated texture fidelity, constrained tether/ledge physics and Melee escape/percent rules remain incomplete.

# 0.2.4 startup compatibility patch

- Remove Samus's Steam constructor/initialization diagnostic hooks entirely.
- Defer native DLL loading, cache checks and extraction until RoR2Application.onLoad.
  Content still registers during bootstrap; body/display/portrait wait for assets.
- Check the built plugin's IL on every package build: no Steam type references or
  native calls reachable from Awake. The previous release is a failing control.
- User confirmed a successful Steam launch with 0.2.4 enabled on 2026-10-17.
  Fresh log shows deferred native initialization, portrait readiness and main menu.
  Steam validation is unchanged; the exact source of the earlier failure is unproven.

# 0.2.3 prototype

- Decode the original shared entry colour overlay from PlCo and feed the common
  model renderer. Blend directional fall poses locally with source response.
- Rust/native checks and C# compile pass; in-game visual checks remain pending.

# 0.2.2 prototype

- Resolve special GFX bone IDs 0x8D/0x8E and useUnkBone indirection. Check all
  207 source animation GFX attachments; isolate presentation errors from gameplay.
- Use the original CSS grid tile, replacing the large selected-character photo.
- Repeat up-B with animation, effects and sound on UI time; retry after extraction.
- Original trophy platform, grow/release entry, source sound and burst effect.
- Disc jump momentum and voice cues, directional falls, aerial landing clips,
  hurt/fall/bounce/stand recovery and common spawn/unstuck integration.
- Native article physics/hitboxes/lifetimes; original charge article, bomb/missile
  impact emitters and sound. Remove procedural explosion spheres. ABI 4.
- Native tests compare 30/60/144 Hz. Full material/transition fidelity and RoR2
  visual/listening verification remain pending; see README.

# 0.2.1 prototype

- Use the original Melee CSS portrait from MnSlChr.usd through the shared texture
  queue. Image and costume palette are decoded from source animation channels;
  no 3D portrait pose/camera capture. Older caches require the fourteenth file.
- Wait for pod/vehicle release before driving the body, probe clear spawn ground,
  and use common Unstuck and WallGuard recovery. Build-checked; playtest pending.
- No DLL backups on deployment, following the user's preference.

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
