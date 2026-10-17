using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
        Native.Load(dll);
        if(args.Length>2) {
            // Exercise the release route: C# worker -> P/Invoke -> native DLL,
            // starting without a cache. No extractor EXE or shell script is used.
            Check(!Directory.Exists(assets),"first-run check requires a fresh cache path");
            string disc=Path.GetFullPath(args[2]),failure=null;
            var worker=new Thread(()=> {
                try {
                    if(Native.ssbm_extract(NativeLib.CString(disc),NativeLib.CString(assets))!=1) failure=Native.Error;
                    else if(Native.ssbm_assets_complete(NativeLib.CString(assets))!=1) failure="DLL extraction left an incomplete cache";
                }catch(Exception e){failure=e.ToString();}
            }) { IsBackground=true,Name="Samus first-run check" };
            worker.Start();Check(worker.Join(30000),"first-run extraction timed out");Check(failure==null,failure);
            Console.WriteLine("PASS: first-run extraction on a C# worker through ssbm.dll, with no external tools");
        }
        if(Native.ssbm_assets_complete(NativeLib.CString(assets))!=1) {
            // Clean release builds, like Mario/Olimar, need neither a disc nor a cache.
            Console.WriteLine("PASS: ABI + 12 layouts; SKIP: asset-dependent checks (no complete local cache)");
            return;
        }
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
            bool gotHitboxes=false,gotSound=false;
            pad.buttons=2;for(int i=0;i<10;i++) {
                Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
                var pcm=new float[1066];
                Check(Native.ssbm_sim_audio(sim,pcm,533)==533,"native PCM render failed: "+Native.Error);
                foreach(float x in pcm){Check(float.IsFinite(x),"nonfinite PCM");if(Math.Abs(x)>0.0001f)gotSound=true;}
                uint count=Native.ssbm_sim_hitboxes(sim,null,0);
                if(count==0)continue;
                var samples=new Native.HitboxSample[count];
                Check(Native.ssbm_sim_hitboxes(sim,samples,count)==count,"hitbox transfer failed");
                foreach(var hit in samples)Check(hit.radius>0&&hit.damage>0&&float.IsFinite(hit.local_position.x)&&float.IsFinite(hit.local_position.y)&&float.IsFinite(hit.local_position.z),"invalid posed hitbox");
                gotHitboxes=true;
            }
            Check(gotHitboxes&&hits==0,"disc melee hitboxes must replace the placeholder hit callback");
            Check(gotSound,"disc attack sound cue did not reach the native PCM mixer");
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
            Console.WriteLine("PASS: ABI + 12 layouts, real DLL callbacks, jump/landing, posed disc hitboxes, 5 costumes, mesh arrays and texture transfers");
        } finally { if(view!=IntPtr.Zero)Native.ssbm_view_free(view);Native.ssbm_sim_free(sim); }
    }
}
