using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Pikachu
{
    // Pikachu's world-side moves. Body hitboxes (A attacks, Skull Bash, Thunder's
    // 17% crackle) are applied by the shared MeleeHitTracker; this adds the two
    // articles: Thunder Shock (the shared bouncing shot) and Thunder's bolt.
    internal static class PikachuCombat
    {
        // itpikachuthunder.c / ftpikachuspecialn.c sound ids.
        const uint ShockBounceSound = 240079; // 0x3A9CF: the jolt landing on a surface
        const float JoltAngle = 40f;          // degrees below level; the disc's -0.698 rad

        internal static void Attack(PikachuDriver driver, CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!NetworkServer.active) return;
            var feet = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            if (e.move_id == 20) Shock(driver, body, feet, scale, e);
            else if (e.move_id == 24)
            {
                if (e.strength < 1.5f) ThunderBolt.Spawn(driver, body, feet, e, scale);
                else ThunderBolt.Strike(driver, body, scale);
            }
        }

        // Thunder Shock: spawned at Pikachu's mouth, then shot along the crosshair like any
        // projectile of the other Melee survivors. The source spawns it at foot height; a
        // crosshair-aimed shot comes from chest height instead.
        static void Shock(PikachuDriver driver, CharacterBody body, Vector3 feet, float scale, MeleeNative.Event e)
        {
            var heading = new Vector3(e.direction.x, 0, e.direction.z);
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : body.transform.forward;
            var muzzle = feet + Vector3.up * 0.6f + heading * 0.5f;
            var aim = body.inputBank
                ? MeleeAim.ProjectileDirection(body, muzzle, MeleeAim.CameraRay(body, body.inputBank))
                : heading;
            // The jolt leaves diagonally downward (itPikachutJoltGroundAttributes.x4 is -0.698 rad on the
            // disc), grounded or not. An aim near level takes that angle; a deliberately raised or lowered
            // crosshair keeps its own, so the shot still goes where it is pointed (host adaptation).
            if (Mathf.Abs(aim.y) < Mathf.Sin(8f * Mathf.Deg2Rad))
            {
                var level = new Vector3(aim.x, 0f, aim.z);
                level = level.sqrMagnitude > 1e-4f ? level.normalized : heading;
                aim = level * Mathf.Cos(JoltAngle * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(JoltAngle * Mathf.Deg2Rad);
            }
            MeleeBouncingShot.Spawn(body, muzzle, aim, new MeleeBouncingShot.Config
            {
                AssetsDir = Samus.Plugin.AssetsDir,
                Fighter = MeleeNative.FighterPikachu,
                // Article 1 is the travelling orb (itpikachutjoltground); it has no
                // model, so the orb is drawn with article 2's (the ground jolt) model
                // and the orb's own trail particle (efSync 0x4BD = EfPkData 7000).
                Article = 1, State = 0,
                ViewArticle = 2, ViewState = 0,
                Scale = scale,
                Particles = driver.Particles,
                TrailParticle = 7000, BouncePuff = 7001, ImpactPuff = 7002, DespawnPuff = 7003,
                PlaySound = id => { if (driver.NativeSim != IntPtr.Zero) MeleeNative.ssbm_sim_play_sound(driver.NativeSim, id); },
                BounceSound = ShockBounceSound,
                // It follows the ground, walls and ceilings after its first contact (itpikachutjoltground.c).
                Crawl = true,
            });
        }
    }

    // Thunder (itpikachuthunder.c): four bolt segments fall from the cloud, each eight
    // ticks after the one before, so they form one column; the leading tip reaches
    // Pikachu on the tick the sim strikes him. Enemies in the column are hit once each.
    // The cloud's height, the strike delay and the fall speed come from the sim's spawn
    // event (disc tuning); only the segment count/delay (xD4/xD8) are constants here.
    internal sealed class ThunderBolt : MonoBehaviour
    {
        const int Segments = 4;      // ftPikachuAttributes.xD4
        const int SegmentDelay = 8;  // ftPikachuAttributes.xD8
        const float SegmentLength = 40f; // the article's quad height in Melee units
        const int LingerTicks = 14;

        static ThunderBolt current;

        PikachuDriver driver;
        CharacterBody owner;
        Vector3 feet;
        float scale, bottomStart, strikeY, speed, damage, radius;
        int strikeTick, tick;
        readonly MeleeFighterView[] segments = new MeleeFighterView[Segments];
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();
        Collider[] candidates = new Collider[32];

        internal static void Spawn(PikachuDriver driver, CharacterBody owner, Vector3 feet, MeleeNative.Event e, float scale)
        {
            if (current) Destroy(current.gameObject);
            var go = new GameObject("Pikachu Thunder bolt");
            var bolt = go.AddComponent<ThunderBolt>();
            current = bolt;
            bolt.driver = driver;
            bolt.owner = owner;
            bolt.feet = feet;
            bolt.scale = scale;
            // Event direction = (cloud height, ticks to strike, fall speed) in Melee units.
            float height = e.direction.x * scale;
            bolt.strikeTick = Mathf.Max(1, Mathf.RoundToInt(e.direction.y));
            float length = SegmentLength * scale;
            bolt.strikeY = feet.y + 0.7f;
            float ceiling = height;
            if (Physics.Raycast(feet + Vector3.up * 1.8f, Vector3.up, out var roof, height, WorldProbe.Mask, QueryTriggerInteraction.Ignore))
                ceiling = roof.distance + 1.8f - 0.15f;
            // Quad top sits at the cloud; its tip is one segment lower. A low ceiling
            // shortens the fall but never the strike delay.
            bolt.bottomStart = Mathf.Max(bolt.strikeY + 1f, feet.y + Mathf.Min(height, ceiling) - length);
            bolt.speed = (bolt.bottomStart - bolt.strikeY) / bolt.strikeTick;
            try
            {
                for (int i = 0; i < Segments; i++)
                    bolt.segments[i] = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, 0, 0);
                // The leading segment's hitbox (itpikachuthunder article 0, state 0).
                var probe = MeleeNative.ssbm_projectile_new(NativeLib.CString(Samus.Plugin.AssetsDir), MeleeNative.FighterPikachu, 0, 0, 0, new MeleeNative.Vec3(0, 0, 1));
                if (probe != IntPtr.Zero)
                {
                    if (MeleeNative.ssbm_projectile_state(probe, out var state) == 1) { bolt.damage = state.damage; bolt.radius = state.radius * scale; }
                    MeleeNative.ssbm_projectile_free(probe);
                }
            }
            catch (Exception ex) { CommonLog.Error("Pikachu Thunder bolt view: " + ex.Message); }
            if (bolt.damage <= 0f) { bolt.damage = 10f; bolt.radius = 0.7f; }
        }

        /// The strike tick: the camera shake of ftPk_SpecialLw_80127608 (Camera_RequestQuake small).
        internal static void Strike(PikachuDriver driver, CharacterBody owner, float scale)
        {
            var at = owner ? owner.corePosition : Vector3.zero;
            ShakeEmitter.CreateSimpleShakeEmitter(at, new Wave { amplitude = 0.2f, frequency = 14f, cycleOffset = 0 }, 0.25f, 25f, true);
        }

        float BottomOf(int segment)
        {
            int local = tick - SegmentDelay * segment;
            if (local < 0) return float.NaN; // not released yet
            return Mathf.Max(feet.y, bottomStart - speed * local);
        }

        void FixedUpdate()
        {
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            tick++;
            if (tick > strikeTick + SegmentDelay * (Segments - 1) + LingerTicks) { Destroy(gameObject); return; }
            if (!NetworkServer.active) return;
            float length = SegmentLength * scale;
            for (int i = 0; i < Segments; i++)
            {
                float bottom = BottomOf(i);
                if (float.IsNaN(bottom)) continue;
                var low = new Vector3(feet.x, bottom, feet.z);
                var high = new Vector3(feet.x, bottom + length, feet.z);
                int n;
                float hit = Mathf.Max(radius * 2f, 0.9f);
                while (true)
                {
                    n = Physics.OverlapCapsuleNonAlloc(low, high, hit, candidates, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
                    if (n < candidates.Length || candidates.Length >= 512) break;
                    candidates = new Collider[candidates.Length * 2];
                }
                for (int j = 0; j < n; j++)
                {
                    var hurt = candidates[j] ? candidates[j].GetComponent<HurtBox>() : null;
                    if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                    var target = hurt.healthComponent.body;
                    if (!target || !target.teamComponent || target.teamComponent.teamIndex == owner.teamComponent.teamIndex) continue;
                    if (victims.Contains(hurt.healthComponent)) continue;
                    if (NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec
                    {
                        coefficient = damage / 3f, position = hurt.collider.ClosestPoint(low), force = Vector3.down * 400f,
                        procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default,
                        staggerDuration = Samus.Plugin.StaggerDuration.Value, staggerBosses = true,
                    })) victims.Add(hurt.healthComponent);
                }
            }
        }

        void LateUpdate()
        {
            if (!owner) return;
            float length = SegmentLength * scale;
            for (int i = 0; i < Segments; i++)
            {
                var view = segments[i];
                if (view == null) continue;
                float bottom = BottomOf(i);
                bool lingering = tick > strikeTick + SegmentDelay * i + LingerTicks - 4;
                if (float.IsNaN(bottom) || lingering) { view.Models.HideAll(); continue; }
                try
                {
                    var centre = new Vector3(feet.x, bottom + length * 0.5f, feet.z);
                    // The quad hangs from its top edge in the article's local space.
                    var at = centre + Vector3.up * length * 0.5f;
                    // The bolt's own animation runs from the tick this segment is released.
                    view.ObjectPose(Mathf.Max(0f, tick - SegmentDelay * i));
                    view.FaceCamera(at, Quaternion.identity, scale);
                    view.Models.Update(scale, d => (at, Quaternion.identity));
                }
                catch (Exception ex) { CommonLog.Error("Pikachu Thunder bolt draw: " + ex.Message); view.Dispose(); segments[i] = null; }
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < Segments; i++) { segments[i]?.Dispose(); segments[i] = null; }
            if (current == this) current = null;
        }
    }
}
