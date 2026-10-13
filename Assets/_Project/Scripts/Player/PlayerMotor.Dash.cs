using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Dashing (replaces sprinting): a short, fast burst in your input direction (forward if none),
    /// on the ground or in the air. Uses charges that refill over time.
    ///  - Air dashes hold your height for the duration of the dash.
    ///  - After a dash you keep some of the speed and it bleeds off naturally.
    ///  - Dash + jump = dash jump (keeps more speed). Dash + crouch on the ground = dash slide.
    ///  - Dashing re-arms the slide (see PlayerMotor.Slide.cs).
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Dash")]
        [SerializeField] bool enableDash = true;
        [SerializeField, Min(1)] int maxDashCharges = 3;
        [Tooltip("Seconds to refill one dash charge.")]
        [SerializeField] float dashRechargeTime = 1.2f;
        [Tooltip("Dash distance = speed x duration (30 x 0.2 = 6 m).")]
        [SerializeField] float dashSpeed = 30f;
        [SerializeField] float dashDuration = 0.2f;
        [Tooltip("Speed you come out of a dash with (if you were going slower).")]
        [SerializeField] float dashExitSpeed = 14f;
        [Tooltip("Speed you keep when jumping out of a dash.")]
        [SerializeField] float dashJumpSpeed = 17f;
        [Tooltip("Minimum seconds between dashes, so all charges can't fire on the same frame.")]
        [SerializeField] float dashCooldown = 0.2f;

        float dashCharge = 3f;
        float dashTimer;
        float lastDashTime = float.NegativeInfinity;
        Vector3 dashDirection;
        float dashEntrySpeed;

        public bool IsDashing => dashTimer > 0f;
        public int MaxDashCharges => maxDashCharges;
        /// <summary>Charges as a float (2.5 = two full charges and one half-refilled).</summary>
        public float DashCharge => dashCharge;
        public event Action Dashed;

        void RechargeDashes(float dt)
        {
            if (dashCharge < maxDashCharges)
                dashCharge = Mathf.Min(maxDashCharges, dashCharge + dt / dashRechargeTime);
        }

        void TryDash()
        {
            if (!enableDash || !input.DashPressed || IsDashing) return;
            if (dashCharge < 1f || Time.time - lastDashTime < dashCooldown) return;

            Vector2 move = input.Move;
            Vector3 dir = transform.right * move.x + transform.forward * move.y;
            if (dir.sqrMagnitude < 0.01f) dir = FlatForward;
            dir.y = 0f;
            dir.Normalize();

            // A dash interrupts most other states.
            if (IsSliding) IsSliding = IsMantleSliding = false;
            if (IsDiving) IsDiving = false; // Dead Eye slow-mo keeps running out its timer
            IsSlamming = false;
            rollTimer = 0f;
            stumbleTimer = 0f;
            isJumping = false;

            dashCharge -= 1f;
            lastDashTime = Time.time;
            dashTimer = dashDuration;
            dashDirection = dir;
            dashEntrySpeed = HorizontalSpeed;
            if (!IsGrounded) verticalVelocity = 0f;

            // Dashing always lets you slide again.
            slideArmed = true;
            rearmTimer = 0f;

            Dashed?.Invoke();
        }

        void UpdateDashVelocity(float dt)
        {
            horizontalVelocity = dashDirection * dashSpeed;
            dashTimer -= dt;
            if (dashTimer <= 0f) EndDash();
        }

        /// <summary>Ends the dash, leaving you with exit speed (or what you had before, if faster).</summary>
        void EndDash(float exitSpeed = -1f)
        {
            if (exitSpeed < 0f) exitSpeed = dashExitSpeed;
            float speed = Mathf.Max(exitSpeed, dashEntrySpeed);
            horizontalVelocity = dashDirection * speed;
            dashTimer = 0f;

            // Don't let the dash's top speed leak into mantle-slide speed.
            speedBeforeWallHit = Mathf.Min(speedBeforeWallHit, speed);
        }
    }
}
