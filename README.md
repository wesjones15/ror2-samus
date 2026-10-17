# Samus from Melee for Risk of Rain 2

Offline prototype for RoR2 DLC4 / Unity 2021.3.45f2, BepInEx 5. Uses libssbm for
gameplay and disc presentation; ror2-common for the host. No R2API required.

Status: prototype; Rust viewer visually checked, native C# boundary tested, and
RoR2 startup confirmed with Samus loaded. **RoR2 gameplay has not been playtested.** Full behavior fidelity is in
the sibling libssbm/docs/behavior-spec.md when building from source.

No game data is shipped. Supply your own GALE01 revision 2 ISO/GCM/CISO. Models,
animation and tuning are extracted locally. Never share your assets folder.

Install the ZIP into your RoR2 DLC4 folder with BepInEx 5 installed. Launch once
to create `BepInEx/config/com.libssbm.samus.cfg`, then set `Paths.Disc` to your
Melee disc image and restart. The C# plugin calls the extractor inside `ssbm.dll`
on a worker thread, builds its local `assets/` cache, and uses that cache on later
launches. This is the same first-run strategy as Mario and Olimar; no external
extractor or setup script is needed or included.

The plugin searches nearby folders and `SSBM_DISC` when Paths.Disc is empty.
Extraction runs on a worker thread. A missing DLL, invalid ABI or failed asset
load logs an error; an initialized body that fails returns to Commando behavior.

| Action | Xbox | Keyboard/mouse |
| --- | --- | --- |
| Move / aim | LS / RS | RoR2 movement / mouse |
| A attacks | RT | Primary (left mouse by default) |
| B specials | LT | Secondary (right mouse by default) |
| Up / down modifier | LB / RB | Utility / special bindings |
| Jump / short hop | A, tap during jump squat | Jump |
| Sprint | LS click | Sprint |
| Store Charge Shot | B or jump | C or jump |
| Interact / equipment | X / Y | RoR2 bindings |

Neutral special starts charging, another press fires, and jump/cancel stores the
charge. `Controls.HoldCharge` optionally changes this to hold/release. Side special
uses missiles; a quick directional flick selects a super missile. Up special is
Screw Attack; down special drops a bomb. Flick direction + attack, hold, then
release for a smash. Tap up/down + attack for vertical smashes. No bubble shield.

Skill slots hold host charges: attacks, Charge Shot, missiles/bombs, Screw Attack.
Cooldowns and damage coefficients are RoR2 adaptations. Four alternate disc
costumes unlock persistently after stage 1, 2, 3 and 4 teleporter events as Samus.

Build from this workspace with `./build.ps1`; `-Deploy` explicitly installs the
two built DLLs and a local assets path into RoR2Dir. Default builds only the ZIP.
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

Known limits: original attack hitbox/command timing, audio and full TEV materials
remain unfinished. Projectile shapes and impact effects are procedural placeholders.
Screw Attack currently lands one area hit. Attack speed, all item-specific effects,
AI companions/clones and fall damage need further integration. Grabs, ledges,
percent/stock HUD, blast-zone effects and multiplayer are not implemented. Use
single-player offline; remote-player native simulation is not supported.
