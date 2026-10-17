using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Samus
{
    [DefaultExecutionOrder(-200)]
    public sealed class SamusDriver:MonoBehaviour
    {
        static readonly Native.CollideFn collideFn=Collide;
        static readonly Native.EventFn eventFn=Event;
        CharacterBody body;CharacterMotor motor;InputBankTest input;NativeBody native;CharacterDirection direction;
        IntPtr sim;SamusView view;Native.Host host;GCHandle self;Native.State state;
        Vector3 feetOffset,safe;StepSmoother smoother;CameraPivot cameraPivot;
        readonly GroundGate ground=new GroundGate();readonly SamusPad pad=new SamusPad();
        readonly List<Vector3> walls=new List<Vector3>();
        bool active,failed,permitDamage;float scale;
        void Awake(){body=GetComponent<CharacterBody>();motor=GetComponent<CharacterMotor>();input=GetComponent<InputBankTest>();native=GetComponent<NativeBody>();direction=GetComponent<CharacterDirection>();}
        void Update()
        {
            if(failed||!body)return;pad.Read(body,input);
            if(active) { smoother.Smooth();cameraPivot.Use(true); }
        }
        void FixedUpdate()
        {
            if(failed||!body||!body.healthComponent||!body.healthComponent.alive)return;
            if(!NetworkServer.active||!body.hasEffectiveAuthority)return; // Offline/server-local prototype.
            if(!active) {
                if(Plugin.AssetsBusy)return;
                if(!Plugin.AssetsReady){Fallback("Assets missing. Set the Samus Paths.Disc config.");return;}
                if(!ground.Ready(body.footPosition,Time.fixedDeltaTime,"Samus"))return;
                try{StartSim();}catch(Exception e){Fallback(e.ToString());return;}
            }
            try {
                if(smoother.MovedElsewhere){Native.ssbm_sim_place(sim,ToSim(transform.position-feetOffset),0);smoother.Reset();native.ForgetVelocity();}
                if(native.TakeExternalVelocity(out var external)) {
                    var velocity=new Native.Vec3(external.x/(scale*60),external.y/(scale*60),external.z/(scale*60));
                    if(native.JumpPadLaunch){var g=Physics.gravity/(scale*3600);Native.ssbm_sim_launch_pad(sim,velocity,new Native.Vec3(g.x,g.y,g.z));}
                    else Native.ssbm_sim_place(sim,velocity,1);
                }
                var control=native.Control();var p=pad.Take(Skills.Allowed(body));
                if(control==NativeBody.Hold.All){p.buttons=0;p.movement=default;p.allowed=0;Native.ssbm_sim_interrupt(sim);}
                else if(control==NativeBody.Hold.Move)p.movement=default;
                bool sprint=native.UpdateSprint((p.buttons&32)!=0,new Vector3(p.movement.x,0,p.movement.z).sqrMagnitude>0.01f,control==NativeBody.Hold.None);
                if(sprint)p.buttons|=32;
                uint extra=(uint)Math.Max(0,body.maxJumpCount-2);
                Native.ssbm_sim_tuning(sim,new Native.Tuning { speed_scale=body.moveSpeed/Mathf.Max(0.1f,body.baseMoveSpeed),jump_scale=Plugin.JumpScale.Value*NativeBody.JumpScale(body),extra_jumps=extra,hold_charge=Plugin.HoldCharge.Value?1u:0u,smash_max_frames=60 });
                Native.ssbm_sim_step(sim,in host,in p,Time.fixedDeltaTime);
                if(Native.ssbm_sim_state(sim,out state)!=1){Fallback(Native.Error);return;}
                var feet=ToWorld(state.position);
                if(state.grounded!=0&&WorldProbe.StandingGround(feet,0.2f,0.4f,1.8f,0.3f,out _))safe=feet;
                if(feet.y<safe.y-100f||WorldProbe.InMapZone(feet)) {
                    Native.ssbm_sim_place(sim,ToSim(safe+Vector3.up*0.1f),0);Native.ssbm_sim_state(sim,out state);feet=ToWorld(state.position);smoother.Reset();
                }
                smoother.Stepped(feet+feetOffset);native.WroteVelocity(ToWorld(state.velocity)*60f);
                motor.Motor.GroundingStatus.IsStableOnGround=state.grounded!=0;
                motor.Motor.GroundingStatus.FoundAnyGround=state.grounded!=0;
                if(direction)direction.moveVector=new Vector3(state.facing.x,0,state.facing.z);
            }catch(Exception e){Fallback(e.ToString());}
        }
        void StartSim()
        {
            scale=Plugin.MetresPerUnit.Value;feetOffset=transform.position-body.footPosition;safe=body.footPosition;
            sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),ToSim(body.footPosition));if(sim==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            view=new SamusView(Math.Min(body.skinIndex,4));self=GCHandle.Alloc(this);
            host=new Native.Host { user=GCHandle.ToIntPtr(self),collide=Marshal.GetFunctionPointerForDelegate(collideFn),evt=Marshal.GetFunctionPointerForDelegate(eventFn) };
            WorldProbe.UseBodyCollision(motor,gameObject);motor.Motor.enabled=false;
            smoother=new StepSmoother(transform);cameraPivot=new CameraPivot(body);cameraPivot.Use(true);active=true;
            Plugin.Log.LogInfo("Samus sim started with costume "+body.skinIndex);
        }
        void LateUpdate()
        {
            if(!active||failed)return;
            try {
                GameSettings.HideRenderers(body.modelLocator.modelTransform,true);
                // The hidden Commando animator must not emit animation-event sounds.
                var animator=body.modelLocator.modelTransform.GetComponent<Animator>();if(animator)animator.enabled=false;
                view.Pose(sim,Time.deltaTime);
                var at=transform.position-feetOffset;float yaw=-Mathf.Atan2(state.facing.z,state.facing.x)*Mathf.Rad2Deg;
                view.Models.Update(scale,d=>(at,Quaternion.Euler(0,yaw,0)));
            }catch(Exception e){Fallback(e.ToString());}
        }
        Native.Vec3 ToSim(Vector3 v)=>new Native.Vec3(v.x/scale,v.y/scale,v.z/scale);
        Vector3 ToWorld(Native.Vec3 v)=>new Vector3(v.x,v.y,v.z)*scale;
        static void Collide(IntPtr user,in Native.Vec3 from,in Native.Vec3 desired,out Native.Collision result)
        {
            result=new Native.Collision { position=from };
            try { var driver=(SamusDriver)GCHandle.FromIntPtr(user).Target;result=driver.Collision(from,desired); }
            catch(Exception e){Plugin.Log.LogError("Samus collision callback: "+e.Message);}
        }
        Native.Collision Collision(Native.Vec3 from,Native.Vec3 desired)
        {
            Vector3 start=ToWorld(from),end=ToWorld(desired);var delta=end-start;bool roof=false,land=false;
            float radius=0.3f,height=1.8f;
            // Sweep the actual displacement to prevent crossing thin world geometry.
            if(delta.sqrMagnitude>0.000001f&&Physics.CapsuleCast(start+Vector3.up*radius,start+Vector3.up*(height-radius),radius,delta.normalized,out var hit,delta.magnitude,WorldProbe.Mask,QueryTriggerInteraction.Ignore)) {
                end=start+delta.normalized*Mathf.Max(0,hit.distance-0.01f);
                if(hit.normal.y>0.6f&&delta.y<=0)land=true;if(hit.normal.y< -0.6f)roof=true;
                var remain=Vector3.ProjectOnPlane(delta-(end-start),hit.normal);
                end+=remain;
            }
            end=WorldProbe.PushOutOfWalls(end+Vector3.up*0.6f,radius,0.6f,walls,8)-Vector3.up*0.6f;
            float above=Mathf.Max(start.y,end.y)+0.35f;
            if(delta.y<=0&&WorldProbe.Ground(new Vector3(end.x,above,end.z),above-end.y+0.15f,out var floor)&&floor.normal.y>0.6f&&floor.point.y>=end.y-0.12f) {
                end.y=floor.point.y;land=true;
            }
            if(delta.y>0&&WorldProbe.Roof(start+Vector3.up*height,delta.y+0.05f,out var ceiling)){end.y=Mathf.Min(end.y,ceiling.point.y-height-0.01f);roof=true;}
            return new Native.Collision { position=ToSim(end),grounded=land?1u:0u,ceiling=roof?1u:0u };
        }
        static void Event(IntPtr user,in Native.Event e)
        {
            try{((SamusDriver)GCHandle.FromIntPtr(user).Target).Receive(e);}catch(Exception error){Plugin.Log.LogError("Samus event callback: "+error.Message);}
        }
        void Receive(Native.Event e)
        {
            if(e.kind==1) {
                int slot=Skills.Slot(e.move_id);var skill=body.skillLocator.GetSkill(Skills.Slots[slot]);
                bool own=skill.skillDef==Skills.Definitions[slot];permitDamage=NativeSkills.Spend(skill,Skills.Definitions[slot])&&own;
                NativeBody.MarkCombat(body);
            } else if(e.kind==2&&permitDamage)Combat.Attack(body,e,scale);
            else if(e.kind==3)native.Jumped();
            else if(e.kind==4)native.Landed(Vector3.down*e.strength*scale*60f);
        }
        void Fallback(string reason)
        {
            if(failed)return;failed=true;Plugin.Log.LogError("Samus falls back to Commando: "+reason);Cleanup();
            var machine=EntityStateMachine.FindByCustomName(gameObject,"Body");if(machine){machine.mainStateType=new SerializableEntityStateType(typeof(GenericCharacterMain));machine.SetNextStateToMain();}
        }
        void Cleanup()
        {
            active=false;cameraPivot?.Use(false);view?.Dispose();view=null;
            if(sim!=IntPtr.Zero){Native.ssbm_sim_free(sim);sim=IntPtr.Zero;}if(self.IsAllocated)self.Free();
            if(motor&&motor.Motor)motor.Motor.enabled=true;
            if(body&&body.modelLocator&&body.modelLocator.modelTransform){GameSettings.HideRenderers(body.modelLocator.modelTransform,false);var a=body.modelLocator.modelTransform.GetComponent<Animator>();if(a)a.enabled=true;}
        }
        void OnDestroy(){Cleanup();}
    }
}
