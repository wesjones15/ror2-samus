using Rewired;
using RoR2;
using UnityEngine;

namespace Samus
{
    internal sealed class SamusPad
    {
        uint held,previous,pending,blocked;
        Vector3 move,aim;
        bool grabbing;
        internal void Read(CharacterBody body,InputBankTest input,bool grabbing=false)
        {
            if(this.grabbing!=grabbing){blocked=1|2|4|128;held=previous=pending=0;}this.grabbing=grabbing;
            LocalUser user=null;foreach(var u in LocalUserManager.readOnlyLocalUsersList)if(u.cachedBody==body){user=u;break;}
            uint buttons=0;move=input?input.moveVector:Vector3.zero;aim=input?input.aimDirection:Vector3.forward;
            if(user!=null&&user.isUIFocused){held=previous=pending=0;move=Vector3.zero;return;}
            var last=user?.inputPlayer?.controllers.GetLastActiveController();
            var pad=last!=null&&last.type==ControllerType.Joystick&&last.ImplementsTemplate<IGamepadTemplate>()?last.GetTemplate<IGamepadTemplate>():null;
            if(pad!=null) {
                if(pad.a.value)buttons|=grabbing?2u:1u;if(pad.rightTrigger.value>0.18f)buttons|=2;if(pad.leftTrigger.value>0.18f)buttons|=4;
                if(pad.leftBumper.value)buttons|=8;if(pad.rightBumper.value)buttons|=16;
                if(pad.leftStick.press.value&&!grabbing)buttons|=32;if(pad.b.value)buttons|=grabbing?4u:128u;
                // Preserve analog magnitude even when InputBankTest supplies a unit heading.
                move=move.normalized*Mathf.Clamp01(pad.leftStick.value.magnitude);
                if(grabbing)move=new Vector3(pad.leftStick.value.x,pad.leftStick.value.y,0);
            } else if(input) {
                if(input.jump.down)buttons|=1;if(input.skill1.down)buttons|=2;if(input.skill2.down)buttons|=4;
                if(input.skill3.down)buttons|=8;if(input.skill4.down)buttons|=16;if(input.sprint.down)buttons|=32;
                if(UnityEngine.Input.GetKey(KeyCode.C))buttons|=128;
                if(grabbing)move=new Vector3((UnityEngine.Input.GetKey(KeyCode.D)?1:0)-(UnityEngine.Input.GetKey(KeyCode.A)?1:0),(UnityEngine.Input.GetKey(KeyCode.W)?1:0)-(UnityEngine.Input.GetKey(KeyCode.S)?1:0),0);
            }
            blocked&=buttons;buttons&=~blocked;
            pending|=buttons&~previous;previous=buttons;held=buttons;
        }
        internal Native.Pad Take(uint allowed)
        {
            uint buttons=held|pending;pending=0;
            // Shoulder modifiers override stick move selection without changing
            // the world movement vector. Captured throws use raw Melee axes.
            return new Native.Pad { movement=new Native.Vec3(move.x,grabbing?move.y:0,move.z),aim=new Native.Vec3(aim.x,aim.y,aim.z),buttons=buttons,allowed=grabbing?15u:allowed };
        }
    }
}
