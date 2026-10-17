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
    static int jumps, hits,throws;
    static Native.Event lastShot;static int shotCount;
    static void Collide(IntPtr user,in Native.Vec3 from,in Native.Vec3 to,out Native.Collision result)
    { result=new Native.Collision { position=new Native.Vec3(to.x,Math.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
    static void Event(IntPtr user,in Native.Event e) { if(e.kind==3) jumps++; if(e.kind==2) hits++;if(e.kind==6)throws++;if(e.kind==2&&e.move_id==20){lastShot=e;shotCount++;} }
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
        PresentationRegressions(assets,in host);
        RebirthRegressions(assets,in host);
        AimingRegressions(assets,in host);
        jumps=hits=throws=0;
        foreach(int fps in new[]{30,60,144}) {
            var dash=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(dash!=IntPtr.Zero,Native.Error);
            try {
                var pad=new Native.Pad {aim=new Native.Vec3(0,0,1),movement=new Native.Vec3(0,0,1),allowed=15,buttons=32};
                for(int f=0;f<60;f++)Native.ssbm_sim_step(dash,in host,in pad,1.0/60);
                Native.ssbm_sim_state(dash,out var before);pad.buttons|=2;
                Native.ssbm_sim_step(dash,in host,in pad,1.0/60);
                Native.ssbm_sim_state(dash,out var state);Check(state.move_id==5,"sprinting RT did not select Dash Attack");
                pad.buttons=0;pad.movement=default;float furthest=state.position.z;
                for(int f=0;f<fps*2;f++) {
                    Native.ssbm_sim_step(dash,in host,in pad,1.0/fps);Native.ssbm_sim_state(dash,out state);
                    Check(state.position.z>=furthest-0.001f,"dash rubberbanded at "+fps+" FPS");furthest=Math.Max(furthest,state.position.z);
                }
                Check(state.move_id==0&&state.position.z-before.position.z>10,"dash did not retain source movement after returning to idle");
            }finally{Native.ssbm_sim_free(dash);}
        }
        Console.WriteLine("PASS: sprint + RT retains Dash Attack travel through the native boundary at 30/60/144 FPS");
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
                var uvs=new float[model.vertex_count*2];Array.Fill(uvs,float.NaN);
                Check(Native.ssbm_view_uvs(view,uvs,model.vertex_count)==model.vertex_count,"UV export must count pairs");
                foreach(float coordinate in uvs)Check(float.IsFinite(coordinate),"UV export left half the source texture coordinates unwritten");
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
    static void AimingRegressions(string assets,in Native.Host host)
    {
        foreach(int fps in new[]{30,60,144}) {
            foreach(var example in new[]{(pitch:.6f,button:2u,want:11u),(pitch:-.9f,button:2u,want:12u),(pitch:.6f,button:4u,want:23u),(pitch:-.9f,button:4u,want:24u),(pitch:.9f,button:2u|16u,want:12u)}) {
                var sim=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,Native.Error);
                try {
                    var pad=new Native.Pad{aim=new Native.Vec3(0,example.pitch,(float)Math.Sqrt(1-example.pitch*example.pitch)),allowed=15};
                    Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
                    Native.ssbm_sim_facing(sim,new Native.Vec3(1,0,0));pad.movement=new Native.Vec3(0,0,1);
                    pad.buttons=example.button;
                    for(int i=0;i<Math.Max(1,fps/30);i++)Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                    Native.ssbm_sim_state(sim,out var state);Check(state.move_id==example.want,"camera directional selection through native DLL at "+fps);
                } finally {Native.ssbm_sim_free(sim);}
            }
            var actor=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(actor!=IntPtr.Zero,Native.Error);
            try {
                var pad=new Native.Pad{aim=new Native.Vec3(0,0,1),movement=new Native.Vec3(0,0,-1),allowed=15};
                Native.ssbm_sim_facing(actor,new Native.Vec3(0,0,1));
                for(int i=0;i<fps/2;i++)Native.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                Native.ssbm_sim_state(actor,out var state);Check(state.facing.z>.999f&&state.velocity.z<0,"native yaw override changed backpedal momentum");
                Native.ssbm_sim_place(actor,default,0);pad.movement=default;pad.buttons=4;
                Native.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)Native.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                Check(Native.ssbm_sim_charge_fraction(actor)>0,"aim test never charged");
                pad.buttons=128;Native.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)Native.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                int before=shotCount;pad.buttons=4;Native.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps/2;i++)Native.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                pad.aim=new Native.Vec3(.8f,.6f,0);Native.ssbm_sim_facing(actor,new Native.Vec3(0,0,-1));
                pad.buttons=4;Native.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)Native.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                Check(shotCount==before+1&&Math.Abs(lastShot.direction.x-.8f)<.001f&&Math.Abs(lastShot.direction.y-.6f)<.001f,"stored shot used body heading/old camera snapshot");
                Check(Native.ssbm_sim_facing_locked(actor)==0,"normal/ranged aim remains locked after completion");
            } finally {Native.ssbm_sim_free(actor);}
        }
        Native.ssbm_sim_facing(IntPtr.Zero,default);Check(Native.ssbm_sim_facing_locked(IntPtr.Zero)==0,"null facing boundary");
        Console.WriteLine("PASS: camera directional moves, independent host yaw/backpedal and stored-shot live aim through native DLL at 30/60/144 Hz");
    }
    static void PresentationRegressions(string assets,in Native.Host host)
    {
        var effect=Native.ssbm_effect_new(NativeLib.CString(assets),2000);Check(effect!=IntPtr.Zero,Native.Error);
        try {
            Check(Native.HasMaterialAlpha,"shared material alpha metadata export missing");
            using(var alpha=new MaterialAlphaBuffer())foreach(float frame in new[]{0f,12f,32f}) {
                Check(Native.ssbm_object_update(effect,frame)==1,Native.Error);
                var colors=Native.ssbm_view_colors(effect,out uint count);var expected=new float[count*4];Marshal.Copy(colors,expected,0,expected.Length);
                var metadata=Native.ssbm_view_alpha_sources(effect,out uint n);var raw=new float[n*3];Marshal.Copy(metadata,raw,0,raw.Length);
                Check(n==count,"alpha metadata record count");for(int i=0;i<raw.Length;i+=3)Check(raw[i]==0&&raw[i+2]==1,"expected zero-alpha material with texture blend");
                var resolved=alpha.Resolve(colors,count,metadata,n);var rgba=new float[count*4];Marshal.Copy(resolved,rgba,0,rgba.Length);
                for(int i=0;i<rgba.Length;i++)Check(Math.Abs(rgba[i]-expected[i])<0.00001f,"common alpha diverged from source viewer");
                if(frame==12)for(int i=3;i<rgba.Length;i+=4)Check(rgba[i]>0.1f,"shared alpha erased Screw Attack");
            }
        }
        finally {Native.ssbm_view_free(effect);}
        var chargeView=Native.ssbm_article_new(NativeLib.CString(assets),1,8);Check(chargeView!=IntPtr.Zero,Native.Error);
        try {
            Check(Native.ssbm_object_update(chargeView,12)==1,Native.Error);var before=Native.ssbm_view_positions(chargeView,out uint vertices);var first=new float[vertices*3];Marshal.Copy(before,first,0,first.Length);
            Check(Native.ssbm_object_camera(chargeView,new Native.Vec3(100,25,-50))==1,Native.Error);var after=Native.ssbm_view_positions(chargeView,out vertices);var second=new float[vertices*3];Marshal.Copy(after,second,0,second.Length);bool changed=false;for(int i=0;i<first.Length;i++){Check(float.IsFinite(second[i]),"camera-facing article produced invalid vertices");changed|=Math.Abs(second[i]-first[i])>0.001f;}Check(changed,"source charge core never faced the camera");
            Native.ssbm_view_model(chargeView,out var info);for(uint texture=0;texture<info.texture_count;texture++){Check(Native.ssbm_view_texture(chargeView,texture,out var tex,null,0)==1,Native.Error);Check(tex.wrap_s==2&&tex.wrap_t==2,"source mirrored shot quadrants lost at C# boundary");}
        } finally {Native.ssbm_view_free(chargeView);}
        for(uint part=0;part<8;part++){var view=Native.ssbm_grapple_new(NativeLib.CString(assets),part);Check(view!=IntPtr.Zero,"grapple part: "+Native.Error);Native.ssbm_view_model(view,out var info);Check(info.vertex_count>0,"empty source grapple part");Native.ssbm_view_free(view);}
        foreach(int fps in new[]{30,60,144}) {
            var sim=Native.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,Native.Error);
            try {
                var pad=new Native.Pad {aim=new Native.Vec3(0,0,1),allowed=15};Native.ssbm_sim_step(sim,in host,in pad,1.0/60);
                pad.buttons=128;Native.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;
                bool beam=false,grabWindow=false;
                for(int f=0;f<fps*2;f++) {
                    Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                    uint n=Native.ssbm_sim_hitboxes(sim,null,0);var boxes=new Native.HitboxSample[n];Check(Native.ssbm_sim_hitboxes(sim,boxes,n)==n,"grab virtual joint 0x8B: "+Native.Error);foreach(var hit in boxes)grabWindow|=hit.element==8;
                    uint p=Native.ssbm_sim_grapple_points(sim,null,0);if(p>0){var points=new Native.Vec3[p];Check(Native.ssbm_sim_grapple_points(sim,points,p)==p,Native.Error);beam=true;foreach(var point in points)Check(float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z),"invalid grapple point");}
                    uint g=Native.ssbm_sim_gfx(sim,null,0);var cues=new Native.Event[g];Check(Native.ssbm_sim_gfx(sim,cues,g)==g,"grab GFX: "+Native.Error);foreach(var cue in cues)Check(Native.ssbm_effect_target(cue.kind)!=int.MaxValue,"unresolved grab effect "+cue.kind);
                }
                Check(beam&&grabWindow,"B grab did not run source beam/contact windows");
                Native.ssbm_sim_set_grabbed(sim,1);pad.movement=new Native.Vec3(0,1,0);pad.buttons=2;Native.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;pad.movement=default;
                Native.ssbm_sim_state(sim,out var state);Check(state.move_id==31,"captured LS up + attack did not select up throw");int before=throws;
                for(int f=0;f<fps*2;f++)Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(throws==before+1&&Native.ssbm_sim_grabbed(sim)==0,"source throw release missing/duplicated");
                Native.ssbm_sim_begin_entry(sim,NativeLib.CString(assets));Native.ssbm_sim_entry_control(sim,0);
                for(int f=0;f<fps*3;f++)Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(Native.ssbm_sim_entry_control(sim,2)==1,"figurine activated without Interact");Native.ssbm_sim_entry_control(sim,1);
                for(int f=0;f<fps;f++)Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(Native.ssbm_sim_entry_control(sim,2)==0,"figurine failed to activate after Interact");
            } finally {Native.ssbm_sim_free(sim);}
        }
        var shot=Native.ssbm_projectile_new(NativeLib.CString(assets),1,8,1,new Native.Vec3(0,0.6f,0.8f));Check(shot!=IntPtr.Zero,Native.Error);
        try {Native.ssbm_projectile_lifetime(shot,0);for(int i=0;i<600;i++)Native.ssbm_projectile_step(shot,1.0/60,default,0);Native.ssbm_projectile_state(shot,out var state);Check((state.flags&2)==0&&state.position.z>1000,"RoR2 charge shot still expires at short source range");Check(Math.Abs(state.position.y/state.position.z-0.75f)<0.001f,"charge shot changed aimed pitch");}
        finally {Native.ssbm_projectile_free(shot);}
        Console.WriteLine("PASS: visible Screw Attack, source grapple geometry/contact, directional throw release, Interact-gated figurine at 30/60/144 Hz, and long aimed charge-shot travel");
    }
    static void RebirthRegressions(string assets,in Native.Host host)
    {
        foreach(int fps in new[]{30,60,144}) {
            ulong automatic=0,movement=0;
            foreach(bool move in new[]{false,true}) {
                var sim=Native.ssbm_sim_new(NativeLib.CString(assets),new Native.Vec3(0,-200,0));Check(sim!=IntPtr.Zero,Native.Error);
                var view=Native.ssbm_view_new(NativeLib.CString(assets),0);Check(view!=IntPtr.Zero,Native.Error);
                try {
                    Check(Native.ssbm_sim_begin_rebirth(sim,NativeLib.CString(assets),default)==1,Native.Error);
                    Check(Native.ssbm_sim_begin_rebirth(sim,NativeLib.CString(assets),default)==0,"duplicate falloff restarted KO");
                    var pad=new Native.Pad {aim=new Native.Vec3(0,0,1),allowed=15};int ko=0,transform=0;bool figurine=false,protectedState=false,koPcm=false,flashOn=false,flashOff=false;
                    var pcm=new float[1024];var overlay=new float[4];
                    for(int f=0;f<fps*10;f++) {
                        uint before=Native.ssbm_sim_rebirth_flags(sim);pad.movement=move&&(before&8)!=0?new Native.Vec3(0,0,1):default;
                        Native.ssbm_sim_step(sim,in host,in pad,1.0/fps);uint flags=Native.ssbm_sim_rebirth_flags(sim);
                        uint n=Native.ssbm_sim_gfx(sim,null,0);var cues=new Native.Event[n];Check(Native.ssbm_sim_gfx(sim,cues,n)==n,Native.Error);
                        foreach(var cue in cues){if(cue.kind==0x42b){ko++;Check(Math.Abs(cue.position.y+200)<.001f,"KO cue moved to respawn point");Check(Native.ssbm_effect_target(cue.kind)==-26,"original KO model not dispatched");}if(cue.kind==0x43e)transform++;}
                        Check(Native.ssbm_sim_audio(sim,pcm,512)==512,Native.Error);if((flags&4)!=0)foreach(float v in pcm)koPcm|=Math.Abs(v)>.00001f;
                        if((flags&4)!=0||(flags&8)!=0) {
                            Check(Native.ssbm_view_update(view,sim,1f/fps)==1,Native.Error);var ptr=Native.ssbm_view_positions(view,out uint vertices);var positions=new float[vertices*3];Marshal.Copy(ptr,positions,0,positions.Length);bool visible=false;foreach(float v in positions)visible|=Math.Abs(v)>.001f;
                            Check(visible==((flags&8)!=0),"fighter visibility wrong during KO/figurine");
                        }
                        figurine|=(flags&8)!=0;
                        if(flags==2) {
                            if(!protectedState){Native.ssbm_sim_state(sim,out var state);if(move)movement=state.frame;else automatic=state.frame;Native.ssbm_sim_place(sim,state.position,2);Check(Native.ssbm_sim_rebirth_flags(sim)==2,"collision correction cancelled protection");}
                            protectedState=true;bool visible=Native.ssbm_sim_overlay(sim,overlay)==1;flashOn|=visible;flashOff|=!visible;
                            Native.ssbm_sim_hurt(sim);
                        }
                    }
                    Check(ko==1&&transform==1&&figurine&&protectedState&&koPcm&&flashOn&&flashOff,"missing/duplicate recovery cues, PCM, figurine, protection or flash at "+fps);
                    Check(Native.ssbm_sim_rebirth_flags(sim)==0&&Native.ssbm_sim_entry_control(sim,2)==0,"respawn/protection failed to expire");
                }finally{Native.ssbm_view_free(view);Native.ssbm_sim_free(sim);}
            }
            Check(movement<automatic,"movement did not release the figurine earlier than timeout");
        }
        Console.WriteLine("PASS: common Melee KO model/PCM, hidden KO, visible figurine, movement/timeout release, source flash and expiring protection at 30/60/144 FPS");
    }
}
