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
        // Host adaptations for an open world. The source tongue is a grab capsule at the
        // mouth; the host stretches it to 20 m so it works as a long-range grab and the
        // drawn lash reaches exactly as far as the cast.
        internal const float TongueReach = 20f;
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
            else if (e.move_id == 23) ThrowEgg(body, camera, scale);
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
            var target = FindTarget(body, mouth, aim, out float distance);
            if (target == null) return;
            // The tongue was drawn to this distance at the start of the move; pin it to the
            // target in case the victim moved into or out of the original line.
            YoshiTongue.SetReach(body, distance + 0.35f);
            if (YoshiFull.Begin(body, target)) body.GetComponent<YoshiDriver>()?.QueueConnect();
        }

        /// Where the drawn tongue should stop: the nearest catchable enemy along the aim, or
        /// the full reach when the lash is thrown into empty space.
        internal static float ScanReach(CharacterBody body, Vector3 aim)
        {
            if (aim.sqrMagnitude < 0.0001f) aim = body.transform.forward;
            var target = FindTarget(body, Mouth(body), aim.normalized, out float distance);
            return target ? Mathf.Min(distance + 0.35f, TongueReach) : TongueReach;
        }

        static CharacterBody FindTarget(CharacterBody body, Vector3 mouth, Vector3 aim, out float distance)
        {
            distance = TongueReach;
            var hits = new Collider[128];
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
                if (!hurt.healthComponent.alive || YoshiEgg.IsTrapped(target) || YoshiFull.IsHolding(target)) continue;
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
        static void ThrowEgg(CharacterBody body, Vector3 camera, float scale)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(camera.y, -1f, 1f)) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(pitch + EggAngleBias, 5f, 70f) * Mathf.Deg2Rad;
            var flat = new Vector3(camera.x, 0f, camera.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : body.transform.forward;
            // Throw from the hand/chest, not the feet: the source spawns at the hand bone.
            // At foot height the egg's first sweep starts inside the ground and bursts.
            var hand = body.corePosition + Vector3.up * 0.2f;
            body.GetComponent<YoshiDriver>()?.PlaySourceSound(0x44612); // SpecialHi frame 18: throw
            Egg.Spawn(body, hand, flat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle), scale, EggSpeed);
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
        const int Points = 16;
        const float TotalSeconds = ExtendSeconds + HoldSeconds + RetractSeconds;
        static readonly List<YoshiTongue> active = new List<YoshiTongue>();
        static Shader shader;
        CharacterBody owner;
        Vector3 aim;
        float reach, age;
        LineRenderer line;
        Material material;

        internal static void Show(CharacterBody owner, Vector3 aim, float reach)
        {
            if (!owner) return;
            for (int i = active.Count - 1; i >= 0; i--)
                if (!active[i] || active[i].owner == owner) { if (active[i]) Destroy(active[i].gameObject); active.RemoveAt(i); }
            var go = new GameObject("Yoshi tongue");
            var tongue = go.AddComponent<YoshiTongue>();
            tongue.owner = owner;
            tongue.aim = aim.normalized;
            tongue.reach = Mathf.Clamp(reach, 1f, YoshiCombat.TongueReach);
            if (!shader) shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (!shader) { Destroy(go); return; }
            var renderer = go.AddComponent<LineRenderer>();
            tongue.line = renderer;
            tongue.material = new Material(shader) { color = new Color(0.92f, 0.27f, 0.36f, 1f) };
            renderer.material = tongue.material;
            renderer.useWorldSpace = true;
            renderer.positionCount = Points;
            float width = Mathf.Clamp(0.22f + tongue.reach * 0.006f, 0.22f, 0.55f);
            renderer.startWidth = width;
            renderer.endWidth = width * 0.42f;
            renderer.numCapVertices = 6;
            renderer.numCornerVertices = 2;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var start = YoshiCombat.Mouth(owner);
            for (int i = 0; i < Points; i++) renderer.SetPosition(i, start);
        }

        /// Shorten the lash to the victim the cast caught, so the tip is on the enemy.
        internal static void SetReach(CharacterBody owner, float reach)
        {
            foreach (var tongue in active)
                if (tongue && tongue.owner == owner)
                    tongue.reach = Mathf.Clamp(reach, 1f, YoshiCombat.TongueReach);
        }

        /// The current tip in world space; the reel keeps the caught enemy there.
        internal static bool TryTip(CharacterBody owner, out Vector3 tip)
        {
            foreach (var tongue in active)
            {
                if (!tongue || tongue.owner != owner) continue;
                float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tongue.Amount()));
                tip = YoshiCombat.Mouth(owner) + tongue.aim * (tongue.reach * amount);
                return true;
            }
            tip = default;
            return false;
        }

        float Amount()
        {
            if (age < ExtendSeconds) return age / ExtendSeconds;
            if (age < ExtendSeconds + HoldSeconds) return 1f;
            return 1f - (age - ExtendSeconds - HoldSeconds) / RetractSeconds;
        }

        void LateUpdate()
        {
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (age >= TotalSeconds) { Destroy(gameObject); return; }
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Amount()));
            float length = reach * amount;
            var origin = YoshiCombat.Mouth(owner);
            // A little gravity sag keeps it from reading as a laser; cap it so a 20 m
            // lash does not bow metres downward.
            float sag = Mathf.Min(0.04f * length, 0.25f);
            for (int i = 0; i < Points; i++)
            {
                float u = i / (Points - 1f);
                var point = origin + aim * (length * u);
                point.y -= Mathf.Sin(u * Mathf.PI) * sag;
                line.SetPosition(i, point);
            }
            line.enabled = amount > 0.01f;
        }

        void OnEnable() { active.Add(this); }
        void OnDestroy()
        {
            active.Remove(this);
            if (line) line.material = null;
            if (material) { Destroy(material); material = null; }
        }
    }

    // A swallowed enemy, inside Yoshi until he lays the egg. The reel drags the visible body
    // to the tongue tip and shrinks it to nothing; then Yoshi carries it (Melee's own
    // WaitItem / HeavyWalk1 / HeavyWalk2 clips are his heavy-item gait) at a slow walk.
    // A second Egg Lay press (or a timeout) lays the egg behind him with SpecialN2.
    internal sealed class YoshiFull : MonoBehaviour
    {
        const float ReelSeconds = 0.45f;
        const float HoldSeconds = 20f;
        /// How long Yoshi carries the swallowed enemy before laying the egg by himself.
        const float AutoLaySeconds = 3f;
        const float LayingSeconds = 0.5f;
        const uint SwallowSound = 0xdd;      // SpecialN2 frame 0: the swallow cue
        const uint LaySound = 0x4460c;       // SpecialN2 frame 25: the source's lay cue
        static readonly List<YoshiFull> live = new List<YoshiFull>();

        internal static bool IsHolding(CharacterBody victim)
        {
            foreach (var full in live) if (full && full.victim == victim) return true;
            return false;
        }
        internal static bool IsCarrying(CharacterBody body)
        {
            var full = For(body);
            return full && full.carrying;
        }
        internal static bool IsLaying(CharacterBody body)
        {
            var full = For(body);
            return full && full.laying;
        }
        internal static YoshiFull For(CharacterBody body)
            => body ? body.GetComponent<YoshiFull>() : null;

        internal static bool Begin(CharacterBody owner, CharacterBody victim)
        {
            if (!MeleeNet.Acts(owner) || !victim || For(owner) || IsHolding(victim)) return false;
            if (!EnemyHold.Hold(victim, HoldSeconds)) return false;
            var full = owner.gameObject.AddComponent<YoshiFull>();
            full.owner = owner;
            full.victim = victim;
            if (victim.modelLocator && victim.modelLocator.modelTransform) {
                full.victimModel = victim.modelLocator.modelTransform;
                full.originalScale = full.victimModel.localScale;
            }
            return true;
        }

        CharacterBody owner, victim;
        float age, carriedAt;
        bool carrying, laying, laid, pendingLay;
        float layAge;
        Vector3 layPoint;
        Transform victimModel;
        Vector3 originalScale = Vector3.one;
        float carryFrame;

        void OnEnable() { live.Add(this); }
        void OnDestroy()
        {
            live.Remove(this);
            if (victimModel) victimModel.localScale = originalScale;
            if (victim && !laid) {
                if (victimModel) GameSettings.HideRenderers(victimModel, false);
                EnemyHold.Release(victim);
            }
        }

        void FixedUpdate()
        {
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive
                || !victim || !victim.healthComponent || !victim.healthComponent.alive)
            {
                Destroy(this);
                return;
            }
            if (laying) {
                layAge += Time.fixedDeltaTime;
                // SpecialN2 frame 25 is where the source spawns the egg.
                if (pendingLay && layAge >= 25f / 60f) {
                    pendingLay = false;
                    if (YoshiEgg.Lay(owner, victim, layPoint)) {
                        laid = true;
                        owner.GetComponent<YoshiDriver>()?.PlaySourceSound(LaySound);
                    } else { Destroy(this); return; }
                }
                if (layAge >= LayingSeconds) Destroy(this);
                return;
            }
            if (carrying) {
                // Keep the belly hold alive; he lays the egg on his own after the carry.
                EnemyHold.Hold(victim, 5f);
                if (Time.time - carriedAt >= AutoLaySeconds) Release();
                return;
            }
            // Reel: the victim hangs on the tongue tip and shrinks into the mouth.
            age += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(age / ReelSeconds);
            float amount = Mathf.SmoothStep(0f, 1f, t);
            Vector3 tip;
            if (!YoshiTongue.TryTip(owner, out tip))
                tip = YoshiCombat.Mouth(owner);
            SetCore(victim, tip);
            if (victimModel) victimModel.localScale = originalScale * (1f - amount);
            if (t >= 1f) {
                carrying = true;
                carriedAt = Time.time;
                if (victimModel) GameSettings.HideRenderers(victimModel, true);
                owner.GetComponent<YoshiDriver>()?.PlaySourceSound(SwallowSound);
            }
        }

        /// Lay the egg behind Yoshi. `FixedUpdate` calls this after the carry window; the
        /// source swallow/lay clip then runs and the egg appears on its frame 25.
        internal void Release()
        {
            if (!carrying || laying || !owner || !victim) return;
            carrying = false;
            laying = true;
            pendingLay = true;
            layAge = 0f;
            if (victimModel) victimModel.localScale = originalScale;
            var behind = owner.footPosition - owner.transform.forward * (owner.radius + 1.0f);
            behind = WorldProbe.PushOutOfWalls(behind + Vector3.up * 0.5f, 0.6f, 0.5f, null, 0, 8) - Vector3.up * 0.5f;
            if (WorldProbe.Ground(behind + Vector3.up * 1.5f, 3f, out var floor))
                behind = floor.point + Vector3.up * 0.03f;
            layPoint = behind;
            SetFeet(victim, behind);
        }

        /// The carry/lay pose for the driver. The reel itself uses the sim's SpecialN2.
        internal bool TryPose(float dt, in MeleeNative.State state, out string clip, out float frame)
        {
            clip = null; frame = 0f;
            if (laying) {
                clip = "SpecialN2";
                frame = Mathf.Clamp(layAge * 60f, 0f, 29f);
                return true;
            }
            if (!carrying || state.grounded == 0) return false;
            float speed = Mathf.Sqrt(state.velocity.x * state.velocity.x + state.velocity.z * state.velocity.z);
            if (speed < 0.08f) {
                carryFrame += dt * 60f * 0.35f;
                clip = "WaitItem";
                frame = Mathf.Repeat(carryFrame, 400f);
            } else {
                carryFrame += dt * 60f * Mathf.Clamp(speed * 1.8f, 0.5f, 1.6f);
                clip = ((int)(carryFrame / 25f) & 1) == 0 ? "HeavyWalk1" : "HeavyWalk2";
                frame = Mathf.Repeat(carryFrame, 25f);
            }
            return true;
        }

        static void SetCore(CharacterBody body, Vector3 core)
        {
            if (!body) return;
            var delta = core - body.corePosition;
            if (body.characterMotor) {
                body.characterMotor.velocity = Vector3.zero;
                body.characterMotor.Motor.SetPosition(body.transform.position + delta);
            } else body.transform.position += delta;
        }
        static void SetFeet(CharacterBody body, Vector3 feet)
        {
            if (!body) return;
            if (body.characterMotor) {
                body.characterMotor.velocity = Vector3.zero;
                body.characterMotor.Motor.SetPosition(feet);
            } else body.transform.position = feet;
        }
    }

    // An enemy laid in an egg (Egg Lay). EnemyHold keeps it helpless where it stands until
    // it breaks out: a few seconds for most, longer for the large and longest for bosses.
    // It can still be hit meanwhile. The egg is sized to the victim, but not to scale: a
    // large enemy's egg tops out near 2.4 m, a boss's is bigger than that and still well
    // short of the boss itself.
    internal sealed class YoshiEgg : MonoBehaviour, IOnIncomingDamageServerReceiver
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
        float releaseAt, eggRadius, spin;
        Vector3 eggOrigin, velocity;
        MeleeFighterView view;
        Transform victimModel;

        internal static bool Lay(CharacterBody owner, CharacterBody victim, Vector3? origin = null)
        {
            if (!MeleeNet.Acts(owner) || !victim || IsTrapped(victim)) return false;
            bool boss = MeleeGrapple.IsBoss(victim);
            float seconds = boss ? BossSeconds : victim.radius >= LargeRadius ? LargeSeconds : NormalSeconds;
            if (!EnemyHold.Hold(victim, seconds, false)) return false;
            var go = new GameObject("Yoshi egg (laid)");
            var egg = go.AddComponent<YoshiEgg>();
            egg.owner = owner;
            egg.victim = victim;
            egg.eggRadius = EggRadius(victim);
            egg.releaseAt = Time.time + seconds;
            egg.eggOrigin = origin ?? EggFoot(victim);
            if (origin.HasValue) SetFeet(victim, egg.eggOrigin);
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
            go.transform.position = egg.eggOrigin;
            if (victim.healthComponent) victim.healthComponent.AddOnIncomingDamageServerReceiver(egg);
            return true;
        }

        static void SetFeet(CharacterBody body, Vector3 feet)
        {
            if (!body) return;
            if (body.characterMotor) {
                body.characterMotor.velocity = Vector3.zero;
                body.characterMotor.Motor.SetPosition(feet);
            } else body.transform.position = feet;
        }

        static Vector3 EggFoot(CharacterBody victim)
            => victim.characterMotor && victim.characterMotor.isGrounded ? victim.footPosition : victim.corePosition;

        void OnEnable() { live.Add(this); }
        void OnDestroy()
        {
            live.Remove(this);
            if (victim && victim.healthComponent) victim.healthComponent.RemoveOnIncomingDamageServerReceiver(this);
            if (victimModel) { GameSettings.HideRenderers(victimModel, false); victimModel = null; }
            view?.Dispose(); view = null;
        }

        // Hurtboxes follow the victim, which follows the egg, so damage to the enemy is
        // damage to the egg: turn it into knockback. Mass scales with the egg's size, so a
        // boss egg barely twitches while a small one can be launched.
        public void OnIncomingDamageServer(DamageInfo info)
        {
            if (!NetworkServer.active || !isActiveAndEnabled || !victim || info.rejected) return;
            var away = transform.position + Vector3.up * eggRadius - info.position;
            var direction = info.force.sqrMagnitude > 1f ? info.force.normalized
                : away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;
            float mass = Mathf.Max(0.5f, Mathf.Pow(Mathf.Max(0.3f, eggRadius), 1.5f));
            float speed = (info.force.magnitude / 900f + Mathf.Max(0f, info.damage) * 0.6f) / mass;
            speed = Mathf.Clamp(speed, 0f, 30f);
            velocity += direction * speed;
            velocity.y += Mathf.Min(speed * 0.3f, 5f);
            velocity = Vector3.ClampMagnitude(velocity, 30f);
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
            float dt = Time.fixedDeltaTime;
            velocity.y += Physics.gravity.y * dt;
            velocity.y = Mathf.Max(velocity.y, -25f);
            var from = transform.position;
            var next = from + velocity * dt;
            bool grounded = false;
            // Ground: the model's origin is the bottom of the egg.
            var probe = next + Vector3.up * (eggRadius + 0.2f);
            if (Physics.Raycast(probe, Vector3.down, out var floor, eggRadius * 2f + 0.6f,
                WorldProbe.Mask, QueryTriggerInteraction.Ignore))
            {
                float baseY = floor.point.y;
                if (next.y <= baseY + 0.02f)
                {
                    next.y = baseY;
                    if (velocity.y < 0f) velocity.y = -velocity.y * 0.25f;
                    if (Mathf.Abs(velocity.y) < 0.6f) velocity.y = 0f;
                    grounded = true;
                }
            }
            // Horizontal walls: bounce the egg and keep its centre outside the wall.
            var flat = new Vector3(next.x - from.x, 0f, next.z - from.z);
            if (flat.sqrMagnitude > 0.000001f)
            {
                var dir = flat.normalized;
                if (Physics.SphereCast(from + Vector3.up * eggRadius, eggRadius * 0.75f, dir,
                    out var wall, flat.magnitude + 0.05f, WorldProbe.Mask, QueryTriggerInteraction.Ignore))
                {
                    var normal = new Vector3(wall.normal.x, 0f, wall.normal.z);
                    if (normal.sqrMagnitude > 0.01f)
                    {
                        normal.Normalize();
                        var horizontal = new Vector3(velocity.x, 0f, velocity.z);
                        horizontal -= 2f * Vector3.Dot(horizontal, normal) * normal;
                        horizontal *= 0.35f;
                        velocity.x = horizontal.x;
                        velocity.z = horizontal.z;
                        next.x = wall.point.x + normal.x * eggRadius * 0.8f;
                        next.z = wall.point.z + normal.z * eggRadius * 0.8f;
                    }
                }
            }
            float drag = grounded ? Mathf.Exp(-5f * dt) : Mathf.Exp(-0.2f * dt);
            velocity.x *= drag;
            velocity.z *= drag;
            if (grounded && new Vector2(velocity.x, velocity.z).sqrMagnitude < 0.04f) { velocity.x = 0f; velocity.z = 0f; }
            transform.position = next;
            spin = Mathf.Repeat(spin + new Vector2(velocity.x, velocity.z).magnitude / Mathf.Max(0.25f, eggRadius) * Mathf.Rad2Deg * dt, 360f);
            SetFeet(victim, next);
        }

        void BreakOut()
        {
            try {
                var effect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniExplosionVFX");
                if (effect && victim) EffectManager.SpawnEffect(effect, new EffectData { origin = transform.position, scale = eggRadius }, NetworkServer.active);
            } catch (Exception) { }
            owner?.GetComponent<YoshiDriver>()?.PlaySourceSound(0x44618); // egg break
            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (view == null) return;
            // The egg sways more as it is about to hatch; movement tilts and spins it.
            float left = Mathf.Max(0f, releaseAt - Time.time);
            float shake = left < 1.5f ? Mathf.Sin(Time.time * 40f) * 0.06f * (1.5f - left) : 0f;
            var rotation = Quaternion.Euler(velocity.z * 1.5f, spin, -velocity.x * 1.5f + shake * 57f);
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
            var driver = owner ? owner.GetComponent<YoshiDriver>() : null;
            driver?.PlaySourceSound(0x44618); // itYoshieggthrow_UnkMotion2: egg break
            var velocity = new Vector3(state.velocity.x, state.velocity.y, state.velocity.z);
            driver?.PlayEffect(1231, at, velocity.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
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
