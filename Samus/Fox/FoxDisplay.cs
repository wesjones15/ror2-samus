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
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, model),
                // Fire Fox follows the aim, so aim it straight up or the preview would fly
                // out of the frame; scale the preview back to the source reach.
                introUp: true, speedScale: 0.5f);
            return new MeleeSelectDisplay(source);
        }
    }
}
