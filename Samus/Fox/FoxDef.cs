using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Fox
{
    internal static class FoxSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeFox0",nameToken="SSBM_FOX_SKILL_0",descriptionToken="SSBM_FOX_SKILL_DESC_0",
                displayName="Blaster",description="RT: fire Fox's laser straight ahead.",
                icon="CommandoBodyFirePistol",recharge=0.5f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeFox1",nameToken="SSBM_FOX_SKILL_1",descriptionToken="SSBM_FOX_SKILL_DESC_1",
                displayName="A Attack",description="LT: neutral A while stationary. LS + LT selects camera-directed attacks; a flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            utility = new MeleeSkillInfo {
                defName="MeleeFox2",nameToken="SSBM_FOX_SKILL_2",descriptionToken="SSBM_FOX_SKILL_DESC_2",
                displayName="Fox Illusion",description="RB: a quick horizontal dash.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=2,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeFox3",nameToken="SSBM_FOX_SKILL_3",descriptionToken="SSBM_FOX_SKILL_DESC_3",
                displayName="Fire Fox",description="LB: charge and launch upward.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeFoxReflector",nameToken="SSBM_FOX_REFLECTOR",descriptionToken="SSBM_FOX_REFLECTOR_DESC",
                displayName="Reflector",description="B: a frame-1 shine that holds Fox in place.",
                icon="EngiBodyPlaceBubbleShield",recharge=1f,stock=1,combat=true },
            extraAlt = new MeleeSkillInfo {
                defName="MeleeFoxShield",nameToken="SSBM_FOX_SHIELD",descriptionToken="SSBM_FOX_SHIELD_DESC",
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

    internal static class FoxSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Fox",bodyNameToken="SSBM_FOX_NAME",bodyName="MeleeFoxBody",
            defPrefix="MeleeFoxSkin",nameTokenPrefix="SSBM_FOX_SKIN_",unlockPrefix="Skins.MeleeFox.",
            // ftfox.c::ftFx_Init_CostumeStrings order: Nr, Or, La, Gr.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_FOX_SKIN_0", displayName="Default", color=new Color32(225,228,238,255) },
                new MeleeSkinInfo { nameToken="SSBM_FOX_SKIN_1", displayName="Orange", color=new Color32(232,136,56,255) },
                new MeleeSkinInfo { nameToken="SSBM_FOX_SKIN_2", displayName="Lavender", color=new Color32(178,140,214,255) },
                new MeleeSkinInfo { nameToken="SSBM_FOX_SKIN_3", displayName="Green", color=new Color32(92,172,92,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeFoxBody");
    }

    internal static class FoxDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeFox",bodyName="MeleeFoxBody",displayName="MeleeFoxDisplay",
            nameToken="SSBM_FOX_NAME",subtitleToken="SSBM_FOX_SUBTITLE",descriptionToken="SSBM_FOX_DESCRIPTION",
            outroToken="SSBM_FOX_OUTRO",failToken="SSBM_FOX_FAIL",
            nameStr="Fox",subtitleStr="Leader of Star Fox",
            descriptionStr="Fox brings his Melee loadout to Petrichor V.\n\nRT: Blaster. LT: contextual A. RB: Fox Illusion. LB: Fire Fox. B: Reflector, with Shield / Air Feint as the fifth skill alternate.\n\nOffline prototype.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.85f,0.6f,0.2f),
            baseMaxHealth=110f,levelMaxHealth=33f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=103f,
            mainStateType=typeof(FoxMain),driverType=typeof(FoxDriver),
            skills=FoxSkills.Layout,skins=FoxSkins.Layout,
            displayFactory=FoxDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.FoxReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, "Melee Fox original CSS grid portrait") : null;
    }

    public sealed class FoxMain : BaseCharacterMain { }
}
