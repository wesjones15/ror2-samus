using System;
using System.Collections.Generic;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    internal sealed class SamusAudio : IDisposable
    {
        static readonly HashSet<SamusAudio> active=new HashSet<SamusAudio>();
        readonly IntPtr sim;
        PcmStream stream;
        public SamusAudio(IntPtr handle)
        {
            sim=handle;
            try { stream=new PcmStream(32000);active.Add(this); }
            catch(Exception e) { Plugin.Log.LogError("Samus audio unavailable: "+e.Message); }
        }
        public void Update()
        {
            if(stream==null)return;
            try { stream.Pump(Render,GameSettings.SfxVolume(),Time.timeScale<=0); }
            catch(Exception e) { Plugin.Log.LogError("Samus audio stopped: "+e.Message+"; "+Native.Error);Dispose(); }
        }
        bool Render(float[] pcm)=>Native.ssbm_sim_audio(sim,pcm,(uint)pcm.Length/2)==pcm.Length/2;
        public void Dispose(){stream?.Dispose();stream=null;active.Remove(this);}
        public static void StopAll(){foreach(var audio in new List<SamusAudio>(active))audio.Dispose();}
    }
}
