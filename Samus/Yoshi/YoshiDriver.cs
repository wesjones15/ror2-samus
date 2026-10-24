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
    public sealed class YoshiDriver : MeleeDriver
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
        bool connectQueued, releaseQueued, primaryWasDown;
        internal void QueueConnect() => connectQueued = true;
        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            if (releaseQueued) {
                releaseQueued = false;
                YoshiFull.For(body)?.Release();
            }
            if (!connectQueued) return;
            connectQueued = false;
            MeleeNative.ssbm_sim_connect(sim);
        }
        protected override bool IsChargeMove(uint moveId) => false;

        /// While full, Egg Lay lays the egg instead of starting another grab; sprint is off.
        protected override uint FilterButtons(uint buttons)
        {
            bool down = (buttons & MeleeControls.NeutralB) != 0;
            bool carrying = YoshiFull.IsCarrying(body), laying = YoshiFull.IsLaying(body);
            if (carrying && down && !primaryWasDown) releaseQueued = true;
            primaryWasDown = down;
            if (!carrying && !laying) return buttons;
            return buttons & ~(MeleeControls.NeutralB | MeleeControls.Sprint);
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
        // camera line (or the full 20 m when the lash is thrown into empty space).
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            if (e.move_id != 20) return;
            var aim = body.inputBank ? MeleeAim.CameraRay(body, body.inputBank).direction : body.transform.forward;
            YoshiTongue.Show(body, aim, YoshiCombat.ScanReach(body, aim));
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
            // walking, and SpecialN2 once he lays the egg. During an actual move the sim's
            // own pose wins, so attacks and the egg roll still read.
            var full = YoshiFull.For(body);
            if (full != null && state.move_id == 0) {
                Present(() => {
                    if (full.TryPose(Time.deltaTime, in state, out var clip, out var frame))
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
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);
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
