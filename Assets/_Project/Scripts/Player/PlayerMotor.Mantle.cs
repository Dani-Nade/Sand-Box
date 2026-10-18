using System;
using UnityEngine;

namespace SandHouse.Player
{
    /// <summary>
    /// Mantling: while airborne and holding forward into a ledge, climb up onto it.
    ///
    /// Mantle slide (timing skill): press crouch near the END of the climb - or just after landing on
    /// top - to come out of it in a slide with a bigger boost than a normal slide. Pressing too early
    /// fails for that mantle, and holding crouch from the start doesn't count.
    /// </summary>
    public partial class PlayerMotor
    {
        [Header("Mantle")]
        [SerializeField] bool enableMantle = true;
        [Tooltip("Lowest ledge (above your feet) that triggers a mantle. Lower ledges you just jump onto.")]
        [SerializeField] float minMantleHeight = 0.4f;
        [Tooltip("Highest ledge above your feet you can grab.")]
        [SerializeField] float maxMantleHeight = 1.5f;
        [Tooltip("How far in front of you a wall can be to grab it.")]
        [SerializeField] float mantleReach = 0.5f;
        [SerializeField] float mantleMinDuration = 0.18f;
        [SerializeField] float mantleMaxDuration = 0.36f;
        [SerializeField] float mantleCooldown = 0.25f;

        [Header("Mantle Slide")]
        [SerializeField] bool enableMantleSlide = true;
        [Tooltip("You need at least this much speed going into the mantle for it to become a slide.")]
        [SerializeField] float mantleSlideMinSpeed = 7f;
        [Tooltip("Bigger than a normal slide's boost, and it ignores the slide boost cooldown.")]
        [SerializeField] float mantleSlideBoost = 7f;
        [SerializeField] float mantleSlideMaxBoostedSpeed = 22f;
        [Tooltip("Timing window opens at this fraction of the climb (0.65 = the last 35%). Pressing before it fails.")]
        [SerializeField, Range(0f, 1f)] float mantleSlideWindowStart = 0.65f;
        [Tooltip("Seconds after the climb finishes where a crouch press still counts.")]
        [SerializeField] float mantleSlideLateWindow = 0.12f;

        Vector3 mantleStart, mantleEnd, mantleDirection;
        float mantleDuration, mantleTimer, mantleEntrySpeed;
        bool mantleEndsCrouched;
        float lastMantleEndTime = float.NegativeInfinity;

        // Mantle slide timing state.
        bool mantleSlideTimed;   // pressed inside the window
        bool mantleSlideFailed;  // pressed too early
        bool lateMantleSlideOpen;

        public bool IsMantling { get; private set; }
        public event Action MantleStarted;
        /// <summary>Fires with true for a well-timed mantle slide, false for a missed (too early) press.</summary>
        public event Action<bool> MantleSlideAttempted;

        bool TryStartMantle()
        {
            if (!enableMantle || IsMantling || IsSliding) return false;
            if (Time.time - lastMantleEndTime < mantleCooldown) return false;
            if (input.Move.y < 0.5f) return false;

            Vector3 feet = transform.position;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            float radius = controller.radius;
            float reachHeight = CanUseClimbMantle ? Mathf.Max(maxMantleHeight, wallClimbMantleReach) : maxMantleHeight;
            var ignore = QueryTriggerInteraction.Ignore;

            // 1. Find a wall face in front of us somewhere between our feet and max reach.
            RaycastHit wall = default;
            bool foundWall = false;
            for (float h = 0.2f; h <= reachHeight; h += 0.25f)
            {
                if (Physics.Raycast(feet + Vector3.up * h, forward, out RaycastHit hit, radius + mantleReach, obstructionMask, ignore)
                    && Mathf.Abs(hit.normal.y) < 0.3f)
                {
                    wall = hit;
                    foundWall = true;
                    break;
                }
            }
            if (!foundWall) return false;

            Vector3 inward = -wall.normal;
            inward.y = 0f;
            inward.Normalize();

            // 2. Find the top of that wall by probing down from just above max reach.
            Vector3 probe = wall.point + inward * 0.15f;
            probe.y = feet.y + reachHeight + 0.05f;
            if (Physics.CheckSphere(probe, 0.05f, obstructionMask, ignore)) return false; // wall is taller than we can reach

            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, reachHeight + 0.05f - minMantleHeight, obstructionMask, ignore))
                return false;
            if (top.normal.y < 0.7f) return false; // too steep to stand on

            float ledgeHeight = top.point.y - feet.y;
            if (ledgeHeight < minMantleHeight || ledgeHeight > reachHeight) return false;
            if (CanUseClimbMantle && top.point.y > wallClimbLedgeCeiling + 0.01f) return false;

            // 3. Nothing above our head on the way up.
            float headY = feet.y + controller.height;
            float climb = top.point.y + crouchingHeight - headY;
            if (climb > 0f && Physics.Raycast(new Vector3(feet.x, headY, feet.z), Vector3.up, climb, obstructionMask, ignore))
                return false;

            // 4. Room for us on top (standing preferred, crouching is enough).
            Vector3 end = new Vector3(wall.point.x, top.point.y, wall.point.z) + inward * (radius + 0.1f);
            if (CapsuleBlocked(end, crouchingHeight)) return false;

            StartMantle(end, inward, ledgeHeight, endCrouched: CapsuleBlocked(end, standingHeight));
            return true;
        }

        void StartMantle(Vector3 end, Vector3 direction, float ledgeHeight, bool endCrouched)
        {
            StopWallClimb();
            IsWallRunning = false;
            if (IsDashing) EndDash();
            IsMantling = true;
            IsGrounded = false;
            IsSliding = false;

            mantleStart = transform.position;
            mantleEnd = end;
            mantleDirection = direction;
            // Running into the ledge's wall zeroes our speed a frame or two before the mantle starts.
            float rememberedSpeed = Time.time - wallHitTime < 0.3f ? speedBeforeWallHit : 0f;
            mantleEntrySpeed = Mathf.Max(horizontalVelocity.magnitude, rememberedSpeed);
            mantleSlideTimed = false;
            mantleSlideFailed = false;
            lateMantleSlideOpen = false;
            mantleEndsCrouched = endCrouched;
            mantleTimer = 0f;
            float heightFraction = Mathf.InverseLerp(minMantleHeight, maxMantleHeight, ledgeHeight);
            mantleDuration = Mathf.Lerp(mantleMinDuration, mantleMaxDuration, heightFraction);

            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            controller.enabled = false; // we drive the position directly during the climb
            MantleStarted?.Invoke();
        }

        void UpdateMantle(float dt)
        {
            mantleTimer += dt;
            float t = Mathf.Clamp01(mantleTimer / mantleDuration);

            if (input.CrouchPressed && !mantleSlideTimed && !mantleSlideFailed)
            {
                if (t >= mantleSlideWindowStart) mantleSlideTimed = true;
                else
                {
                    mantleSlideFailed = true;
                    MantleSlideAttempted?.Invoke(false);
                }
            }

            // Pull up first, then over the edge.
            float up = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.7f), 2f);
            float over = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));

            transform.position = new Vector3(
                Mathf.Lerp(mantleStart.x, mantleEnd.x, over),
                Mathf.Lerp(mantleStart.y, mantleEnd.y, up),
                Mathf.Lerp(mantleStart.z, mantleEnd.z, over));

            if (t >= 1f) FinishMantle();
        }

        void FinishMantle()
        {
            IsMantling = false;
            controller.enabled = true;
            lastMantleEndTime = Time.time;
            lastGroundedTime = Time.time; // counts as grounded so the slide/jump grace windows work
            verticalVelocity = -2f;

            if (mantleEndsCrouched)
            {
                IsCrouching = true;
                SetHeight(crouchingHeight);
            }

            horizontalVelocity = mantleDirection * Mathf.Min(mantleEntrySpeed, runSpeed);

            bool canMantleSlide = enableMantleSlide && enableSlide && mantleEntrySpeed >= mantleSlideMinSpeed;
            if (canMantleSlide && mantleSlideTimed)
                StartMantleSlide();
            else
                lateMantleSlideOpen = canMantleSlide && !mantleSlideFailed;
        }

        /// <summary>Called from the slide logic: a crouch press just after the climb still counts.</summary>
        bool TryLateMantleSlide()
        {
            if (!lateMantleSlideOpen) return false;
            if (Time.time - lastMantleEndTime > mantleSlideLateWindow)
            {
                lateMantleSlideOpen = false;
                return false;
            }
            if (!input.CrouchPressed) return false;

            lateMantleSlideOpen = false;
            StartMantleSlide();
            return true;
        }

        void StartMantleSlide()
        {
            StartSlide(mantleDirection, Mathf.Max(mantleEntrySpeed, slideMinStartSpeed),
                mantleSlideBoost, mantleSlideMaxBoostedSpeed, useBoostCooldown: false);
            IsMantleSliding = true;
            MantleSlideAttempted?.Invoke(true);
        }

        void CancelMantle()
        {
            IsMantling = false;
            controller.enabled = true;
        }
    }
}
