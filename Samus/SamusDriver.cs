using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    // Samus-specific driver: the shared MeleeDriver owns the sim stepping,
    // collision, facing, hitbox sampling and recovery wiring; this class only
    // supplies Samus presentation (view, audio, grapple, particles/effects,
    // shield, entry platform, charge HUD) and her skill/control layout.
    [DefaultExecutionOrder(-200)]
    public sealed class SamusDriver : MeleeDriver
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Plugin.AssetsDir,MeleeNative.FighterSamus,model);
        static IMeleeObjectView ChargeView()=>new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,1,0);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        float chargeFraction;
        readonly Combat.HitTracker hits = new Combat.HitTracker();

        internal static Run LocalRun { get; private set; }
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        internal IntPtr Simulation => sim;
        internal float Units => scale;
        internal SamusGrab Grab { get; private set; }
        internal bool GrabHeld => pad.GrabHeld;
        internal bool JumpHeld => pad.JumpHeld;
        internal bool ControlsInterrupted => Recovering || Arriving || native.Control() != NativeBody.Hold.None;
        internal void AnchorGrapple(Vector3 desiredFeet)
        {
            if (!active) return;
            MeleeNative.ssbm_sim_state(sim, out var current);
            var contact = Collision(current.position, ToSim(desiredFeet));
            MeleeNative.ssbm_sim_anchor(sim, contact.position, contact.grounded);
        }

        // --- MeleeDriver seam -------------------------------------------------
        protected override string FighterName => "Samus";
        protected override string AssetsDir => Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterSamus;
        protected override float MetresPerUnit => Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Plugin.JumpScale.Value;
        protected override float SpeedScale => Plugin.SpeedScale.Value;
        protected override bool HoldCharge => Plugin.HoldCharge.Value;
        protected override bool AssetsReady => Plugin.AssetsReady;
        protected override bool AssetsBusy => Plugin.AssetsBusy;
        protected override uint AllowedButtons => Skills.Allowed(body);
        protected override bool ShieldSelected => Skills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool GrabAttached => Grab != null && Grab.Attached;
        protected override bool GrabHolding => Grab != null && Grab.Holding;
        protected override bool GenericGrab => false;
        protected override bool IsRangedMove(uint moveId) => moveId >= 20 && moveId <= 22;
        protected override bool IsChargeMove(uint moveId) => moveId == 20;

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterSamus, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, ChargeView, Plugin.LogEffects.Value);
            Grab = new SamusGrab(this, body);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void DisposePresentation()
        {
            shieldView?.Dispose(); shieldView = null;
            view?.Dispose(); view = null;
            entryPlatform?.Dispose(); entryPlatform = null;
            audio?.Dispose(); audio = null;
            Effects?.Dispose(); Effects = null;
            Particles?.Dispose(); Particles = null;
            Grab?.Dispose(); Grab = null;
        }
        protected override void ResetPresentation()
        {
            Grab?.Dispose(); Grab = new SamusGrab(this, body);
            Effects?.Dispose(); Particles?.Dispose();
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, ChargeView, Plugin.LogEffects.Value);
        }
        protected override void ResetEntryPlatform()
        {
            entryPlatform?.Dispose(); entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void StepPresentation(float dt)
        {
            Grab?.AfterStep(dt);
            Present(() => Effects?.Step(sim, dt));
            Present(() => Particles?.Step(dt));
            chargeFraction = MeleeNative.ssbm_sim_charge_fraction(sim);
        }
        protected override void DrawPresentation(Vector3 at, Quaternion rotation)
        {
            view.Pose(sim, Time.deltaTime);
            view.Models.Update(scale, d => (at, rotation));
            Present(() => {
                uint defense = MeleeNative.ssbm_sim_defense_flags(sim);
                if ((defense & 16) != 0) {
                    uint model = (defense & 32) != 0 ? 11u : (defense & 1) != 0 ? 13u : 14u;
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterSamus, model); shieldModel = model; }
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
            Present(() => Grab?.Draw());
        }
        protected override void DrawHud()
        {
            if (!Arriving && chargeFraction > 0) {
                string label = (state.phase == 4 ? "Charging" : "Stored Charge Shot") + ": " + Mathf.RoundToInt(chargeFraction * 100) + "%";
                if (chargeFraction >= 1) label = "Charge Shot ready";
                GUI.Box(new Rect(Screen.width / 2f - 110, Screen.height - 130, 220, 30), label);
            }
            if (Grab?.Holding ?? false) GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 165, 360, 30), "LS + LT/RT: throw    Neutral LT/RT: pummel");
        }
        protected override void BeginDeathPresentation(Vector3 feet, Quaternion rotation)
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 4), feet, rotation, scale));
        protected override void GrabDetach() => Grab.Detach();
        protected override void GrabReleaseCapture() => Grab.ReleaseCapture();
        protected override void GrabThrow(MeleeNative.Event e) => Grab?.Throw(e);
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            int slot = Skills.Slot(e.move_id);
            var skill = Skills.Get(body, slot);
            var expected = Skills.Expected(body, slot);
            bool own = skill.skillDef == expected;
            permitDamage = NativeSkills.Spend(skill, expected) && own;
        }
        protected override void OnEventHit(MeleeNative.Event e) => Combat.Attack(body, e, scale);
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in Skills.Slots) {
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
