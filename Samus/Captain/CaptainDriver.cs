using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Captain
{
    // Captain Falcon's driver: the shared MeleeDriver owns sim stepping,
    // collision, facing, hitbox sampling and recovery; this supplies Falcon's
    // presentation and skill layout. No grapple tether or charge shot.
    [DefaultExecutionOrder(-200)]
    public sealed class CaptainDriver : MeleeDriver
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Samus.Plugin.AssetsDir,MeleeNative.FighterCaptain,model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        CaptainCombat.HitTracker hitTracker;
        CaptainCombat.HitTracker hits => hitTracker ?? (hitTracker = new CaptainCombat.HitTracker(this));

        protected override string FighterName => "Captain Falcon";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterCaptain;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale => Samus.Plugin.SpeedScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.CaptainReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => CaptainSkills.Allowed(body);
        protected override bool ShieldSelected => CaptainSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => false;
        protected override bool IsChargeMove(uint moveId) => false;

        // --- Raptor Boost and Falcon Dive ---------------------------------------------
        // The sim hears about contact before its next step (ssbm_sim_connect cannot run
        // inside a callback). The Dive takes hold of what its grab boxes touch: an
        // ordinary enemy is held in front of him through the explosion; a boss cannot
        // be carried, so Falcon clings to the spot he hit until he throws.
        bool connectQueued;
        EnemyCapture catchCapture;
        CharacterBody attachedBoss;
        Vector3 bossLocal;
        float pinUntil;
        internal void QueueConnect() => connectQueued = true;

        internal bool Catch(HealthComponent target, Vector3 point)
        {
            if (!target || !target.alive || (catchCapture != null && catchCapture.Target) || attachedBoss) return false;
            if (MeleeGrapple.IsBoss(target.body))
            {
                attachedBoss = target.body;
                bossLocal = target.body.transform.InverseTransformPoint(point);
                pinUntil = Time.time + 0.45f;
                connectQueued = true;
                return true;
            }
            if (catchCapture == null) catchCapture = new EnemyCapture(body);
            if (!catchCapture.TryCapture(target)) return false;
            connectQueued = true;
            return true;
        }

        // The explosion throw: whatever he caught is launched up and away.
        void ThrowCaught()
        {
            var forward = new Vector3(state.facing.x, 0f, state.facing.z).normalized;
            var launch = (forward * 0.7f + Vector3.up * 0.8f).normalized * CaptainCombat.ThrowForce;
            HealthComponent victim = catchCapture != null ? catchCapture.Target : null;
            if (!victim && attachedBoss) victim = attachedBoss.healthComponent;
            catchCapture?.Dispose();
            attachedBoss = null;
            if (!victim || !victim.alive) return;
            NativeHit.Deal(body, victim, new NativeHitSpec {
                coefficient = 2f, position = victim.body ? victim.body.corePosition : transform.position, force = launch,
                procCoefficient = 1f, damageType = DamageType.IgniteOnHit, color = DamageColorIndex.Default,
            });
        }

        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            if (connectQueued) { connectQueued = false; MeleeNative.ssbm_sim_connect(sim); }
            if (state.move_id != 23) { catchCapture?.Dispose(); attachedBoss = null; return; }
            var forward = new Vector3(state.facing.x, 0f, state.facing.z);
            if (catchCapture != null && catchCapture.Target) catchCapture.Step(forward, Time.fixedDeltaTime);
            if (attachedBoss && Time.time < pinUntil)
            {
                var anchor = attachedBoss.transform.TransformPoint(bossLocal);
                var feet = anchor - (body.corePosition - body.footPosition) - forward.normalized * (body.radius * 0.6f);
                MeleeNative.ssbm_sim_place(sim, ToSim(feet), 0);
            }
        }
        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterCaptain, Math.Min(body.skinIndex, 5));
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
            view.Models.Update(scale, d => (at, rotation));
            Present(() => {
                uint defense = MeleeNative.ssbm_sim_defense_flags(sim);
                if ((defense & 16) != 0) {
                    uint model = (defense & 32) != 0 ? 11u : (defense & 1) != 0 ? 13u : 14u;
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterCaptain, model); shieldModel = model; }
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
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale, state.move_id);
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            int slot = CaptainSkills.Slot(e.move_id);
            var skill = CaptainSkills.Get(body, slot);
            var expected = CaptainSkills.Expected(body, slot);
            bool own = skill.skillDef == expected;
            permitDamage = NativeSkills.Spend(skill, expected) && own;
        }
        protected override void OnEventHit(MeleeNative.Event e) { CaptainCombat.Attack(body, e, scale); if (e.move_id == 23 && e.strength >= 2f) ThrowCaught(); }
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in CaptainSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
