using System;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    internal sealed class DiscParticles : IDisposable
    {
        readonly ParticleView view;
        public DiscParticles(float scale){view=new ParticleView(new Source(),scale);}
        public static Matrix4x4 At(Vector3 world,Quaternion rotation,float scale)=>ParticleView.At(world,rotation,scale);
        public void Spawn(uint id,Matrix4x4 transform,ulong key)=>view.Spawn(id,transform,key);
        public void Attach(ulong key,Matrix4x4 transform)=>view.Attach(key,transform);
        public void Stop(ulong key)=>view.Stop(key);
        public void Step(float dt)=>view.Step(dt);
        public void Draw(Matrix4x4? placement=null)=>view.Draw(placement);
        public void Dispose()=>view.Dispose();
        sealed class Source : IParticleSource
        {
            IntPtr handle;
            public Source(){handle=Native.ssbm_particles_new(NativeLib.CString(Plugin.AssetsDir));if(handle==IntPtr.Zero)throw new InvalidOperationException(Native.Error);}
            public string Error=>Native.Error;
            public bool Spawn(uint id,float[] transform,ulong key)=>Native.ssbm_particles_spawn(handle,id,transform,key)==1;
            public bool Attach(ulong key,float[] transform)=>Native.ssbm_particles_attach(handle,key,transform)==1;
            public void Stop(ulong key)=>Native.ssbm_particles_stop(handle,key);
            public bool Step(double dt)=>Native.ssbm_particles_step(handle,dt)==1;
            public uint Draws(ViewParticle[] output,uint max)=>Native.ssbm_particles_draws(handle,output,max);
            public bool Texture(uint id,out ViewTexture info,byte[] rgba,uint len)=>Native.ssbm_particles_texture(handle,id,out info,rgba,len)==1;
            public void Dispose(){if(handle!=IntPtr.Zero){Native.ssbm_particles_free(handle);handle=IntPtr.Zero;}}
        }
    }
}
