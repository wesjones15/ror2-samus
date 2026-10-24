# 0.3.0 one Melee plugin with eight fighters

A single **Melee** plugin with eight survivors. Install `Samus.dll` and `ssbm.dll` (native ABI 5) from one ZIP.
**Samus, Fox and Luigi are the most complete. Pikachu, Captain Falcon, Link, Yoshi and Donkey Kong are a first pass: not
complete and not ready.** Their attacks still need to be finished and tuned, and other per-character details are missing or
rough. They are covered by headless tests against the disc and native-boundary checks, not by a playtest.

**Upgrading from Samus 0.2.x: read this first.** The plugin has a new identity.

- The plugin GUID is now `com.libssbm.melee` (was `com.libssbm.samus`) and its folder is `BepInEx/plugins/Melee`
  (was `MeleeSamus`). The config file is `BepInEx/config/com.libssbm.melee.cfg`; set `Paths.Disc` again there.
- Delete `BepInEx/plugins/MeleeSamus/` and `BepInEx/config/com.libssbm.samus.cfg`. Both plugins would register Samus.
- The extracted cache lived in `MeleeSamus/assets`. Move it to `Melee/assets`, or set `Paths.AssetsDir`; otherwise the
  first launch extracts again from your disc. Fox, Luigi, Pikachu and the rest are extracted on that first launch either way.
- Costume unlocks and loadout choices are kept under the same content names (`MeleeSamus*`).

**New fighters** (each with his disc costumes, CSS portrait, announcer call and intro, pause-menu controls window, KO, and
the five-skill layout RT / LT / RB / LB / B; the skin unlocks follow stages 1, 2, 3... as that fighter):
- **Fox** (4 costumes): Blaster (spammable), Fox Illusion, Fire Fox (launches along the camera aim), and a Reflector that
  is a frame-1 area burst with damage and outward knockback around Fox.
- **Luigi** (4): bouncing Fireball (spammable, a despawn puff when it expires, it dies on walls and ceilings), Green
  Missile charged from 250% to 1600%, Super Jump Punch and Luigi Cyclone.
- **Pikachu** (4, first pass): see below.
- **Captain Falcon** (6, first pass): Falcon Punch, Raptor Boost, Falcon Dive and Falcon Kick with the disc's hitboxes.
- **Link** (5, first pass): Hero's Bow (hold to draw, charge scales damage and speed), Boomerang, Spin Attack and Bomb. The arrow,
  boomerang and bomb speeds are prototype values.
- **Yoshi** (6, first pass): Egg Lay (tongue grab), Egg Roll, Egg Throw (lobs an egg and hops) and Yoshi Bomb.
- **Donkey Kong** (5, first pass): Giant Punch, Headbutt, Spinning Kong and Hand Slap.

**Pikachu** (RT Thunder Shock, LT A, **RB Thunder**, LB Quick Attack, B Skull Bash):
- *Thunder Shock* is shot along the crosshair and bounces and reflects off surfaces, ending after ten ground bounces, like
  Luigi's Fireball (10% in flight, 6% after its first bounce). The orb draws the disc's attached `0x4BD` effect
  (`EfPkData` generator 7000), not the air-jolt trail model; the old bounce/impact puffs could leave permanent sparks.
  It flies level for 20 m before arcing down, and each surface contact now gets the source's air-jolt arc as a short blue flash.
  The 27.5-unit blue aura uses a centred radial glow instead of its corner-centred texture, so the edges fade. A ground contact before 20 m still bounces and draws its arc, and each bounce now draws one arc in sequence instead of ten at the first contact.
- *Egg Lay* is the RT alternate: Pikachu holds the upright Thunder cast pose (`SpecialLwStart` frame 15), pitches his
  upper body with the camera, and lashes Yoshi's tongue from his mouth. Caught enemies are laid in Yoshi eggs behind
  him, with Yoshi's Egg Lay swallow/lay/break sounds (no voice). Eggs fan behind Pikachu's aim; the tongue now eats slimes and other HealthComponent bodies, not only HurtBoxes. The 3 s cooldown is spent only on a catch.
- *Quick Attack* zips along the camera/stick direction in its two source zips, with no enemy lock-on. It has 2 charges, each recharging in 1 s, and damages enemies it passes through. A second zip needs a turn of at least 38 degrees or another LB press. Zips never rubber-band: Pikachu keeps the ground he covers.
- *Skull Bash* charges while B is held (up to 90 frames), lunges with damage and distance that grow with the charge, and
  stops on what it hits.
- *Thunder* drops a cloud and four bolt segments on Pikachu; the bolt shocks the column, and the strike puts out a 10 m electric burst with heavy damage and outward knockback. An overhead surface blocks the cloud, bolt and burst entirely. The host draws four fixed 4 m article-0 segments in a 16 m column above him, with the source draw
  tinted yellow and the 1216 crackle at the strike; it no longer raycasts against the world ceiling or stretches one quad, and it is forced additive with a distance-faded yellow halo where the black texture border was.

**All fighters**
- Shared systems: per-fighter skill layouts and button remapping, fighter-selected select-screen intros, the GAME! banner
  with the extracted model and voice at the end of a run, the shared KO and recovery sequence, the pause-menu controls
  window, and a shared hit tracker and bouncing-shot component.
- Melee A attacks stagger a non-boss enemy for `Combat.StaggerDuration` (0.25 s, refreshed per hit; bosses and elites are exempt).
- `Combat.Armor` (default 20) sets the base armor of every Melee survivor.
- Dash attack and dash grab keep the ground their animation covers, with no snap-back at the end.
- Native: libssbm ABI 5 carries the fighter kind through every export; new exports for a lock-on target, hit notification and
  projectile reflection (used by Pikachu).

**Samus**
- Character select plays the announcer's name call, then the Screw Attack as her intro; the call plays to its end.
- The up special (Screw Attack) makes her invulnerable from launch until she lands or starts another attack.
- GAME! at the end of a run is drawn with the extracted model and voice.

Headless libssbm, native-boundary and host checks pass on all fighters. None of this was played in game before release.

# 0.2.17 down-B spin, longer and harder missiles, easier Charge Shot

- Down-B and Screw Attack are drawn from their own clips again. Since 0.2.13 they were layered over the walking legs, which replaced the joints that carry the spin and left only the upper body bobbing and restarting. They still steer as before.
- Side-B missiles fly up to 120 m (`Combat.MissileRangeMetres`, 0 for unlimited) at their cruise speed, with homing lock-on out to the same range, and hit 5x as hard (`Combat.MissileDamageScale`) because the move has a cooldown.
- Charge Shot detonates on enemies within 3x its radius (`Combat.ChargeShotHitboxScale`) and its blast is as wide, so shots at range connect; walls and the floor still use its own size.
- The Screw Attack's ground dust ring expanded and then sat at full opacity for 0.3 s, which looked static. It now dims by its authored blend value and fades out over its last 10 frames (an adaptation; the disc data holds it opaque).
- `Debug.LogEffects` logs each Melee model effect (such as the Screw Attack ground burst) when it spawns and ends, with how often its texture changed while drawn, to diagnose effects that still look static.
- Native, shared presenter and build checks pass; in-game playtesting remains pending.

# 0.2.16 side-B fires forward

- Side-B and Super Missile are always drawn over Samus's walking or idle legs, standing still and in the air as well as walking. The disc clips fire sideways and bake that turn into the pelvis and feet, which showed as turned feet and body and a cannon pointing at the camera.
- Only the upper body is turned 90 degrees (plus the difference to the camera aim), so the cannon fires forward and tracks the crosshair without moving the legs. Muzzle effects use the same pose.
- Real-disc pose checks cover standing, aimed and airborne side-B for every costume; in-game playtesting remains pending.

# 0.2.15 animated effect textures and upright side-B

- Side-B keeps Samus upright and turns the cannon toward the camera instead of tilting the whole body.
- Ground-burst and other common effects play their disc texture frames and UV transforms; the shared renderer now receives animated image frames and UVs.
- Native, shared presenter and build checks pass; in-game visual playtesting remains pending.

# 0.2.14 knockdown protection, down-B selection and aimed side-B pose

- Melee knockdown and get-up now reject repeated damage, with one second of protection after control returns. RoR2 stun extends that window; buffered LT can fire when control is available.
- Samus's character-select intro plays down-B once and idles on the floor. The shared presenter now accepts a character-selected intro move.
- Side-B and Super Missile pitch and yaw Samus's upper body/cannon toward the camera aim while the legs retain their independent gait. The posed muzzle uses the same aim correction.
- Native, shared presenter and build checks pass; in-game control and visual playtesting remain pending.

# 0.2.13 mobile specials, death and input recovery

- Grounded Melee special moves share independent walking legs and upper-body attack pose. Contextual A remains a full-body attack. Samus's special recharge is shorter.
- B now uses down-B/Shield; X aims the Z-grab while visible vanilla interact prompts retain priority. LT presses during stun or knockdown are buffered and execute once after control returns.
- Character select preserves Screw Attack's airborne rise and returns to floor idle. Final death plays a disc knockdown, then the falloff KO effect/sound and vanishes. At run end a shared GAME! banner plays the extracted Melee voice cue; first-run extraction now includes nr_name.ssm.
- Core, disc asset, shared host, controller and native-boundary checks pass. In-game feel, death timing and banner visuals still need a playtest.

# 0.2.12 mobile Charge Shot

- Grounded RT Charge Shot permits walking during startup, charge and firing recovery. Source upper-body animation plays over a separate, continuous walk cycle on Samus's legs; cannon, effects and hitboxes use the same posed skeleton.
- Sprint during startup or charging stores the current shot and continues into sprint. The generic Melee core exposes opt-in mobile moves and a fighter-defined upper-body joint root for future characters.
- Fixed-rate movement/storage tests, real-disc all-costume pose checks, viewer capture and native boundary checks; Unity playtest pending.

# 0.2.11 five-skill Melee controls

- RT neutral-B, LT contextual A/smash, RB side-B, LB up-B. X becomes a real fifth loadout/HUD skill: down-B by default, with Shield / Air Feint as the alternate. Existing vanilla interact prompts take priority over X.
- A always jumps, including releasing a held enemy. Ground/short/aerial jumps reach Commando's standard jump height with item scaling. Native movement and special launch rules remain separate.
- Shared native shield meter, break/recovery, Guard/EscapeAir source animations, common sounds, hurt-state invulnerability, extracted guard models/player palette and health-dependent source radius. Advanced shield tilt/reflection/hitlag remain incomplete.
- Camera-aimed, wall-occluded 20m Z-grab using repeated original tether parts. Boss/champion targets pull Samus while B stays held and allow contextual LT without capture/throws; normal enemies retain LS-directed pummel/throws.
- Reuse common pad controls, extra GenericSkill/HUD registration, incoming-damage receiver and boss grapple. Headless/native tests cover bindings, interaction ownership, source defense, jumps, wall occlusion, player-only pull and normal capture restoration. Unity/controller/HUD playtest pending.

# 0.2.10 automatic arrival and earlier falloff

- Initial spawn enters the same native figurine return as falloff recovery, skipping the KO and removing the X/Start prompt. Movement after the minimum arrival or the source timeout triggers transformation and flashing protection.
- Begin falloff KO/recovery at Mario's TOO BAD onset: >20m below remembered safe ground with no floor within 2000m, or a valid stage return zone. Preserve descents onto lower terrain.
- Share the early-void predicate with Mario and Olimar; keep their own initial arrival sequences intact.
- Core/native initial and falloff return checks pass at 30/60/144 FPS; common checks cover the threshold, lower terrain, zones and protection ownership. Unity playtest pending.

# 0.2.9 arrival interaction and vanilla facing/aim integration

- Retry the vanilla Start interaction when local player control, authority or InteractionDriver arrives after state entry.
- Use RoR2 CharacterBody.shouldAim and the actual CharacterDirection turn controller instead of forcing camera yaw. Keep movement independent and preserve authored heading locks.
- LS-modified RT/LT attacks use camera pitch: up from +20 degrees, down from -50; LB/RB override, neutral inputs and captured throw axes remain intact.
- Use RoR2's camera-corrected live crosshair ray for shots, ignoring owner colliders and preventing backwards convergence on near-camera obstructions.
- Shared arrival/aim checks, core and native 30/60/144 FPS regressions; Unity playtest pending.

# 0.2.8 shared Melee falloff recovery

- Keep common map-zone/AnyDrop thresholds and safe-ground placement; falloff now plays the original Melee downward KO model and sound before figurine return.
- Release the figurine on movement after its minimum return phase or after the source timeout; play the entry transformation once, followed by source flashing and two seconds of invulnerability.
- Share the native sequence through libssbm::rebirth and host buff ownership through common MeleeRecovery. Initial Start arrival and ordinary collision recovery retain their separate behavior.
- Native/core and C# tests cover 30/60/144 FPS, repeated falls, source assets/PCM, visibility, input/timeout release, protection expiry and cleanup. Viewer poses inspected; Unity playtest pending.

# 0.2.7 shared effect alpha

- Route source texture alpha through ror2-common MaterialAlphaBuffer so future characters can reuse multiply/blend/replace handling.
- Preserve zero-material-alpha effects, transparent texture borders, RGB and animated fades; legacy pre-baked sources retain their existing behavior.
- Compare shared and native Screw Attack output at active/fade frames through the actual DLL boundary. Shared alpha, native, movement and startup checks pass; Unity playtest remains pending.

# 0.2.6 dash movement and start prompt

- Use RoR2's vanilla interaction UI and bound-button glyph with Start as the arrival prompt.
- Stage deployments in pikmin-main/target/BepInEx/plugins/MeleeSamus/ for manual installation.
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
