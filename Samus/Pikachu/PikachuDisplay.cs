using System;
using Ror2Common;

namespace Pikachu
{
    // Pikachu's character-select presenter: the shared MeleeSelectSource plays the
    // announcer call and intro, then returns him to idle.
    internal static class PikachuDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.PikachuReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, PikachuSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, model),
                introUp: true, speedScale: 0.625f); // undo the host zip scale so the preview matches the source reach
            return new MeleeSelectDisplay(source);
        }
    }
}
