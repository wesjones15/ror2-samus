using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Samus
{
    // Disc-command hitboxes are posed on their original bones. The host converts
    // their geometry and maps Melee damage to the survivor's RoR2 damage stat.
    internal static class Combat
    {
        internal static void Attack(CharacterBody body,Native.Event e,float scale)
        {
            if(!NetworkServer.active)return;
            var aim=new Vector3(e.direction.x,e.direction.y,e.direction.z).normalized;
            var origin=new Vector3(e.position.x,e.position.y,e.position.z)*scale;
            switch(e.move_id) {
                case 20: Shot.Spawn(body,origin,aim,1,(uint)Mathf.Clamp(Mathf.RoundToInt(e.strength*7)+1,1,8),e.strength,scale);break;
                case 21: Shot.Spawn(body,origin,aim,2,0,0,scale);break;
                case 22: Shot.Spawn(body,origin,aim,2,1,0,scale);break;
                case 24: Shot.Spawn(body,origin,Vector3.zero,0,0,0,scale);break;
            }
        }
        internal sealed class HitTracker
        {
            ulong serial=ulong.MaxValue;
            readonly Dictionary<(uint,uint),HashSet<HealthComponent>> victims=new Dictionary<(uint,uint),HashSet<HealthComponent>>();
            readonly Dictionary<(uint,uint,uint),Vector3> previous=new Dictionary<(uint,uint,uint),Vector3>();
            Collider[] candidates=new Collider[64];
            internal void Apply(CharacterBody owner,Native.HitboxSample[] samples,int count,float scale)
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
                        float angle=hit.angle==361?45f:Mathf.Min(hit.angle,360u);
                        var direction=forward.normalized*Mathf.Cos(angle*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(angle*Mathf.Deg2Rad);
                        var type=hit.element==1?DamageType.IgniteOnHit:DamageType.Generic;
                        if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec {
                            coefficient=hit.damage/3f,position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f,
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default
                        })) {
                            seen.Add(hurt.healthComponent);
                            var driver=owner.GetComponent<SamusDriver>();
                            uint effect=Native.ssbm_hit_feedback(driver?driver.Simulation:System.IntPtr.Zero,hit.element,(hit.flags>>8)&31,(hit.flags>>16)&7);
                            if(effect!=0)driver?.Effects?.Spawn(effect,DiscParticles.At(hurt.collider.ClosestPoint(point),Quaternion.identity,scale));
                        }
                    }
                }
            }
        }
        internal static void Area(CharacterBody owner,Vector3 point,float radius,float coefficient,float proc,Vector3 direction,HashSet<HealthComponent> seen=null)
        {
            if(seen==null)seen=new HashSet<HealthComponent>();var search=new SphereSearch { origin=point,radius=radius,mask=LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach(var hurt in search.GetHurtBoxes()) {
                if(!hurt||!hurt.healthComponent||!seen.Add(hurt.healthComponent))continue;
                NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec { coefficient=coefficient,position=hurt.transform.position,
                    force=direction*500f,procCoefficient=proc,damageType=DamageType.Generic,color=DamageColorIndex.Default });
            }
        }
    }
    internal sealed class Shot:MonoBehaviour
    {
        CharacterBody owner;Vector3 origin,velocity;float modelScale;bool impacted;
        System.IntPtr projectile;Native.ProjectileState state;
        readonly HashSet<HealthComponent> explosionVictims=new HashSet<HealthComponent>();
        HealthComponent target;SamusDriver driver;SamusView view;DiscParticles particles;ulong particleKey;uint particleKind,particleState,particleFrame;bool fullCharge;readonly uint[] effectCues=new uint[32];
        internal static void Spawn(CharacterBody owner,Vector3 point,Vector3 direction,uint article,uint state,float charge,float scale)
        {
            var go=new GameObject("Samus disc article");go.transform.position=point;
            var shot=go.AddComponent<Shot>();shot.owner=owner;shot.origin=point;shot.modelScale=scale;shot.fullCharge=charge>=1;
            shot.driver=owner.GetComponent<SamusDriver>();shot.particles=shot.driver?.Particles;shot.particleKey=0x100000000ul|(uint)go.GetInstanceID();shot.particleKind=article;shot.particleState=state;
            try {
                shot.projectile=Native.ssbm_projectile_new(NativeLib.CString(Plugin.AssetsDir),article,state,charge,new Native.Vec3(direction.x,direction.y,direction.z));
                if(shot.projectile==System.IntPtr.Zero)throw new System.InvalidOperationException(Native.Error);
                shot.view=new SamusView(article,state);
                shot.ReadState();
            }
            catch(System.Exception e) { Plugin.Log.LogError("Samus article presentation failed: "+e.Message);Object.Destroy(go);return; }
            if(article==2&&state==0){var lockOn=new EnemyLock { Range=45f };shot.target=lockOn.Update(owner,true,false,Vector2.zero)?.healthComponent;}
            var transform=DiscParticles.At(point,direction.sqrMagnitude>0?Quaternion.LookRotation(direction):Quaternion.identity,scale);
            // efAlt_Spawn: normal/smash missile source generator graphs.
            shot.SourceEffects(uint.MaxValue,0,transform);
        }
        void FixedUpdate()
        {
            if(!owner||!owner.healthComponent||!owner.healthComponent.alive||!Run.instance){Destroy(gameObject);return;}
            if(projectile==System.IntPtr.Zero)return;
            var from=transform.position;
            var at=target&&target.alive?(target.transform.position-origin)/modelScale:Vector3.zero;
            Native.ssbm_projectile_step(projectile,Time.fixedDeltaTime,new Native.Vec3(at.x,at.y,at.z),target&&target.alive?1u:0u);
            ReadState();
            if(!impacted&&(state.flags&1)!=0)Impact(from);
            if(!impacted) {
                var next=origin+new Vector3(state.position.x,state.position.y,state.position.z)*modelScale;
                var step=next-from;
                if(step.sqrMagnitude>0&&Physics.SphereCast(from,state.radius*modelScale,step.normalized,out var hit,step.magnitude,WorldProbe.Mask|LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Ignore)) {
                    var hurt=hit.collider.GetComponent<HurtBox>();
                    bool friendly=hurt&&hurt.healthComponent&&hurt.healthComponent.body&&hurt.healthComponent.body.teamComponent.teamIndex==owner.teamComponent.teamIndex;
                    if(friendly)transform.position=next;
                    else if(particleKind==0&&!hurt) {
                        transform.position=hit.point+hit.normal*state.radius*modelScale;
                        var relative=(transform.position-origin)/modelScale;
                        Native.ssbm_projectile_place(projectile,new Native.Vec3(relative.x,relative.y,relative.z),1);
                    } else Impact(hit.point);
                } else transform.position=next;
            }
            if((state.flags&2)!=0){Destroy(gameObject);return;}
            if(impacted&&particleKind==0&&state.damage>0)Combat.Area(owner,transform.position,state.radius*modelScale,state.damage/3f,1f,Vector3.up,explosionVictims);
            var particleTransform=DiscParticles.At(transform.position,velocity.sqrMagnitude>0?Quaternion.LookRotation(velocity):Quaternion.identity,modelScale);
            particles?.Attach(particleKey,particleTransform);
            uint frame=(uint)state.frame;if(!impacted)SourceEffects(particleFrame,frame,particleTransform);particleFrame=frame;
        }
        void ReadState()
        {
            if(Native.ssbm_projectile_state(projectile,out state)!=1)throw new System.InvalidOperationException(Native.Error);
            velocity=new Vector3(state.velocity.x,state.velocity.y,state.velocity.z);
        }
        void Impact(Vector3 at)
        {
            if(impacted)return;impacted=true;transform.position=at;particles?.Stop(particleKey);
            var force=velocity.sqrMagnitude>0?velocity.normalized:Vector3.up;
            uint cue=Native.ssbm_projectile_impact(projectile,driver?driver.Simulation:System.IntPtr.Zero);ReadState();
            if(cue!=0)driver?.Effects?.Spawn(cue,DiscParticles.At(at,Quaternion.identity,modelScale));
            Combat.Area(owner,at,state.radius*modelScale,state.damage/3f,1f,force,explosionVictims);
            if((state.flags&2)==0){view.Dispose();view=new SamusView(particleKind,state.state);}
        }
        void LateUpdate(){if(view==null)return;view.ObjectPose(state.animation_frame);var rotation=velocity.sqrMagnitude>0.0001f?Quaternion.LookRotation(velocity):Quaternion.identity;view.Models.Update(modelScale,d=>(transform.position,rotation));}
        void SourceEffects(uint previous,uint frame,Matrix4x4 transform)
        {
            uint count=Native.ssbm_article_cues(particleKind,particleState,previous,frame,fullCharge?1u:0u,effectCues,(uint)effectCues.Length);
            for(int i=0;i<count&&i<effectCues.Length;i++){int target=Native.ssbm_effect_target(effectCues[i]);if(target>=0&&target<int.MaxValue)particles?.Spawn((uint)target,transform,particleKey);}
        }
        void OnDestroy(){particles?.Stop(particleKey);view?.Dispose();if(projectile!=System.IntPtr.Zero){Native.ssbm_projectile_free(projectile);projectile=System.IntPtr.Zero;}}
    }
}
