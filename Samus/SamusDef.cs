using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    // Samus's survivor identity: the shared MeleeSurvivor builds the body,
    // display, skills, skins and survivor registration from this.
    internal static class SamusDef
    {
        internal static MeleeFighterDef Def() => new MeleeFighterDef
        {
            cachedName="MeleeSamus",bodyName="MeleeSamusBody",displayName="MeleeSamusDisplay",
            nameToken="SSBM_SAMUS_NAME",subtitleToken="SSBM_SAMUS_SUBTITLE",descriptionToken="SSBM_SAMUS_DESCRIPTION",
            outroToken="SSBM_SAMUS_OUTRO",failToken="SSBM_SAMUS_FAIL",
            nameStr="Samus",subtitleStr="Intergalactic Bounty Hunter",
            descriptionStr="Samus brings her Melee Power Suit to Petrichor V.\n\nRT: Charge Shot. B: contextual A; LS + B selects camera-directed attacks and a timed flick charges smashes. RB: missiles. LB: Screw Attack. LT: Bomb, with Shield / Air Feint and Morph Ball Roll as its alternates.\n\nA jumps at Commando height, LS click sprints and X aims a 20m Z-grab. X yields to visible interact prompts. Captured enemies use LS + B/RT to throw or neutral B/RT to pummel. Bosses pull Samus instead; hold X and use contextual B, or A to detach.\n\nOffline prototype: advanced shield rules, full material fidelity, capture escape rules and item integration remain in progress.",
            outroStr="..and so she left, mission complete.",failStr="..and so she vanished, beyond the blast zone.",
            bodyColor=new Color(1f,0.48f,0.06f),
            baseMaxHealth=140f,levelMaxHealth=42f,baseArmor=Plugin.Armor.Value,baseJumpCount=2,
            sortPosition=102f,
            fighterKind=MeleeNative.FighterSamus,assetsDir=()=>Samus.Plugin.AssetsDir,assetsReady=()=>Samus.Plugin.AssetsReady,
            mainStateType=typeof(SamusMain),driverType=typeof(SamusDriver),
            skills=Skills.Layout,skins=Skins.Layout,
            displayFactory=SamusDisplay.Make,
            portraitFactory=Portrait,
            glyphs=new Dictionary<SkillSlot,PadButton[]> {
                [SkillSlot.Primary]=new[]{PadButton.RT},
                [SkillSlot.Secondary]=new[]{PadButton.LT},
                [SkillSlot.Utility]=new[]{PadButton.RB},
                [SkillSlot.Special]=new[]{PadButton.LB} },
        };

        static Texture2D Portrait() => Plugin.AssetsReady ? MeleePortrait.Load(Plugin.AssetsDir, MeleeNative.FighterSamus, "Melee Samus original CSS grid portrait") : null;
    }
}
