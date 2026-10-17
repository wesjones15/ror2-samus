using System;
using System.Collections.Generic;
using Samus;
namespace UnityEngine {
    public class Object {public static implicit operator bool(Object value)=>value!=null;}
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float magnitude=>MathF.Sqrt(x*x+y*y);}
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>default;public static Vector3 forward=>new Vector3(0,0,1);public Vector3 normalized{get{float m=MathF.Sqrt(x*x+y*y+z*z);return m>0?new Vector3(x/m,y/m,z/m):default;}}public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);}
    public static class Mathf {public static float Clamp01(float f)=>Math.Clamp(f,0,1);}
    public enum KeyCode{A,D,W,S,C}public static class Input{public static bool GetKey(KeyCode key)=>false;}
}
namespace Rewired {
    public enum ControllerType{Joystick}
    public class Button{public bool value;}
    public class Axis{public float value;}
    public class Stick{public Button press=new Button();public UnityEngine.Vector2 value;}
    public interface IGamepadTemplate{Button a{get;}Button b{get;}Axis rightTrigger{get;}Axis leftTrigger{get;}Button leftBumper{get;}Button rightBumper{get;}Stick leftStick{get;}}
    public class Gamepad:IGamepadTemplate{public Button a{get;}=new Button();public Button b{get;}=new Button();public Axis rightTrigger{get;}=new Axis();public Axis leftTrigger{get;}=new Axis();public Button leftBumper{get;}=new Button();public Button rightBumper{get;}=new Button();public Stick leftStick{get;}=new Stick();}
    public class Controller{public ControllerType type;public Gamepad pad=new Gamepad();public bool ImplementsTemplate<T>()=>true;public T GetTemplate<T>()=>(T)(object)pad;}
    public class Controllers{public Controller controller=new Controller();public Controller GetLastActiveController()=>controller;}
    public class Player{public Controllers controllers=new Controllers();}
}
namespace RoR2 {
    public class CharacterBody{}
    public class LocalUser{public CharacterBody cachedBody;public bool isUIFocused;public Rewired.Player inputPlayer=new Rewired.Player();}
    public static class LocalUserManager{public static List<LocalUser> readOnlyLocalUsersList=new List<LocalUser>();}
    public class InputBankTest:UnityEngine.Object{public UnityEngine.Vector3 moveVector,aimDirection=UnityEngine.Vector3.forward;public class Button{public bool down;}public Button jump=new Button(),skill1=new Button(),skill2=new Button(),skill3=new Button(),skill4=new Button(),sprint=new Button();}
}
namespace Samus {static class Native{public struct Vec3{public float x,y,z;public Vec3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}public struct Pad{public Vec3 movement,aim;public uint buttons,allowed;}}}
class Program{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main(){
        var body=new RoR2.CharacterBody();var user=new RoR2.LocalUser{cachedBody=body};RoR2.LocalUserManager.readOnlyLocalUsersList.Add(user);
        var device=user.inputPlayer.controllers.controller.pad;var input=new RoR2.InputBankTest{moveVector=new UnityEngine.Vector3(1,0,0)};var pad=new SamusPad();
        device.b.value=true;pad.Read(body,input);Check(pad.Take(15).buttons==128,"B must grab outside charging/capture");
        pad.Read(body,input,true);Check((pad.Take(15).buttons&7)==0,"B held through capture must not instantly throw/pummel");
        device.b.value=false;pad.Read(body,input,true);pad.Take(15);
        device.a.value=true;device.leftStick.value=new UnityEngine.Vector2(.1f,1);device.leftBumper.value=true;
        pad.Read(body,input,true);var held=pad.Take(0);Check((held.buttons&2)!=0&&(held.buttons&1)==0&&held.movement.y==1&&held.allowed==15,"captured A must attack with raw LS axes");Check((held.buttons&8)!=0,"shoulder held during capture must retain priority");
        device.a.value=false;device.leftBumper.value=false;device.b.value=true;pad.Read(body,input,true);Check((pad.Take(15).buttons&4)!=0,"captured B must select a throw/pummel");
        pad.Read(body,input,false);Check((pad.Take(15).buttons&128)==0,"B held across release must not instantly re-grab");
        device.b.value=false;pad.Read(body,input);pad.Take(15);device.b.value=true;pad.Read(body,input);device.b.value=false;pad.Read(body,input);Check((pad.Take(15).buttons&128)!=0,"short B press between fixed ticks was lost");
        user.isUIFocused=true;pad.Read(body,input);Check(pad.Take(15).buttons==0,"UI focus must clear gameplay presses");
        user.isUIFocused=false;pad.Read(body,input,false,new UnityEngine.Vector3(0,.8f,.6f));var aimed=pad.Take(15);
        Check(aimed.aim.y==.8f&&aimed.aim.z==.6f,"corrected camera aim was replaced by body aim");
        Console.WriteLine("PASS: B grab, capture/release edge suppression, raw throw axes, A/B remapping, shoulder priority, between-tick press and UI focus");
    }
}
