using System;
using Ror2Common;
using RoR2;
using UnityEngine;

namespace Samus
{
    internal sealed class SamusView:IViewSource,IDisposable
    {
        internal IntPtr Handle { get; private set; }
        internal ModelView Models { get; private set; }
        internal Quaternion LocalRotation=Quaternion.identity;
        internal Vector3 LocalPosition;
        internal SamusView(uint costume)
            :this(Native.ssbm_view_new(NativeLib.CString(Plugin.AssetsDir),Math.Min(costume,4))) {}
        internal SamusView(uint article,uint state)
            :this(Native.ssbm_article_new(NativeLib.CString(Plugin.AssetsDir),article,state)) {}
        internal static SamusView Effect(uint model)=>new SamusView(Native.ssbm_effect_new(NativeLib.CString(Plugin.AssetsDir),model));
        SamusView(IntPtr handle)
        {
            Handle=handle;
            if(Handle==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            Models=ModelView.Create(this,"Melee Samus",translucentOpacity:1f);
            if(Models==null){Dispose();throw new InvalidOperationException("Samus ModelView creation failed");}
        }
        internal void Pose(IntPtr sim,float dt){if(Native.ssbm_view_update(Handle,sim,dt)!=1)throw new InvalidOperationException(Native.Error);}
        internal void ObjectPose(float frame){if(Native.ssbm_object_update(Handle,frame)!=1)throw new InvalidOperationException(Native.Error);}
        public uint ModelCount()=>1;
        public bool Model(uint id,out ViewModelInfo info)=>Native.ssbm_view_model(Handle,out info)==1;
        public uint Batches(uint id,ViewBatch[] output,uint max)=>Native.ssbm_view_batches(Handle,output,max);
        public uint Indices(uint id,int[] output,uint max)=>Native.ssbm_view_indices(Handle,output,max);
        public uint Uvs(uint id,float[] output,uint max)=>Native.ssbm_view_uvs(Handle,output,max);
        public bool Texture(uint id,out ViewTexture info,byte[] rgba,uint len)=>Native.ssbm_view_texture(Handle,id,out info,rgba,len)==1;
        public uint Draws(ViewDraw[] output,uint max){if(output!=null&&max>0)output[0]=new ViewDraw { key=1,pos=new ViewVec3(LocalPosition.x,LocalPosition.y,LocalPosition.z),rot_x=LocalRotation.x,rot_y=LocalRotation.y,rot_z=LocalRotation.z,rot_w=LocalRotation.w,alpha=1 };return 1;}
        public IntPtr Positions(out uint count)=>Native.ssbm_view_positions(Handle,out count);
        public IntPtr Normals(out uint count)=>Native.ssbm_view_normals(Handle,out count);
        public IntPtr Colors(out uint count)=>Native.ssbm_view_colors(Handle,out count);
        public IntPtr TextureIds(out uint count)=>Native.ssbm_view_texture_ids(Handle,out count);
        public IntPtr FrameUvs(out uint count){count=0;return IntPtr.Zero;}
        public void Dispose(){Models?.Dispose();Models=null;if(Handle!=IntPtr.Zero){Native.ssbm_view_free(Handle);Handle=IntPtr.Zero;}}
    }
    internal sealed class SamusDisplay:IMeleeSelectSource
    {
        static readonly Native.CollideFn collide=Floor;
        SamusView view;
        uint costume;
        readonly IntPtr sim;
        readonly Native.Host host;
        bool requestUpSpecial,upSpecialStarted;
        readonly float[] anchorMatrix=new float[16];
        static readonly Quaternion previewRotation=Quaternion.Euler(0,-90,0);
        public bool UpSpecialFinished { get; private set; }
        public int AudioRate=>32000;
        public Vector3 DrawOffset {set{view.LocalPosition=value;}}
        public Vector3 PoseAnchor {
            get {
                if(Native.ssbm_sim_state(sim,out var state)!=1 || Native.ssbm_sim_bone(sim,4,anchorMatrix)!=1)throw new InvalidOperationException(Native.Error);
                var facing=new Vector3(state.facing.x,0,state.facing.z);
                var posed=new Vector3(anchorMatrix[12]-state.position.x,anchorMatrix[13]-state.position.y,anchorMatrix[14]-state.position.z);
                if(facing.sqrMagnitude>0.001f)posed=Quaternion.Inverse(Quaternion.LookRotation(facing))*posed;
                // Native bone and ModelView use the same X mirror. Remove only
                // the fighter's world heading/translation, then orient the preview.
                return previewRotation*posed;
            }
        }
        public ModelView Models=>view.Models;
        public float Scale=>Plugin.MetresPerUnit.Value;
        public static IDisplayActor Make()=>Plugin.AssetsReady?new MeleeSelectDisplay(new SamusDisplay()):null;
        public static IDisplayActor Portrait()=>Plugin.AssetsReady?new MeleeSelectDisplay(new SamusDisplay(),intro:false):null;
        SamusDisplay()
        {
            sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),default);
            if(sim==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            try { costume=Skins.Selected();view=new SamusView(costume); }
            catch { Native.ssbm_sim_free(sim);throw; }
            host=new Native.Host { collide=System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(collide) };
        }
        static void Floor(IntPtr user,in Native.Vec3 from,in Native.Vec3 to,out Native.Collision c)
        { c=new Native.Collision { position=new Native.Vec3(to.x,Mathf.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
        public void BeginUpSpecial(){requestUpSpecial=true;}
        public bool RenderAudio(float[] stereo)=>Native.ssbm_sim_audio(sim,stereo,(uint)stereo.Length/2)==stereo.Length/2;
        public void Step(float dt)
        {
            uint selected=Skins.Selected();if(selected!=costume){var replacement=new SamusView(selected);view.Dispose();view=replacement;costume=selected;}
            var pad=new Native.Pad { aim=new Native.Vec3(0,0,1),allowed=0 };
            if(requestUpSpecial) {
                // Settle the preview's flat floor before the ground up-special.
                if(Native.ssbm_sim_step(sim,in host,in pad,1.0/60)!=1)throw new InvalidOperationException(Native.Error);
                requestUpSpecial=false;upSpecialStarted=true;
                pad.buttons=4|8;pad.allowed=8; // libssbm SPECIAL|UP, up-special slot.
                // One complete native tick latches the press even at high refresh.
                if(Native.ssbm_sim_step(sim,in host,in pad,1.0/60)!=1)throw new InvalidOperationException(Native.Error);
                pad.buttons=0;pad.allowed=0;
            }
            // Return value is the number of fixed ticks, including zero at high refresh.
            Native.ssbm_sim_step(sim,in host,in pad,dt);
            if(upSpecialStarted){if(Native.ssbm_sim_state(sim,out var state)!=1)throw new InvalidOperationException(Native.Error);UpSpecialFinished=state.move_id!=23;}
            view.Pose(sim,dt);
            // The authored Wait pose faces +X; displays/portraits face +Z.
            view.LocalRotation=previewRotation;
        }
        public void Dispose(){view.Dispose();Native.ssbm_sim_free(sim);}
    }
}
