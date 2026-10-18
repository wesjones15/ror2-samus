using RoR2;
using Ror2Common;
using UnityEngine;
namespace Samus {
    internal sealed class SamusPad {
        readonly MeleePad pad=new MeleePad();
        internal bool GrabHeld=>pad.GrabHeld;
        internal bool JumpHeld=>pad.JumpHeld;
        internal void ReleaseInteraction()=>pad.ReleaseInteraction();
        internal void Read(CharacterBody body,InputBankTest input,bool grabbing=false,Vector3? cameraAim=null,bool shield=false)=>pad.Read(body,input,grabbing,cameraAim,shield);
        internal Native.Pad Take(uint allowed){var p=pad.Take(allowed);return new Native.Pad{movement=new Native.Vec3(p.movement.x,p.movement.y,p.movement.z),aim=new Native.Vec3(p.aim.x,p.aim.y,p.aim.z),buttons=p.buttons,allowed=p.allowed};}
    }
}
