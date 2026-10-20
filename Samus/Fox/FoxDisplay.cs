using System;
using Ror2Common;

namespace Fox
{
    // Fox's character-select presenter: the shared MeleeSelectSource plays the
    // announcer call and up-B intro, then returns Fox to idle. The up special is
    // Fire Fox, which shares the generic SpecialHi (move id 23) detection.
    internal static class FoxDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.FoxReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterFox, FoxSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
