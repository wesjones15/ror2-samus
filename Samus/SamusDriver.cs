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
    public sealed class SamusDriver:MonoBehaviour,IMeleeArrival,IMeleeRecovery,IMeleeDefense
    {
        static readonly Native.CollideFn collideFn=Collide;
        static readonly Native.EventFn eventFn=Event;
        CharacterBody body;CharacterMotor motor;InputBankTest input;NativeBody native;CharacterDirection direction;
        IntPtr sim;SamusView view;Native.Host host;GCHandle self;Native.State state;
        SamusView entryPlatform;
        SamusView shieldView;uint shieldModel;
        public bool MeleeInvulnerable=>active&&!Recovering&&(Native.ssbm_sim_defense_flags(sim)&(2|64))!=0;
        public bool BlockMeleeDamage(float amount){if(!active||Recovering)return false;uint flags=Native.ssbm_sim_defense_flags(sim);return (flags&1)!=0&&Native.ssbm_sim_shield_hit(sim,amount)!=0;}
        MeleeRecovery recovery;uint recoveryFlags;
        public bool Recovering=>(recoveryFlags&1)!=0;
        public bool RecoveryInvulnerable=>(recoveryFlags&2)!=0;
        Vector3 feetOffset,safe;StepSmoother smoother;CameraPivot cameraPivot;
        readonly GroundGate ground=new GroundGate();readonly SamusPad pad=new SamusPad();
        readonly Unstuck unstuck=new Unstuck();readonly WallGuard wallGuard=new WallGuard();
        static readonly float[] sweepHeights={0.1f,0.6f,1.2f,1.7f};
        readonly List<Vector3> walls=new List<Vector3>();
        bool active,failed,permitDamage,deathStarted;float scale,lastHealth;
        internal static Run LocalRun {get;private set;}
        Native.HitboxSample[] hitboxes=new Native.HitboxSample[64];
        readonly Combat.HitTracker hits=new Combat.HitTracker();
        readonly HashSet<string> presentationErrors=new HashSet<string>();
        SamusAudio audio;
        internal DiscParticles Particles { get; private set; }
        internal MeleeEffects Effects { get; private set; }
        internal IntPtr Simulation=>sim;
        internal float Units=>scale;
        internal SamusGrab Grab {get;private set;}
        internal bool GrabHeld=>pad.GrabHeld;
        internal bool JumpHeld=>pad.JumpHeld;
        internal bool ControlsInterrupted=>Recovering||Arriving||native.Control()!=NativeBody.Hold.None;
        internal void AnchorGrapple(Vector3 desiredFeet){if(!active)return;Native.ssbm_sim_state(sim,out var current);var contact=Collision(current.position,ToSim(desiredFeet));Native.ssbm_sim_anchor(sim,contact.position,contact.grounded);}
        public bool Arriving {get;set;}
        float chargeFraction;
        bool FacingLocked=>Arriving||Recovering||Native.ssbm_sim_facing_locked(sim)!=0;
        void FaceTarget()
        {
            if(state.move_id>=20&&state.move_id<=22)MeleeAim.BeginAttack(body,direction,MeleeAim.CameraRay(body,input).direction);
            MeleeAim.FacingTarget(body,direction,input?input.moveVector:Vector3.zero,MeleeAim.CameraRay(body,input).direction,FacingLocked,new Vector3(state.facing.x,0,state.facing.z));
        }
        void Awake(){body=GetComponent<CharacterBody>();motor=GetComponent<CharacterMotor>();input=GetComponent<InputBankTest>();native=GetComponent<NativeBody>();direction=GetComponent<CharacterDirection>();}
        void Update()
        {
            if(failed||!body)return;pad.Read(body,input,Grab?.Holding??false,MeleeAim.CameraRay(body,input).direction,Skills.ShieldSelected(body));
            if(body.healthComponent&&!body.healthComponent.alive){BeginDeath();return;}
            audio?.Update();
            if(active) { smoother.Smooth();cameraPivot.Use(true);FaceTarget(); }
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
                bool hitImpulse=false;
                if(native.TakeExternalVelocity(out var external)&&!Recovering) {
                    hitImpulse=!native.JumpPadLaunch&&external.sqrMagnitude>0.01f;
                    var velocity=new Native.Vec3(external.x/(scale*60),external.y/(scale*60),external.z/(scale*60));
                    if(native.JumpPadLaunch){var g=Physics.gravity/(scale*3600);Native.ssbm_sim_launch_pad(sim,velocity,new Native.Vec3(g.x,g.y,g.z));}
                    else Native.ssbm_sim_place(sim,velocity,1);
                }
                float health=body.healthComponent.combinedHealth;
                if(health<lastHealth-0.001f&&(hitImpulse||native.Control()==NativeBody.Hold.All))Native.ssbm_sim_hurt(sim);
                lastHealth=health;
                var control=native.Control();var p=pad.Take(Skills.Allowed(body),control==NativeBody.Hold.All);
                if(control==NativeBody.Hold.All)Native.ssbm_sim_hold_knockdown(sim);
                if(Grab!=null&&Grab.Attached&&((p.buttons&1)!=0||control!=NativeBody.Hold.None)){Grab.Detach();Native.ssbm_sim_interrupt(sim);}
                if(Grab!=null&&Grab.Holding&&((p.buttons&1)!=0||control!=NativeBody.Hold.None))Grab.ReleaseCapture();
                if(control==NativeBody.Hold.All){p.buttons=0;p.movement=default;p.allowed=0;Native.ssbm_sim_interrupt(sim);}
                else if(control==NativeBody.Hold.Move)p.movement=default;
                bool sprint=native.UpdateSprint((p.buttons&32)!=0,new Vector3(p.movement.x,0,p.movement.z).sqrMagnitude>0.01f,control==NativeBody.Hold.None);
                if(sprint)p.buttons|=32;
                // Starting RT from an already latched vanilla sprint drops to a
                // walk. A fresh LS-click during the charge is the store gesture.
                if(state.move_id!=20&&(p.buttons&256)!=0&&sprint){native.UpdateSprint(false,false);p.buttons&=~32u;}
                uint extra=(uint)Math.Max(0,body.maxJumpCount-2);
                float speedScale=body.moveSpeed/Mathf.Max(0.1f,body.baseMoveSpeed);
                // Native Run already supplies Melee's run/walk ratio. Keep item
                // scaling without applying RoR2's base sprint bonus a second time.
                if(body.isSprinting)speedScale/=Mathf.Max(0.1f,body.sprintingSpeedMultiplier);
                Native.ssbm_sim_tuning(sim,new Native.Tuning { speed_scale=speedScale,jump_scale=Plugin.JumpScale.Value*NativeBody.JumpScale(body),extra_jumps=extra,hold_charge=Plugin.HoldCharge.Value?1u:0u,smash_max_frames=60 });
                Native.ssbm_sim_jump_height(sim,MeleeControls.JumpHeight(body.jumpPower,Physics.gravity.y,motor.jumpPowerPenaltyCoefficient)/scale);
                FaceTarget();
                if(direction){var facing=direction.forward;Native.ssbm_sim_facing(sim,new Native.Vec3(facing.x,0,facing.z));}
                Native.ssbm_sim_step(sim,in host,in p,Time.fixedDeltaTime);
                uint previousRecovery=recoveryFlags;recoveryFlags=Native.ssbm_sim_rebirth_flags(sim);
                recovery?.Refresh();
                if(Arriving&&!Recovering)Arriving=false;
                Grab?.AfterStep(Time.fixedDeltaTime);
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
                chargeFraction=Native.ssbm_sim_charge_fraction(sim);
                var feet=ToWorld(state.position);
                if((previousRecovery&4)!=0&&(recoveryFlags&4)==0){
                    // The native KO phase placed the fighter back on safe ground.
                    transform.position=feet+feetOffset;smoother.Reset();native.ForgetVelocity();wallGuard.Placed(feet);
                    unstuck.Step(feet,false,true,false,0.25f,1.8f,RecoveryGround,out _);
                    entryPlatform?.Dispose();entryPlatform=SamusView.EntryPlatform();
                }
                bool supported=RecoveryGround(feet,0.45f,0.45f,out var support)&&Mathf.Abs(support.y-feet.y)<0.15f;
                bool embedded=WorldProbe.InsideSolid(feet+Vector3.up*0.9f);
                bool blocked=(new Vector3(p.movement.x,0,p.movement.z).sqrMagnitude>0.05f&&!supported)||embedded;
                bool fallen=state.grounded==0&&state.velocity.y<=0;
                if(!Recovering&&recovery!=null&&recovery.CheckFall(unstuck,feet,1.8f,RecoveryGround)) {
                    Plugin.Log.LogInfo("Samus falloff: common threshold, Melee KO and figurine recovery");
                } else if(!Recovering&&unstuck.Step(feet,fallen||blocked,state.grounded!=0&&supported,control!=NativeBody.Hold.None||(Grab?.Attached??false),Time.fixedDeltaTime,1.8f,RecoveryGround,out var recovered)) {
                    Recover(recovered);feet=ToWorld(state.position);Plugin.Log.LogInfo("Samus recovered using common Unstuck");
                } else if(!Recovering&&wallGuard.Step(feet,1.8f,sweepHeights,0.6f,control!=NativeBody.Hold.None||(Grab?.Attached??false),false,out var back,out var why)) {
                    Recover(back);feet=ToWorld(state.position);Plugin.Log.LogInfo("Samus wall recovery: "+why);
                }
                smoother.Stepped(feet+feetOffset);native.WroteVelocity(ToWorld(state.velocity)*60f);
                motor.Motor.GroundingStatus.IsStableOnGround=state.grounded!=0;
                motor.Motor.GroundingStatus.FoundAnyGround=state.grounded!=0;
                FaceTarget();
            }catch(Exception e){Fallback(e.ToString());}
        }
        void StartSim()
        {
            scale=Plugin.MetresPerUnit.Value;feetOffset=transform.position-body.footPosition;safe=body.footPosition;
            recovery=GetComponent<MeleeRecovery>();
            lastHealth=body.healthComponent.combinedHealth;
            // A pod exit can leave the feet slightly below a one-sided floor.
            // The common standing probe starts above it and checks full clearance.
            if(RecoveryGround(safe,0.6f,0.8f,out var standing)){safe=standing;transform.position=standing+feetOffset;}
            sim=Native.ssbm_sim_new(NativeLib.CString(Plugin.AssetsDir),ToSim(safe));if(sim==IntPtr.Zero)throw new InvalidOperationException(Native.Error);
            // The shared Melee presenter layers all grounded special abilities
            // over walking legs. A attacks and authored grabs remain full-body.
            const ulong mobileSpecials=(1UL<<20)|(1UL<<21)|(1UL<<22)|(1UL<<23)|(1UL<<24);
            if(Native.ssbm_sim_mobile_moves(sim,mobileSpecials)!=1)throw new InvalidOperationException("Native mobile specials unavailable: "+Native.Error);
            view=new SamusView(Math.Min(body.skinIndex,4));self=GCHandle.Alloc(this);
            audio=new SamusAudio(sim);
            Particles=new DiscParticles(scale);
            Effects=new MeleeEffects(Particles,scale);
            Grab=new SamusGrab(this,body);
            entryPlatform=SamusView.EntryPlatform();
            if(Native.ssbm_sim_begin_arrival(sim,NativeLib.CString(Plugin.AssetsDir))!=1)throw new InvalidOperationException(Native.Error);
            recoveryFlags=Native.ssbm_sim_rebirth_flags(sim);recovery?.Refresh();
            host=new Native.Host { user=GCHandle.ToIntPtr(self),collide=Marshal.GetFunctionPointerForDelegate(collideFn),evt=Marshal.GetFunctionPointerForDelegate(eventFn) };
            WorldProbe.UseBodyCollision(motor,gameObject);motor.Motor.enabled=false;
            smoother=new StepSmoother(transform);cameraPivot=new CameraPivot(body);cameraPivot.Use(true);active=true;
            unstuck.Reset();unstuck.Step(safe,false,true,false,0.25f,1.8f,RecoveryGround,out _);wallGuard.Placed(safe);
            Plugin.Log.LogInfo("Samus sim started with costume "+body.skinIndex);
            if(body.isPlayerControlled&&body.hasEffectiveAuthority)LocalRun=Run.instance;
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
                Present(()=>{
                uint defense=Native.ssbm_sim_defense_flags(sim);
                if((defense&16)!=0){
                    uint model=(defense&32)!=0?11u:(defense&1)!=0?13u:14u;
                    if(shieldView==null||shieldModel!=model){shieldView?.Dispose();shieldView=SamusView.Effect(model);shieldModel=model;}
                    shieldView.ObjectPose(Native.ssbm_sim_motion_frame(sim));
                    shieldView.Models.Update(scale*Native.ssbm_sim_shield_scale(sim),d=>(body.corePosition,rotation));
                }else shieldView?.Models.HideAll();
                });
                if(entryPlatform!=null) {
                    if(Native.ssbm_entry_platform_update(entryPlatform.Handle,sim)==1)entryPlatform.Models.Update(scale,d=>(at,rotation));
                    else {entryPlatform.Dispose();entryPlatform=null;}
                }
                Present(()=>Particles?.Draw());
                Present(()=>Effects?.Draw());
                Present(()=>Grab?.Draw());
            }catch(Exception e){Fallback(e.ToString());}
        }
        void OnGUI()
        {
            if(!active||failed||!body||!body.isPlayerControlled||!body.hasEffectiveAuthority||!body.healthComponent.alive)return;
            if(!Arriving&&chargeFraction>0) {
                string label=(state.phase==4?"Charging":"Stored Charge Shot")+": "+Mathf.RoundToInt(chargeFraction*100)+"%";
                if(chargeFraction>=1)label="Charge Shot ready";
                GUI.Box(new Rect(Screen.width/2f-110,Screen.height-130,220,30),label);
            }
            if(Grab?.Holding??false)GUI.Box(new Rect(Screen.width/2f-180,Screen.height-165,360,30),"LS + LT/RT: throw    Neutral LT/RT: pummel");
        }
        Native.Vec3 ToSim(Vector3 v)=>new Native.Vec3(v.x/scale,v.y/scale,v.z/scale);
        void Present(Action draw)
        {
            try{draw();}catch(Exception e){if(presentationErrors.Add(e.Message))Plugin.Log.LogError("Samus presentation failed; character simulation remains active: "+e);}
        }
        bool RecoveryGround(Vector3 sample,float rise,float drop,out Vector3 feet)=>WorldProbe.StandingGround(sample,rise,drop,1.8f,0.3f,out feet);
        public void BeginFallRecovery(Vector3 safeFeet)
        {
            if(!active||Recovering)return;
            Grab?.Dispose();Grab=new SamusGrab(this,body);
            Effects?.Dispose();Particles?.Dispose();Particles=new DiscParticles(scale);Effects=new MeleeEffects(Particles,scale);
            permitDamage=false;
            if(Native.ssbm_sim_begin_rebirth(sim,NativeLib.CString(Plugin.AssetsDir),ToSim(safeFeet))!=1)throw new InvalidOperationException("Melee fall recovery: "+Native.Error);
            recoveryFlags=Native.ssbm_sim_rebirth_flags(sim);
        }
        void Recover(Vector3 feet)
        {
            Native.ssbm_sim_place(sim,ToSim(feet),2);Native.ssbm_sim_state(sim,out state);
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
                int slot=Skills.Slot(e.move_id);var skill=Skills.Get(body,slot);
                var expected=Skills.Expected(body,slot);bool own=skill.skillDef==expected;permitDamage=NativeSkills.Spend(skill,expected)&&own;
                NativeBody.MarkCombat(body);
                MeleeAim.BeginAttack(body,direction,MeleeAim.CameraRay(body,input).direction);
            } else if(e.kind==2&&permitDamage)Combat.Attack(body,e,scale);
            else if(e.kind==6)Grab?.Throw(e);
            else if(e.kind==3)native.Jumped();
            else if(e.kind==4)native.Landed(Vector3.down*e.strength*scale*60f);
        }
        void Fallback(string reason)
        {
            if(failed)return;failed=true;Plugin.Log.LogError("Samus falls back to Commando: "+reason);Cleanup();
            var extraIcon=GetComponent<ExtraSkillIcon>();if(extraIcon)extraIcon.enabled=false;
            var machine=EntityStateMachine.FindByCustomName(gameObject,"Body");if(machine){machine.mainStateType=new SerializableEntityStateType(typeof(GenericCharacterMain));machine.SetNextStateToMain();}
            var commando=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            var stock=commando?commando.GetComponent<SkillLocator>():null;
            if(stock&&body.skillLocator)foreach(var slot in Skills.Slots) {
                var replacement=stock.GetSkill(slot);var skill=body.skillLocator.GetSkill(slot);
                if(skill&&replacement&&replacement.skillFamily&&replacement.skillFamily.variants.Length>0)
                    skill.SetSkillOverride(this,replacement.skillFamily.variants[0].skillDef,GenericSkill.SkillOverridePriority.Contextual);
            }
        }
        void BeginDeath()
        {
            if(deathStarted||!active)return;deathStarted=true;
            try {
                var feet=transform.position-feetOffset;
                var facing=new Vector3(state.facing.x,0,state.facing.z);
                var rotation=facing.sqrMagnitude>0.001f?Quaternion.LookRotation(facing):transform.rotation;
                MeleeDeathSequence.Begin(new SamusDeath(Math.Min(body.skinIndex,4),feet,rotation,scale));
            } catch(Exception e){Plugin.Log.LogError("Samus death presentation failed: "+e);}
            Cleanup();
        }
        void Cleanup()
        {
            pad.ReleaseInteraction();
            shieldView?.Dispose();shieldView=null;
            Arriving=false;recoveryFlags=0;recovery?.Refresh();
            active=false;cameraPivot?.Use(false);view?.Dispose();view=null;
            entryPlatform?.Dispose();entryPlatform=null;
            audio?.Dispose();audio=null;
            Effects?.Dispose();Effects=null;Particles?.Dispose();Particles=null;
            Grab?.Dispose();Grab=null;
            if(sim!=IntPtr.Zero){Native.ssbm_sim_free(sim);sim=IntPtr.Zero;}if(self.IsAllocated)self.Free();
            if(motor&&motor.Motor)motor.Motor.enabled=true;
            if(body&&body.modelLocator&&body.modelLocator.modelTransform){if(!deathStarted)GameSettings.HideRenderers(body.modelLocator.modelTransform,false);var a=body.modelLocator.modelTransform.GetComponent<Animator>();if(a)a.enabled=!deathStarted;}
        }
        void OnDestroy(){if(active&&body&&body.healthComponent&&!body.healthComponent.alive)BeginDeath();Cleanup();}
    }
}
