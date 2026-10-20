using System;
using Ror2Common;
using UnityEngine;

namespace Fox
{
    // Fox's character-select preview: an idle pose, no intro move. The shared
    // MeleeSelectDisplay owns framing; this supplies the idle source.
    internal sealed class FoxDisplay : IMeleeSelectSource
    {
        MeleeFighterView view;
        uint costume;
        readonly IntPtr sim;
        readonly float[] anchorMatrix = new float[16];
        Vector3 drawOffset;
        static readonly Quaternion previewRotation = Quaternion.identity;

        public bool IntroFinished => true;
        public int AudioRate => 32000;
        public Vector3 DrawOffset { set { drawOffset = value; } }
        public ModelView Models => view.Models;
        public float Scale => Samus.Plugin.MetresPerUnit.Value;
        public float RiseMetres => 0f;
        public Vector3 PoseAnchor
        {
            get
            {
                if (MeleeNative.ssbm_sim_state(sim, out var state) != 1 || MeleeNative.ssbm_sim_bone(sim, 4, anchorMatrix) != 1) throw new InvalidOperationException(MeleeNative.Error);
                var facing = new Vector3(state.facing.x, 0, state.facing.z);
                var posed = new Vector3(anchorMatrix[12] - state.position.x, anchorMatrix[13] - state.position.y, anchorMatrix[14] - state.position.z);
                if (facing.sqrMagnitude > 0.001f) posed = Quaternion.Inverse(Quaternion.LookRotation(facing)) * posed;
                return previewRotation * posed;
            }
        }

        public static IDisplayActor Make() => Samus.Plugin.FoxReady ? new MeleeSelectDisplay(new FoxDisplay(), intro: false) : null;

        FoxDisplay()
        {
            sim = MeleeNative.ssbm_sim_new_fighter(NativeLib.CString(Samus.Plugin.AssetsDir), default, MeleeNative.FighterFox);
            if (sim == IntPtr.Zero) throw new InvalidOperationException(MeleeNative.Error);
            costume = FoxSkins.Selected();
            view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, costume);
        }

        public void BeginIntro() { }
        public bool RenderAudio(float[] stereo) => MeleeNative.ssbm_sim_audio(sim, stereo, (uint)stereo.Length / 2) == stereo.Length / 2;
        public void Step(float dt)
        {
            uint selected = FoxSkins.Selected();
            if (selected != costume) { var replacement = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterFox, selected); view.Dispose(); view = replacement; costume = selected; }
            view.IdlePose();
            view.LocalPosition = drawOffset;
            view.LocalRotation = previewRotation;
        }
        public void Dispose() { view.Dispose(); MeleeNative.ssbm_sim_free(sim); }
    }
}
