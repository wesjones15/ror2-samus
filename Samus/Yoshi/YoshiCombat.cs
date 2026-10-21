using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Yoshi
{
    // Yoshi's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the tongue and egg, the thrown egg and the Yoshi Bomb's stars.
    internal static class YoshiCombat
    {
        // Host adaptations for an open world.
        const float TongueReach = 7f, TongueRadius = 0.8f;
        const float EggSpeed = 4.5f;                 // Melee units per tick of a thrown egg
        const float EggAngleBias = 22f;              // degrees added to the camera pitch
        const float BlastRadius = 4.5f, BlastCoefficient = 7f, BlastOutward = 3000f, BlastUpward = 1500f;

        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!NetworkServer.active) return;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            var camera = body.inputBank ? MeleeAim.CameraRay(body, body.inputBank).direction : aim;
            if (e.move_id == 20 && e.strength < 1.5f) Tongue(body, origin + Vector3.up * (body.radius + 0.4f), camera);
            else if (e.move_id == 23) ThrowEgg(body, origin, camera, scale);
            else if (e.move_id == 24) Bomb(body, origin, scale);
        }

        // Egg Lay: the tongue swallows the first enemy in its reach. There is no boss
        // immunity: Mithrix is laid in an egg like anything else.
        static void Tongue(CharacterBody body, Vector3 mouth, Vector3 aim)
        {
            var hits = Physics.SphereCastAll(mouth, TongueRadius, aim, TongueReach, WorldProbe.Mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (!hit.collider) continue;
                var hurt = hit.collider.GetComponent<HurtBox>();
                if (hurt && hurt.healthComponent) {
                    var target = hurt.healthComponent.body;
                    if (!target || target == body || !target.teamComponent || target.teamComponent.teamIndex == body.teamComponent.teamIndex) continue;
                    if (!hurt.healthComponent.alive || YoshiEgg.IsTrapped(target)) continue;
                    if (YoshiEgg.Lay(body, target)) body.GetComponent<YoshiDriver>()?.QueueConnect();
                    return;
                }
                // Something solid is in the way.
                if (!hit.collider.isTrigger && !hit.collider.GetComponentInParent<CharacterBody>()) return;
            }
        }

        // Egg Throw: the camera pitch sets the arc, as the reticle did, and the egg
        // follows it ballistically (the source's launch speed grows with the time the
        // reticle was up; here it is fixed and the angle is the control).
        static void ThrowEgg(CharacterBody body, Vector3 origin, Vector3 camera, float scale)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(camera.y, -1f, 1f)) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(pitch + EggAngleBias, 5f, 70f) * Mathf.Deg2Rad;
            var flat = new Vector3(camera.x, 0f, camera.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : body.transform.forward;
            Egg.Spawn(body, origin, flat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle), scale, EggSpeed);
        }

        // Yoshi Bomb: the landing's shockwave and the stars that run out from it.
        static void Bomb(CharacterBody body, Vector3 feet, float scale)
        {
            var center = feet + Vector3.up * 0.3f;
            var search = new SphereSearch { origin = center, radius = BlastRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var away = hurt.transform.position - center; away.y = 0f;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                NativeHit.Deal(body, hurt.healthComponent, new NativeHitSpec {
                    coefficient = BlastCoefficient, position = hurt.transform.position,
                    force = dir * BlastOutward + Vector3.up * BlastUpward, procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                });
            }
            const int stars = 8;
            for (int i = 0; i < stars; i++)
            {
                float turn = i * (360f / stars) * Mathf.Deg2Rad;
                YoshiStar.Spawn(body, center, new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)), scale);
            }
        }

        internal sealed class HitTracker
        {
            ulong serial=ulong.MaxValue;
            readonly Dictionary<(uint,uint),HashSet<HealthComponent>> victims=new Dictionary<(uint,uint),HashSet<HealthComponent>>();
            readonly Dictionary<(uint,uint,uint),Vector3> previous=new Dictionary<(uint,uint,uint),Vector3>();
            Collider[] candidates=new Collider[64];
            internal void Apply(CharacterBody owner,MeleeNative.HitboxSample[] samples,int count,float scale)
            {
                if(!NetworkServer.active)return;
                for(int i=0;i<count;i++) {
                    var hit=samples[i];
                    if(serial!=hit.serial){serial=hit.serial;victims.Clear();previous.Clear();}
                    var forward=new Vector3(hit.facing.x,0,hit.facing.z);
                    var rotation=forward.sqrMagnitude>0.0001f?Quaternion.LookRotation(forward):Quaternion.identity;
                    var local=new Vector3(-hit.local_position.x,hit.local_position.y,hit.local_position.z);
                    var point=new Vector3(hit.origin.x,hit.origin.y,hit.origin.z)*scale+rotation*local*scale;
                    var key=(hit.group,hit.generation,hit.id);
                    if(!previous.TryGetValue(key,out var from))from=point;
                    previous[key]=point;
                    int n;
                    while(true) {
                        n=Physics.OverlapCapsuleNonAlloc(from,point,hit.radius*scale,candidates,LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Collide);
                        if(n<candidates.Length||candidates.Length>=1024)break;
                        candidates=new Collider[candidates.Length*2];
                    }
                    var group=(hit.group,hit.generation);
                    if(!victims.TryGetValue(group,out var seen)){seen=new HashSet<HealthComponent>();victims[group]=seen;}
                    for(int j=0;j<n;j++) {
                        var hurt=candidates[j]?candidates[j].GetComponent<HurtBox>():null;
                        if(!hurt||!hurt.healthComponent||!hurt.healthComponent.alive||seen.Contains(hurt.healthComponent))continue;
                        var target=hurt.healthComponent.body;
                        if(!target||!target.teamComponent||target.teamComponent.teamIndex==owner.teamComponent.teamIndex)continue;
                        bool ground=target.characterMotor&&target.characterMotor.isGrounded;
                        if((hit.flags&(ground?1u:2u))==0)continue;
                        if(hit.element==8&&hit.damage==0)continue;
                        if(hit.damage<=0)continue;
                        float angle=hit.angle==361?45f:Mathf.Min(hit.angle,360u);
                        var direction=forward.normalized*Mathf.Cos(angle*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(angle*Mathf.Deg2Rad);
                        var type=hit.element==1?DamageType.IgniteOnHit:DamageType.Generic;
                        if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec {
                            coefficient=hit.damage/3f,position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f,
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default
                        })) { seen.Add(hurt.healthComponent); owner.GetComponent<MeleeDriver>()?.HitSpark(hit.element,hit.damage,hurt.collider.ClosestPoint(point)); }
                    }
                }
            }
        }
    }

    // An enemy laid in an egg (Egg Lay). EnemyHold keeps it helpless where it stands until
    // it breaks out: a few seconds for most, longer for the large and longest for bosses.
    // It can still be hit meanwhile. The egg is sized to the victim, but not to scale: a
    // large enemy's egg tops out near 2.4 m, a boss's is bigger than that and still well
    // short of the boss itself.
    internal sealed class YoshiEgg : MonoBehaviour
    {
        const float NormalSeconds = 4f, LargeSeconds = 6f, BossSeconds = 9f;
        const float LargeRadius = 1.6f;
        const float EggModelRadiusUnits = 9f;   // guess at the egg model's radius; unseen in game

        static readonly List<YoshiEgg> live = new List<YoshiEgg>();
        internal static bool IsTrapped(CharacterBody body)
        {
            foreach (var egg in live) if (egg && egg.victim == body) return true;
            return false;
        }

        // The egg's radius for a victim of this size and kind.
        internal static float EggRadius(CharacterBody victim)
        {
            float r = Mathf.Max(0.3f, victim.radius);
            if (MeleeGrapple.IsBoss(victim)) return Mathf.Clamp(r * 0.5f, 3f, 5.5f);
            return Mathf.Clamp(0.55f + r * 0.9f, 0.8f, 2.4f);
        }

        CharacterBody victim;
        float releaseAt, eggRadius;
        MeleeFighterView view;

        internal static bool Lay(CharacterBody owner, CharacterBody victim)
        {
            if (!NetworkServer.active || !owner || !victim || IsTrapped(victim)) return false;
            bool boss = MeleeGrapple.IsBoss(victim);
            float seconds = boss ? BossSeconds : victim.radius >= LargeRadius ? LargeSeconds : NormalSeconds;
            if (!EnemyHold.Hold(victim, seconds)) return false;
            var go = new GameObject("Yoshi egg (laid)");
            var egg = go.AddComponent<YoshiEgg>();
            egg.victim = victim;
            egg.eggRadius = EggRadius(victim);
            egg.releaseAt = Time.time + seconds;
            egg.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, 2, 0);
            egg.view.ObjectPose(0f);
            go.transform.position = victim.corePosition;
            return true;
        }

        void OnEnable() { live.Add(this); }
        void OnDestroy()
        {
            live.Remove(this);
            view?.Dispose(); view = null;
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            // The hold ends it: it ran out, or the victim died.
            if (!victim || !EnemyHold.IsHeld(victim)) { BreakOut(); return; }
            transform.position = victim.corePosition;
        }

        void BreakOut()
        {
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect && victim) EffectManager.SpawnEffect(effect, new EffectData { origin = victim.corePosition, scale = eggRadius }, true);
            } catch (Exception) { }
            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (view == null) return;
            // The egg sways more as it is about to hatch.
            float left = Mathf.Max(0f, releaseAt - Time.time);
            float shake = left < 1.5f ? Mathf.Sin(Time.time * 40f) * 0.06f * (1.5f - left) : 0f;
            var rotation = Quaternion.Euler(0f, 0f, shake * 57f);
            float units = eggRadius / EggModelRadiusUnits;
            view.FaceCamera(transform.position, rotation, units);
            view.Models.Update(units, d => (transform.position, rotation));
        }
    }
    // A star from the Yoshi Bomb's landing: it runs out along the ground and hits what it meets.
    internal sealed class YoshiStar : MonoBehaviour
    {
        const float Speed = 14f, Life = 0.9f, Radius = 0.7f, Coefficient = 2f, Force = 1800f;
        CharacterBody owner;
        Vector3 direction;
        float scale, age;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();
        static readonly Collider[] touching = new Collider[32];

        internal static void Spawn(CharacterBody owner, Vector3 at, Vector3 direction, float scale)
        {
            var go = new GameObject("Yoshi star");
            go.transform.position = at;
            var star = go.AddComponent<YoshiStar>();
            star.owner = owner; star.direction = direction.normalized; star.scale = scale;
            star.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, 1, 0);
            star.view.ObjectPose(0f);
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (age > Life || !owner) { Destroy(gameObject); return; }
            var from = transform.position;
            var next = from + direction * (Speed * dt);
            // Stay on the ground it runs along; a wall ends it.
            if (Physics.Raycast(next + Vector3.up * 0.6f, Vector3.down, out var floor, 2.5f, WorldProbe.Mask, QueryTriggerInteraction.Ignore))
                next.y = floor.point.y + 0.4f;
            if (Physics.Raycast(from, direction, out _, Speed * dt + Radius, WorldProbe.Mask, QueryTriggerInteraction.Ignore)) { Destroy(gameObject); return; }
            transform.position = next;
            int n = Physics.OverlapSphereNonAlloc(next, Radius, touching, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var hurt = touching[i] ? touching[i].GetComponent<HurtBox>() : null;
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var body = hurt.healthComponent.body;
                if (!body || !body.teamComponent || body.teamComponent.teamIndex == owner.teamComponent.teamIndex) continue;
                if (!victims.Add(hurt.healthComponent)) continue;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = Coefficient, position = hurt.transform.position,
                    force = direction * Force + Vector3.up * (Force * 0.4f), procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                });
            }
        }

        void LateUpdate()
        {
            if (view == null) return;
            var rotation = Quaternion.Euler(0f, age * 540f, 0f);
            view.FaceCamera(transform.position, rotation, scale);
            view.Models.Update(scale, d => (transform.position, rotation));
        }
        void OnDestroy() { view?.Dispose(); view = null; }
    }

    // Yoshi's thrown egg: spawns the native article, steps it along its ballistic arc, and
    // bursts on the first enemy or wall it meets, hitting everything near.
    internal sealed class Egg : MonoBehaviour
    {
        const float BurstRadius = 2f, BurstForce = 1500f;   // metres, newtons (host adaptation)
        CharacterBody owner;
        Vector3 origin;
        float modelScale;
        bool impacted;
        IntPtr projectile;
        MeleeNative.ProjectileState state;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();

        internal static void Spawn(CharacterBody owner, Vector3 point, Vector3 direction, float scale, float speed)
        {
            var go = new GameObject("Yoshi egg");
            go.transform.position = point;
            var shot = go.AddComponent<Egg>();
            shot.owner = owner;
            shot.origin = point;
            shot.modelScale = scale;
            // The launch speed travels in the charge argument (libssbm uses it for the egg).
            shot.projectile = MeleeNative.ssbm_projectile_new(NativeLib.CString(Samus.Plugin.AssetsDir), MeleeNative.FighterYoshi, 0, 0, speed, new MeleeNative.Vec3(direction.x, direction.y, direction.z));
            if (shot.projectile == IntPtr.Zero) { Destroy(go); return; }
            MeleeNative.ssbm_projectile_lifetime(shot.projectile, 0);
            shot.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, 0, 0);
            shot.ReadState();
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;
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
                if (step.sqrMagnitude > 0 && MeleeSweep.First(owner, from, Mathf.Max(state.radius * modelScale, 0.25f), step.normalized, step.magnitude, WorldProbe.Mask | LayerIndex.entityPrecise.mask, out var hit))
                {
                    var struck = hit.collider.GetComponent<HurtBox>();
                    if (!struck && hit.normal.y > 0.5f)
                    {
                        // Ground: bounce (Melee-style), bursting once the source's
                        // bounce budget is spent. Walls and ceilings burst it.
                        if (MeleeNative.ssbm_projectile_bounce(projectile) == 0) Impact(hit.point);
                        else
                        {
                            var nativePoint = new MeleeNative.Vec3(
                                (hit.point.x - origin.x) / modelScale,
                                (hit.point.y - origin.y) / modelScale,
                                (hit.point.z - origin.z) / modelScale);
                            MeleeNative.ssbm_projectile_place(projectile, nativePoint, 0);
                            transform.position = hit.point + Vector3.up * 0.02f;
                        }
                    }
                    else Impact(hit.point);
                }
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
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect) EffectManager.SpawnEffect(effect, new EffectData { origin = at, scale = BurstRadius }, true);
            } catch (Exception) { }
            var search = new SphereSearch { origin = at, radius = Mathf.Max(state.radius * modelScale, BurstRadius), mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !victims.Add(hurt.healthComponent)) continue;
                var away = hurt.transform.position - at;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = state.damage / 3f, position = hurt.transform.position, force = dir * BurstForce + Vector3.up * (BurstForce * 0.3f),
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
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
