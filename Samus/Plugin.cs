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
using Path=System.IO.Path;

namespace Samus
{
    [BepInPlugin(Guid,"Melee","0.3.0")]
    [BepInDependency("___riskofthunder.RoR2BepInExPack",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.libssbm.melee";
        internal static ManualLogSource Log;
        internal static string AssetsDir;
        internal static volatile bool AssetsReady,AssetsBusy,FoxReady;
        internal static ConfigEntry<float> MetresPerUnit,JumpScale;
        internal static ConfigEntry<float> Armor;
        internal static ConfigEntry<bool> HoldCharge;
        internal static ConfigEntry<float> MissileRange,MissileDamageScale,ChargeShotHitboxScale;
        internal static ConfigEntry<float> StaggerDuration;
        internal static ConfigEntry<bool> LogEffects;
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
            Armor=Config.Bind("Combat","Armor",20f,new ConfigDescription("Base armor. RoR2 multiplies incoming damage by 100/(100+armor), so 20 cuts a hit to ~83%. Melee survivors (Mercenary, Loader) use 20.",new AcceptableValueRange<float>(0f,100f)));
            HoldCharge=Config.Bind("Controls","HoldCharge",false,"False: tap neutral special to charge, tap again to fire; sprint/jump/X grab stores. True: hold and release to fire.");
            MissileRange=Config.Bind("Combat","MissileRangeMetres",120f,new ConfigDescription("How far a side-B missile flies before it is removed, in metres. 0 is unlimited. Melee's own cutoff is off-screen distance.",new AcceptableValueRange<float>(0f,2000f)));
            MissileDamageScale=Config.Bind("Combat","MissileDamageScale",5f,new ConfigDescription("Multiplier on side-B missile and Super Missile damage, because the move has a cooldown.",new AcceptableValueRange<float>(0.1f,50f)));
            ChargeShotHitboxScale=Config.Bind("Combat","ChargeShotHitboxScale",3f,new ConfigDescription("Charge Shot enemy hit radius multiplier, so shots at range hit more easily. Walls and floor use the shot's own size.",new AcceptableValueRange<float>(1f,10f)));
            StaggerDuration=Config.Bind("Combat","StaggerDuration",0.25f,new ConfigDescription("Seconds a melee A attack stuns a non-boss enemy. Refreshed per hit so continued attacks lock trash; bosses/elites are exempt. 0 disables.",new AcceptableValueRange<float>(0f,2f)));
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
            ItemHooks.Install();GlyphOverride.Install();MeleeSkins.Install();
            PauseControls.Install(gameObject);
            PauseControls.Register("MeleeSamusBody", _ => MeleeSheets.SamusSheet());
            PauseControls.Register("MeleeFoxBody", _ => MeleeSheets.FoxSheet());
            RunAudio.Initialize(MeleeAudio.StopAll);
            Run.onClientGameOverGlobal+=(run,report)=>{
                if(run!=SamusDriver.LocalRun||!AssetsReady)return;
                try{MeleeGameBanner.Show(new MeleeGameEffect(AssetsDir, MeleeNative.FighterSamus, MetresPerUnit.Value));}
                catch(Exception e){Logger.LogError("Melee GAME presentation: "+e);MeleeGameBanner.Show(null);}
            };
            Logger.LogInfo("Melee 0.3.0 registered; native initialization waits for RoR2 startup completion.");
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
                if(!AssetsReady||!FoxReady) {
                    new Thread(()=> {
                        try {
                            string image=FindDisc(configuredDisc,pluginDirectory);
                            if(image==null) throw new FileNotFoundException("Set Paths.Disc to your Melee GALE01 rev 2 disc image.");
                            // Each fighter extracts independently; a failure only affects that fighter.
                            if(!AssetsReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterSamus)!=1)throw new IOException(MeleeNative.Error);}
                            if(!FoxReady){if(MeleeNative.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir),MeleeNative.FighterFox)!=1)throw new IOException(MeleeNative.Error);}
                            Log.LogInfo("Melee assets extracted from your disc.");
                        } catch(Exception e) { Log.LogError("Melee assets unavailable: "+e.Message); }
                        finally {
                            // Re-check each from the actual cache: a missing disc or a
                            // partial extract must not disable a fighter whose assets
                            // were already complete.
                            AssetsReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterSamus)==1;
                            FoxReady=MeleeNative.ssbm_assets_complete(NativeLib.CString(AssetsDir),MeleeNative.FighterFox)==1;
                            AssetsBusy=false;
                        }
                    }) { IsBackground=true,Name="Melee first-run extraction" }.Start();
                } else AssetsBusy=false;
                Logger.LogInfo("Melee 0.3.0 / libssbm ABI "+MeleeNative.AbiVersion+" loaded after RoR2 startup.");
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
            Tokens();Log.LogInfo("Melee survivors registered");
        }
        static void Tokens()
        {
            var l=Language.currentLanguage;if(l==null)return;
            MeleeSurvivor.Tokens(l,SamusDef.Def());
            MeleeSurvivor.Tokens(l,Fox.FoxDef.Def());
        }
        void OnDestroy(){destroyed=true;RoR2Application.onLoad-=InitializeAssets;Language.onCurrentLanguageChanged-=Tokens;MeleeSkins.Uninstall();}
    }
    public sealed class SamusMain:BaseCharacterMain { }
}

