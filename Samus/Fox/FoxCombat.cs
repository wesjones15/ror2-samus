using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Fox
{
    // Fox's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the Blaster laser projectile.
    internal static class FoxCombat
    {
        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            if (e.move_id == 20 && body.inputBank)
                aim = MeleeAim.ProjectileDirection(body, origin, MeleeAim.CameraRay(body, body.inputBank));
            if (e.move_id == 20) FoxShot.Spawn(body, origin, aim, scale);
            // Strength 1 is the shine starting (the burst, then the bubble stays up); 2 is it going down.
            else if (e.move_id == 24 && e.strength < 1.5f) Reflector.Burst(body, origin, scale);
            // Fire Fox's end event (strength 3) is the host's fiery impact burst.
            else if (e.move_id == 23 && e.strength >= 2.5f) FireFoxImpact(body, origin, scale);
        }

        // Fire Fox's landing/end burst (host adaptation): a fire AoE with falloff,
        // plus a much harder impact on the nearest enemy. The source ends Fire Fox
        // with a rebound/landing; this gives the end of the move a payoff without
        // replacing the disc launch hitboxes.
        internal static void FireFoxImpact(CharacterBody body, Vector3 origin, float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            const float RadiusUnits = 75f;        // 7.5 m
            const float InnerUnits = 22f;         // full AoE damage within 2.2 m
            const float ImpactPercent = 32f;      // nearest enemy only
            const float ClosePercent = 22f;       // AoE at the centre
            const float EdgePercent = 6f;         // AoE at the rim
            var center = body.corePosition;
            float radius = RadiusUnits * scale;
            float inner = InnerUnits * scale;
            var search = new SphereSearch { origin = center, radius = radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            HealthComponent impact = null;
            float nearest = float.MaxValue;
            var hits = new List<(HurtBox hurt, float distance)>();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                float distance = Vector3.Distance(hurt.transform.position, center);
                hits.Add((hurt, distance));
                if (distance < nearest) { nearest = distance; impact = hurt.healthComponent; }
            }
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect) EffectManager.SpawnEffect(effect, new EffectData { origin = center, scale = Mathf.Max(1f, radius) }, NetworkServer.active);
                body.GetComponent<MeleeDriver>()?.HitSpark(1u, 30f, center);
            } catch (Exception) { }
            foreach (var (hurt, distance) in hits)
            {
                bool direct = hurt.healthComponent == impact;
                float falloff = Mathf.InverseLerp(inner, radius, distance);
                float coefficient = Mathf.Lerp(ClosePercent, EdgePercent, falloff) / 3f;
                if (direct) coefficient += ImpactPercent / 3f;
                var away = hurt.transform.position - center;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                NativeHit.Deal(body, hurt.healthComponent, new NativeHitSpec {
                    coefficient = coefficient,
                    position = hurt.transform.position,
                    force = new Vector3(dir.x, 0f, dir.z) * (direct ? 2600f : 1300f) + Vector3.up * (direct ? 1400f : 700f),
                    procCoefficient = 1f, damageType = DamageType.IgniteOnHit, color = DamageColorIndex.Default,
                    staggerDuration = direct ? 0.5f : 0.25f, staggerBosses = direct,
                });
            }
        }

        // The Reflector (down-B): a frame-1 AoE that clears space around Fox with
        // damage and outward knockback. Melee's reflect bubble (ftfoxspeciallw.c::
        // ftColl_CreateReflectHit) is a projectile-reflection hitbox, which the
        // shared sampler does not pose; this adapts it to an open-space burst.
        internal static class Reflector
        {
            // Host adaptation: the burst clears a body-sized area (Melee units, so
            // 28 units is 2.8 m) and throws everything in it outward and up hard.
            internal const float RadiusUnits = 28f;
            const float OutwardForce = 4500f, UpwardForce = 2200f, Coefficient = 3.5f;
            internal static void Burst(CharacterBody body, Vector3 feet, float scale)
            {
                if (!MeleeNet.Acts(body)) return;
                // Centred on Fox's hips, not his feet.
                var center = feet + Vector3.up * (body.radius + 0.4f);
                float radius = RadiusUnits * scale;
                var search = new SphereSearch { origin = center, radius = radius, mask = LayerIndex.entityPrecise.mask };
                search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
                foreach (var hurt in search.GetHurtBoxes())
                {
                    if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                    var to = hurt.transform.position - center; to.y = 0f;
                    var dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
                    NativeHit.Deal(body, hurt.healthComponent, new NativeHitSpec {
                        coefficient = Coefficient, position = hurt.transform.position,
                        force = dir * OutwardForce + Vector3.up * UpwardForce, procCoefficient = 1f,
                        damageType = DamageType.Generic, color = DamageColorIndex.Default,
                        staggerDuration = 0.4f, staggerBosses = true,
                    });
                }
            }
        }
    }

    // A straight Blaster laser: spawns the native article, steps it, and deals
    // its authored damage on impact. No homing or charge scaling.
    internal sealed class FoxShot : MonoBehaviour
    {
        CharacterBody owner;
        Vector3 origin;
        float modelScale;
        bool impacted;
        IntPtr projectile;
        MeleeNative.ProjectileState state;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();

        internal static void Spawn(CharacterBody owner, Vector3 point, Vector3 direction, float scale)
        {
            var go = new GameObject("Fox blaster laser");
            go.transform.position = point;
            var shot = go.AddComponent<FoxShot>();
            shot.owner = owner;
            shot.origin = point;
            shot.modelScale = scale;
            shot.projectile = MeleeNative.ssbm_projectile_new(NativeLib.CString(Samus.Plugin.AssetsDir), MeleeNative.FighterFox, 0, 0, 0, new MeleeNative.Vec3(direction.x, direction.y, direction.z));
            if (shot.projectile == IntPtr.Zero) { Destroy(go); return; }
            MeleeNative.ssbm_projectile_lifetime(shot.projectile, 0); // host range, no offscreen cutoff
            shot.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, 0, 0);
            shot.ReadState();
        }

        void FixedUpdate()
        {
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            if (projectile == IntPtr.Zero) return;
            var from = transform.position;
            MeleeNative.ssbm_projectile_step(projectile, Time.fixedDeltaTime, default, 0);
            ReadState();
            if (!impacted && (state.flags & 1) != 0) Impact(from);
            if (!impacted)
            {
                var next = origin + new Vector3(state.position.x, state.position.y, state.position.z) * modelScale;
                var step = next - from;
                if (step.sqrMagnitude > 0 && Physics.SphereCast(from, state.radius * modelScale, step.normalized, out var hit, step.magnitude, WorldProbe.Mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
                    Impact(hit.point);
                else transform.position = next;
            }
            if ((state.flags & 2) != 0) { Destroy(gameObject); return; }
        }

        void Impact(Vector3 at)
        {
            if (impacted) return;
            impacted = true;
            transform.position = at;
            MeleeNative.ssbm_projectile_impact(projectile, IntPtr.Zero);
            ReadState();
            var search = new SphereSearch { origin = at, radius = state.radius * modelScale, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !victims.Add(hurt.healthComponent)) continue;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = state.damage / 3f, position = hurt.transform.position, force = Vector3.zero,
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
                owner.GetComponent<MeleeDriver>()?.HitSpark(state.element, state.damage, hurt.transform.position);
            }
        }

        void ReadState() { if (MeleeNative.ssbm_projectile_state(projectile, out state) != 1) throw new InvalidOperationException(MeleeNative.Error); }
        void LateUpdate()
        {
            if (view == null) return;
            view.ObjectPose(state.animation_frame);
            var velocity = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z);
            var rotation = velocity.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
            view.FaceCamera(transform.position, rotation, modelScale);
            view.Models.Update(modelScale, d => (transform.position, rotation));
        }
        void OnDestroy() { view?.Dispose(); view = null; if (projectile != IntPtr.Zero) { MeleeNative.ssbm_projectile_free(projectile); projectile = IntPtr.Zero; } }
    }
}
