using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Luigi
{
    // Luigi's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the Fireball projectile (the shared bouncing shot).
    internal static class LuigiCombat
    {
        internal static void Attack(LuigiDriver driver, CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            if (e.move_id == 20 && body.inputBank)
                aim = MeleeAim.ProjectileDirection(body, origin, MeleeAim.CameraRay(body, body.inputBank));
            if (e.move_id == 20)
            {
                // The fireball leaves the hand, not the feet. Spawned at the feet it starts inside
                // the ground while Luigi is standing on it, and its first sweep ends it there, so
                // only the airborne shots were ever seen.
                var muzzle = origin + Vector3.up * 0.9f + aim * 0.4f;
                MeleeBouncingShot.Spawn(body, muzzle, aim, new MeleeBouncingShot.Config
                {
                    AssetsDir = Samus.Plugin.AssetsDir,
                    Fighter = MeleeNative.FighterLuigi,
                    Article = 0, State = 0,
                    ViewArticle = 0, ViewState = 0,
                    Scale = scale,
                    Particles = driver.Particles,
                    // The fireball is a model (article 0), not a particle trail.
                    TrailParticle = 0, BouncePuff = 0,
                    ImpactPuff = 1036,   // efasync.c fireball impact
                    DespawnPuff = 1001,  // fireball despawn puff
                    BounceSound = 0,
                });
            }
        }

        /// The Super Scope's shot: a beam of the charge level in `e.strength`, from the right hand along the crosshair.
        internal static void Scope(CharacterBody body, MeleeNative.Event e, float scale, MeleeEffects effects)
        {
            if (!MeleeNet.Acts(body) || !MeleeScopeBeam.Available(Samus.Plugin.AssetsDir)) return;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            // A gun fires along the crosshair ray, not from the hand toward the point the crosshair
            // happens to hit: converging from a low muzzle to a floor point bends the shot into the
            // ground at the player's feet. Keep the camera's forward direction; the sweep handles walls.
            if (body.inputBank)
                aim = MeleeAim.CameraRay(body, body.inputBank).direction.normalized;
            var muzzle = origin + aim * 0.6f;
            if (effects != null && aim.sqrMagnitude > 0.0001f)
            {
                var rotation = Quaternion.LookRotation(aim);
                // The drawable normal scope smoke (0x405) plus the white 0x42F burst at the muzzle.
                // 0x430's generator 0x140 child trail generators are still not drawn by our renderer.
                effects.Spawn(0x42Fu, MeleeParticles.At(muzzle, rotation, scale) * Matrix4x4.Scale(Vector3.one * 2.5f));
                effects.Spawn(0x405u, MeleeParticles.At(muzzle, rotation, scale));
            }
            MeleeScopeBeam.Spawn(body, muzzle, aim, (uint)Mathf.Clamp(Mathf.RoundToInt(e.strength), 0, 9),
                new MeleeScopeBeam.Config { AssetsDir = Samus.Plugin.AssetsDir, Scale = scale, RangeScale = Samus.Plugin.ScopeRange.Value });
        }

        // Host adaptations for an open world. The specials hit far harder than the generic knockback
        // formula gives; this scales it per move so a Green Missile, a sweetspotted Super Jump Punch
        // and the Cyclone's last hit send an enemy flying, while the Cyclone's earlier hits keep it
        // where it is being pulled.
        static float ForceScale(uint moveId, float damage, uint element) =>
            moveId == 21 ? 4f : moveId == 23 ? (element == 4 ? 1f : 6f) : moveId == 24 ? (damage >= 11f ? 9f : 2f) : 1f;

        /// Host adaptation: the Cyclone's disc hits are contact damage; here each one also has to
        /// read as a real hit, so they land harder.
        static float DamageScale(uint moveId) => moveId == 24 ? 1.6f : 1f;

        // The Cyclone pulls everything near it toward Luigi while it spins (the source's hits keep
        // enemies close; here they are drawn in), bosses included.
        const float PullRadius = 7f, PullSpeed = 14f, PullStop = 1.5f;
        static readonly System.Collections.Generic.List<HurtBox> pulled = new System.Collections.Generic.List<HurtBox>();
        internal static void Pull(CharacterBody owner)
        {
            if (!MeleeNet.Acts(owner) || !owner.teamComponent) return;
            var centre = owner.corePosition;
            var search = new SphereSearch { origin = centre, radius = PullRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var target = hurt.healthComponent.body;
                if (!target || target.isPlayerControlled) continue;
                var to = centre - target.corePosition; to.y = 0f;
                if (to.magnitude <= PullStop) continue;
                var dir = to.normalized;
                // The enemy is the server's to move: a client Luigi asks for the pull.
                if (!NetworkServer.active) { MeleeNet.SendPush(target, dir.x * PullSpeed, dir.z * PullSpeed); continue; }
                if (target.characterMotor)
                {
                    var v = target.characterMotor.velocity;
                    target.characterMotor.velocity = new Vector3(dir.x * PullSpeed, v.y, dir.z * PullSpeed);
                }
                else
                {
                    var rb = target.GetComponent<Rigidbody>();
                    if (rb && !rb.isKinematic) rb.velocity = new Vector3(dir.x * PullSpeed, rb.velocity.y, dir.z * PullSpeed);
                }
            }
        }

        // A coin hit (element 4: the Super Jump Punch's weak hit) pays out, scaled to the stage.
        static void Coins(CharacterBody owner)
        {
            if (!owner || !owner.master || !Run.instance) return;
            owner.master.GiveMoney((uint)Mathf.Max(1, Run.instance.GetDifficultyScaledCost(2)));
        }

        internal sealed class HitTracker
        {
            ulong serial=ulong.MaxValue;
            readonly Dictionary<(uint,uint),HashSet<HealthComponent>> victims=new Dictionary<(uint,uint),HashSet<HealthComponent>>();
            readonly Dictionary<(uint,uint,uint),Vector3> previous=new Dictionary<(uint,uint,uint),Vector3>();
            Collider[] candidates=new Collider[64];
            internal void Apply(CharacterBody owner,MeleeNative.HitboxSample[] samples,int count,float scale,uint moveId)
            {
                if (!MeleeNet.Acts(owner)) return;
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
                            coefficient=hit.damage/3f*DamageScale(moveId),position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f*ForceScale(moveId,hit.damage,hit.element),
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default
                        })) {
                            seen.Add(hurt.healthComponent);                            owner.GetComponent<MeleeDriver>()?.HitSpark(hit.element,hit.damage,hurt.collider.ClosestPoint(point));
                            if(hit.element==4)Coins(owner);
                        }
                    }
                }
            }
        }
    }
}
