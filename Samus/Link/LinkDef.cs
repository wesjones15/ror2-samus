using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Link
{
    internal static class LinkSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeLink0",nameToken="SSBM_LINK_SKILL_0",descriptionToken="SSBM_LINK_SKILL_DESC_0",
                displayName="Hero's Bow",description="RT: hold to draw, release to fire an arrow. Damage and speed scale with charge.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeLink1",nameToken="SSBM_LINK_SKILL_1",descriptionToken="SSBM_LINK_SKILL_DESC_1",
                displayName="A Attack",description="LT: neutral A while stationary. LS + LT selects camera-directed attacks; a flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            utility = new MeleeSkillInfo {
                defName="MeleeLink2",nameToken="SSBM_LINK_SKILL_2",descriptionToken="SSBM_LINK_SKILL_DESC_2",
                displayName="Boomerang",description="RB: throw a boomerang that flies out and despawns.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeLink3",nameToken="SSBM_LINK_SKILL_3",descriptionToken="SSBM_LINK_SKILL_DESC_3",
                displayName="Spin Attack",description="LB: launch upward with a rising spin.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeLinkBomb",nameToken="SSBM_LINK_BOMB",descriptionToken="SSBM_LINK_BOMB_DESC",
                displayName="Bomb",description="B: lob a bomb that detonates on the ground.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            extraAlt = new MeleeSkillInfo {
                defName="MeleeLinkShield",nameToken="SSBM_LINK_SHIELD",descriptionToken="SSBM_LINK_SHIELD_DESC",
                displayName="Shield / Air Feint",description="B: hold a shield on the ground or evade once in the air.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        internal static void Set(MeleeSkillSet built) => set = built;
        internal static GenericSkill Get(CharacterBody body, int slot) => set.Get(body, slot);
        internal static bool ShieldSelected(CharacterBody body) => set.ShieldSelected(body);
        internal static NativeSkillDef Expected(CharacterBody body, int slot) => set.Expected(body, slot);
        internal static int Slot(uint move) => set.Slot(move);
        internal static uint Allowed(CharacterBody body) => set.Allowed(body);
    }

    internal static class LinkSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Link",bodyNameToken="SSBM_LINK_NAME",bodyName="MeleeLinkBody",
            defPrefix="MeleeLinkSkin",nameTokenPrefix="SSBM_LINK_SKIN_",unlockPrefix="Skins.MeleeLink.",
            // ftlink.c::ftLk_Init_CostumeStrings order: Nr, Re, Bu, Bk, Wh.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_LINK_SKIN_0", displayName="Default", color=new Color32(92,180,92,255) },
                new MeleeSkinInfo { nameToken="SSBM_LINK_SKIN_1", displayName="Red", color=new Color32(206,80,80,255) },
                new MeleeSkinInfo { nameToken="SSBM_LINK_SKIN_2", displayName="Blue", color=new Color32(80,120,210,255) },
                new MeleeSkinInfo { nameToken="SSBM_LINK_SKIN_3", displayName="Black", color=new Color32(120,120,128,255) },
                new MeleeSkinInfo { nameToken="SSBM_LINK_SKIN_4", displayName="White", color=new Color32(232,232,238,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeLinkBody");
    }

    internal static class LinkDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeLink",bodyName="MeleeLinkBody",displayName="MeleeLinkDisplay",
            nameToken="SSBM_LINK_NAME",subtitleToken="SSBM_LINK_SUBTITLE",descriptionToken="SSBM_LINK_DESCRIPTION",
            outroToken="SSBM_LINK_OUTRO",failToken="SSBM_LINK_FAIL",
            nameStr="Link",subtitleStr="Hero of Hyrule",
            descriptionStr="Link brings his Melee loadout to Petrichor V.\n\nRT: Hero's Bow. LT: contextual A. RB: Boomerang. LB: Spin Attack. B: Bomb, with Shield / Air Feint as the fifth skill alternate.\n\nOffline prototype.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.3f,0.7f,0.3f),
            baseMaxHealth=100f,levelMaxHealth=30f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=106f,
            mainStateType=typeof(LinkMain),driverType=typeof(LinkDriver),
            skills=LinkSkills.Layout,skins=LinkSkins.Layout,
            displayFactory=LinkDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.LinkReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterLink, "Melee Link original CSS grid portrait") : null;
    }

    public sealed class LinkMain : BaseCharacterMain { }
}
