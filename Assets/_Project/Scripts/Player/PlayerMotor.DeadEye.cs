using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Dead Eye Dive: a Max Payne style dive in your movement direction with slow motion.
    /// Slow-mo lasts until you land (you roll out of it) or a time limit, then recharges.
    /// Weapons fire faster while Dead Eye is active (see WeaponData.deadEyeFireIntervalMultiplier).
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Dead Eye Dive")]
        [SerializeField] bool enableDeadEye = true;
        [SerializeField] float diveSpeed = 13f;
        [SerializeField] float diveUpSpeed = 4.5f;
        [SerializeField, Range(0.05f, 1f)] float deadEyeTimeScale = 0.3f;
        [Tooltip("Longest the slow-mo can last, in real seconds.")]
        [SerializeField] float deadEyeMaxDuration = 1.6f;
        [Tooltip("Recharge time in real seconds.")]
        [SerializeField] float deadEyeCooldown = 6f;

        bool deadEyeActive;
        float deadEyeEndRealTime;
        float deadEyeReadyRealTime;
        float defaultFixedDeltaTime = -1f;

        public bool IsDiving { get; private set; }
        public bool IsDeadEye => deadEyeActive;
        /// <summary>0 = just used, 1 = ready.</summary>
        public float DeadEyeCharge01 =>
            deadEyeActive ? 0f : 1f - Mathf.Clamp01((deadEyeReadyRealTime - Time.unscaledTime) / deadEyeCooldown);

        public event Action DeadEyeStarted;
        public event Action DeadEyeEnded;

        void TryDeadEyeDive()
        {
            if (!enableDeadEye || !input.DeadEyePressed || IsDiving || deadEyeActive) return;
            if (Time.unscaledTime < deadEyeReadyRealTime) return;

            Vector2 move = input.Move;
            Vector3 dir = transform.right * move.x + transform.forward * move.y;
            if (dir.sqrMagnitude < 0.01f) dir = FlatForward;
            dir.Normalize();

            if (IsDashing) EndDash();
            horizontalVelocity = dir * Mathf.Max(diveSpeed, Vector3.Dot(horizontalVelocity, dir));
            verticalVelocity = Mathf.Max(verticalVelocity, diveUpSpeed);
            lastGroundedTime = float.NegativeInfinity; // no coyote jump out of a dive
            isJumping = false;
            IsDiving = true;
            IsSliding = false;
            rollTimer = 0f;
            StartDeadEye();
        }

        void StartDeadEye()
        {
            if (defaultFixedDeltaTime < 0f) defaultFixedDeltaTime = Time.fixedDeltaTime;
            deadEyeActive = true;
            Time.timeScale = deadEyeTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * deadEyeTimeScale;
            deadEyeEndRealTime = Time.unscaledTime + deadEyeMaxDuration;
            DeadEyeStarted?.Invoke();
        }

        /// <summary>Pushes the real-time Dead Eye timers back (used while the game is paused).</summary>
        void HoldDeadEyeTimers(float realSeconds)
        {
            deadEyeEndRealTime += realSeconds;
            deadEyeReadyRealTime += realSeconds;
        }

        void UpdateDeadEyeTimer()
        {
            if (deadEyeActive && Time.unscaledTime >= deadEyeEndRealTime)
                StopDeadEye();
        }

        void StopDeadEye()
        {
            if (!deadEyeActive) return;
            deadEyeActive = false;
            Time.timeScale = 1f;
            if (defaultFixedDeltaTime > 0f) Time.fixedDeltaTime = defaultFixedDeltaTime;
            deadEyeReadyRealTime = Time.unscaledTime + deadEyeCooldown;
            DeadEyeEnded?.Invoke();
        }

        void EndDive()
        {
            IsDiving = false;
            StopDeadEye();
        }
    }
}
