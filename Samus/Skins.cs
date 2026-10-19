using RoR2;
using Ror2Common;
using UnityEngine;

namespace Samus
{
    // Samus's costume identity only. MeleeSurvivor builds the SkinDefs from this
    // layout; Selected() reads the loadout for Samus's body.
    internal static class Skins
    {
        internal static readonly MeleeSkinLayout Layout = new MeleeSkinLayout
        {
            fighterName="Samus",bodyNameToken="SSBM_SAMUS_NAME",bodyName="MeleeSamusBody",
            defPrefix="MeleeSamusSkin",nameTokenPrefix="SSBM_SKIN_",unlockPrefix="Skins.MeleeSamus.",
            skins=new MeleeSkinInfo[] {
                new MeleeSkinInfo { nameToken="SSBM_SKIN_0", displayName="Power Suit", color=new Color32(245,125,25,255) },
                new MeleeSkinInfo { nameToken="SSBM_SKIN_1", displayName="Pink", color=new Color32(225,125,180,255) },
                new MeleeSkinInfo { nameToken="SSBM_SKIN_2", displayName="Black", color=new Color32(75,70,95,255) },
                new MeleeSkinInfo { nameToken="SSBM_SKIN_3", displayName="Green", color=new Color32(80,190,90,255) },
                new MeleeSkinInfo { nameToken="SSBM_SKIN_4", displayName="Lavender", color=new Color32(170,155,235,255) },
            },
        };

        internal static uint Selected()=>MeleeSkins.Selected("MeleeSamusBody");
    }
}
