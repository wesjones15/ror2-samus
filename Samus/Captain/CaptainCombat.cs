using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Captain
{
    // Captain Falcon's combat: disc hitboxes posed on their original bones,
    // mapped to RoR2 damage. Every special is a body move, so there is no
    // projectile to spawn here.
    internal static class CaptainCombat
    {
        // Falcon's specials hit far harder than the generic knockback formula gives (an
        // enemy shrugs off 660 N): these scale it per move. Host adaptation; the translation
        // keeps his attacks overpowered on purpose.
        static float ForceScale(uint moveId) => moveId == 20 ? 10f : moveId == 21 ? 5f : moveId == 23 ? 4f : moveId == 24 ? 6f : 1f;
        internal const float ThrowForce = 6500f;

        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            // Falcon Punch / Raptor Boost / Falcon Kick deal through the shared hitbox
            // sampler. The Dive's catch (strength 1) and its explosion throw (strength 2)
            // are the driver's.
        }

        internal sealed class HitTracker
        {
            readonly CaptainDriver driver;
            internal HitTracker(CaptainDriver driver) { this.driver = driver; }
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
                        // Raptor Boost's lunge carries inert trigger boxes: touching anything
                        // turns the lunge into the uppercut (ftCa_SpecialS_OnDetect).
                        if(hit.element==11&&hit.damage==0){ if(moveId==21)driver.QueueConnect(); continue; }
                        // The Dive's grab boxes take hold of what they touch, bosses too.
                        if(hit.element==8&&moveId==23&&hit.damage>0){ if(driver.Catch(hurt.healthComponent,point))seen.Add(hurt.healthComponent); continue; }
                        if(hit.element==8&&hit.damage==0)continue;
                        if(hit.damage<=0)continue;
                        float angle=hit.angle==361?45f:Mathf.Min(hit.angle,360u);
                        var direction=forward.normalized*Mathf.Cos(angle*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(angle*Mathf.Deg2Rad);
                        var type=hit.element==1?DamageType.IgniteOnHit:DamageType.Generic;
                        if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec {
                            coefficient=hit.damage/3f,position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f*ForceScale(moveId),
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default
                        })) { seen.Add(hurt.healthComponent); owner.GetComponent<MeleeDriver>()?.HitSpark(hit.element,hit.damage,hurt.collider.ClosestPoint(point)); }
                    }
                }
            }
        }
    }
}
