using RoR2;
using RoR2.ContentManagement;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    internal static class Skills
    {
        internal static readonly NativeSkillDef[] Definitions=new NativeSkillDef[4];
        internal static NativeSkillDef DownB,Defense;
        internal const string ExtraName="MeleeExtra";
        internal static readonly SkillSlot[] Slots={SkillSlot.Primary,SkillSlot.Secondary,SkillSlot.Utility,SkillSlot.Special};
        static readonly string[] Names={"Charge Shot","Power Suit","Missile","Screw Attack"};
        static readonly string[] Descriptions={
            "Walk while charging. Tap again to fire; sprint, jump or X grab to store. Airborne neutral special fires stored charge immediately.",
            "LT: neutral A while stationary. LS + LT selects camera-directed A attacks; a timed flick charges a smash.",
            "RB: side-B fires a homing missile; a quick direction fires a super missile.",
            "LB: up-B launches Screw Attack, followed by helpless fall."};
        internal static void Add(GameObject body,ContentPack pack)
        {
            float[] cooldowns={0.75f,0f,1f,3f};int[] stock={1,1,2,1};
            string[] icons={"MageBodyFireFirebolt","CommandoBodyFirePistol","EngiBodyPlaceSpiderMine","LoaderBodyChargeFist"};
            for(int i=0;i<4;i++) {
                var d=NativeSkills.Make("MeleeSamus"+i,"SSBM_SKILL_"+i,"SSBM_SKILL_DESC_"+i,null,cooldowns[i],stock[i],true);
                Definitions[i]=d;NativeSkills.SetFamily(body,Slots[i],d,pack);VanillaIcons.Use(d,icons[i]);
            }
            DownB=NativeSkills.Make("MeleeSamusBomb","SSBM_BOMB","SSBM_BOMB_DESC",null,1f,2,true);
            Defense=NativeSkills.Make("MeleeShield","SSBM_SHIELD","SSBM_SHIELD_DESC",null,0f,1,false);
            NativeSkills.AddExtra(body,ExtraName,"SSBM_EXTRA_SLOT",pack,DownB,Defense);
            var hud=body.AddComponent<ExtraSkillIcon>();hud.skillName=ExtraName;hud.actionName="Cancel";
            VanillaIcons.Use(DownB,"EngiBodyPlaceSpiderMine");VanillaIcons.Use(Defense,"EngiBodyPlaceBubbleShield");
        }
        internal static void Tokens(Language l){for(int i=0;i<4;i++){l.SetStringByToken("SSBM_SKILL_"+i,Names[i]);l.SetStringByToken("SSBM_SKILL_DESC_"+i,Descriptions[i]);}l.SetStringByToken("SSBM_EXTRA_SLOT","B Skill");l.SetStringByToken("SSBM_BOMB","Bomb");l.SetStringByToken("SSBM_BOMB_DESC","B: down-B drops Samus's original timed bomb.");l.SetStringByToken("SSBM_SHIELD","Shield / Air Feint");l.SetStringByToken("SSBM_SHIELD_DESC","B: hold a shield on the ground or evade once in the air. Shield drains when held or hit; it regenerates while released and breaks when depleted.");}
        internal static GenericSkill Get(CharacterBody body,int slot)=>slot==4?body.skillLocator.FindSkill(ExtraName):body.skillLocator.GetSkill(Slots[slot]);
        internal static bool ShieldSelected(CharacterBody body)=>Get(body,4)?.skillDef==Defense;
        internal static NativeSkillDef Expected(CharacterBody body,int slot)=>slot==4?(ShieldSelected(body)?Defense:DownB):Definitions[slot];
        internal static int Slot(uint move)=>move==20?0:move==21||move==22?2:move==23?3:move==24?4:1;
        internal static uint Allowed(CharacterBody body)
        {
            uint bits=0;for(int i=0;i<5;i++)if(NativeSkills.Ready(Get(body,i),Expected(body,i)))bits|=1u<<i;return bits;
        }
    }
}
