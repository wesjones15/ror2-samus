using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Link
{
    // Link's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the Hero's Bow arrow, Boomerang and Bomb.
    internal static class LinkCombat
    {
        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!NetworkServer.active) return;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            if (e.move_id == 20 || e.move_id == 21 || e.move_id == 24) {
                if (body.inputBank)
                    aim = MeleeAim.ProjectileDirection(body, origin, MeleeAim.CameraRay(body, body.inputBank));
                uint kind = e.move_id == 20 ? LinkShot.Arrow : e.move_id == 21 ? LinkShot.Boomerang : LinkShot.Bomb;
                // Link carries one bomb at a time; a second pull has nothing to do.
                if (kind == LinkShot.Bomb && LinkShot.CarriedBomb(body) != null) return;
                LinkShot.Spawn(body, origin, aim, scale, kind, e.strength);
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
                        })) seen.Add(hurt.healthComponent);
                    }
                }
            }
        }
    }

    // One article of Link's: the arrow, the boomerang or the bomb.
    //  - The arrow flies straight at the crosshair; its speed and damage come from the
    //    bow's charge (libssbm). It bursts on the first thing it meets.
    //  - The boomerang decelerates on its way out, curves back to Link and is caught; it
    //    hits everything it passes on both legs and bounces off walls (libssbm steps it,
    //    this carries it through the world). Link cannot throw another until it is back.
    //  - The bomb is carried in his hand. LT throws it, B drops it, and it explodes on
    //    touching an enemy or when its fuse runs out; the blast launches Link as well as
    //    his enemies (bomb jumping).
    internal sealed class LinkShot : MonoBehaviour
    {
        internal const uint Arrow = 0, Boomerang = 1, Bomb = 2;
        // Host flags the sim reads (libssbm link.rs HOST_*).
        const uint HostBoomerangOut = 1, HostBombHeld = 2;

        // Host adaptations for an open world; the source's values are screen-sized.
        const float RangeCap = 200f;          // metres an arrow may fly
        const float BoomerangForce = 900f;    // newtons along its heading
        const float BombFuse = 5f;            // seconds, held or loose (the item's own timer is not read)
        const float BombRadius = 0.35f;       // metres, the bomb's body
        const float BombBlastRadius = 3.5f;   // metres
        const float BombCoefficient = 4f;     // damage coefficient of the blast
        const float BombOutward = 3200f, BombUpward = 1400f;
        const float BombThrowSpeed = 15f;     // metres per second
        const float BombGravity = 0.7f, BombBounce = 0.55f;
        const float BombSelfSpeed = 20f;      // metres per second at the centre of a blast

        static readonly List<LinkShot> live = new List<LinkShot>();
        static readonly Collider[] touching = new Collider[32];

        internal static uint HostFlags(CharacterBody owner)
        {
            uint flags = 0;
            foreach (var shot in live) {
                if (!shot || shot.owner != owner) continue;
                if (shot.kind == Boomerang) flags |= HostBoomerangOut;
                else if (shot.kind == Bomb && shot.carried) flags |= HostBombHeld;
            }
            return flags;
        }
        internal static LinkShot CarriedBomb(CharacterBody owner)
        {
            foreach (var shot in live) if (shot && shot.owner == owner && shot.kind == Bomb && shot.carried) return shot;
            return null;
        }

        CharacterBody owner;
        LinkDriver driver;
        uint kind;
        Vector3 origin, spawn;
        float modelScale, age;
        bool impacted, wasReturning;
        IntPtr projectile;
        MeleeNative.ProjectileState state;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();
        // Bomb.
        bool carried;
        float fuse;
        Vector3 velocity;

        internal static void Spawn(CharacterBody owner, Vector3 point, Vector3 direction, float scale, uint kind, float charge)
        {
            var go = new GameObject(kind == Arrow ? "Link arrow" : kind == Boomerang ? "Link boomerang" : "Link bomb");
            go.transform.position = point;
            var shot = go.AddComponent<LinkShot>();
            shot.owner = owner;
            shot.driver = owner.GetComponent<LinkDriver>();
            shot.kind = kind;
            shot.origin = point;
            shot.spawn = point;
            shot.modelScale = scale;
            if (kind == Bomb) {
                // Held until thrown or dropped; libssbm only draws it (the item's model).
                shot.carried = true;
                shot.fuse = BombFuse;
                shot.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterLink, kind, 0);
                shot.view.ObjectPose(0f);
                return;
            }
            shot.projectile = MeleeNative.ssbm_projectile_new(NativeLib.CString(Samus.Plugin.AssetsDir), MeleeNative.FighterLink, kind, 0, charge, new MeleeNative.Vec3(direction.x, direction.y, direction.z));
            if (shot.projectile == IntPtr.Zero) { Destroy(go); return; }
            // The arrow flies to the range cap; the boomerang keeps its own flight time.
            if (kind == Arrow) MeleeNative.ssbm_projectile_lifetime(shot.projectile, 0);
            shot.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterLink, kind, 0);
            shot.ReadState();
        }

        void OnEnable() { live.Add(this); }

        internal void Throw(Vector3 aim)
        {
            if (!carried) return;
            carried = false;
            velocity = aim.normalized * BombThrowSpeed;
            if (owner && owner.characterMotor) velocity += owner.characterMotor.velocity * 0.5f;
        }
        internal void Drop()
        {
            if (!carried) return;
            carried = false;
            velocity = Vector3.zero;
            transform.position = owner.footPosition + Vector3.up * (BombRadius + 0.1f) + new Vector3(owner.transform.forward.x, 0f, owner.transform.forward.z) * 0.5f;
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            if (kind == Bomb) { StepBomb(); return; }
            if (projectile == IntPtr.Zero) return;
            var from = transform.position;
            var ownerRelative = (owner.corePosition - origin) / modelScale;
            MeleeNative.ssbm_projectile_step(projectile, Time.fixedDeltaTime, new MeleeNative.Vec3(ownerRelative.x, ownerRelative.y, ownerRelative.z), kind == Boomerang ? 1u : 0u);
            ReadState();
            if (kind == Boomerang) { StepBoomerang(from); return; }
            StepArrow(from);
        }

        // --- Arrow -------------------------------------------------------------
        void StepArrow(Vector3 from)
        {
            if (!impacted && (state.flags & 1) != 0) Impact(from);
            if (!impacted) {
                var next = origin + new Vector3(state.position.x, state.position.y, state.position.z) * modelScale;
                var step = next - from;
                if ((next - spawn).sqrMagnitude > RangeCap * RangeCap) { Destroy(gameObject); return; }
                if (step.sqrMagnitude > 0 && Sweep(from, state.radius * modelScale, step.normalized, step.magnitude, WorldProbe.Mask | LayerIndex.entityPrecise.mask, out var hit))
                    Impact(hit.point);
                else transform.position = next;
            }
            if ((state.flags & 2) != 0) Destroy(gameObject);
        }

        void Impact(Vector3 at)
        {
            if (impacted) return;
            impacted = true;
            transform.position = at;
            var heading = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z).normalized;
            MeleeNative.ssbm_projectile_impact(projectile, IntPtr.Zero);
            ReadState();
            var search = new SphereSearch { origin = at, radius = state.radius * modelScale, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !victims.Add(hurt.healthComponent)) continue;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = state.damage / 3f, position = hurt.transform.position, force = heading * 400f,
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
            }
        }

        // --- Boomerang ---------------------------------------------------------
        void StepBoomerang(Vector3 from)
        {
            bool returning = (state.flags & 4) != 0;
            // Everything it passed on the way out can be hit again on the way back.
            if (returning && !wasReturning) victims.Clear();
            wasReturning = returning;
            if ((state.flags & 2) != 0) { Destroy(gameObject); return; }
            float radius = Mathf.Max(state.radius * modelScale, 0.3f);
            var next = origin + new Vector3(state.position.x, state.position.y, state.position.z) * modelScale;
            var step = next - from;
            // Walls and floors turn it (lbVector_Mirror in the source) and it flies on.
            if (step.sqrMagnitude > 1e-6f && Sweep(from, radius, step.normalized, step.magnitude, WorldProbe.Mask, out var wall)) {
                MeleeNative.ssbm_projectile_reflect(projectile, new MeleeNative.Vec3(wall.normal.x, wall.normal.y, wall.normal.z));
                next = wall.point + wall.normal * radius;
                var relative = (next - origin) / modelScale;
                MeleeNative.ssbm_projectile_place(projectile, new MeleeNative.Vec3(relative.x, relative.y, relative.z), 0);
            }
            transform.position = next;
            HitAlong(from, next, radius);
        }

        void HitAlong(Vector3 from, Vector3 to, float radius)
        {
            var heading = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z).normalized;
            int n = Physics.OverlapCapsuleNonAlloc(from, to, radius, touching, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var hurt = touching[i] ? touching[i].GetComponent<HurtBox>() : null;
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var body = hurt.healthComponent.body;
                if (!body || !body.teamComponent || body.teamComponent.teamIndex == owner.teamComponent.teamIndex) continue;
                if (!victims.Add(hurt.healthComponent)) continue;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = state.damage / 3f, position = hurt.transform.position, force = heading * BoomerangForce,
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
            }
        }

        // --- Bomb --------------------------------------------------------------
        void StepBomb()
        {
            float dt = Time.fixedDeltaTime;
            age += dt;
            fuse -= dt;
            if (carried) {
                transform.position = driver ? driver.HandWorld() : owner.corePosition;
                if (fuse <= 0f) Explode();
                return;
            }
            velocity += Physics.gravity * (BombGravity * dt);
            var from = transform.position;
            var step = velocity * dt;
            if (step.sqrMagnitude > 1e-8f && Sweep(from, BombRadius, step.normalized, step.magnitude, WorldProbe.Mask, out var hit)) {
                velocity = Vector3.Reflect(velocity, hit.normal) * BombBounce;
                if (hit.normal.y > 0.7f) {
                    velocity.x *= 0.7f; velocity.z *= 0.7f;
                    if (velocity.magnitude < 1.5f) velocity = Vector3.zero;
                }
                transform.position = hit.point + hit.normal * BombRadius;
            } else transform.position = from + step;
            if (TouchesEnemy() || fuse <= 0f) Explode();
        }

        bool TouchesEnemy()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, BombRadius * 1.3f, touching, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var hurt = touching[i] ? touching[i].GetComponent<HurtBox>() : null;
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var body = hurt.healthComponent.body;
                if (body && body.teamComponent && body.teamComponent.teamIndex != owner.teamComponent.teamIndex) return true;
            }
            return false;
        }

        void Explode()
        {
            var center = transform.position;
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect) EffectManager.SpawnEffect(effect, new EffectData { origin = center, scale = BombBlastRadius }, true);
            } catch (Exception) { }
            var search = new SphereSearch { origin = center, radius = BombBlastRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var away = hurt.transform.position - center;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = BombCoefficient, position = hurt.transform.position,
                    force = new Vector3(dir.x, 0f, dir.z) * BombOutward + Vector3.up * BombUpward,
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
            }
            // Bomb jumping: the blast throws Link too, harder the closer he is.
            if (owner.characterMotor) {
                var toLink = owner.corePosition - center;
                float reach = BombBlastRadius + owner.radius;
                if (toLink.magnitude < reach) {
                    float falloff = Mathf.Clamp01(1f - toLink.magnitude / (reach * 1.2f));
                    var dir = toLink.sqrMagnitude > 0.01f ? toLink.normalized : Vector3.up;
                    dir.y = Mathf.Max(dir.y, 0.35f);
                    owner.characterMotor.ApplyForce(dir.normalized * owner.characterMotor.mass * BombSelfSpeed * falloff, true, false);
                }
            }
            Destroy(gameObject);
        }

        // --- Shared ------------------------------------------------------------
        // The nearest thing a swept sphere meets, ignoring Link and his allies.
        bool Sweep(Vector3 from, float radius, Vector3 direction, float distance, int mask, out RaycastHit best)
            => MeleeSweep.First(owner, from, radius, direction, distance, mask, out best);

        void ReadState() { if (MeleeNative.ssbm_projectile_state(projectile, out state) != 1) throw new InvalidOperationException(MeleeNative.Error); }

        void LateUpdate()
        {
            if (view == null) return;
            Quaternion rotation = Quaternion.identity;
            if (kind != Bomb) {
                view.ObjectPose(state.animation_frame);
                var heading = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z);
                rotation = heading.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(heading) : Quaternion.identity;
            } else view.ObjectPose(age * 60f);
            view.FaceCamera(transform.position, rotation, modelScale);
            view.Models.Update(modelScale, d => (transform.position, rotation));
        }

        void OnDestroy()
        {
            live.Remove(this);
            view?.Dispose(); view = null;
            if (projectile != IntPtr.Zero) { MeleeNative.ssbm_projectile_free(projectile); projectile = IntPtr.Zero; }
        }
    }
}
