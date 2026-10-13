using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Air movement. Below walking speed you get simple direct air control.
    /// Above it, Quake/Source-style air strafing: you only gain speed in directions you're not
    /// already moving, so holding A/D while turning the mouse the same way builds speed,
    /// and holding W alone keeps your momentum without slowing down.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Air Strafe")]
        [Tooltip("Max speed you can add in a direction while strafing. Small = needs real mouse turning to gain speed.")]
        [SerializeField] float airStrafeWishCap = 1.2f;
        [SerializeField] float airStrafeAcceleration = 60f;
        [SerializeField] float maxAirSpeed = 32f;
        [Tooltip("Gentle steering in the air towards your input, without gaining speed (°/s). 0 = pure air strafing.")]
        [SerializeField] float airSteering = 90f;

        void AirMove(Vector3 wishDir, float wishSpeed, float dt)
        {
            if (wishDir.sqrMagnitude < 0.01f) return; // no input: keep momentum

            Vector3 dir = wishDir.normalized;
            float speed = horizontalVelocity.magnitude;

            // Steer: bend the current direction towards the input without changing speed
            // (only for inputs that aren't pointing backwards).
            if (airSteering > 0f && speed > 0.5f && Vector3.Dot(horizontalVelocity, dir) > 0f)
                horizontalVelocity = Vector3.RotateTowards(horizontalVelocity / speed, dir, airSteering * Mathf.Deg2Rad * dt, 0f) * speed;

            if (speed <= wishSpeed)
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, dir * wishSpeed, airAcceleration * dt);
                return;
            }

            float currentSpeedInDir = Vector3.Dot(horizontalVelocity, dir);
            float addSpeed = airStrafeWishCap - currentSpeedInDir;
            if (addSpeed <= 0f) return;

            horizontalVelocity += dir * Mathf.Min(airStrafeAcceleration * dt, addSpeed);
            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxAirSpeed);
        }
    }
}
