using RoR2;
using RoR2.ContentManagement;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    // Samus's skill identity only. MeleeSurvivor builds the SkillDefs from this
    // layout; the built set backs the shared slot helpers the driver reads.
    internal static class Skills
    {
        static MeleeSkillSet set;
        internal static readonly MeleeSkillLayout Layout = new MeleeSkillLayout
        {
            primary = new MeleeSkillInfo {
                defName="MeleeSamus0",nameToken="SSBM_SKILL_0",descriptionToken="SSBM_SKILL_DESC_0",
                displayName="Charge Shot",description="Walk while charging. Tap again to fire; sprint, jump or X grab to store. Airborne neutral special fires stored charge immediately.",
                icon="MageBodyFireFirebolt",recharge=0.75f,stock=1,combat=true },
            secondary = new MeleeSkillInfo {
                defName="MeleeSamusBomb",nameToken="SSBM_BOMB",descriptionToken="SSBM_BOMB_DESC",
                displayName="Bomb",description="LT: down-B drops Samus's original timed bomb.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=2,combat=true },
            secondaryAlt = new MeleeSkillInfo {
                defName="MeleeShield",nameToken="SSBM_SHIELD",descriptionToken="SSBM_SHIELD_DESC",
                displayName="Shield / Air Feint",description="LT: hold a shield on the ground or evade once in the air. Shield drains when held or hit; it regenerates while released and breaks when depleted.",
                icon="EngiBodyPlaceBubbleShield",recharge=0f,stock=1,combat=false },
            utility = new MeleeSkillInfo {
                defName="MeleeSamus2",nameToken="SSBM_SKILL_2",descriptionToken="SSBM_SKILL_DESC_2",
                displayName="Missile",description="RB: side-B fires a homing missile; a quick direction fires a super missile.",
                icon="EngiBodyPlaceSpiderMine",recharge=1f,stock=2,combat=true },
            special = new MeleeSkillInfo {
                defName="MeleeSamus3",nameToken="SSBM_SKILL_3",descriptionToken="SSBM_SKILL_DESC_3",
                displayName="Screw Attack",description="LB: up-B launches Screw Attack, followed by helpless fall.",
                icon="LoaderBodyChargeFist",recharge=3f,stock=1,combat=true },
            extra = new MeleeSkillInfo {
                defName="MeleeSamus1",nameToken="SSBM_SKILL_1",descriptionToken="SSBM_SKILL_DESC_1",
                displayName="Power Suit",description="B: neutral A while stationary. LS + B selects camera-directed A attacks; a timed flick charges a smash.",
                icon="CommandoBodyFirePistol",recharge=0f,stock=1,combat=true },
            extraName="MeleeExtra",extraTitleToken="SSBM_EXTRA_SLOT",extraTitle="B Skill",extraActionName="Cancel",
        };

        internal static readonly SkillSlot[] Slots = MeleeSkillSet.Slots;
        /// This fighter's built skill layout, for the shared MeleeDriver.BeginMove.
        internal static MeleeSkillSet Built => set;
        internal static void Set(MeleeSkillSet built)=>set=built;
        internal static GenericSkill Get(CharacterBody body,int slot)=>set.Get(body,slot);
        internal static bool ShieldSelected(CharacterBody body)=>set.ShieldSelected(body);
        internal static NativeSkillDef Expected(CharacterBody body,int slot)=>set.Expected(body,slot);
        internal static int Slot(uint move)=>set.Slot(move);
        internal static uint Allowed(CharacterBody body)=>set.Allowed(body);
    }
}
