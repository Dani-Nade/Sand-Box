using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Sliding: press crouch while running (or dashing) to slide with a speed boost. Keeps momentum,
    /// speeds up downhill, slight steering. Jumping out of a slide keeps the speed.
    ///
    /// Rules:
    ///  - Anything that disrupts the slide cancels it: releasing crouch, pressing back, shooting
    ///    (reloading is allowed).
    ///  - After a slide ends you can't crouch straight back into another one - you have to
    ///    run standing up for a moment first, or dash (no slide spamming / slide hopping).
    ///  - Mantle slides (see PlayerMotor.Mantle.cs) skip both rules and get a bigger boost.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Slide")]
        [SerializeField] bool enableSlide = true;
        [Tooltip("Minimum speed to start a slide (run speed is 10).")]
        [SerializeField] float slideMinStartSpeed = 8.5f;
        [SerializeField] float slideBoost = 4f;
        [Tooltip("The boost can't push you above this speed.")]
        [SerializeField] float slideMaxBoostedSpeed = 18f;
        [Tooltip("Seconds before another slide gives a boost again.")]
        [SerializeField] float slideBoostCooldown = 1.2f;
        [SerializeField] float slideFriction = 7f;
        [Tooltip("Slide ends when you slow down below this.")]
        [SerializeField] float slideEndSpeed = 6f;
        [Tooltip("How strongly slopes speed you up (downhill) or slow you down (uphill). 1 = gravity.")]
        [SerializeField] float slideSlopeAcceleration = 1.5f;
        [SerializeField] float slideMaxSpeed = 30f;
        [Tooltip("Steering while sliding, in radians per second.")]
        [SerializeField] float slideSteering = 1.5f;

        [Header("Slide Rules")]
        [Tooltip("After a slide, you must run standing up for this long (or dash) before you can slide again.")]
        [SerializeField] float slideRearmRunTime = 0.25f;
        [SerializeField] bool cancelSlideOnShoot = true;
        [SerializeField] bool cancelSlideOnBackInput = true;

        const float SlideAirGrace = 0.15f; // brief bumps don't end a slide

        float lastSlideBoostTime = float.NegativeInfinity;
        bool slideArmed = true;
        float rearmTimer;

        public bool IsSliding { get; private set; }
        public bool IsMantleSliding { get; private set; }
        public event Action SlideStarted;

        void UpdateSlideState()
        {
            if (!enableSlide) return;

            if (IsSliding)
            {
                if (ShouldCancelSlide()) EndSlide();
                return;
            }

            if (TryLateMantleSlide()) return;
            if (IsRolling || IsSlamming || IsDiving) return;

            // Re-arm by running standing up (dashing re-arms instantly, see PlayerMotor.Dash.cs).
            if (!slideArmed)
            {
                if (IsGrounded && !IsCrouching && HorizontalSpeed >= runSpeed * 0.8f)
                {
                    rearmTimer += Time.deltaTime;
                    if (rearmTimer >= slideRearmRunTime) slideArmed = true;
                }
                else rearmTimer = 0f;
                return;
            }

            if (!IsGrounded || HorizontalSpeed < slideMinStartSpeed) return;

            // Press crouch while fast, or land with crouch held.
            if (input.CrouchPressed || (justLanded && input.CrouchHeld))
            {
                if (IsDashing) EndDash(); // dash slide starts from the dash's exit speed, not its peak
                StartSlide(horizontalVelocity, HorizontalSpeed, slideBoost, slideMaxBoostedSpeed, useBoostCooldown: true);
            }
        }

        bool ShouldCancelSlide()
        {
            if (!IsGrounded && Time.time - lastGroundedTime > SlideAirGrace) return true;
            if (HorizontalSpeed < slideEndSpeed) return true;
            if (input.CrouchReleased) return true;
            if (cancelSlideOnBackInput && input.Move.y < -0.5f) return true;
            if (cancelSlideOnShoot && input.AttackPressed && PlayerLook.CursorLocked) return true;
            return false;
        }

        void StartSlide(Vector3 direction, float speed, float boost, float maxBoostedSpeed, bool useBoostCooldown)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            if (!useBoostCooldown || Time.time - lastSlideBoostTime >= slideBoostCooldown)
            {
                speed = Mathf.Max(speed, Mathf.Min(speed + boost, maxBoostedSpeed));
                if (useBoostCooldown) lastSlideBoostTime = Time.time;
            }

            horizontalVelocity = direction.normalized * speed;
            IsSliding = true;
            IsMantleSliding = false;
            IsCrouching = true;
            SlideStarted?.Invoke();
        }

        void EndSlide()
        {
            IsSliding = false;
            IsMantleSliding = false;
            slideArmed = false;
            rearmTimer = 0f;
        }

        void UpdateSlideVelocity(float dt)
        {
            float speed = horizontalVelocity.magnitude;
            Vector3 dir = speed > 0.001f ? horizontalVelocity / speed : transform.forward;

            // Gravity along the slope: downhill speeds you up, uphill slows you down.
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal); // length = sin(slope angle)
            Vector3 velocity = dir * speed + new Vector3(downhill.x, 0f, downhill.z) * (gravity * slideSlopeAcceleration * dt);
            speed = velocity.magnitude;
            if (speed > 0.001f) dir = velocity / speed;

            speed = Mathf.Max(0f, speed - slideFriction * dt);

            Vector2 move = input.Move;
            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 0.01f)
                dir = Vector3.RotateTowards(dir, wish.normalized, slideSteering * dt, 0f);

            horizontalVelocity = dir * Mathf.Min(speed, slideMaxSpeed);
        }
    }
}
