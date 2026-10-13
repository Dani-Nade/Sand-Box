using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Lasso: hold the lasso button aimed at any surface to swing from it (pendulum physics with a slow
    /// reel-in). Release to fling off with a speed bonus, or press jump mid-swing for a lasso jump.
    /// Lasso a loose physics object and it gets yanked towards you instead.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Lasso")]
        [SerializeField] bool enableLasso = true;
        [SerializeField] float lassoRange = 35f;
        [SerializeField] float lassoMinLength = 2f;
        [Tooltip("Rope shortens by this many metres per second while swinging.")]
        [SerializeField] float lassoReelSpeed = 3f;
        [Tooltip("Constant pull towards the anchor (m/s²).")]
        [SerializeField] float lassoPullAcceleration = 10f;
        [Tooltip("Instant speed towards the anchor when the rope catches.")]
        [SerializeField] float lassoYankSpeed = 6f;
        [Tooltip("How much WASD steers you mid-swing (m/s²).")]
        [SerializeField] float lassoSwingControl = 7f;
        [Tooltip("Speed multiplier when you let go.")]
        [SerializeField] float lassoReleaseBoost = 1.1f;
        [SerializeField] float lassoJumpUpSpeed = 6f;
        [SerializeField] float lassoMaxSwingSpeed = 38f;
        [SerializeField] float lassoCooldown = 0.35f;
        [Tooltip("Speed loose objects are yanked towards you.")]
        [SerializeField] float lassoObjectPullSpeed = 14f;

        [Header("Lasso Visuals")]
        [SerializeField] LineRenderer lassoRope;
        [Tooltip("Where the rope leaves your hand (usually a point on the camera, bottom-left).")]
        [SerializeField] Transform lassoHand;

        const float RopeFlashTime = 0.15f;

        Transform anchorBody;
        Vector3 anchorLocal;
        float ropeLength;
        Vector3 swingVelocity;
        float lastLassoTime = float.NegativeInfinity;
        Vector3 ropeFlashPoint;
        float ropeFlashUntil;

        public bool IsSwinging { get; private set; }
        public event Action LassoAttached;
        public event Action LassoReleased;

        Vector3 AnchorPoint => anchorBody != null ? anchorBody.TransformPoint(anchorLocal) : transform.position;
        Vector3 GripPoint => transform.position + Vector3.up * (controller.height * 0.6f);

        bool TryLasso()
        {
            if (!enableLasso || !input.LassoPressed || IsSwinging || !PlayerLook.CursorLocked) return false;
            if (Time.time - lastLassoTime < lassoCooldown) return false;

            Transform aim = Aim;
            if (!Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, lassoRange, obstructionMask, QueryTriggerInteraction.Ignore))
                return false;

            lastLassoTime = Time.time;

            // Loose objects get yanked to you.
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                Vector3 toPlayer = (GripPoint - hit.point).normalized;
                hit.rigidbody.AddForce(toPlayer * lassoObjectPullSpeed + Vector3.up * 3f, ForceMode.VelocityChange);
                ropeFlashPoint = hit.point;
                ropeFlashUntil = Time.time + RopeFlashTime;
                return false;
            }

            if (IsDashing) EndDash();
            anchorBody = hit.transform;
            anchorLocal = hit.transform.InverseTransformPoint(hit.point);
            ropeLength = Mathf.Max(lassoMinLength, Vector3.Distance(GripPoint, hit.point));

            swingVelocity = Velocity + (hit.point - GripPoint).normalized * lassoYankSpeed;
            IsSwinging = true;
            IsGrounded = false;
            IsSliding = false;
            IsSlamming = false;
            IsDiving = false; // Dead Eye slow-mo keeps running if it was on
            rollTimer = 0f;
            LassoAttached?.Invoke();
            return true;
        }

        void UpdateLasso(float dt)
        {
            if (anchorBody == null || !input.LassoHeld)
            {
                ReleaseLasso(jump: false);
                return;
            }
            if (input.JumpPressed)
            {
                ReleaseLasso(jump: true);
                return;
            }

            Vector3 anchor = AnchorPoint;
            Vector3 dir = (anchor - GripPoint).normalized;

            swingVelocity += Vector3.down * (gravity * dt);
            swingVelocity += dir * (lassoPullAcceleration * dt);

            Vector2 move = input.Move;
            swingVelocity += (transform.right * move.x + transform.forward * move.y) * (lassoSwingControl * dt);
            swingVelocity = Vector3.ClampMagnitude(swingVelocity, lassoMaxSwingSpeed);

            ropeLength = Mathf.Max(lassoMinLength, ropeLength - lassoReelSpeed * dt);

            CollisionFlags flags = controller.Move(swingVelocity * dt);
            if ((flags & CollisionFlags.Sides) != 0) swingVelocity *= 0.9f;
            if ((flags & CollisionFlags.Below) != 0 && swingVelocity.y < 0f) swingVelocity.y = 0f;
            if ((flags & CollisionFlags.Above) != 0 && swingVelocity.y > 0f) swingVelocity.y = 0f;

            // Rope constraint: never further than the rope length; remove speed going outwards (pendulum).
            Vector3 toAnchor = anchor - GripPoint;
            float distance = toAnchor.magnitude;
            if (distance > ropeLength)
            {
                dir = toAnchor / distance;
                controller.Move(dir * (distance - ropeLength));
                float outward = Vector3.Dot(swingVelocity, dir);
                if (outward < 0f) swingVelocity -= dir * outward;
            }

            // Mirror into the normal velocity so the HUD, camera and weapon read it correctly.
            horizontalVelocity = new Vector3(swingVelocity.x, 0f, swingVelocity.z);
            verticalVelocity = swingVelocity.y;
        }

        void ReleaseLasso(bool jump)
        {
            Vector3 v = swingVelocity * lassoReleaseBoost;
            horizontalVelocity = Vector3.ClampMagnitude(new Vector3(v.x, 0f, v.z), maxAirSpeed);
            verticalVelocity = jump ? Mathf.Max(v.y, 0f) + lassoJumpUpSpeed : v.y;
            isJumping = false;
            wallBouncesUsed = 0; // reward chaining: fresh wall bounces after a swing
            DetachLasso();
        }

        void DetachLasso()
        {
            IsSwinging = false;
            anchorBody = null;
            lastLassoTime = Time.time;
            LassoReleased?.Invoke();
        }

        void LateUpdate()
        {
            if (lassoRope == null) return;

            bool flashing = Time.time < ropeFlashUntil;
            bool show = IsSwinging || flashing;
            lassoRope.enabled = show;
            if (!show) return;

            Vector3 from = lassoHand != null ? lassoHand.position : GripPoint;
            lassoRope.positionCount = 2;
            lassoRope.SetPosition(0, from);
            lassoRope.SetPosition(1, IsSwinging ? AnchorPoint : ropeFlashPoint);
        }
    }
}
