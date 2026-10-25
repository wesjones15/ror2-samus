# Melee fighters — Risk of Rain 2 Item Compatibility Ruleset

**Status:** Item audit and implementation handoff, October 2026. Complements
`libssbm/docs/behavior-spec.md` and `libssbm/docs/first-pass-audit.md`. This document is
a static audit of the workspace game copy (`2026 Mod Stuff/RiskOfRain2_DLC4`, DLC4 item
catalog) against the shared native-sim driver `ror2-common/src/MeleeDriver.cs` (the
"generic-melee-character" adapter), plus the fixes landed there.

## 1. Why items need attention at all

Mario and Olimar are native-sim survivors too, but they got a bespoke item layer
(`MarioItems.cs`, `OlimarItems.cs`) because their resources (FLUDD water, the Pikmin squad)
have no RoR2 counterpart. The Melee fighters were assumed to "just work" because their moves
map directly onto RoR2's five skill slots and their damage goes through RoR2's own pipeline.
That is true for damage, procs, crits, kill credit and most stats. It is **not** true for the
items that read the *body's own movement state machine*, because a native-sim body never runs
`GenericCharacterMain`, `CharacterMotor` jump code, or the Headstompers states.

`ror2-common/src/NativeBody.cs` already re-raises the events those items listen for (jump,
landing, sprint, out-of-combat clock), and `MeleeDriver` bridges the stats the sim consumes
(move speed, jump power, extra jumps). The gap was the items whose *effect* lives in the motor,
not just their *trigger*:

- **Wax Quail** — the forward boost is applied inside `GenericCharacterMain.ProcessJump`. The
  sim jumps in `libssbm/src/sim.rs` (`Sim::jump`), which knows nothing about it. Fixed (§3).
- **H3AD-5T v2** — its stomp, landing blast and fall-immunity live in the `Headstompers*`
  entity states. A native-sim body has no stomp state. Fixed (§4).
- **Attack speed** — `body.attackSpeed` is a RoR2 stat, but the sim never read it. Fixed (§5):
  the fighter's current move plays faster, hitboxes and all.

## 2. The item catalog, by category

The game copy exposes 153 `ITEM_*` entries (including scraps, consumed/void/secret variants and
keys). They fall into four buckets for a native-sim Melee fighter:

### A. Works automatically — no Melee code involved

Every item whose effect is a **stat** on `CharacterBody`, a **proc** on a hit, or a **kill
reward**. Melee hits are RoR2 damage from the fighter's body (`NativeHit.Deal`: `TakeDamage`,
then `OnHitEnemy`/`OnHitAll` with the body's crit roll and proc chain), so these are all live:

damage (`Focus Crystal`, `Crowbar`, `Armor-Piercing Rounds`, `Brilliant Behemoth`, …), crit
(`Lens-Maker's Glasses`, `Laser Scope`, `Harvester's Scythe`, `Shatterspleen`, …), on-hit procs
(`AtG Missile Mk. 1`, `Ukulele`, `Sticky Bomb`, `Tri-Tip Dagger`, `Gasoline`, `Ignition Tank`,
the Kjaro/Runald bands, `Plasma Shrimp`, `Needletick`, `Polylute`, `Charged/Molten Perforator`,
`Sentient Meat Hook`, …), on-kill (`Ceremonial Dagger`, `Will-o'-the-wisp`, `Bundle of
Fireworks`, `Ghor's Tome`, `Topaz Brooch`, `Hunter's Harpoon`, `Wake of Vultures`, `Happiest
Mask`, `Defense Nucleus`, …), health/armor/regen/shield (`Bison Steak`, `Infusion`, `Titanic
Knurl`, `Repulsion Armor Plate`, `Safer Spaces`, `Tougher Times`, `Personal Shield Generator`,
`Transcendence`, `Aegis`, `Oddly-shaped Opal`, `Power Elixir`, `Bustling Fungus`, `Cautious
Slug`, `Medkit`, `Monster Tooth`, `Leeching Seed`, `Planula`, `Interstellar Desk Plant`, …),
luck and economy items, and the equipment/cooldown items that act on RoR2's own skill slots
(`Backup Magazine`, `Hardlight Afterburner`, `Alien Head`, `Purity`, `Brainstalks`, `Bandolier`,
`Lysate Cell`, the Heresy set, `Fuel Cell`, `Soulbound Catalyst`, … — Melee moves sit in RoR2
skill slots via `MeleeSkills`/`NativeSkills`, so their charges and cooldowns are RoR2's).

### B. Works via the shared bridges already in `MeleeDriver`/`NativeBody`

| Item | Bridge |
|---|---|
| **Paul's Goat Hoof**, **Mocha** (move half), **Red Whip**, **Hunter's Harpoon** (move half) | `body.moveSpeed` → `ssbm_sim_tuning.speed_scale`. Red Whip's out-of-combat gate is restarted by `NativeBody.MarkCombat`. |
| **Hopoo Feather** | `body.maxJumpCount` → `ssbm_sim_tuning.extra_jumps`; the feather burst/voice is spawned in `MeleeDriver.OnJump`. |
| **Faraday Spur / Earthbound Vessel / Hex of Leaping** | `NativeBody.Jumped()` re-raises RoR2's jump event; jump-power changes flow through `NativeBody.JumpScale`. |
| **Mycelium Cap** | `NativeBody.Landed(velocity)` re-raises the motor's landing event. |
| **Energy Drink**, **Weeping Fungus** | sprint state (`NativeBody.UpdateSprint` → `body.isSprinting`) is bridged. Weeping Fungus heals while sprinting. Energy Drink's +25% is baked into `body.moveSpeed` while sprinting by `RecalculateStats` (as `0.25 · stacks / sprintingSpeedMultiplier`), so it rides `body.moveSpeed` and the driver's `÷ sprintingSpeedMultiplier` is the correct inverse of that — the bonus lands at RoR2's value. |
| **Shuriken** | `NativeBody.PulsePrimary` reports a primary-skill use; Melee primaries spend through `NativeSkills`. |
| **H3AD-5T v2** (jump-height half) | its `body.jumpPower` stat already flows through `NativeBody.JumpScale` and `MeleeControls.JumpHeight`. |

### C. Won't map on its own — fixed here in the generic driver

| Item (internal name) | The gap | Fix |
|---|---|---|
| **Wax Quail** (`JumpBoost`) | The sprint-jump forward boost is applied by `GenericCharacterMain.ProcessJump`, which never runs. | A sprinting **ground** jump scales the sim's horizontal jump speed (`ssbm_sim_boost_jump`), by RoR2's formula, on a `WaxQuail` scale config. |
| **H3AD-5T v2** (`FallBoots`) | The stomp and landing blast live in `Headstompers*` states that never run. | Jumping in the air with no jumps left slams down (`Sim::start_stomp` + `ssbm_sim_set_stomp`); the landing fires the item's blast on its cooldown, sized by the fall, on a `Headstompers` scale config. |
| **Attack speed** (`Soldier's Syringe`, `Mocha`, `Predatory Instincts`, `Berserker's Pauldron`, `War Horn`, `Warbanner`, …) | `body.attackSpeed` is a RoR2 stat the sim never read; moves play at the source's fixed rate. | `body.attackSpeed` scales the current move's playback (`ssbm_sim_set_attack_speed`): animation, hitbox windows, effects and the move's end all follow `motion.frame`, so they speed up together, on an `AttackSpeed` scale config. |

### D. Deferred

- Items whose identity is **on another survivor's kit** (e.g. `Beads of Fealty`, `Artifact
  Key`, scraps, void/secret keys) — normal RoR2 handling applies where the game handles them.

The only mobility nuance left is a **calibration** one, not an item gap: the sim's `SpeedScale`
(1.37) is a single global lift set so the slowest runner matches Commando's sprint, so per-fighter
sprint speed is *approximately* Commando's sprint rather than exactly. Sprint items still apply
their bonus on top of that correctly.

## 3. The Wax Quail rule

- Triggers on a **sprinting ground jump** — the jump event's `jumps_used == 1` and
  `body.isSprinting` — matching RoR2's "jump while sprinting" and Mario/Olimar's "sprinting
  jump from the ground". Mid-air (Hopoo Feather) jumps are not boosted.
- The boost is a horizontal-speed multiplier `1 + sqrt(10 · n · acceleration · airControl) /
  moveSpeed`, the same formula Mario's `QuailBoost` uses; a `WaxQuail` config scales it (1 is
  RoR2's). The jump's vertical speed and the fighter's own jump feel are untouched.

## 4. The H3AD-5T v2 rule

- Trigger: **jump in the air with no jumps left** (the sim's `jumps_used` is already at
  `jumps + extra_jumps`). The sim records its start height and slams straight down at a
  constant `STOMP_SPEED` (5 sim units/tick, ~2.5× the normal fall cap), using the falling
  pose — a host adaptation, not a source move.
- On landing the sim reports the fall distance (`EventKind::Stomped`); the driver fires the
  item's blast on its cooldown (`10 s / stacks`), damage and radius interpolated over
  `maxDistance` 30 m (the `HeadstompersFall` state's numbers, as Mario/Olimar read them).
- Because Melee fighters take no fall damage in the source, the item's fall-immunity half is
  moot; only the slam + shockwave is modelled. Its +10% jump height already works (§2 B).

## 5. The attack-speed rule

- `body.attackSpeed` (the stat items raise) scales the **playback rate of the fighter's
  current move**: the disc clip, its hitbox windows, effects, sounds and the move's end all
  read `motion.frame`, which advances by the scaled rate, so the whole move is faster end to
  end. An `AttackSpeed` config scales the item's effect (1 is full RoR2 attack speed, 0
  ignores it).
- It does **not** speed up movement, jumps, the smash charge, a held charge shot, or the fall
  — those clocks stay on the source's rate. A dash attack's root-motion delta is scaled with
  its playback, so it covers the same distance in less time rather than travelling further.

## 6. Where the fixes live

- `libssbm/crates/libssbm/src/sim.rs` — `Sim::boost_jump`, `Sim::start_stomp`, the stomp
  state, `attack_speed` and `EventKind::Stomped`.
- `libssbm/crates/libssbm-ffi/src/lib.rs` — `ssbm_sim_boost_jump`, `ssbm_sim_set_stomp`,
  `ssbm_sim_set_attack_speed`; `replay.rs` records them so a multiplayer shadow sim replays
  the same trajectory.
- `ror2-common/src/MeleeNative.cs` — the P/Invokes and `HasBoostJump`/`HasStomp`/
  `HasAttackSpeed` flags.
- `ror2-common/src/MeleeItems.cs` — the shared item config holder (`WaxQuail`, `Headstompers`,
  `AttackSpeed`).
- `ror2-common/src/MeleeDriver.cs` — the generic-melee-character wiring: Wax Quail on the jump
  event, the stomp enable flag and the H3AD-5T blast on the stomp event, and the attack-speed
  feed. Every fighter (Samus, Fox, Luigi, Pikachu, Captain Falcon, Link, Yoshi, Donkey Kong)
  inherits this because they all subclass `MeleeDriver`.
- `ror2-samus/Samus/Plugin.cs` — the `Items.WaxQuail`, `Items.Headstompers` and
  `Items.AttackSpeed` config entries.

## 7. Verification boundary

`cargo test` covers `Sim::boost_jump`, `Sim::start_stomp` and the attack-speed playback rate
headlessly (synthetic fixture, no disc). The C# wiring is compiled by `ror2-samus/build.ps1`
and its boundary checks. **In-game behaviour is not claimed** until the user plays it; the
sprint-jump boost, the stomp blast and the attack-speed scaling are "built and
headless-checked", not "played".
