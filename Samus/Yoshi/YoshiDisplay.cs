using System;
using Ror2Common;

namespace Yoshi
{
    // Yoshi's character-select presenter: the shared MeleeSelectSource plays the
    // announcer call and up-B intro (Egg Throw), then returns Yoshi to idle.
    internal static class YoshiDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.YoshiReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, YoshiSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
