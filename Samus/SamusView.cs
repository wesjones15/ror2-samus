using System;
using Ror2Common;
using RoR2;
using UnityEngine;

namespace Samus
{
    // Samus's character-select presenter: her up-B intro and live rise. The
    // shared MeleeSelectDisplay owns the camera pan; this supplies the source.
    internal sealed class SamusDisplay:IMeleeSelectSource,IDisplayEffects
    {
        static readonly MeleeNative.CollideFn collide=Floor;
        static IMeleeObjectView EffectView(uint model)=>MeleeFighterView.Effect(Plugin.AssetsDir,MeleeNative.FighterSamus,model);
        static IMeleeObjectView ChargeView()=>new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,1,0);
        MeleeFighterView view;
        uint costume;
        readonly IntPtr sim;
        readonly MeleeNative.Host host;
        bool requestIntro,introStarted,sawUpB;
        readonly bool idleOnly;
        MeleeParticles particles;
        MeleeEffects effects;
        readonly float[] anchorMatrix=new float[16];
        Vector3 drawOffset;
        static readonly Quaternion previewRotation=Quaternion.identity;
        public bool IntroFinished { get; private set; }
        public bool AudioActive => MeleeNative.ssbm_sim_audio_active(sim) == 1;
        public int AudioRate=>32000;
        public Vector3 DrawOffset {set{drawOffset=value;}}
        public Vector3 PoseAnchor {
            get {
                if(MeleeNative.ssbm_sim_state(sim,out var state)!=1 || MeleeNative.ssbm_sim_bone(sim,4,anchorMatrix)!=1)throw new InvalidOperationException(MeleeNative.Error);
                var facing=new Vector3(state.facing.x,0,state.facing.z);
                var posed=new Vector3(anchorMatrix[12]-state.position.x,anchorMatrix[13]-state.position.y,anchorMatrix[14]-state.position.z);
                if(facing.sqrMagnitude>0.001f)posed=Quaternion.Inverse(Quaternion.LookRotation(facing))*posed;
                return previewRotation*posed;
            }
        }
        public ModelView Models=>view.Models;
        public float Scale=>Plugin.MetresPerUnit.Value;
        public float RiseMetres {
            get {
                if(idleOnly || MeleeNative.ssbm_sim_state(sim,out var state)!=1)return 0f;
                return state.position.y*Scale;
            }
        }
        public static IDisplayActor Make()=>Plugin.AssetsReady?new MeleeSelectDisplay(new SamusDisplay()):null;
        public static IDisplayActor Portrait()=>Plugin.AssetsReady?new MeleeSelectDisplay(new SamusDisplay(idleOnly:true),intro:false):null;
        SamusDisplay(bool idleOnly=false)
        {
            this.idleOnly=idleOnly;
            sim=MeleeNative.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),default);
            if(sim==IntPtr.Zero)throw new InvalidOperationException(MeleeNative.Error);
            try { costume=Skins.Selected();view=new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,costume);if(!idleOnly){particles=new MeleeParticles(Plugin.AssetsDir,Scale);effects=new MeleeEffects(particles,Scale,EffectView,ChargeView,Plugin.LogEffects.Value);} }
            catch { effects?.Dispose();particles?.Dispose();view?.Dispose();MeleeNative.ssbm_sim_free(sim);throw; }
            host=new MeleeNative.Host { collide=System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(collide) };
        }
        static void Floor(IntPtr user,in MeleeNative.Vec3 from,in MeleeNative.Vec3 to,out MeleeNative.Collision c)
        { c=new MeleeNative.Collision { position=new MeleeNative.Vec3(to.x,Mathf.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
        public void BeginIntro(){MeleeNative.ssbm_sim_place(sim,default,0);IntroFinished=false;requestIntro=true;sawUpB=false;
            MeleeNative.ssbm_sim_play_sound(sim,MeleeNative.ssbm_announce_sound(MeleeNative.FighterSamus));}
        public bool RenderAudio(float[] stereo)=>MeleeNative.ssbm_sim_audio(sim,stereo,(uint)stereo.Length/2)==stereo.Length/2;
        public void Step(float dt)
        {
            uint selected=Skins.Selected();if(selected!=costume){var replacement=new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,selected);view.Dispose();view=replacement;costume=selected;}
            if(idleOnly){view.IdlePose();view.LocalPosition=drawOffset;view.LocalRotation=previewRotation;return;}
            var pad=new MeleeNative.Pad { aim=new MeleeNative.Vec3(0,0,1),allowed=0 };
            if(requestIntro) {
                Advance(in pad,1f/60);
                requestIntro=false;introStarted=true;
                pad.buttons=1024;pad.allowed=8;
                Advance(in pad,1f/60);
                pad.buttons=0;pad.allowed=0;
            }
            Advance(in pad,dt);
            if(introStarted){
                if(MeleeNative.ssbm_sim_state(sim,out var state)!=1)throw new InvalidOperationException(MeleeNative.Error);
                if(state.move_id==23)sawUpB=true;
                if(sawUpB&&state.move_id!=23&&state.grounded!=0){
                    IntroFinished=true;introStarted=false;
                    MeleeNative.ssbm_sim_place(sim,default,0);Advance(in pad,1f/60);
                }
            }
            view.Pose(sim,dt);
            if(MeleeNative.ssbm_sim_state(sim,out var rendered)!=1)throw new InvalidOperationException(MeleeNative.Error);
            view.LocalPosition=drawOffset+new Vector3(rendered.position.x,rendered.position.y,rendered.position.z);
            view.LocalRotation=previewRotation;
        }
        void Advance(in MeleeNative.Pad pad,float dt)
        {
            MeleeNative.ssbm_sim_step(sim,in host,in pad,dt);
            effects?.Step(sim,dt);particles?.Step(dt);
        }
        public void Draw(Transform display)
        {
            if(idleOnly)return;
            MeleeNative.ssbm_sim_state(sim,out var state);
            var facing=new Vector3(state.facing.x,0,state.facing.z);
            var heading=facing.sqrMagnitude>0.001f?Quaternion.LookRotation(facing):Quaternion.identity;
            var root=Matrix4x4.TRS(display.position,display.rotation,Vector3.one)
                *Matrix4x4.Translate(view.LocalPosition*Scale)*Matrix4x4.Rotate(previewRotation)
                *Matrix4x4.Rotate(Quaternion.Inverse(heading))*Matrix4x4.Translate(-new Vector3(state.position.x,state.position.y,state.position.z)*Scale);
            particles?.Draw(root);effects?.Draw(root);
        }
        public void Dispose(){effects?.Dispose();particles?.Dispose();view.Dispose();MeleeNative.ssbm_sim_free(sim);}
    }
}
