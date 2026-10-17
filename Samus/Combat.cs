using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Samus
{
    // Host damage coefficients and simplified projectiles. These are RoR2 balance,
    // not copied Melee hitbox/frame data. NativeHit supplies item procs and credit.
    internal static class Combat
    {
        internal static void Attack(CharacterBody body,Native.Event e,float scale)
        {
            if(!NetworkServer.active)return;
            var aim=new Vector3(e.direction.x,e.direction.y,e.direction.z).normalized;
            var origin=new Vector3(e.position.x,e.position.y,e.position.z)*scale+Vector3.up*1.2f;
            switch(e.move_id) {
                case 20: Shot.Spawn(body,origin,aim,2f+10f*e.strength,55f,0.15f+0.3f*e.strength,false,false);break;
                case 21: Shot.Spawn(body,origin,aim,3f,24f,0.18f,true,false);break;
                case 22: Shot.Spawn(body,origin,aim,6f,45f,0.22f,false,false);break;
                case 24: Shot.Spawn(body,origin-Vector3.up*0.8f,Vector3.zero,4f,0,0.3f,false,true);break;
                case 23: Area(body,origin,2.4f,4.5f,0.5f,aim);break;
                default:
                    bool smash=e.move_id>=10&&e.move_id<=12;
                    float damage=smash?6f*e.strength:e.move_id==1?1.5f:2.5f;
                    Area(body,origin+aim*(smash?1.4f:0.8f),smash?2.4f:1.8f,damage,1f,aim);break;
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
        HealthComponent target;Material material;
        internal static void Spawn(CharacterBody owner,Vector3 point,Vector3 direction,float coefficient,float speed,float radius,bool homing,bool bomb)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Samus energy projectile";
            Object.Destroy(go.GetComponent<Collider>());go.transform.position=point;go.transform.localScale=Vector3.one*radius*2f;
            var shot=go.AddComponent<Shot>();shot.owner=owner;shot.velocity=direction*speed;shot.coefficient=coefficient;shot.radius=radius;shot.homing=homing;shot.bomb=bomb;
            shot.material=new Material(Shaders.HgStandard());shot.material.SetColor("_Color",bomb?Color.red:homing?Color.yellow:Color.cyan);
            shot.material.SetColor("_EmColor",bomb?Color.red:Color.cyan);shot.material.SetFloat("_EmPower",2f);go.GetComponent<Renderer>().sharedMaterial=shot.material;
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
        void OnDestroy(){if(material)Destroy(material);}
    }
    internal sealed class FlashFade:MonoBehaviour
    {
        internal Material Material;float age;
        void Update(){age+=Time.deltaTime;transform.localScale*=1f+Time.deltaTime*3;if(age>0.15f||!Run.instance)Destroy(gameObject);}
        void OnDestroy(){if(Material)Destroy(Material);}
    }
}
