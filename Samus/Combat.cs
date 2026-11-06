using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Samus
{
    // Disc-command hitboxes are posed on their original bones. The host converts
    // their geometry and maps Melee damage to the survivor's RoR2 damage stat.
    internal static class Combat
    {
        internal static void Attack(CharacterBody body,MeleeNative.Event e,float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            var aim=new Vector3(e.direction.x,e.direction.y,e.direction.z).normalized;
            var origin=new Vector3(e.position.x,e.position.y,e.position.z)*scale;
            if(e.move_id!=24&&body.inputBank) {
                // Converge the cannon on the live crosshair rather than the
                // aim snapshot captured before several seconds of charging.
                aim=MeleeAim.ProjectileDirection(body,origin,MeleeAim.CameraRay(body,body.inputBank));
            }
            switch(e.move_id) {
                case 20: Shot.Spawn(body,origin,aim,1,(uint)Mathf.Clamp(Mathf.RoundToInt(e.strength*7)+1,1,8),e.strength,scale);break;
                case 21: Shot.Spawn(body,origin,aim,2,0,0,scale);break;
                case 22: Shot.Spawn(body,origin,aim,2,1,0,scale);break;
                case 24:
                    // The combined move emits its one bomb at entry; Shield has none.
                    if(Skills.BombSelected(body)) Shot.Spawn(body,origin,Vector3.zero,0,0,0,scale);
                    break;
            }
        }
        internal sealed class HitTracker
        {
            ulong serial=ulong.MaxValue;
            readonly Dictionary<(uint,uint),HashSet<HealthComponent>> victims=new Dictionary<(uint,uint),HashSet<HealthComponent>>();
            readonly Dictionary<(uint,uint,uint),Vector3> previous=new Dictionary<(uint,uint,uint),Vector3>();
            Collider[] candidates=new Collider[64];
            internal void Apply(CharacterBody owner,MeleeNative.HitboxSample[] samples,int count,float scale)
            {
                if (!MeleeNet.Acts(owner)) return;
                for(int i=0;i<count;i++) {
                    var hit=samples[i];
                    var grab=owner.GetComponent<SamusDriver>()?.Grab;
                    if(grab!=null&&grab.Pummel(in hit))continue;
                    if(serial!=hit.serial){serial=hit.serial;victims.Clear();previous.Clear();}
                    var forward=new Vector3(hit.facing.x,0,hit.facing.z);
                    var rotation=forward.sqrMagnitude>0.0001f?Quaternion.LookRotation(forward):Quaternion.identity;
                    var local=new Vector3(-hit.local_position.x,hit.local_position.y,hit.local_position.z);
                    var point=new Vector3(hit.origin.x,hit.origin.y,hit.origin.z)*scale+rotation*local*scale;
                    var key=(hit.group,hit.generation,hit.id);
                    if(!previous.TryGetValue(key,out var from))from=point;
                    previous[key]=point;
                    int n;
                    while(true) {
                        n=Physics.OverlapCapsuleNonAlloc(from,point,hit.radius*scale,candidates,LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Collide);
                        if(n<candidates.Length||candidates.Length>=1024)break;
                        candidates=new Collider[candidates.Length*2];
                    }
                    var group=(hit.group,hit.generation);
                    if(!victims.TryGetValue(group,out var seen)){seen=new HashSet<HealthComponent>();victims[group]=seen;}
                    for(int j=0;j<n;j++) {
                        var hurt=candidates[j]?candidates[j].GetComponent<HurtBox>():null;
                        if(!hurt||!hurt.healthComponent||!hurt.healthComponent.alive||seen.Contains(hurt.healthComponent))continue;
                        var target=hurt.healthComponent.body;
                        if(!target||!target.teamComponent||target.teamComponent.teamIndex==owner.teamComponent.teamIndex)continue;
                        bool ground=target.characterMotor&&target.characterMotor.isGrounded;
                        if((hit.flags&(ground?1u:2u))==0)continue;
                        if(hit.element==8&&hit.damage==0)continue; // The shared aimed grapple trace owns captures.
                        if(hit.damage<=0)continue;
                        float angle=hit.angle==361?45f:Mathf.Min(hit.angle,360u);
                        var direction=forward.normalized*Mathf.Cos(angle*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(angle*Mathf.Deg2Rad);
                        var type=hit.element==1?DamageType.IgniteOnHit:DamageType.Generic;
                        if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec {
                            coefficient=hit.damage/3f,position=hurt.collider.ClosestPoint(point),force=direction*(hit.base_knockback+hit.growth)*5f,
                            procCoefficient=1f,damageType=type,color=DamageColorIndex.Default,staggerDuration=Plugin.StaggerDuration.Value
                        })) {
                            seen.Add(hurt.healthComponent);                            owner.GetComponent<MeleeDriver>()?.HitSpark(hit.element,hit.damage,hurt.collider.ClosestPoint(point));
                            var driver=owner.GetComponent<SamusDriver>();
                            // The Morph Ball's contact hitbox (0x8001) bounces Samus back off what it hits,
                            // instead of ending the roll; an armor break is the only thing that knocks her out.
                            if(hit.id==0x8001&&driver!=null&&driver.Simulation!=System.IntPtr.Zero)MeleeNative.ssbm_sim_bonk_roll(driver.Simulation);
                            uint effect=MeleeNative.ssbm_hit_feedback(driver?driver.Simulation:System.IntPtr.Zero,hit.element,(hit.flags>>8)&31,(hit.flags>>16)&7);
                            if(effect!=0)driver?.Effects?.Spawn(effect,MeleeParticles.At(hurt.collider.ClosestPoint(point),Quaternion.identity,scale));
                        }
                    }
                }
            }
        }
        internal static void Area(CharacterBody owner,Vector3 point,float radius,float coefficient,float proc,Vector3 direction,HashSet<HealthComponent> seen=null,uint element=0)
        {
            if(coefficient<=0||radius<=0)return;
            if(seen==null)seen=new HashSet<HealthComponent>();var search=new SphereSearch { origin=point,radius=radius,mask=LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach(var hurt in search.GetHurtBoxes()) {
                if(!hurt||!hurt.healthComponent||!seen.Add(hurt.healthComponent))continue;
                if(NativeHit.Deal(owner,hurt.healthComponent,new NativeHitSpec { coefficient=coefficient,position=hurt.transform.position,
                    force=direction*500f,procCoefficient=proc,damageType=element==1?DamageType.IgniteOnHit:DamageType.Generic,color=DamageColorIndex.Default })) {
                    var driver=owner.GetComponent<SamusDriver>();
                    uint effect=MeleeNative.ssbm_hit_feedback(driver?driver.Simulation:System.IntPtr.Zero,element,element==2?7u:2u,2);
                    if(effect!=0)driver?.Effects?.Spawn(effect,MeleeParticles.At(hurt.transform.position,Quaternion.identity,driver.Units));
                }
            }
        }
    }
    internal sealed class Shot:MonoBehaviour
    {
        const float FullChargeCoefficient=30f; // 3000% at full charge (host adaptation: the charge time must pay off)
        const float ChargeShotMinCoefficient=8f; // 800% uncharged floor: much higher than the Scope's new minimum
        CharacterBody owner;Vector3 origin,velocity;float modelScale,speed=1f,age;bool impacted;bool spawnedInMapZone;float spawnCoefficient;readonly Collider[] assistHits=new Collider[16];
        System.IntPtr projectile;MeleeNative.ProjectileState state;
        readonly HashSet<HealthComponent> explosionVictims=new HashSet<HealthComponent>();
        HealthComponent target;SamusDriver driver;MeleeFighterView view;MeleeParticles particles;ulong particleKey;uint particleKind,particleState,particleFrame;bool fullCharge;readonly uint[] effectCues=new uint[32];
        internal static void Spawn(CharacterBody owner,Vector3 point,Vector3 direction,uint article,uint state,float charge,float scale)
        {
            var go=new GameObject("Samus disc article");go.transform.position=point;
            var shot=go.AddComponent<Shot>();shot.owner=owner;shot.origin=point;shot.modelScale=scale;shot.fullCharge=charge>=1;
            // A map zone that already contains the muzzle must not cull the shot on its
            // first step; only a shot that flies into one away from its launch point is.
            shot.spawnedInMapZone=WorldProbe.InMapZone(point);
            // Host adaptation: the Charge Shot and the missiles fly this many times as fast as the source's (the bomb is
            // placed, not flown). Only the distance covered is scaled; their size, damage and timing are the disc's.
            shot.speed=article==1||article==2?Mathf.Max(1f,Plugin.ProjectileSpeed.Value):1f;
            shot.driver=owner.GetComponent<SamusDriver>();shot.particles=shot.driver?.Particles;shot.particleKey=0x100000000ul|(uint)go.GetInstanceID();shot.particleKind=article;shot.particleState=state;
            try {
                shot.projectile=MeleeNative.ssbm_projectile_new(NativeLib.CString(Plugin.AssetsDir),MeleeNative.FighterSamus,article,state,charge,new MeleeNative.Vec3(direction.x,direction.y,direction.z));
                if(shot.projectile==System.IntPtr.Zero)throw new System.InvalidOperationException(MeleeNative.Error);
                if(article==1)MeleeNative.ssbm_projectile_lifetime(shot.projectile,0); // RoR2 gun travel: no offscreen lifetime cutoff.
                if(article==2)MeleeNative.ssbm_projectile_lifetime(shot.projectile,0); // Culled by Culled(), not by Melee's off-screen cutoff.
                shot.view=new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,article,state);
                shot.ReadState();
                shot.spawnCoefficient=shot.state.damage/3f;
            }
            catch(System.Exception e) { Plugin.Log.LogError("Samus article presentation failed: "+e.Message);Object.Destroy(go);return; }
            if(article==2&&state==0){var lockOn=new EnemyLock { Range=Mathf.Max(45f,Plugin.MissileRange.Value), DistanceWeight=.00002f, FromCamera=true };shot.target=lockOn.Update(owner,true,false,Vector2.zero)?.healthComponent;}
            var transform=MeleeParticles.At(point,direction.sqrMagnitude>0?Quaternion.LookRotation(direction):Quaternion.identity,scale);
            // efAlt_Spawn: normal/smash missile source generator graphs.
            shot.SourceEffects(uint.MaxValue,0,transform);
        }
        void FixedUpdate()
        {
            if(!owner||!owner.healthComponent||!owner.healthComponent.alive||!Run.instance){Destroy(gameObject);return;}
            if(projectile==System.IntPtr.Zero)return;
            var from=transform.position;
            var at=target&&target.alive?(target.transform.position-origin)/(modelScale*speed):Vector3.zero;
            MeleeNative.ssbm_projectile_step(projectile,Time.fixedDeltaTime,new MeleeNative.Vec3(at.x,at.y,at.z),target&&target.alive?1u:0u);
            ReadState();
            if(!impacted&&(state.flags&1)!=0)Impact(from);
            age+=Time.fixedDeltaTime;
            if(!impacted&&Culled(transform.position)){Destroy(gameObject);return;}
            if(!impacted) {
                var next=origin+new Vector3(state.position.x,state.position.y,state.position.z)*(modelScale*speed);
                var step=next-from;
                if(particleKind==1) {
                    if(step.sqrMagnitude>0&&MeleeSweep.Ranged(owner,from,state.radius*modelScale*HitboxScale,step.normalized,step.magnitude,out var chargeHit))Impact(chargeHit.point);
                    else if(AssistTarget(from,next,out var assistPoint))Impact(assistPoint);
                    else transform.position=next;
                }
                else if(step.sqrMagnitude>0&&Physics.SphereCast(from,state.radius*modelScale,step.normalized,out var hit,step.magnitude,WorldProbe.Mask|LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Ignore)) {
                    var hurt=hit.collider.GetComponent<HurtBox>();
                    bool friendly=hurt&&hurt.healthComponent&&hurt.healthComponent.body&&hurt.healthComponent.body.teamComponent.teamIndex==owner.teamComponent.teamIndex;
                    if(friendly)transform.position=next;
                    else if(particleKind==0&&!hurt) {
                        transform.position=hit.point+hit.normal*state.radius*modelScale;
                        var relative=(transform.position-origin)/modelScale;
                        MeleeNative.ssbm_projectile_place(projectile,new MeleeNative.Vec3(relative.x,relative.y,relative.z),1);
                    } else Impact(hit.point);
                } else transform.position=next;
            }
            if((state.flags&2)!=0){Destroy(gameObject);return;}
            if(impacted&&particleKind==0&&state.damage>0)Combat.Area(owner,transform.position,BlastRadius,ImpactCoefficient,1f,Vector3.up,explosionVictims,state.element);
            var particleTransform=MeleeParticles.At(transform.position,velocity.sqrMagnitude>0?Quaternion.LookRotation(velocity):Quaternion.identity,modelScale);
            particles?.Attach(particleKey,particleTransform);
            uint frame=(uint)state.frame;if(!impacted)SourceEffects(particleFrame,frame,particleTransform);particleFrame=frame;
        }
        // The only cull a flying shot has (it has no range cap): it has flown long enough, left the stage's bounds
        // (a map zone that returns bodies) or fallen far below the stage's lowest ground.
        const float MaxSeconds=20f,BelowStage=30f;
        bool Culled(Vector3 at)=>age>MaxSeconds||(!spawnedInMapZone&&WorldProbe.InMapZone(at))||(StageFloor.TryLowest(out float floor)&&at.y<floor-BelowStage);
        float HitboxScale=>particleKind==1?Plugin.ChargeShotHitboxScale.Value:1f;
        // RoR2 tuning for the combined Morph Ball Bomb (source bomb data unchanged).
        float BlastRadius=>state.radius*modelScale*(particleKind==0?1.5f:HitboxScale);
        float DamageScale=>particleKind==0?2f:particleKind==2?Plugin.MissileDamageScale.Value:1f;
        // A fully charged shot is at least 2000% (coefficient 20). Use the coefficient
        // captured at spawn: the projectile's damage field changes on impact.
        float ImpactCoefficient=>particleKind==1
            ?(fullCharge?Mathf.Max(spawnCoefficient,FullChargeCoefficient):Mathf.Max(state.damage/3f,ChargeShotMinCoefficient))
            :state.damage/3f*DamageScale;
        // Endpoint overlap fallback for close enemies already inside the sphere.
        // The shared ranged sweep has first refusal, including nearer terrain.
        bool AssistTarget(Vector3 from,Vector3 next,out Vector3 point)
        {
            point=next;
            float scale=HitboxScale;
            if(particleKind!=1||scale<=1.01f)return false;
            float radius=state.radius*modelScale*scale;
            int n=Physics.OverlapSphereNonAlloc(next,radius,assistHits,LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(Enemy(assistHits[i])){point=assistHits[i].ClosestPoint(next);return true;}
            return false;
        }
        bool Enemy(Collider collider)
        {
            var hurt=collider?collider.GetComponent<HurtBox>():null;
            var body=hurt&&hurt.healthComponent?hurt.healthComponent.body:null;
            return body&&body.teamComponent&&hurt.healthComponent.alive&&body.teamComponent.teamIndex!=owner.teamComponent.teamIndex;
        }
        void ReadState()
        {
            if(MeleeNative.ssbm_projectile_state(projectile,out state)!=1)throw new System.InvalidOperationException(MeleeNative.Error);
            velocity=new Vector3(state.velocity.x,state.velocity.y,state.velocity.z);
        }
        void Impact(Vector3 at)
        {
            if(impacted)return;impacted=true;transform.position=at;particles?.Stop(particleKey);
            var force=velocity.sqrMagnitude>0?velocity.normalized:Vector3.up;
            uint cue=MeleeNative.ssbm_projectile_impact(projectile,driver?driver.Simulation:System.IntPtr.Zero);ReadState();
            if(cue!=0)driver?.Effects?.Spawn(cue,MeleeParticles.At(at,Quaternion.identity,modelScale));
            Combat.Area(owner,at,BlastRadius,ImpactCoefficient,1f,force,explosionVictims,state.element);
            if((state.flags&2)==0){view.Dispose();view=new MeleeFighterView(Plugin.AssetsDir,MeleeNative.FighterSamus,particleKind,state.state);}
        }
        void LateUpdate(){if(view==null)return;view.ObjectPose(state.animation_frame);var rotation=velocity.sqrMagnitude>0.0001f?Quaternion.LookRotation(velocity):Quaternion.identity;view.FaceCamera(transform.position,rotation,modelScale);view.Models.Update(modelScale,d=>(transform.position,rotation));}
        void SourceEffects(uint previous,uint frame,Matrix4x4 transform)
        {
            uint count=MeleeNative.ssbm_article_cues(particleKind,particleState,previous,frame,fullCharge?1u:0u,effectCues,(uint)effectCues.Length);
            for(int i=0;i<count&&i<effectCues.Length;i++){int target=MeleeNative.ssbm_effect_target(effectCues[i]);if(target>=0&&target<int.MaxValue)particles?.Spawn((uint)target,transform,particleKey);}
        }
        void OnDestroy(){particles?.Stop(particleKey);view?.Dispose();if(projectile!=System.IntPtr.Zero){MeleeNative.ssbm_projectile_free(projectile);projectile=System.IntPtr.Zero;}}
    }
}

