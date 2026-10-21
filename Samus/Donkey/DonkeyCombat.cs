using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Donkey
{
    // Donkey Kong's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the Headbutt's burying and the Hand Slap's shockwaves and stunlock. No
    // enemy is exempt from any of it: bosses are buried and held like anything else.
    internal static class DonkeyCombat
    {
        // Host adaptations for an open world.
        // The specials hit far harder than the generic knockback formula gives; this scales it
        // per move so a Giant Punch, a Headbutt spike and the first hit of Spinning Kong send
        // an enemy flying.
        static float ForceScale(uint moveId) => moveId == 20 ? 10f : moveId == 21 ? 6f : moveId == 23 ? 4f : 1f;
        const float BurySeconds = 3f;         // how long a buried enemy is stuck
        const float SlapHoldSeconds = 0.5f;   // renewed by every slam, so the lock never lapses

        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!NetworkServer.active) return;
            // Giant Punch, Headbutt and Spinning Kong deal through the shared hitbox sampler.
            // Each Hand Slap slam sends a shockwave out along the ground.
            if (e.move_id == 24) HandSlapWave.Spawn(body, new Vector3(e.position.x, e.position.y, e.position.z) * scale);
        }

        internal sealed class HitTracker
        {
            ulong serial=ulong.MaxValue;
            readonly Dictionary<(uint,uint),HashSet<HealthComponent>> victims=new Dictionary<(uint,uint),HashSet<HealthComponent>>();
            readonly Dictionary<(uint,uint,uint),Vector3> previous=new Dictionary<(uint,uint,uint),Vector3>();
            Collider[] candidates=new Collider[64];
            internal void Apply(CharacterBody owner,MeleeNative.HitboxSample[] samples,int count,float scale,uint moveId)
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
                        // The Headbutt's ground hit (element 9) buries what it hits; in the air the same
                        // move spikes (its hit angle is straight down).
                        bool bury=hit.element==9&&ground;
                        float force=bury?0f:ForceScale(moveId);
                        if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec {
                            coefficient=hit.damage/3f,position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f*force,
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default
                        })) {
                            seen.Add(hurt.healthComponent);
                            if(bury)Bury(target);
                            // Every slam holds what it hits; the next one renews it.
                            if(moveId==24)EnemyHold.Hold(target,SlapHoldSeconds);
                        }
                    }
                }
            }

            static void Bury(CharacterBody target)
            {
                if(!EnemyHold.Hold(target,BurySeconds))return;
                try {
                    var effect=LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                    if(effect)EffectManager.SpawnEffect(effect,new EffectData{origin=target.footPosition,scale=Mathf.Max(1f,target.radius)},true);
                } catch(Exception){}
            }
        }
    }

    // A Hand Slap slam's shockwave: it runs out along the ground from DK's feet, hitting each
    // enemy it passes once and holding it (renewed every slam, so a slap held down keeps an
    // enemy, Mithrix included, locked for as long as it lasts).
    internal sealed class HandSlapWave : MonoBehaviour
    {
        const float Speed = 22f, MaxRadius = 9f, Band = 1.2f;
        const float Coefficient = 3.5f, Outward = 1200f, Upward = 600f, HoldSeconds = 0.5f;
        CharacterBody owner;
        Vector3 center;
        float radius;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();

        internal static void Spawn(CharacterBody owner, Vector3 feet)
        {
            var go = new GameObject("Donkey Kong shockwave");
            go.transform.position = feet;
            var wave = go.AddComponent<HandSlapWave>();
            wave.owner = owner;
            wave.center = feet;
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect) EffectManager.SpawnEffect(effect, new EffectData { origin = feet + Vector3.up * 0.2f, scale = 3f }, true);
            } catch (Exception) { }
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            if (!owner) { Destroy(gameObject); return; }
            radius += Speed * Time.fixedDeltaTime;
            if (radius > MaxRadius) { Destroy(gameObject); return; }
            var search = new SphereSearch { origin = center, radius = radius + Band, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive || victims.Contains(hurt.healthComponent)) continue;
                var body = hurt.healthComponent.body;
                if (!body) continue;
                // Through grounded enemies: on the ground, or hovering within reach of it.
                bool grounded = body.characterMotor ? body.characterMotor.isGrounded : true;
                if (!grounded && body.footPosition.y - center.y > 2.5f) continue;
                var flat = body.corePosition - center; flat.y = 0f;
                if (flat.magnitude > radius + Band) continue;
                victims.Add(hurt.healthComponent);
                var dir = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = Coefficient, position = hurt.transform.position,
                    force = dir * Outward + Vector3.up * Upward, procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                });
                EnemyHold.Hold(body, HoldSeconds);
            }
        }
    }
}
