# ror2-samus

C# host for [libssbm](https://github.com/wesjones15/libssbm). This repository builds the **Melee** plugin for Risk of Rain 2: fighter movement, contextual attacks and specials connected to RoR2's stages, enemies, health, items and progression.

The repository name reflects the first fighter. The plugin now contains the shared roster, and its managed assembly retains the name `Samus.dll`. Native behavior and presentation come from `ssbm.dll`; the C# code handles world queries, input, combat/item adaptation, UI, extraction and networking.

Development is on `main`, targeting the project's **RoR2 DLC4 build, Unity 2021.3 and BepInEx 5**. The installed package folder is `BepInEx/plugins/wesleyasaurus-Melee/`.

## Available fighters

**Samus and Yoshi are shown by default.** Fox, Luigi, Pikachu, Captain Falcon, Link and Donkey Kong are available as unfinished content through `ShowUnfinished` in `BepInEx/config/Ror2Common.Experimental.cfg`.

The roster shares movement, contextual attacks, defense, item handling, arrival/recovery and control conventions. Each fighter supplies its own specials, costumes and combat adapters. The [implementation audit](https://github.com/wesjones15/libssbm/blob/main/docs/first-pass-audit.md) records individual coverage and remaining work.

## Controls

Gamepad inputs use physical positions with Xbox-style names. Keyboard/mouse uses your RoR2 bindings, with **C** as the additional attack key. The pause-menu controls page displays the selected fighter's moves and loadout.

| Gamepad | Keyboard/mouse | Action |
| --- | --- | --- |
| Left stick | Movement | Walk/run. |
| A | Jump | Jump; press again in the air for an air jump. |
| Left stick click | Sprint | Sprint. |
| B | `C` | Contextual A attack: jab, tilt, smash, dash attack or aerial. |
| RT | Primary | Primary special. |
| LT | Secondary | Secondary special or selected defensive alternate. |
| RB | Utility | Utility special. |
| LB | Special | Special-slot move. |
| X | Interact | Grab/grapple; an active RoR2 interaction prompt takes priority. |
| Y | Equipment | Use equipment. |

**Contextual attacks.** Press B/C from neutral for a jab, add movement for a tilt, or flick a direction with the attack to start a smash. Hold the attack to charge a smash and release to strike. Sprinting selects a dash attack; airborne inputs select aerials. Camera aim supplies up/down direction for attacks and aims projectiles toward the crosshair.

**Grabs.** X/Interact uses the fighter's grab or tether. A captured enemy can be pummeled with B/C and thrown using movement inputs. Samus's grapple pulls ordinary enemies toward her; large targets act as anchors that pull her toward them. Her pause sheet includes release controls.

The additional attack has its own loadout/HUD entry alongside the four standard skill slots. Skill names in the HUD and pause sheet follow the selected alternates.

## Default loadouts

| Fighter | RT / Primary | LT / Secondary | RB / Utility | LB / Special |
| --- | --- | --- | --- | --- |
| Samus | Charge Shot | Morph Ball Bomb | Missile | Screw Attack |
| Yoshi | Egg Lay | Yoshi Bomb | Egg Roll | Egg Throw |

**Samus:** Charge Shot can store charge for later use. Morph Ball Bomb drops a bomb, then holding the input rolls in ball form with a separate armor pool. Missiles and Screw Attack provide ranged pressure and a rising attack; the grapple adds capture and traversal.

**Yoshi:** Egg Lay catches enemies with the tongue and lays them in eggs; its recharge begins on a successful swallow. Egg Roll is steered while held, Egg Throw supplies a ranged attack, and Yoshi Bomb slams down from the air or after a grounded hop.

Both offer **Shield / Air Feint** as a secondary loadout alternate. Hold it on the ground to shield; airborne use evades. The shield has its own drain/recovery rules, and air evasion is limited per airtime. Fighter-specific timings, damage, travel and resource limits are adapted for RoR2.

## Items, costumes and recovery

Hits use RoR2 damage, crits, proc chains and kill credit. Stats and skill stocks/recharge feed into the native driver. Item bridges cover movement/jumps, sprint effects, attack-speed scaling, Wax Quail, H3AD-5T v2 and primary-use hooks. See the [item ruleset](docs/melee_ror2_item_ruleset.md) for the integration categories and configurable scales.

Alternate costumes unlock persistently through stage 1–4 teleporter events while playing the corresponding fighter. The selected costume is used in the lobby and during the run.

Falling off a stage uses a KO/return sequence that brings the fighter back to safe ground. Arrival and recovery share the native return presentation. Final death and run-end presentation follow the fighter's own pose, effects and sound path. AI-controlled clones use the shared fighter brain and combat controls.

## First run and configuration

Install Melee with the release's required BepInEx dependencies and play. No disc or asset setup is required. Missing game assets use colored fighters, generic projectiles/held objects, authored approximate move schedules and RoR2 drop pods; missing audio/effects are omitted. While a stand-in is shown, the character overview names the missing game and tells you to put its disc image in `BepInEx/plugins/wesleyasaurus-SharedFiles/put_games_here/`. To add original presentation and disc-derived gameplay data, use the optional **wesleyasaurus-SharedFiles** package and:

1. Put your **Melee USA revision 1.02 disc image** (`GALE01`, revision 2) in `BepInEx/plugins/wesleyasaurus-SharedFiles/put_games_here/`. ISO, GCM and CISO are supported; convert other containers first.
2. Launch RoR2 and let the background extraction finish. The cache is `wesleyasaurus-SharedFiles/assets/melee/`.
3. If a lobby display is still waiting for assets, select another survivor and return to it.

The plugin prepares the enabled fighters and reuses their complete caches on later launches. Filenames are arbitrary, and one level of subfolders is supported. Keep the disc available for extraction refreshes when updating. Companion plugins share the same per-game cache.

Configuration is in `BepInEx/config/com.libssbm.melee.cfg`. It includes simulation scale/speed, combat and projectile scaling, item bridges, presentation, diagnostics, VR and fighter-specific options. The shared `ShowUnfinished` setting controls roster availability; co-op peers need compatible settings.

For extraction or native-loading problems, inspect `BepInEx/LogOutput.log` and use the plugin/native DLL pair from the same build. The [SharedFiles guide](https://github.com/wesjones15/ror2-common/blob/modern/1.5.0/SHARED-FILES.md) describes discovery, caches and extraction versions.

## VR and multiplayer

Optional VR integration provides pointer aiming, controller arms, a wrist HUD and first/third-person settings through the shared `MeleeVr` layer. A jumps, B attacks, right trigger/grip map to RT/RB, left trigger/grip map to LT/LB, X grabs/interacts and Y uses equipment. The pause sheet labels the controller inputs. Headset validation remains pending.

The owning player runs the fighter simulation and records calls for remote shadow simulations to replay. Peers need matching builds and compatible roster settings. Use offline play or private friends-only co-op. Asset-free replication and sessions with differing asset availability require manual co-op playtesting; native checks do not establish that validation.

Source overview and contributor instructions: [Development](https://github.com/wesjones15/ror2-samus/blob/main/docs/Development.md).

## License and credits

Original contributions by **wesleyasaurus** are dedicated to the public domain under
[CC0 1.0 Universal](LICENSE), to the extent wesleyasaurus owns the relevant rights.
Attribution for those contributions is appreciated but not required. Third-party software
and original game content retain their respective rights and terms.
See [CREDITS.md](CREDITS.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
