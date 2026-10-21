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
        // Pikachu's specials sit on a shuffled layout (native moves: 20 Thunder Shock,
        // 21 Skull Bash, 23 Quick Attack, 24 Thunder): Thunder takes RB, Quick Attack LB
        // and Skull Bash the B slot. libssbm's Pikachu `Behavior::slot` agrees.
        internal static int SlotForMove(uint move) =>
            move == 20 ? 0 : move == 21 || move == 22 ? 4 : move == 23 ? 3 : move == 24 ? 2 : 1;

        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleePikachu0",nameToken="SSBM_PIKACHU_SKILL_0",descriptionToken="SSBM_PIKACHU_SKILL_DESC_0",
                displayName="Thunder Shock",description="RT: shoot an electric orb along your crosshair. It bounces off every surface and fizzles after ten ground bounces.",
                icon="CommandoBodyFirePistol",recharge=0.25f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleePikachu1",nameToken="SSBM_PIKACHU_SKILL_1",descriptionToken="SSBM_PIKACHU_SKILL_DESC_1",
                displayName="A Attack",description="LT: neutral A while stationary. LS + LT selects camera-directed attacks; a flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            utility = new MeleeSkillInfo {
                defName="MeleePikachu2",nameToken="SSBM_PIKACHU_SKILL_2",descriptionToken="SSBM_PIKACHU_SKILL_DESC_2",
                displayName="Thunder",description="RB: call a lightning bolt down on yourself. The bolt shocks anything in its column; the strike then electrifies everything around Pikachu.",
                icon="CallAirstrikeAlt",recharge=5f,stock=1,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleePikachu3",nameToken="SSBM_PIKACHU_SKILL_3",descriptionToken="SSBM_PIKACHU_SKILL_DESC_3",
                displayName="Quick Attack",description="LB: zip to your lock-on (the enemy nearest your crosshair) in two zips, or along your stick and camera. Zip again by turning your stick or pressing LB.",
                icon="MercBodyAssaulter",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleePikachuSkullBash",nameToken="SSBM_PIKACHU_SKULLBASH",descriptionToken="SSBM_PIKACHU_SKULLBASH_DESC",
                displayName="Skull Bash",description="B: hold to charge, release to lunge forward. Damage and distance grow with the charge; it stops on what it hits.",
                icon="LoaderBodyChargeFist",recharge=1.5f,stock=1,combat=true },
            extraAlt = new MeleeSkillInfo {
                defName="MeleePikachuShield",nameToken="SSBM_PIKACHU_SHIELD",descriptionToken="SSBM_PIKACHU_SHIELD_DESC",
                displayName="Shield / Air Feint",description="B: hold a shield on the ground or evade once in the air.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
            slotForMove=SlotForMove,
        };

        /// RT neutral special, LT A, RB Thunder (down special), LB Quick Attack (up
        /// special), B Skull Bash (side special) or the shield alternate.
        internal static readonly MeleeControlLayout Controls = new MeleeControlLayout
        {
            Primary=MeleeControls.NeutralB,Secondary=MeleeControls.Attack,
            Utility=MeleeControls.DownB,Special=MeleeControls.UpB,
            Extra=MeleeControls.SideB,ExtraAlt=MeleeControls.Defend,
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        internal static void Set(MeleeSkillSet built) => set = built;
        internal static GenericSkill Get(CharacterBody body, int slot) => set.Get(body, slot);
        internal static bool ShieldSelected(CharacterBody body) => set.ShieldSelected(body);
        internal static NativeSkillDef Expected(CharacterBody body, int slot) => set.Expected(body, slot);
        internal static int Slot(uint move) => set.Slot(move);
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
            descriptionStr="Pikachu brings his Melee loadout to Petrichor V.\n\nRT: Thunder Shock. LT: contextual A. RB: Thunder. LB: Quick Attack, homing on your lock-on. B: Skull Bash, with Shield / Air Feint as the fifth skill alternate.\n\nOffline prototype.",
            outroStr="..and so he left, shocked and satisfied.",failStr="..and so he vanished, beyond the blast zone.",
            bodyColor=new Color(0.97f,0.84f,0.24f),
            baseMaxHealth=90f,levelMaxHealth=27f,baseArmor=Samus.Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=107f,
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
