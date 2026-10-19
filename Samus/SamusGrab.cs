using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
namespace Samus
{
    internal sealed class SamusGrab:IDisposable
    {
        readonly SamusDriver driver;readonly CharacterBody owner;readonly EnemyCapture capture;
        readonly MeleeGrapple boss;
        Vector3 beamStart,beamEnd;bool beamVisible,contactSpent;
        public bool Attached=>boss.Attached;
        public void Detach(){boss.Release();beamVisible=false;}
        public void ReleaseCapture(){capture.Dispose();MeleeNative.ssbm_sim_set_grabbed(driver.Simulation,0);}
        readonly MeleeFighterView[] views=new MeleeFighterView[4];
        readonly MeleeFighterView[] throwViews=new MeleeFighterView[4];readonly float[] attachment=new float[16];
        readonly MeleeNative.Vec3[] points=new MeleeNative.Vec3[64];
        readonly HashSet<(ulong,uint)> pummels=new HashSet<(ulong,uint)>();
        MeleeNative.Event? release;
        public bool Holding=>capture.Target;
        public SamusGrab(SamusDriver driver,CharacterBody owner){this.driver=driver;this.owner=owner;capture=new EnemyCapture(owner);boss=new MeleeGrapple(owner);}
        public bool TryCapture(HealthComponent target)
        {
            MeleeNative.ssbm_sim_state(driver.Simulation,out var state);
            if(state.move_id!=25&&state.move_id!=26&&state.move_id!=27)return false;
            if(MeleeGrapple.IsBoss(target.body))return false;
            if(!capture.TryCapture(target))return false;
            MeleeNative.ssbm_sim_set_grabbed(driver.Simulation,1);pummels.Clear();return true;
        }
        public void Throw(MeleeNative.Event cue){if(!boss.Attached)release=cue;}
        public void AfterStep(float dt)
        {
            MeleeNative.ssbm_sim_state(driver.Simulation,out var current);
            bool catching=current.move_id>=25&&current.move_id<=27;
            if(!catching&&!boss.Attached)contactSpent=false;
            beamVisible=false;
            if(boss.Attached){
                if(boss.Step(driver.GrabHeld,driver.ControlsInterrupted,dt,out var desired)){
                    driver.AnchorGrapple(desired);beamStart=Cannon();beamEnd=boss.Anchor;beamVisible=true;return;
                }
            }
            uint nativeCount=MeleeNative.ssbm_sim_grapple_points(driver.Simulation,points,(uint)points.Length);
            if(catching&&nativeCount>0&&!capture.Target&&!contactSpent){
                float frame=MeleeNative.ssbm_sim_motion_frame(driver.Simulation),start=current.move_id==27?8f:17f,end=current.move_id==27?28f:41f;
                float length=MeleeGrapple.Range*Mathf.Clamp01((frame-start)/6f)*Mathf.Clamp01((end-frame)/6f);
                beamStart=Cannon();var aim=MeleeAim.CameraRay(owner,owner.inputBank).direction.normalized;beamEnd=beamStart+aim*length;beamVisible=length>0.01f;
                if(length>0&&MeleeGrapple.Trace(owner,beamStart,aim,length,out var contact,out var hurt)){
                    beamEnd=contact.point;
                    if(hurt&&hurt.healthComponent){contactSpent=true;if(MeleeGrapple.IsBoss(hurt.healthComponent.body)){boss.Attach(hurt.healthComponent,contact.point);}else TryCapture(hurt.healthComponent);}
                }
            }
            if(release.HasValue){
                var cue=release.Value;release=null;var target=capture.Target;capture.Dispose();
                if(target&&target.alive){var direction=new Vector3(cue.direction.x,cue.direction.y,cue.direction.z);
                    NativeHit.Deal(owner,target,new NativeHitSpec {coefficient=cue.strength/3f,position=target.body.corePosition,force=direction*900f,procCoefficient=1f,damageType=DamageType.Generic,color=DamageColorIndex.Default});Feedback(target.body.corePosition,0);}
            }
            if(capture.Target){
                MeleeNative.ssbm_sim_state(driver.Simulation,out var state);
                if(MeleeNative.ssbm_sim_grabbed(driver.Simulation)==0){capture.Dispose();return;}
                Vector3? at=null;
                if(state.move_id>=29&&state.move_id<=32&&MeleeNative.ssbm_sim_bone(driver.Simulation,51,attachment)==1)at=new Vector3(attachment[12],attachment[13],attachment[14])*driver.Units;
                if(!capture.Step(new Vector3(state.facing.x,0,state.facing.z),dt,at))MeleeNative.ssbm_sim_set_grabbed(driver.Simulation,0);
            }
        }
        Vector3 Cannon(){if(MeleeNative.ssbm_sim_bone(driver.Simulation,50,attachment)==1)return new Vector3(attachment[12],attachment[13],attachment[14])*driver.Units;return owner.corePosition;}
        public bool Pummel(in MeleeNative.HitboxSample hit)
        {
            if(!capture.Target)return false;
            MeleeNative.ssbm_sim_state(driver.Simulation,out var state);if(state.move_id!=28)return false;
            if(pummels.Add((hit.serial,hit.generation))){var at=capture.Target.body.corePosition;
                NativeHit.Deal(owner,capture.Target,new NativeHitSpec {coefficient=hit.damage/3f,position=at,procCoefficient=1f,damageType=DamageType.Generic,color=DamageColorIndex.Default});Feedback(at,hit.element);}
            return true;
        }
        void Feedback(Vector3 at,uint element){uint effect=MeleeNative.ssbm_hit_feedback(driver.Simulation,element,2,1);if(effect!=0)driver.Effects.Spawn(effect,MeleeParticles.At(at,Quaternion.identity,driver.Units));}
        public void Draw()
        {
            MeleeNative.ssbm_sim_state(driver.Simulation,out var state);
            for(int i=0;i<4;i++){
                if(state.move_id==29+i){
                    if(throwViews[i]==null)throwViews[i]=MeleeFighterView.Grapple(Plugin.AssetsDir,(uint)i+4);
                    if(MeleeNative.ssbm_sim_bone(driver.Simulation,51,attachment)==1){
                        var matrix=new Matrix4x4();for(int n=0;n<16;n++)matrix[n]=attachment[n];matrix*=Matrix4x4.Scale(new Vector3(-1,1,1));
                        var f=matrix.GetColumn(2);var u=matrix.GetColumn(1);var rotation=Quaternion.LookRotation(new Vector3(f.x,f.y,f.z),new Vector3(u.x,u.y,u.z));
                        var position=new Vector3(attachment[12],attachment[13],attachment[14])*driver.Units;
                        throwViews[i].ObjectPose(MeleeNative.ssbm_sim_motion_frame(driver.Simulation));throwViews[i].FaceCamera(position,rotation,driver.Units);throwViews[i].Models.Update(driver.Units,d=>(position,rotation));
                    }
                }else throwViews[i]?.Models.HideAll();
            }
            int count=beamVisible?Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(beamStart,beamEnd)/0.15f)+1,2,256):0;
            var draws=new List<ViewDraw>[] {new List<ViewDraw>(),new List<ViewDraw>(),new List<ViewDraw>(),new List<ViewDraw>()};
            for(int i=0;i<count;i++){
                int part=i==count-1?3:i==0?0:i%3;
                var p=Vector3.Lerp(beamStart,beamEnd,(float)i/(count-1));var direction=beamEnd-beamStart;var rotation=direction.sqrMagnitude>0.001f?Quaternion.LookRotation(direction):Quaternion.identity;
                draws[part].Add(new ViewDraw {key=(ulong)i+1,pos=new ViewVec3(p.x/driver.Units,p.y/driver.Units,p.z/driver.Units),rot_x=rotation.x,rot_y=rotation.y,rot_z=rotation.z,rot_w=rotation.w,alpha=1});
            }
            for(int part=0;part<4;part++){
                if(draws[part].Count>0&&views[part]==null)views[part]=MeleeFighterView.Grapple(Plugin.AssetsDir,(uint)part);
                if(views[part]!=null){views[part].InstanceDraws=draws[part].ToArray();views[part].Models.Update(driver.Units);}
            }
        }
        public void Dispose(){boss.Release();capture.Dispose();foreach(var view in views)view?.Dispose();foreach(var view in throwViews)view?.Dispose();}
    }
}

