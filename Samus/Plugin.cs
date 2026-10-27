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
    [BepInPlugin(Guid,"Melee","0.4.4")]
    [BepInDependency("___riskofthunder.RoR2BepInExPack",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.libssbm.melee";
        internal static ManualLogSource Log;
        internal static string AssetsDir;
        internal static volatile bool AssetsReady,AssetsBusy,FoxReady,LuigiReady,CaptainReady,LinkReady,PikachuReady,YoshiReady,DonkeyReady;
        internal static ConfigEntry<float> MetresPerUnit,JumpScale,SpeedScale;
        internal static ConfigEntry<float> QuailScale,StompScale,AttackSpeedScale;
        internal static ConfigEntry<float> Armor;
        internal static ConfigEntry<bool> HoldCharge;
        internal static ConfigEntry<bool> RimLight;
        internal static ConfigEntry<bool> GummyClones;
        internal static ConfigEntry<float> FighterBrightness;
        internal static ConfigEntry<float> MissileRange,MissileDamageScale,ChargeShotHitboxScale,ProjectileSpeed;
        internal static ConfigEntry<float> StaggerDuration;
        internal static ConfigEntry<float> LockRange;
        internal static ConfigEntry<bool> LogEffects;
        internal static ConfigEntry<bool> Diagnostics;
        internal static ConfigEntry<float> ScopeRange;
        internal static ConfigEntry<bool> ScopeChargeSmoke;
        string nativePath,configuredDisc,pluginDirectory;
        bool nativeStarted,destroyed;
        void Awake()
        {
            CommonLog.Log=Logger;Log=Logger;
            string here=Path.GetDirectoryName(Info.Location);
            var assets=Config.Bind("Paths","AssetsDir","","Extracted Melee cache. Empty uses assets_dir.txt or assets beside the plugin. Never distribute it.");
            var disc=Config.Bind("Paths","Disc","","Your Melee GALE01 revision 2 ISO/GCM/CISO, used to extract missing assets at first launch.");
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
            Diagnostics=Config.Bind("Debug","Diagnostics",true,"Log each Melee move start (slot, charge, authority, whether it may deal damage), each hitbox batch and each hit dealt, as [MeleeDiag] lines in the BepInEx log. For finding why a move deals no damage. Cheap, but turn it off once everything works.");
            MeleeDiag.On=Diagnostics.Value;
            ScopeRange=Config.Bind("Luigi","ScopeRangeScale",3f,new ConfigDescription("Super Scope: how many times as fast and as far as the source's the beam flies, because an open world is bigger than a stage.",new AcceptableValueRange<float>(1f,10f)));
            ScopeChargeSmoke=Config.Bind("Luigi","ScopeChargeSmoke",true,"Super Scope: repeat the disc's animated ground smoke and wind streaks (EfCoData model 5, cue 0x3FF) at the feet while charging on the ground. Host visual timing; smoke grows slightly with charge and finishes its animation after release.");
            // Melee multiplayer: the messages that carry a fighter to the other players (MeleeNet).
            MeleeNet.Register();
            // The shared Egg Lay, for a character outside this plugin (Sunshine Luigi's secret
            // dpad move): the egg, trap, fan, knockback and break-out stay the Melee ones.
            EggLay.Catch=(owner,victim)=>YoshiFull.Begin(owner,victim);
            EggLay.Lay=(owner,victim,at)=>YoshiEgg.Lay(owner,victim,at);
            EggLay.Laying=owner=>YoshiFull.IsLaying(owner);
            EggLay.Show=(owner,target)=>YoshiTongue.Show(owner,target);
            LogEffects=Config.Bind("Debug","LogEffects",false,"Log each Melee model effect (Screw Attack burst etc.) when it spawns and ends, with how often its texture changed while drawn. For diagnosing static effects.");
            string hint=Path.Combine(here,"assets_dir.txt");
            AssetsDir=Path.GetFullPath(!string.IsNullOrWhiteSpace(assets.Value)?assets.Value:
                File.Exists(hint)?File.ReadAllText(hint).Trim():Path.Combine(here,"assets"));
            nativePath=Path.Combine(here,"ssbm.dll");configuredDisc=disc.Value;pluginDirectory=here;
            // Register content while RoR2 collects providers, but do no native
            // loading/extraction or Steam reflection/hooks during bootstrap.
            // Displays/portraits/drivers already wait for AssetsReady/AssetsBusy.
            AssetsReady=false;AssetsBusy=true;
            RoR2Application.onLoad+=InitializeAssets;
            ContentProvider.Register(Guid,AddContent);
            Language.onCurrentLanguageChanged+=Tokens;
            ItemHooks.Install();GlyphOverride.Install();MeleeSkins.Install();SurvivorSelectLayout.Install();
            PauseControls.Install(gameObject);
            PauseControls.Register("MeleeSamusBody", body => MeleeSheets.SamusSheet(body));
            PauseControls.Register("MeleeFoxBody", body => MeleeSheets.FoxSheet(body));
            PauseControls.Register("MeleeLuigiBody", body => MeleeSheets.LuigiSheet(body));
            PauseControls.Register("MeleeCaptainBody", body => MeleeSheets.CaptainSheet(body));
            PauseControls.Register("MeleeLinkBody", body => MeleeSheets.LinkSheet(body));
            PauseControls.Register("MeleePikachuBody", body => MeleeSheets.PikachuSheet(body));
            PauseControls.Register("MeleeYoshiBody", body => MeleeSheets.YoshiSheet(body));
            PauseControls.Register("MeleeDonkeyBody", body => MeleeSheets.DonkeySheet(body));
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
            Logger.LogInfo("Melee 0.4.3 registered; native initialization waits for RoR2 startup completion.");
        }
        /// Whether the extracted cache for the fighter of native kind `kind` is complete.
        static bool AssetsFor(uint kind)
        {
            if(kind==MeleeNative.FighterSamus)return AssetsReady;
            if(kind==MeleeNative.FighterFox)return FoxReady;
            if(kind==MeleeNative.FighterLuigi)return LuigiReady;
            if(kind==MeleeNative.FighterCaptain)return CaptainReady;
            if(kind==MeleeNative.FighterLink)return LinkReady;
            if(kind==MeleeNative.FighterPikachu)return PikachuReady;
            if(kind==MeleeNative.FighterYoshi)return YoshiReady;
            if(kind==MeleeNative.FighterDonkey)return DonkeyReady;
            return false;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void InitializeAssets()
        {
            RoR2Application.onLoad-=InitializeAssets;
            if(nativeStarted||destroyed)return;nativeStarted=true;
            Logger.LogInfo("Samus late native initialization: RoR2 startup completed.");
            try {
                MeleeNative.Load(nativePath);
                AssetsReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterSamus)==1;
                FoxReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterFox)==1;
                LuigiReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterLuigi)==1;
                CaptainReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterCaptain)==1;
                LinkReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterLink)==1;
                PikachuReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterPikachu)==1;
                YoshiReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterYoshi)==1;
                DonkeyReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterDonkey)==1;
                bool itemsMissing=MeleeNative.HasScope&&!File.Exists(Path.Combine(AssetsDir,"ItCo.dat"));
                if(!AssetsReady||!FoxReady||!LuigiReady||!CaptainReady||!LinkReady||!PikachuReady||!YoshiReady||!DonkeyReady||itemsMissing) {
                    new Thread(()=> {
                        try {
                            string image=FindDisc(configuredDisc,pluginDirectory);
                            if(image==null&&itemsMissing&&AssetsReady&&FoxReady&&LuigiReady&&CaptainReady&&LinkReady&&PikachuReady&&YoshiReady&&DonkeyReady)
                                throw new FileNotFoundException("Set Paths.Disc to your Melee disc to extract the Super Scope's data.");
                            if(image==null) throw new FileNotFoundException("Set Paths.Disc to your Melee GALE01 rev 2 disc image.");
                            // Each fighter extracts independently; a failure only affects that fighter.
                            if(!AssetsReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterSamus)!=1)throw new IOException(MeleeNative.Error);}
                            if(!FoxReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterFox)!=1)throw new IOException(MeleeNative.Error);}
                            if(!LuigiReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterLuigi)!=1)throw new IOException(MeleeNative.Error);}
                            if(!CaptainReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterCaptain)!=1)throw new IOException(MeleeNative.Error);}
                            if(!LinkReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterLink)!=1)throw new IOException(MeleeNative.Error);}
                            if(!PikachuReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterPikachu)!=1)throw new IOException(MeleeNative.Error);}
                            if(!YoshiReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterYoshi)!=1)throw new IOException(MeleeNative.Error);}
                            if(!DonkeyReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterDonkey)!=1)throw new IOException(MeleeNative.Error);}
                            // The items archive (the Super Scope): optional, so a failure only loses the scope.
                            if(itemsMissing){
                                try {
                                    if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),100)!=1)throw new IOException(MeleeNative.Error);
                                    Log.LogInfo("Super Scope data extracted from your disc.");
                                } catch(Exception e) { Log.LogWarning("Super Scope data unavailable: "+e.Message); }
                            }
                            Log.LogInfo("Melee assets extracted from your disc.");
                        } catch(Exception e) { Log.LogError("Melee assets unavailable: "+e.Message); }
                        finally {
                            // Re-check each from the actual cache: a missing disc or a
                            // partial extract must not disable a fighter whose assets
                            // were already complete.
                            AssetsReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterSamus)==1;
                            FoxReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterFox)==1;
                            LuigiReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterLuigi)==1;
                            CaptainReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterCaptain)==1;
                            LinkReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterLink)==1;
                            PikachuReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterPikachu)==1;
                            YoshiReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterYoshi)==1;
                            DonkeyReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterDonkey)==1;
                            AssetsBusy=false;
                        }
                    }) { IsBackground=true,Name="Melee first-run extraction" }.Start();
                } else AssetsBusy=false;
                Logger.LogInfo("Melee 0.4.4 / libssbm ABI "+MeleeNative.AbiVersion+" loaded after RoR2 startup.");
                } catch(Exception e) {AssetsReady=false;FoxReady=false;AssetsBusy=false;Logger.LogError("Samus disabled: "+e);}
        }
        static string FindDisc(string configured,string here)
        {
            if(!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
            string env=Environment.GetEnvironmentVariable("SSBM_DISC");if(!string.IsNullOrWhiteSpace(env))return Path.GetFullPath(env);
            // Bounded discovery around this installation; content always remains local.
            var at=new DirectoryInfo(here);
            for(int i=0;i<7&&at!=null;i++,at=at.Parent) {
                foreach(string folder in new[]{at.FullName,Path.Combine(at.FullName,"iso backups")}) {
                    if(!Directory.Exists(folder))continue;
                    foreach(string image in Directory.EnumerateFiles(folder,"*Melee*"))
                        if(Array.Exists(new[]{".iso",".gcm",".ciso"},e=>string.Equals(Path.GetExtension(image),e,StringComparison.OrdinalIgnoreCase)))return image;
                }
            }
            return null;
        }
        static void AddContent(ContentPack pack)
        {
            try { Skills.Set(MeleeSurvivor.Register(pack,Guid,SamusDef.Def())); }
            catch(Exception e) { Log.LogError("Samus registration failed: "+e); }
            try { Fox.FoxSkills.Set(MeleeSurvivor.Register(pack,Guid,Fox.FoxDef.Def())); }
            catch(Exception e) { Log.LogError("Fox registration failed: "+e); }
            try { Luigi.LuigiSkills.Set(MeleeSurvivor.Register(pack,Guid,Luigi.LuigiDef.Def())); }
            catch(Exception e) { Log.LogError("Luigi registration failed: "+e); }
            try { Captain.CaptainSkills.Set(MeleeSurvivor.Register(pack,Guid,Captain.CaptainDef.Def())); }
            catch(Exception e) { Log.LogError("Captain Falcon registration failed: "+e); }
            try { Link.LinkSkills.Set(MeleeSurvivor.Register(pack,Guid,Link.LinkDef.Def())); }
            catch(Exception e) { Log.LogError("Link registration failed: "+e); }
            try { Pikachu.PikachuSkills.Set(MeleeSurvivor.Register(pack,Guid,Pikachu.PikachuDef.Def())); }
            catch(Exception e) { Log.LogError("Pikachu registration failed: "+e); }
            try { Yoshi.YoshiSkills.Set(MeleeSurvivor.Register(pack,Guid,Yoshi.YoshiDef.Def())); }
            catch(Exception e) { Log.LogError("Yoshi registration failed: "+e); }
            try { Donkey.DonkeySkills.Set(MeleeSurvivor.Register(pack,Guid,Donkey.DonkeyDef.Def())); }
            catch(Exception e) { Log.LogError("Donkey Kong registration failed: "+e); }
            Tokens();Log.LogInfo("Melee survivors registered");
        }
        static void Tokens()
        {
            var l=Language.currentLanguage;if(l==null)return;
            MeleeSurvivor.Tokens(l,SamusDef.Def());
            MeleeSurvivor.Tokens(l,Fox.FoxDef.Def());
            MeleeSurvivor.Tokens(l,Luigi.LuigiDef.Def());
            MeleeSurvivor.Tokens(l,Captain.CaptainDef.Def());
            MeleeSurvivor.Tokens(l,Link.LinkDef.Def());
            MeleeSurvivor.Tokens(l,Pikachu.PikachuDef.Def());
            MeleeSurvivor.Tokens(l,Yoshi.YoshiDef.Def());
            MeleeSurvivor.Tokens(l,Donkey.DonkeyDef.Def());
        }
        void OnDestroy(){destroyed=true;RoR2Application.onLoad-=InitializeAssets;Language.onCurrentLanguageChanged-=Tokens;MeleeSkins.Uninstall();}
    }
    public sealed class SamusMain:BaseCharacterMain { }
}

