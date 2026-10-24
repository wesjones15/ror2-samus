using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using Ror2Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Yoshi
{
    // Yoshi's combat: disc hitboxes posed on their original bones, mapped to RoR2
    // damage, plus the tongue and egg, the thrown egg and the Yoshi Bomb's stars.
    internal static class YoshiCombat
    {
        // Host adaptations for an open world. The source tongue is a grab capsule at
        // the mouth; the reach is stretched here so it matches the drawn tongue and
        // the open-world scale (7 m read as short next to RoR2 enemies).
        internal const float TongueReach = 9f;
        const float TongueRadius = 0.7f;
        const float EggSpeed = 4.5f;                 // Melee units per tick of a thrown egg
        const float EggAngleBias = 22f;              // degrees added to the camera pitch
        const float BlastRadius = 4.5f, BlastCoefficient = 7f, BlastOutward = 3000f, BlastUpward = 1500f;

        internal static void Attack(CharacterBody body, MeleeNative.Event e, float scale)
        {
            if (!MeleeNet.Acts(body)) return;
            var aim = new Vector3(e.direction.x, e.direction.y, e.direction.z).normalized;
            var origin = new Vector3(e.position.x, e.position.y, e.position.z) * scale;
            var camera = body.inputBank ? MeleeAim.CameraRay(body, body.inputBank).direction : aim;
            if (e.move_id == 20 && e.strength < 1.5f) Tongue(body, camera);
            else if (e.move_id == 23) ThrowEgg(body, origin, camera, scale);
            else if (e.move_id == 24) Bomb(body, origin, scale);
        }

        /// Where the tongue leaves the body: the sim position is at the feet, so lift it
        /// to the head. Shared by the hit cast and the drawn tongue so they line up.
        internal static Vector3 Mouth(CharacterBody body)
            => body.corePosition + Vector3.up * Mathf.Max(0.35f, body.radius * 0.8f);

        // Egg Lay: the tongue swallows the first enemy in its reach. There is no boss
        // immunity: Mithrix is laid in an egg like anything else. A capsule overlap
        // (rather than a sphere cast) sweeps the whole tongue and cannot be stopped
        // early by the ground under a downward camera aim; walls still block it.
        static void Tongue(CharacterBody body, Vector3 aim)
        {
            if (aim.sqrMagnitude < 0.0001f) aim = body.transform.forward;
            aim = aim.normalized;
            var mouth = Mouth(body);
            var target = FindTarget(body, mouth, aim, out _);
            if (target && YoshiEgg.Lay(body, target)) body.GetComponent<YoshiDriver>()?.QueueConnect();
        }

        static CharacterBody FindTarget(CharacterBody body, Vector3 mouth, Vector3 aim, out float distance)
        {
            distance = TongueReach;
            var hits = new Collider[64];
            int n = Physics.OverlapCapsuleNonAlloc(mouth, mouth + aim * TongueReach, TongueRadius, hits,
                LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            CharacterBody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hurt = hits[i] ? hits[i].GetComponent<HurtBox>() : null;
                if (!hurt || !hurt.healthComponent) continue;
                var target = hurt.healthComponent.body;
                if (!target || target == body || !target.teamComponent
                    || target.teamComponent.teamIndex == body.teamComponent.teamIndex) continue;
                if (!hurt.healthComponent.alive || YoshiEgg.IsTrapped(target)) continue;
                var point = hurt.collider ? hurt.collider.ClosestPoint(mouth) : hurt.transform.position;
                float d = Vector3.Distance(mouth, point);
                if (d > TongueReach || d >= bestDistance) continue;
                // Do not eat through a wall. Terrain only: the victim's own hurtboxes are
                // on the entity layer and are excluded from this mask.
                if (Physics.Linecast(mouth, point, out var wall, WorldProbe.Mask, QueryTriggerInteraction.Ignore)
                    && Vector3.Distance(mouth, wall.point) < d - 0.05f) continue;
                best = target;
                bestDistance = d;
            }
            if (best) distance = bestDistance;
            return best;
        }

        // Egg Throw: the camera pitch sets the arc, as the reticle did, and the egg
        // follows it ballistically (the source's launch speed grows with the time the
        // reticle was up; here it is fixed and the angle is the control).
        static void ThrowEgg(CharacterBody body, Vector3 origin, Vector3 camera, float scale)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(camera.y, -1f, 1f)) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(pitch + EggAngleBias, 5f, 70f) * Mathf.Deg2Rad;
            var flat = new Vector3(camera.x, 0f, camera.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : body.transform.forward;
            Egg.Spawn(body, origin, flat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle), scale, EggSpeed);
        }

        // Yoshi Bomb: the landing's shockwave and the stars that run out from it.
        static void Bomb(CharacterBody body, Vector3 feet, float scale)
        {
            var center = feet + Vector3.up * 0.3f;
            var search = new SphereSearch { origin = center, radius = BlastRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var away = hurt.transform.position - center; away.y = 0f;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                NativeHit.Deal(body, hurt.healthComponent, new NativeHitSpec {
                    coefficient = BlastCoefficient, position = hurt.transform.position,
                    force = dir * BlastOutward + Vector3.up * BlastUpward, procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                });
            }
            const int stars = 8;
            for (int i = 0; i < stars; i++)
            {
                float turn = i * (360f / stars) * Mathf.Deg2Rad;
                YoshiStar.Spawn(body, center, new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)), scale);
            }
        }
    }

    // The drawn Egg Lay tongue. The disc's tongue is a grab capsule with no model of its
    // own in the fighter's costume archive, so the host draws the lash: a tapered,
    // camera-facing ribbon from the mouth to the reach (or to the victim it caught) that
    // shoots out, holds a moment and retracts. Its length is the same reach the capture
    // uses, so what is drawn is what bites.
    internal sealed class YoshiTongue : MonoBehaviour
    {
        // The SpecialN clip runs 40 ticks: the tongue is fully out at its frame-20 grab and
        // back by the end of the clip.
        const float ExtendSeconds = 0.18f, HoldSeconds = 0.22f, RetractSeconds = 0.25f;
        const int Points = 12;
        static Shader shader;
        CharacterBody owner;
        Vector3 aim;
        float reach, age;
        LineRenderer line;
        Material material;

        internal static void Show(CharacterBody owner, Vector3 aim, float reach)
        {
            if (!owner) return;
            var go = new GameObject("Yoshi tongue");
            var tongue = go.AddComponent<YoshiTongue>();
            tongue.owner = owner;
            tongue.aim = aim.normalized;
            tongue.reach = Mathf.Clamp(reach, 1f, 12f);
            if (!shader) shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (!shader) { Destroy(go); return; }
            var line = go.AddComponent<LineRenderer>();
            tongue.line = line;
            tongue.material = new Material(shader) { color = new Color(0.92f, 0.27f, 0.36f, 1f) };
            line.material = tongue.material;
            line.useWorldSpace = true;
            line.positionCount = Points;
            line.startWidth = 0.26f;
            line.endWidth = 0.11f;
            line.numCapVertices = 6;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (age >= ExtendSeconds + HoldSeconds + RetractSeconds) { Destroy(gameObject); return; }
            float amount;
            if (age < ExtendSeconds) amount = age / ExtendSeconds;
            else if (age < ExtendSeconds + HoldSeconds) amount = 1f;
            else amount = 1f - (age - ExtendSeconds - HoldSeconds) / RetractSeconds;
            amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
            float length = reach * amount;
            var origin = YoshiCombat.Mouth(owner);
            for (int i = 0; i < Points; i++)
            {
                float u = i / (Points - 1f);
                var point = origin + aim * (length * u);
                // A little gravity sag keeps it from reading as a laser.
                point.y -= Mathf.Sin(u * Mathf.PI) * 0.06f * length;
                line.SetPosition(i, point);
            }
            line.enabled = amount > 0.01f;
        }

        void OnDestroy()
        {
            if (line) line.material = null;
            if (material) { Destroy(material); material = null; }
        }
    }

    // An enemy laid in an egg (Egg Lay). EnemyHold keeps it helpless where it stands until
    // it breaks out: a few seconds for most, longer for the large and longest for bosses.
    // It can still be hit meanwhile. The egg is sized to the victim, but not to scale: a
    // large enemy's egg tops out near 2.4 m, a boss's is bigger than that and still well
    // short of the boss itself.
    internal sealed class YoshiEgg : MonoBehaviour
    {
        const float NormalSeconds = 4f, LargeSeconds = 6f, BossSeconds = 9f;
        const float LargeRadius = 1.6f;
        // Costume model DObj 35's half-width in Melee units (the egg that replaces Yoshi
        // in the roll); the laid egg uses the same mesh through the variant view.
        const float EggModelRadiusUnits = 5.77f;

        static readonly List<YoshiEgg> live = new List<YoshiEgg>();
        internal static bool IsTrapped(CharacterBody body)
        {
            foreach (var egg in live) if (egg && egg.victim == body) return true;
            return false;
        }

        // The egg's radius for a victim of this size and kind.
        internal static float EggRadius(CharacterBody victim)
        {
            float r = Mathf.Max(0.3f, victim.radius);
            if (MeleeGrapple.IsBoss(victim)) return Mathf.Clamp(r * 0.5f, 3f, 5.5f);
            return Mathf.Clamp(0.55f + r * 0.9f, 0.8f, 2.4f);
        }

        CharacterBody owner, victim;
        float releaseAt, eggRadius;
        MeleeFighterView view;
        Transform victimModel;

        internal static bool Lay(CharacterBody owner, CharacterBody victim)
        {
            if (!MeleeNet.Acts(owner) || !victim || IsTrapped(victim)) return false;
            bool boss = MeleeGrapple.IsBoss(victim);
            float seconds = boss ? BossSeconds : victim.radius >= LargeRadius ? LargeSeconds : NormalSeconds;
            if (!EnemyHold.Hold(victim, seconds)) return false;
            var go = new GameObject("Yoshi egg (laid)");
            var egg = go.AddComponent<YoshiEgg>();
            egg.owner = owner;
            egg.victim = victim;
            egg.eggRadius = EggRadius(victim);
            egg.releaseAt = Time.time + seconds;
            if (victim.modelLocator && victim.modelLocator.modelTransform) {
                egg.victimModel = victim.modelLocator.modelTransform;
                GameSettings.HideRenderers(egg.victimModel, true);
            }
            try {
                egg.view = MeleeFighterView.FighterChoice(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi,
                    (uint)Math.Min(owner.skinIndex, 5), 1);
                egg.view.ObjectPose(0f);
            } catch (Exception e) {
                CommonLog.Warn("Yoshi egg view: " + e.Message);
                egg.view?.Dispose(); egg.view = null;
            }
            go.transform.position = EggFoot(victim);
            return true;
        }

        static Vector3 EggFoot(CharacterBody victim)
            => victim.characterMotor && victim.characterMotor.isGrounded ? victim.footPosition : victim.corePosition;

        void OnEnable() { live.Add(this); }
        void OnDestroy()
        {
            live.Remove(this);
            if (victimModel) { GameSettings.HideRenderers(victimModel, false); victimModel = null; }
            view?.Dispose(); view = null;
        }

        void FixedUpdate()
        {
            if (!MeleeNet.Acts(owner)) return;
            // The hold ends it: it ran out, or the victim died.
            // On the server the hold itself says so; a client fighter's egg runs on its own clock.
            bool over = NetworkServer.active ? !victim || !EnemyHold.IsHeld(victim)
                : !victim || !victim.healthComponent || !victim.healthComponent.alive || Time.time >= releaseAt;
            if (over) { BreakOut(); return; }
            if (victimModel) GameSettings.HideRenderers(victimModel, true);
            transform.position = EggFoot(victim);
        }

        void BreakOut()
        {
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect && victim) EffectManager.SpawnEffect(effect, new EffectData { origin = victim.corePosition, scale = eggRadius }, NetworkServer.active);
            } catch (Exception) { }
            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (view == null) return;
            // The egg sways more as it is about to hatch.
            float left = Mathf.Max(0f, releaseAt - Time.time);
            float shake = left < 1.5f ? Mathf.Sin(Time.time * 40f) * 0.06f * (1.5f - left) : 0f;
            var rotation = Quaternion.Euler(0f, 0f, shake * 57f);
            float units = eggRadius / EggModelRadiusUnits;
            view.FaceCamera(transform.position, rotation, units);
            view.Models.Update(units, d => (transform.position, rotation));
        }
    }
    // A star from the Yoshi Bomb's landing: it runs out along the ground and hits what it meets.
    internal sealed class YoshiStar : MonoBehaviour
    {
        const float Speed = 14f, Life = 0.9f, Radius = 0.7f, Coefficient = 2f, Force = 1800f;
        CharacterBody owner;
        Vector3 direction;
        float scale, age;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();
        static readonly Collider[] touching = new Collider[32];

        internal static void Spawn(CharacterBody owner, Vector3 at, Vector3 direction, float scale)
        {
            var go = new GameObject("Yoshi star");
            go.transform.position = at;
            var star = go.AddComponent<YoshiStar>();
            star.owner = owner; star.direction = direction.normalized; star.scale = scale;
            star.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, 1, 0);
            star.view.ObjectPose(0f);
        }

        void FixedUpdate()
        {
            if (owner && !MeleeNet.Acts(owner)) return;
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (age > Life || !owner) { Destroy(gameObject); return; }
            var from = transform.position;
            var next = from + direction * (Speed * dt);
            // Stay on the ground it runs along; a wall ends it.
            if (Physics.Raycast(next + Vector3.up * 0.6f, Vector3.down, out var floor, 2.5f, WorldProbe.Mask, QueryTriggerInteraction.Ignore))
                next.y = floor.point.y + 0.4f;
            if (Physics.Raycast(from, direction, out _, Speed * dt + Radius, WorldProbe.Mask, QueryTriggerInteraction.Ignore)) { Destroy(gameObject); return; }
            transform.position = next;
            int n = Physics.OverlapSphereNonAlloc(next, Radius, touching, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var hurt = touching[i] ? touching[i].GetComponent<HurtBox>() : null;
                if (!hurt || !hurt.healthComponent || !hurt.healthComponent.alive) continue;
                var body = hurt.healthComponent.body;
                if (!body || !body.teamComponent || body.teamComponent.teamIndex == owner.teamComponent.teamIndex) continue;
                if (!victims.Add(hurt.healthComponent)) continue;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = Coefficient, position = hurt.transform.position,
                    force = direction * Force + Vector3.up * (Force * 0.4f), procCoefficient = 1f,
                    damageType = DamageType.Generic, color = DamageColorIndex.Default,
                });
            }
        }

        void LateUpdate()
        {
            if (view == null) return;
            var rotation = Quaternion.Euler(0f, age * 540f, 0f);
            view.FaceCamera(transform.position, rotation, scale);
            view.Models.Update(scale, d => (transform.position, rotation));
        }
        void OnDestroy() { view?.Dispose(); view = null; }
    }

    // Yoshi's thrown egg: spawns the native article, steps it along its ballistic arc, and
    // bursts on the first enemy or wall it meets, hitting everything near.
    internal sealed class Egg : MonoBehaviour
    {
        const float BurstRadius = 2f, BurstForce = 1500f;   // metres, newtons (host adaptation)
        CharacterBody owner;
        Vector3 origin;
        float modelScale;
        bool impacted;
        IntPtr projectile;
        MeleeNative.ProjectileState state;
        MeleeFighterView view;
        readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();

        internal static void Spawn(CharacterBody owner, Vector3 point, Vector3 direction, float scale, float speed)
        {
            var go = new GameObject("Yoshi egg");
            go.transform.position = point;
            var shot = go.AddComponent<Egg>();
            shot.owner = owner;
            shot.origin = point;
            shot.modelScale = scale;
            // The launch speed travels in the charge argument (libssbm uses it for the egg).
            shot.projectile = MeleeNative.ssbm_projectile_new(NativeLib.CString(Samus.Plugin.AssetsDir), MeleeNative.FighterYoshi, 0, 0, speed, new MeleeNative.Vec3(direction.x, direction.y, direction.z));
            if (shot.projectile == IntPtr.Zero) { Destroy(go); return; }
            MeleeNative.ssbm_projectile_lifetime(shot.projectile, 0);
            shot.view = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterYoshi, 0, 0);
            shot.ReadState();
        }

        void FixedUpdate()
        {
            if (owner && !MeleeNet.Acts(owner)) return;
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            if (projectile == IntPtr.Zero) return;
            var from = transform.position;
            MeleeNative.ssbm_projectile_step(projectile, Time.fixedDeltaTime, default, 0);
            ReadState();
            if (!impacted && (state.flags & 1) != 0) Impact(from);
            if (!impacted)
            {
                var next = origin + new Vector3(state.position.x, state.position.y, state.position.z) * modelScale;
                var step = next - from;
                if (step.sqrMagnitude > 0 && MeleeSweep.First(owner, from, Mathf.Max(state.radius * modelScale, 0.25f), step.normalized, step.magnitude, WorldProbe.Mask | LayerIndex.entityPrecise.mask, out var hit))
                {
                    var struck = hit.collider.GetComponent<HurtBox>();
                    if (!struck && hit.normal.y > 0.5f)
                    {
                        // Ground: bounce (Melee-style), bursting once the source's
                        // bounce budget is spent. Walls and ceilings burst it.
                        if (MeleeNative.ssbm_projectile_bounce(projectile) == 0) Impact(hit.point);
                        else
                        {
                            var nativePoint = new MeleeNative.Vec3(
                                (hit.point.x - origin.x) / modelScale,
                                (hit.point.y - origin.y) / modelScale,
                                (hit.point.z - origin.z) / modelScale);
                            MeleeNative.ssbm_projectile_place(projectile, nativePoint, 0);
                            transform.position = hit.point + Vector3.up * 0.02f;
                        }
                    }
                    else Impact(hit.point);
                }
                else transform.position = next;
            }
            if ((state.flags & 2) != 0) { Destroy(gameObject); return; }
        }

        void Impact(Vector3 at)
        {
            if (impacted) return;
            impacted = true;
            transform.position = at;
            MeleeNative.ssbm_projectile_impact(projectile, IntPtr.Zero);
            ReadState();
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect) EffectManager.SpawnEffect(effect, new EffectData { origin = at, scale = BurstRadius }, NetworkServer.active);
            } catch (Exception) { }
            var search = new SphereSearch { origin = at, radius = Mathf.Max(state.radius * modelScale, BurstRadius), mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex)).FilterCandidatesByDistinctHurtBoxEntities();
            foreach (var hurt in search.GetHurtBoxes())
            {
                if (!hurt || !hurt.healthComponent || !victims.Add(hurt.healthComponent)) continue;
                var away = hurt.transform.position - at;
                var dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                NativeHit.Deal(owner, hurt.healthComponent, new NativeHitSpec {
                    coefficient = state.damage / 3f, position = hurt.transform.position, force = dir * BurstForce + Vector3.up * (BurstForce * 0.3f),
                    procCoefficient = 1f, damageType = DamageType.Generic, color = DamageColorIndex.Default
                });
            }
        }

        void ReadState() { if (MeleeNative.ssbm_projectile_state(projectile, out state) != 1) throw new InvalidOperationException(MeleeNative.Error); }
        void LateUpdate()
        {
            if (view == null) return;
            view.ObjectPose(state.animation_frame);
            var velocity = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z);
            var rotation = velocity.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
            view.FaceCamera(transform.position, rotation, modelScale);
            view.Models.Update(modelScale, d => (transform.position, rotation));
        }
        void OnDestroy() { view?.Dispose(); view = null; if (projectile != IntPtr.Zero) { MeleeNative.ssbm_projectile_free(projectile); projectile = IntPtr.Zero; } }
    }
}
