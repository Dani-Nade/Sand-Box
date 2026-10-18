using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Forward-facing climb, separate from sideways wall running. Defaults use measured
    /// Apex movement units (1 hu = 0.0254 m); the slowdown curve is our own tuning.
    /// Climb space is based on the last grounded height, not each new wall contact.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Wall Climb")]
        [SerializeField] bool enableWallClimb = true;
        [SerializeField, Min(0f)] float wallClimbSpeed = 5.715f; // 225 hu/s
        [SerializeField, Min(0f)] float wallClimbCutoffHeight = 3.7338f; // 147 hu
        [SerializeField, Min(0f)] float wallClimbEndBoostHeight = 0.7112f; // 28 hu
        [Tooltip("Maximum ledge height above the last grounded feet position, including the final mantle.")]
        [SerializeField, Min(0f)] float wallClimbMaxLedgeHeight = 6.9342f; // 273 hu
        [SerializeField, Min(0f)] float wallClimbMantleReach = 2.5f;
        [Tooltip("Climbing eases down over the last part of its upward travel.")]
        [SerializeField, Min(0.01f)] float wallClimbSlowdownDistance = 0.65f;
        [SerializeField, Min(0f)] float wallClimbTopSpeed = 2f;
        [SerializeField, Min(0f)] float wallClimbStrafeSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] float wallClimbFacingDot = 0.7f;
        [SerializeField, Min(0f)] float wallClimbCheckDistance = 0.3f;
        [SerializeField, Min(0f)] float wallClimbReattachDelay = 0.2f;
        [Tooltip("Height removed from the remaining climb space after detaching, preventing infinite regrabs.")]
        [SerializeField, Min(0f)] float wallClimbDetachPenalty = 3.2512f; // 128 hu

        Vector3 wallClimbNormal;
        float wallClimbBaselineY;
        float wallClimbNextAttachTime = float.NegativeInfinity;
        bool wallClimbExhausted, wallClimbJumpArmed;
        float wallClimbMantleAvailableUntil = float.NegativeInfinity;
        float wallClimbLedgeCeiling;

        public bool IsWallClimbing { get; private set; }
        public Vector3 WallClimbNormal => IsWallClimbing ? wallClimbNormal : Vector3.zero;
        public float WallClimbHeightLeft => Mathf.Max(0f, wallClimbBaselineY + wallClimbCutoffHeight - transform.position.y);

        bool CanUseClimbMantle => IsWallClimbing || Time.time < wallClimbMantleAvailableUntil;

        void ResetWallClimb()
        {
            IsWallClimbing = false;
            wallClimbBaselineY = transform.position.y;
            wallClimbExhausted = wallClimbJumpArmed = false;
            wallClimbNextAttachTime = wallClimbMantleAvailableUntil = float.NegativeInfinity;
        }

        // True means this frame's horizontal/vertical climb motion has been supplied,
        // including the exit boost or push. Normal gravity is applied by UpdateVertical.
        bool UpdateWallClimb(float dt)
        {
            if (transform.position.y < wallClimbBaselineY) wallClimbBaselineY = transform.position.y;
            bool wasClimbing = IsWallClimbing;
            if (!enableWallClimb || IsGrounded || IsDashing || IsSlamming || IsDiving ||
                IsSliding || IsRolling || input.CrouchHeld || input.Move.y <= 0.1f)
            {
                StopWallClimb();
                return false;
            }

            // Jump initiates the grab; forward input sustains it. Releasing jump
            // arms a fresh press to push off, rather than instantly losing the grab.
            if ((!wasClimbing && !input.JumpHeld) || wallClimbExhausted || Time.time < wallClimbNextAttachTime)
                return false;

            if (!FindClimbWall(out Vector3 normal))
            {
                StopWallClimb();
                return false;
            }
            if (!wasClimbing && WallClimbHeightLeft <= 0f) return false;

            wallClimbNormal = normal;
            if (!wasClimbing)
            {
                IsWallClimbing = true;
                IsWallRunning = false;
                wallClimbJumpArmed = false;
                wallClimbLedgeCeiling = wallClimbBaselineY + wallClimbMaxLedgeHeight;
                // A grab consumes jump buffering/coyote time without spending another jump.
                lastJumpPressedTime = lastGroundedTime = float.NegativeInfinity;
                isJumping = false;
                horizontalVelocity = Vector3.ProjectOnPlane(horizontalVelocity, normal);
                verticalVelocity = Mathf.Max(verticalVelocity, wallClimbSpeed);
            }
            if (!input.JumpHeld) wallClimbJumpArmed = true;
            if (wasClimbing && wallClimbJumpArmed && input.JumpPressed)
            {
                float height = transform.position.y - wallClimbBaselineY;
                float push = height <= 0.4826f ? 4.7752f : 6.5532f; // 188/258 hu/s
                horizontalVelocity += normal * Mathf.Max(0f, push - Vector3.Dot(horizontalVelocity, normal));
                if (height <= 1.1938f)
                    verticalVelocity = Mathf.Max(verticalVelocity, Mathf.Sqrt(2f * gravity * wallClimbEndBoostHeight));
                StopWallClimb();
                wallRunReattachTime = Time.time + wallClimbReattachDelay;
                lastJumpPressedTime = float.NegativeInfinity;
                Jumped?.Invoke();
                return true;
            }

            float remaining = WallClimbHeightLeft;
            if (remaining <= 0.001f)
            {
                // The final boost creates a brief opportunity to grab a higher ledge.
                StopWallClimb();
                wallClimbExhausted = true;
                wallClimbMantleAvailableUntil = Time.time + 0.6f;
                verticalVelocity = Mathf.Sqrt(2f * gravity * wallClimbEndBoostHeight);
                return true;
            }

            float speed = Mathf.Lerp(wallClimbTopSpeed, wallClimbSpeed,
                Mathf.SmoothStep(0f, 1f, remaining / wallClimbSlowdownDistance));
            verticalVelocity = Mathf.Min(Mathf.MoveTowards(verticalVelocity, speed, groundAcceleration * dt),
                remaining / Mathf.Max(dt, 0.0001f));
            Vector3 along = Vector3.Cross(Vector3.up, normal).normalized;
            if (Vector3.Dot(along, transform.right) < 0f) along = -along;
            Vector3 target = along * (input.Move.x * wallClimbStrafeSpeed);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, groundAcceleration * dt);
            wallClimbMantleAvailableUntil = Time.time + 0.15f;
            return true;
        }

        bool FindClimbWall(out Vector3 normal)
        {
            normal = Vector3.zero;
            float range = controller.radius + wallClimbCheckDistance;
            Vector3 direction = FlatForward;
            for (int i = 0; i < 2; i++)
            {
                Vector3 origin = transform.position + Vector3.up * controller.height * (i == 0 ? 0.5f : 0.8f);
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
                // Floors/ramps and overhangs aren't climbable wall faces.
                if (Mathf.Abs(hit.normal.y) > 0.7f) continue;
                Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                if (Vector3.Dot(direction, -flatNormal) < wallClimbFacingDot) continue;
                normal = flatNormal;
                return true;
            }
            return false;
        }

        void StopWallClimb()
        {
            if (!IsWallClimbing) return;
            IsWallClimbing = false;
            wallClimbJumpArmed = false;
            wallClimbBaselineY -= wallClimbDetachPenalty;
            wallClimbNextAttachTime = Time.time + wallClimbReattachDelay;
        }
    }
}
