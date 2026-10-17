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
        readonly SamusView[] views=new SamusView[4];
        readonly SamusView[] throwViews=new SamusView[4];readonly float[] attachment=new float[16];
        readonly Native.Vec3[] points=new Native.Vec3[64];
        readonly HashSet<(ulong,uint)> pummels=new HashSet<(ulong,uint)>();
        Native.Event? release;
        public bool Holding=>capture.Target;
        public SamusGrab(SamusDriver driver,CharacterBody owner){this.driver=driver;this.owner=owner;capture=new EnemyCapture(owner);}
        public bool TryCapture(HealthComponent target)
        {
            Native.ssbm_sim_state(driver.Simulation,out var state);
            if(state.move_id!=25&&state.move_id!=26)return false;
            if(!capture.TryCapture(target))return false;
            Native.ssbm_sim_set_grabbed(driver.Simulation,1);pummels.Clear();return true;
        }
        public void Throw(Native.Event cue){release=cue;}
        public void AfterStep(float dt)
        {
            if(release.HasValue){
                var cue=release.Value;release=null;var target=capture.Target;capture.Dispose();
                if(target&&target.alive){var direction=new Vector3(cue.direction.x,cue.direction.y,cue.direction.z);
                    NativeHit.Deal(owner,target,new NativeHitSpec {coefficient=cue.strength/3f,position=target.body.corePosition,force=direction*900f,procCoefficient=1f,damageType=DamageType.Generic,color=DamageColorIndex.Default});Feedback(target.body.corePosition,0);}
            }
            if(capture.Target){
                Native.ssbm_sim_state(driver.Simulation,out var state);
                if(Native.ssbm_sim_grabbed(driver.Simulation)==0){capture.Dispose();return;}
                Vector3? at=null;
                if(state.move_id>=29&&state.move_id<=32&&Native.ssbm_sim_bone(driver.Simulation,51,attachment)==1)at=new Vector3(attachment[12],attachment[13],attachment[14])*driver.Units;
                if(!capture.Step(new Vector3(state.facing.x,0,state.facing.z),dt,at))Native.ssbm_sim_set_grabbed(driver.Simulation,0);
            }
        }
        public bool Pummel(in Native.HitboxSample hit)
        {
            if(!capture.Target)return false;
            Native.ssbm_sim_state(driver.Simulation,out var state);if(state.move_id!=28)return false;
            if(pummels.Add((hit.serial,hit.generation))){var at=capture.Target.body.corePosition;
                NativeHit.Deal(owner,capture.Target,new NativeHitSpec {coefficient=hit.damage/3f,position=at,procCoefficient=1f,damageType=DamageType.Generic,color=DamageColorIndex.Default});Feedback(at,hit.element);}
            return true;
        }
        void Feedback(Vector3 at,uint element){uint effect=Native.ssbm_hit_feedback(driver.Simulation,element,2,1);if(effect!=0)driver.Effects.Spawn(effect,DiscParticles.At(at,Quaternion.identity,driver.Units));}
        public void Draw()
        {
            Native.ssbm_sim_state(driver.Simulation,out var state);
            for(int i=0;i<4;i++){
                if(state.move_id==29+i){
                    if(throwViews[i]==null)throwViews[i]=SamusView.Grapple((uint)i+4);
                    if(Native.ssbm_sim_bone(driver.Simulation,51,attachment)==1){
                        var matrix=new Matrix4x4();for(int n=0;n<16;n++)matrix[n]=attachment[n];matrix*=Matrix4x4.Scale(new Vector3(-1,1,1));
                        var f=matrix.GetColumn(2);var u=matrix.GetColumn(1);var rotation=Quaternion.LookRotation(new Vector3(f.x,f.y,f.z),new Vector3(u.x,u.y,u.z));
                        var position=new Vector3(attachment[12],attachment[13],attachment[14])*driver.Units;
                        throwViews[i].ObjectPose(Native.ssbm_sim_motion_frame(driver.Simulation));throwViews[i].FaceCamera(position,rotation,driver.Units);throwViews[i].Models.Update(driver.Units,d=>(position,rotation));
                    }
                }else throwViews[i]?.Models.HideAll();
            }
            uint count=Native.ssbm_sim_grapple_points(driver.Simulation,points,(uint)points.Length);
            var draws=new List<ViewDraw>[] {new List<ViewDraw>(),new List<ViewDraw>(),new List<ViewDraw>(),new List<ViewDraw>()};
            for(int i=0;i<count;i++){
                int part=i==count-1?3:i==0?0:i%3;
                var p=points[i];var next=points[Math.Min(i+1,(int)count-1)];
                var direction=new Vector3(next.x-p.x,next.y-p.y,next.z-p.z);var rotation=direction.sqrMagnitude>0.001f?Quaternion.LookRotation(direction):Quaternion.identity;
                draws[part].Add(new ViewDraw {key=(ulong)i+1,pos=new ViewVec3(p.x,p.y,p.z),rot_x=rotation.x,rot_y=rotation.y,rot_z=rotation.z,rot_w=rotation.w,alpha=1});
            }
            for(int part=0;part<4;part++){
                if(draws[part].Count>0&&views[part]==null)views[part]=SamusView.Grapple((uint)part);
                if(views[part]!=null){views[part].InstanceDraws=draws[part].ToArray();views[part].Models.Update(driver.Units);}
            }
        }
        public void Dispose(){capture.Dispose();foreach(var view in views)view?.Dispose();foreach(var view in throwViews)view?.Dispose();}
    }
}
