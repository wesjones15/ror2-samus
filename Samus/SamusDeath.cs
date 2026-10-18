using System;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    /// Shared MeleeDeathSequence owns timing; this adapter supplies Samus's
    /// extracted fall pose, ftCo_DeadDown KO effect (0x42b) and sound (0x61).
    internal sealed class SamusDeath : IMeleeDeathSource
    {
        readonly Vector3 feet;
        readonly Quaternion facing;
        readonly float scale;
        IntPtr sim;
        SamusView view;
        DiscParticles particles;
        MeleeEffects effects;
        PcmStream audio;
        bool vanished;
        public SamusDeath(uint costume,Vector3 feet,Quaternion facing,float scale)
        {
            this.feet=feet;this.facing=facing;this.scale=scale;
            try {
                view=new SamusView(costume);
                sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),new Native.Vec3(feet.x/scale,feet.y/scale,feet.z/scale));
                if(sim==IntPtr.Zero)throw new InvalidOperationException("Melee death sound: "+Native.Error);
                particles=new DiscParticles(scale);effects=new MeleeEffects(particles,scale);
                try{audio=new PcmStream(32000);}catch(Exception e){Plugin.Log.LogWarning("Melee death audio unavailable: "+e.Message);}
            } catch{Dispose();throw;}
        }
        public void Fall(float frame)
        {
            // RoR2 adaptation: play the source grounded knockdown before the
            // blast-zone KO, which in Melee normally occurs offscreen at once.
            view.ClipPose("DownBoundU",frame);
        }
        public void Vanish()
        {
            if(vanished)return;vanished=true;
            if(Native.ssbm_sim_begin_rebirth(sim,NativeLib.CString(Plugin.AssetsDir),new Native.Vec3(feet.x/scale,feet.y/scale,feet.z/scale))!=1)
                throw new InvalidOperationException("Melee death KO: "+Native.Error);
            var host=new Native.Host();var pad=new Native.Pad();
            Native.ssbm_sim_step(sim,in host,in pad,1.0/60.0);
            effects.Step(sim,1f/60f);
        }
        public void Step(float dt)
        {
            if(vanished){effects.Step(sim,dt,false);particles.Step(dt);}
            audio?.Pump(Render,GameSettings.SfxVolume(),false);
        }
        bool Render(float[] pcm)=>Native.ssbm_sim_audio(sim,pcm,(uint)pcm.Length/2)==pcm.Length/2;
        public void Draw()
        {
            if(!vanished)view.Models.Update(scale,d=>(feet,facing));
            else view.Models.HideAll();
            if(vanished){particles.Draw();effects.Draw();}
        }
        public void Dispose()
        {
            audio?.Dispose();audio=null;effects?.Dispose();effects=null;particles?.Dispose();particles=null;
            view?.Dispose();view=null;if(sim!=IntPtr.Zero){Native.ssbm_sim_free(sim);sim=IntPtr.Zero;}
        }
    }

    internal sealed class SamusGameSound : IMeleeRoundSound
    {
        IntPtr sim;
        PcmStream stream;
        public SamusGameSound()
        {
            try {
                sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),default);
                if(sim==IntPtr.Zero)throw new InvalidOperationException("Melee GAME sound: "+Native.Error);
                // if_2F6E.c::ifStatus_803F9628[5] / if_2F72.c::fn_802F7288.
                // The clip's sample 1513 lives in the user's nr_name.ssm.
                if(Native.ssbm_sim_play_sound(sim,0x7c855)!=1)throw new InvalidOperationException("Melee GAME cue: "+Native.Error);
                try{stream=new PcmStream(32000);}catch(Exception e){Plugin.Log.LogWarning("Melee GAME audio unavailable: "+e.Message);}
            }catch{Dispose();throw;}
        }
        public void Step(){stream?.Pump(Render,GameSettings.SfxVolume(),false);}
        bool Render(float[] pcm)=>Native.ssbm_sim_audio(sim,pcm,(uint)pcm.Length/2)==pcm.Length/2;
        public void Dispose(){stream?.Dispose();stream=null;if(sim!=IntPtr.Zero){Native.ssbm_sim_free(sim);sim=IntPtr.Zero;}}
    }
}
