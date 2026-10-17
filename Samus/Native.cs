using System;
using System.Runtime.InteropServices;
using Ror2Common;

namespace Samus
{
    internal static class Native
    {
        public const uint AbiVersion = 4;
        const string Dll = "ssbm";
        [StructLayout(LayoutKind.Sequential)] public struct Vec3
        {
            public float x, y, z;
            public Vec3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        }
        [StructLayout(LayoutKind.Sequential)] public struct Pad { public Vec3 movement, aim; public uint buttons, allowed; }
        [StructLayout(LayoutKind.Sequential)] public struct State
        {
            public Vec3 position, velocity, facing;
            public uint grounded, phase, move_id, jumps_used, shot_charge, smash_charge;
            public ulong frame;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Collision { public Vec3 position; public uint grounded, ceiling; }
        [StructLayout(LayoutKind.Sequential)] public struct Event { public uint kind, move_id; public float strength; public Vec3 position, direction; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void CollideFn(IntPtr user, in Vec3 from, in Vec3 desired, out Collision collision);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void EventFn(IntPtr user, in Event ev);
        [StructLayout(LayoutKind.Sequential)] public struct Host { public IntPtr user, collide, evt; }
        [StructLayout(LayoutKind.Sequential)] public struct Tuning { public float speed_scale, jump_scale; public uint extra_jumps, hold_charge, smash_max_frames; }
        [StructLayout(LayoutKind.Sequential)] public struct HitboxSample
        {
            public ulong serial;
            public uint generation,group,id;
            public float damage,radius;
            public Vec3 local_position,origin,facing;
            public uint angle,growth,fixed_knockback,base_knockback,element,flags;
        }
        [StructLayout(LayoutKind.Sequential)] public struct ProjectileState
        {
            public Vec3 position,velocity;
            public float frame,animation_frame,damage,radius;
            public uint kind,state,flags,element;
        }

        public static void Load(string path)
        {
            if (!NativeLib.Load(path, out string error)) throw new InvalidOperationException(error);
            if (ssbm_abi_version() != AbiVersion) throw new InvalidOperationException("Samus native ABI mismatch");
            var sizes = new uint[14]; uint count=ssbm_struct_sizes(sizes,(uint)sizes.Length);
            error=NativeLib.CompareSizes(sizes,count,new[] {
                ("Vec3",NativeLib.Size<Vec3>()),("Pad",NativeLib.Size<Pad>()),("State",NativeLib.Size<State>()),
                ("Collision",NativeLib.Size<Collision>()),("Event",NativeLib.Size<Event>()),("Host",NativeLib.Size<Host>()),
                ("Tuning",NativeLib.Size<Tuning>()),("ViewModelInfo",NativeLib.Size<ViewModelInfo>()),
                ("ViewBatch",NativeLib.Size<ViewBatch>()),("ViewTexture",NativeLib.Size<ViewTexture>()),("ViewDraw",NativeLib.Size<ViewDraw>()),
                ("HitboxSample",NativeLib.Size<HitboxSample>()),("ViewParticle",NativeLib.Size<ViewParticle>()),("ProjectileState",NativeLib.Size<ProjectileState>()) });
            if (error!=null) throw new InvalidOperationException(error);
        }
        public static string Error => NativeLib.Utf8(ssbm_last_error());
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_charge_visual(IntPtr sim,[Out]float[] matrix);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_hit_feedback(IntPtr audio,uint element,uint kind,uint severity);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_projectile_new(byte[] dir,uint kind,uint state,float charge,Vec3 direction);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_projectile_step(IntPtr projectile,double dt,Vec3 target,uint hasTarget);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_projectile_state(IntPtr projectile,out ProjectileState state);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_projectile_place(IntPtr projectile,Vec3 relative,uint grounded);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_projectile_impact(IntPtr projectile,IntPtr audio);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_projectile_free(IntPtr projectile);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_particles_new(byte[] dir);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_particles_free(IntPtr handle);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_particles_spawn(IntPtr handle,uint id,float[] transform,ulong key);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_particles_attach(IntPtr handle,ulong key,float[] transform);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_particles_stop(IntPtr handle,ulong key);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_particles_step(IntPtr handle,double dt);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_particles_draws(IntPtr handle,[Out]ViewParticle[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_particles_texture(IntPtr handle,uint id,out ViewTexture info,[Out]byte[] rgba,uint len);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_abi_version();
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_struct_sizes([Out]uint[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr ssbm_last_error();
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_assets_complete(byte[] dir);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_portrait(byte[] dir,uint costume,out ViewTexture info,[Out]byte[] rgba,uint len);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_extract(byte[] disc,byte[] dir);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_sim_new(byte[] dir,Vec3 position);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_sim_free(IntPtr sim);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_step(IntPtr sim,in Host host,in Pad pad,double dt);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_state(IntPtr sim,out State state);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_hitboxes(IntPtr sim,[Out]HitboxSample[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_audio(IntPtr sim,[Out]float[] stereo,uint frames);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_tuning(IntPtr sim,Tuning tuning);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_sim_place(IntPtr sim,Vec3 vector,uint kind);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_sim_interrupt(IntPtr sim);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_sim_hurt(IntPtr sim);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_sim_launch_pad(IntPtr sim,Vec3 velocity,Vec3 gravity);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_view_new(byte[] dir,uint costume);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_article_new(byte[] dir,uint kind,uint state);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_object_update(IntPtr view,float frame);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_article_cues(uint kind,uint state,uint previous,uint frame,uint fullCharge,[Out]uint[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int ssbm_effect_target(uint id);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_effect_flags(uint id);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_effect_new(byte[] dir,uint id);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_entry_platform_new(byte[] dir);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_entry_platform_update(IntPtr view,IntPtr sim);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_begin_entry(IntPtr sim,byte[] dir);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern float ssbm_object_duration(IntPtr view);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_gfx(IntPtr sim,[Out]Event[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_sim_bone(IntPtr sim,uint bone,[Out]float[] matrix);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern ulong ssbm_sim_motion_serial(IntPtr sim);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void ssbm_view_free(IntPtr view);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_pose(IntPtr view,byte[] clip,float frame);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_update(IntPtr view,IntPtr sim,float dt);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_model(IntPtr view,out ViewModelInfo info);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_batches(IntPtr view,[Out]ViewBatch[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_indices(IntPtr view,[Out]int[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_uvs(IntPtr view,[Out]float[] output,uint max);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_view_positions(IntPtr view,out uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_view_normals(IntPtr view,out uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_view_colors(IntPtr view,out uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr ssbm_view_texture_ids(IntPtr view,out uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint ssbm_view_texture(IntPtr view,uint id,out ViewTexture info,[Out]byte[] rgba,uint len);
    }
}
