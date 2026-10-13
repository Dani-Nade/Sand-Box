using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Procedural camera motion: head bob synced to footsteps, strafe/slide tilt, landing dips,
    /// a forward flip on landing rolls, slam punches and FOV changes (sprint, slide, swing, Dead Eye).
    /// Sits on a transform between CameraRoot and the camera, so it stacks on top of crouch height and look.
    /// WeaponSway reads <see cref="Phase"/> / <see cref="Weight"/> so the gun bobs in sync.
    /// </summary>
    public class HeadBob : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] Camera targetCamera;
        [Tooltip("Scales every effect. 0 turns camera motion off (motion-sickness option).")]
        [SerializeField, Range(0f, 1f)] float intensity = 1f;

        [Header("Bob")]
        [Tooltip("Metres travelled per full bob cycle (two footsteps).")]
        [SerializeField] float walkCycleLength = 2.1f;
        [SerializeField] float sprintCycleLength = 2.6f;
        [SerializeField] float verticalAmplitude = 0.04f;
        [SerializeField] float horizontalAmplitude = 0.025f;
        [SerializeField] float sprintAmplitudeMultiplier = 1.5f;
        [SerializeField] float crouchAmplitudeMultiplier = 0.6f;
        [Tooltip("Speed at which the bob reaches full strength.")]
        [SerializeField] float fullBobSpeed = 5f;
        [SerializeField] float weightSmoothing = 10f;

        [Header("Tilt")]
        [Tooltip("Degrees of roll per m/s of sideways speed.")]
        [SerializeField] float tiltPerSpeed = 0.25f;
        [SerializeField] float maxTilt = 2f;
        [SerializeField] float slideRoll = 4f;
        [SerializeField] float tiltSmoothing = 8f;

        [Header("Landing")]
        [Tooltip("Downward kick per m/s of landing speed.")]
        [SerializeField] float landingImpulse = 0.06f;
        [SerializeField] float maxLandingImpulse = 1.2f;
        [Tooltip("Extra kick on a hard landing (stumble).")]
        [SerializeField] float hardLandingImpulse = 2.2f;
        [SerializeField] float springStiffness = 160f;
        [SerializeField] float springDamping = 16f;

        [Header("Landing Roll")]
        [Tooltip("Full 360° forward flip of the camera when rolling. Off = a short dip instead.")]
        [SerializeField] bool fullRollFlip = true;
        [SerializeField] float rollDipAngle = 35f;

        [Header("Slam")]
        [SerializeField] float slamImpulse = 2.5f;
        [SerializeField] float slamFovPunch = 12f;

        [Header("FOV")]
        [SerializeField] float sprintFovIncrease = 7f;
        [SerializeField] float slideFovIncrease = 10f;
        [Tooltip("FOV added per m/s above 10 m/s while swinging or flying.")]
        [SerializeField] float speedFovPerMetre = 0.6f;
        [SerializeField] float maxSpeedFov = 14f;
        [Tooltip("Negative = zoom in during Dead Eye.")]
        [SerializeField] float deadEyeFovChange = -8f;
        [SerializeField] float fovSmoothing = 7f;
        [SerializeField] float fovPunchRecovery = 6f;

        float baseFov;
        float tilt;
        float landOffset, landVelocity;
        float previousStepSignal;
        float rollTimer;
        float fovPunch;

        /// <summary>Bob cycle angle in radians (0..2π, one cycle = two footsteps).</summary>
        public float Phase { get; private set; }
        /// <summary>0..1, how strongly bobbing currently applies (0 in the air or standing still).</summary>
        public float Weight { get; private set; }

        /// <summary>Fires on each footstep contact. Hook footstep sounds up to this.</summary>
        public event Action Footstep;

        /// <summary>Landings slower than this (stepping off kerbs, slopes) don't trigger a dip.</summary>
        public const float MinLandingSpeed = 3.5f;

        void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            if (targetCamera != null) baseFov = targetCamera.fieldOfView;
        }

        void OnEnable()
        {
            if (motor == null) return;
            motor.Landed += OnLanded;
            motor.HardLanded += OnHardLanded;
            motor.Rolled += OnRolled;
            motor.Slammed += OnSlammed;
        }

        void OnDisable()
        {
            if (motor == null) return;
            motor.Landed -= OnLanded;
            motor.HardLanded -= OnHardLanded;
            motor.Rolled -= OnRolled;
            motor.Slammed -= OnSlammed;
        }

        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < MinLandingSpeed || motor.IsRolling) return;
            landVelocity -= Mathf.Min(impactSpeed * landingImpulse, maxLandingImpulse);
        }

        void OnHardLanded(float impactSpeed) => landVelocity -= hardLandingImpulse;
        void OnRolled() => rollTimer = motor.RollDuration;

        void OnSlammed(float height)
        {
            landVelocity -= slamImpulse;
            fovPunch += slamFovPunch;
        }

        void LateUpdate()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            float speed = motor.HorizontalSpeed;
            Vector3 localVelocity = motor.transform.InverseTransformDirection(motor.Velocity);

            // Bob cycle advances with distance travelled on the ground.
            bool bobbing = motor.IsGrounded && !motor.IsSliding && !motor.IsMantling && !motor.IsRolling;
            float targetWeight = bobbing ? Mathf.Clamp01(speed / fullBobSpeed) : 0f;
            Weight = Mathf.Lerp(Weight, targetWeight, 1f - Mathf.Exp(-weightSmoothing * dt));

            if (motor.IsGrounded)
            {
                float cycleLength = motor.IsSprinting ? sprintCycleLength : walkCycleLength;
                Phase = Mathf.Repeat(Phase + speed / cycleLength * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            }

            // A footstep lands at the low point of the vertical bob (sin(2φ) crossing from + to -).
            float stepSignal = Mathf.Sin(Phase * 2f);
            if (previousStepSignal > 0f && stepSignal <= 0f && Weight > 0.3f)
                Footstep?.Invoke();
            previousStepSignal = stepSignal;

            float amplitude = motor.IsCrouching ? crouchAmplitudeMultiplier : motor.IsSprinting ? sprintAmplitudeMultiplier : 1f;
            Vector3 bob = BobOffset(Phase, horizontalAmplitude, verticalAmplitude) * (amplitude * Weight);

            // Landing spring.
            landVelocity += (-springStiffness * landOffset - springDamping * landVelocity) * dt;
            landOffset += landVelocity * dt;

            // Tilt: lean into strafes, roll into slides.
            float targetTilt = Mathf.Clamp(-localVelocity.x * tiltPerSpeed, -maxTilt, maxTilt);
            if (motor.IsSliding) targetTilt += slideRoll;
            tilt = Mathf.Lerp(tilt, targetTilt, 1f - Mathf.Exp(-tiltSmoothing * dt));

            // Landing roll: forward flip (or dip).
            float rollPitch = 0f;
            if (rollTimer > 0f)
            {
                rollTimer -= dt;
                float p = 1f - Mathf.Clamp01(rollTimer / motor.RollDuration);
                rollPitch = fullRollFlip ? 360f * Mathf.SmoothStep(0f, 1f, p) : Mathf.Sin(p * Mathf.PI) * rollDipAngle;
            }

            transform.localPosition = (bob + Vector3.up * landOffset) * intensity;
            transform.localRotation = Quaternion.Euler(rollPitch * intensity, 0f, tilt * intensity);

            UpdateFov(dt, speed);
        }

        void UpdateFov(float dt, float speed)
        {
            if (targetCamera == null) return;

            float boost = 0f;
            if (motor.IsSliding) boost = slideFovIncrease;
            else if (motor.IsSprinting && motor.IsGrounded && speed > motor.WalkSpeed + 0.5f) boost = sprintFovIncrease;

            if (!motor.IsGrounded || motor.IsSwinging)
                boost = Mathf.Max(boost, Mathf.Clamp((motor.Velocity.magnitude - 10f) * speedFovPerMetre, 0f, maxSpeedFov));
            if (motor.IsDeadEye) boost += deadEyeFovChange;

            fovPunch = Mathf.Lerp(fovPunch, 0f, 1f - Mathf.Exp(-fovPunchRecovery * dt));
            float targetFov = baseFov + (boost + fovPunch) * intensity;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));
        }

        /// <summary>Figure-8 bob: one side-to-side sway per cycle, a dip on every footstep.</summary>
        public static Vector3 BobOffset(float phase, float horizontal, float vertical)
        {
            return new Vector3(
                Mathf.Sin(phase) * horizontal,
                (Mathf.Cos(phase * 2f) - 1f) * 0.5f * vertical,
                0f);
        }
    }
}
