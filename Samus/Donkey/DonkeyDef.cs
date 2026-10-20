using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Donkey
{
    internal static class DonkeySkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeDonkey0",nameToken="SSBM_DONKEY_SKILL_0",descriptionToken="SSBM_DONKEY_SKILL_DESC_0",
                displayName="Giant Punch",description="RT: a slow, devastating punch.",
                icon="CommandoBodyFirePistol",recharge=0.5f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeDonkey1",nameToken="SSBM_DONKEY_SKILL_1",descriptionToken="SSBM_DONKEY_SKILL_DESC_1",
                displayName="A Attack",description="LT: neutral A while stationary. LS + LT selects camera-directed attacks; a flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            utility = new MeleeSkillInfo {
                defName="MeleeDonkey2",nameToken="SSBM_DONKEY_SKILL_2",descriptionToken="SSBM_DONKEY_SKILL_DESC_2",
                displayName="Headbutt",description="RB: a quick horizontal lunge.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeDonkey3",nameToken="SSBM_DONKEY_SKILL_3",descriptionToken="SSBM_DONKEY_SKILL_DESC_3",
                displayName="Spinning Kong",description="LB: spin upward and recover.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeDonkeySlap",nameToken="SSBM_DONKEY_SLAP",descriptionToken="SSBM_DONKEY_SLAP_DESC",
                displayName="Hand Slap",description="B: a ground-pounding wave.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=1,combat=true },
            extraAlt = new MeleeSkillInfo {
                defName="MeleeDonkeyShield",nameToken="SSBM_DONKEY_SHIELD",descriptionToken="SSBM_DONKEY_SHIELD_DESC",
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

    internal static class DonkeySkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Donkey Kong",bodyNameToken="SSBM_DONKEY_NAME",bodyName="MeleeDonkeyBody",
            defPrefix="MeleeDonkeySkin",nameTokenPrefix="SSBM_DONKEY_SKIN_",unlockPrefix="Skins.MeleeDonkey.",
            // ftdonkey.c::ftDk_Init_CostumeStrings order: Nr, Bk, Re, Bu, Gr.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_DONKEY_SKIN_0", displayName="Default", color=new Color32(150,90,50,255) },
                new MeleeSkinInfo { nameToken="SSBM_DONKEY_SKIN_1", displayName="Black", color=new Color32(60,52,48,255) },
                new MeleeSkinInfo { nameToken="SSBM_DONKEY_SKIN_2", displayName="Red", color=new Color32(210,72,72,255) },
                new MeleeSkinInfo { nameToken="SSBM_DONKEY_SKIN_3", displayName="Blue", color=new Color32(72,120,232,255) },
                new MeleeSkinInfo { nameToken="SSBM_DONKEY_SKIN_4", displayName="Green", color=new Color32(88,180,88,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleeDonkeyBody");
    }

    internal static class DonkeyDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeDonkey",bodyName="MeleeDonkeyBody",displayName="MeleeDonkeyDisplay",
            nameToken="SSBM_DONKEY_NAME",subtitleToken="SSBM_DONKEY_SUBTITLE",descriptionToken="SSBM_DONKEY_DESCRIPTION",
            outroToken="SSBM_DONKEY_OUTRO",failToken="SSBM_DONKEY_FAIL",
            nameStr="Donkey Kong",subtitleStr="King of the Jungle",
            descriptionStr="Donkey Kong brings his Melee loadout to Petrichor V.\n\nRT: Giant Punch. LT: contextual A. RB: Headbutt. LB: Spinning Kong. B: Hand Slap, with Shield / Air Feint as the fifth skill alternate.\n\nOffline prototype.",
            outroStr="..and so he left, mission complete.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.55f,0.35f,0.2f),
            baseMaxHealth=130f,levelMaxHealth=39f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=109f,
            mainStateType=typeof(DonkeyMain),driverType=typeof(DonkeyDriver),
            skills=DonkeySkills.Layout,skins=DonkeySkins.Layout,
            displayFactory=DonkeyDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.DonkeyReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterDonkey, "Melee Donkey Kong original CSS grid portrait") : null;
    }

    public sealed class DonkeyMain : BaseCharacterMain { }
}
