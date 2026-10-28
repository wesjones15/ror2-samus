using System;
using System.Collections.Generic;
using RoR2;
using Ror2Common;
using UnityEngine;

namespace Link
{
    // Link's hookshot, carried by his grab button. The source launches an
    // It_Kind_Link_HShot from FtPart_RThumbNb during Catch/AirCatch
    // (ftCo_0D8E.c::fn_800D8EC8, ftCo_AirCatch.c::ftCo_AirCatch_Anim, and
    // itlinkhookshot.c::it_802A2BA4). The held launcher is public article 4;
    // the chain links and hook head are the item's source joints x54/x58/x5C
    // (itlinkhookshot.c::it_802A2568). Here the host owns the chain because the
    // reach is world-scale: 15 m toward Link's facing, not Melee's screen-scale
    // tether. A wall or a boss/big body is an anchor that reels Link in and
    // restores his jumps; an ordinary enemy is pulled to him and enters the
    // shared capture/pummel/throw flow.
    internal sealed class LinkHookshot : IDisposable
    {
        // Host adaptation for open worlds: the source tether is a few character
        // lengths; 15 m sits in the requested 10-20 m band without crossing a
        // whole RoR2 stage.
        internal const float Range = 15f;
        const float ExtendSpeed = 55f;      // metres/s, about 0.27 s to full reach
        const float RetractSpeed = 70f;
        internal const float PullSpeed = 26f; // reel-in speed of Link or a caught enemy
        const float ClimbSpeed = 4.5f;      // metres/s of stick-driven climb above the anchor
        const float ClimbLimit = 4f;        // how far above the anchor the chain lets him climb
        const float ChainSpacingUnits = 1.9f;   // source link length is 1.8125 units
        const float LauncherLengthUnits = 5.6f;  // held item model ends at 5.375 units
        const float AnchorBodyRadius = 1.8f;     // bosses and bodies this big anchor Link

        enum State { Idle, Flying, Returning, Wall, Body, Enemy }

        readonly LinkDriver driver;
        readonly CharacterBody owner;
        State state;
        Vector3 origin, direction, anchor, localAnchor;
        HealthComponent anchorBody;
        float extension, climb;
        bool jumpsRestored;
        // The held launcher (article 4) and the source chain parts x54/x58/x5C.
        MeleeFighterView held, linkA, linkB, hook;
        readonly List<ViewDraw> drawsA = new List<ViewDraw>();
        readonly List<ViewDraw> drawsB = new List<ViewDraw>();

        internal LinkHookshot(LinkDriver driver, CharacterBody owner)
        {
            this.driver = driver;
            this.owner = owner;
        }

        internal bool Active => state != State.Idle;
        internal bool Anchored => state == State.Wall || state == State.Body;
        internal bool Holding => Active;
        internal bool PullingEnemy => state == State.Enemy && driver.CapturedTarget != null;

        /// Bosses and "big" bodies are anchors, not captures. isBoss/isChampion is
        /// RoR2's own classification; the radius catches large non-boss bodies.
        internal static bool IsAnchorBody(CharacterBody body) =>
            body && (MeleeGrapple.IsBoss(body) || body.radius >= AnchorBodyRadius);

        internal void Fire(Vector3 facing)
        {
            if (Active || driver.CapturedTarget != null) return;
            direction = new Vector3(facing.x, 0f, facing.z);
            if (direction.sqrMagnitude < 0.0001f)
                direction = owner ? new Vector3(owner.transform.forward.x, 0f, owner.transform.forward.z) : Vector3.forward;
            direction.Normalize();
            origin = driver.RightHandWorld();
            extension = 0f;
            climb = 0f;
            jumpsRestored = false;
            anchorBody = null;
            state = State.Flying;
        }

        internal void Release()
        {
            state = State.Idle;
            anchorBody = null;
            extension = 0f;
            climb = 0f;
            Hide();
        }

        void Hide()
        {
            held?.Models?.HideAll();
            linkA?.Models?.HideAll();
            linkB?.Models?.HideAll();
            hook?.Models?.HideAll();
        }

        /// The enemy reached Link; keep the capture (the shared flow holds it) but
        /// put the chain away.
        internal void EnemyArrived()
        {
            if (state == State.Enemy) Release();
        }

        /// Called after the sim step, before the host reads the new state, so an
        /// anchor position is already in the sim for collision, camera and drawing.
        internal void AfterStep(float dt)
        {
            if (!driver.IsOwner || state == State.Idle) return;
            // The disc's close grab boxes caught someone first; let the shared capture
            // own the move instead of adding an anchor to the same action.
            if (state == State.Flying && driver.CapturedTarget != null) { Release(); return; }
            if (!driver.ControlsInterrupted && driver.HealthAlive)
            {
                switch (state)
                {
                    case State.Flying: StepFlight(dt); break;
                    case State.Returning: StepReturn(dt); break;
                    case State.Wall:
                    case State.Body: StepAnchor(dt); break;
                    case State.Enemy:
                        if (driver.CapturedTarget == null) Release();
                        break;
                }
            }
            else Release();
        }

        void StepFlight(float dt)
        {
            float reach = Mathf.Min(Range, extension + ExtendSpeed * dt);
            if (MeleeGrapple.Trace(owner, origin, direction, reach, out var hit, out var hurt))
            {
                extension = Mathf.Max(0f, Vector3.Distance(origin, hit.point) - 0.05f);
                var target = hurt && hurt.healthComponent ? hurt.healthComponent : null;
                var body = target ? target.body : null;
                if (body && !body.isPlayerControlled && IsAnchorBody(body))
                {
                    AttachBody(target, hit.point);
                    return;
                }
                if (target && !body.isPlayerControlled && driver.TryCaptureTarget(target))
                {
                    anchor = hit.point;
                    state = State.Enemy;
                    return;
                }
                if (!hurt || !hurt.healthComponent || !body || !body.isPlayerControlled)
                {
                    AttachWall(hit.point);
                    return;
                }
            }
            extension = reach;
            if (extension >= Range) state = State.Returning;
        }

        void StepReturn(float dt)
        {
            extension = Mathf.Max(0f, extension - RetractSpeed * dt);
            if (extension <= 0f) Release();
        }

        void AttachWall(Vector3 point)
        {
            anchor = point;
            state = State.Wall;
            climb = 0f;
            RestoreJumps();
        }

        void AttachBody(HealthComponent target, Vector3 point)
        {
            anchorBody = target;
            localAnchor = target.body.transform.InverseTransformPoint(point);
            anchor = point;
            state = State.Body;
            climb = 0f;
            RestoreJumps();
        }

        void RestoreJumps()
        {
            if (jumpsRestored) return;
            jumpsRestored = true;
            driver.ResetJumps();
        }

        void StepAnchor(float dt)
        {
            if (state == State.Body)
            {
                if (!anchorBody || !anchorBody.alive || !anchorBody.body)
                {
                    Release();
                    return;
                }
                anchor = anchorBody.body.transform.TransformPoint(localAnchor);
            }
            var core = driver.SimCore;
            var currentFeet = driver.SimFeet;
            if ((anchor - core).sqrMagnitude > (Range + 5f) * (Range + 5f))
            {
                Release();
                return;
            }
            // Hold the stick up/down to climb above or below the hook point. The
            // source tether does not climb; this is the requested host extension.
            float axis = driver.ClimbAxis;
            if (Mathf.Abs(axis) > 0.2f)
                climb = Mathf.Clamp(climb + axis * ClimbSpeed * dt, -2f, ClimbLimit);
            var hang = anchor + Vector3.up * climb;
            var delta = hang - core;
            float standOff = owner.radius + 0.25f;
            var destination = hang - (delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.up) * standOff;
            var feet = Vector3.MoveTowards(currentFeet, destination - (core - currentFeet), PullSpeed * dt);
            driver.AnchorHookshot(feet);
            if (driver.Grounded && climb <= 0.01f && Vector3.Distance(currentFeet, feet) < 0.05f)
                Release();
        }

        internal Vector3 Tip()
        {
            switch (state)
            {
                case State.Wall:
                case State.Body: return anchor;
                case State.Enemy:
                    var target = driver.CapturedTarget;
                    return target ? target.body.corePosition : anchor;
                case State.Flying:
                case State.Returning:
                    return origin + direction * extension;
                default: return origin;
            }
        }

        internal void Draw()
        {
            if (!Active || driver.Units <= 0f)
            {
                Hide();
                return;
            }
            if (held == null)
            {
                held = new MeleeFighterView(Samus.Plugin.AssetsDir, MeleeNative.FighterLink, 4, 0);
                if (MeleeNative.HasHookshotParts)
                {
                    linkA = MeleeFighterView.Hookshot(Samus.Plugin.AssetsDir, 0);
                    linkB = MeleeFighterView.Hookshot(Samus.Plugin.AssetsDir, 1);
                    hook = MeleeFighterView.Hookshot(Samus.Plugin.AssetsDir, 2);
                }
            }
            var start = driver.RightHandWorld();
            var end = Tip();
            var delta = end - start;
            float length = delta.magnitude;
            if (length < 0.02f) { Hide(); return; }
            var direction = delta / length;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            // The source held item is the blue launcher in Link's hand; its +Z
            // points down the chain.
            held.Models.Update(driver.Units, d => (start, rotation));
            if (linkA == null) return;
            var chainStart = start + direction * (LauncherLengthUnits * driver.Units);
            float chainLength = Vector3.Distance(chainStart, end);
            int count = Mathf.Clamp(Mathf.CeilToInt(chainLength / (ChainSpacingUnits * driver.Units)), 1, 128);
            drawsA.Clear(); drawsB.Clear();
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                var p = Vector3.Lerp(chainStart, end, t);
                var draw = new ViewDraw {
                    key = (ulong)i + 1,
                    pos = new ViewVec3(p.x / driver.Units, p.y / driver.Units, p.z / driver.Units),
                    rot_x = rotation.x, rot_y = rotation.y, rot_z = rotation.z, rot_w = rotation.w,
                    alpha = 1f,
                };
                if ((i & 1) == 0) drawsA.Add(draw); else drawsB.Add(draw);
            }
            if (drawsA.Count > 0) { linkA.InstanceDraws = drawsA.ToArray(); linkA.Models.Update(driver.Units); }
            else linkA.Models.HideAll();
            if (drawsB.Count > 0) { linkB.InstanceDraws = drawsB.ToArray(); linkB.Models.Update(driver.Units); }
            else linkB.Models.HideAll();
            // The hook head's claw is local +Z: point it away from Link.
            var hookRotation = Quaternion.LookRotation(direction, Vector3.up);
            hook.Models.Update(driver.Units, d => (end, hookRotation));
        }

        public void Dispose()
        {
            Release();
            held?.Dispose(); held = null;
            linkA?.Dispose(); linkA = null;
            linkB?.Dispose(); linkB = null;
            hook?.Dispose(); hook = null;
        }
    }
}
