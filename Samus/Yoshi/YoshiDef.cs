using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Yoshi
{
    internal static class YoshiSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeYoshi0",nameToken="SSBM_YOSHI_SKILL_0",descriptionToken="SSBM_YOSHI_SKILL_DESC_0",
                displayName="Egg Lay",description="RT: lash the tongue to swallow enemies and lay eggs behind you. Recharges when it catches.",
                icon="CommandoBodyFirePistol",recharge=3f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeYoshiBomb",nameToken="SSBM_YOSHI_BOMB",descriptionToken="SSBM_YOSHI_BOMB_DESC",
                displayName="Yoshi Bomb",description="LT: hop and slam down; in the air, slam immediately. Recharges.",
                icon="EngiBodyPlaceSpiderMine",recharge=3f,stock=1,combat=true },
            secondaryAlt = new MeleeSkillInfo {
                defName="MeleeYoshiShield",nameToken="SSBM_YOSHI_SHIELD",descriptionToken="SSBM_YOSHI_SHIELD_DESC",
                displayName="Shield / Air Feint",description="LT: hold to shield on the ground; in the air, evade once.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            extra = new MeleeSkillInfo {
                defName="MeleeYoshi1",nameToken="SSBM_YOSHI_SKILL_1",descriptionToken="SSBM_YOSHI_SKILL_DESC_1",
                displayName="A Attack",description="B: neutral A. LS + B aims attacks; flick to charge a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            utility = new MeleeSkillInfo {
                defName="MeleeYoshi2",nameToken="SSBM_YOSHI_SKILL_2",descriptionToken="SSBM_YOSHI_SKILL_DESC_2",
                displayName="Egg Roll",description="RB: hold to roll as an egg, steering. The egg absorbs hits; release to stop. Recharges.",
                icon="EngiBodyPlaceSpiderMine",recharge=5f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeYoshi3",nameToken="SSBM_YOSHI_SKILL_3",descriptionToken="SSBM_YOSHI_SKILL_DESC_3",
                displayName="Egg Throw",description="LB: hop and throw an egg that bursts on contact. Recharges.",
                icon="LoaderBodyChargeFist",recharge=2f,stock=1,combat=true },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        /// This fighter's built skill layout, for the shared MeleeDriver.BeginMove.
        internal static MeleeSkillSet Built => set;
        internal static void Set(MeleeSkillSet built) => set = built;
        internal static GenericSkill Get(CharacterBody body, int slot) => set.Get(body, slot);
        internal static bool ShieldSelected(CharacterBody body) => set.ShieldSelected(body);
        internal static NativeSkillDef Expected(CharacterBody body, int slot) => set.Expected(body, slot);
        internal static int Slot(CharacterBody body, uint move) => set.Slot(body, move);
        internal static uint Allowed(CharacterBody body) => set.Allowed(body);
    }

    internal static class YoshiSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Yoshi",bodyNameToken="SSBM_YOSHI_NAME",bodyName="MeleeYoshiBody",
            defPrefix="MeleeYoshiSkin",nameTokenPrefix="SSBM_YOSHI_SKIN_",unlockPrefix="Skins.MeleeYoshi.",
            // ftyoshi.c::ftYs_Init_CostumeStrings order: Nr, Re, Bu, Ye, Pi, Aq.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_0", displayName="Default", color=new Color32(90,180,90,255) },
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_1", displayName="Red", color=new Color32(210,72,72,255) },
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_2", displayName="Blue", color=new Color32(72,120,232,255) },
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_3", displayName="Yellow", color=new Color32(236,208,72,255) },
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_4", displayName="Pink", color=new Color32(236,150,180,255) },
                new MeleeSkinInfo { nameToken="SSBM_YOSHI_SKIN_5", displayName="Aqua", color=new Color32(90,190,200,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeYoshiBody");
    }

    internal static class YoshiDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeYoshi",bodyName="MeleeYoshiBody",displayName="MeleeYoshiDisplay",
            nameToken="SSBM_YOSHI_NAME",subtitleToken="SSBM_YOSHI_SUBTITLE",descriptionToken="SSBM_YOSHI_DESCRIPTION",
            outroToken="SSBM_YOSHI_OUTRO",failToken="SSBM_YOSHI_FAIL",
            nameStr="Yoshi",subtitleStr="Mario's Dog",
            descriptionStr="Yoshi brings his Melee loadout to Petrichor V.\n\nRT: Egg Lay. B: contextual A. RB: Egg Roll. LB: Egg Throw. LT: Yoshi Bomb, with Shield / Air Feint as its alternate.\n\nOffline prototype.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.35f,0.7f,0.35f),
            baseMaxHealth=100f,levelMaxHealth=30f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=108f,
            fighterKind=MeleeNative.FighterYoshi,assetsDir=()=>Samus.Plugin.AssetsDir,assetsReady=()=>Samus.Plugin.YoshiReady,
            mainStateType=typeof(YoshiMain),driverType=typeof(YoshiDriver),
            skills=YoshiSkills.Layout,skins=YoshiSkins.Layout,
            displayFactory=YoshiDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.YoshiReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, "Melee Yoshi original CSS grid portrait") : null;
    }

    public sealed class YoshiMain : BaseCharacterMain { }
}
