using RoR2;
using RoR2.ContentManagement;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    internal static class Skills
    {
        internal static readonly NativeSkillDef[] Definitions=new NativeSkillDef[4];
        internal static readonly SkillSlot[] Slots={SkillSlot.Primary,SkillSlot.Secondary,SkillSlot.Utility,SkillSlot.Special};
        static readonly string[] Names={"Power Suit","Charge Shot","Missile / Bomb","Screw Attack"};
        static readonly string[] Descriptions={
            "Directional attacks. Flick a direction or up/down modifier with attack, hold to charge a smash, then release.",
            "Neutral special starts charging. Tap again to fire; jump or cancel to store. Airborne neutral special fires stored charge immediately.",
            "Side special fires a homing missile; a quick direction fires a super missile. Down + special drops a timed bomb.",
            "Up + special launches a spinning attack, followed by helpless fall until landing."};
        internal static void Add(GameObject body,ContentPack pack)
        {
            float[] cooldowns={0f,3f,2f,6f};int[] stock={1,1,2,1};
            string[] icons={"CommandoBodyFirePistol","MageBodyFireFirebolt","EngiBodyPlaceSpiderMine","LoaderBodyChargeFist"};
            for(int i=0;i<4;i++) {
                var d=NativeSkills.Make("MeleeSamus"+i,"SSBM_SKILL_"+i,"SSBM_SKILL_DESC_"+i,null,cooldowns[i],stock[i],true);
                Definitions[i]=d;NativeSkills.SetFamily(body,Slots[i],d,pack);VanillaIcons.Use(d,icons[i]);
            }
        }
        internal static void Tokens(Language l){for(int i=0;i<4;i++){l.SetStringByToken("SSBM_SKILL_"+i,Names[i]);l.SetStringByToken("SSBM_SKILL_DESC_"+i,Descriptions[i]);}}
        internal static int Slot(uint move)=>move==20?1:move==21||move==22||move==24?2:move==23?3:0;
        internal static uint Allowed(CharacterBody body)
        {
            uint bits=0;for(int i=0;i<4;i++)if(NativeSkills.Ready(body.skillLocator.GetSkill(Slots[i]),Definitions[i]))bits|=1u<<i;return bits;
        }
    }
}
