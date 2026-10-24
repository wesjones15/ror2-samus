using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using Yoshi;

namespace Pikachu
{
    // Pikachu's driver: the shared MeleeDriver owns sim stepping, collision, facing,
    // hitbox sampling and recovery; this supplies his presentation, skill layout and
    // host-side context for his staged specials (the Quick Attack lock-on).
    [DefaultExecutionOrder(-200)]
    public sealed class PikachuDriver : MeleeDriver, IEggLayer
    {
        const uint ThunderShock = 20, SkullBash = 21, QuickAttack = 23;
        /// libssbm::pikachu::HOST_EGG_LAY: the RT slot holds Yoshi's Egg Lay.
        const uint HostEggLay = 1;

        // Effects are shared assets, not part of a body: Pikachu's RT alternate borrows
        // Yoshi's Egg Lay tongue/egg presentation, so resolve every cue across all banks.
        static IMeleeObjectView EffectView(uint model) => MeleeFighterView.EffectAny(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, model);
        MeleeFighterView view, entryPlatform, shieldView; uint shieldModel;
        MeleeAudio audio;
        // Quick Attack is camera/stick-vector travel; damage every enemy its path crosses.
        Vector3 quickPrev;
        bool quickActive;
        readonly HashSet<HealthComponent> quickHits = new HashSet<HealthComponent>();
        internal MeleeParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        protected override MeleeEffects EffectsForSparks => Effects;
        internal IntPtr NativeSim => sim;
        readonly MeleeHitTracker hits = new MeleeHitTracker();
        // The borrowed Egg Lay's mouth: Pikachu's forward snout joint (16), the analogue
        // of Yoshi's jaw joint 42. It follows the pitched upper body.
        readonly float[] mouthBone = new float[16];
        bool connectQueued;

        protected override string FighterName => "Pikachu";
        protected override string AssetsDir => Samus.Plugin.AssetsDir;
        protected override uint FighterKind => MeleeNative.FighterPikachu;
        protected override float MetresPerUnit => Samus.Plugin.MetresPerUnit.Value;
        protected override float JumpScale => Samus.Plugin.JumpScale.Value;
        protected override float SpeedScale
            => Samus.Plugin.SpeedScale.Value
                * (YoshiFull.IsCarrying(body) || YoshiFull.IsLaying(body) ? 0.45f : 1f);
        protected override bool HoldCharge => false;
        protected override bool AssetsReady => Samus.Plugin.PikachuReady;
        protected override bool AssetsBusy => Samus.Plugin.AssetsBusy;
        protected override uint AllowedButtons => PikachuSkills.Allowed(body);
        protected override bool ShieldSelected => PikachuSkills.ShieldSelected(body);
        protected override MeleeControlLayout Layout => MeleeControlLayout.Samus;
        protected override bool IsRangedMove(uint moveId) => moveId == ThunderShock;
        protected override bool IsChargeMove(uint moveId) => false;
        /// Egg Lay pays its cooldown only when the tongue swallows something; a miss
        /// leaves the stock and recharge untouched (Yoshi's shared rule).
        protected override bool SpendsOnStart(uint moveId)
            => !(moveId == ThunderShock && PikachuSkills.EggLaySelected(body));
        /// While an egg lay carries a victim, RT cannot start another lash.
        protected override uint FilterButtons(uint buttons)
        {
            if (!YoshiFull.IsCarrying(body) && !YoshiFull.IsLaying(body)) return buttons;
            return buttons & ~(MeleeControls.NeutralB | MeleeControls.Sprint);
        }

        protected override void UpdateAudio() => audio?.Update();

        /// The borrowed Egg Lay draws Yoshi's tongue from Pikachu's mouth on move start;
        /// YoshiTongue catches continuously while it is out.
        protected override void OnEventStarted(MeleeNative.Event e)
        {
            if (e.move_id != ThunderShock || !PikachuSkills.EggLaySelected(body)) return;
            var mouth = Mouth();
            YoshiTongue.Show(body, YoshiCombat.CrosshairPoint(body, mouth));
        }

        internal Vector3 Mouth()
        {
            if (sim != IntPtr.Zero && MeleeNative.ssbm_sim_bone(sim, 16, mouthBone) == 1)
            {
                var point = new Vector3(mouthBone[12], mouthBone[13], mouthBone[14]) * scale;
                var forward = body ? body.transform.forward : Vector3.forward;
                return point + new Vector3(forward.x, 0f, forward.z).normalized * 0.12f;
            }
            return YoshiCombat.MouthFallback(body);
        }
        internal void QueueConnect() => connectQueued = true;
        internal void PlaySourceSound(uint sound)
        {
            if (sim == IntPtr.Zero) return;
            // The borrowed move's cues live in Yoshi's bank (no Yoshi voice: Egg Lay has none).
            if (MeleeNative.ssbm_sim_play_sound_from(sim, MeleeNative.FighterYoshi, sound) != 1)
                CommonLog.Warn("Pikachu borrowed sound " + sound + ": " + MeleeNative.Error);
        }
        internal void PlayEffect(uint id, Vector3 world, Quaternion rotation)
        {
            if (Effects == null || scale <= 0f) return;
            Present(() => Effects.Spawn(id, Matrix4x4.TRS(world / scale, rotation, new Vector3(-1, 1, 1))));
        }
        CharacterBody IEggLayer.Body => body;
        float IEggLayer.UnitScale => scale;
        MeleeSkillSet IEggLayer.SkillSet => PikachuSkills.Built;
        Vector3 IEggLayer.LayForward
        {
            get
            {
                var aim = body && body.inputBank
                    ? MeleeAim.CameraRay(body, body.inputBank).direction
                    : body ? body.transform.forward : Vector3.forward;
                aim = Vector3.ProjectOnPlane(aim, Vector3.up);
                // Eggs fan behind the aim, opposite the tongue's forward lash.
                return aim.sqrMagnitude > 0.0001f ? -aim.normalized : -body.transform.forward;
            }
        }
        Vector3 IEggLayer.Mouth() => Mouth();
        void IEggLayer.QueueConnect() => QueueConnect();
        void IEggLayer.PlaySourceSound(uint sound) => PlaySourceSound(sound);
        void IEggLayer.PlayEffect(uint id, Vector3 world, Quaternion rotation) => PlayEffect(id, world, rotation);
        protected override void CreatePresentation()
        {
            view = new MeleeFighterView(AssetsDir, MeleeNative.FighterPikachu, Math.Min(body.skinIndex, 4));
            audio = new MeleeAudio(sim);
            Particles = new MeleeParticles(AssetsDir, scale);
            // The source 0x4BD aura's child generator 7002 is a 27.5-unit additive sprite,
            // which reads as a hard blue square around the orb. Scale and soften it so it
            // reads as a glow; the small 7001/7003 sparks keep their size.
            Particles.Tune(7002, 0.45f, 0.60f, true);
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
            // The source 0x4BD aura's child generator 7002 is a 27.5-unit additive sprite,
            // which reads as a hard blue square around the orb. Scale and soften it so it
            // reads as a glow; the small 7001/7003 sparks keep their size.
            Particles.Tune(7002, 0.45f, 0.60f, true);
            Effects = new MeleeEffects(Particles, scale, EffectView, null, Samus.Plugin.LogEffects.Value);
        }
        protected override void ResetEntryPlatform()
        {
            entryPlatform?.Dispose(); entryPlatform = MeleeFighterView.EntryPlatform(AssetsDir);
        }
        protected override void StepPresentation(float dt)
        {
            if (state.move_id == QuickAttack)
            {
                if (!quickActive)
                {
                    quickActive = true;
                    quickHits.Clear();
                    quickPrev = body.footPosition;
                }
                QuickSweep(quickPrev, body.footPosition);
                quickPrev = body.footPosition;
            }
            else quickActive = false;
            Present(() => Effects?.Step(sim, dt));
            Present(() => Particles?.Step(dt));
        }
        readonly Collider[] quickContacts = new Collider[32];
        void QuickSweep(Vector3 from, Vector3 to)
        {
            var step = to - from;
            if (step.sqrMagnitude < 1e-5f) return;
            int n = Physics.OverlapCapsuleNonAlloc(from, to, 0.9f, quickContacts,
                LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            if (n == 0) return;
            var dir = step.normalized;
            for (int i = 0; i < n; i++)
            {
                var health = quickContacts[i] ? quickContacts[i].GetComponentInParent<HealthComponent>() : null;
                if (!health || !health.alive || !health.body || !health.body.teamComponent) continue;
                if (health.body.teamComponent.teamIndex == body.teamComponent.teamIndex) continue;
                if (!quickHits.Add(health)) continue;
                NativeHit.Deal(body, health, new NativeHitSpec
                {
                    coefficient = 3f, position = health.transform.position,
                    force = dir * 400f + Vector3.up * 120f, procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                    staggerDuration = Samus.Plugin.StaggerDuration.Value, staggerBosses = true,
                });
            }
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
        protected override void ApplyHitboxes(MeleeNative.HitboxSample[] samples, int count, float scale) => hits.Apply(body, samples, count, scale);

        protected override void BeforeStep(in MeleeNative.Pad p)
        {
            // The RT slot's loadout can hold Yoshi's Egg Lay; the sim needs to know before
            // it reads the pad, so DownB starts Egg Lay instead of Thunder Jolt.
            MeleeNative.ssbm_sim_set_host_flags(sim,
                PikachuSkills.EggLaySelected(body) ? HostEggLay : 0u);
            if (connectQueued) { connectQueued = false; MeleeNative.ssbm_sim_connect(sim); }
        }

        protected override MeleeSkillSet SkillSet => PikachuSkills.Built;
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
