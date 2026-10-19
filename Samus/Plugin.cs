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
    [BepInPlugin(Guid,"Samus from Melee","0.2.17")]
    [BepInDependency("___riskofthunder.RoR2BepInExPack",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.libssbm.samus";
        internal static ManualLogSource Log;
        internal static string AssetsDir;
        internal static volatile bool AssetsReady,AssetsBusy;
        internal static ConfigEntry<float> MetresPerUnit,JumpScale;
        internal static ConfigEntry<bool> HoldCharge;
        internal static ConfigEntry<float> MissileRange,MissileDamageScale,ChargeShotHitboxScale;
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
            HoldCharge=Config.Bind("Controls","HoldCharge",false,"False: tap neutral special to charge, tap again to fire; sprint/jump/X grab stores. True: hold and release to fire.");
            MissileRange=Config.Bind("Combat","MissileRangeMetres",120f,new ConfigDescription("How far a side-B missile flies before it is removed, in metres. 0 is unlimited. Melee's own cutoff is off-screen distance.",new AcceptableValueRange<float>(0f,2000f)));
            MissileDamageScale=Config.Bind("Combat","MissileDamageScale",5f,new ConfigDescription("Multiplier on side-B missile and Super Missile damage, because the move has a cooldown.",new AcceptableValueRange<float>(0.1f,50f)));
            ChargeShotHitboxScale=Config.Bind("Combat","ChargeShotHitboxScale",3f,new ConfigDescription("Charge Shot enemy hit radius multiplier, so shots at range hit more easily. Walls and floor use the shot's own size.",new AcceptableValueRange<float>(1f,10f)));
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
            ItemHooks.Install();GlyphOverride.Install();Skins.Install();
            RunAudio.Initialize(SamusAudio.StopAll);
            Run.onClientGameOverGlobal+=(run,report)=>{
                if(run!=SamusDriver.LocalRun||!AssetsReady)return;
                try{MeleeGameBanner.Show(new SamusGameEffect());}
                catch(Exception e){Logger.LogError("Melee GAME presentation: "+e);MeleeGameBanner.Show(null);}
            };
            Logger.LogInfo("Samus 0.2.17 registered; native initialization waits for RoR2 startup completion.");
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void InitializeAssets()
        {
            RoR2Application.onLoad-=InitializeAssets;
            if(nativeStarted||destroyed)return;nativeStarted=true;
            Logger.LogInfo("Samus late native initialization: RoR2 startup completed.");
            try {
                Native.Load(nativePath);
                AssetsReady=Native.ssbm_assets_complete(NativeLib.CString(AssetsDir))==1;
                if(!AssetsReady) {
                    new Thread(()=> {
                        try {
                            string image=FindDisc(configuredDisc,pluginDirectory);
                            if(image==null) throw new FileNotFoundException("Set Paths.Disc to your Melee GALE01 rev 2 disc image.");
                            if(Native.ssbm_extract(NativeLib.CString(image),NativeLib.CString(AssetsDir))!=1) throw new IOException(Native.Error);
                            AssetsReady=Native.ssbm_assets_complete(NativeLib.CString(AssetsDir))==1;
                            if(!AssetsReady) throw new IOException("Extraction finished without a complete Samus cache. Set Paths.Disc and restart to retry.");
                            Log.LogInfo("Samus assets extracted from your disc.");
                        } catch(Exception e) { Log.LogError("Samus assets unavailable: "+e.Message); }
                        finally { AssetsBusy=false; }
                    }) { IsBackground=true,Name="Samus first-run extraction" }.Start();
                } else AssetsBusy=false;
                Logger.LogInfo("Samus 0.2.17 / libssbm ABI "+Native.AbiVersion+" loaded after RoR2 startup.");
                } catch(Exception e) {AssetsReady=false;AssetsBusy=false;Logger.LogError("Samus disabled: "+e);}
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
            var commando=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var display=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            if(!commando||!display)throw new InvalidOperationException("Commando prefabs unavailable");
            var body=Prefabs.Clone(commando,"MeleeSamusBody",Guid);var cb=body.GetComponent<CharacterBody>();
            cb.baseNameToken="SSBM_SAMUS_NAME";cb.subtitleNameToken="SSBM_SAMUS_SUBTITLE";cb.bodyColor=new Color(1f,0.48f,0.06f);
            cb.baseMaxHealth=140f;cb.levelMaxHealth=42f;cb.baseArmor=10f;cb.baseJumpCount=2;
            cb.preferredPodPrefab=null;cb.preferredInitialStateType=new SerializableEntityStateType(typeof(MeleeArrival));
            foreach(var state in body.GetComponents<EntityStateMachine>()) if(state.customName=="Body"){state.mainStateType=new SerializableEntityStateType(typeof(SamusMain));state.initialStateType=new SerializableEntityStateType(typeof(MeleeArrival));}
            body.AddComponent<NativeBody>();body.AddComponent<SamusDriver>();body.AddComponent<MeleeRecovery>();body.AddComponent<MeleeDefense>();Skills.Add(body,pack);
            var lobby=Prefabs.Clone(display,"MeleeSamusDisplay",Guid);DisplayModel.Attach(lobby,Guid,SamusDisplay.Make);Portrait.RequestTexture(body,SamusPortrait.Load);
            Skins.Add(body,pack);
            var survivor=ScriptableObject.CreateInstance<SurvivorDef>();survivor.cachedName="MeleeSamus";survivor.bodyPrefab=body;survivor.displayPrefab=lobby;
            survivor.displayNameToken="SSBM_SAMUS_NAME";survivor.descriptionToken="SSBM_SAMUS_DESCRIPTION";survivor.outroFlavorToken="SSBM_SAMUS_OUTRO";
            survivor.mainEndingEscapeFailureFlavorToken="SSBM_SAMUS_FAIL";survivor.primaryColor=cb.bodyColor;survivor.desiredSortPosition=102f;
            pack.bodyPrefabs.Add(new[]{body});pack.survivorDefs.Add(new[]{survivor});pack.entityStateTypes.Add(new[]{typeof(SamusMain),typeof(MeleeArrival)});
            GlyphOverride.Set(body.name,()=>true,new System.Collections.Generic.Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},[SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},[SkillSlot.Special]=new[]{PadButton.LB} });
            Tokens();Log.LogInfo("Melee Samus survivor registered");
        }
        static void Tokens()
        {
            var l=Language.currentLanguage;if(l==null)return;
            l.SetStringByToken("SSBM_SAMUS_NAME","Samus");l.SetStringByToken("SSBM_SAMUS_SUBTITLE","Intergalactic Bounty Hunter");
            l.SetStringByToken("SSBM_SAMUS_DESCRIPTION","Samus brings her Melee Power Suit to Petrichor V.\n\nRT: Charge Shot. LT: contextual A; LS + LT selects camera-directed attacks and a timed flick charges smashes. RB: missiles. LB: Screw Attack. B: Bomb, with Shield / Air Feint as the fifth skill loadout alternate.\n\nA jumps at Commando height, LS click sprints and X aims a 20m Z-grab. X yields to visible interact prompts. Captured enemies use LS + LT/RT to throw or neutral LT/RT to pummel. Bosses pull Samus instead; hold X and use contextual LT, or A to detach.\n\nOffline prototype: advanced shield rules, full material fidelity, capture escape rules and item integration remain in progress.");
            l.SetStringByToken("SSBM_SAMUS_OUTRO","..and so she left, mission complete.");l.SetStringByToken("SSBM_SAMUS_FAIL","..and so she vanished, beyond the blast zone.");Skills.Tokens(l);Skins.Tokens(l);
        }
        void OnDestroy(){destroyed=true;RoR2Application.onLoad-=InitializeAssets;Language.onCurrentLanguageChanged-=Tokens;Skins.Uninstall();}
    }
    public sealed class SamusMain:BaseCharacterMain { }
}
