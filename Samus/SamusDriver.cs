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
        SamusView entryPlatform;
        Vector3 feetOffset,safe;StepSmoother smoother;CameraPivot cameraPivot;
        readonly GroundGate ground=new GroundGate();readonly SamusPad pad=new SamusPad();
        readonly Unstuck unstuck=new Unstuck();readonly WallGuard wallGuard=new WallGuard();
        static readonly float[] sweepHeights={0.1f,0.6f,1.2f,1.7f};
        readonly List<Vector3> walls=new List<Vector3>();
        bool active,failed,permitDamage;float scale;
        Native.HitboxSample[] hitboxes=new Native.HitboxSample[64];
        readonly Combat.HitTracker hits=new Combat.HitTracker();
        readonly HashSet<string> presentationErrors=new HashSet<string>();
        SamusAudio audio;
        internal DiscParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        void Awake(){body=GetComponent<CharacterBody>();motor=GetComponent<CharacterMotor>();input=GetComponent<InputBankTest>();native=GetComponent<NativeBody>();direction=GetComponent<CharacterDirection>();}
        void Update()
        {
            if(failed||!body)return;pad.Read(body,input);
            if(body.healthComponent&&!body.healthComponent.alive){audio?.Dispose();return;}
            audio?.Update();
            if(active) { smoother.Smooth();cameraPivot.Use(true); }
        }
        void FixedUpdate()
        {
            if(failed||!body||!body.healthComponent||!body.healthComponent.alive)return;
            if(!body.hasEffectiveAuthority)return;
            // The pod/vehicle owns placement until it releases the survivor, as
            // in the common Mario host. Do not disable its motor while seated.
            if(body.currentVehicle){if(active)Cleanup();return;}
            if(!NetworkServer.active){Fallback("Remote-player native simulation is not supported by this offline prototype.");return;}
            if(!active) {
                if(Plugin.AssetsBusy)return;
                if(!Plugin.AssetsReady){Fallback("Assets missing. Set the Samus Paths.Disc config.");return;}
                if(!ground.Ready(body.footPosition,Time.fixedDeltaTime,"Samus"))return;
                try{StartSim();}catch(Exception e){Fallback(e.ToString());return;}
            }
            try {
                if(smoother.MovedElsewhere){var moved=transform.position-feetOffset;Native.ssbm_sim_place(sim,ToSim(moved),0);smoother.Reset();native.ForgetVelocity();unstuck.Reset();wallGuard.Placed(moved);}
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
                Present(()=>Effects?.Step(sim,Time.fixedDeltaTime));
                Present(()=>Particles?.Step(Time.fixedDeltaTime));
                uint count=Native.ssbm_sim_hitboxes(sim,null,0);
                if(count>1024)throw new InvalidOperationException("Native hitbox sample budget exceeded");
                if(count>hitboxes.Length)hitboxes=new Native.HitboxSample[count];
                if(count>0) {
                    if(Native.ssbm_sim_hitboxes(sim,hitboxes,(uint)hitboxes.Length)!=count)throw new InvalidOperationException(Native.Error);
                    if(permitDamage)hits.Apply(body,hitboxes,(int)count,scale);
                }
                if(Native.ssbm_sim_state(sim,out state)!=1){Fallback(Native.Error);return;}
                var feet=ToWorld(state.position);
                bool supported=RecoveryGround(feet,0.45f,0.45f,out var support)&&Mathf.Abs(support.y-feet.y)<0.15f;
                bool embedded=WorldProbe.InsideSolid(feet+Vector3.up*0.9f);
                bool blocked=(new Vector3(p.movement.x,0,p.movement.z).sqrMagnitude>0.05f&&!supported)||embedded;
                bool fallen=state.grounded==0&&state.velocity.y<=0;
                if(unstuck.Step(feet,fallen||blocked,state.grounded!=0&&supported,control!=NativeBody.Hold.None,Time.fixedDeltaTime,1.8f,RecoveryGround,out var recovered)
                    ||unstuck.Fell(feet,1.8f,RecoveryGround,out recovered)) {
                    Recover(recovered);feet=ToWorld(state.position);Plugin.Log.LogInfo("Samus recovered using common Unstuck");
                } else if(wallGuard.Step(feet,1.8f,sweepHeights,0.6f,control!=NativeBody.Hold.None,false,out var back,out var why)) {
                    Recover(back);feet=ToWorld(state.position);Plugin.Log.LogInfo("Samus wall recovery: "+why);
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
            // A pod exit can leave the feet slightly below a one-sided floor.
            // The common standing probe starts above it and checks full clearance.
            if(RecoveryGround(safe,0.6f,0.8f,out var standing)){safe=standing;transform.position=standing+feetOffset;}
            sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),ToSim(safe));if(sim==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            view=new SamusView(Math.Min(body.skinIndex,4));self=GCHandle.Alloc(this);
            audio=new SamusAudio(sim);
            Particles=new DiscParticles(scale);
            Effects=new MeleeEffects(Particles,scale);
            entryPlatform=SamusView.EntryPlatform();
            if(Native.ssbm_sim_begin_entry(sim,NativeLib.CString(Plugin.AssetsDir))!=1)throw new InvalidOperationException(Native.Error);
            host=new Native.Host { user=GCHandle.ToIntPtr(self),collide=Marshal.GetFunctionPointerForDelegate(collideFn),evt=Marshal.GetFunctionPointerForDelegate(eventFn) };
            WorldProbe.UseBodyCollision(motor,gameObject);motor.Motor.enabled=false;
            smoother=new StepSmoother(transform);cameraPivot=new CameraPivot(body);cameraPivot.Use(true);active=true;
            unstuck.Reset();wallGuard.Placed(safe);
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
                var at=transform.position-feetOffset;
                // Melee Samus's authored forward is +Z. ModelView mirrors local X.
                var facing=new Vector3(state.facing.x,0,state.facing.z);
                var rotation=facing.sqrMagnitude>0.0001f?Quaternion.LookRotation(facing):Quaternion.identity;
                view.Models.Update(scale,d=>(at,rotation));
                if(entryPlatform!=null) {
                    if(Native.ssbm_entry_platform_update(entryPlatform.Handle,sim)==1)entryPlatform.Models.Update(scale,d=>(at,rotation));
                    else {entryPlatform.Dispose();entryPlatform=null;}
                }
                Present(()=>Particles?.Draw());
                Present(()=>Effects?.Draw());
            }catch(Exception e){Fallback(e.ToString());}
        }
        Native.Vec3 ToSim(Vector3 v)=>new Native.Vec3(v.x/scale,v.y/scale,v.z/scale);
        void Present(Action draw)
        {
            try{draw();}catch(Exception e){if(presentationErrors.Add(e.Message))Plugin.Log.LogError("Samus presentation failed; character simulation remains active: "+e);}
        }
        bool RecoveryGround(Vector3 sample,float rise,float drop,out Vector3 feet)=>WorldProbe.StandingGround(sample,rise,drop,1.8f,0.3f,out feet);
        void Recover(Vector3 feet)
        {
            Native.ssbm_sim_place(sim,ToSim(feet),0);Native.ssbm_sim_state(sim,out state);
            smoother.Reset();native.ForgetVelocity();unstuck.Reset();wallGuard.Placed(feet);
        }
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
            var commando=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock=commando?commando.GetComponent<SkillLocator>():null;
            if(stock&&body.skillLocator)foreach(var slot in Skills.Slots) {
                var replacement=stock.GetSkill(slot);var skill=body.skillLocator.GetSkill(slot);
                if(skill&&replacement&&replacement.skillFamily&&replacement.skillFamily.variants.Length>0)
                    skill.SetSkillOverride(this,replacement.skillFamily.variants[0].skillDef,GenericSkill.SkillOverridePriority.Contextual);
            }
        }
        void Cleanup()
        {
            active=false;cameraPivot?.Use(false);view?.Dispose();view=null;
            entryPlatform?.Dispose();entryPlatform=null;
            audio?.Dispose();audio=null;
            Effects?.Dispose();Effects=null;Particles?.Dispose();Particles=null;
            if(sim!=IntPtr.Zero){Native.ssbm_sim_free(sim);sim=IntPtr.Zero;}if(self.IsAllocated)self.Free();
            if(motor&&motor.Motor)motor.Motor.enabled=true;
            if(body&&body.modelLocator&&body.modelLocator.modelTransform){GameSettings.HideRenderers(body.modelLocator.modelTransform,false);var a=body.modelLocator.modelTransform.GetComponent<Animator>();if(a)a.enabled=true;}
        }
        void OnDestroy(){Cleanup();}
    }
}
