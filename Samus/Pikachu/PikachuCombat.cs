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
    // articles: Thunder Jolt (the shared bouncing shot) and Thunder's bolt.
    internal static class PikachuCombat
    {
        // itpikachuthunder.c / ftpikachuspecialn.c sound ids.
        const uint ShockBounceSound = 240079; // 0x3A9CF: the jolt landing on a surface

        internal static void Attack(PikachuDriver driver, CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            var feet = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            if (e.move_id == 20)
            {
                // The RT slot can hold Yoshi's Egg Lay instead; that move's tongue and
                // egg belong to PikachuDriver/YoshiTongue, not to Thunder Jolt.
                if (!PikachuSkills.EggLaySelected(body)) Shock(driver, body, feet, scale, e);
            }
            else if (e.move_id == 24)
            {
                if (e.strength < 1.5f) ThunderBolt.Spawn(driver, body, feet, e, scale);
                else ThunderBolt.Strike(driver, body, scale);
            }
        }

        // Thunder Jolt: spawned at Pikachu's mouth, then shot along the crosshair like any
        // projectile of the other Melee survivors. The source spawns it at foot height; a
        // crosshair-aimed shot comes from chest height instead. It rides the shared bouncing
        // shot, the same arc and surface bounce as Luigi's fireball, and ends after ten surface
        // contacts (Projectile::bounce / reflect). It does not use the shot's Crawl mode: the
        // source's terrain-hugging crawl (itpikachutjoltground.c) read as a rolling ball and
        // never bounced, which broke the design rule for this move (ddba74e regressed it).
        static void Shock(PikachuDriver driver, CharacterBody body, Vector3 feet, float scale, MeleeNative.Event e)
        {
            var heading = new Vector3(e.direction.x, 0, e.direction.z);
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : body.transform.forward;
            var muzzle = feet + Vector3.up * 0.6f + heading * 0.5f;
            var aim = body.inputBank
                ? MeleeAim.ProjectileDirection(body, muzzle, MeleeAim.CameraRay(body, body.inputBank))
                : heading;
            MeleeBouncingShot.Spawn(body, muzzle, aim, new MeleeBouncingShot.Config
            {
                AssetsDir = Samus.Plugin.AssetsDir,
                Fighter = MeleeNative.FighterPikachu,
                // Article 1 is the travelling ground jolt (itpikachutjoltground.c); it has no
                // model. Its whole visual is effect 0x4BD, attached to the item:
                //   efsync.c case 0x4BD -> hsd_8039EFAC(0, 7, 0x1B58, jobj)
                //   = EfPkData generator 7000, attached to the jolt item's JObj.
                // Article 2 is the air-jolt arc the source spawns on a surface contact
                // (it_802B3554 -> it_802B4224, itpikachutjoltair.c xDF4 <= 24). It is drawn
                // only as a short flash at each bounce, never carried by the flying orb (a
                // 36-frame clip on a 10-second shot froze into a static white web).
                Article = 1, State = 0,
                ViewArticle = uint.MaxValue,
                Scale = scale,
                // Host adaptation: fly level for 20 m, then the disc's gentle arc and the
                // unchanged ten-surface bounce budget take over.
                StraightMetres = 20f,
                Particles = driver.Particles,
                TrailParticle = 7000, BouncePuff = 0, ImpactPuff = 0, DespawnPuff = 0,
                BounceViewArticle = 2, BounceViewState = 0, BounceViewScale = 0.6f, BounceViewFrames = 24,
                PlaySound = id => { if (driver.NativeSim != IntPtr.Zero) MeleeNative.ssbm_sim_play_sound(driver.NativeSim, id); },
                BounceSound = ShockBounceSound,
            });
        }
    }

    // Thunder (itpikachuthunder.c): a fixed 16 m column of four 4 m article-0 bolt
    // segments above Pikachu. The sim still owns the falling hitbox timeline (four
    // segments, one every xD8 ticks); the host no longer raycasts a world ceiling or
    // stretches one quad. Enemies in the column are hit once each.
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
            bolt.strikeTick = Mathf.Max(1, Mathf.RoundToInt(e.direction.y));
            float length = SegmentLength * scale;
            bolt.strikeY = feet.y + 0.7f;
            // Fixed 16 m column above Pikachu: four 4 m article-0 segments, no world
            // ceiling and no stretching. The source's four pieces are the visual too.
            bolt.bottomStart = bolt.strikeY + length * (Segments - 1);
            bolt.speed = (bolt.bottomStart - bolt.strikeY) / bolt.strikeTick;
            try
            {
                for (int i = 0; i < Segments; i++)
                {
                    bolt.segments[i] = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterPikachu, 0, 0);
                    bolt.segments[i].Tint = new Color(1f, 0.92f, 0.3f, 1f);
                }
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
            // ftPk_SpecialLwLoop0: efAsync_Spawn(1216, FtPart_TopN) when the bolt lands.
            driver?.PlayEffect(1216, at + Vector3.up * 1.2f, Quaternion.identity);
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
            if (!MeleeNet.Acts(owner)) return;
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
                bool lingering = tick > strikeTick + LingerTicks - 4;
                if (lingering) { view.Models.HideAll(); continue; }
                try
                {
                    // Fixed stack: segment 0 is the top piece, segment 3 reaches Pikachu.
                    float top = feet.y + length * Segments - i * length;
                    var centre = new Vector3(feet.x, top - length * 0.5f, feet.z);
                    view.ObjectPose(Mathf.Max(0f, tick - strikeTick + i * SegmentDelay));
                    view.FaceCamera(centre, Quaternion.identity, scale);
                    view.Models.Update(scale, d => (centre, Quaternion.identity));
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
