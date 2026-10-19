using System;
using Ror2Common;
using RoR2;
using UnityEngine;

namespace Samus
{
    internal sealed class SamusView:IViewSource,IAnimatedTextureSource,IDisposable
    {
        internal IntPtr Handle { get; private set; }
        internal ModelView Models { get; private set; }
        internal Quaternion LocalRotation=Quaternion.identity;
        internal Vector3 LocalPosition;
        readonly float[] overlay=new float[4];
        readonly MaterialAlphaBuffer alpha=new MaterialAlphaBuffer();
        byte drawFlags;
        internal ViewDraw[] InstanceDraws;
        internal SamusView(uint costume)
            :this(MeleeNative.ssbm_view_new(NativeLib.CString(Plugin.AssetsDir),MeleeNative.FighterSamus,Math.Min(costume,4))) {}
        internal SamusView(uint article,uint state)
            :this(MeleeNative.ssbm_article_new(NativeLib.CString(Plugin.AssetsDir),MeleeNative.FighterSamus,article,state)) {}
        internal static SamusView Effect(uint model)=>new SamusView(MeleeNative.ssbm_effect_new(NativeLib.CString(Plugin.AssetsDir),MeleeNative.FighterSamus,model));
        internal static SamusView Game()=>new SamusView(MeleeNative.ssbm_game_new(NativeLib.CString(Plugin.AssetsDir)));
        internal static SamusView EntryPlatform()=>new SamusView(MeleeNative.ssbm_entry_platform_new(NativeLib.CString(Plugin.AssetsDir)));
        internal static SamusView Grapple(uint part)=>new SamusView(MeleeNative.ssbm_grapple_new(NativeLib.CString(Plugin.AssetsDir),part));
        SamusView(IntPtr handle)
        {
            Handle=handle;
            if(Handle==IntPtr.Zero)throw new InvalidOperationException(MeleeNative.Error);
            Models=ModelView.Create(this,"Melee Samus",translucentOpacity:1f);
            if(Models==null){Dispose();throw new InvalidOperationException("Samus ModelView creation failed");}
        }
        internal void Pose(IntPtr sim,float dt){if(MeleeNative.ssbm_view_update(Handle,sim,dt)!=1)throw new InvalidOperationException(MeleeNative.Error);
            drawFlags=MeleeNative.HasOverlay&&MeleeNative.ssbm_sim_overlay(sim,overlay)==1?(byte)1:(byte)0;
            if(drawFlags!=0)Models.FlashColor=new Color(overlay[0],overlay[1],overlay[2],overlay[3]);
        }
        internal void IdlePose(){if(MeleeNative.ssbm_view_pose(Handle,NativeLib.CString("Wait"),0)!=1)throw new InvalidOperationException(MeleeNative.Error);}
        internal void ClipPose(string clip,float frame){if(MeleeNative.ssbm_view_pose(Handle,NativeLib.CString(clip),frame)!=1)throw new InvalidOperationException(MeleeNative.Error);}
        internal void ObjectPose(float frame){if(MeleeNative.ssbm_object_update(Handle,frame)!=1)throw new InvalidOperationException(MeleeNative.Error);}
        internal void FaceCamera(Vector3 position,Quaternion rotation,float scale){var camera=GameSettings.PlayerCamera();if(!camera||scale<=0)return;var local=Quaternion.Inverse(rotation)*(camera.position-position)/scale;local.x=-local.x;if(MeleeNative.ssbm_object_camera(Handle,new MeleeNative.Vec3(local.x,local.y,local.z))!=1)throw new InvalidOperationException(MeleeNative.Error);}
        public uint ModelCount()=>1;
        public bool Model(uint id,out ViewModelInfo info)=>MeleeNative.ssbm_view_model(Handle,out info)==1;
        public uint Batches(uint id,ViewBatch[] output,uint max)=>MeleeNative.ssbm_view_batches(Handle,output,max);
        public uint Indices(uint id,int[] output,uint max)=>MeleeNative.ssbm_view_indices(Handle,output,max);
        public uint Uvs(uint id,float[] output,uint max)=>MeleeNative.ssbm_view_uvs(Handle,output,max);
        public bool Texture(uint id,out ViewTexture info,byte[] rgba,uint len)=>MeleeNative.ssbm_view_texture(Handle,id,out info,rgba,len)==1;
        public uint Draws(ViewDraw[] output,uint max){if(InstanceDraws!=null){if(output!=null)Array.Copy(InstanceDraws,output,Math.Min(InstanceDraws.Length,(int)max));return(uint)InstanceDraws.Length;}if(output!=null&&max>0)output[0]=new ViewDraw { key=1,flags=drawFlags,pos=new ViewVec3(LocalPosition.x,LocalPosition.y,LocalPosition.z),rot_x=LocalRotation.x,rot_y=LocalRotation.y,rot_z=LocalRotation.z,rot_w=LocalRotation.w,alpha=1 };return 1;}
        public IntPtr Positions(out uint count)=>MeleeNative.ssbm_view_positions(Handle,out count);
        public IntPtr Normals(out uint count)=>MeleeNative.ssbm_view_normals(Handle,out count);
        public IntPtr Colors(out uint count){var colors=MeleeNative.ssbm_view_colors(Handle,out count);if(!MeleeNative.HasMaterialAlpha)return colors;var sources=MeleeNative.ssbm_view_alpha_sources(Handle,out uint n);return alpha.Resolve(colors,count,sources,n);}
        public IntPtr TextureIds(out uint count)=>MeleeNative.ssbm_view_texture_ids(Handle,out count);
        public IntPtr FrameUvs(out uint count)=>MeleeNative.ssbm_view_frame_uvs(Handle,out count);
        public uint TextureVersion(uint id)=>MeleeNative.ssbm_view_texture_version(Handle,id);
        public void Dispose(){Models?.Dispose();Models=null;alpha.Dispose();if(Handle!=IntPtr.Zero){MeleeNative.ssbm_view_free(Handle);Handle=IntPtr.Zero;}}
    }
    internal sealed class SamusDisplay:IMeleeSelectSource,IDisplayEffects
    {
        static readonly MeleeNative.CollideFn collide=Floor;
        SamusView view;
        uint costume;
        readonly IntPtr sim;
        readonly MeleeNative.Host host;
        bool requestIntro,introStarted,sawUpB;
        readonly bool idleOnly;
        DiscParticles particles;
        MeleeEffects effects;
        readonly float[] anchorMatrix=new float[16];
        Vector3 drawOffset;
        static readonly Quaternion previewRotation=Quaternion.identity;
        public bool IntroFinished { get; private set; }
        public int AudioRate=>32000;
        public Vector3 DrawOffset {set{drawOffset=value;}}
        public Vector3 PoseAnchor {
            get {
                if(MeleeNative.ssbm_sim_state(sim,out var state)!=1 || MeleeNative.ssbm_sim_bone(sim,4,anchorMatrix)!=1)throw new InvalidOperationException(MeleeNative.Error);
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
            try { costume=Skins.Selected();view=new SamusView(costume);if(!idleOnly){particles=new DiscParticles(Scale);effects=new MeleeEffects(particles,Scale);} }
            catch { effects?.Dispose();particles?.Dispose();view?.Dispose();MeleeNative.ssbm_sim_free(sim);throw; }
            host=new MeleeNative.Host { collide=System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(collide) };
        }
        static void Floor(IntPtr user,in MeleeNative.Vec3 from,in MeleeNative.Vec3 to,out MeleeNative.Collision c)
        { c=new MeleeNative.Collision { position=new MeleeNative.Vec3(to.x,Mathf.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
        public void BeginIntro(){MeleeNative.ssbm_sim_place(sim,default,0);IntroFinished=false;requestIntro=true;sawUpB=false;
            // mncharsel.c calls gm_80168C5C on pick: the announcer says the
            // fighter's name before the intro move plays. The native bank owns it.
            MeleeNative.ssbm_sim_play_sound(sim,MeleeNative.ssbm_announce_sound(MeleeNative.FighterSamus));}
        public bool RenderAudio(float[] stereo)=>MeleeNative.ssbm_sim_audio(sim,stereo,(uint)stereo.Length/2)==stereo.Length/2;
        public void Step(float dt)
        {
            uint selected=Skins.Selected();if(selected!=costume){var replacement=new SamusView(selected);view.Dispose();view=replacement;costume=selected;}
            if(idleOnly){view.IdlePose();view.LocalPosition=drawOffset;view.LocalRotation=previewRotation;return;}
            var pad=new MeleeNative.Pad { aim=new MeleeNative.Vec3(0,0,1),allowed=0 };
            if(requestIntro) {
                // Settle the preview's flat floor before Samus's grounded up-B.
                Advance(in pad,1f/60);
                requestIntro=false;introStarted=true;
                pad.buttons=1024;pad.allowed=8; // libssbm UP_B, up-special (Screw Attack) slot.
                // One complete native tick latches the press even at high refresh.
                Advance(in pad,1f/60);
                pad.buttons=0;pad.allowed=0;
            }
            // Return value is the number of fixed ticks, including zero at high refresh.
            Advance(in pad,dt);
            if(introStarted){
                if(MeleeNative.ssbm_sim_state(sim,out var state)!=1)throw new InvalidOperationException(MeleeNative.Error);
                if(state.move_id==23)sawUpB=true;
                // Finish only once the Screw Attack has run and the fighter has
                // returned to the lobby floor, so the full rise-and-fall shows.
                if(sawUpB&&state.move_id!=23&&state.grounded!=0){
                    IntroFinished=true;introStarted=false;
                    // Leave Samus standing on the lobby floor after the up-B.
                    MeleeNative.ssbm_sim_place(sim,default,0);Advance(in pad,1f/60);
                }
            }
            view.Pose(sim,dt);
            // Source animation pose is local to the fighter. Add the native
            // authored displacement for any selection move remains visible.
            if(MeleeNative.ssbm_sim_state(sim,out var rendered)!=1)throw new InvalidOperationException(MeleeNative.Error);
            view.LocalPosition=drawOffset+new Vector3(rendered.position.x,rendered.position.y,rendered.position.z);
            // Viewer front-view validation: authored Wait faces +Z.
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

