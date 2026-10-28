using System;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Luigi
{
    // Luigi-specific driver: the shared MeleeDriver owns sim stepping, collision,
    // facing, hitbox sampling and recovery; this supplies Luigi's presentation
    // and skill layout. No grapple tether or charge shot.
    [DefaultExecutionOrder(-200)]
    public sealed class LuigiDriver : MeleeDriver
    {
        // Effects are shared assets, not part of a body: the missile's ignition flame is Fox's
        // Fire Fox cue (model 2004) and lives in his bank, so resolve every cue across all banks.
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.EffectAny(Samus.Plugin.AssetsDir,MeleeNative.FighterLuigi,model);
        MeleeFighterView view, entryPlatform, shieldView, scopeView; uint shieldModel;
        // The Super Scope: whether it is in hand, and the right-hand bone it is drawn on.
        bool scopeOn, scopeWarned;
        readonly float[] scopeMatrix = new float[16];
        MeleeAudio audio;
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        protected override MeleeEffects EffectsForSparks => Effects;
        readonly LuigiCombat.HitTracker hits = new LuigiCombat.HitTracker();

        protected override string FighterName => "Luigi";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterLuigi;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale => Samus.Plugin.SpeedScale.Value;
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.LuigiReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => LuigiSkills.Allowed(body);
        protected override bool ShieldSelected => LuigiSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => moveId == 20 || moveId == 21 || moveId == 17;
        protected override bool IsChargeMove(uint moveId) => false;

        protected override void UpdateAudio() => audio?.Update();
        protected override void CreatePresentation()
        {
            view = MeleeFighterView.ForBody(body, AssetsDir, MeleeNative.FighterLuigi, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
            entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void DisposePresentation()
        {
            shieldView?.Dispose(); shieldView = null;
            scopeView?.Dispose(); scopeView = null;
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
                    if (shieldView == null || shieldModel != model) { shieldView?.Dispose(); shieldView = MeleeFighterView.Effect(AssetsDir, MeleeNative.FighterLuigi, model); shieldModel = model; }
                    shieldView.ObjectPose(MeleeNative.ssbm_sim_motion_frame(sim));
                    shieldView.Models.Update(scale * MeleeNative.ssbm_sim_shield_scale(sim), d => (body.corePosition, rotation));
                } else shieldView?.Models.HideAll();
            });
            if (entryPlatform != null) {
                if (MeleeNative.ssbm_entry_platform_update(entryPlatform.Handle, sim) == 1) entryPlatform.Models.Update(scale, d => (at, rotation));
                else { entryPlatform.Dispose(); entryPlatform = null; }
            }
            Present(() => DrawScope());
            Present(() => Particles?.Draw());
            Present(() => Effects?.Draw());
        }
        // The Super Scope rides the right hand (the same bone Fox's Blaster does) while it is being used.
        void DrawScope()
        {
            if (state.move_id != 17 || !MeleeNative.HasScope || MeleeNative.ssbm_sim_common_bone(sim, 49, scopeMatrix) != 1)
            {
                scopeView?.Models.HideAll(); return;
            }
            if (scopeView == null) scopeView = MeleeFighterView.Item(AssetsDir, 0x15, 0);
            var m = new Matrix4x4();
            for (int n = 0; n < 16; n++) m[n] = scopeMatrix[n];
            m *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
            var f = m.GetColumn(2); var u = m.GetColumn(1);
            var rot = Quaternion.LookRotation(new Vector3(f.x, f.y, f.z), new Vector3(u.x, u.y, u.z));
            var pos = new Vector3(scopeMatrix[12], scopeMatrix[13], scopeMatrix[14]) * scale;
            scopeView.ObjectPose(0f);
            scopeView.Models.Update(scale, d => (pos, rot));
        }
        bool ScopeMuzzle(out Vector3 muzzle, out Quaternion rotation)
        {
            muzzle = Vector3.zero; rotation = Quaternion.identity;
            if (MeleeNative.ssbm_sim_common_bone(sim, 49, scopeMatrix) != 1) return false;
            var m = new Matrix4x4();
            for (int n = 0; n < 16; n++) m[n] = scopeMatrix[n];
            m *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
            var f = m.GetColumn(2); var u = m.GetColumn(1);
            rotation = Quaternion.LookRotation(new Vector3(f.x, f.y, f.z), new Vector3(u.x, u.y, u.z));
            muzzle = new Vector3(scopeMatrix[12], scopeMatrix[13], scopeMatrix[14]) * scale
                + rotation * (Vector3.up * 5.7f * scale);
            return true;
        }
        // The charge's flash (cue 0x3F3) and ground dust (0x3FF, every eight frames) are the Start script's own
        // GFX events: the sim queues them and MeleeEffects.Step spawns them like any other cue.
        // Gives the sim the scope while the loadout holds it (and the items archive was extracted), every step.
        void UpdateScope()
        {
            if (!MeleeNative.HasScope || !IsOwner) return;
            var set = SkillSet;
            bool want = set != null && (set.PrimaryAltSelected(body) || set.SpecialAltSelected(body));
            bool available = MeleeScopeBeam.Available(AssetsDir);
            scopeOn = MeleeNative.ssbm_sim_scope(sim, want && available ? 1u : 0u, 0f) == 1;
            if (want && !scopeOn && !scopeWarned)
            {
                scopeWarned = true;
                CommonLog.Warn("Luigi: the Super Scope is selected but not available (" + (available ? MeleeNative.Error : "ItCo.dat is not extracted yet: set Paths.Disc to your Melee disc and restart") + "); RT is the Fireball.");
            }
        }
        protected override void DrawHud()
        {
            if (!scopeOn || Arriving || MeleeNative.ssbm_sim_scope_state(sim, out var info) != 1) return;
            // The charge is announced by the disc's own cue; the box only shows the magazine.
            GUI.Box(new Rect(Screen.width / 2f - 130, Screen.height - 130, 260, 30), "Super Scope  " + info.ammo + "/" + info.capacity);
        }
        protected override void BeginDeathPresentation(Vector3 feet, Quaternion rotation)
            => MeleeDeathSequence.Begin(new MeleeDeath(AssetsDir, FighterKind, Math.Min(body.skinIndex, 4), feet, rotation, scale));
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale, state.move_id);
        protected override MeleeSkillSet SkillSet => LuigiSkills.Built;
        // The Cyclone's spin pulls enemies in from its start event to its end event.
        bool pulling;
        protected override void OnEventHit(MeleeNative.Event e)
        {
            if (e.move_id == 17) { LuigiCombat.Scope(body, e, scale, Effects); return; }
            if (e.move_id == 24) pulling = e.strength < 1.5f;
            LuigiCombat.Attack(this, body, e, scale);
        }
        protected override void BeforeStep(in MeleeNative.Pad pad)
        {
            UpdateScope();
            // Tell the sim which slot holds the Super Scope, so LB drives it instead of the
            // Super Jump Punch when the loadout swapped them (libssbm::luigi::HOST_SCOPE_ON_SPECIAL).
            MeleeNative.ssbm_sim_set_host_flags(sim, LuigiSkills.SpecialAltSelected(body) ? 1u : 0u);
            if (!pulling) return;
            if (state.move_id != 24) { pulling = false; return; }
            LuigiCombat.Pull(body);
        }
        protected override void FallbackSkills()
        {
            var extraIcon = GetComponent<ExtraSkillIcon>(); if (extraIcon) extraIcon.enabled = false;
            var commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock = commando ? commando.GetComponent<SkillLocator>() : null;
            if (stock && body.skillLocator) foreach (var slot in LuigiSkills.Slots) {
                var replacement = stock.GetSkill(slot); var skill = body.skillLocator.GetSkill(slot);
                if (skill && replacement && replacement.skillFamily && replacement.skillFamily.variants.Length > 0)
                    skill.SetSkillOverride(this, replacement.skillFamily.variants[0].skillDef, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
