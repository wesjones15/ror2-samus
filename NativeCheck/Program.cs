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
    static int Main(string[] args)
    {
        try {Run(args);return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Run(string[] args)
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
            Console.WriteLine("PASS: ABI 4 + 14 layouts; SKIP: asset-dependent checks (no complete local cache)");
            return;
        }
        var host=new Native.Host { collide=Marshal.GetFunctionPointerForDelegate(collide),evt=Marshal.GetFunctionPointerForDelegate(evt) };
        for(uint c=0;c<5;c++) {
            Check(Native.ssbm_portrait(NativeLib.CString(assets),c,out var portrait,null,0)==1,Native.Error);
            var rgba=new byte[portrait.width*portrait.height*4];
            Check(Native.ssbm_portrait(NativeLib.CString(assets),c,out portrait,rgba,(uint)rgba.Length)==1,Native.Error);
            Check(portrait.width==64&&portrait.height==56,"expected CSS grid photo, not selected-character photo");
            Check(Native.ssbm_portrait(NativeLib.CString(assets),c,out portrait,new byte[1],1)==0,"portrait accepted short output buffer");
        }
        var sim=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,Native.Error);
        IntPtr view=IntPtr.Zero;
        try {
            var platform=Native.ssbm_entry_platform_new(NativeLib.CString(assets));Check(platform!=IntPtr.Zero,"entry platform: "+Native.Error);
            try {
                Check(Native.ssbm_sim_begin_entry(sim,NativeLib.CString(assets))==1,"entry start: "+Native.Error);
                if(Native.HasOverlay) {
                    var tint=new float[4];Check(Native.ssbm_sim_overlay(sim,tint)==1&&tint[3]>0,"source entry overlay absent");
                    foreach(float channel in tint)Check(float.IsFinite(channel)&&channel>=0&&channel<=1,"entry overlay channel outside bounds");
                }
                bool entrySound=false;var entryPcm=new float[1066];
                var entryPad=new Native.Pad {aim=new Native.Vec3(0,0,1),allowed=15,buttons=2|4};
                for(int f=0;f<60;f++) {
                    Native.ssbm_sim_step(sim,in host,in entryPad,1.0/60);
                    Native.ssbm_sim_state(sim,out var entryState);Check(entryState.move_id==0,"entry allowed an attack");
                    if(f<59)Check(Native.ssbm_entry_platform_update(platform,sim)==1,"entry platform stopped early");
                    Check(Native.ssbm_sim_audio(sim,entryPcm,533)==533,Native.Error);
                    foreach(float v in entryPcm)if(Math.Abs(v)>0.00001f)entrySound=true;
                }
                Check(entrySound,"entry source sound missing");Check(Native.ssbm_entry_platform_update(platform,sim)==0,"entry did not finish");
            } finally {Native.ssbm_view_free(platform);}
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
            var particles=Native.ssbm_particles_new(NativeLib.CString(assets));Check(particles!=IntPtr.Zero,Native.Error);
            try {
                var identity=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
                for(uint id=2000;id<2022;id++) {
                    Check(Native.ssbm_particles_spawn(particles,id,identity,1)==1,Native.Error);
                    bool drawn=false;
                    for(int frame=0;frame<30;frame++) {
                        Check(Native.ssbm_particles_step(particles,1.0/60)==1,Native.Error);
                        uint n=Native.ssbm_particles_draws(particles,null,0);if(n==0)continue;
                        var sprites=new ViewParticle[n];Check(Native.ssbm_particles_draws(particles,sprites,n)==n,Native.Error);
                        foreach(var sprite in sprites) {
                            Check(float.IsFinite(sprite.position.x)&&float.IsFinite(sprite.size)&&float.IsFinite(sprite.r),"invalid particle snapshot");
                            Check(Native.ssbm_particles_texture(particles,sprite.texture,out var info,null,0)==1,Native.Error);
                            var rgba=new byte[info.width*info.height*4];
                            Check(Native.ssbm_particles_texture(particles,sprite.texture,out info,rgba,(uint)rgba.Length)==1,Native.Error);
                        }
                        drawn=true;
                    }
                    Check(drawn,"emitter never produced a billboard: "+id);Native.ssbm_particles_stop(particles,1);
                }
            } finally { Native.ssbm_particles_free(particles); }
            for(uint kind=0;kind<4;kind++)for(uint articleState=0;articleState<(kind==0?2:kind==1?9:kind==2?4:1);articleState++) {
                var article=Native.ssbm_article_new(NativeLib.CString(assets),kind,articleState);Check(article!=IntPtr.Zero,"article: "+Native.Error);
                try { Check(Native.ssbm_object_update(article,10)==1,Native.Error);Native.ssbm_view_model(article,out var model);Check(model.vertex_count>0&&model.index_count>0,"empty article model"); }
                finally { Native.ssbm_view_free(article); }
            }
            foreach(var articleCase in new[]{(kind:0u,state:0u),(kind:1u,state:1u),(kind:1u,state:8u),(kind:2u,state:0u),(kind:2u,state:1u)}) {
                Native.ProjectileState? reference=null;
                foreach(int fps in new[]{30,60,144}) {
                    var projectile=Native.ssbm_projectile_new(NativeLib.CString(assets),articleCase.kind,articleCase.state,articleCase.state==8?1:0,new Native.Vec3(0,0,1));
                    Check(projectile!=IntPtr.Zero,"projectile create: "+Native.Error);
                    try {
                        for(int f=0;f<fps;f++)Native.ssbm_projectile_step(projectile,1.0/fps,default,0);
                        Check(Native.ssbm_projectile_state(projectile,out var flight)==1,Native.Error);
                        Check(flight.frame==60&&flight.damage>0&&flight.radius>0,"disc projectile clock/hitbox");
                        if(reference.HasValue)Check(Math.Abs(reference.Value.position.z-flight.position.z)<0.0001f&&Math.Abs(reference.Value.position.y-flight.position.y)<0.0001f,"projectile changed with host rate");
                        reference=flight;
                        uint cue=Native.ssbm_projectile_impact(projectile,sim);
                        Check(cue==1001||cue==1036||cue==1038,"source impact callback absent");
                        Check(Native.ssbm_projectile_impact(projectile,sim)==0,"impact replayed");
                        Check(Native.ssbm_projectile_state(projectile,out var explosion)==1,Native.Error);
                        Check((explosion.flags&1)!=0,"impact state missing");
                        if(articleCase.kind!=1)Check(explosion.state==(articleCase.kind==0?1:articleCase.state+2),"wrong source explosion animation");
                        for(int f=0;f<fps;f++)Native.ssbm_projectile_step(projectile,1.0/fps,default,0);
                        Check(Native.ssbm_projectile_state(projectile,out explosion)==1&&(explosion.flags&2)!=0,"source explosion never finished");
                    } finally {Native.ssbm_projectile_free(projectile);}
                }
            }
            Console.WriteLine("PASS: disc projectile speeds, hitboxes, fixed clocks and explosion states at 30/60/144 Hz");
            for(uint costume=0;costume<5;costume++) {
                view=Native.ssbm_view_new(NativeLib.CString(assets),costume);Check(view!=IntPtr.Zero,Native.Error);
                Check(Native.ssbm_view_update(view,sim,1f/60)==1,Native.Error);
                Check(Native.ssbm_view_pose(view,NativeLib.CString("Wait"),0)==1,"direct idle portrait pose failed");
                var idlePtr=Native.ssbm_view_positions(view,out uint idleCount);var idleVertices=new float[idleCount*3];Marshal.Copy(idlePtr,idleVertices,0,idleVertices.Length);
                Check(Native.ssbm_view_pose(view,NativeLib.CString("SpecialHi"),15)==1,"up-special test pose failed");
                Check(Native.ssbm_view_pose(view,NativeLib.CString("Wait"),0)==1,"return to idle failed");
                var restoredPtr=Native.ssbm_view_positions(view,out uint restoredCount);var restored=new float[restoredCount*3];Marshal.Copy(restoredPtr,restored,0,restored.Length);
                Check(idleCount==restoredCount,"portrait vertex count changed");
                for(int vertex=0;vertex<restored.Length;vertex++)Check(idleVertices[vertex]==restored[vertex],"portrait retained attack/landing pose");
                Check(Native.ssbm_view_pose(view,NativeLib.CString("missing-clip"),0)==0,"unknown portrait clip accepted");
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
            // Character select uses the actual native up-special and sound cues.
            // Different host rates must accept zero/multiple ticks per call.
            foreach(int fps in new[]{30,60,144}) {
                var select=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(select!=IntPtr.Zero,Native.Error);
                try {
                    var selectPad=new Native.Pad {aim=new Native.Vec3(0,0,1),allowed=0};
                    Native.ssbm_sim_step(select,in host,in selectPad,1.0/60);
                    selectPad.buttons=4|8;selectPad.allowed=8;
                    Native.ssbm_sim_step(select,in host,in selectPad,1.0/60);
                    Native.ssbm_sim_state(select,out var selectState);Check(selectState.move_id==23,"selection did not start up-special");
                    selectPad.buttons=0;selectPad.allowed=0;
                    bool audible=false,completed=false;
                    var pcm=new float[2048];
                    for(int frame=0;frame<fps*4;frame++) {
                        Native.ssbm_sim_step(select,in host,in selectPad,1.0/fps);
                        Check(Native.ssbm_sim_audio(select,pcm,1024)==1024,Native.Error);
                        foreach(float sample in pcm){Check(float.IsFinite(sample),"selection PCM nonfinite");if(Math.Abs(sample)>0.00001f)audible=true;}
                        Native.ssbm_sim_state(select,out selectState);if(selectState.move_id!=23)completed=true;
                    }
                    Check(audible&&completed,"up-special selection sound/completion failed at "+fps+" Hz");
                } finally {Native.ssbm_sim_free(select);}
            }
            Console.WriteLine("PASS: character-select up-special and source PCM at 30/60/144 Hz");
            // Reproduce the user's delayed failure through the exact C# bindings,
            // consuming GFX after each step rather than merely decoding archives.
            var longRun=Native.ssbm_sim_new(NativeLib.CString(assets),default);
            var longParticles=Native.ssbm_particles_new(NativeLib.CString(assets));
            var effectViews=new System.Collections.Generic.Dictionary<int,IntPtr>();
            Check(longRun!=IntPtr.Zero&&longParticles!=IntPtr.Zero,Native.Error);
            try {
                var pcm=new float[1066];var matrix=new float[16];int gfxCount=0,chargeFrames=0;
                for(int frame=0;frame<3600;frame++) {
                    int f=frame%600;
                    var p=new Native.Pad {aim=new Native.Vec3(0,0,1),allowed=15,
                        buttons=f==1?4u|8u:f==150||f==280?4u:f==350?4u|16u:f==450?2u|8u:f==500?2u:0u};
                    Native.ssbm_sim_step(longRun,in host,in p,1.0/60);
                    Check(Native.ssbm_sim_audio(longRun,pcm,533)==533,Native.Error);
                    if(Native.ssbm_sim_charge_visual(longRun,matrix)==1){foreach(float value in matrix)Check(float.IsFinite(value),"charge attachment nonfinite");chargeFrames++;}
                    uint count=Native.ssbm_sim_gfx(longRun,null,0);
                    var cues=new Native.Event[count];
                    Check(Native.ssbm_sim_gfx(longRun,cues,count)==count,"long-run GFX: "+Native.Error);
                    foreach(var cue in cues) {
                        gfxCount++;int target=Native.ssbm_effect_target(cue.kind);
                        Check(target!=int.MaxValue,"unresolved runtime effect "+cue.kind);
                        Check(Native.ssbm_sim_bone(longRun,cue.move_id&65535,matrix)==1,"posed effect bone: "+Native.Error);
                        if(target>=0)Check(Native.ssbm_particles_spawn(longParticles,(uint)target,matrix,(ulong)gfxCount)==1,Native.Error);
                        else if(target>int.MinValue+1000) {
                            if(!effectViews.TryGetValue(target,out var effect)) {
                                effect=Native.ssbm_effect_new(NativeLib.CString(assets),(uint)(-target-1));
                                Check(effect!=IntPtr.Zero,"runtime effect model: "+Native.Error);effectViews.Add(target,effect);
                            }
                            Check(Native.ssbm_object_update(effect,10)==1,Native.Error);
                            Check(Native.ssbm_view_positions(effect,out uint vertices)!=IntPtr.Zero&&vertices>0,"empty effect geometry");
                        }
                    }
                    Check(Native.ssbm_particles_step(longParticles,1.0/60)==1,"runtime particles: "+Native.Error);
                    Check(Native.ssbm_sim_state(longRun,out var running)==1&&float.IsFinite(running.position.y),"simulation failed after "+frame+" ticks");
                }
                Check(gfxCount>100&&chargeFrames>100,"long-run did not exercise attack/held-article cues");
                Console.WriteLine("PASS: 60 simulated seconds consuming "+gfxCount+" attack GFX cues, posed bones, effect models, particles and PCM through C#");
            } finally {
                foreach(var effect in effectViews.Values)Native.ssbm_view_free(effect);
                Native.ssbm_particles_free(longParticles);Native.ssbm_sim_free(longRun);
            }
            Console.WriteLine("PASS: ABI 4 + 14 layouts, trophy entry, grid tile, callbacks, jump/landing, posed hitboxes, disc attack PCM, all 22 Samus emitters, 16 article states, 5 costumes and texture transfers");
        } finally { if(view!=IntPtr.Zero)Native.ssbm_view_free(view);Native.ssbm_sim_free(sim); }
    }
}
