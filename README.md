# Melee for Risk of Rain 2

Since 0.3.0 this repository builds the single **Melee** plugin (`com.libssbm.melee`, folder `BepInEx/plugins/Melee`): Samus, Fox,
Luigi, Pikachu, Captain Falcon, Link, Yoshi and Donkey Kong. The player guide is `docs/Melee.md` in the ror2-characters release
repository, and the notes below are the development record, written when Samus was the only fighter. Samus 0.2.x users must
delete `BepInEx/plugins/MeleeSamus` before installing it (see CHANGELOG.md).

## Samus (development record)

Offline prototype for RoR2 DLC4 / Unity 2021.3.45f2, BepInEx 5. Uses libssbm for
gameplay and disc presentation; ror2-common for the host. No R2API required.

Shared integration: libssbm's character asset profile supplies file references,
costumes, sound banks and article layouts to reusable readers. ror2-common owns
model/particle rendering and PCM device output. Samus's special behavior remains
character-specific; the gameplay state machine is not yet a generic fighter API.
See the sibling `libssbm/docs/reuse.md` for the ownership and remaining split.

Character select plays Samus's down-B once, with its extracted animation,
effects and sounds, then returns to animated idle on the UI clock. The shared
Melee selection presenter lets each fighter choose its own intro. The preview
starts and ends on its floor. The icon uses Samus's
original 64x56 CSS grid tile from MnSlChr.usd,
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
| Neutral-B / Charge Shot | RT | Primary (left mouse by default) |
| Contextual A / smash | B; LS direction selects attack | C |
| Side-B / missiles | RB | Utility |
| Up-B / Screw Attack | LB | Special |
| Fifth skill: down-B / Bomb | LT | Secondary (right mouse by default) |
| Fifth skill alternate: Shield / Air Feint | Select in loadout; LT shields on ground, evades in air | Secondary |
| Fifth skill alternate (Samus): Morph Ball Roll | Select in loadout; LT rolls the Morph Ball, bouncing off enemies and walls with 25% health armor | Secondary |
| Jump | A; every jump has Commando's standard height | Jump |
| Sprint / interrupt Charge Shot | LS click | Sprint |
| Grab / aerial Z-air | X, except visible interact prompts | Interact, away from prompts |
| Store Charge Shot while charging | Sprint, X or jump | Sprint, Interact or jump |
| Throw a captured non-boss | LS direction + B/RT | WASD + C/primary |
| Pummel a captured non-boss | Neutral B/RT | Neutral C/primary |
| Boss grapple | Hold X to pull self / stay attached; B contextual attacks; A jumps off | Hold Interact; C attacks; jump detaches |
| Interact / equipment | X / Y | RoR2 bindings |

Neutral special starts charging, another press fires, and jump/cancel stores the
charge. Samus can walk during Charge Shot startup, hold and firing recovery;
the original upper-body attack plays over a continuous Melee walk cycle on her
legs. All grounded special abilities use this generic mobile pose; contextual A,
grabs and authored dashes remain full-body. Sprint during startup or charging stores the current shot and resumes
sprinting. `Controls.HoldCharge` optionally changes firing to hold/release. Side special
uses missiles; a quick directional flick selects a super missile. Up special is
Screw Attack; LT's default down-B drops a bomb. Select Shield / Air Feint in the
fifth loadout row to replace LT's bomb with ground shielding and one air evade per
airtime. Its other alternate, Morph Ball Roll, keeps the down-B morph animation but
skips the bomb: hold LT to bowl the Morph Ball along, bouncing off enemies and walls.
It deals light damage without knockback and carries 25% of maximum health as armor;
release LT to break out. Shield drains when held/hit, regenerates when released and stuns on
depletion. Its source models, player palette, scale, sounds and dodge hurt-state
window are native-driven. An existing vanilla Interact prompt owns X until release.
Flick direction + B, hold, then release for a smash. LB always remains up-B.
Side-B rotates Samus's upper body and cannon from the disc's vertical pose
toward the live camera aim while her legs and travel continue independently.
After a knockdown, repeated ordinary hits are rejected through get-up and for
one second afterward. Existing RoR2 stun extends this protection until control
returns; a buffered B attack can start during that window. This anti-stunlock
timing is a RoR2 adaptation and awaits an in-game playtest.

With LS held above 0.3, camera pitch selects up attacks from 20 degrees above
level and down attacks from 50 degrees below. This applies to contextual B only;
RT/RB/LB/B each explicitly select their assigned B move. Neutral controls remain neutral,
and captured throws retain raw LS axes. These selection bands are a 3D adaptation.

Ordinary facing uses RoR2's real CharacterBody.shouldAim decision (aim vs movement,
including sprint/alwaysAim rules) and CharacterDirection's turn smoothing. Attacks
set the real aim timer; held ranged actions refresh it. Native movement keeps its
own momentum. Authored dash/back-air/spin/grab/throw/entry/reaction headings stay
fixed; other attacks follow the game's smooth turn. Melee assets do not provide
vanilla upper-body aim or strafe blends. Charge shots use the camera-corrected live
crosshair ray, ignoring the owner's colliders; nearby obstructions cannot aim the
shot backwards into Samus. Unity playtest of these changes is pending.

The first map spawn materializes Samus as a figure on the original entry platform.
It enters the same automatic figurine phase as falloff recovery, skipping the KO.
After the one-second minimum, movement transforms early; otherwise the source
four-second wait expires. The source transformation, burst, sound and two seconds
of flashing protection follow. There is no X/Interact prompt or indefinite wait.

Falling off the map uses common FellEarly detection at Mario's TOO BAD onset:
more than 20 metres below last safe ground with no landing surface within 2000
metres below, or a valid stage return zone. Long descents onto lower platforms
remain possible. The shared Melee
sequence plays its original downward KO effect/sound, then returns Samus as a
figurine on verified safe ground. After a one-second minimum return phase, move
to transform early or wait up to four more seconds. The source entry burst and
sound play once; two seconds of source flashing/invulnerability follow release.
This preserves RoR2 health and does not add stocks. Initial arrival shares the
same return phase; ordinary wall/unstuck correction stays immediate. In-game
verification is pending.
The shared MeleeArrival state replaces the vanilla pod. Ground X grabs; air X uses
Z-air. Both extend original tether geometry along the live camera vector to 20m,
stopping at walls. Ordinary captured enemies enable Melee-style LS axes and B/RT
pummel or throws; A releases to jump. Boss/champion targets pull Samus toward the
contact instead. Hold X to remain attached and use contextual B; they never enter
native capture or throw states and their AI/position is not changed.
RoR2 capture stops on interruption, death, release or an eight-second timeout; player
bodies and stationary encounter objects are excluded. Constrained tether/ledge physics
and Melee escape/percent knockback rules remain incomplete.

Stored Charge Shot shows a HUD indicator; full charge also uses the original white
colour pulse. Firing aims from the cannon at the live crosshair and travels until
collision without the source offscreen timeout. Charge article size and opacity use
disc values. Melee and projectile damage request source elemental hit effects/sounds.

Five skill slots hold host charges: Charge Shot, contextual A, missiles, Screw
Attack and Bomb/Shield. The fifth has its own loadout row and HUD stock/cooldown
icon, above the fourth icon. Pad bindings, extra-skill registration/HUD, boss
grapple and defensive incoming-damage adaptation live in ror2-common; libssbm owns
fighter behavior. Ground/short/aerial jumps use the same Commando-based apex,
including jump-power item scaling. Special-attack launch velocities stay separate.
Cooldowns and damage coefficients are RoR2 adaptations. Four alternate disc
costumes unlock persistently after stage 1, 2, 3 and 4 teleporter events as Samus.
Special recharge is shorter in 0.2.13: Charge Shot 0.75s, missile 1s, Screw
Attack 3s and Bomb 1s. B presses made during host stun or native knockdown
are buffered briefly and execute once when Samus regains control.

At final death Samus plays her source grounded knockdown pose, then the same
Melee KO beam and sound as falloff recovery, and vanishes. At run end a shared
GAME! banner plays the disc's GAME cue from the newly extracted nr_name.ssm.
The banner typography/timing is a RoR2 rendering of Melee's UI; in-game visual
and audio validation is pending.

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
costume unlock after restarting. New controls, fifth-slot HUD/loadout, shield
appearance and boss tether still need an in-game controller playtest. Shield tilt,
powershield reflection, source hitlag and source escape/percent rules remain
incomplete. Diagnostics are in BepInEx/LogOutput.log.

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

Luigi's `ScopeChargeSmoke` uses the disc's animated ground-smoke model (common
effect 5 / cue `0x3FF`) at his feet while charging on the ground. It replaces the
empty-click puffs at the muzzle. Waves finish their 20-frame animation after
release; emission timing and charge scaling are host-authored. Verified in the
local asset viewer and build/boundary checks; not yet played in RoR2.
The Scope's upper-body pose follows camera elevation up/down (up to 60 degrees
of torso bend), including the held weapon and firing-hand attachment. Legs keep
their existing stance/walk animation. This is a host adaptation, verified in
the viewer and automated pose/boundary checks; RoR2 play remains unverified.

Yoshi's Egg Lay tongue aims at the camera crosshair point instead of following the
camera's parallel forward ray (the shared `MeleeAim.ProjectilePoint` fix behind
Samus's Charge Shot and Luigi's Scope), so a mouth below the camera no longer fires
under a close target. The point is fixed when the move starts and the lash re-aims
from the animated mouth to it, so the clip's head motion cannot drag the tip off
target. The lash stops at the first terrain it meets (a narrow centre probe, so its
own thickness does not eat it on a floor it passes over), and the drawn tongue is
clamped to the same contact. Yoshi's upper body also pitches toward camera elevation
(the Scope's bounded 60-degree bend), moving the head and mouth with the aim. Egg
Lay's 3 s cooldown now starts only when the tongue swallows an enemy: a miss or a
lash stopped by terrain costs no stock or cooldown. The laid egg's ground and wall
probes ignore every character body, including the victim inside it: the probe mask
contains `EnemyBody`, and a small victim's capsule sat above the egg, so the ground
clamp lifted the egg on the victim's head every tick. Large eggs were unaffected only
because the probe started inside their taller capsule. Small and large eggs now fall
at gravity. Shared camera/sweep, damage-permission, real-disc pose checks and viewer
captures at 45/0/-45 degrees pass and the plugin builds; not yet played in RoR2.

Known limits: full TEV/animated texture and UV fidelity, other source transition blending,
complete particle trail/mirror/alpha-compare semantics, and gameplay visual/audio
validation remain unfinished. Attack speed, all item-specific effects,
AI companions/clones and fall damage need further integration. Grabs, ledges,
percent/stock HUD, blast-zone effects and multiplayer are not implemented. Use
single-player offline; remote-player native simulation is not supported.
