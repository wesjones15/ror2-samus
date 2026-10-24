using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Pikachu
{
    internal static class PikachuSkills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleePikachu0",nameToken="SSBM_PIKACHU_SKILL_0",descriptionToken="SSBM_PIKACHU_SKILL_DESC_0",
                displayName="Thunder Jolt",description="RT: fire a bouncing electric orb.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            primaryAlt = new MeleeSkillInfo {
                defName="MeleePikachuEggLay",nameToken="SSBM_PIKACHU_EGGLAY",descriptionToken="SSBM_PIKACHU_EGGLAY_DESC",
                displayName="Egg Lay (Yoshi)",description="RT: lash Yoshi's tongue to swallow enemies and lay eggs behind you. Recharges when it catches.",
                icon="CommandoBodyFirePistol",recharge=3f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleePikachu2",nameToken="SSBM_PIKACHU_SKILL_2",descriptionToken="SSBM_PIKACHU_SKILL_DESC_2",
                displayName="Thunder",description="LT: call lightning onto yourself, then shock the area.",
                icon="CallAirstrikeAlt",recharge=5f,stock=1,combat=true },
            secondaryAlt = new MeleeSkillInfo {
                defName="MeleePikachuShield",nameToken="SSBM_PIKACHU_SHIELD",descriptionToken="SSBM_PIKACHU_SHIELD_DESC",
                displayName="Shield / Air Feint",description="LT: hold to shield on the ground; in the air, evade once.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            utility = new MeleeSkillInfo {
                defName="MeleePikachuSkullBash",nameToken="SSBM_PIKACHU_SKULLBASH",descriptionToken="SSBM_PIKACHU_SKULLBASH_DESC",
                displayName="Skull Bash",description="RB: hold to charge, release to lunge. Stops on impact.",
                icon="LoaderBodyChargeFist",recharge=1.5f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleePikachu3",nameToken="SSBM_PIKACHU_SKILL_3",descriptionToken="SSBM_PIKACHU_SKILL_DESC_3",
                displayName="Quick Attack",description="LB: zip twice in a chosen direction, damaging enemies passed. Two charges.",
                icon="MercBodyAssaulter",recharge=1f,stock=2,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleePikachu1",nameToken="SSBM_PIKACHU_SKILL_1",descriptionToken="SSBM_PIKACHU_SKILL_DESC_1",
                displayName="A Attack",description="B: neutral A. LS + B aims attacks; flick to charge a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        /// This fighter's built skill layout, for the shared MeleeDriver.BeginMove.
        internal static MeleeSkillSet Built => set;
        internal static void Set(MeleeSkillSet built) => set = built;
        internal static GenericSkill Get(CharacterBody body, int slot) => set.Get(body, slot);
        internal static bool ShieldSelected(CharacterBody body) => set.ShieldSelected(body);
        /// The RT slot's loadout holds Yoshi's Egg Lay instead of Thunder Jolt.
        internal static bool EggLaySelected(CharacterBody body) => set != null && set.PrimaryAltSelected(body);
        // A stored Quick Attack charge is available (stock, not cooldown).
        internal static bool QuickReady(CharacterBody body)
            => set != null && NativeSkills.Ready(set.Get(body, 3), set.Expected(body, 3));
        internal static NativeSkillDef Expected(CharacterBody body, int slot) => set.Expected(body, slot);
        internal static int Slot(CharacterBody body, uint move) => set.Slot(body, move);
        internal static uint Allowed(CharacterBody body) => set.Allowed(body);
    }

    internal static class PikachuSkins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Pikachu",bodyNameToken="SSBM_PIKACHU_NAME",bodyName="MeleePikachuBody",
            defPrefix="MeleePikachuSkin",nameTokenPrefix="SSBM_PIKACHU_SKIN_",unlockPrefix="Skins.MeleePikachu.",
            // ftpikachu.c::ftPk_Init_CostumeStrings order: Nr, Re, Bu, Gr.
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_PIKACHU_SKIN_0", displayName="Default", color=new Color32(247,214,60,255) },
                new MeleeSkinInfo { nameToken="SSBM_PIKACHU_SKIN_1", displayName="Red", color=new Color32(222,76,70,255) },
                new MeleeSkinInfo { nameToken="SSBM_PIKACHU_SKIN_2", displayName="Blue", color=new Color32(84,132,224,255) },
                new MeleeSkinInfo { nameToken="SSBM_PIKACHU_SKIN_3", displayName="Green", color=new Color32(94,182,96,255) },
            },
        };
        internal static uint Selected() => MeleeSkins.Selected("MeleePikachuBody");
    }

    internal static class PikachuDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleePikachu",bodyName="MeleePikachuBody",displayName="MeleePikachuDisplay",
            nameToken="SSBM_PIKACHU_NAME",subtitleToken="SSBM_PIKACHU_SUBTITLE",descriptionToken="SSBM_PIKACHU_DESCRIPTION",
            outroToken="SSBM_PIKACHU_OUTRO",failToken="SSBM_PIKACHU_FAIL",
            nameStr="Pikachu",subtitleStr="The Electric Mouse Pokemon",
            descriptionStr="Pikachu brings his Melee loadout to Petrichor V.\n\nRT: Thunder Jolt, with Yoshi's Egg Lay as its alternate. B: contextual A. RB: Skull Bash. LB: Quick Attack, camera-vector zips with two charges and pass-through damage. LT: Thunder, with Shield / Air Feint as its alternate.\n\nOffline prototype.",
            outroStr="..and so he left, shocked and satisfied.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.97f,0.84f,0.24f),
            baseMaxHealth=90f,levelMaxHealth=27f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=107f,
            fighterKind=MeleeNative.FighterPikachu,assetsDir=()=>Samus.Plugin.AssetsDir,assetsReady=()=>Samus.Plugin.PikachuReady,
            mainStateType=typeof(PikachuMain),driverType=typeof(PikachuDriver),
            skills=PikachuSkills.Layout,skins=PikachuSkins.Layout,
            displayFactory=PikachuDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Samus.Plugin.PikachuReady ? MeleePortrait.Load(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, "Melee Pikachu original CSS grid portrait") : null;
    }

    public sealed class PikachuMain : BaseCharacterMain { }
}
