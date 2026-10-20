using System;
using Ror2Common;

namespace Luigi
{
    // Luigi's character-select presenter: the shared MeleeSelectSource plays the
    // announcer call and up-B intro (Super Jump Punch), then returns Luigi to idle.
    internal static class LuigiDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.LuigiReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterLuigi, LuigiSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterLuigi, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
