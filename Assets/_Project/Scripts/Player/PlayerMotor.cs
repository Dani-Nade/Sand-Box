using System;
using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// First-person movement on a CharacterController. The transform's pivot is at the player's feet.
    ///
    /// Split into partial files, one per movement feature:
    ///   PlayerMotor.cs            core: run/crouch/jump/turning, grounding, collision
    ///   PlayerMotor.Dash.cs       ground + air dashes (replaces sprinting)
    ///   PlayerMotor.Landing.cs    hard landings, fall damage, landing roll
    ///   PlayerMotor.Slide.cs      sliding
    ///   PlayerMotor.Mantle.cs     mantling + timed mantle slide
    ///   PlayerMotor.WallRun.cs  timed wall running
    ///   PlayerMotor.AirStrafe.cs  air strafing + air steering
    ///   PlayerMotor.BunnyHop.cs
    ///   PlayerMotor.Lasso.cs      lasso swing / yank
    ///   PlayerMotor.DeadEye.cs    slow-motion dive
    ///   PlayerMotor.Slam.cs       ground slam + slam jump
    ///   PlayerMotor.RecoilHop.cs  revolver recoil boosts in the air
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public partial class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform cameraRoot;
        [Tooltip("Where aimed abilities (lasso) shoot from. Defaults to the main camera.")]
        [SerializeField] Transform aimTransform;

        [Header("Speed (m/s)")]
        [Tooltip("Normal running speed. There's no sprint - Shift dashes instead (see PlayerMotor.Dash.cs).")]
        [SerializeField] float runSpeed = 10f;
        [SerializeField] float crouchSpeed = 4.5f;
        [Tooltip("Speed multiplier when moving backwards.")]
        [SerializeField, Range(0.1f, 1f)] float backwardSpeedMultiplier = 0.65f;
        [Tooltip("Speed multiplier when strafing sideways.")]
        [SerializeField, Range(0.1f, 1f)] float strafeSpeedMultiplier = 0.85f;

        [Header("Acceleration (m/s²)")]
        [SerializeField] float groundAcceleration = 60f;
        [SerializeField] float groundDeceleration = 50f;
        [Tooltip("Slowdown when moving faster than your normal top speed on the ground (after dashes, slides, bounces). Lower = keep momentum longer.")]
        [SerializeField] float overspeedDeceleration = 12f;
        [Tooltip("Air control used while slower than running speed. Above that, air strafing rules apply.")]
        [SerializeField] float airAcceleration = 14f;

        [Header("Turning (°/s)")]
        [Tooltip("How fast your running direction follows your input at normal speed.")]
        [SerializeField] float groundTurnRate = 720f;
        [Tooltip("Turn rate when moving faster than normal (after a dash/slide/hop) - still steerable, just heavier.")]
        [SerializeField] float fastTurnRate = 450f;
        [Tooltip("Fraction of speed lost per 90° of turning (sharp turns cost a little speed).")]
        [SerializeField, Range(0f, 1f)] float turnSpeedLoss = 0.1f;
        [Tooltip("Turning further than this counts as reversing: you brake instead of turning.")]
        [SerializeField, Range(90f, 180f)] float reverseAngle = 140f;

        [Header("Jump")]
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = 25f;
        [Tooltip("Grace period after walking off a ledge where jumping still works.")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Jump presses this long before landing still count.")]
        [SerializeField] float jumpBufferTime = 0.12f;
        [Tooltip("Releasing jump early multiplies upward speed by this (lower = shorter tap jumps).")]
        [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.5f;

        [Header("Crouch")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchingHeight = 1.1f;
        [SerializeField] float crouchTransitionSpeed = 10f;
        [SerializeField] float eyeOffsetFromTop = 0.15f;

        [Header("Collision")]
        [Tooltip("Layers that count as solid for crouching, mantling, wall running, lasso and slams.")]
        [SerializeField] LayerMask obstructionMask = ~(1 << 2);

        CharacterController controller;
        PlayerInputReader input;
        Health selfHealth;

        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressedTime = float.NegativeInfinity;
        bool isJumping;
        bool justLanded;
        Vector3 groundNormal = Vector3.up;

        // Speed right before we last ran into a wall - hitting a ledge's wall shouldn't erase mantle-slide speed.
        float speedBeforeWallHit;
        float wallHitTime = float.NegativeInfinity;

        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public float RunSpeed => runSpeed;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }

        public event Action Jumped;
        public event Action<float> Landed; // impact speed (m/s, positive)

        Transform Aim => aimTransform != null ? aimTransform : Camera.main != null ? Camera.main.transform : transform;
        Vector3 FlatForward
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.normalized;
            }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            selfHealth = GetComponent<Health>();
            dashCharge = maxDashCharges;
            SetHeight(standingHeight);
        }

        void OnDisable()
        {
            IsWallRunning = false;
            StopDeadEye();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (input.JumpPressed) lastJumpPressedTime = Time.time;
            if (input.CrouchPressed && !IsGrounded) lastAirCrouchPressTime = Time.time;
            UpdateDeadEyeTimer();
            RechargeDashes(dt);

            if (IsMantling)
            {
                IsWallRunning = false;
                UpdateMantle(dt);
                return;
            }
            if (IsSwinging)
            {
                IsWallRunning = false;
                UpdateLasso(dt);
                return;
            }

            UpdateGrounded();
            UpdateLandingTimers(dt);

            if (TryLasso())
            {
                IsWallRunning = false;
                return;
            }
            TryDeadEyeDive();
            TryDash();
            TryStartSlam();
            UpdateSlideState();
            UpdateCrouch(dt);

            if (IsDashing) UpdateDashVelocity(dt);
            else if (IsSliding) UpdateSlideVelocity(dt);
            else if (IsRolling) UpdateRollVelocity(dt);
            else if (!IsSlamming && !IsDiving) UpdateHorizontal(dt);

            UpdateWallRun(dt);
            UpdateVertical(dt);
            Move(dt);

            if (!IsGrounded && !IsSlamming && !IsDiving)
                TryStartMantle();
        }

        void UpdateGrounded()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = controller.isGrounded;
            justLanded = IsGrounded && !wasGrounded;

            if (!IsGrounded)
            {
                groundNormal = Vector3.up;
                return;
            }

            lastGroundedTime = Time.time;
            wallRunTimeUsed = 0f;
            recoilHopsUsed = 0;
            if (justLanded) landedTime = Time.time;
            UpdateBhopChain();

            if (justLanded)
            {
                float impact = -verticalVelocity;
                HandleLanding(impact);
                Landed?.Invoke(impact);
            }

            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // keeps the controller snapped to slopes and steps
                isJumping = false;
            }
        }

        void UpdateCrouch(float dt)
        {
            bool wantsCrouch = input.CrouchHeld || IsSliding || IsRolling || IsDiving;
            if (!wantsCrouch && IsCrouching && !CanStandUp())
                wantsCrouch = true;
            IsCrouching = wantsCrouch;

            float target = IsCrouching ? crouchingHeight : standingHeight;
            if (!Mathf.Approximately(controller.height, target))
                SetHeight(Mathf.MoveTowards(controller.height, target, crouchTransitionSpeed * dt));
        }

        void UpdateHorizontal(float dt)
        {
            Vector2 move = input.Move;
            Vector3 wishDir = Vector3.ClampMagnitude(transform.right * move.x + transform.forward * move.y, 1f);

            float baseSpeed = IsCrouching ? crouchSpeed : runSpeed;
            float maxSpeed = baseSpeed * DirectionalMultiplier(move) * StumbleMultiplier;

            if (!IsGrounded)
            {
                AirMove(wishDir, maxSpeed * wishDir.magnitude, dt);
                return;
            }

            float current = horizontalVelocity.magnitude;

            // Landing into a jump, or inside the bunny hop window: skip ground friction so hops keep their speed.
            if (GroundJumpQueued || (InBhopWindow && current > maxSpeed)) return;

            // No input: slow down.
            if (wishDir.sqrMagnitude < 0.01f)
            {
                float decel = current > maxSpeed + 0.1f ? overspeedDeceleration : groundDeceleration;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, decel * dt);
                return;
            }

            Vector3 wishNormal = wishDir.normalized;
            float targetSpeed = maxSpeed * wishDir.magnitude;

            // From standstill, or reversing direction: plain acceleration (brakes through zero).
            if (current < 0.5f || Vector3.Angle(horizontalVelocity, wishNormal) > reverseAngle)
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * maxSpeed, groundAcceleration * dt);
                return;
            }

            // Turning: rotate the running direction towards the input separately from speed changes,
            // so you can steer at speed instead of sliding around like a car.
            Vector3 dir = horizontalVelocity / current;
            float turnRate = (current > runSpeed + 0.5f ? fastTurnRate : groundTurnRate) * Mathf.Deg2Rad * dt;
            Vector3 newDir = Vector3.RotateTowards(dir, wishNormal, turnRate, 0f);
            float speed = current * (1f - turnSpeedLoss * Vector3.Angle(dir, newDir) / 90f);

            float accel = speed > targetSpeed + 0.1f ? overspeedDeceleration : groundAcceleration;
            horizontalVelocity = newDir * Mathf.MoveTowards(speed, targetSpeed, accel * dt);
        }

        float DirectionalMultiplier(Vector2 move)
        {
            if (move.sqrMagnitude < 0.01f) return 1f;
            Vector2 n = move.normalized;
            float multiplier = Mathf.Lerp(1f, strafeSpeedMultiplier, Mathf.Abs(n.x));
            return Mathf.Lerp(multiplier, backwardSpeedMultiplier, Mathf.Clamp01(-n.y));
        }

        bool GroundJumpQueued =>
            (Time.time - lastJumpPressedTime <= jumpBufferTime || AutoHopQueued) &&
            Time.time - lastGroundedTime <= coyoteTime &&
            (!IsCrouching || IsSliding || IsRolling);

        void UpdateVertical(float dt)
        {
            if (TryWallRunJump())
            {
                // handled
            }
            else if (TrySlamJump())
            {
                // handled
            }
            else if (GroundJumpQueued)
            {
                if (IsDashing) EndDash(dashJumpSpeed); // dash jump: keep extra speed
                if (IsSliding) EndSlide(); // slide jump: horizontal speed is kept
                rollTimer = 0f;            // roll jump: same
                ApplyBunnyHop();
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                isJumping = true;
                Jumped?.Invoke();
            }
            if (isJumping && verticalVelocity > 0f && !input.JumpHeld)
            {
                verticalVelocity *= jumpCutMultiplier;
                isJumping = false;
            }

            if (IsDashing && !IsGrounded) verticalVelocity = 0f; // air dashes hold your height
            else if (IsWallRunning && verticalVelocity <= 0f)
                verticalVelocity = Mathf.Max(verticalVelocity - gravity * wallRunGravityScale * dt, -wallRunMaxFallSpeed);
            else verticalVelocity -= gravity * dt;
            if (IsSlamming) verticalVelocity = Mathf.Min(verticalVelocity, -slamSpeed);
        }

        void Move(float dt)
        {
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            if (IsWallRunning) velocity -= wallRunNormal * wallRunStickSpeed;

            // On the ground, run along the surface so we follow slopes instead of launching off them.
            if (IsGrounded && verticalVelocity <= 0f && horizontalVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 along = Vector3.ProjectOnPlane(horizontalVelocity, groundNormal).normalized * horizontalVelocity.magnitude;
                velocity = along + Vector3.up * verticalVelocity;
            }

            CollisionFlags flags = controller.Move(velocity * dt);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;

            // Lose speed into walls instead of keeping it "stored" while pressed against them.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                if (Time.time - wallHitTime > 0.3f || horizontalVelocity.magnitude > speedBeforeWallHit)
                    speedBeforeWallHit = horizontalVelocity.magnitude;
                wallHitTime = Time.time;
                Vector3 actual = controller.velocity;
                horizontalVelocity = Vector3.ClampMagnitude(new Vector3(actual.x, 0f, actual.z), horizontalVelocity.magnitude);
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Remember the surface we're standing on (used for slope following and slide acceleration).
            if (hit.normal.y > 0.5f && hit.point.y < transform.position.y + controller.radius)
                groundNormal = hit.normal;
        }

        bool CanStandUp() => !CapsuleBlocked(transform.position, standingHeight);

        bool CapsuleBlocked(Vector3 feet, float height)
        {
            float radius = controller.radius * 0.95f;
            Vector3 bottom = feet + Vector3.up * (controller.radius + 0.05f);
            Vector3 top = feet + Vector3.up * (height - controller.radius);
            return Physics.CheckCapsule(bottom, top, radius, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        void SetHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraRoot != null)
                cameraRoot.localPosition = new Vector3(0f, height - eyeOffsetFromTop, 0f);
        }

        /// <summary>Instantly moves the player, clearing all momentum and movement states.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsMantling) CancelMantle();
            if (IsSwinging) DetachLasso();
            IsSliding = false;
            IsSlamming = false;
            IsDiving = false;
            dashTimer = 0f;
            rollTimer = 0f;
            stumbleTimer = 0f;
            StopDeadEye();

            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            IsWallRunning = false;
            wallRunTimeUsed = 0f;
            wallRunJumpArmed = false;
            wallRunReattachTime = float.NegativeInfinity;
        }
    }
}
