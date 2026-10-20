using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Pikachu
{
    // Pikachu's driver: the shared MeleeDriver owns sim stepping, collision, facing,
    // hitbox sampling and recovery; this supplies his presentation, skill layout and
    // host-side context for his staged specials (the Quick Attack lock-on).
    [DefaultExecutionOrder(-200)]
    public sealed class PikachuDriver : MeleeDriver
    {
        const uint ThunderShock = 20, SkullBash = 21, QuickAttack = 23;

        static IMeleeObjectView EffectView(uint model) => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        // The same lock-on Olimar's RT and Mario's LT use: the enemy nearest where the
        // camera points, preferring ones in front, kept until the move ends.
        readonly EnemyLock enemyLock = new EnemyLock();
        bool locked;
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        internal IntPtr NativeSim => sim;
        readonly MeleeHitTracker hits = new MeleeHitTracker();

        protected override string FighterName => "Pikachu";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterPikachu;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.PikachuReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => PikachuSkills.Allowed(body);
        protected override bool ShieldSelected => PikachuSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => PikachuSkills.Controls;
        protected override bool GrabAttached => false;
        protected override bool GrabHolding => false;
        protected override bool IsRangedMove(uint moveId) => moveId == ThunderShock;
        protected override bool IsChargeMove(uint moveId) => false;

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterPikachu, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
            hits.Stagger = Samus.Plugin.StaggerDuration.Value;
            // Skull Bash stops on its victim (ftPk_SpecialS_ZeroVelocity).
            hits.Connected += (victim, at) => { if (state.move_id == SkullBash && MeleeNative.HasStagedSpecials) MeleeNative.ssbm_sim_connect(sim); };
        }
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
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterPikachu, model); shieldModel = model; }
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
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 4), feet, rotation, scale));
        protected override void GrabDetach() { }
        protected override void GrabReleaseCapture() { }
        protected override void GrabThrow(MeleeNative.Event e) { }
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);

        // Quick Attack steers to the enemy the lock-on picks, chosen when LB is pressed and
        // kept while the move runs; the sim spends its two source zips getting there.
        protected override void BeforeStep(in MeleeNative.Pad p)
        {
            if (!MeleeNative.HasStagedSpecials) return;
            bool quick = (p.buttons & MeleeControls.UpB) != 0 || state.move_id == QuickAttack;
            if (!quick)
            {
                if (locked) { MeleeNative.ssbm_sim_lock_target(sim, default, 0); locked = false; }
                enemyLock.Update(body, false, false, Vector2.right);
                return;
            }
            enemyLock.Range = Samus.Plugin.LockRange.Value;
            var target = enemyLock.Update(body, true, false, Vector2.right);
            if (!target) { if (locked) { MeleeNative.ssbm_sim_lock_target(sim, default, 0); locked = false; } return; }
            // The sim places Pikachu by his feet; aim his centre at the enemy's.
            var feetToCore = body.corePosition - body.footPosition;
            MeleeNative.ssbm_sim_lock_target(sim, ToSim(target.corePosition - feetToCore), 1);
            locked = true;
        }

        protected override void OnEventStarted(MeleeNative.Event e)
        {
            int slot = PikachuSkills.Slot(e.move_id);
            var skill = PikachuSkills.Get(body, slot);
            var expected = PikachuSkills.Expected(body, slot);
            bool own = skill.skillDef == expected;
            permitDamage = NativeSkills.Spend(skill, expected) && own;
        }
        protected override void OnEventHit(MeleeNative.Event e) => PikachuCombat.Attack(this, body, e, scale);
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in PikachuSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
