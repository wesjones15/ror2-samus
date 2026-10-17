using System;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace Samus
{
    // ftSamus/ftsamus.c ftSs_Init_CostumeStrings order. The archives remain local.
    internal static class Skins
    {
        static readonly UnlockableDef[] unlocks=new UnlockableDef[5];
        static readonly string[] names={"Power Suit","Pink","Black","Green","Lavender"};
        static readonly Color32[] colors={new Color32(245,125,25,255),new Color32(225,125,180,255),new Color32(75,70,95,255),new Color32(80,190,90,255),new Color32(170,155,235,255)};
        internal static void Add(GameObject body,ContentPack pack)
        {
            var model=body.GetComponent<ModelLocator>().modelTransform;
            var controller=model.GetComponent<ModelSkinController>();if(!controller)controller=model.gameObject.AddComponent<ModelSkinController>();
            var character=model.GetComponent<CharacterModel>();var skins=new SkinDef[5];
            for(int i=0;i<5;i++) {
                var skin=ScriptableObject.CreateInstance<SkinDef>();skin.name="MeleeSamusSkin"+i;skin.nameToken="SSBM_SKIN_"+i;skin.rootObject=model.gameObject;
                skin.baseSkins=Array.Empty<SkinDef>();skin.icon=Icon(colors[i]);
                var p=ScriptableObject.CreateInstance<SkinDefParams>();p.name=skin.name+"Params";
                p.rendererInfos=character.baseRendererInfos;p.gameObjectActivations=Array.Empty<SkinDefParams.GameObjectActivation>();
                p.meshReplacements=Array.Empty<SkinDefParams.MeshReplacement>();p.projectileGhostReplacements=Array.Empty<SkinDefParams.ProjectileGhostReplacement>();
                p.minionSkinReplacements=Array.Empty<SkinDefParams.MinionSkinReplacement>();p.lightReplacements=Array.Empty<CharacterModel.LightInfo>();
                skin.skinDefParams=p;skin.optimizedSkinDefParams=p;
                if(i>0) {
                    int stage=i;var u=ScriptableObject.CreateInstance<UnlockableDef>();u.cachedName="Skins.MeleeSamus."+i;u.nameToken=skin.nameToken;
                    u.getHowToUnlockString=()=>"Complete stage "+stage+"'s teleporter event as Samus.";u.getUnlockedString=()=>"Samus costume unlocked.";
                    skin.unlockableDef=u;unlocks[i]=u;pack.unlockableDefs.Add(new[]{u});
                }
                skins[i]=skin;
            }
            controller.skins=skins;
            // SkinCatalog enumerates registered body ModelSkinControllers.
        }
        static Sprite Icon(Color32 suit)
        {
            var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);var pixels=new Color32[64];
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)pixels[y*8+x]=y>=3&&y<=4&&x>1&&x<6?new Color32(50,220,115,255):suit;
            texture.SetPixelData(pixels,0);texture.Apply(false,true);return Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(0.5f,0.5f));
        }
        internal static void Install(){TeleporterInteraction.onTeleporterChargedGlobal+=Charged;}
        internal static void Uninstall(){TeleporterInteraction.onTeleporterChargedGlobal-=Charged;}
        static void Charged(TeleporterInteraction teleporter)
        {
            if(!Run.instance)return;int stage=Run.instance.stageClearCount+1;
            foreach(var user in LocalUserManager.readOnlyLocalUsersList) {
                if(!user.cachedBody||user.cachedBody.baseNameToken!="SSBM_SAMUS_NAME")continue;
                for(int i=1;i<unlocks.Length&&i<=stage;i++)if(unlocks[i]&&!user.userProfile.HasUnlockable(unlocks[i])) {
                    user.userProfile.GrantUnlockable(unlocks[i]);user.userProfile.RequestEventualSave();Plugin.Log.LogInfo("Unlocked Samus costume: "+names[i]);
                }
            }
        }
        internal static uint Selected()
        {
            var index=BodyCatalog.FindBodyIndex("MeleeSamusBody");if(index==BodyIndex.None)return 0;
            foreach(var user in LocalUserManager.readOnlyLocalUsersList)if(user.userProfile!=null)return Math.Min(4,user.userProfile.loadout.bodyLoadoutManager.GetSkinIndex(index));
            return 0;
        }
        internal static void Tokens(Language l){for(int i=0;i<5;i++)l.SetStringByToken("SSBM_SKIN_"+i,names[i]);}
    }
}
