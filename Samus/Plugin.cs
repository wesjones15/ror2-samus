using System;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EntityStates;
using RoR2;
using RoR2.ContentManagement;
using Ror2Common;
using UnityEngine;
using Yoshi;
using Path=System.IO.Path;

namespace Samus
{
    [BepInPlugin(Guid,"Melee","0.5.2")]
    [BepInDependency("___riskofthunder.RoR2BepInExPack",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.libssbm.melee";
        internal static ManualLogSource Log;
        /// SharedFiles' Melee folder: every plugin's Melee files (this one's fighters, Mario's A attacks, the skins).
        internal static string AssetsDir;
        internal static volatile bool AssetsBusy;
        /// One row per fighter. A fighter marked `experimental` in its def is skipped everywhere (not registered,
        /// checked, extracted or baked) unless Experimental.Enabled.
        sealed class Fighter
        {
            public uint Kind;public string Name,Body;public Func<MeleeFighterDef> Def;
            public Action<MeleeSkillSet> SetSkills;public Func<CharacterBody,ControlSheet> Sheet;
            public volatile bool Ready;
        }
        static readonly Fighter[] Roster={
            new Fighter{Kind=MeleeNative.FighterSamus,Name="Samus",Body="MeleeSamusBody",Def=SamusDef.Def,SetSkills=Skills.Set,Sheet=MeleeSheets.SamusSheet},
            new Fighter{Kind=MeleeNative.FighterFox,Name="Fox",Body="MeleeFoxBody",Def=Fox.FoxDef.Def,SetSkills=Fox.FoxSkills.Set,Sheet=MeleeSheets.FoxSheet},
            new Fighter{Kind=MeleeNative.FighterLuigi,Name="Luigi",Body="MeleeLuigiBody",Def=Luigi.LuigiDef.Def,SetSkills=Luigi.LuigiSkills.Set,Sheet=MeleeSheets.LuigiSheet},
            new Fighter{Kind=MeleeNative.FighterCaptain,Name="Captain Falcon",Body="MeleeCaptainBody",Def=Captain.CaptainDef.Def,SetSkills=Captain.CaptainSkills.Set,Sheet=MeleeSheets.CaptainSheet},
            new Fighter{Kind=MeleeNative.FighterLink,Name="Link",Body="MeleeLinkBody",Def=Link.LinkDef.Def,SetSkills=Link.LinkSkills.Set,Sheet=MeleeSheets.LinkSheet},
            new Fighter{Kind=MeleeNative.FighterPikachu,Name="Pikachu",Body="MeleePikachuBody",Def=Pikachu.PikachuDef.Def,SetSkills=Pikachu.PikachuSkills.Set,Sheet=MeleeSheets.PikachuSheet},
            new Fighter{Kind=MeleeNative.FighterYoshi,Name="Yoshi",Body="MeleeYoshiBody",Def=Yoshi.YoshiDef.Def,SetSkills=Yoshi.YoshiSkills.Set,Sheet=MeleeSheets.YoshiSheet},
            new Fighter{Kind=MeleeNative.FighterDonkey,Name="Donkey Kong",Body="MeleeDonkeyBody",Def=Donkey.DonkeyDef.Def,SetSkills=Donkey.DonkeySkills.Set,Sheet=MeleeSheets.DonkeySheet},
        };
        /// The fighters in this game: finished ones, plus the unfinished ones when Experimental.Enabled.
        static System.Collections.Generic.IEnumerable<Fighter> Enabled()
        {
            foreach(var f in Roster)if(Experimental.Allows(f.Def().experimental))yield return f;
        }
        static bool ReadyFor(uint kind){if(OptionalAssets.BuiltinOnly)return false;foreach(var f in Roster)if(f.Kind==kind)return f.Ready;return false;}
        internal static bool AssetsReady=>ReadyFor(MeleeNative.FighterSamus);
        internal static bool FoxReady=>ReadyFor(MeleeNative.FighterFox);
        internal static bool LuigiReady=>ReadyFor(MeleeNative.FighterLuigi);
        internal static bool CaptainReady=>ReadyFor(MeleeNative.FighterCaptain);
        internal static bool LinkReady=>ReadyFor(MeleeNative.FighterLink);
        internal static bool PikachuReady=>ReadyFor(MeleeNative.FighterPikachu);
        internal static bool YoshiReady=>ReadyFor(MeleeNative.FighterYoshi);
        internal static bool DonkeyReady=>ReadyFor(MeleeNative.FighterDonkey);
        internal static ConfigEntry<float> MetresPerUnit,JumpScale,SpeedScale;
        internal static ConfigEntry<float> QuailScale,StompScale,AttackSpeedScale;
        internal static ConfigEntry<float> Armor;
        internal static ConfigEntry<bool> HoldCharge;
        internal static ConfigEntry<bool> RimLight;
        internal static ConfigEntry<bool> GummyClones;
        internal static ConfigEntry<bool> ItemDisplays;
        internal static ConfigEntry<float> FighterBrightness;
        internal static ConfigEntry<float> MissileRange,MissileDamageScale,ChargeShotHitboxScale,ProjectileSpeed;
        internal static ConfigEntry<float> StaggerDuration;
        internal static ConfigEntry<float> LockRange;
        internal static ConfigEntry<bool> LogEffects;
        internal static ConfigEntry<bool> Diagnostics;
        internal static ConfigEntry<float> ScopeRange;
        string nativePath;
        bool nativeStarted,destroyed;
        void Awake()
        {
            CommonLog.Log=Logger;Log=Logger;
            Experimental.ShareWithLobby();
            // Only read with Blowntobytes' VR mod running in a headset (ror2-common's Vr, MeleeVr).
            MeleeVr.Settings=new VrSettings(Config,"Melee");
            MeleeVr.ControlsSetting=Config.Bind("VR","Controls",true,
                "In VR with motion controls, every Melee fighter plays in one layout (right-handed; the VR mod's left-handed "+
                "mode mirrors it), the pad's buttons on the controllers: A jumps, B is the fifth button (the A attack or the "+
                "shield), the right trigger and grip are RT and RB, the left trigger and grip LT and LB, X grabs and interacts, "+
                "Y uses equipment, the left stick moves (where you look is forward) and its click sprints, the right stick click "+
                "pings, and the right hand points the aim. RoR2 reads this layout, not the VR mod's own buttons. Off: the VR "+
                "mod's own buttons through RoR2's skills.");
            string here=Path.GetDirectoryName(Info.Location);
            MetresPerUnit=Config.Bind("Sim","MetresPerUnit",0.1f,new ConfigDescription("World metres per Melee unit. Movement, reach and model share this scale.",new AcceptableValueRange<float>(0.03f,0.3f)));
            JumpScale=Config.Bind("Sim","JumpScale",1f,new ConfigDescription("Takeoff speed multiplier. 1 uses Melee tuning; increase for RoR2 traversal.",new AcceptableValueRange<float>(0.5f,3f)));
            SpeedScale=Config.Bind("Sim","SpeedScale",1.37f,new ConfigDescription("Walk/run speed multiplier. 1 uses raw Melee tuning; 1.37 lifts the slowest walker (Captain Falcon) to Commando's walk speed and the slowest runner to Commando's sprint, preserving each fighter's relative speed.",new AcceptableValueRange<float>(0.5f,3f)));
            QuailScale=Config.Bind("Items","WaxQuail",1f,new ConfigDescription("Wax Quail's boost to a sprinting jump, times this (1: RoR2's). 0 disables the item's boost.",new AcceptableValueRange<float>(0f,10f)));
            MeleeItems.QuailScale=QuailScale.Value;
            StompScale=Config.Bind("Items","Headstompers",1f,new ConfigDescription("H3AD-5T v2's stomp blast, times this (1: RoR2's). 0 disables the item's blast. Jump in the air with no jumps left to slam down.",new AcceptableValueRange<float>(0f,10f)));
            MeleeItems.StompScale=StompScale.Value;
            AttackSpeedScale=Config.Bind("Items","AttackSpeed",1f,new ConfigDescription("How much of RoR2's attack speed (Soldier's Syringe, Mocha, ...) speeds up Melee moves. 1 applies it in full, 0 ignores it.",new AcceptableValueRange<float>(0f,10f)));
            MeleeItems.AttackSpeedScale=AttackSpeedScale.Value;
            Armor=Config.Bind("Combat","Armor",20f,new ConfigDescription("Base armor. RoR2 multiplies incoming damage by 100/(100+armor), so 20 cuts a hit to ~83%. Melee survivors (Mercenary, Loader) use 20.",new AcceptableValueRange<float>(0f,100f)));
            HoldCharge=Config.Bind("Controls","HoldCharge",false,"False: tap neutral special to charge, tap again to fire; sprint/jump/X grab stores. True: hold and release to fire.");
            MissileRange=Config.Bind("Combat","MissileRangeMetres",120f,new ConfigDescription("How far away a homing missile can lock onto an enemy, in metres. A flying Charge Shot or missile has no range cap: it is removed when it has flown 20 s, left the stage's bounds or fallen far below the stage.",new AcceptableValueRange<float>(0f,2000f)));
            ProjectileSpeed=Config.Bind("Combat","ProjectileSpeedScale",3f,new ConfigDescription("How many times as fast the Charge Shot and the missiles fly as the source's (host adaptation: an open world is bigger than a stage). 1 is the source's speed.",new AcceptableValueRange<float>(1f,10f)));
            MissileDamageScale=Config.Bind("Combat","MissileDamageScale",5f,new ConfigDescription("Multiplier on side-B missile and Super Missile damage, because the move has a cooldown.",new AcceptableValueRange<float>(0.1f,50f)));
            ChargeShotHitboxScale=Config.Bind("Combat","ChargeShotHitboxScale",3f,new ConfigDescription("Charge Shot enemy hit radius multiplier, so shots at range hit more easily. Walls and floor use the shared narrow energy-shot terrain probe.",new AcceptableValueRange<float>(1f,10f)));
            LockRange=Config.Bind("Combat","LockRangeMetres",25f,new ConfigDescription("How far away Pikachu's Quick Attack can lock onto an enemy (the nearest to your crosshair, preferring ones in front), in metres.",new AcceptableValueRange<float>(5f,100f)));
            StaggerDuration=Config.Bind("Combat","StaggerDuration",0.25f,new ConfigDescription("Seconds a melee A attack stuns a non-boss enemy. Refreshed per hit so continued attacks lock trash; bosses/elites are exempt. 0 disables.",new AcceptableValueRange<float>(0f,2f)));
            RimLight=Config.Bind("Visuals","RimLight",true,"Draw the glow edge the source gives some moves (Captain Falcon's forward smash, Luigi's fireball) using RoR2's Fresnel emission. Turn off if models look wrong with it.");
            MeleeFighterView.RimLight=RimLight.Value;
            GummyClones=Config.Bind("Visuals","GummyClones",true,"Draw a Melee fighter no player controls (Goobo Jr.'s clone) in RoR2's gummy clone material.");
            MeleeFighterView.GummyClones=GummyClones.Value;
            FighterBrightness=Config.Bind("Visuals","FighterBrightness",1.5f,new ConfigDescription("How much brighter than the source's material colour the fighters are drawn. Melee's models carry a diffuse colour of 0.70 and its stage lighting makes up for it; RoR2's lighting does not, so they looked dark. 1 is the source's colour; raise it if they still look dark, lower it if they look washed out.",new AcceptableValueRange<float>(0.5f,3f)));
            MeleeFighterView.FighterBrightness=FighterBrightness.Value;
            ItemDisplays=Config.Bind("Visuals","ItemDisplays",false,"Draw RoR2's cosmetic item displays on the fighters: glasses, syringes, crowns and the rest follow the animated body instead of being hidden with Commando. Turn off to hide them.");
            ItemDisplayRig.Enabled=ItemDisplays.Value;
            Diagnostics=Config.Bind("Debug","Diagnostics",true,"Log each Melee move start (slot, charge, authority, whether it may deal damage), each hitbox batch and each hit dealt, as [MeleeDiag] lines in the BepInEx log. For finding why a move deals no damage. Cheap, but turn it off once everything works.");
            MeleeDiag.On=Diagnostics.Value;
            ScopeRange=Config.Bind("Luigi","ScopeRangeScale",3f,new ConfigDescription("Super Scope: how many times as fast and as far as the source's the beam flies, because an open world is bigger than a stage.",new AcceptableValueRange<float>(1f,10f)));
            // Melee multiplayer: the messages that carry a fighter to the other players (MeleeNet).
            MeleeNet.Register();
            // The shared Egg Lay, for a character outside this plugin (Sunshine Luigi's secret
            // dpad move): the egg, trap, fan, knockback and break-out stay the Melee ones.
            EggLay.Catch=(owner,victim)=>YoshiFull.Begin(owner,victim);
            EggLay.Lay=(owner,victim,at)=>YoshiEgg.Lay(owner,victim,at);
            EggLay.Laying=owner=>YoshiFull.IsLaying(owner);
            EggLay.Show=(owner,target)=>YoshiTongue.Show(owner,target);
            LogEffects=Config.Bind("Debug","LogEffects",false,"Log each Melee model effect (Screw Attack burst etc.) when it spawns and ends, with how often its texture changed while drawn. For diagnosing static effects.");
            AssetsDir=OptionalAssets.BuiltinOnly ? "" : Path.GetFullPath(SharedFiles.Assets(SharedFiles.Melee));
            Logger.LogInfo("Melee assets "+AssetsDir);
            nativePath=Path.Combine(here,"ssbm.dll");
            // Register content while RoR2 collects providers, but do no native
            // loading/extraction or Steam reflection/hooks during bootstrap.
            // Displays/portraits/drivers already wait for AssetsReady/AssetsBusy.
            AssetsBusy=true;
            RoR2Application.onLoad+=InitializeAssets;
            ContentProvider.Register(Guid,AddContent);
            Language.onCurrentLanguageChanged+=Tokens;
            ItemHooks.Install();GlyphOverride.Install();MeleeSkins.Install();SurvivorSelectLayout.Install();
            PauseControls.Install(gameObject);
            foreach(var f in Enabled())PauseControls.Register(f.Body,f.Sheet);
            RunAudio.Initialize(MeleeAudio.StopAll);
            // End of run: the GAME! banner for whichever Melee fighter the local player played (MeleeLocal,
            // recorded by every MeleeDriver when its sim starts), voiced from that fighter's own audio bank.
            Run.onClientGameOverGlobal+=(run,report)=>{
                if(!MeleeLocal.TryGet(run,out uint kind)){if(MeleeDiag.On)Logger.LogInfo("[MeleeDiag] GAME!: the local player has no Melee fighter in this run");return;}
                if(!AssetsFor(kind)){Logger.LogWarning("Melee GAME!: assets for fighter kind "+kind+" are not ready");return;}
                if(MeleeDiag.On)Logger.LogInfo("[MeleeDiag] GAME!: banner for fighter kind "+kind);
                try{MeleeGameBanner.Show(new MeleeGameEffect(AssetsDir, kind, MetresPerUnit.Value));}
                catch(Exception e){Logger.LogError("Melee GAME presentation: "+e);MeleeGameBanner.Show(null);}
            };
            Logger.LogInfo("Melee 0.5.2 registered; native initialization waits for RoR2 startup completion.");
        }
        /// Whether the extracted cache for the fighter of native kind `kind` is complete.
        static bool AssetsFor(uint kind)=>ReadyFor(kind);
        static bool Complete(uint kind)=>MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),kind)==1;
        /// Re-reads each enabled fighter's completeness from the cache itself. A fighter that isn't enabled stays not ready.
        static void Recheck(){foreach(var f in Enabled())f.Ready=Complete(f.Kind);}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void InitializeAssets()
        {
            RoR2Application.onLoad-=InitializeAssets;
            if(nativeStarted||destroyed)return;nativeStarted=true;
            Logger.LogInfo("Melee late native initialization: RoR2 startup completed.");
            try {
                MeleeNative.Load(nativePath);
                if (OptionalAssets.BuiltinOnly) { AssetsBusy = false; return; }
                Recheck();
                // A set written by an older extractor (SharedFiles extracted.txt) is extracted again when the disc is
                // there; without the disc its files are used as they are.
                bool Current(uint kind)=>SharedFiles.Current(SharedFiles.Melee,MeleeNative.ExtractSet(kind),MeleeNative.ExtractorVersion);
                bool ItemsMissing()=>MeleeNative.HasScope&&!File.Exists(Path.Combine(AssetsDir,"ItCo.dat"));
                bool ItemsStale()=>MeleeNative.HasScope&&!ItemsMissing()&&!Current(100);
                bool anyMissing=ItemsMissing()||ItemsStale();foreach(var f in Enabled())anyMissing|=!f.Ready||!Current(f.Kind);
                if(anyMissing) {
                    new Thread(()=> {
                        try {
                            // Mario and Olimar extract their own Melee files into the same folder; wait for them.
                            using(SharedFiles.Lock(SharedFiles.Melee)) {
                                Recheck();
                                bool fightersMissing=false,fightersStale=false;
                                foreach(var f in Enabled()){fightersMissing|=!f.Ready;fightersStale|=f.Ready&&!Current(f.Kind);}
                                if(!fightersMissing&&!fightersStale&&!ItemsMissing()&&!ItemsStale())return;
                                string image=SharedFiles.FindDisc("GALE01",2);
                                if(image==null)
                                {
                                    if(fightersMissing)Log.LogInfo("Melee uses built-in gameplay and generic shapes; no optional Melee disc was found.");
                                    else if(ItemsMissing())Log.LogWarning("Melee: the Super Scope's data isn't extracted. "+SharedFiles.MissingDisc("Super Smash Bros. Melee (USA, revision 1.02)"));
                                    else Log.LogWarning("Melee: files extracted by an older version of the mod are used as they are. "+SharedFiles.MissingDisc("Super Smash Bros. Melee (USA, revision 1.02)"));
                                    return;
                                }
                                // Each fighter extracts independently; a failure only affects that fighter.
                                foreach(var f in Enabled()) {
                                    if(f.Ready&&Current(f.Kind))continue;
                                    if(f.Ready)Log.LogInfo("Melee: "+f.Name+"'s files are from an older version of the mod; extracting them again.");
                                    f.Ready=false; // nothing reads them while they're rewritten
                                    if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),f.Kind)==1)
                                        SharedFiles.MarkCurrent(SharedFiles.Melee,MeleeNative.ExtractSet(f.Kind),MeleeNative.ExtractorVersion);
                                    else Log.LogError("Melee: "+f.Name+"'s files didn't extract: "+MeleeNative.Error);
                                }
                                // The items archive (the Super Scope): optional, so a failure only loses the scope.
                                if(ItemsMissing()||ItemsStale()){
                                    if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),100)==1){
                                        SharedFiles.MarkCurrent(SharedFiles.Melee,MeleeNative.ExtractSet(100),MeleeNative.ExtractorVersion);
                                        Log.LogInfo("Super Scope data extracted from your disc.");
                                    }
                                    else Log.LogWarning("Super Scope data unavailable: "+MeleeNative.Error);
                                }
                                Log.LogInfo("Melee assets extracted from "+image+" into "+AssetsDir+".");
                            }
                        } catch(Exception e) { Log.LogError("Melee assets unavailable: "+e.Message); }
                        finally {
                            // Re-check each from the actual cache: a missing disc or a partial extract must not disable a
                            // fighter whose assets were already complete.
                            Recheck();
                            AssetsBusy=false;
                            BakeVrArms();
                        }
                    }) { IsBackground=true,Name="Melee first-run extraction" }.Start();
                } else { AssetsBusy=false; new Thread(BakeVrArms) { IsBackground=true,Name="Melee VR arms" }.Start(); }
                Logger.LogInfo("Melee 0.5.2 / libssbm ABI "+MeleeNative.AbiVersion+" loaded after RoR2 startup.");
            } catch(Exception e) {foreach(var f in Roster)f.Ready=false;AssetsBusy=false;Logger.LogError("Melee disabled: "+e);}
        }
        // Every extracted fighter costume's forearms and hands for the VR mod's controllers (ror2-common VrArmsBake),
        // again whenever a fighter joins the cache.
        static void BakeVrArms()
        {
            int ready=0;foreach(var f in Enabled())if(f.Ready)ready++;
            VrArmsBake.Run(AssetsDir,"Melee","0.5.0|"+ready,(buf,len)=>{
                uint n=MeleeNative.ssbm_bake_vr_arms(NativeLib.CString(AssetsDir));
                byte[] text=System.Text.Encoding.UTF8.GetBytes(n==0?MeleeNative.Error:n==1?"":(n-1)+" costumes cut");
                Array.Copy(text,buf,Math.Min(text.Length,(int)len-1));
                return (byte)(n==0?0:1);
            });
        }
        static void AddContent(ContentPack pack)
        {
            foreach(var f in Enabled()) {
                try { f.SetSkills(MeleeSurvivor.Register(pack,Guid,f.Def())); }
                catch(Exception e) { Log.LogError(f.Name+" registration failed: "+e); }
            }
            Tokens();Log.LogInfo("Melee survivors registered");
        }
        static void Tokens()
        {
            var l=Language.currentLanguage;if(l==null)return;
            foreach(var f in Enabled())MeleeSurvivor.Tokens(l,f.Def());
        }
        void OnDestroy(){destroyed=true;RoR2Application.onLoad-=InitializeAssets;Language.onCurrentLanguageChanged-=Tokens;MeleeSkins.Uninstall();}
    }
    public sealed class SamusMain:BaseCharacterMain { }
}

