using System;
using Ror2Common;

namespace Donkey
{
    // Donkey Kong's character-select presenter: the shared MeleeSelectSource
    // plays the announcer call and up-B intro (Spinning Kong), then returns to idle.
    internal static class DonkeyDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.DonkeyReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterDonkey, DonkeySkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterDonkey, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
