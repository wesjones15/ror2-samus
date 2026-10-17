# Samus from Melee for Risk of Rain 2

Offline prototype for RoR2 DLC4 / Unity 2021.3.45f2, BepInEx 5. Uses libssbm for
gameplay and disc presentation; ror2-common for the host. No R2API required.

Shared integration: libssbm's character asset profile supplies file references,
costumes, sound banks and article layouts to reusable readers. ror2-common owns
model/particle rendering and PCM device output. Samus's special behavior remains
character-specific; the gameplay state machine is not yet a generic fighter API.
See the sibling `libssbm/docs/reuse.md` for the ownership and remaining split.

Character select plays up-B (LB+B), using the shared Melee selection rule and the
attack's original animation, effects and sounds. Samus plays Screw Attack once,
then returns to an animated idle on the UI clock. The preview stays at the mannequin's
origin. The icon uses Samus's original 64x56 CSS grid tile from MnSlChr.usd,
with its palette and aspect ratio preserved. The shared
portrait queue installs this texture after extraction; no 3D icon capture is used.
Selection audio stops when the intro completes and on deselection.

Status: incomplete; Rust viewer visually checked and native C# boundary tested.
The user successfully started single player as Samus with the diagnostic build,
then reported broken locomotion and placeholder attacks. The delayed return to
Commando was traced to special GFX bone IDs being used as physical joints. Source
bone indirections now resolve and presentation failures cannot replace gameplay.
The user confirmed a successful Steam launch with 0.2.4 enabled on 2026-10-17,
after removing Steam diagnostic hooks and deferring native loading to onLoad.
The exact earlier cause is unproven; gameplay changes still need an offline
visual/listening playtest.
Full behavior fidelity is in
the sibling libssbm/docs/behavior-spec.md when building from source.

No game data is shipped. Supply your own GALE01 revision 2 ISO/GCM/CISO. Models,
animation, tuning, sound banks, sequences and effect/particle archives are
extracted locally. Old incomplete caches automatically require extraction again.
Never share your assets folder.

Install the ZIP into your RoR2 DLC4 folder with BepInEx 5 installed. Launch once
to create `BepInEx/config/com.libssbm.samus.cfg`, then set `Paths.Disc` to your
Melee disc image and restart. The C# plugin calls the extractor inside `ssbm.dll`
on a worker thread, builds its local `assets/` cache, and uses that cache on later
launches. This is the same first-run strategy as Mario and Olimar; no external
extractor or setup script is needed or included.

The plugin searches nearby folders and `SSBM_DISC` when Paths.Disc is empty.
Native loading and cache/extraction work begin at RoR2Application.onLoad, after
game startup; content providers register earlier and wait for asset readiness.
Samus no longer hooks or reflects Steam initialization. The old Debug.StartupTrace
config entry is ignored. Extraction runs on a worker thread. A missing DLL, invalid ABI or failed asset
load logs an error; an initialized body that fails returns to Commando behavior.

| Action | Xbox | Keyboard/mouse |
| --- | --- | --- |
| Move / aim | LS / RS | RoR2 movement / mouse |
| A attacks | RT | Primary (left mouse by default) |
| B specials | LT | Secondary (right mouse by default) |
| Up / down modifier | LB / RB | Utility / special bindings |
| Jump / short hop | A, tap during jump squat | Jump |
| Sprint | LS click | Sprint |
| Grab / aerial Z-air | B | C |
| Store Charge Shot while charging | B or jump | C or jump |
| Throw a captured enemy | LS direction + A or B (RT/LT also work) | WASD + primary/secondary |
| Pummel a captured enemy | Neutral A or B | Neutral primary/secondary |
| Interact / equipment | X / Y | RoR2 bindings |

Neutral special starts charging, another press fires, and jump/cancel stores the
charge. `Controls.HoldCharge` optionally changes this to hold/release. Side special
uses missiles; a quick directional flick selects a super missile. Up special is
Screw Attack; down special drops a bomb. Flick direction + attack, hold, then
release for a smash. Tap up/down + attack for vertical smashes. LB/RB take priority over LS for move selection. No bubble shield.

The first map spawn materializes Samus as a figure on the original entry platform.
RoR2's vanilla Start prompt shows the bound Interact button (X on the pad). Press it to play the source transformation, burst and sound, then take control.
The shared MeleeArrival state replaces the vanilla pod. Ground B grabs; air B uses
the Z-air hitbox. Captured enemies enable Melee-style LS axes and A/B pummel or throws.
RoR2 capture stops on interruption, death, release or an eight-second timeout; player
bodies and stationary encounter objects are excluded. Constrained tether/ledge physics
and Melee escape/percent knockback rules remain incomplete.

Stored Charge Shot shows a HUD indicator; full charge also uses the original white
colour pulse. Firing aims from the cannon at the live crosshair and travels until
collision without the source offscreen timeout. Charge article size and opacity use
disc values. Melee and projectile damage request source elemental hit effects/sounds.

Skill slots hold host charges: attacks, Charge Shot, missiles/bombs, Screw Attack.
Cooldowns and damage coefficients are RoR2 adaptations. Four alternate disc
costumes unlock persistently after stage 1, 2, 3 and 4 teleporter events as Samus.

Build from this workspace with `./build.ps1`; `-Deploy` stages the two built DLLs
in `../target/BepInEx/plugins/MeleeSamus/` for manual copying into the game folder.
RoR2Dir supplies build references only. Default builds only the ZIP.
The release ZIP contains only Samus.dll, ssbm.dll, README.md, CHANGELOG.md and the
checksum manifest release.json. Its explicit allowlist excludes executables,
setup scripts, extracted assets and Nintendo/game data. A clean build needs no
disc or asset cache; asset-dependent harness checks skip when the cache is absent.

Verification completed: matching Melee decomp rebuild; Rust behavior/parser/ABI
tests; five costumes and all clip transforms; rendered viewer inspection; real
Rust DLL called through this plugin's Native.cs; C# compile against DLC4 assemblies.

For the first offline playtest, check selection/skin preview, spawn/camera, analog
movement, short/double/Feather jumps, each modifier and smash release, stored and
airborne Charge Shot, missile/bomb damage, Screw Attack landing, chests/equipment,
jump pads, stun/freeze and falling off the stage. Finish a teleporter and check the
costume unlock after restarting. Diagnostics are in BepInEx/LogOutput.log.

Disc subaction commands now drive normal/Screw Attack hit windows and posed bone
hitboxes; gait clocks use fixed ticks and disc speed scaling. ABI 4 transfers posed
hitboxes, particle snapshots and native projectile state. Walk/run sampling checks
at least 18 distinct poses across 20 samples. Ground jump momentum, aerial steering,
directional falls, aerial landings and hurt/fall/bounce/stand poses use the shared
Melee layer. Knockdown is a RoR2 health/impulse adaptation, with automatic get-up.

Disc sound cues now feed an SSM/SEM decoder and native stereo mixer, streamed
through the shared Windows PCM queue. Playback follows action commands, supports
sound-slot replacement/stop and follows the game's volume/pause/run lifecycle.
In-game listening verification and remaining source audio routing are pending.

All 22 Samus particle streams execute in native checks. Source billboard textures,
colour/size/rotation and child effect graphs now drive missile/charge presentation.
The attack/entry/recovery effect IDs resolve, including source bone cycles and
attached Screw Attack. Held Charge Shot has its original article and generator;
bombs and normal/super missiles use source impact particles and sound. The
diagnostic explosion spheres have been removed. Source article attributes now
drive native fixed-tick speed, gravity, homing, lifetime and explosion duration.

The shared trophy platform and growing figurine entry are implemented with source
tuning, sound and effect. Host collision feet stay on clear ground while the
presentation rises. Source entry colour program 0x75 is connected to the common
renderer's overlay input, alongside local-joint
blending for directional fall/air-fall/helpless poses.

Known limits: full TEV/animated texture and UV fidelity, other source transition blending,
complete particle trail/mirror/alpha-compare semantics, and gameplay visual/audio
validation remain unfinished. Attack speed, all item-specific effects,
AI companions/clones and fall damage need further integration. Grabs, ledges,
percent/stock HUD, blast-zone effects and multiplayer are not implemented. Use
single-player offline; remote-player native simulation is not supported.
