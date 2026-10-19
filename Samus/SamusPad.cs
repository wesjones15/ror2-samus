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
        internal MeleeNative.Pad Take(uint allowed,bool locked=false){var p=pad.Take(allowed,locked);return new MeleeNative.Pad{movement=new MeleeNative.Vec3(p.movement.x,p.movement.y,p.movement.z),aim=new MeleeNative.Vec3(p.aim.x,p.aim.y,p.aim.z),buttons=p.buttons,allowed=p.allowed};}
    }
}

