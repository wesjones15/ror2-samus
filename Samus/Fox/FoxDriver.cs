using System;
using System.Collections.Generic;
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
        // Fox Illusion's blue afterimages: the It_Kind_Fox_Illusion article, laid at the
        // positions Fox has just left (ftfoxspecials.c::ftFox_SpecialS_SetPhys and
        // itfoxillusion.c::itFoxillusion_Phys keep four historical positions; the item
        // is drawn one frame behind and its ghost copy three frames behind).
        MeleeFighterView illusionView;
        readonly List<(Vector3 at, Quaternion rotation, float time)> illusionTrail = new List<(Vector3 at, Quaternion rotation, float time)>();
        readonly float[] gunMatrix = new float[16];
        readonly MeleeHitTracker hits = new MeleeHitTracker();

        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        protected override MeleeEffects EffectsForSparks => Effects;

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
            view = MeleeFighterView.ForBody(body, AssetsDir, MeleeNative.FighterFox, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void DisposePresentation()
        {
            shieldView?.Dispose(); shieldView = null;
            gunView?.Dispose(); gunView = null;
            illusionView?.Dispose(); illusionView = null; illusionTrail.Clear();
            view?.Dispose(); view = null;
            entryPlatform?.Dispose(); entryPlatform = null;
            audio?.Dispose(); audio = null;
            Effects?.Dispose(); Effects = null;
            Particles?.Dispose(); Particles = null;
        }
        protected override void ResetPresentation()
        {
            illusionView?.Dispose(); illusionView = null; illusionTrail.Clear();
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
            Present(() => KeepIllusionAboveGround());
            Present(() => Effects?.Step(sim, dt));
            Present(() => Particles?.Step(dt));
        }
        // A horizontal Illusion can end up inside a rising slope before the host
        // collision callback sees it. While the dash is active, lift the sim onto a
        // nearby non-flat floor instead of letting the visual and the sim go under
        // the map. Only a slope (0.55..0.97 up normal) is used, so a platform above
        // the fighter is never mistaken for ground; the sim is never pulled down.
        void KeepIllusionAboveGround()
        {
            if (!MeleeNative.HasNudge || !MeleeNet.Acts(body)) return;
            if (MeleeNative.ssbm_sim_state(sim, out var current) != 1 || current.move_id != 21) return;
            var feet = ToWorld(current.position);
            if (!WorldProbe.Ground(feet + Vector3.up * 3f, 6f, out var floor)) return;
            if (floor.normal.y <= 0.55f || floor.normal.y >= 0.97f) return;
            float lift = floor.point.y - feet.y;
            if (lift <= 0.02f || lift > 2.5f) return;
            MeleeNative.ssbm_sim_nudge(sim, ToSim(new Vector3(feet.x, floor.point.y + 0.02f, feet.z)), 1);
        }
        protected override void DrawPresentation(Vector3 at, Quaternion rotation)
        {
            view.Pose(sim, Time.deltaTime);
            DrawBody(view, at, rotation);
            Present(() => DrawIllusionGhosts(at, rotation));
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
        // Fox Illusion (native move 21) leaves the blue ghost copies of his body behind him.
        // The source keeps four positions and one X rotation every dash tick; its
        // It_Kind_Fox_Illusion item sits at history[1] and the extra ghost model at
        // history[3] (ftfoxspecials.c, itfoxillusion.c). The article itself carries the
        // translucent blue Fox trail, so one draw per historical position is enough.
        void DrawIllusionGhosts(Vector3 at, Quaternion rotation)
        {
            float now = Time.unscaledTime;
            var velocity = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z);
            // SpecialS is the dash; the 40-frame end clip keeps the last ghosts in place
            // until the move ends, as ftFx_SpecialS_CheckGhostRemove does.
            bool dashing = state.move_id == 21 && velocity.sqrMagnitude > 16f;
            if (dashing)
            {
                if (illusionTrail.Count == 0 || now - illusionTrail[0].time >= 1f / 60f)
                {
                    illusionTrail.Insert(0, (at, rotation, now));
                    if (illusionTrail.Count > 8) illusionTrail.RemoveAt(8);
                }
            }
            else if (state.move_id != 21) illusionTrail.Clear();
            // One ghost at history[1], a second at history[3], as itfoxillusion.c does.
            int count = 0;
            if (illusionTrail.Count > 0)
            {
                float span = now - illusionTrail[illusionTrail.Count - 1].time;
                count = span >= 3f / 60f ? 2 : illusionTrail.Count >= 2 ? 1 : 0;
            }
            if (count == 0) { illusionView?.Models.HideAll(); return; }
            (Vector3 at, Quaternion rotation) At(float time)
            {
                var best = illusionTrail[0];
                float bestDelta = Mathf.Abs(best.time - time);
                for (int i = 1; i < illusionTrail.Count; i++)
                {
                    float delta = Mathf.Abs(illusionTrail[i].time - time);
                    if (delta < bestDelta) { best = illusionTrail[i]; bestDelta = delta; }
                }
                return (best.at, best.rotation);
            }
            if (illusionView == null) illusionView = new MeleeFighterView(AssetsDir, MeleeNative.FighterFox, 2, 0);
            if (illusionView.InstanceDraws == null || illusionView.InstanceDraws.Length != count)
            {
                var draws = new ViewDraw[count];
                for (int i = 0; i < count; i++) draws[i] = new ViewDraw { key = (ulong)(i + 1), alpha = 1f };
                illusionView.InstanceDraws = draws;
            }
            var near = At(now - 1f / 60f);
            var far = At(now - 3f / 60f);
            illusionView.ObjectPose(0f);
            illusionView.Models.Update(scale, d => d.key == 1 ? near : far);
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
        protected override MeleeSkillSet SkillSet => FoxSkills.Built;
        // The shine's bubble: up from its start event until the release event, the jump cancel or a hit.
        bool reflecting;
        float reflectDamage = 1.5f, reflectSpeed = 1f;
        protected override void OnEventHit(MeleeNative.Event e)
        {
            if (e.move_id == 24)
            {
                reflecting = e.strength < 1.5f;
                if (reflecting) { reflectDamage = Mathf.Max(e.direction.y, 1f); reflectSpeed = e.direction.z > 0f ? e.direction.z : 1f; }
            }
            FoxCombat.Attack(body, e, scale);
        }
        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            if (!reflecting) return;
            if (state.move_id != 24) { reflecting = false; return; }
            if (!IsOwner) return;
            // Host adaptation: the bubble is as big as the burst, so a shot at range is caught the way
            // a Melee shot is caught at the bubble's edge.
            MeleeReflect.Reflect(body, body.corePosition, FoxCombat.Reflector.RadiusUnits * scale, reflectDamage, reflectSpeed);
        }
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
    }
}
