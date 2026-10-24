using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Ror2Common;

static class Program
{
    static readonly MeleeNative.CollideFn collide=Collide;
    static readonly MeleeNative.EventFn evt=Event;
    static int jumps, hits,throws,fireFoxImpacts;
    static MeleeNative.Event lastShot;static int shotCount;
    static MeleeNative.Event lastThunder;static int thunderSpawn,thunderStrike;
    static void Collide(IntPtr user,in MeleeNative.Vec3 from,in MeleeNative.Vec3 to,out MeleeNative.Collision result)
    { result=new MeleeNative.Collision { position=new MeleeNative.Vec3(to.x,Math.Max(0,to.y),to.z),grounded=to.y<=0?1u:0u }; }
    static void Event(IntPtr user,in MeleeNative.Event e) { if(e.kind==3) jumps++; if(e.kind==2) hits++;if(e.kind==6)throws++;if(e.kind==2&&e.move_id==20){lastShot=e;shotCount++;} if(e.kind==2&&e.move_id==24){if(e.strength<1.5f){thunderSpawn++;lastThunder=e;}else thunderStrike++;} if(e.kind==2&&e.move_id==23&&e.strength>=2.5f)fireFoxImpacts++; }
    static void Check(bool ok,string what) { if(!ok) throw new Exception(what); }
    static void ScopePitchRegression(string assets,in MeleeNative.Host host)
    {
        var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterLuigi);
        if(sim==IntPtr.Zero) {Console.WriteLine("SKIP: Luigi assets absent for Scope pitch");return;}
        var control=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterLuigi);
        Check(control!=IntPtr.Zero,MeleeNative.Error);
        try {
            if(MeleeNative.ssbm_sim_scope(sim,1,0)!=1) {Console.WriteLine("SKIP: Scope assets absent");return;}
            Check(MeleeNative.ssbm_sim_scope(control,1,0)==1,"control Scope unavailable");
            var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31,buttons=256};
            var flat=pad;
            for(int i=0;i<30;i++) {
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                MeleeNative.ssbm_sim_step(control,in host,in flat,1.0/60);
            }
            MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==17,"Scope charge did not begin");
            var hip=new float[16];var hand=new float[16];var next=new float[16];
            foreach(float y in new[]{0.6f,-0.6f}) {
                pad.aim=new MeleeNative.Vec3(0,y,0.8f);
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                MeleeNative.ssbm_sim_step(control,in host,in flat,1.0/60);
                Check(MeleeNative.ssbm_sim_common_bone(control,4,hip)==1,"Scope hip missing");
                Check(MeleeNative.ssbm_sim_common_bone(control,49,hand)==1,"Scope hand missing");
                Check(MeleeNative.ssbm_sim_common_bone(sim,4,next)==1,"Scope hip missing");
                for(int i=0;i<16;i++) Check(Math.Abs(next[i]-hip[i])<0.001,"Scope aim moved hips/legs");
                Check(MeleeNative.ssbm_sim_common_bone(sim,49,next)==1,"Scope hand missing");
                Check(Math.Abs(next[13]-hand[13])>0.1,"Scope hand did not follow camera elevation");
            }
            pad.buttons=256|1;
            for(int i=0;i<12;i++) MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            MeleeNative.ssbm_sim_state(sim,out state);
            Check(state.move_id==17&&state.grounded==0&&state.position.y>0,"Scope jump cancelled charge or failed to leave ground");
            Console.WriteLine("PASS: Scope camera pitch moves the held-item bone while hips stay planted; jumping preserves charge");
        } finally {MeleeNative.ssbm_sim_free(sim);MeleeNative.ssbm_sim_free(control);}
    }
    // Multiplayer: another machine replays an owner's recorded calls on a shadow sim and ends each batch where
    // the owner is, in the same action, for every fighter; a corrupt log is refused.
    static void ReplayRegressions(string assets,in MeleeNative.Host host)
    {
        if(!MeleeNative.HasReplay) {Console.WriteLine("SKIP: this ssbm.dll has no multiplayer replay");return;}
        uint[] kinds={MeleeNative.FighterSamus,MeleeNative.FighterFox,MeleeNative.FighterLuigi,MeleeNative.FighterPikachu,MeleeNative.FighterCaptain,MeleeNative.FighterLink,MeleeNative.FighterYoshi,MeleeNative.FighterDonkey};
        var tuning=new MeleeNative.Tuning {speed_scale=1.37f,jump_scale=1f,smash_max_frames=60};
        var buffer=new byte[1<<14];
        foreach(uint kind in kinds) {
            var owner=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,kind);
            if(owner==IntPtr.Zero) continue;
            var shadow=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,kind);
            Check(shadow!=IntPtr.Zero,MeleeNative.Error);
            try {
                Check(MeleeNative.ssbm_sim_record(owner,1)==1,"recording did not start");
                for(int batch=0;batch<200;batch++) {
                    for(int i=0;i<3;i++) {
                        int t=batch*3+i;
                        var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(1,0,0),allowed=31};
                        if(t>=30&&t<90) pad.movement=new MeleeNative.Vec3(1,0,0);
                        if(t==90||t==91) pad.buttons=1;
                        if(t==152||t==158||t==164) pad.buttons=2;
                        if(t==260) pad.buttons=4;
                        MeleeNative.ssbm_sim_tuning(owner,tuning);
                        MeleeNative.ssbm_sim_facing(owner,new MeleeNative.Vec3(1,0,0));
                        if(t==300) MeleeNative.ssbm_sim_hurt_scaled(owner,0.3f);
                        MeleeNative.ssbm_sim_step(owner,in host,in pad,1.0/60);
                    }
                    uint length=MeleeNative.ssbm_sim_ops_take(owner,null,0);
                    Check(length>0&&length<=buffer.Length,"recorded log size "+length);
                    Check(MeleeNative.ssbm_sim_ops_take(owner,buffer,(uint)buffer.Length)==length,"taking the log failed");
                    var bytes=new byte[length];Array.Copy(buffer,bytes,length);
                    Check(MeleeNative.ssbm_sim_replay(shadow,in host,bytes,length)!=uint.MaxValue,"replay refused a good log: "+MeleeNative.Error);
                    MeleeNative.ssbm_sim_state(owner,out var a);MeleeNative.ssbm_sim_state(shadow,out var b);
                    float dx=a.position.x-b.position.x,dy=a.position.y-b.position.y,dz=a.position.z-b.position.z;
                    Check(Math.Sqrt(dx*dx+dy*dy+dz*dz)<1.5&&a.move_id==b.move_id&&a.frame==b.frame&&a.grounded==b.grounded,
                        "fighter "+kind+" shadow diverged at batch "+batch);
                }
                MeleeNative.ssbm_sim_state(owner,out var end);Check(end.position.x>5,"fighter "+kind+" never walked");
                Check(MeleeNative.ssbm_sim_replay(shadow,in host,new byte[]{200,1,2,3},4)==uint.MaxValue,"replay accepted a corrupt log");
            } finally {MeleeNative.ssbm_sim_free(owner);MeleeNative.ssbm_sim_free(shadow);}
        }
        Console.WriteLine("PASS: multiplayer replay keeps a shadow sim on the owner's position, action and frame for every fighter; corrupt logs are refused");
    }
    // The pause sheet words its Attacks and Grab rows by the sim's own move list: every fighter reports its
    // common moves and four specials, and the roster differs per fighter the way the disc's action tables do
    // (Fox has a rapid jab, Pikachu has no second jab), so the jab row names only what the fighter has.
    static void MovesetChecks(string assets)
    {
        if(!MeleeNative.HasMoves) {Console.WriteLine("SKIP: this ssbm.dll has no move-list export");return;}
        uint[] kinds={MeleeNative.FighterSamus,MeleeNative.FighterFox,MeleeNative.FighterLuigi,MeleeNative.FighterPikachu,MeleeNative.FighterCaptain,MeleeNative.FighterLink,MeleeNative.FighterYoshi,MeleeNative.FighterDonkey};
        var lists=new System.Collections.Generic.Dictionary<uint,System.Collections.Generic.HashSet<uint>>();
        var all=new System.Collections.Generic.Dictionary<uint,MeleeMoveInfo[]>();
        foreach(uint kind in kinds) {
            var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,kind);
            if(sim==IntPtr.Zero) {Console.WriteLine("SKIP: moveset check for fighter "+kind+" ("+MeleeNative.Error+")");continue;}
            try {
                var moves=MeleeMoveset.Read(sim);Check(moves!=null&&moves.Length>=20,"fighter "+kind+" reported no move list");
                var ids=new System.Collections.Generic.HashSet<uint>();
                foreach(var move in moves) {
                    Check(ids.Add(move.Id),"fighter "+kind+" lists move "+move.Id+" twice");
                    Check(move.Source.Length>0,"move "+move.Id+" has no source name");
                }
                foreach(uint id in new[]{MeleeMoveset.Jab,MeleeMoveset.SideTilt,MeleeMoveset.UpTilt,MeleeMoveset.DownTilt,MeleeMoveset.DashAttack,MeleeMoveset.SideSmash,MeleeMoveset.UpSmash,
                    MeleeMoveset.DownSmash,MeleeMoveset.NeutralAir,MeleeMoveset.ForwardAir,MeleeMoveset.BackAir,MeleeMoveset.UpAir,MeleeMoveset.DownAir,MeleeMoveset.Grab,MeleeMoveset.Pummel,
                    MeleeMoveset.ThrowForward,MeleeMoveset.ThrowBack,MeleeMoveset.ThrowUp,MeleeMoveset.ThrowDown,MeleeMoveset.SpecialN,MeleeMoveset.SpecialHi,MeleeMoveset.SpecialLw})
                    Check(ids.Contains(id),"fighter "+kind+" lacks move "+id);
                lists[kind]=ids;all[kind]=moves;
            } finally {MeleeNative.ssbm_sim_free(sim);}
        }
        if(lists.Count==kinds.Length) {
            Check(lists[MeleeNative.FighterFox].Contains(MeleeMoveset.RapidJab),"Fox lost its rapid jab");
            Check(!lists[MeleeNative.FighterPikachu].Contains(MeleeMoveset.Jab2),"Pikachu gained a second jab");
            Check(!lists[MeleeNative.FighterSamus].Contains(MeleeMoveset.RapidJab),"Samus gained a rapid jab");
            Check(lists[MeleeNative.FighterLuigi].Contains(MeleeMoveset.Jab3),"Luigi lost his third jab");
            Check(MeleeMoveset.JabText(all[MeleeNative.FighterFox]).Contains("rapid jab"),"Fox's jab row lost its rapid jab");
            Check(!MeleeMoveset.JabText(all[MeleeNative.FighterSamus]).Contains("rapid"),"Samus's jab row names a rapid jab");
            Check(MeleeMoveset.JabText(null)=="Jab combo","the jab row without a move list changed");
        }
        Console.WriteLine("PASS: the move list each fighter's sim reports (common moves and specials) words the pause sheet's jab row per fighter and differs per fighter");
    }
    // Fox Illusion's blue visuals are two source pieces: efSync 0x48D, a generator attached to
    // TopN (Party/Effect dispatch), and the It_Kind_Fox_Illusion ghost article, a translucent
    // blue copy of Fox's body laid at the positions he just left.
    static void FoxIllusionRegressions(string assets,in MeleeNative.Host host)
    {
        Check(MeleeNative.ssbm_effect_target(1165)==3008,"Fox Illusion streak target changed: "+MeleeNative.Error);
        Check((MeleeNative.ssbm_effect_flags(1165)&1)!=0,"Fox Illusion streak is no longer joint-bound");
        var ghost=MeleeNative.ssbm_article_new(NativeLib.CString(assets),MeleeNative.FighterFox,2,0);
        if(ghost!=IntPtr.Zero) {
            try {
                Check(MeleeNative.ssbm_view_model(ghost,out var model)==1&&model.vertex_count>6000,"Fox Illusion ghost model is empty");
                var colors=MeleeNative.ssbm_view_colors(ghost,out uint count);
                Check(colors!=IntPtr.Zero&&count>0,"Fox Illusion ghost has no source colours");
                var rgba=new float[count*4];Marshal.Copy(colors,rgba,0,rgba.Length);
                Check(rgba.Length>=4&&rgba[3]<1f&&rgba[2]>rgba[0],"Fox Illusion ghost is not a faded blue body");
            } finally {MeleeNative.ssbm_view_free(ghost);}
        } else Console.WriteLine("SKIP: Fox Illusion ghost article ("+MeleeNative.Error+")");
        var bolt=MeleeNative.ssbm_article_new(NativeLib.CString(assets),MeleeNative.FighterFox,0,0);
        if(bolt!=IntPtr.Zero) {
            try {
                var colors=MeleeNative.ssbm_view_colors(bolt,out uint count);
                Check(colors!=IntPtr.Zero&&count>0,"Blaster bolt has no source colours");
                var rgba=new float[count*4];Marshal.Copy(colors,rgba,0,rgba.Length);
                Check(rgba.Length>=4&&rgba[0]>0.9f&&rgba[1]<0.1f&&rgba[2]<0.1f,"Blaster bolt is not red");
            } finally {MeleeNative.ssbm_view_free(bolt);}
        } else Console.WriteLine("SKIP: Blaster bolt article ("+MeleeNative.Error+")");
        var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterFox);
        if(sim==IntPtr.Zero) {Console.WriteLine("SKIP: Fox Illusion cue ("+MeleeNative.Error+")");return;}
        try {
            var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(1,0,0),allowed=31};
            for(int i=0;i<4;i++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            pad.buttons=512; // SIDE_B
            bool streak=false;
            for(int i=0;i<40;i++) {
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                uint n=MeleeNative.ssbm_sim_gfx(sim,null,0);
                if(n==0)continue;
                var cues=new MeleeNative.Event[n];
                Check(MeleeNative.ssbm_sim_gfx(sim,cues,n)==n,"Fox Illusion GFX: "+MeleeNative.Error);
                foreach(var cue in cues)if(cue.kind==1165){streak=true;Check(cue.move_id==0,"Illusion streak is not on TopN");}
            }
            Check(streak,"Fox Illusion never spawned its 0x48D blue streak");
        } finally {MeleeNative.ssbm_sim_free(sim);}
        Console.WriteLine("PASS: Fox Illusion's blue streak is joint-bound and its ghost article is a faded blue body through C#");
    }
    // A host terrain correction may move the fighter without resetting its action.
    static void NudgeRegression(string assets)
    {
        var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterFox);
        if(sim==IntPtr.Zero) {Console.WriteLine("SKIP: nudge ("+MeleeNative.Error+")");return;}
        try {
            MeleeNative.ssbm_sim_state(sim,out var before);
            Check(MeleeNative.HasNudge,"this ssbm.dll has no ssbm_sim_nudge");
            MeleeNative.ssbm_sim_nudge(sim,new MeleeNative.Vec3(0,10,0),1);
            MeleeNative.ssbm_sim_state(sim,out var after);
            Check(Math.Abs(after.position.y-10)<.001f&&after.move_id==before.move_id&&after.frame==before.frame,
                "nudge changed the action or did not move the fighter");
        } finally {MeleeNative.ssbm_sim_free(sim);}
        Console.WriteLine("PASS: ssbm_sim_nudge moves a live fighter without resetting its action");
    }
    // Fire Fox ends with one host burst event (move 23, strength 3) at the end of its travel.
    static void FireFoxImpactRegressions(string assets,in MeleeNative.Host host)
    {
        var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterFox);
        if(sim==IntPtr.Zero) {Console.WriteLine("SKIP: Fire Fox impact ("+MeleeNative.Error+")");return;}
        fireFoxImpacts=0;
        try {
            var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(1,0.4f,0),allowed=31};
            for(int i=0;i<4;i++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            pad.buttons=1024; // UP_B
            for(int i=0;i<30;i++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            pad.buttons=0;
            for(int i=0;i<240;i++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            Check(fireFoxImpacts==1,"Fire Fox emitted "+fireFoxImpacts+" impact bursts");
        } finally {MeleeNative.ssbm_sim_free(sim);}
        Console.WriteLine("PASS: Fire Fox emits one end-of-travel impact burst through C#");
    }
    static int Main(string[] args)
    {
        try {Run(args);return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Run(string[] args)
    {
        string dll=Path.GetFullPath(args.Length>0?args[0]:"../../libssbm/target/release/ssbm.dll");
        string assets=Path.GetFullPath(args.Length>1?args[1]:"../../libssbm/assets");
        MeleeNative.Load(dll);
        if(args.Length>2) {
            // Exercise the release route: C# worker -> P/Invoke -> native DLL,
            // starting without a cache. No extractor EXE or shell script is used.
            Check(!Directory.Exists(assets),"first-run check requires a fresh cache path");
            string disc=Path.GetFullPath(args[2]),failure=null;
            var worker=new Thread(()=> {
                try {
                    if(MeleeNative.ssbm_extract(NativeLib.CString(disc),NativeLib.CString(assets),MeleeNative.FighterSamus)!=1) failure=MeleeNative.Error;
                    else if(MeleeNative.ssbm_assets_complete(NativeLib.CString(assets),MeleeNative.FighterSamus)!=1) failure="DLL extraction left an incomplete cache";
                }catch(Exception e){failure=e.ToString();}
            }) { IsBackground=true,Name="Samus first-run check" };
            worker.Start();Check(worker.Join(30000),"first-run extraction timed out");Check(failure==null,failure);
            Console.WriteLine("PASS: first-run extraction on a C# worker through ssbm.dll, with no external tools");
        }
        if(MeleeNative.ssbm_assets_complete(NativeLib.CString(assets),MeleeNative.FighterSamus)!=1) {
            // Clean release builds, like Mario/Olimar, need neither a disc nor a cache.
            Console.WriteLine("PASS: ABI 5 + 14 layouts; SKIP: asset-dependent checks (no complete local cache)");
            return;
        }
        var host=new MeleeNative.Host { collide=Marshal.GetFunctionPointerForDelegate(collide),evt=Marshal.GetFunctionPointerForDelegate(evt) };
        PresentationRegressions(assets,in host);
        GameCueRegressions(assets);
        MobileChargeRegressions(assets,in host);
        RebirthRegressions(assets,in host);
        AimingRegressions(assets,in host);
        ScopePitchRegression(assets,in host);
        MorphBallRegressions(assets,in host);
        DefenseRegressions(assets,in host);
        PikachuRegressions(assets,in host);
        ReplayRegressions(assets,in host);
        MovesetChecks(assets);
        FoxIllusionRegressions(assets,in host);
        NudgeRegression(assets);
        FireFoxImpactRegressions(assets,in host);
        jumps=hits=throws=0;
        foreach(int fps in new[]{30,60,144}) {
            var dash=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(dash!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),movement=new MeleeNative.Vec3(0,0,1),allowed=31,buttons=32};
                for(int f=0;f<60;f++)MeleeNative.ssbm_sim_step(dash,in host,in pad,1.0/60);
                MeleeNative.ssbm_sim_state(dash,out var before);pad.buttons|=2;
                MeleeNative.ssbm_sim_step(dash,in host,in pad,1.0/60);
                MeleeNative.ssbm_sim_state(dash,out var state);Check(state.move_id==5,"sprinting LT did not select Dash Attack");
                pad.buttons=0;pad.movement=default;float furthest=state.position.z;
                for(int f=0;f<fps*2;f++) {
                    MeleeNative.ssbm_sim_step(dash,in host,in pad,1.0/fps);MeleeNative.ssbm_sim_state(dash,out state);
                    Check(state.position.z>=furthest-0.001f,"dash rubberbanded at "+fps+" FPS");furthest=Math.Max(furthest,state.position.z);
                }
                Check(state.move_id==0&&state.position.z-before.position.z>10,"dash did not retain source movement after returning to idle");
            }finally{MeleeNative.ssbm_sim_free(dash);}
        }
        Console.WriteLine("PASS: sprint + LT retains Dash Attack travel through the native boundary at 30/60/144 FPS");
        for(uint c=0;c<5;c++) {
            Check(MeleeNative.ssbm_portrait(NativeLib.CString(assets),MeleeNative.FighterSamus,c,out var portrait,null,0)==1,MeleeNative.Error);
            var rgba=new byte[portrait.width*portrait.height*4];
            Check(MeleeNative.ssbm_portrait(NativeLib.CString(assets),MeleeNative.FighterSamus,c,out portrait,rgba,(uint)rgba.Length)==1,MeleeNative.Error);
            Check(portrait.width==64&&portrait.height==56,"expected CSS grid photo, not selected-character photo");
            Check(MeleeNative.ssbm_portrait(NativeLib.CString(assets),MeleeNative.FighterSamus,c,out portrait,new byte[1],1)==0,"portrait accepted short output buffer");
        }
        var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
        IntPtr view=IntPtr.Zero;
        try {
            var platform=MeleeNative.ssbm_entry_platform_new(NativeLib.CString(assets));Check(platform!=IntPtr.Zero,"entry platform: "+MeleeNative.Error);
            try {
                Check(MeleeNative.ssbm_sim_begin_entry(sim,NativeLib.CString(assets))==1,"entry start: "+MeleeNative.Error);
                if(MeleeNative.HasOverlay) {
                    var tint=new float[4];Check(MeleeNative.ssbm_sim_overlay(sim,tint)==1&&tint[3]>0,"source entry overlay absent");
                    foreach(float channel in tint)Check(float.IsFinite(channel)&&channel>=0&&channel<=1,"entry overlay channel outside bounds");
                }
                bool entrySound=false;var entryPcm=new float[1066];
                var entryPad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31,buttons=2|4};
                for(int f=0;f<60;f++) {
                    MeleeNative.ssbm_sim_step(sim,in host,in entryPad,1.0/60);
                    MeleeNative.ssbm_sim_state(sim,out var entryState);Check(entryState.move_id==0,"entry allowed an attack");
                    if(f<59)Check(MeleeNative.ssbm_entry_platform_update(platform,sim)==1,"entry platform stopped early");
                    Check(MeleeNative.ssbm_sim_audio(sim,entryPcm,533)==533,MeleeNative.Error);
                    foreach(float v in entryPcm)if(Math.Abs(v)>0.00001f)entrySound=true;
                }
                Check(entrySound,"entry source sound missing");Check(MeleeNative.ssbm_entry_platform_update(platform,sim)==0,"entry did not finish");
            } finally {MeleeNative.ssbm_view_free(platform);}
            var pad=new MeleeNative.Pad { aim=new MeleeNative.Vec3(1,0,0),allowed=31 };
            MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            pad.buttons=1;for(int i=0;i<6;i++) MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            Check(jumps==1,"jump callback/layout failed");
            pad.buttons=0;for(int i=0;i<180;i++) MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
            MeleeNative.ssbm_sim_state(sim,out var state);Check(state.grounded==1&&state.jumps_used==0,"landing state mismatch");
            bool gotHitboxes=false,gotSound=false;
            pad.buttons=2;for(int i=0;i<10;i++) {
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                var pcm=new float[1066];
                Check(MeleeNative.ssbm_sim_audio(sim,pcm,533)==533,"native PCM render failed: "+MeleeNative.Error);
                foreach(float x in pcm){Check(float.IsFinite(x),"nonfinite PCM");if(Math.Abs(x)>0.0001f)gotSound=true;}
                uint count=MeleeNative.ssbm_sim_hitboxes(sim,null,0);
                if(count==0)continue;
                var samples=new MeleeNative.HitboxSample[count];
                Check(MeleeNative.ssbm_sim_hitboxes(sim,samples,count)==count,"hitbox transfer failed");
                foreach(var hit in samples)Check(hit.radius>0&&hit.damage>0&&float.IsFinite(hit.local_position.x)&&float.IsFinite(hit.local_position.y)&&float.IsFinite(hit.local_position.z),"invalid posed hitbox");
                gotHitboxes=true;
            }
            Check(gotHitboxes&&hits==0,"disc melee hitboxes must replace the placeholder hit callback");
            Check(gotSound,"disc attack sound cue did not reach the native PCM mixer");
            var particles=MeleeNative.ssbm_particles_new(NativeLib.CString(assets));Check(particles!=IntPtr.Zero,MeleeNative.Error);
            try {
                var identity=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
                for(uint id=2000;id<2022;id++) {
                    Check(MeleeNative.ssbm_particles_spawn(particles,id,identity,1)==1,MeleeNative.Error);
                    bool drawn=false;
                    for(int frame=0;frame<30;frame++) {
                        Check(MeleeNative.ssbm_particles_step(particles,1.0/60)==1,MeleeNative.Error);
                        uint n=MeleeNative.ssbm_particles_draws(particles,null,0);if(n==0)continue;
                        var sprites=new ViewParticle[n];Check(MeleeNative.ssbm_particles_draws(particles,sprites,n)==n,MeleeNative.Error);
                        foreach(var sprite in sprites) {
                            Check(float.IsFinite(sprite.position.x)&&float.IsFinite(sprite.size)&&float.IsFinite(sprite.r),"invalid particle snapshot");
                            Check(MeleeNative.ssbm_particles_texture(particles,sprite.texture,out var info,null,0)==1,MeleeNative.Error);
                            var rgba=new byte[info.width*info.height*4];
                            Check(MeleeNative.ssbm_particles_texture(particles,sprite.texture,out info,rgba,(uint)rgba.Length)==1,MeleeNative.Error);
                        }
                        drawn=true;
                    }
                    Check(drawn,"emitter never produced a billboard: "+id);MeleeNative.ssbm_particles_stop(particles,1);
                }
            } finally { MeleeNative.ssbm_particles_free(particles); }
            for(uint kind=0;kind<4;kind++)for(uint articleState=0;articleState<(kind==0?2:kind==1?9:kind==2?4:1);articleState++) {
                var article=MeleeNative.ssbm_article_new(NativeLib.CString(assets),MeleeNative.FighterSamus,kind,articleState);Check(article!=IntPtr.Zero,"article: "+MeleeNative.Error);
                try { Check(MeleeNative.ssbm_object_update(article,10)==1,MeleeNative.Error);MeleeNative.ssbm_view_model(article,out var model);Check(model.vertex_count>0&&model.index_count>0,"empty article model"); }
                finally { MeleeNative.ssbm_view_free(article); }
            }
            foreach(var articleCase in new[]{(kind:0u,state:0u),(kind:1u,state:1u),(kind:1u,state:8u),(kind:2u,state:0u),(kind:2u,state:1u)}) {
                MeleeNative.ProjectileState? reference=null;
                foreach(int fps in new[]{30,60,144}) {
                    var projectile=MeleeNative.ssbm_projectile_new(NativeLib.CString(assets),MeleeNative.FighterSamus,articleCase.kind,articleCase.state,articleCase.state==8?1:0,new MeleeNative.Vec3(0,0,1));
                    Check(projectile!=IntPtr.Zero,"projectile create: "+MeleeNative.Error);
                    try {
                        for(int f=0;f<fps;f++)MeleeNative.ssbm_projectile_step(projectile,1.0/fps,default,0);
                        Check(MeleeNative.ssbm_projectile_state(projectile,out var flight)==1,MeleeNative.Error);
                        Check(flight.frame==60&&flight.damage>0&&flight.radius>0,"disc projectile clock/hitbox");
                        if(reference.HasValue)Check(Math.Abs(reference.Value.position.z-flight.position.z)<0.0001f&&Math.Abs(reference.Value.position.y-flight.position.y)<0.0001f,"projectile changed with host rate");
                        reference=flight;
                        uint cue=MeleeNative.ssbm_projectile_impact(projectile,sim);
                        Check(cue==1001||cue==1036||cue==1038,"source impact callback absent");
                        Check(MeleeNative.ssbm_projectile_impact(projectile,sim)==0,"impact replayed");
                        Check(MeleeNative.ssbm_projectile_state(projectile,out var explosion)==1,MeleeNative.Error);
                        Check((explosion.flags&1)!=0,"impact state missing");
                        if(articleCase.kind!=1)Check(explosion.state==(articleCase.kind==0?1:articleCase.state+2),"wrong source explosion animation");
                        for(int f=0;f<fps;f++)MeleeNative.ssbm_projectile_step(projectile,1.0/fps,default,0);
                        Check(MeleeNative.ssbm_projectile_state(projectile,out explosion)==1&&(explosion.flags&2)!=0,"source explosion never finished");
                    } finally {MeleeNative.ssbm_projectile_free(projectile);}
                }
            }
            Console.WriteLine("PASS: disc projectile speeds, hitboxes, fixed clocks and explosion states at 30/60/144 Hz");
            for(uint costume=0;costume<5;costume++) {
                view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,costume);Check(view!=IntPtr.Zero,MeleeNative.Error);
                Check(MeleeNative.ssbm_view_update(view,sim,1f/60)==1,MeleeNative.Error);
                Check(MeleeNative.ssbm_view_pose(view,NativeLib.CString("Wait"),0)==1,"direct idle portrait pose failed");
                var idlePtr=MeleeNative.ssbm_view_positions(view,out uint idleCount);var idleVertices=new float[idleCount*3];Marshal.Copy(idlePtr,idleVertices,0,idleVertices.Length);
                Check(MeleeNative.ssbm_view_pose(view,NativeLib.CString("SpecialHi"),15)==1,"up-special test pose failed");
                Check(MeleeNative.ssbm_view_pose(view,NativeLib.CString("Wait"),0)==1,"return to idle failed");
                var restoredPtr=MeleeNative.ssbm_view_positions(view,out uint restoredCount);var restored=new float[restoredCount*3];Marshal.Copy(restoredPtr,restored,0,restored.Length);
                Check(idleCount==restoredCount,"portrait vertex count changed");
                for(int vertex=0;vertex<restored.Length;vertex++)Check(idleVertices[vertex]==restored[vertex],"portrait retained attack/landing pose");
                Check(MeleeNative.ssbm_view_pose(view,NativeLib.CString("missing-clip"),0)==0,"unknown portrait clip accepted");
                MeleeNative.ssbm_view_model(view,out var model);Check(model.vertex_count>0&&model.batch_count>0,"empty model");
                var uvs=new float[model.vertex_count*2];Array.Fill(uvs,float.NaN);
                Check(MeleeNative.ssbm_view_uvs(view,uvs,model.vertex_count)==model.vertex_count,"UV export must count pairs");
                foreach(float coordinate in uvs)Check(float.IsFinite(coordinate),"UV export left half the source texture coordinates unwritten");
                var pos=MeleeNative.ssbm_view_positions(view,out uint n);Check(n==model.vertex_count&&pos!=IntPtr.Zero,"position pointer/count");
                var indices=new int[model.index_count];MeleeNative.ssbm_view_indices(view,indices,(uint)indices.Length);
                foreach(int i in indices) Check(i>=0&&i<model.vertex_count,"index outside vertices");
                var batches=new ViewBatch[model.batch_count];MeleeNative.ssbm_view_batches(view,batches,(uint)batches.Length);
                foreach(var b in batches) if(b.texture>=0) {
                    Check(MeleeNative.ssbm_view_texture(view,(uint)b.texture,out var tex,null,0)==1,"texture info failed");
                    var pixels=new byte[tex.width*tex.height*4];Check(MeleeNative.ssbm_view_texture(view,(uint)b.texture,out tex,pixels,(uint)pixels.Length)==1,"texture pixels failed");
                }
                MeleeNative.ssbm_view_free(view);view=IntPtr.Zero;
            }
            Check(MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,99)==IntPtr.Zero,"bad costume not rejected");
            // Alternate-DObj views (Yoshi's egg, Samus's morph ball) do not need a sim.
            var variant=MeleeNative.ssbm_view_new_choice(NativeLib.CString(assets),MeleeNative.FighterSamus,0,1);Check(variant!=IntPtr.Zero,"alternate model view: "+MeleeNative.Error);
            try {
                MeleeNative.ssbm_view_model(variant,out var variantModel);Check(variantModel.vertex_count>0&&variantModel.batch_count>0,"alternate model view is empty");
            } finally { MeleeNative.ssbm_view_free(variant); }
            // Samus character select uses one native down-B and its sound cues.
            // Different host rates must accept zero/multiple ticks per call.
            foreach(int fps in new[]{30,60,144}) {
                var select=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(select!=IntPtr.Zero,MeleeNative.Error);
                try {
                    var selectPad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=0};
                    MeleeNative.ssbm_sim_step(select,in host,in selectPad,1.0/60);
                    selectPad.buttons=4|16;selectPad.allowed=2;
                    MeleeNative.ssbm_sim_step(select,in host,in selectPad,1.0/60);
                    MeleeNative.ssbm_sim_state(select,out var selectState);Check(selectState.move_id==24,"selection did not start down-B");
                    selectPad.buttons=0;selectPad.allowed=0;
                    bool audible=false,completed=false;
                    var pcm=new float[2048];
                    for(int frame=0;frame<fps*4;frame++) {
                        MeleeNative.ssbm_sim_step(select,in host,in selectPad,1.0/fps);
                        Check(MeleeNative.ssbm_sim_audio(select,pcm,1024)==1024,MeleeNative.Error);
                        foreach(float sample in pcm){Check(float.IsFinite(sample),"selection PCM nonfinite");if(Math.Abs(sample)>0.00001f)audible=true;}
                        MeleeNative.ssbm_sim_state(select,out selectState);if(selectState.move_id!=24)completed=true;
                    }
                    Check(audible&&completed&&Math.Abs(selectState.position.y)<0.1f,"down-B selection sound/completion/floor failed at "+fps+" Hz");
                } finally {MeleeNative.ssbm_sim_free(select);}
            }
            Console.WriteLine("PASS: character-select down-B and source PCM at 30/60/144 Hz");
            // Reproduce the user's delayed failure through the exact C# bindings,
            // consuming GFX after each step rather than merely decoding archives.
            var longRun=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);
            var longParticles=MeleeNative.ssbm_particles_new(NativeLib.CString(assets));
            var effectViews=new System.Collections.Generic.Dictionary<int,IntPtr>();
            Check(longRun!=IntPtr.Zero&&longParticles!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pcm=new float[1066];var matrix=new float[16];int gfxCount=0,chargeFrames=0;
                for(int frame=0;frame<3600;frame++) {
                    int f=frame%600;
                    var p=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31,
                        buttons=f==1?4u|8u:f==150||f==280?4u:f==350?4u|16u:f==450?2u|8u:f==500?2u:0u};
                    MeleeNative.ssbm_sim_step(longRun,in host,in p,1.0/60);
                    Check(MeleeNative.ssbm_sim_audio(longRun,pcm,533)==533,MeleeNative.Error);
                    if(MeleeNative.ssbm_sim_charge_visual(longRun,matrix)==1){foreach(float value in matrix)Check(float.IsFinite(value),"charge attachment nonfinite");chargeFrames++;}
                    uint count=MeleeNative.ssbm_sim_gfx(longRun,null,0);
                    var cues=new MeleeNative.Event[count];
                    Check(MeleeNative.ssbm_sim_gfx(longRun,cues,count)==count,"long-run GFX: "+MeleeNative.Error);
                    foreach(var cue in cues) {
                        gfxCount++;int target=MeleeNative.ssbm_effect_target(cue.kind);
                        Check(target!=int.MaxValue,"unresolved runtime effect "+cue.kind);
                        Check(MeleeNative.ssbm_sim_bone(longRun,cue.move_id&65535,matrix)==1,"posed effect bone: "+MeleeNative.Error);
                        if(target>=0)Check(MeleeNative.ssbm_particles_spawn(longParticles,(uint)target,matrix,(ulong)gfxCount)==1,MeleeNative.Error);
                        else if(target>int.MinValue+1000) {
                            if(!effectViews.TryGetValue(target,out var effect)) {
                                effect=MeleeNative.ssbm_effect_new(NativeLib.CString(assets),MeleeNative.FighterSamus,(uint)(-target-1));
                                Check(effect!=IntPtr.Zero,"runtime effect model: "+MeleeNative.Error);effectViews.Add(target,effect);
                            }
                            Check(MeleeNative.ssbm_object_update(effect,10)==1,MeleeNative.Error);
                            Check(MeleeNative.ssbm_view_positions(effect,out uint vertices)!=IntPtr.Zero&&vertices>0,"empty effect geometry");
                        }
                    }
                    Check(MeleeNative.ssbm_particles_step(longParticles,1.0/60)==1,"runtime particles: "+MeleeNative.Error);
                    Check(MeleeNative.ssbm_sim_state(longRun,out var running)==1&&float.IsFinite(running.position.y),"simulation failed after "+frame+" ticks");
                }
                Check(gfxCount>100&&chargeFrames>100,"long-run did not exercise attack/held-article cues");
                Console.WriteLine("PASS: 60 simulated seconds consuming "+gfxCount+" attack GFX cues, posed bones, effect models, particles and PCM through C#");
            } finally {
                foreach(var effect in effectViews.Values)MeleeNative.ssbm_view_free(effect);
                MeleeNative.ssbm_particles_free(longParticles);MeleeNative.ssbm_sim_free(longRun);
            }
            Console.WriteLine("PASS: ABI 5 + 14 layouts, trophy entry, grid tile, callbacks, jump/landing, posed hitboxes, disc attack PCM, all 22 Samus emitters, 16 article states, 5 costumes and texture transfers");
        } finally { if(view!=IntPtr.Zero)MeleeNative.ssbm_view_free(view);MeleeNative.ssbm_sim_free(sim); }
    }
    static void GameCueRegressions(string assets)
    {
        var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
        try {
            Check(MeleeNative.ssbm_sim_play_sound(sim,0x7c855)==1,"source GAME cue missing: "+MeleeNative.Error);
            var pcm=new float[32000*2];Check(MeleeNative.ssbm_sim_audio(sim,pcm,32000)==32000,MeleeNative.Error);
            Check(Array.Exists(pcm,x=>Math.Abs(x)>0.0001f),"GAME cue decoded silence");
            var view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,0);Check(view!=IntPtr.Zero,MeleeNative.Error);
            try{Check(MeleeNative.ssbm_view_pose(view,NativeLib.CString("DownBoundU"),12)==1,"death fall pose unavailable: "+MeleeNative.Error);}
            finally{MeleeNative.ssbm_view_free(view);}
        }finally{MeleeNative.ssbm_sim_free(sim);}
        var impact=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(impact!=IntPtr.Zero,MeleeNative.Error);
        try {
            Check(MeleeNative.ssbm_sim_play_sound(impact,0x74)==1,"common item-explosion cue missing: "+MeleeNative.Error);
            var pcm=new float[32000*2];Check(MeleeNative.ssbm_sim_audio(impact,pcm,32000)==32000,MeleeNative.Error);
            Check(Array.Exists(pcm,x=>Math.Abs(x)>0.0001f),"impact cue decoded silence");
        } finally {MeleeNative.ssbm_sim_free(impact);}
        Console.WriteLine("PASS: extracted Melee GAME voice, common impact cue and grounded death fall pose through C#");
    }
    static void MobileChargeRegressions(string assets,in MeleeNative.Host host)
    {
        foreach(int fps in new[]{30,60,144}) {
            var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
            var view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,0);Check(view!=IntPtr.Zero,MeleeNative.Error);
            try {
                Check(MeleeNative.ssbm_sim_mobile_moves(sim,1UL<<20)==1,"mobile Charge Shot capability missing");
                var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(1,0,0),allowed=31};
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                pad.movement=new MeleeNative.Vec3(1,0,0);pad.buttons=256;
                for(int f=0;f<fps*3/4;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                MeleeNative.ssbm_sim_state(sim,out var moving);
                Check(moving.move_id==20&&moving.phase==4&&moving.position.x>1&&moving.shot_charge>0,"Charge Shot stopped walking or charging at "+fps+" FPS");
                Check(MeleeNative.ssbm_view_update(view,sim,1f/fps)==1,"mobile pose: "+MeleeNative.Error);
                var cannon=new float[16];Check(MeleeNative.ssbm_sim_bone(sim,50,cannon)==1,"mobile cannon attachment");
                foreach(float v in cannon)Check(float.IsFinite(v),"mobile cannon attachment nonfinite");
                var ptr=MeleeNative.ssbm_view_positions(view,out uint count);var vertices=new float[count*3];Marshal.Copy(ptr,vertices,0,vertices.Length);
                foreach(float v in vertices)Check(float.IsFinite(v),"mobile fighter pose nonfinite");
                pad.buttons=32;uint stored=moving.shot_charge;
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/30);
                MeleeNative.ssbm_sim_state(sim,out var running);
                Check(running.move_id==0&&running.shot_charge==stored&&running.position.x>moving.position.x,"sprint failed to store charge and continue motion");
            } finally {MeleeNative.ssbm_view_free(view);MeleeNative.ssbm_sim_free(sim);}
        }
        Console.WriteLine("PASS: mobile Charge Shot pose/cannon, grounded travel and sprint storage at 30/60/144 FPS");
    }
    static void MorphBallRegressions(string assets,in MeleeNative.Host host)
    {
        foreach(int fps in new[]{30,60,144}) foreach(bool air in new[]{false,true}) {
            var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
            var view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,0);Check(view!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31};
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                if(air)MeleeNative.ssbm_sim_anchor(sim,new MeleeNative.Vec3(0,1000,0),0);
                MeleeNative.ssbm_sim_set_host_flags(sim,4);
                int before=thunderSpawn;
                pad.buttons=2048;pad.movement=new MeleeNative.Vec3(0,0,1);
                for(int f=0;f<fps;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(thunderSpawn==before+1,"Morph Ball must emit one startup bomb, ground or air");
                Check(Math.Abs(MeleeNative.ssbm_sim_motion_frame(sim)-13f)<0.001f,"Morph Ball pose did not freeze");
                Check(MeleeNative.ssbm_view_update(view,sim,1f/fps)==1,MeleeNative.Error);
                var ptr=MeleeNative.ssbm_view_positions(view,out uint n);var first=new float[n*3];Marshal.Copy(ptr,first,0,first.Length);
                pad.buttons|=1;MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/30);pad.buttons=2048;
                for(int f=0;f<fps*11;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                MeleeNative.ssbm_sim_state(sim,out var held);Check(held.move_id==24,"held ball ended or landing cancelled it");
                Check(Math.Abs(MeleeNative.ssbm_sim_motion_frame(sim)-13f)<0.001f,"jump/landing restarted the morph");
                Check(thunderSpawn==before+1,"held ball dropped another bomb");
                Check(MeleeNative.ssbm_view_update(view,sim,1f/fps)==1,MeleeNative.Error);
                ptr=MeleeNative.ssbm_view_positions(view,out n);var second=new float[n*3];Marshal.Copy(ptr,second,0,second.Length);
                bool rotated=false;for(int i=0;i<second.Length;i++)if(Math.Abs(first[i]-second[i])>0.1f)rotated=true;
                Check(rotated,"held ball mesh never rotated");
                if(air)Check(MeleeNative.ssbm_sim_break_roll(sim)==1,"ball armor break rejected");else pad.buttons=0;
                MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/30);
                MeleeNative.ssbm_sim_state(sim,out var ended);Check(ended.move_id==0,"release/armor break failed to restore normal form");
                Check(MeleeNative.ssbm_view_update(view,sim,1f/fps)==1,MeleeNative.Error);
                ptr=MeleeNative.ssbm_view_positions(view,out n);var normal=new float[n*3];Marshal.Copy(ptr,normal,0,normal.Length);
                int ballVertices=0,normalVertices=0;for(int i=0;i<normal.Length;i+=3) {
                    if(Math.Abs(second[i])+Math.Abs(second[i+1])+Math.Abs(second[i+2])>0.001f)ballVertices++;
                    if(Math.Abs(normal[i])+Math.Abs(normal[i+1])+Math.Abs(normal[i+2])>0.001f)normalVertices++;
                }
                Check(normalVertices>ballVertices,"exit retained the folded alternate mesh");
            } finally { MeleeNative.ssbm_view_free(view);MeleeNative.ssbm_sim_free(sim); }
        }
        Console.WriteLine("PASS: Morph Ball startup bomb, frozen rotating mesh, jumps/landings, release/armor break at 30/60/144 FPS");
    }
    static void AimingRegressions(string assets,in MeleeNative.Host host)
    {
        foreach(int fps in new[]{30,60,144}) {
            foreach(var example in new[]{(pitch:.6f,button:2u,want:11u),(pitch:-.9f,button:2u,want:12u),(pitch:.6f,button:4u,want:23u),(pitch:-.9f,button:4u,want:24u),(pitch:.9f,button:2u|16u,want:12u)}) {
                var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
                try {
                    var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,example.pitch,(float)Math.Sqrt(1-example.pitch*example.pitch)),allowed=31};
                    MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                    MeleeNative.ssbm_sim_facing(sim,new MeleeNative.Vec3(1,0,0));pad.movement=new MeleeNative.Vec3(0,0,1);
                    pad.buttons=example.button;
                    for(int i=0;i<Math.Max(1,fps/30);i++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                    MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==example.want,"camera directional selection through native DLL at "+fps);
                } finally {MeleeNative.ssbm_sim_free(sim);}
            }
            var actor=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(actor!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),movement=new MeleeNative.Vec3(0,0,-1),allowed=31};
                MeleeNative.ssbm_sim_facing(actor,new MeleeNative.Vec3(0,0,1));
                for(int i=0;i<fps/2;i++)MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                MeleeNative.ssbm_sim_state(actor,out var state);Check(state.facing.z>.999f&&state.velocity.z<0,"native yaw override changed backpedal momentum");
                MeleeNative.ssbm_sim_place(actor,default,0);pad.movement=default;pad.buttons=4;
                MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                Check(MeleeNative.ssbm_sim_charge_fraction(actor)>0,"aim test never charged");
                pad.buttons=128;MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                int before=shotCount;pad.buttons=4;MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps/2;i++)MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                pad.aim=new MeleeNative.Vec3(.8f,.6f,0);MeleeNative.ssbm_sim_facing(actor,new MeleeNative.Vec3(0,0,-1));
                pad.buttons=4;MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=0;
                for(int i=0;i<fps;i++)MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);
                Check(shotCount==before+1&&Math.Abs(lastShot.direction.x-.8f)<.001f&&Math.Abs(lastShot.direction.y-.6f)<.001f,"stored shot used body heading/old camera snapshot");
                Check(MeleeNative.ssbm_sim_facing_locked(actor)==0,"normal/ranged aim remains locked after completion");
            } finally {MeleeNative.ssbm_sim_free(actor);}
        }
        MeleeNative.ssbm_sim_facing(IntPtr.Zero,default);Check(MeleeNative.ssbm_sim_facing_locked(IntPtr.Zero)==0,"null facing boundary");
        Console.WriteLine("PASS: camera directional moves, independent host yaw/backpedal and stored-shot live aim through native DLL at 30/60/144 Hz");
    }
    // Pikachu staged specials and Thunder Shock through the native boundary: Quick Attack
    // reaches a locked enemy in its two zips, no move snaps the fighter back, Skull Bash
    // travels, Thunder reports its spawn and strike, and the shot bounces ten times.
    static void PikachuRegressions(string assets,in MeleeNative.Host host)
    {
        if(MeleeNative.ssbm_assets_complete(NativeLib.CString(assets),MeleeNative.FighterPikachu)!=1){Console.WriteLine("SKIP: Pikachu checks (no complete Pikachu cache)");return;}
        Check(MeleeNative.HasStagedSpecials,"staged-special exports missing from ssbm.dll");
        foreach(int fps in new[]{30,60,144}) {
            var sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterPikachu);Check(sim!=IntPtr.Zero,"Pikachu sim: "+MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),allowed=31};
                for(int f=0;f<10;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                MeleeNative.ssbm_sim_lock_target(sim,new MeleeNative.Vec3(0,0,60),1);
                pad.buttons=1024;MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;
                MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==23,"LB did not start Quick Attack");
                float furthest=state.position.z;
                for(int f=0;f<fps*4&&state.move_id==23;f++) {
                    MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);MeleeNative.ssbm_sim_state(sim,out state);
                    Check(state.position.z>=furthest-0.001f,"Quick Attack rubberbanded at "+fps+" FPS");furthest=Math.Max(furthest,state.position.z);
                }
                Check(state.move_id==0,"Quick Attack never ended");
                Check(state.position.z>=60-14&&state.position.z<=60,"Quick Attack stopped "+(60-state.position.z)+" short of the locked enemy at "+fps+" FPS");
                MeleeNative.ssbm_sim_lock_target(sim,default,0);
            }finally{MeleeNative.ssbm_sim_free(sim);}
            sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterPikachu);Check(sim!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),allowed=31};
                for(int f=0;f<10;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                pad.buttons=512;
                for(int f=0;f<fps/2;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                pad.buttons=0;MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==21,"B did not start Skull Bash");
                float furthest=0;
                for(int f=0;f<fps*4;f++) {
                    MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);MeleeNative.ssbm_sim_state(sim,out state);
                    float distance=(float)Math.Sqrt(state.position.x*state.position.x+state.position.z*state.position.z);
                    Check(distance>=furthest-0.001f,"Skull Bash rubberbanded at "+fps+" FPS");furthest=Math.Max(furthest,distance);
                }
                Check(state.move_id==0&&furthest>15,"Skull Bash did not travel and end ("+furthest+")");
            }finally{MeleeNative.ssbm_sim_free(sim);}
            thunderSpawn=thunderStrike=0;
            sim=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterPikachu);Check(sim!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),allowed=31};
                for(int f=0;f<10;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                pad.buttons=2048;MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;
                MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==24,"RB did not start Thunder");
                for(int f=0;f<fps*6&&state.move_id==24;f++){MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);MeleeNative.ssbm_sim_state(sim,out state);}
                Check(thunderSpawn==1&&thunderStrike==1,"Thunder reported "+thunderSpawn+" spawn / "+thunderStrike+" strike events");
                Check(lastThunder.direction.x>100&&lastThunder.direction.y>=1&&lastThunder.direction.z>0,"Thunder spawn event lost the cloud height/strike delay/speed");
            }finally{MeleeNative.ssbm_sim_free(sim);}
        }
        var straight=MeleeNative.ssbm_projectile_new(NativeLib.CString(assets),MeleeNative.FighterPikachu,1,0,0,new MeleeNative.Vec3(0,0,1));Check(straight!=IntPtr.Zero,"Thunder Shock straight: "+MeleeNative.Error);
        try {
            MeleeNative.ssbm_projectile_lifetime(straight,0);
            MeleeNative.ssbm_projectile_straight(straight,200f);
            for(int f=0;f<19;f++)MeleeNative.ssbm_projectile_step(straight,1.0/60,default,0);
            MeleeNative.ssbm_projectile_state(straight,out var level);
            Check(Math.Abs(level.position.y)<0.001f,"Thunder Shock arced before 20 m: y "+level.position.y);
            for(int f=0;f<3;f++)MeleeNative.ssbm_projectile_step(straight,1.0/60,default,0);
            MeleeNative.ssbm_projectile_state(straight,out var falling);
            Check(falling.position.y<0f,"Thunder Shock did not arc after 20 m: y "+falling.position.y);
        }finally{MeleeNative.ssbm_projectile_free(straight);}
        var shot=MeleeNative.ssbm_projectile_new(NativeLib.CString(assets),MeleeNative.FighterPikachu,1,0,0,new MeleeNative.Vec3(0,0.2f,1));Check(shot!=IntPtr.Zero,"Thunder Shock: "+MeleeNative.Error);
        try {
            MeleeNative.ssbm_projectile_lifetime(shot,0);
            MeleeNative.ssbm_projectile_state(shot,out var s);float flying=s.damage;int bounces=0;bool wall=false;
            for(int f=0;f<6000&&bounces<=10;f++) {
                MeleeNative.ssbm_projectile_step(shot,1.0/60,default,0);MeleeNative.ssbm_projectile_state(shot,out s);
                if(!wall&&s.position.z>60){MeleeNative.ssbm_projectile_reflect(shot,new MeleeNative.Vec3(0,0,-1));MeleeNative.ssbm_projectile_state(shot,out s);Check(s.velocity.z<0,"wall did not reflect Thunder Shock");wall=true;}
                if(s.position.y<-20&&s.velocity.y<0) {
                    if(MeleeNative.ssbm_projectile_bounce(shot)==0)break;
                    bounces++;MeleeNative.ssbm_projectile_place(shot,new MeleeNative.Vec3(s.position.x,-20,s.position.z),0);
                    if(bounces==1){MeleeNative.ssbm_projectile_state(shot,out s);Check(s.damage<flying,"Thunder Shock damage did not drop after landing ("+flying+" -> "+s.damage+")");}
                }
            }
            // A wall reflect spends the same ten-contact budget as a ground bounce (Projectile::reflect).
            Check(wall&&bounces+1==10,"Thunder Shock took "+bounces+" ground bounces plus one wall reflect, expected ten surface contacts in all");
        }finally{MeleeNative.ssbm_projectile_free(shot);}
        // The RT alternate borrows Yoshi's Egg Lay: the sim starts move 20 on its tense
        // Thunder clip, and the swallow/lay cues resolve from Yoshi's bank on Pikachu.
        if(MeleeNative.ssbm_assets_complete(NativeLib.CString(assets),MeleeNative.FighterYoshi)==1) {
            var lay=MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(assets),default,MeleeNative.FighterPikachu);Check(lay!=IntPtr.Zero,"Pikachu Egg Lay sim: "+MeleeNative.Error);
            try {
                MeleeNative.ssbm_sim_set_host_flags(lay,1);
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),allowed=31};
                for(int f=0;f<10;f++)MeleeNative.ssbm_sim_step(lay,in host,in pad,1.0/60);
                pad.buttons=256;MeleeNative.ssbm_sim_step(lay,in host,in pad,1.0/60);pad.buttons=0;
                MeleeNative.ssbm_sim_state(lay,out var state);Check(state.move_id==20,"RT did not start borrowed Egg Lay");
                Check(MeleeNative.ssbm_sim_play_sound_from(lay,MeleeNative.FighterYoshi,0x4460c)==1,"Yoshi lay cue missing: "+MeleeNative.Error);
                var pcm=new float[32000*2];Check(MeleeNative.ssbm_sim_audio(lay,pcm,32000)==32000,MeleeNative.Error);
                Check(Array.Exists(pcm,x=>Math.Abs(x)>0.0001f),"Yoshi lay cue decoded silence on Pikachu");
                for(int f=0;f<90&&state.move_id==20;f++){MeleeNative.ssbm_sim_step(lay,in host,in pad,1.0/60);MeleeNative.ssbm_sim_state(lay,out state);}
                Check(state.move_id==0,"borrowed Egg Lay never ended");
            }finally{MeleeNative.ssbm_sim_free(lay);}
        }
        Console.WriteLine("PASS: Pikachu Quick Attack reaches a locked enemy, Skull Bash and Thunder complete without rubberbanding at 30/60/144 FPS, Thunder Shock flies level for 20 m then arcs and reflects/bounces ten times, and the borrowed Egg Lay starts on move 20 with Yoshi's lay cue");
    }
    static void DefenseRegressions(string assets,in MeleeNative.Host host)
    {
        foreach(uint model in new uint[]{11,13,14}) {
            var effect=MeleeNative.ssbm_effect_new(NativeLib.CString(assets),MeleeNative.FighterSamus,model);Check(effect!=IntPtr.Zero,"shield model: "+MeleeNative.Error);
            try {Check(MeleeNative.ssbm_object_update(effect,4)==1,MeleeNative.Error);MeleeNative.ssbm_view_model(effect,out var info);Check(info.vertex_count>0,"empty shield geometry");
                var p=MeleeNative.ssbm_view_positions(effect,out uint count);var xyz=new float[count*3];Marshal.Copy(p,xyz,0,xyz.Length);foreach(float v in xyz)Check(float.IsFinite(v),"invalid shield position");
            }finally{MeleeNative.ssbm_view_free(effect);}
        }
        foreach(int fps in new[]{30,60,144}) {
            var actor=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(actor!=IntPtr.Zero,"defense tuning: "+MeleeNative.Error);
            var view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,0);Check(view!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad{aim=new MeleeNative.Vec3(0,0,1),allowed=31};MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);
                pad.buttons=4096;bool pcm=false;var samples=new float[1066];
                for(int frame=0;frame<fps;frame++) {MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);Check(MeleeNative.ssbm_view_update(view,actor,0)==1,"Guard pose: "+MeleeNative.Error);MeleeNative.ssbm_sim_audio(actor,samples,533);foreach(float sample in samples)pcm|=Math.Abs(sample)>0.0001f;}
                Check((MeleeNative.ssbm_sim_defense_flags(actor)&1)!=0,"held source Guard failed");Check(pcm,"shield-on source sound missing");
                float meter=MeleeNative.ssbm_sim_shield_fraction(actor);Check(meter>0&&meter<1,"source shield drain absent");
                Check(MeleeNative.ssbm_sim_shield_hit(actor,.1f)==1&&MeleeNative.ssbm_sim_shield_fraction(actor)<meter,"shield hit did not drain");
                Check(MeleeNative.ssbm_sim_shield_hit(actor,100)==1&&(MeleeNative.ssbm_sim_defense_flags(actor)&4)!=0,"shield break absent");
                MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);Check(MeleeNative.ssbm_view_update(view,actor,0)==1,"Furafura pose: "+MeleeNative.Error);
                MeleeNative.ssbm_sim_interrupt(actor);MeleeNative.ssbm_sim_place(actor,new MeleeNative.Vec3(0,1000,0),2);
                pad.buttons=0;MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/60);pad.buttons=4096;pad.movement=new MeleeNative.Vec3(1,0,0);
                bool immune=false,ended=false;
                for(int frame=0;frame<fps*2;frame++) {MeleeNative.ssbm_sim_step(actor,in host,in pad,1.0/fps);uint flags=MeleeNative.ssbm_sim_defense_flags(actor);immune|=(flags&2)!=0;ended|=immune&&(flags&8)==0;Check(MeleeNative.ssbm_view_update(view,actor,0)==1,"EscapeAir pose: "+MeleeNative.Error);}
                Check(immune&&ended,"source air-feint hurt-state window absent");
                MeleeNative.ssbm_sim_state(actor,out var state);float before=MeleeNative.ssbm_sim_motion_frame(actor);MeleeNative.ssbm_sim_anchor(actor,new MeleeNative.Vec3(10,50,20),0);MeleeNative.ssbm_sim_state(actor,out var anchored);
                Check(anchored.position.x==10&&anchored.move_id==state.move_id&&MeleeNative.ssbm_sim_motion_frame(actor)==before,"boss anchor reset action clock");
                MeleeNative.ssbm_sim_jump_height(actor,40);MeleeNative.ssbm_sim_jump_height(actor,0);
            }finally{MeleeNative.ssbm_view_free(view);MeleeNative.ssbm_sim_free(actor);}
        }
        Console.WriteLine("PASS: extracted shield models, Guard/Furafura/EscapeAir poses, common sound, source meter/hurt-state window and grapple clock preservation at 30/60/144 FPS");
    }
    static void PresentationRegressions(string assets,in MeleeNative.Host host)
    {
        var effect=MeleeNative.ssbm_effect_new(NativeLib.CString(assets),MeleeNative.FighterSamus,2000);Check(effect!=IntPtr.Zero,MeleeNative.Error);
        try {
            Check(MeleeNative.HasMaterialAlpha,"shared material alpha metadata export missing");
            using(var alpha=new MaterialAlphaBuffer())foreach(float frame in new[]{0f,12f,32f}) {
                Check(MeleeNative.ssbm_object_update(effect,frame)==1,MeleeNative.Error);
                var colors=MeleeNative.ssbm_view_colors(effect,out uint count);var expected=new float[count*4];Marshal.Copy(colors,expected,0,expected.Length);
                var metadata=MeleeNative.ssbm_view_alpha_sources(effect,out uint n);var raw=new float[n*3];Marshal.Copy(metadata,raw,0,raw.Length);
                Check(n==count,"alpha metadata record count");for(int i=0;i<raw.Length;i+=3)Check(raw[i]==0&&raw[i+2]==1,"expected zero-alpha material with texture blend");
                var resolved=alpha.Resolve(colors,count,metadata,n);var rgba=new float[count*4];Marshal.Copy(resolved,rgba,0,rgba.Length);
                for(int i=0;i<rgba.Length;i++)Check(Math.Abs(rgba[i]-expected[i])<0.00001f,"common alpha diverged from source viewer");
                if(frame==12)for(int i=3;i<rgba.Length;i+=4)Check(rgba[i]>0.1f,"shared alpha erased Screw Attack");
            }
        }
        finally {MeleeNative.ssbm_view_free(effect);}
        var chargeView=MeleeNative.ssbm_article_new(NativeLib.CString(assets),MeleeNative.FighterSamus,1,8);Check(chargeView!=IntPtr.Zero,MeleeNative.Error);
        try {
            Check(MeleeNative.ssbm_object_update(chargeView,12)==1,MeleeNative.Error);var before=MeleeNative.ssbm_view_positions(chargeView,out uint vertices);var first=new float[vertices*3];Marshal.Copy(before,first,0,first.Length);
            Check(MeleeNative.ssbm_object_camera(chargeView,new MeleeNative.Vec3(100,25,-50))==1,MeleeNative.Error);var after=MeleeNative.ssbm_view_positions(chargeView,out vertices);var second=new float[vertices*3];Marshal.Copy(after,second,0,second.Length);bool changed=false;for(int i=0;i<first.Length;i++){Check(float.IsFinite(second[i]),"camera-facing article produced invalid vertices");changed|=Math.Abs(second[i]-first[i])>0.001f;}Check(changed,"source charge core never faced the camera");
            MeleeNative.ssbm_view_model(chargeView,out var info);for(uint texture=0;texture<info.texture_count;texture++){Check(MeleeNative.ssbm_view_texture(chargeView,texture,out var tex,null,0)==1,MeleeNative.Error);Check(tex.wrap_s==2&&tex.wrap_t==2,"source mirrored shot quadrants lost at C# boundary");}
        } finally {MeleeNative.ssbm_view_free(chargeView);}
        for(uint part=0;part<8;part++){var view=MeleeNative.ssbm_grapple_new(NativeLib.CString(assets),part);Check(view!=IntPtr.Zero,"grapple part: "+MeleeNative.Error);MeleeNative.ssbm_view_model(view,out var info);Check(info.vertex_count>0,"empty source grapple part");MeleeNative.ssbm_view_free(view);}
        foreach(int fps in new[]{30,60,144}) {
            var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),default);Check(sim!=IntPtr.Zero,MeleeNative.Error);
            try {
                var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31};MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);
                pad.buttons=128;MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;
                bool beam=false,grabWindow=false;
                for(int f=0;f<fps*2;f++) {
                    MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                    uint n=MeleeNative.ssbm_sim_hitboxes(sim,null,0);var boxes=new MeleeNative.HitboxSample[n];Check(MeleeNative.ssbm_sim_hitboxes(sim,boxes,n)==n,"grab virtual joint 0x8B: "+MeleeNative.Error);foreach(var hit in boxes)grabWindow|=hit.element==8;
                    uint p=MeleeNative.ssbm_sim_grapple_points(sim,null,0);if(p>0){var points=new MeleeNative.Vec3[p];Check(MeleeNative.ssbm_sim_grapple_points(sim,points,p)==p,MeleeNative.Error);beam=true;foreach(var point in points)Check(float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z),"invalid grapple point");}
                    uint g=MeleeNative.ssbm_sim_gfx(sim,null,0);var cues=new MeleeNative.Event[g];Check(MeleeNative.ssbm_sim_gfx(sim,cues,g)==g,"grab GFX: "+MeleeNative.Error);foreach(var cue in cues)Check(MeleeNative.ssbm_effect_target(cue.kind)!=int.MaxValue,"unresolved grab effect "+cue.kind);
                }
                Check(beam&&grabWindow,"B grab did not run source beam/contact windows");
                MeleeNative.ssbm_sim_set_grabbed(sim,1);pad.movement=new MeleeNative.Vec3(0,1,0);pad.buttons=2;MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/60);pad.buttons=0;pad.movement=default;
                MeleeNative.ssbm_sim_state(sim,out var state);Check(state.move_id==31,"captured LS up + attack did not select up throw");int before=throws;
                for(int f=0;f<fps*2;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(throws==before+1&&MeleeNative.ssbm_sim_grabbed(sim)==0,"source throw release missing/duplicated");
                MeleeNative.ssbm_sim_begin_entry(sim,NativeLib.CString(assets));MeleeNative.ssbm_sim_entry_control(sim,0);
                for(int f=0;f<fps*3;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(MeleeNative.ssbm_sim_entry_control(sim,2)==1,"figurine activated without Interact");MeleeNative.ssbm_sim_entry_control(sim,1);
                for(int f=0;f<fps;f++)MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);
                Check(MeleeNative.ssbm_sim_entry_control(sim,2)==0,"figurine failed to activate after Interact");
            } finally {MeleeNative.ssbm_sim_free(sim);}
        }
        var shot=MeleeNative.ssbm_projectile_new(NativeLib.CString(assets),MeleeNative.FighterSamus,1,8,1,new MeleeNative.Vec3(0,0.6f,0.8f));Check(shot!=IntPtr.Zero,MeleeNative.Error);
        try {MeleeNative.ssbm_projectile_lifetime(shot,0);for(int i=0;i<600;i++)MeleeNative.ssbm_projectile_step(shot,1.0/60,default,0);MeleeNative.ssbm_projectile_state(shot,out var state);Check((state.flags&2)==0&&state.position.z>1000,"RoR2 charge shot still expires at short source range");Check(Math.Abs(state.position.y/state.position.z-0.75f)<0.001f,"charge shot changed aimed pitch");}
        finally {MeleeNative.ssbm_projectile_free(shot);}
        Console.WriteLine("PASS: visible Screw Attack, source grapple geometry/contact, directional throw release, manual entry controls and long aimed charge-shot travel");
    }
    static void RebirthRegressions(string assets,in MeleeNative.Host host)
    {
        foreach(int fps in new[]{30,60,144}) foreach(bool initial in new[]{false,true}) {
            ulong automatic=0,movement=0;
            foreach(bool move in new[]{false,true}) {
                var sim=MeleeNative.ssbm_sim_new(NativeLib.CString(assets),new MeleeNative.Vec3(0,initial?0:-200,0));Check(sim!=IntPtr.Zero,MeleeNative.Error);
                var view=MeleeNative.ssbm_view_new(NativeLib.CString(assets),MeleeNative.FighterSamus,0);Check(view!=IntPtr.Zero,MeleeNative.Error);
                try {
                    Check((initial?MeleeNative.ssbm_sim_begin_arrival(sim,NativeLib.CString(assets)):MeleeNative.ssbm_sim_begin_rebirth(sim,NativeLib.CString(assets),default))==1,MeleeNative.Error);
                    if(initial)Check((MeleeNative.ssbm_sim_rebirth_flags(sim)&12)==8,"initial arrival did not start as a visible figurine");
                    Check(MeleeNative.ssbm_sim_begin_rebirth(sim,NativeLib.CString(assets),default)==0,"duplicate falloff restarted KO");
                    var pad=new MeleeNative.Pad {aim=new MeleeNative.Vec3(0,0,1),allowed=31};int ko=0,transform=0;bool figurine=false,protectedState=false,koPcm=false,flashOn=false,flashOff=false;
                    var pcm=new float[1024];var overlay=new float[4];
                    for(int f=0;f<fps*10;f++) {
                        uint before=MeleeNative.ssbm_sim_rebirth_flags(sim);pad.movement=move&&(before&8)!=0?new MeleeNative.Vec3(0,0,1):default;
                        MeleeNative.ssbm_sim_step(sim,in host,in pad,1.0/fps);uint flags=MeleeNative.ssbm_sim_rebirth_flags(sim);
                        uint n=MeleeNative.ssbm_sim_gfx(sim,null,0);var cues=new MeleeNative.Event[n];Check(MeleeNative.ssbm_sim_gfx(sim,cues,n)==n,MeleeNative.Error);
                        foreach(var cue in cues){if(cue.kind==0x42b){ko++;Check(Math.Abs(cue.position.y+200)<.001f,"KO cue moved to respawn point");Check(MeleeNative.ssbm_effect_target(cue.kind)==-26,"original KO model not dispatched");}if(cue.kind==0x43e)transform++;}
                        Check(MeleeNative.ssbm_sim_audio(sim,pcm,512)==512,MeleeNative.Error);if((flags&4)!=0)foreach(float v in pcm)koPcm|=Math.Abs(v)>.00001f;
                        if((flags&4)!=0||(flags&8)!=0) {
                            Check(MeleeNative.ssbm_view_update(view,sim,1f/fps)==1,MeleeNative.Error);var ptr=MeleeNative.ssbm_view_positions(view,out uint vertices);var positions=new float[vertices*3];Marshal.Copy(ptr,positions,0,positions.Length);bool visible=false;foreach(float v in positions)visible|=Math.Abs(v)>.001f;
                            Check(visible==((flags&8)!=0),"fighter visibility wrong during KO/figurine");
                        }
                        figurine|=(flags&8)!=0;
                        if(flags==2) {
                            if(!protectedState){MeleeNative.ssbm_sim_state(sim,out var state);if(move)movement=state.frame;else automatic=state.frame;MeleeNative.ssbm_sim_place(sim,state.position,2);Check(MeleeNative.ssbm_sim_rebirth_flags(sim)==2,"collision correction cancelled protection");}
                            protectedState=true;bool visible=MeleeNative.ssbm_sim_overlay(sim,overlay)==1;flashOn|=visible;flashOff|=!visible;
                            MeleeNative.ssbm_sim_hurt(sim);
                        }
                    }
                    Check(ko==(initial?0:1)&&transform==1&&figurine&&protectedState&&(initial||koPcm)&&flashOn&&flashOff,"missing/duplicate recovery cues, PCM, figurine, protection or flash at "+fps+" initial="+initial);
                    Check(MeleeNative.ssbm_sim_rebirth_flags(sim)==0&&MeleeNative.ssbm_sim_entry_control(sim,2)==0,"respawn/protection failed to expire");
                }finally{MeleeNative.ssbm_view_free(view);MeleeNative.ssbm_sim_free(sim);}
            }
            Check(movement<automatic,"movement did not release the figurine earlier than timeout");
        }
        Console.WriteLine("PASS: shared initial/falloff return, initial spawn without KO or Interact, fall KO model/PCM, visible figurine, movement/timeout release, flash and expiring protection at 30/60/144 FPS");
    }
}

