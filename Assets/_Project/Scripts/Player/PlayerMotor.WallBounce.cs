using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Wall bounce: press jump in the air next to a wall to kick off it, up and away.
    /// Limited number of bounces until you touch the ground again.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Wall Bounce")]
        [SerializeField] bool enableWallBounce = true;
        [SerializeField] float wallBounceUpSpeed = 7f;
        [Tooltip("Speed pushed away from the wall.")]
        [SerializeField] float wallBounceOutSpeed = 6.5f;
        [Tooltip("How much of your speed along the wall you keep (1 = all of it).")]
        [SerializeField, Range(0f, 1.5f)] float wallBounceAlongKeep = 1f;
        [Tooltip("How close (beyond your body) a wall must be.")]
        [SerializeField] float wallCheckDistance = 0.5f;
        [SerializeField] int maxWallBounces = 3;

        const int WallCheckDirections = 8;

        int wallBouncesUsed;

        public int WallBouncesUsed => wallBouncesUsed;
        public int MaxWallBounces => maxWallBounces;
        public event Action WallBounced;

        bool TryWallBounce()
        {
            if (!enableWallBounce || IsSlamming || wallBouncesUsed >= maxWallBounces) return false;

            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);
            float range = controller.radius + wallCheckDistance;
            float closest = float.MaxValue;
            Vector3 normal = Vector3.zero;

            for (int i = 0; i < WallCheckDirections; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 360f / WallCheckDirections, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, obstructionMask, QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.normal.y) < 0.3f && hit.distance < closest)
                {
                    closest = hit.distance;
                    normal = hit.normal;
                }
            }
            if (closest == float.MaxValue) return false;

            normal.y = 0f;
            normal.Normalize();

            Vector3 along = Vector3.ProjectOnPlane(horizontalVelocity, normal) * wallBounceAlongKeep;
            horizontalVelocity = along + normal * wallBounceOutSpeed;
            verticalVelocity = wallBounceUpSpeed;
            isJumping = false; // releasing jump shouldn't cut a bounce short
            wallBouncesUsed++;
            WallBounced?.Invoke();
            return true;
        }
    }
}
