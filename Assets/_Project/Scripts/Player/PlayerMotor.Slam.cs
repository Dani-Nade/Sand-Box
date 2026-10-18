using System;
using System.Collections.Generic;
using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Ground slam: press crouch high in the air to slam straight down. The impact sends out a
    /// shockwave that damages and knocks back everything nearby - bigger the further you fell.
    /// Slam jump: press jump right as you land (or just before) to bounce back up; the fall
    /// height is stored, so big slams give huge jumps.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Ground Slam")]
        [SerializeField] bool enableSlam = true;
        [Tooltip("You must be at least this high above the ground to slam (lower = it's a landing roll press instead).")]
        [SerializeField] float slamMinHeight = 2.5f;
        [SerializeField] float slamSpeed = 32f;
        [SerializeField, Range(0f, 1f)] float slamHorizontalKeep = 0.2f;
        [SerializeField] float slamRadius = 4f;
        [Tooltip("Extra shockwave radius per metre fallen.")]
        [SerializeField] float slamRadiusPerMetre = 0.25f;
        [SerializeField] float slamMaxRadius = 9f;
        [SerializeField] float slamDamage = 60f;
        [Tooltip("Extra damage per metre fallen (0.05 = +5% per metre).")]
        [SerializeField] float slamDamagePerMetre = 0.05f;
        [SerializeField] float slamKnockback = 12f;

        [Header("Slam Jump")]
        [Tooltip("Seconds after landing a slam where jumping gives a slam jump.")]
        [SerializeField] float slamJumpWindow = 0.3f;
        [Tooltip("Extra jump height per metre of slam fall.")]
        [SerializeField] float slamJumpHeightPerMetre = 0.5f;
        [SerializeField] float slamJumpMaxExtraHeight = 7f;

        static readonly Collider[] SlamHits = new Collider[64];
        static readonly HashSet<Health> SlamDamaged = new HashSet<Health>();

        float slamStartY;
        float storedSlamHeight;
        float slamJumpAvailableUntil = float.NegativeInfinity;

        public bool IsSlamming { get; private set; }
        public event Action<float> Slammed;    // metres fallen
        public event Action<float> SlamJumped; // extra height gained

        void TryStartSlam()
        {
            if (!enableSlam || IsGrounded || IsSlamming || !input.CrouchPressed) return;

            // Too close to the ground: this press is a landing roll / landing slide instead.
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, slamMinHeight + 0.1f, obstructionMask, QueryTriggerInteraction.Ignore))
                return;

            if (IsDiving) EndDive();
            if (IsDashing) EndDash();
            IsSlamming = true;
            slamStartY = transform.position.y;
            horizontalVelocity *= slamHorizontalKeep;
            verticalVelocity = -slamSpeed;
            isJumping = false;
        }

        void FinishSlam()
        {
            IsSlamming = false;
            float height = Mathf.Max(0f, slamStartY - transform.position.y);
            storedSlamHeight = height;
            slamJumpAvailableUntil = Time.time + slamJumpWindow;
            horizontalVelocity = Vector3.zero;

            float radius = Mathf.Min(slamRadius + height * slamRadiusPerMetre, slamMaxRadius);
            Shockwave(transform.position, radius, height);
            Slammed?.Invoke(height);
        }

        void Shockwave(Vector3 center, float radius, float height)
        {
            float power = 1f + height * slamDamagePerMetre;
            float magic = stats != null ? stats.MagicDamageMultiplier : 1f; // the slam counts as an ability
            int count = Physics.OverlapSphereNonAlloc(center, radius, SlamHits, ~0, QueryTriggerInteraction.Ignore);
            SlamDamaged.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider col = SlamHits[i];
                if (col.transform.IsChildOf(transform)) continue;

                if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
                    col.attachedRigidbody.AddExplosionForce(slamKnockback * power, center, radius, 1f, ForceMode.VelocityChange);

                Health health = col.GetComponentInParent<Health>();
                if (health == null || health == selfHealth || !SlamDamaged.Add(health)) continue;

                Vector3 point = col.ClosestPoint(center);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, point) / radius);
                health.TakeDamage(new DamageInfo
                {
                    Amount = slamDamage * power * magic * Mathf.Lerp(0.4f, 1f, falloff),
                    Point = point + Vector3.up * 0.5f,
                    Direction = (point - center).normalized,
                    Source = gameObject,
                });
            }
        }

        bool TrySlamJump()
        {
            if (!IsGrounded || Time.time > slamJumpAvailableUntil) return false;
            if (Time.time - lastJumpPressedTime > jumpBufferTime) return false;

            float extra = Mathf.Min(storedSlamHeight * slamJumpHeightPerMetre, slamJumpMaxExtraHeight);
            verticalVelocity = Mathf.Sqrt(2f * gravity * (jumpHeight + extra));
            slamJumpAvailableUntil = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            isJumping = false; // full height even if jump is released
            Jumped?.Invoke();
            SlamJumped?.Invoke(extra);
            return true;
        }
    }
}
