using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Donkey
{
    // Donkey Kong's driver: the shared MeleeDriver owns sim stepping, collision,
    // facing, hitbox sampling and recovery; this supplies DK's presentation and
    // skill layout. No grapple tether, charge shot or projectile.
    [DefaultExecutionOrder(-200)]
    public sealed class DonkeyDriver : MeleeDriver
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Samus.Plugin.AssetsDir,MeleeNative.FighterDonkey,model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        readonly DonkeyCombat.HitTracker hits = new DonkeyCombat.HitTracker();

        protected override string FighterName => "Donkey Kong";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterDonkey;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale => Samus.Plugin.SpeedScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.DonkeyReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => DonkeySkills.Allowed(body);
        protected override bool ShieldSelected => DonkeySkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => false;
        protected override bool IsChargeMove(uint moveId) => false;

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = MeleeFighterView.ForBody(body, AssetsDir, MeleeNative.FighterDonkey, Math.Min(body.skinIndex, 4));
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
            DrawBody(view, at, rotation);
            Present(() => {
                uint defense = MeleeNative.ssbm_sim_defense_flags(sim);
                if ((defense & 16) != 0) {
                    uint model = (defense & 32) != 0 ? 11u : (defense & 1) != 0 ? 13u : 14u;
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterDonkey, model); shieldModel = model; }
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
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale, state.move_id);
        protected override MeleeSkillSet SkillSet => DonkeySkills.Built;
        protected override void OnEventHit(MeleeNative.Event e) => DonkeyCombat.Attack(body, e, scale);
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in DonkeySkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
