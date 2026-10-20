using System;
using Ror2Common;

namespace Captain
{
    // Captain Falcon's character-select presenter: the shared MeleeSelectSource
    // plays the announcer call and up-B intro (Falcon Dive), then returns to idle.
    internal static class CaptainDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.CaptainReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterCaptain, CaptainSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterCaptain, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
