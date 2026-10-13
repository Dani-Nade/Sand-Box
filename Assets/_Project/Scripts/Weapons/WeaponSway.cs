using SandHouse.Player;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Procedural first-person weapon motion on the weapon holder: look sway, bob (synced with HeadBob),
    /// strafe tilt, jump/fall offset, landing dip and a sprint pose.
    /// The weapon itself animates its own model (recoil, reload) underneath this, so the effects stack.
    /// </summary>
    public class WeaponSway : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] HeadBob headBob;
        [SerializeField, Range(0f, 1f)] float intensity = 1f;

        [Header("Look Sway")]
        [Tooltip("Degrees of lag per pixel of mouse movement.")]
        [SerializeField] float lookSwayRotation = 0.06f;
        [SerializeField] float lookSwayPosition = 0.0006f;
        [SerializeField] float maxLookSwayRotation = 6f;
        [SerializeField] float maxLookSwayPosition = 0.03f;
        [SerializeField] float lookSwaySmoothing = 10f;

        [Header("Bob")]
        [SerializeField] float bobHorizontal = 0.014f;
        [SerializeField] float bobVertical = 0.012f;
        [SerializeField] float bobRoll = 1.2f;
        [SerializeField] float sprintBobMultiplier = 1.8f;

        [Header("Movement")]
        [SerializeField] float strafeRollPerSpeed = 0.7f;
        [SerializeField] float maxStrafeRoll = 5f;
        [Tooltip("Metres of offset per m/s of vertical speed (gun lags behind jumps and falls).")]
        [SerializeField] float verticalLag = 0.004f;
        [SerializeField] float maxVerticalLag = 0.05f;
        [SerializeField] float movementSmoothing = 10f;

        [Header("Landing")]
        [SerializeField] float landingImpulse = 0.08f;
        [SerializeField] float maxLandingImpulse = 1.5f;
        [SerializeField] float springStiffness = 120f;
        [SerializeField] float springDamping = 12f;

        [Header("Sprint Pose")]
        [SerializeField] Vector3 sprintPosition = new Vector3(-0.04f, -0.05f, -0.03f);
        [SerializeField] Vector3 sprintRotation = new Vector3(14f, -22f, 10f);
        [SerializeField] float sprintBlendSpeed = 8f;
        [Tooltip("After shooting, the sprint pose stays off for this long.")]
        [SerializeField] float sprintPoseShotCooldown = 0.4f;

        [Header("Crouch Pose")]
        [SerializeField] Vector3 crouchPosition = new Vector3(-0.015f, -0.015f, 0f);
        [SerializeField] float crouchRoll = 4f;

        [Header("Slide Pose")]
        [SerializeField] Vector3 slidePosition = new Vector3(-0.03f, -0.03f, 0f);
        [SerializeField] Vector3 slideRotation = new Vector3(4f, -6f, 18f);

        [Header("Mantle Pose")]
        [Tooltip("The gun drops out of view while climbing.")]
        [SerializeField] Vector3 mantlePosition = new Vector3(0f, -0.22f, -0.05f);
        [SerializeField] Vector3 mantleRotation = new Vector3(35f, 0f, -10f);
        [SerializeField] float mantleBlendSpeed = 14f;

        PlayerInputReader input;
        Revolver weapon;
        Vector3 restPosition;
        Quaternion restRotation;

        Vector3 lookSwayPos, lookSwayRot;
        float strafeRoll, verticalOffset;
        float landOffset, landVelocity;
        float sprintBlend, crouchBlend, slideBlend, mantleBlend;
        float lastShotTime = float.NegativeInfinity;

        void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            weapon = GetComponentInChildren<Revolver>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        void OnEnable()
        {
            if (motor != null) motor.Landed += OnLanded;
            if (weapon != null) weapon.Fired += OnFired;
        }

        void OnDisable()
        {
            if (motor != null) motor.Landed -= OnLanded;
            if (weapon != null) weapon.Fired -= OnFired;
        }

        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < HeadBob.MinLandingSpeed) return;
            landVelocity -= Mathf.Min(impactSpeed * landingImpulse, maxLandingImpulse);
        }

        void OnFired()
        {
            lastShotTime = Time.time;
            sprintBlend = 0f; // snap out of the sprint pose immediately
        }

        void LateUpdate()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            float smooth = 1f - Mathf.Exp(-movementSmoothing * dt);

            // Look sway: the gun lags behind camera rotation.
            Vector2 look = PlayerLook.CursorLocked && input != null ? input.Look : Vector2.zero;
            if (input != null && input.LookIsGamepad) look *= 8f; // stick values are -1..1, scale to roughly mouse-pixel range
            Vector3 targetSwayRot = new Vector3(
                Mathf.Clamp(look.y * lookSwayRotation, -maxLookSwayRotation, maxLookSwayRotation),
                Mathf.Clamp(-look.x * lookSwayRotation, -maxLookSwayRotation, maxLookSwayRotation),
                Mathf.Clamp(-look.x * lookSwayRotation, -maxLookSwayRotation, maxLookSwayRotation) * 0.5f);
            Vector3 targetSwayPos = new Vector3(
                Mathf.Clamp(-look.x * lookSwayPosition, -maxLookSwayPosition, maxLookSwayPosition),
                Mathf.Clamp(-look.y * lookSwayPosition, -maxLookSwayPosition, maxLookSwayPosition),
                0f);
            float swaySmooth = 1f - Mathf.Exp(-lookSwaySmoothing * dt);
            lookSwayRot = Vector3.Lerp(lookSwayRot, targetSwayRot, swaySmooth);
            lookSwayPos = Vector3.Lerp(lookSwayPos, targetSwayPos, swaySmooth);

            // Bob, synced to the head bob's footstep cycle.
            Vector3 bobPos = Vector3.zero;
            float bobRot = 0f;
            if (headBob != null)
            {
                float amount = headBob.Weight * (motor.IsSprinting ? sprintBobMultiplier : 1f);
                bobPos = HeadBob.BobOffset(headBob.Phase, bobHorizontal, bobVertical) * amount;
                bobRot = Mathf.Sin(headBob.Phase) * bobRoll * amount;
            }

            // Strafe roll and vertical lag.
            Vector3 localVelocity = motor.transform.InverseTransformDirection(motor.Velocity);
            strafeRoll = Mathf.Lerp(strafeRoll, Mathf.Clamp(-localVelocity.x * strafeRollPerSpeed, -maxStrafeRoll, maxStrafeRoll), smooth);
            float airborneVertical = motor.IsGrounded ? 0f : localVelocity.y;
            verticalOffset = Mathf.Lerp(verticalOffset, Mathf.Clamp(-airborneVertical * verticalLag, -maxVerticalLag, maxVerticalLag), smooth);

            // Landing spring.
            landVelocity += (-springStiffness * landOffset - springDamping * landVelocity) * dt;
            landOffset += landVelocity * dt;

            // Poses.
            bool recentlyShot = Time.time - lastShotTime < sprintPoseShotCooldown;
            bool reloading = weapon != null && weapon.IsReloading;
            bool sprintPose = motor.IsSprinting && motor.IsGrounded && motor.HorizontalSpeed > 1f && !recentlyShot && !reloading;
            sprintBlend = Mathf.MoveTowards(sprintBlend, sprintPose ? 1f : 0f, sprintBlendSpeed * dt);
            crouchBlend = Mathf.MoveTowards(crouchBlend, motor.IsCrouching && !motor.IsSliding ? 1f : 0f, sprintBlendSpeed * dt);
            slideBlend = Mathf.MoveTowards(slideBlend, motor.IsSliding ? 1f : 0f, sprintBlendSpeed * dt);
            // Gun drops out of view while the hands are busy (climbing, rolling).
            bool handsBusy = motor.IsMantling || motor.IsRolling;
            mantleBlend = Mathf.MoveTowards(mantleBlend, handsBusy ? 1f : 0f, mantleBlendSpeed * dt);
            float sprintEase = Mathf.SmoothStep(0f, 1f, sprintBlend);
            float crouchEase = Mathf.SmoothStep(0f, 1f, crouchBlend);
            float slideEase = Mathf.SmoothStep(0f, 1f, slideBlend);
            float mantleEase = Mathf.SmoothStep(0f, 1f, mantleBlend);

            Vector3 position = lookSwayPos + bobPos
                               + new Vector3(0f, verticalOffset + landOffset, 0f)
                               + sprintPosition * sprintEase
                               + crouchPosition * crouchEase
                               + slidePosition * slideEase
                               + mantlePosition * mantleEase;
            Vector3 euler = lookSwayRot
                            + new Vector3(-verticalOffset * 100f, 0f, strafeRoll + bobRot + crouchRoll * crouchEase)
                            + sprintRotation * sprintEase
                            + slideRotation * slideEase
                            + mantleRotation * mantleEase;

            transform.localPosition = restPosition + position * intensity;
            transform.localRotation = restRotation * Quaternion.Euler(euler * intensity);
        }
    }
}
