using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Hold jump in the air alongside a wall to start running along it and slow your fall.
    /// Release and press jump again to launch off with your momentum.
    /// The run has a shared time limit until you land or release a lasso swing.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Wall Run")]
        [SerializeField] bool enableWallRun = true;
        [SerializeField] float wallRunMinSpeed = 5f;
        [SerializeField] float wallRunSpeed = 10f;
        [SerializeField] float wallRunAcceleration = 25f;
        [SerializeField] float wallRunDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] float wallRunGravityScale = 0.3f;
        [SerializeField] float wallRunMaxFallSpeed = 2f;
        [SerializeField] float wallRunStickSpeed = 2f;
        [Tooltip("Horizontal speed added away from the wall when jumping out of a wall run.")]
        [SerializeField] float wallRunJumpOutSpeed = 6f;
        [Tooltip("Upward speed when jumping out of a wall run.")]
        [SerializeField] float wallRunJumpUpSpeed = 7f;
        [Tooltip("Seconds before a wall run can start again after jumping off.")]
        [SerializeField, Min(0f)] float wallRunJumpReattachDelay = 0.25f;
        [Tooltip("How close (beyond your body) a side wall must be.")]
        [SerializeField] float wallCheckDistance = 0.5f;

        float wallRunTimeUsed;
        Vector3 wallRunNormal;
        bool wallRunJumpArmed;
        float wallRunReattachTime = float.NegativeInfinity;

        public bool IsWallRunning { get; private set; }
        public float WallRunTimeLeft => Mathf.Max(0f, wallRunDuration - wallRunTimeUsed);

        bool TryWallRunJump()
        {
            if (!IsWallRunning || !wallRunJumpArmed || !input.JumpPressed) return false;

            // Preserve all along-wall momentum and any existing outward/upward speed.
            float outwardSpeed = Vector3.Dot(horizontalVelocity, wallRunNormal);
            horizontalVelocity += wallRunNormal * Mathf.Max(0f, wallRunJumpOutSpeed - outwardSpeed);
            verticalVelocity = Mathf.Max(verticalVelocity, wallRunJumpUpSpeed);
            IsWallRunning = false;
            wallRunJumpArmed = false;
            wallRunReattachTime = Time.time + wallRunJumpReattachDelay;
            isJumping = false; // a wall-run jump is not shortened when jump is released
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
            return true;
        }

        void UpdateWallRun(float dt)
        {
            bool wasWallRunning = IsWallRunning;
            IsWallRunning = false;
            if (!enableWallRun || IsGrounded || IsDashing || IsSlamming || IsDiving ||
                (!wasWallRunning && !input.JumpHeld) || Time.time < wallRunReattachTime ||
                horizontalVelocity.magnitude < wallRunMinSpeed ||
                wallRunTimeUsed >= wallRunDuration)
                return;

            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);
            float range = controller.radius + wallCheckDistance;
            Vector3 bestTangent = Vector3.zero;
            float bestAlignment = 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (!Physics.Raycast(origin, transform.right * side, out RaycastHit hit, range,
                        obstructionMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y) >= 0.3f)
                    continue;

                Vector3 normal = hit.normal;
                normal.y = 0f;
                normal.Normalize();
                // Moving away from the wall must not pull a jump back into another run.
                if (Vector3.Dot(horizontalVelocity, normal) > 0.5f) continue;
                Vector3 along = Vector3.ProjectOnPlane(horizontalVelocity, normal);
                if (along.magnitude < wallRunMinSpeed) continue;
                Vector3 tangent = along.normalized;
                float alignment = Vector3.Dot(horizontalVelocity.normalized, tangent);
                if (alignment < 0.2f || alignment <= bestAlignment) continue;
                bestAlignment = alignment;
                bestTangent = tangent;
                wallRunNormal = normal;
            }
            if (bestTangent == Vector3.zero) return;

            IsWallRunning = true;
            // Entry's held press cannot also jump off. Releasing arms the next press.
            if (!wasWallRunning) wallRunJumpArmed = false;
            if (!input.JumpHeld) wallRunJumpArmed = true;
            wallRunTimeUsed = Mathf.Min(wallRunDuration, wallRunTimeUsed + dt);
            float alongSpeed = Mathf.Max(wallRunSpeed, Vector3.Dot(horizontalVelocity, bestTangent));
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, bestTangent * alongSpeed,
                wallRunAcceleration * dt);
        }
    }
}
