using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Recoil hop: firing in the air pushes you opposite to where you aimed. Shoot at the ground
    /// to hop up or break a fall; shoot behind you for a little forward boost.
    /// Limited per airtime. Called by the weapon when it fires.
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
        public bool TryRecoilHop(Vector3 aimDirection)
        {
            if (!enableRecoilHop || IsGrounded || IsMantling || recoilHopsUsed >= maxRecoilHops) return false;

            if (IsDashing) EndDash();
            Vector3 push = -aimDirection.normalized * recoilHopSpeed;
            Vector3 horizontalPush = new Vector3(push.x, 0f, push.z) * recoilHopHorizontalScale;

            if (IsSwinging)
            {
                swingVelocity += horizontalPush + Vector3.up * push.y;
            }
            else
            {
                if (IsSlamming) IsSlamming = false;
                horizontalVelocity += horizontalPush;
                // Pushing up cancels your fall first, so shooting down always feels like a hop.
                verticalVelocity = push.y > 0f ? Mathf.Max(verticalVelocity, 0f) + push.y : verticalVelocity + push.y;
                isJumping = false;
            }

            recoilHopsUsed++;
            RecoilHopped?.Invoke();
            return true;
        }
    }
}
