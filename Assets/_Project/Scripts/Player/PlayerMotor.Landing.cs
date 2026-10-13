using System;
using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Hard landings: big drops make you stumble (lose most of your speed for a moment) and
    /// really big drops hurt. Press crouch just before a hard landing to roll out of it instead:
    /// no stumble, momentum kept, less damage.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Hard Landing")]
        [SerializeField] bool enableHardLandings = true;
        [Tooltip("Landing faster than this (m/s) is a hard landing. 13 m/s ≈ a 3.4 m drop.")]
        [SerializeField] float hardLandingSpeed = 13f;
        [Tooltip("Fraction of horizontal speed kept after a hard landing.")]
        [SerializeField, Range(0f, 1f)] float stumbleSpeedKeep = 0.35f;
        [SerializeField] float stumbleDuration = 0.4f;
        [Tooltip("Movement speed multiplier while stumbling.")]
        [SerializeField, Range(0f, 1f)] float stumbleMoveMultiplier = 0.45f;

        [Header("Fall Damage")]
        [Tooltip("Landing faster than this (m/s) hurts. 19 m/s ≈ a 7 m drop.")]
        [SerializeField] float fallDamageSpeed = 19f;
        [SerializeField] float fallDamagePerSpeed = 5f;

        [Header("Landing Roll")]
        [SerializeField] bool enableLandingRoll = true;
        [Tooltip("Crouch pressed this long before a hard landing triggers a roll.")]
        [SerializeField] float rollInputWindow = 0.25f;
        [SerializeField] float rollDuration = 0.5f;
        [Tooltip("Rolling always carries you at least this fast.")]
        [SerializeField] float rollMinSpeed = 6f;
        [SerializeField] float rollFriction = 3f;
        [SerializeField, Range(0f, 1f)] float rollDamageMultiplier = 0.4f;

        float rollTimer, stumbleTimer;
        Vector3 rollDirection;
        float lastAirCrouchPressTime = float.NegativeInfinity;

        public bool IsRolling => rollTimer > 0f;
        public bool IsStumbling => stumbleTimer > 0f;
        public float RollDuration => rollDuration;
        float StumbleMultiplier => IsStumbling ? stumbleMoveMultiplier : 1f;

        public event Action<float> HardLanded; // impact speed
        public event Action Rolled;

        void HandleLanding(float impact)
        {
            if (IsSlamming)
            {
                FinishSlam();
                return;
            }

            if (IsDiving)
            {
                // Dead Eye dives always land in a roll.
                EndDive();
                StartRoll();
                ApplyFallDamage(impact, rollDamageMultiplier);
                return;
            }

            if (!enableHardLandings || impact < hardLandingSpeed) return;

            bool rollInput = Time.time - lastAirCrouchPressTime <= rollInputWindow;
            if (enableLandingRoll && rollInput)
            {
                StartRoll();
                ApplyFallDamage(impact, rollDamageMultiplier);
            }
            else
            {
                horizontalVelocity *= stumbleSpeedKeep;
                stumbleTimer = stumbleDuration;
                IsSliding = false;
                ApplyFallDamage(impact, 1f);
                HardLanded?.Invoke(impact);
            }
        }

        void StartRoll()
        {
            rollDirection = horizontalVelocity.sqrMagnitude > 1f ? horizontalVelocity.normalized : FlatForward;
            horizontalVelocity = rollDirection * Mathf.Max(HorizontalSpeed, rollMinSpeed);
            rollTimer = rollDuration;
            stumbleTimer = 0f;
            IsSliding = false;
            IsCrouching = true;
            Rolled?.Invoke();
        }

        void UpdateRollVelocity(float dt)
        {
            float speed = Mathf.Max(0f, HorizontalSpeed - rollFriction * dt);
            horizontalVelocity = rollDirection * speed;
        }

        void UpdateLandingTimers(float dt)
        {
            if (rollTimer > 0f) rollTimer -= dt;
            if (stumbleTimer > 0f) stumbleTimer -= dt;
        }

        void ApplyFallDamage(float impact, float multiplier)
        {
            if (selfHealth == null || impact <= fallDamageSpeed) return;
            float amount = (impact - fallDamageSpeed) * fallDamagePerSpeed * multiplier;
            if (amount < 1f) return;
            selfHealth.TakeDamage(new DamageInfo
            {
                Amount = amount,
                Point = transform.position,
                Direction = Vector3.down,
                Source = gameObject,
            });
        }
    }
}
