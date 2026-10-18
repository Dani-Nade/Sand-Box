using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// External pushes on the player.
    ///  - Recoil hop: firing in the air pushes you opposite to where you aimed. Shoot at the ground
    ///    to hop up or break a fall; shoot behind you for a little forward boost. Limited per airtime.
    ///    Each weapon scales it (the shotgun's is huge - shotgun jumping).
    ///  - Knockback: explosions etc. (dynamite jumping). Not limited.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Recoil Hop")]
        [SerializeField] bool enableRecoilHop = true;
        [SerializeField] float recoilHopSpeed = 7f;
        [Tooltip("Horizontal part of the push is scaled by this (vertical hops matter most).")]
        [SerializeField, Range(0f, 1f)] float recoilHopHorizontalScale = 0.6f;
        [SerializeField] int maxRecoilHops = 2;

        int recoilHopsUsed;

        public int RecoilHopsLeft => Mathf.Max(0, maxRecoilHops - recoilHopsUsed);
        public event Action RecoilHopped;

        /// <summary>Applies a recoil push if airborne. Returns true if it did.</summary>
        /// <param name="strength">Weapon-specific multiplier (1 = revolver, 2+ = shotgun jump).</param>
        public bool TryRecoilHop(Vector3 aimDirection, float strength = 1f)
        {
            if (!enableRecoilHop || IsGrounded || IsMantling || recoilHopsUsed >= maxRecoilHops) return false;

            Vector3 push = -aimDirection.normalized * (recoilHopSpeed * strength);
            Push(new Vector3(push.x, 0f, push.z) * recoilHopHorizontalScale + Vector3.up * push.y);

            recoilHopsUsed++;
            RecoilHopped?.Invoke();
            return true;
        }

        /// <summary>Shoves the player (explosions). Works on the ground and in the air.</summary>
        public void ApplyKnockback(Vector3 velocity)
        {
            if (IsMantling) return;
            Push(velocity);
            if (velocity.y > 0f) lastGroundedTime = float.NegativeInfinity; // launched, not grounded
        }

        void Push(Vector3 velocity)
        {
            if (IsDashing) EndDash();
            if (IsSwinging)
            {
                swingVelocity += velocity;
                return;
            }

            IsSlamming = false;
            horizontalVelocity += new Vector3(velocity.x, 0f, velocity.z);
            // Pushing up cancels your fall first, so upward pushes always feel like a hop.
            verticalVelocity = velocity.y > 0f ? Mathf.Max(verticalVelocity, 0f) + velocity.y : verticalVelocity + velocity.y;
            isJumping = false;
        }
    }
}
