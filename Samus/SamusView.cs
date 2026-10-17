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
        internal SamusView(uint costume)
        {
            Handle=Native.ssbm_view_new(NativeLib.CString(Plugin.AssetsDir),Math.Min(costume,4));
            if(Handle==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            Models=ModelView.Create(this,"Melee Samus");
            if(Models==null){Dispose();throw new InvalidOperationException("Samus ModelView creation failed");}
        }
        internal void Pose(IntPtr sim,float dt){if(Native.ssbm_view_update(Handle,sim,dt)!=1)throw new InvalidOperationException(Native.Error);}
        public uint ModelCount()=>1;
        public bool Model(uint id,out ViewModelInfo info)=>Native.ssbm_view_model(Handle,out info)==1;
        public uint Batches(uint id,ViewBatch[] output,uint max)=>Native.ssbm_view_batches(Handle,output,max);
        public uint Indices(uint id,int[] output,uint max)=>Native.ssbm_view_indices(Handle,output,max);
        public uint Uvs(uint id,float[] output,uint max)=>Native.ssbm_view_uvs(Handle,output,max);
        public bool Texture(uint id,out ViewTexture info,byte[] rgba,uint len)=>Native.ssbm_view_texture(Handle,id,out info,rgba,len)==1;
        public uint Draws(ViewDraw[] output,uint max){if(output!=null&&max>0)output[0]=new ViewDraw { key=1,rot_x=LocalRotation.x,rot_y=LocalRotation.y,rot_z=LocalRotation.z,rot_w=LocalRotation.w,alpha=1 };return 1;}
        public IntPtr Positions(out uint count)=>Native.ssbm_view_positions(Handle,out count);
        public IntPtr Normals(out uint count)=>Native.ssbm_view_normals(Handle,out count);
        public IntPtr Colors(out uint count)=>Native.ssbm_view_colors(Handle,out count);
        public IntPtr TextureIds(out uint count)=>Native.ssbm_view_texture_ids(Handle,out count);
        public IntPtr FrameUvs(out uint count){count=0;return IntPtr.Zero;}
        public void Dispose(){Models?.Dispose();Models=null;if(Handle!=IntPtr.Zero){Native.ssbm_view_free(Handle);Handle=IntPtr.Zero;}}
    }
    internal sealed class SamusDisplay:IDisplayActor
    {
        static readonly Native.CollideFn collide=Floor;
        SamusView view;
        uint costume;
        readonly IntPtr sim;
        readonly Native.Host host;
        public ModelView Models=>view.Models;
        public float Scale=>Plugin.MetresPerUnit.Value;
        public static IDisplayActor Make()=>Plugin.AssetsReady?new SamusDisplay():null;
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
        public void Step(float dt,Transform display)
        {
            uint selected=Skins.Selected();if(selected!=costume){var replacement=new SamusView(selected);view.Dispose();view=replacement;costume=selected;}
            var pad=new Native.Pad { aim=new Native.Vec3(0,0,1),allowed=0 };
            Native.ssbm_sim_step(sim,in host,in pad,dt);view.Pose(sim,dt);
            // Disc Samus faces +X. Rotate her lobby model toward +Z.
            view.LocalRotation=Quaternion.Euler(0,-90,0);
        }
        public void Release(){view.Dispose();Native.ssbm_sim_free(sim);}
    }
}
