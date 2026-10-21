using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Fox
{
    // Fox-specific driver: the shared MeleeDriver owns sim stepping, collision,
    // facing, hitbox sampling and recovery; this supplies Fox's presentation
    // and skill layout. No grapple tether or charge shot.
    [DefaultExecutionOrder(-200)]
    public sealed class FoxDriver : MeleeDriver
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Samus.Plugin.AssetsDir,MeleeNative.FighterFox,model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        // The Blaster's gun (the disc's second Fox article) while he shoots.
        MeleeFighterView gunView;
        readonly float[] gunMatrix = new float[16];
        readonly FoxCombat.HitTracker hits = new FoxCombat.HitTracker();

        internal static Run LocalRun { get; private set; }
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }

        protected override string FighterName => "Fox";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterFox;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale => Samus.Plugin.SpeedScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.FoxReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => FoxSkills.Allowed(body);
        protected override bool ShieldSelected => FoxSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => moveId == 20;
        protected override bool IsChargeMove(uint moveId) => false;

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterFox, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void DisposePresentation()
        {
            shieldView?.Dispose(); shieldView = null;
            gunView?.Dispose(); gunView = null;
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
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterFox, model); shieldModel = model; }
                    shieldView.ObjectPose(MeleeNative.ssbm_sim_motion_frame(sim));
                    shieldView.Models.Update(scale * MeleeNative.ssbm_sim_shield_scale(sim), d => (body.corePosition, rotation));
                } else shieldView?.Models.HideAll();
            });
            if (entryPlatform != null) {
                if (MeleeNative.ssbm_entry_platform_update(entryPlatform.Handle, sim) == 1) entryPlatform.Models.Update(scale, d => (at, rotation));
                else { entryPlatform.Dispose(); entryPlatform = null; }
            }
            Present(() => DrawGun());
            Present(() => Particles?.Draw());
            Present(() => Effects?.Draw());
        }
        // Blaster (native move 20): the gun rides the right thumb (FtPart_RThumbNb 49), as
        // the source attaches its Blaster item in ftFx_SpecialN_Enter.
        void DrawGun()
        {
            if (state.move_id != 20 || MeleeNative.ssbm_sim_common_bone(sim, 49, gunMatrix) != 1) { gunView?.Models.HideAll(); return; }
            if (gunView == null) gunView = new MeleeFighterView(AssetsDir, MeleeNative.FighterFox, 1, 0);
            var m = new Matrix4x4();
            for (int n = 0; n < 16; n++) m[n] = gunMatrix[n];
            m *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
            var f = m.GetColumn(2); var u = m.GetColumn(1);
            var rot = Quaternion.LookRotation(new Vector3(f.x, f.y, f.z), new Vector3(u.x, u.y, u.z));
            var pos = new Vector3(gunMatrix[12], gunMatrix[13], gunMatrix[14]) * scale;
            gunView.ObjectPose(MeleeNative.ssbm_sim_motion_frame(sim));
            gunView.Models.Update(scale, d => (pos, rot));
        }
        protected override void DrawHud() { }
        protected override void BeginDeathPresentation(Vector3 feet, Quaternion rotation)
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 4), feet, rotation, scale));
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            int slot = FoxSkills.Slot(e.move_id);
            var skill = FoxSkills.Get(body, slot);
            var expected = FoxSkills.Expected(body, slot);
            bool own = skill.skillDef == expected;
            permitDamage = NativeSkills.Spend(skill, expected) && own;
        }
        protected override void OnEventHit(MeleeNative.Event e) => FoxCombat.Attack(body, e, scale);
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in FoxSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
        protected override void OnSimStarted()
        {
            if (body.isPlayerControlled && body.hasEffectiveAuthority) LocalRun = Run.instance;
        }
    }
}
