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
            var origin=new Vector3(e.position.x,e.position.y,e.position.z)*scale+Vector3.up*1.2f;
            switch(e.move_id) {
                case 20: Shot.Spawn(body,origin,aim,2f+10f*e.strength,55f,0.15f+0.3f*e.strength,false,false,1,(uint)Mathf.Clamp(Mathf.RoundToInt(e.strength*7)+1,1,8),scale);break;
                case 21: Shot.Spawn(body,origin,aim,3f,24f,0.18f,true,false,2,0,scale);break;
                case 22: Shot.Spawn(body,origin,aim,6f,45f,0.22f,false,false,2,1,scale);break;
                case 24: Shot.Spawn(body,origin-Vector3.up*0.8f,Vector3.zero,4f,0,0.3f,false,true,0,0,scale);break;
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
                        }))seen.Add(hurt.healthComponent);
                    }
                }
            }
        }
        internal static void Area(CharacterBody owner,Vector3 point,float radius,float coefficient,float proc,Vector3 direction)
        {
            var seen=new HashSet<HealthComponent>();var search=new SphereSearch { origin=point,radius=radius,mask=LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach(var hurt in search.GetHurtBoxes()) {
                if(!hurt||!hurt.healthComponent||!seen.Add(hurt.healthComponent))continue;
                NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec { coefficient=coefficient,position=hurt.transform.position,
                    force=direction*500f,procCoefficient=proc,damageType=DamageType.Generic,color=DamageColorIndex.Default });
            }
            Shot.Flash(point,radius,new Color(1f,0.7f,0.1f,0.6f));
        }
    }
    internal sealed class Shot:MonoBehaviour
    {
        CharacterBody owner;Vector3 velocity;float coefficient,radius,age;bool homing,bomb;
        HealthComponent target;SamusView view;float modelScale;
        internal static void Spawn(CharacterBody owner,Vector3 point,Vector3 direction,float coefficient,float speed,float radius,bool homing,bool bomb,uint article,uint state,float scale)
        {
            var go=new GameObject("Samus disc article");go.transform.position=point;
            var shot=go.AddComponent<Shot>();shot.owner=owner;shot.velocity=direction*speed;shot.coefficient=coefficient;shot.radius=radius;shot.homing=homing;shot.bomb=bomb;
            shot.modelScale=scale;
            try { shot.view=new SamusView(article,state); }
            catch(System.Exception e) { Plugin.Log.LogError("Samus article presentation failed: "+e.Message);Object.Destroy(go);return; }
            if(homing){var lockOn=new EnemyLock { Range=45f };shot.target=lockOn.Update(owner,true,false,Vector2.zero)?.healthComponent;}
        }
        void FixedUpdate()
        {
            if(!owner||!owner.healthComponent||!owner.healthComponent.alive||!Run.instance){Destroy(gameObject);return;}
            age+=Time.fixedDeltaTime;
            if(bomb) {
                velocity+=Physics.gravity*Time.fixedDeltaTime;var delta=velocity*Time.fixedDeltaTime;
                if(Physics.SphereCast(transform.position,radius,delta.normalized,out var floor,delta.magnitude,WorldProbe.Mask,QueryTriggerInteraction.Ignore)){velocity=Vector3.zero;transform.position=floor.point+floor.normal*radius;}
                else transform.position+=delta;
                if(age>=1.1f){Combat.Area(owner,transform.position,4f,coefficient,1f,Vector3.up);Destroy(gameObject);}return;
            }
            if(homing&&target&&target.alive)velocity=Vector3.RotateTowards(velocity,(target.transform.position-transform.position).normalized*velocity.magnitude,Time.fixedDeltaTime*3f,0);
            var step=velocity*Time.fixedDeltaTime;
            if(step.sqrMagnitude>0&&Physics.SphereCast(transform.position,radius,step.normalized,out var hit,step.magnitude,WorldProbe.Mask|LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Ignore)) {
                var hurt=hit.collider.GetComponent<HurtBox>();
                if(hurt&&hurt.healthComponent&&hurt.healthComponent.body&&hurt.healthComponent.body.teamComponent.teamIndex==owner.teamComponent.teamIndex) {
                    transform.position+=step; // Friendlies do not consume a shot.
                } else {
                    Combat.Area(owner,hit.point,homing?1.8f:radius+0.6f,coefficient,1f,velocity.normalized);Destroy(gameObject);
                }
            } else transform.position+=step;
            if(age>=4f)Destroy(gameObject);
        }
        internal static void Flash(Vector3 at,float radius,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);Object.Destroy(go.GetComponent<Collider>());go.transform.position=at;go.transform.localScale=Vector3.one*radius*2;
            var fade=go.AddComponent<FlashFade>();fade.Material=Shaders.SeeThrough("Samus diagnostic effect",Texture2D.whiteTexture,true,false);
            fade.Material.SetColor("_TintColor",color);go.GetComponent<Renderer>().sharedMaterial=fade.Material;
        }
        void LateUpdate(){if(view==null)return;view.ObjectPose(age*60);var rotation=velocity.sqrMagnitude>0.0001f?Quaternion.LookRotation(velocity):Quaternion.identity;view.Models.Update(modelScale,d=>(transform.position,rotation));}
        void OnDestroy(){view?.Dispose();}
    }
    internal sealed class FlashFade:MonoBehaviour
    {
        internal Material Material;float age;
        void Update(){age+=Time.deltaTime;transform.localScale*=1f+Time.deltaTime*3;if(age>0.15f||!Run.instance)Destroy(gameObject);}
        void OnDestroy(){if(Material)Destroy(Material);}
    }
}
