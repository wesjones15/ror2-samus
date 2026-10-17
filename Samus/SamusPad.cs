using Rewired;
using RoR2;
using UnityEngine;

namespace Samus
{
    internal sealed class SamusPad
    {
        uint held,previous,pending;
        Vector3 move,aim;
        internal void Read(CharacterBody body,InputBankTest input)
        {
            LocalUser user=null;foreach(var u in LocalUserManager.readOnlyLocalUsersList)if(u.cachedBody==body){user=u;break;}
            uint buttons=0;move=input?input.moveVector:Vector3.zero;aim=input?input.aimDirection:Vector3.forward;
            if(user!=null&&user.isUIFocused){held=previous=pending=0;move=Vector3.zero;return;}
            var last=user?.inputPlayer?.controllers.GetLastActiveController();
            var pad=last!=null&&last.type==ControllerType.Joystick&&last.ImplementsTemplate<IGamepadTemplate>()?last.GetTemplate<IGamepadTemplate>():null;
            if(pad!=null) {
                if(pad.a.value)buttons|=1;if(pad.rightTrigger.value>0.18f)buttons|=2;if(pad.leftTrigger.value>0.18f)buttons|=4;
                if(pad.leftBumper.value)buttons|=8;if(pad.rightBumper.value)buttons|=16;
                if(pad.leftStick.press.value)buttons|=32;if(pad.b.value)buttons|=64;
                // Preserve analog magnitude even when InputBankTest supplies a unit heading.
                move=move.normalized*Mathf.Clamp01(pad.leftStick.value.magnitude);
            } else if(input) {
                if(input.jump.down)buttons|=1;if(input.skill1.down)buttons|=2;if(input.skill2.down)buttons|=4;
                if(input.skill3.down)buttons|=8;if(input.skill4.down)buttons|=16;if(input.sprint.down)buttons|=32;
                if(UnityEngine.Input.GetKey(KeyCode.C))buttons|=64;
            }
            pending|=buttons&~previous;previous=buttons;held=buttons;
        }
        internal Native.Pad Take(uint allowed)
        {
            uint buttons=held|pending;pending=0;
            return new Native.Pad { movement=new Native.Vec3(move.x,0,move.z),aim=new Native.Vec3(aim.x,aim.y,aim.z),buttons=buttons,allowed=allowed };
        }
    }
}
