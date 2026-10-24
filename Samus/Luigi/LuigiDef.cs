using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Luigi
{
    internal static class LuigiSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeLuigi0",nameToken="SSBM_LUIGI_SKILL_0",descriptionToken="SSBM_LUIGI_SKILL_DESC_0",
                displayName="Fireball",description="RT: fire a bouncing green fireball.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            primaryAlt = new MeleeSkillInfo {
                defName="MeleeLuigiScope",nameToken="SSBM_LUIGI_SCOPE",descriptionToken="SSBM_LUIGI_SCOPE_DESC",
                displayName="Super Scope",description="Gives Luigi the Super Scope: tap to fire small shots, or hold to charge and release a bigger shot for more damage.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeLuigiCyclone",nameToken="SSBM_LUIGI_CYCLONE",descriptionToken="SSBM_LUIGI_CYCLONE_DESC",
                displayName="Luigi Cyclone",description="LT: spin, pulling nearby enemies into repeated hits before a launching last hit. Steer while spinning; tap LT again and again in the air to climb.",
                icon="EngiBodyPlaceSpiderMine",recharge=0f,stock=1,combat=true },
            secondaryAlt = new MeleeSkillInfo {
                defName="MeleeLuigiShield",nameToken="SSBM_LUIGI_SHIELD",descriptionToken="SSBM_LUIGI_SHIELD_DESC",
                displayName="Shield / Air Feint",description="LT: hold a shield on the ground or evade once in the air.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            utility = new MeleeSkillInfo {
                defName="MeleeLuigi2",nameToken="SSBM_LUIGI_SKILL_2",descriptionToken="SSBM_LUIGI_SKILL_DESC_2",
                displayName="Green Missile",description="RB: hold to charge (up to 90 frames), release to launch along your crosshair. Speed and damage grow with the charge, and it stops on what it hits. One launch in eight misfires: a far faster, 25% blast.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=2,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeLuigi3",nameToken="SSBM_LUIGI_SKILL_3",descriptionToken="SSBM_LUIGI_SKILL_DESC_3",
                displayName="Super Jump Punch",description="LB: rise with a punch. Right on top of Luigi it is a 25% fire hit (20% in the air); anything farther gets a weak coin hit that pays gold. It ends in a helpless fall with long landing lag.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            specialAlt = new MeleeSkillInfo {
                defName="MeleeLuigiScopeSpecial",nameToken="SSBM_LUIGI_SCOPE",descriptionToken="SSBM_LUIGI_SCOPE_SPECIAL_DESC",
                displayName="Super Scope",description="Gives Luigi the Super Scope: tap to fire small shots, or hold to charge and release a bigger shot for more damage.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeLuigi1",nameToken="SSBM_LUIGI_SKILL_1",descriptionToken="SSBM_LUIGI_SKILL_DESC_1",
                displayName="A Attack",description="B: neutral A while stationary. LS + B selects camera-directed attacks; a flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
            // The Super Scope (native move 17) may be the primary slot's alternate or the
            // special (LB) slot's alternate; the slot the loadout holds decides the charge.
            slotForMove=(body,m) => m == 17 ? (LuigiSkills.SpecialAltSelected(body) ? 3 : 0) : MeleeSkillSet.DefaultSlot(m),
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        /// This fighter's built skill layout, for the shared MeleeDriver.BeginMove.
        internal static MeleeSkillSet Built => set;
        internal static void Set(MeleeSkillSet built) => set = built;
        internal static GenericSkill Get(CharacterBody body, int slot) => set.Get(body, slot);
        internal static bool ShieldSelected(CharacterBody body) => set.ShieldSelected(body);
        /// The loadout holds the Super Scope in the special (LB) slot instead of the Super Jump Punch.
        internal static bool SpecialAltSelected(CharacterBody body) => set != null && set.SpecialAltSelected(body);
        internal static NativeSkillDef Expected(CharacterBody body, int slot) => set.Expected(body, slot);
        internal static int Slot(CharacterBody body, uint move) => set.Slot(body, move);
        internal static uint Allowed(CharacterBody body) => set.Allowed(body);
    }

    internal static class LuigiSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Luigi",bodyNameToken="SSBM_LUIGI_NAME",bodyName="MeleeLuigiBody",
            defPrefix="MeleeLuigiSkin",nameTokenPrefix="SSBM_LUIGI_SKIN_",unlockPrefix="Skins.MeleeLuigi.",
            // ftluigi.c::ftLg_Init_CostumeStrings order: Nr, Wh, Aq, Pi.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_LUIGI_SKIN_0", displayName="Default", color=new Color32(72,164,72,255) },
                new MeleeSkinInfo { nameToken="SSBM_LUIGI_SKIN_1", displayName="White", color=new Color32(232,232,238,255) },
                new MeleeSkinInfo { nameToken="SSBM_LUIGI_SKIN_2", displayName="Aqua", color=new Color32(96,186,206,255) },
                new MeleeSkinInfo { nameToken="SSBM_LUIGI_SKIN_3", displayName="Pink", color=new Color32(232,132,172,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeLuigiBody");
    }

    internal static class LuigiDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeLuigi",bodyName="MeleeLuigiBody",displayName="MeleeLuigiDisplay",
            nameToken="SSBM_LUIGI_NAME",subtitleToken="SSBM_LUIGI_SUBTITLE",descriptionToken="SSBM_LUIGI_DESCRIPTION",
            outroToken="SSBM_LUIGI_OUTRO",failToken="SSBM_LUIGI_FAIL",
            nameStr="Luigi",subtitleStr="The Eternal Understudy",
            descriptionStr="Luigi brings his Melee loadout to Petrichor V.\n\nRT: Fireball. B: contextual A. RB: Green Missile. LB: Super Jump Punch. LT: Luigi Cyclone, with Shield / Air Feint as its alternate.\n\nOffline prototype.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.3f,0.75f,0.35f),
            baseMaxHealth=100f,levelMaxHealth=30f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=104f,
            fighterKind=MeleeNative.FighterLuigi,assetsDir=()=>Samus.Plugin.AssetsDir,assetsReady=()=>Samus.Plugin.LuigiReady,
            mainStateType=typeof(LuigiMain),driverType=typeof(LuigiDriver),
            skills=LuigiSkills.Layout,skins=LuigiSkins.Layout,
            displayFactory=LuigiDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.LuigiReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterLuigi, "Melee Luigi original CSS grid portrait") : null;
    }

    public sealed class LuigiMain : BaseCharacterMain { }
}
