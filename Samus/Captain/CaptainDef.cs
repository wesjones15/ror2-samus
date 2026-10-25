using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Captain
{
    internal static class CaptainSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeCaptain0",nameToken="SSBM_CAPTAIN_SKILL_0",descriptionToken="SSBM_CAPTAIN_SKILL_DESC_0",
                displayName="Falcon Punch",description="Wind up, then release a flaming punch.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeCaptainKick",nameToken="SSBM_CAPTAIN_KICK",descriptionToken="SSBM_CAPTAIN_KICK_DESC",
                displayName="Falcon Kick",description="A forward flaming kick; in the air it dives.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            secondaryAlt = new MeleeSkillInfo {
                defName="MeleeCaptainShield",nameToken="SSBM_CAPTAIN_SHIELD",descriptionToken="SSBM_CAPTAIN_SHIELD_DESC",
                displayName="Shield / Air Feint",description="Hold to shield on the ground; evade in the air.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            utility = new MeleeSkillInfo {
                defName="MeleeCaptain2",nameToken="SSBM_CAPTAIN_SKILL_2",descriptionToken="SSBM_CAPTAIN_SKILL_DESC_2",
                displayName="Raptor Boost",description="Lunge; hitting an enemy turns it into an uppercut, or a spike in the air.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeCaptain3",nameToken="SSBM_CAPTAIN_SKILL_3",descriptionToken="SSBM_CAPTAIN_SKILL_DESC_3",
                displayName="Falcon Dive",description="Leap and grab; on hit, burn and launch the target, then rebound.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeCaptain1",nameToken="SSBM_CAPTAIN_SKILL_1",descriptionToken="SSBM_CAPTAIN_SKILL_DESC_1",
                displayName="A Attack",description="Neutral jab; flick to charge a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
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

    internal static class CaptainSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Captain Falcon",bodyNameToken="SSBM_CAPTAIN_NAME",bodyName="MeleeCaptainBody",
            defPrefix="MeleeCaptainSkin",nameTokenPrefix="SSBM_CAPTAIN_SKIN_",unlockPrefix="Skins.MeleeCaptain.",
            // ftcaptain.c::ftCa_Init_CostumeStrings order: Nr, Gy, Re, Wh, Gr, Bu.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_0", displayName="Default", color=new Color32(64,96,200,255) },
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_1", displayName="Grey", color=new Color32(150,150,158,255) },
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_2", displayName="Red", color=new Color32(210,72,72,255) },
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_3", displayName="White", color=new Color32(236,236,240,255) },
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_4", displayName="Green", color=new Color32(88,180,88,255) },
                new MeleeSkinInfo { nameToken="SSBM_CAPTAIN_SKIN_5", displayName="Blue", color=new Color32(72,120,232,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeCaptainBody");
    }

    internal static class CaptainDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeCaptain",bodyName="MeleeCaptainBody",displayName="MeleeCaptainDisplay",
            nameToken="SSBM_CAPTAIN_NAME",subtitleToken="SSBM_CAPTAIN_SUBTITLE",descriptionToken="SSBM_CAPTAIN_DESCRIPTION",
            outroToken="SSBM_CAPTAIN_OUTRO",failToken="SSBM_CAPTAIN_FAIL",
            nameStr="Captain Falcon",subtitleStr="F-Zero Racer",
            descriptionStr="Captain Falcon brings his Melee loadout to Petrichor V. Falcon Punch, Falcon Kick, Raptor Boost, and Falcon Dive, plus a shield that doubles as an air feint.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.2f,0.35f,0.75f),
            baseMaxHealth=110f,levelMaxHealth=33f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=105f,
            fighterKind=MeleeNative.FighterCaptain,assetsDir=()=>Samus.Plugin.AssetsDir,assetsReady=()=>Samus.Plugin.CaptainReady,
            mainStateType=typeof(CaptainMain),driverType=typeof(CaptainDriver),
            skills=CaptainSkills.Layout,skins=CaptainSkins.Layout,
            displayFactory=CaptainDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.CaptainReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterCaptain, "Melee Captain Falcon original CSS grid portrait") : null;
    }

    public sealed class CaptainMain : BaseCharacterMain { }
}
