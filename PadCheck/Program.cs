using System;
using System.Collections.Generic;
using Samus;
namespace UnityEngine {
    public class Object {public static implicit operator bool(Object value)=>value!=null;public T GetComponent<T>()=>default;}
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
    public interface IGamepadTemplate{Button a{get;}Button b{get;}Button x{get;}Axis rightTrigger{get;}Axis leftTrigger{get;}Button leftBumper{get;}Button rightBumper{get;}Stick leftStick{get;}}
    public class Gamepad:IGamepadTemplate{public Button a{get;}=new Button();public Button b{get;}=new Button();public Button x{get;}=new Button();public Axis rightTrigger{get;}=new Axis();public Axis leftTrigger{get;}=new Axis();public Button leftBumper{get;}=new Button();public Button rightBumper{get;}=new Button();public Stick leftStick{get;}=new Stick();}
    public class Controller{public ControllerType type;public Gamepad pad=new Gamepad();public bool ImplementsTemplate<T>()=>true;public T GetTemplate<T>()=>(T)(object)pad;}
    public class Controllers{public Controller controller=new Controller();public Controller GetLastActiveController()=>controller;}
    public class Player{public Controllers controllers=new Controllers();}
}
namespace RoR2 {
    public class CharacterBody{public InteractionDriver interaction=new InteractionDriver();public T GetComponent<T>()=>(T)(object)interaction;}
    public enum Interactability{Disabled,Available}public interface IInteractable{Interactability GetInteractability(object interactor);}
    public class InteractionDriver:UnityEngine.Object{public bool enabled=true;public object interactor;public UnityEngine.Object prompt;public UnityEngine.Object FindBestInteractableObject()=>prompt;}
    public class LocalUser{public CharacterBody cachedBody;public bool isUIFocused;public Rewired.Player inputPlayer=new Rewired.Player();}
    public static class LocalUserManager{public static List<LocalUser> readOnlyLocalUsersList=new List<LocalUser>();}
    public class InputBankTest:UnityEngine.Object{public UnityEngine.Vector3 moveVector,aimDirection=UnityEngine.Vector3.forward;public class Button{public bool down;}public Button jump=new Button(),skill1=new Button(),skill2=new Button(),skill3=new Button(),skill4=new Button(),sprint=new Button(),interact=new Button();}
}
namespace Samus { }
class Program{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main(){
        var body=new RoR2.CharacterBody();var user=new RoR2.LocalUser{cachedBody=body};RoR2.LocalUserManager.readOnlyLocalUsersList.Add(user);
        var device=user.inputPlayer.controllers.controller.pad;var input=new RoR2.InputBankTest{moveVector=new UnityEngine.Vector3(1,0,0)};var pad=new Ror2Common.MeleePad();
        device.x.value=true;pad.Read(body,input);Check(pad.Take(15).buttons==128,"X must grab outside charging/capture");
        pad.Read(body,input,true);Check((pad.Take(15).buttons&7)==0,"X held through capture must not instantly throw/pummel");
        device.x.value=false;pad.Read(body,input,true);pad.Take(15);
        device.b.value=true;device.leftStick.value=new UnityEngine.Vector2(.1f,1);
        pad.Read(body,input,true);var held=pad.Take(0);Check((held.buttons&2)!=0&&held.movement.y==1&&held.allowed==31,"captured B must attack with raw LS axes");
        device.b.value=false;device.rightTrigger.value=1;pad.Read(body,input,true);Check((pad.Take(31).buttons&4)!=0,"captured RT must select a throw/pummel");
        device.rightTrigger.value=0;device.x.value=true;
        pad.Read(body,input,false);Check((pad.Take(15).buttons&128)==0,"X held across release must not instantly re-grab");
        device.x.value=false;pad.Read(body,input);pad.Take(15);device.x.value=true;pad.Read(body,input);device.x.value=false;pad.Read(body,input);Check((pad.Take(15).buttons&128)!=0,"short X press between fixed ticks was lost");
        user.isUIFocused=true;pad.Read(body,input);Check(pad.Take(15).buttons==0,"UI focus must clear gameplay presses");
        user.isUIFocused=false;pad.Read(body,input,false,new UnityEngine.Vector3(0,.8f,.6f));var aimed=pad.Take(15);
        Check(aimed.aim.y==.8f&&aimed.aim.z==.6f,"corrected camera aim was replaced by body aim");
        var stunPad=new Ror2Common.MeleePad();device.b.value=true;stunPad.Read(body,input);stunPad.Take(31,true);
        device.b.value=false;stunPad.Read(body,input);
        for(int tick=0;tick<180;tick++)stunPad.Take(31,true);
        Check((stunPad.Take(31).buttons&2)!=0,"B tapped during host stun was lost");
        Check((stunPad.Take(31).buttons&2)==0,"buffered B replayed more than once");
        device.rightTrigger.value=1;device.leftTrigger.value=1;device.leftBumper.value=true;device.rightBumper.value=true;device.a.value=true;device.b.value=true;
        pad.Read(body,input);uint all=pad.Take(31).buttons;Check((all&(256|2|1024|512|1|2048))==(256|2|1024|512|1|2048),"five skills and jump mapping");
        pad.Read(body,input,false,null,true);all=pad.Take(31).buttons;Check((all&4096)!=0&&(all&2048)==0&&(all&1024)!=0,"LT shield alternate changed the extra slot");
        device.b.value=false;
        device.x.value=false;pad.Read(body,input);pad.Take(31);body.interaction.prompt=new UnityEngine.Object();device.x.value=true;pad.Read(body,input);Check((pad.Take(31).buttons&128)==0&&body.interaction.enabled,"X with prompt must stay vanilla");
        body.interaction.prompt=null;pad.Read(body,input);Check((pad.Take(31).buttons&128)==0,"prompt ownership lost mid-hold");
        device.x.value=false;pad.Read(body,input);pad.Take(31);device.x.value=true;pad.Read(body,input);Check((pad.Take(31).buttons&128)!=0&&!body.interaction.enabled,"native X must suppress repeat interaction");
        body.interaction.prompt=new UnityEngine.Object();pad.Read(body,input);Check((pad.Take(31).buttons&128)!=0&&!body.interaction.enabled,"native hold lost ownership on entering prompt");
        pad.ReleaseInteraction();Check(body.interaction.enabled,"cleanup left interaction disabled");
        Check(Math.Abs(Ror2Common.MeleeControls.JumpHeight(15,-30)-3.75f)<1e-6,"Commando height conversion");
        // A Goobo clone is not a local user: its brain's Press is the controls for the step,
        // and the AI input bank must not overwrite them.
        var cloneBody=new RoR2.CharacterBody();var clonePad=new Ror2Common.MeleePad { Brained=true };
        clonePad.Read(cloneBody,input);
        clonePad.Press(new UnityEngine.Vector3(0,0,1),new UnityEngine.Vector3(1,0,0),Ror2Common.MeleeControls.Attack);
        var cloneStep=clonePad.Take(31);
        Check(cloneStep.buttons==2&&cloneStep.movement.z==1&&cloneStep.aim.x==1,"a brain's buttons, movement and aim must reach the sim");
        clonePad.Press(UnityEngine.Vector3.zero,new UnityEngine.Vector3(1,0,0),0);
        Check(clonePad.Take(31).buttons==0,"a brain must be able to release its buttons");
        Console.WriteLine("PASS: five skill bindings, B A attack / LT shield, X grapple and vanilla interaction ownership, capture B/RT axes, jump, edge suppression, UI focus, camera aim and a brain-controlled clone pad");
    }
}
