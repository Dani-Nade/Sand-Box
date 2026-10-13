using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Bunny hopping: jump within a short window of landing (just before or just after) to keep
    /// all your speed - ground friction is skipped during that window. Each chained hop adds a
    /// little speed (up to a cap) and bends your direction towards where you're steering.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Bunny Hop")]
        [SerializeField] bool enableBunnyHop = true;
        [Tooltip("A jump within this long after landing counts as a hop (no friction during this window).")]
        [SerializeField] float bhopWindow = 0.12f;
        [Tooltip("Speed added on each chained hop.")]
        [SerializeField] float bhopSpeedGain = 0.6f;
        [Tooltip("Hops can't push you above this speed (you keep higher speeds, they just stop growing).")]
        [SerializeField] float bhopMaxSpeed = 20f;
        [Tooltip("How much each hop bends your direction towards your input (0 = none, 1 = snap).")]
        [SerializeField, Range(0f, 1f)] float bhopRedirect = 0.35f;
        [Tooltip("Hold jump to hop automatically on every landing.")]
        [SerializeField] bool autoBunnyHop = false;

        int bhopChain;
        float landedTime = float.NegativeInfinity;

        public int BunnyHopChain => bhopChain;
        public event Action<int> BunnyHopped;

        bool InBhopWindow => enableBunnyHop && IsGrounded && Time.time - landedTime <= bhopWindow;
        bool AutoHopQueued => autoBunnyHop && input.JumpHeld && InBhopWindow;

        void UpdateBhopChain()
        {
            if (IsGrounded && Time.time - landedTime > bhopWindow)
                bhopChain = 0;
        }

        /// <summary>Called right as a ground jump happens.</summary>
        void ApplyBunnyHop()
        {
            if (!InBhopWindow || HorizontalSpeed < runSpeed * 0.9f)
            {
                bhopChain = 0;
                return;
            }

            float speed = HorizontalSpeed;
            Vector3 dir = horizontalVelocity / speed;

            Vector2 move = input.Move;
            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 0.01f)
                dir = Vector3.Slerp(dir, wish.normalized, bhopRedirect).normalized;

            if (speed < bhopMaxSpeed) speed = Mathf.Min(speed + bhopSpeedGain, bhopMaxSpeed);
            horizontalVelocity = dir * speed;

            bhopChain++;
            BunnyHopped?.Invoke(bhopChain);
        }
    }
}
