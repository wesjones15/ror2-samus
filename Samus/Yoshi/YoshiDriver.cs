using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Yoshi
{
    // Yoshi's driver: the shared MeleeDriver owns sim stepping, collision,
    // facing, hitbox sampling and recovery; this supplies Yoshi's presentation
    // and skill layout. The Egg Throw (SpecialHi) is a ranged projectile.
    [DefaultExecutionOrder(-200)]
    public sealed class YoshiDriver : MeleeDriver, IMeleeBypassDefense
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Samus.Plugin.AssetsDir,MeleeNative.FighterYoshi,model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        readonly MeleeHitTracker hits = new MeleeHitTracker();

        protected override string FighterName => "Yoshi";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterYoshi;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale
            => Samus.Plugin.SpeedScale.Value
                * (YoshiFull.IsCarrying(body) || YoshiFull.IsLaying(body) ? 0.45f : 1f);
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.YoshiReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => YoshiSkills.Allowed(body);
        protected override bool ShieldSelected => YoshiSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => moveId == 20 || moveId == 23; // Egg Lay's tongue, Egg Throw

        // The tongue reports a swallow while the sim is stepping; the sim hears about it
        // before the next step (ssbm_sim_connect cannot run inside a callback).
        // Egg-roll armor, as a fraction of maximum health. Every hit while rolling is
        // rejected, the pool drains, and when it is gone the egg breaks open. No health
        // damage reaches Yoshi while he is in the egg.
        const float RollArmorFraction = 0.5f, RollDamageScale = 2.5f, RollForceScale = 3f;
        float rollArmor;

        bool connectQueued;
        internal void QueueConnect() => connectQueued = true;
        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            if (!connectQueued) return;
            connectQueued = false;
            MeleeNative.ssbm_sim_connect(sim);
        }
        protected override bool IsChargeMove(uint moveId) => false;

        /// Egg Lay's charge is spent when the tongue actually swallows something, not when
        /// the lash is thrown: a miss leaves the stock and cooldown untouched.
        protected override bool SpendsOnStart(uint moveId) => moveId != 20;

        /// While full, Egg Lay is masked (the egg is laid automatically) and sprint is off.
        protected override uint FilterButtons(uint buttons)
        {
            if (!YoshiFull.IsCarrying(body) && !YoshiFull.IsLaying(body)) return buttons;
            return buttons & ~(MeleeControls.NeutralB | MeleeControls.Sprint);
        }

        /// The egg absorbs hits until its armor runs out. No health damage lands while the
        /// roll is out; the hit that empties the pool breaks the egg (and is absorbed too).
        public override bool BlockMeleeDamage(float healthFraction)
        {
            if (!active || Recovering || state.move_id != 21) return base.BlockMeleeDamage(healthFraction);
            AbsorbRollDamage(healthFraction);
            return true;
        }
        public bool BlockBypassDamage(float healthFraction)
        {
            if (!active || Recovering || state.move_id != 21) return false;
            AbsorbRollDamage(healthFraction);
            return true;
        }
        void AbsorbRollDamage(float healthFraction)
        {
            if (rollArmor <= 0f) return;
            rollArmor -= Mathf.Max(0f, healthFraction);
            if (rollArmor > 0f) return;
            rollArmor = 0f;
            if (sim != IntPtr.Zero) MeleeNative.ssbm_sim_break_roll(sim);
            PlaySourceSound(0x44618); // the egg shell breaks
        }

        readonly float[] mouthBone = new float[16];
        /// The live animated mouth bone, in world metres. Yoshi's model joint 42 is the
        /// jaw/mouth; unlike a fixed core-height point it follows the head down when the
        /// tongue-out clip plays.
        internal Vector3 Mouth()
        {
            if (sim != IntPtr.Zero && MeleeNative.ssbm_sim_bone(sim, 42, mouthBone) == 1)
            {
                var point = new Vector3(mouthBone[12], mouthBone[13], mouthBone[14]) * scale;
                var forward = body ? body.transform.forward : Vector3.forward;
                return point + new Vector3(forward.x, 0f, forward.z).normalized * 0.12f;
            }
            return YoshiCombat.MouthFallback(body);
        }

        internal void PlaySourceSound(uint sound)
        {
            if (sim == IntPtr.Zero) return;
            if (MeleeNative.ssbm_sim_play_sound(sim, sound) != 1)
                CommonLog.Warn("Yoshi sound " + sound + ": " + MeleeNative.Error);
        }

        /// Spawn one of Yoshi's own effect models at a world point (the egg shell bits,
        /// 1231, are the source's 0x4CF impact).
        internal void PlayEffect(uint id, Vector3 world, Quaternion rotation)
        {
            if (Effects == null || scale <= 0f) return;
            Present(() => Effects.Spawn(id, Matrix4x4.TRS(world / scale, rotation, new Vector3(-1, 1, 1))));
        }

        // Egg Lay draws its tongue from the first frame of the clip, so it is already out
        // when the sim's frame-20 grab event runs. It stretches to the first enemy in the
        // camera line, or stops at terrain, or reaches the full 20 m into empty space.
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            // 21 is the egg roll: a fresh armor pool for this roll.
            if (e.move_id == 21) { rollArmor = RollArmorFraction; return; }
            if (e.move_id != 20) return;
            var mouth = Mouth();
            var target = YoshiCombat.CrosshairPoint(body, mouth);
            YoshiTongue.Show(body, target);
        }

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterYoshi, Math.Min(body.skinIndex, 5));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        protected override MeleeEffects EffectsForSparks => Effects;
        protected override void DisposePresentation()
        {
            shieldView?.Dispose(); shieldView = null;
            view?.Dispose(); view = null;
            entryPlatform?.Dispose(); entryPlatform = null;
            audio?.Dispose(); audio = null;
            Effects?.Dispose(); Effects = null;
            Particles?.Dispose(); Particles = null;
        }
        protected override void ResetPresentation()
        {
            Effects?.Dispose(); Particles?.Dispose();
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
        }
        protected override void ResetEntryPlatform()
        {
            entryPlatform?.Dispose(); entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void StepPresentation(float dt)
        {
            Present(() => Effects?.Step(sim, dt));
            Present(() => Particles?.Step(dt));
        }
        protected override void DrawPresentation(Vector3 at, Quaternion rotation)
        {
            view.Pose(sim, Time.deltaTime);
            // A full Yoshi shows Melee's heavy-carry clips (WaitItem / HeavyWalk1/2) while
            // walking, and one SpecialN2 per victim while laying the queue. During an actual
            // move the sim's own pose wins, so attacks and the egg roll still read.
            if (state.move_id == 0) {
                Present(() => {
                    if (YoshiFull.TryPoseAny(body, Time.deltaTime, in state, out var clip, out var frame))
                        view.ClipPose(clip, frame);
                });
            }
            // The egg model replaces the body through the fighter's visibility table and
            // is spun about its own centre by the presentation's roll angle. Draw it a
            // little larger than the body so it reads as Yoshi curled inside.
            view.Models.Update(state.move_id == 23 ? scale * 1.2f : scale, d => (at, rotation));
            Present(() => {
                uint defense = MeleeNative.ssbm_sim_defense_flags(sim);
                if ((defense & 16) != 0) {
                    uint model = (defense & 32) != 0 ? 11u : (defense & 1) != 0 ? 13u : 14u;
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterYoshi, model); shieldModel = model; }
                    shieldView.ObjectPose(MeleeNative.ssbm_sim_motion_frame(sim));
                    shieldView.Models.Update(scale * MeleeNative.ssbm_sim_shield_scale(sim), d => (body.corePosition, rotation));
                } else shieldView?.Models.HideAll();
            });
            if (entryPlatform != null) {
                if (MeleeNative.ssbm_entry_platform_update(entryPlatform.Handle, sim) == 1) entryPlatform.Models.Update(scale, d => (at, rotation));
                else { entryPlatform.Dispose(); entryPlatform = null; }
            }
            Present(() => Particles?.Draw());
            Present(() => Effects?.Draw());
        }
        protected override void DrawHud() { }
        protected override void BeginDeathPresentation(Vector3 feet, Quaternion rotation)
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 5), feet, rotation, scale));
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale)
        {
            // The rolling egg is a bowling ball: keep the authored hitboxes and timing but
            // hit much harder and send victims flying.
            if (state.move_id == 21) {
                for (int i = 0; i < count; i++) {
                    samples[i].damage *= RollDamageScale;
                    samples[i].base_knockback = (uint)Mathf.Round(samples[i].base_knockback * RollForceScale);
                    samples[i].growth = (uint)Mathf.Round(samples[i].growth * RollForceScale);
                }
            }
            hits.Apply(body, samples, count, scale);
        }
        protected override MeleeSkillSet SkillSet => YoshiSkills.Built;
        protected override void OnEventHit(MeleeNative.Event e) => YoshiCombat.Attack(body, e, scale);
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in YoshiSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
