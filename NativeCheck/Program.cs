using System;
using System.IO;
using System.Runtime.InteropServices;
using Ror2Common;
using Samus;

static class Program
{
    static readonly Native.CollideFn collide=Collide;
    static readonly Native.EventFn evt=Event;
    static int jumps, hits;
    static void Collide(IntPtr user,in Native.Vec3 from,in Native.Vec3 to,out Native.Collision result)
    { result=new Native.Collision { position=new Native.Vec3(to.x,Math.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
    static void Event(IntPtr user,in Native.Event e) { if(e.kind==3) jumps++; if(e.kind==2) hits++; }
    static void Check(bool ok,string what) { if(!ok) throw new Exception(what); }
    static void Main(string[] args)
    {
        string dll=Path.GetFullPath(args.Length>0?args[0]:"../../libssbm/target/release/ssbm.dll");
        string assets=Path.GetFullPath(args.Length>1?args[1]:"../../libssbm/assets");
        Native.Load(dll); Check(Native.ssbm_assets_complete(NativeLib.CString(assets))==1,"assets incomplete");
        var host=new Native.Host { collide=Marshal.GetFunctionPointerForDelegate(collide),evt=Marshal.GetFunctionPointerForDelegate(evt) };
        var sim=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,Native.Error);
        IntPtr view=IntPtr.Zero;
        try {
            var pad=new Native.Pad { aim=new Native.Vec3(1,0,0),allowed=15 };
            Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
            pad.buttons=1;for(int i=0;i<6;i++) Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
            Check(jumps==1,"jump callback/layout failed");
            pad.buttons=0;for(int i=0;i<180;i++) Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
            Native.ssbm_sim_state(sim,out var state);Check(state.grounded==1&&state.jumps_used==0,"landing state mismatch");
            pad.buttons=2;for(int i=0;i<10;i++) Native.ssbm_sim_step(sim,in host,in pad,1.0/60);Check(hits==1,"hit callback failed");
            for(uint costume=0;costume<5;costume++) {
                view=Native.ssbm_view_new(NativeLib.CString(assets),costume);Check(view!=IntPtr.Zero,Native.Error);
                Check(Native.ssbm_view_update(view,sim,1f/60)==1,Native.Error);
                Native.ssbm_view_model(view,out var model);Check(model.vertex_count>0&&model.batch_count>0,"empty model");
                var pos=Native.ssbm_view_positions(view,out uint n);Check(n==model.vertex_count&&pos!=IntPtr.Zero,"position pointer/count");
                var indices=new int[model.index_count];Native.ssbm_view_indices(view,indices,(uint)indices.Length);
                foreach(int i in indices) Check(i>=0&&i<model.vertex_count,"index outside vertices");
                var batches=new ViewBatch[model.batch_count];Native.ssbm_view_batches(view,batches,(uint)batches.Length);
                foreach(var b in batches) if(b.texture>=0) {
                    Check(Native.ssbm_view_texture(view,(uint)b.texture,out var tex,null,0)==1,"texture info failed");
                    var pixels=new byte[tex.width*tex.height*4];Check(Native.ssbm_view_texture(view,(uint)b.texture,out tex,pixels,(uint)pixels.Length)==1,"texture pixels failed");
                }
                Native.ssbm_view_free(view);view=IntPtr.Zero;
            }
            Check(Native.ssbm_view_new(NativeLib.CString(assets),99)==IntPtr.Zero,"bad costume not rejected");
            Console.WriteLine("PASS: ABI + 11 layouts, real DLL callbacks, jump/landing/attack, 5 costumes, mesh arrays and texture transfers");
        } finally { if(view!=IntPtr.Zero)Native.ssbm_view_free(view);Native.ssbm_sim_free(sim); }
    }
}
