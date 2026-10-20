using System;
using Ror2Common;

namespace Link
{
    // Link's character-select presenter: the shared MeleeSelectSource plays the
    // announcer call and up-B intro (Spin Attack), then returns Link to idle.
    internal static class LinkDisplay
    {
        internal static IDisplayActor Make()
        {
            if (!Samus.Plugin.LinkReady) return null;
            var source = new MeleeSelectSource(
                Samus.Plugin.AssetsDir, MeleeNative.FighterLink, LinkSkins.Selected,
                Samus.Plugin.MetresPerUnit.Value, logEffects: Samus.Plugin.LogEffects.Value,
                effectView: model => MeleeFighterView.Effect(Samus.Plugin.AssetsDir, MeleeNative.FighterLink, model));
            return new MeleeSelectDisplay(source);
        }
    }
}
