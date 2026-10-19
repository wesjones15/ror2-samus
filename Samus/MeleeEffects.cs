using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace Samus
{
    // Fighter-independent effect lifecycle. Source dispatch and posed attachment
    // matrices come from libssbm; this class owns Unity model instances only.
    internal sealed class MeleeEffects : IDisposable
    {
        sealed class Effect
        {
            public ulong key,serial;
            public uint bone;
            public bool attached,stop,chargeAttachment;
            public Matrix4x4 transform;
            public SamusView view;
            public float frame,duration;
            public uint cue,model,version;public int versions;
        }
        readonly DiscParticles particles;
        readonly float scale;
        readonly List<Effect> active=new List<Effect>();
        Effect charge;
        readonly float[] boneMatrix=new float[16];
        Native.Event[] cues=new Native.Event[32];
        ulong nextKey=1;
        public MeleeEffects(DiscParticles particles,float scale){this.particles=particles;this.scale=scale;}
        Matrix4x4 Bone(IntPtr sim,uint bone)
        {
            if(Native.ssbm_sim_bone(sim,bone,boneMatrix)!=1)throw new InvalidOperationException("Effect attachment: "+Native.Error);
            var matrix=new Matrix4x4();for(int i=0;i<16;i++)matrix[i]=boneMatrix[i];return matrix;
        }
        public void Spawn(uint id,Matrix4x4 transform,ulong serial=0,uint bone=0,bool stop=false,IntPtr sim=default)
        {
            int target=Native.ssbm_effect_target(id);
            if(target==int.MaxValue){Plugin.Log.LogError("Unresolved Melee effect "+id);return;}
            if(target==int.MinValue)return;
            if(target<int.MinValue+1000) {
                // RoR2 adaptation of the source Camera_RequestQuake cue.
                var point=transform.GetColumn(3);ShakeEmitter.CreateSimpleShakeEmitter(new Vector3(point.x,point.y,point.z)*scale,new Wave {amplitude=0.15f,frequency=12f,cycleOffset=0},0.15f,20f,true);return;
            }
            var effect=new Effect {key=nextKey++,serial=serial,bone=bone,stop=stop,transform=transform,attached=(Native.ssbm_effect_flags(id)&1)!=0&&sim!=IntPtr.Zero,chargeAttachment=id==1151};
            if(effect.attached)effect.transform=effect.chargeAttachment&&charge!=null?charge.transform:Bone(sim,bone);
            if(target>=0)particles.Spawn((uint)target,effect.transform,effect.key);
            else {
                effect.view=SamusView.Effect((uint)(-target-1));effect.duration=Native.ssbm_object_duration(effect.view.Handle);
                effect.cue=id;effect.model=(uint)(-target-1);
                if(Plugin.LogEffects.Value)Plugin.Log.LogInfo("Melee effect "+id+" -> model "+effect.model+", duration "+effect.duration+" frames");
            }
            if(effect.view!=null||effect.attached||effect.stop)active.Add(effect);
            if(active.Count>128){var old=active[0];old.view?.Dispose();particles.Stop(old.key);active.RemoveAt(0);}
        }
        public void Step(IntPtr sim,float dt,bool readCues=true)
        {
            if(Native.ssbm_sim_charge_visual(sim,boneMatrix)==1) {
                if(charge==null)charge=new Effect {view=new SamusView(1,0)};
                var matrix=new Matrix4x4();for(int n=0;n<16;n++)matrix[n]=boneMatrix[n];
                charge.transform=matrix;charge.frame+=dt*60;
            } else if(charge!=null){charge.view.Dispose();charge=null;}
            ulong serial=Native.ssbm_sim_motion_serial(sim);
            for(int i=active.Count-1;i>=0;i--) {
                var effect=active[i];effect.frame+=dt*60;
                if(effect.stop&&effect.serial!=serial || effect.view!=null&&effect.duration>0&&effect.frame>=effect.duration) {
                    LogEnd(effect);effect.view?.Dispose();particles.Stop(effect.key);active.RemoveAt(i);continue;
                }
                if(effect.attached){effect.transform=effect.chargeAttachment&&charge!=null?charge.transform:Bone(sim,effect.bone);particles.Attach(effect.key,effect.transform);}
            }
            if(!readCues)return;
            uint count=Native.ssbm_sim_gfx(sim,null,0);if(count>1024)throw new InvalidOperationException("Native GFX cue budget exceeded");
            if(count>cues.Length)cues=new Native.Event[count];
            if(count>0&&Native.ssbm_sim_gfx(sim,cues,(uint)cues.Length)!=count)throw new InvalidOperationException(Native.Error);
            for(int i=0;i<count;i++) {
                var cue=cues[i];var facing=new Vector3(cue.direction.x,0,cue.direction.z);
                var rotation=facing.sqrMagnitude>0?Quaternion.LookRotation(facing):Quaternion.identity;
                var transform=Matrix4x4.TRS(new Vector3(cue.position.x,cue.position.y,cue.position.z),rotation,new Vector3(-1,1,1));
                // efasync 0x42B scales the original KO model by common x4F4.
                if(cue.kind==0x42b)transform*=Matrix4x4.Scale(Vector3.one*cue.strength);
                Spawn(cue.kind,transform,serial,cue.move_id&65535,(cue.move_id&(1<<17))!=0,sim);
            }
        }
        public void Draw(Matrix4x4? placement=null)
        {
            foreach(var effect in Drawing()) {
                if(effect.view==null)continue;
                effect.view.ObjectPose(effect.frame);
                if(Plugin.LogEffects.Value){uint version=effect.view.TextureVersion(0);if(version!=effect.version){effect.version=version;effect.versions++;}}
                var transform=effect.transform*Matrix4x4.Scale(new Vector3(-1,1,1));
                var p=transform.GetColumn(3);var f=transform.GetColumn(2);var u=transform.GetColumn(1);
                var position=new Vector3(p.x,p.y,p.z)*scale;
                var rotation=Quaternion.LookRotation(new Vector3(f.x,f.y,f.z),new Vector3(u.x,u.y,u.z));
                if(placement.HasValue){position=placement.Value.MultiplyPoint3x4(position);rotation=placement.Value.rotation*rotation;}
                float size=transform.GetColumn(0).magnitude*scale;
                effect.view.FaceCamera(position,rotation,size);
                effect.view.Models.Update(size,d=>(position,rotation));
            }
        }
        static void LogEnd(Effect effect)
        {
            if(effect.view!=null&&Plugin.LogEffects.Value)
                Plugin.Log.LogInfo("Melee effect "+effect.cue+" (model "+effect.model+") ended at frame "+effect.frame.ToString("F1")+" of "+effect.duration+"; first texture changed "+Math.Max(0,effect.versions-1)+" times while drawn");
        }
        IEnumerable<Effect> Drawing(){foreach(var effect in active)yield return effect;if(charge!=null)yield return charge;}
        public void Dispose(){foreach(var effect in active){effect.view?.Dispose();particles.Stop(effect.key);}active.Clear();charge?.view.Dispose();charge=null;}
    }
}
