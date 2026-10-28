using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Link
{
    // Link's driver: the shared MeleeDriver owns sim stepping, collision, facing,
    // hitbox sampling and recovery; this supplies Link's presentation and skill
    // layout. The grab button also runs the hookshot (LinkGrab): a long chain, a
    // wall/boss anchor with jump restore, or a far capture that pulls the enemy in.
    [DefaultExecutionOrder(-200)]
    public sealed class LinkDriver : MeleeDriver
    {
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Samus.Plugin.AssetsDir,MeleeNative.FighterLink,model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        MeleeHeldArticle bow;
        LinkHookshot hook;
        readonly MeleeHitTracker hits = new MeleeHitTracker();

        protected override string FighterName => "Link";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterLink;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale => Samus.Plugin.SpeedScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.LinkReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        // With a bomb in his hand B throws it and LT drops it (BeforeStep), so the sim must not
        // start a jab or pull a second bomb on the same press.
        protected override uint AllowedButtons => LinkSkills.Allowed(body) & ~(LinkShot.CarriedBomb(body) != null ? (2u | 16u) : 0u);
        protected override bool ShieldSelected => LinkSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => moveId == 20 || moveId == 21 || moveId == 24;
        protected override bool IsChargeMove(uint moveId) => moveId == 20;
        protected override bool GrabAttached => hook != null && hook.Anchored;
        protected override bool GrabHolding => (hook != null && hook.Holding) || base.GrabHolding;

        readonly float[] handMatrix = new float[16];
        readonly float[] rightHandMatrix = new float[16];
        uint previousButtons;
        /// Link's left hand (FtPart_LThumbNb) in the world, where the bomb is carried.
        internal Vector3 HandWorld()
        {
            if (sim != IntPtr.Zero && MeleeNative.ssbm_sim_common_bone(sim, 31, handMatrix) == 1)
                return new Vector3(handMatrix[12], handMatrix[13], handMatrix[14]) * scale;
            return body.corePosition;
        }
        /// Link's right hand (FtPart_RThumbNb), where the hookshot leaves
        /// (ftCo_0D8E.c::fn_800D8EC8, ftCo_AirCatch.c::ftCo_AirCatch_Anim).
        internal Vector3 RightHandWorld()
        {
            if (sim != IntPtr.Zero && MeleeNative.ssbm_sim_common_bone(sim, 49, rightHandMatrix) == 1)
                return new Vector3(rightHandMatrix[12], rightHandMatrix[13], rightHandMatrix[14]) * scale;
            return body.corePosition;
        }
        internal float Units => scale;
        internal bool HealthAlive => body && body.healthComponent && body.healthComponent.alive;
        internal bool ControlsInterrupted => Recovering || Arriving || native.Control() != NativeBody.Hold.None;
        internal bool Grounded { get { return state.grounded != 0; } }
        internal Vector3 SimFeet => ToWorld(state.position);
        internal Vector3 SimCore => SimFeet + (body.corePosition - body.footPosition);
        internal float ClimbAxis => pad.GrabAxisY;
        internal HealthComponent CapturedTarget => Capture != null ? Capture.Target : null;
        internal bool TryCaptureTarget(HealthComponent target) => TryGrabTarget(target);
        internal void ResetJumps()
        {
            if (MeleeNative.HasJumpReset) MeleeNative.ssbm_sim_reset_jumps(sim);
        }
        internal void AnchorHookshot(Vector3 desiredFeet)
        {
            if (!active) return;
            MeleeNative.ssbm_sim_state(sim, out var current);
            var contact = Collision(current.position, ToSim(desiredFeet));
            MeleeNative.ssbm_sim_anchor(sim, contact.position, contact.grounded);
        }
        protected override bool CanCapture(HealthComponent target) =>
            target != null && target.body != null && !LinkHookshot.IsAnchorBody(target.body);

        protected override bool StepCapture(Vector3 forward, float dt)
        {
            if (hook == null || !hook.PullingEnemy) return base.StepCapture(forward, dt);
            var target = CapturedTarget;
            if (target == null || !target.body) return base.StepCapture(forward, dt);
            var hold = body.corePosition + forward.normalized * (body.radius + target.body.radius + 0.35f);
            var at = Vector3.MoveTowards(target.body.corePosition, hold, LinkHookshot.PullSpeed * dt);
            bool held = Capture.Step(forward, dt, at);
            if (Vector3.Distance(at, hold) < 0.08f) hook.EnemyArrived();
            return held;
        }

        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            // What the world is doing that the sim cares about: a boomerang still out
            // plays the empty-handed throw, a bomb in hand is not pulled twice.
            MeleeNative.ssbm_sim_set_host_flags(sim, LinkShot.HostFlags(body));
            uint pressed = pad.buttons & ~previousButtons;
            previousButtons = pad.buttons;
            var bomb = LinkShot.CarriedBomb(body);
            if ((pressed & MeleeControls.Grab) != 0)
            {
                bool held = (pad.buttons & MeleeControls.Grab) != 0;
                if (hook != null && hook.Active) hook.Release();
                else if (hook != null && held && !ControlsInterrupted && state.phase == 0) {
                    var facing = direction ? direction.forward : new Vector3(state.facing.x, 0f, state.facing.z);
                    hook.Fire(facing);
                }
            }
            if (bomb == null) return;
            if ((pressed & MeleeControls.Attack) != 0)
            {
                var muzzle = bomb.transform.position;
                bomb.Throw(body.inputBank ? MeleeAim.ProjectileDirection(body, muzzle, MeleeAim.CameraRay(body, body.inputBank)) : body.transform.forward);
            }
            else if ((pressed & MeleeControls.DownB) != 0) bomb.Drop();
        }

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = MeleeFighterView.ForBody(body, AssetsDir, MeleeNative.FighterLink, Math.Min(body.skinIndex, 4));
            bow = MeleeHeldArticle.ForFighter(AssetsDir, FighterKind);
            hook = new LinkHookshot(this, body);
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        protected override MeleeEffects EffectsForSparks => Effects;
        // Link's sword trail (the afterimage his scripts switch on during a swing).
        MeleeTrail trail;
        protected override void DisposePresentation()
        {
            bow?.Dispose(); bow = null;
            hook?.Dispose(); hook = null;
            trail?.Dispose(); trail = null;
            shieldView?.Dispose(); shieldView = null;
            view?.Dispose(); view = null;
            entryPlatform?.Dispose(); entryPlatform = null;
            audio?.Dispose(); audio = null;
            Effects?.Dispose(); Effects = null;
            Particles?.Dispose(); Particles = null;
        }
        protected override void ResetPresentation()
        {
            bow?.Hide();
            hook?.Dispose(); hook = new LinkHookshot(this, body);
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
            Present(() => hook?.AfterStep(dt));
            Present(() => Effects?.Step(sim, dt));
            Present(() => Particles?.Step(dt));
        }
        protected override void DrawPresentation(Vector3 at, Quaternion rotation)
        {
            view.Pose(sim, Time.deltaTime);
            view.Models.Update(scale, d => (at, rotation));
            Present(() => bow?.Draw(sim, scale, at, rotation));
            Present(() => {
                uint defense = MeleeNative.ssbm_sim_defense_flags(sim);
                if ((defense & 16) != 0) {
                    uint model = (defense & 32) != 0 ? 11u : (defense & 1) != 0 ? 13u : 14u;
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterLink, model); shieldModel = model; }
                    shieldView.ObjectPose(MeleeNative.ssbm_sim_motion_frame(sim));
                    shieldView.Models.Update(scale * MeleeNative.ssbm_sim_shield_scale(sim), d => (body.corePosition, rotation));
                } else shieldView?.Models.HideAll();
            });
            if (entryPlatform != null) {
                if (MeleeNative.ssbm_entry_platform_update(entryPlatform.Handle, sim) == 1) entryPlatform.Models.Update(scale, d => (at, rotation));
                else { entryPlatform.Dispose(); entryPlatform = null; }
            }
            Present(() => { if (trail == null) trail = new MeleeTrail(); trail.Draw(sim, scale); });
            Present(() => hook?.Draw());
            Present(() => Particles?.Draw());
            Present(() => Effects?.Draw());
        }
        protected override void DrawHud()
        {
            if (hook != null && hook.Anchored)
                GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 130, 360, 30), "Jump: release hook    LS up/down: climb");
        }
        protected override void BeginDeathPresentation(Vector3 feet, Quaternion rotation)
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 4), feet, rotation, scale));
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);
        protected override MeleeSkillSet SkillSet => LinkSkills.Built;
        protected override void OnEventHit(MeleeNative.Event e) => LinkCombat.Attack(body, e, scale);
        protected override void GrabDetach() => hook?.Release();
        protected override void GrabReleaseCapture() { base.GrabReleaseCapture(); hook?.Release(); }
        protected override void GrabThrow(MeleeNative.Event e) { base.GrabThrow(e); hook?.Release(); }
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in LinkSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
