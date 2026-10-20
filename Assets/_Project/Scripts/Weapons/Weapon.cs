using System;
using System.Collections;
using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Stats;
using UnityEngine;

namespace SandHouse.Weapons
{
    public enum ReloadPhase
    {
        None,
        /// <summary>One-at-a-time reloads: getting into the reload pose (gate opens, casings drop).</summary>
        Opening,
        /// <summary>Loading rounds (ReloadProgress is per round). Whole-magazine reloads use this too.</summary>
        Loading,
        /// <summary>One-at-a-time reloads: getting back to firing.</summary>
        Closing,
    }

    public struct HitResult
    {
        public Vector3 Point;
        public float Damage;
        public bool IsCritical;
        public bool Killed;
    }

    /// <summary>
    /// Shared weapon behaviour: trigger input, ammo, reloading, recoil, model animation and holstering.
    /// Each weapon type only decides what a shot does (<see cref="Shoot"/>). All tuning numbers live in
    /// a <see cref="WeaponData"/> asset.
    ///
    /// Model slot: put the gun's model under the "model" transform. Child objects named "Muzzle",
    /// "Cylinder" (spins each shot) and "Lever" (swings while cycling) are found automatically.
    /// Shots always go from the centre of the camera, so they hit exactly where the crosshair is.
    /// </summary>
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField] protected WeaponData data;

        [Header("References")]
        [SerializeField] protected Camera aimCamera;
        [Tooltip("The visual model - drop your Blender model in under this.")]
        [SerializeField] protected Transform model;
        [Tooltip("Barrel tip. Auto-found if the model has a child called 'Muzzle'.")]
        [SerializeField] protected Transform muzzle;
        [SerializeField] protected Light muzzleFlash;
        [SerializeField] protected LayerMask hitMask = ~(1 << 2);

        [Header("Moving Parts (auto-found by name when empty)")]
        [Tooltip("Turns a step on every shot. Auto-found: a child named 'Cylinder'.")]
        [SerializeField] Transform spinPart;
        [SerializeField] float spinPerShot = 60f;
        [Tooltip("How snappily the spin part turns (higher = faster). With the damping below it overshoots a touch and settles - a visible 'click'.")]
        [SerializeField] float spinStiffness = 260f;
        [SerializeField] float spinDamping = 20f;
        [Tooltip("Local axis the spin part turns around (the barrel direction).")]
        [SerializeField] Vector3 spinAxis = Vector3.forward;
        [Tooltip("Swings out and back while cycling. Auto-found: a child named 'Lever'.")]
        [SerializeField] Transform leverPart;
        [Tooltip("Negative swings the back of the lever down (pivot at the front, like a real lever-action).")]
        [SerializeField] float leverAngle = -55f;
        [SerializeField] Vector3 leverAxis = Vector3.right;

        [Header("Inspect (optional)")]
        [Tooltip("Animation played when inspecting the weapon (Inspect key). Comes from the model's FBX.")]
        [SerializeField] AnimationClip inspectClip;
        [Tooltip("Object the inspect clip animates - the imported model's root.")]
        [SerializeField] GameObject inspectTarget;
        [Tooltip("Extra offset while inspecting, so the gun comes towards the middle of the screen.")]
        [SerializeField] Vector3 inspectOffset = new Vector3(-0.17f, 0.05f, 0.05f);
        [SerializeField] float inspectBlendTime = 0.3f;

        [Header("Aim Down Sights (hold Aim)")]
        [Tooltip("Model offset when aiming, relative to its normal spot - brings the sights to the middle of the screen.")]
        [SerializeField] Vector3 adsOffset = new Vector3(-0.22f, 0.135f, -0.06f);
        [Tooltip("Camera field of view multiplier while aiming (0.8 = 20% zoom).")]
        [SerializeField, Range(0.4f, 1f)] float adsZoom = 0.8f;
        [Tooltip("Spread multiplier while aiming (0.4 = 60% tighter).")]
        [SerializeField, Range(0f, 1f)] float adsSpreadMultiplier = 0.4f;
        [SerializeField] float adsBlendTime = 0.18f;

        [Header("Aim At Self")]
        [Tooltip("Model offset with the gun held to your head, relative to its normal spot. Kept on the right edge of the screen so you can see it.")]
        [SerializeField] Vector3 selfAimOffset = new Vector3(0.04f, 0.18f, -0.22f);
        [Tooltip("Model rotation with the gun held to your head (muzzle turned back towards your temple).")]
        [SerializeField] Vector3 selfAimRotation = new Vector3(10f, -120f, 20f);
        [SerializeField] float selfAimBlendTime = 0.35f;

        [Header("Effects")]
        [SerializeField] protected Material tracerMaterial;
        [SerializeField] protected Material impactMaterial;

        protected PlayerInputReader input;
        protected PlayerLook look;
        protected PlayerMotor motor;
        protected PlayerStats stats;

        float nextFireTime;
        Coroutine reloadRoutine;
        Vector3 rootRestPosition, modelRestPosition;
        Quaternion rootRestRotation, modelRestRotation, spinRest, leverRest;
        float kick, cycleTimer, spinAngle, spinTarget, spinVelocity, kickRollSign = 1f;
        float inspectTime = -1f; // < 0 = not inspecting
        float gateOpen, roundJolt; // LoadingGate pose: smoothed 0..1 open amount, per-round push
        bool selfAiming;
        float selfAimWeight, adsWeight;
        RussianRoulette roulette;

        public WeaponData Data => data;
        public string DisplayName => data != null ? data.displayName : name;
        public int Ammo { get; private set; }
        public int MagazineSize => data != null ? Mathf.Max(1, Mathf.CeilToInt(data.magazineSize * (Upgrades != null ? Upgrades.MagazineMultiplier : 1f))) : 0;
        public float FireInterval => data == null ? 0f : data.fireInterval / (Upgrades != null ? Upgrades.FireRateMultiplier : 1f);
        public float ReloadTimeMultiplier => Upgrades != null ? Upgrades.ReloadTimeMultiplier : 1f;
        protected virtual bool SupportsGunUpgrades => false;
        protected GunUpgrades Upgrades => SupportsGunUpgrades && stats != null ? stats.GunUpgrades : null;
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public ReloadPhase ReloadPhase { get; private set; }
        /// <summary>Spin part (e.g. revolver cylinder), for reload visuals.</summary>
        public Transform SpinPart => spinPart;
        public Transform Model => model;
        /// <summary>0 = in hand, 1 = lowered out of view. Set by WeaponInventory while switching.</summary>
        public float Holster { get; set; }
        public bool IsAimingAtSelf => selfAiming;
        /// <summary>0..1 how far into aim-down-sights (used for zoom, sway, sensitivity, spread).</summary>
        public float AimWeight => adsWeight;
        public float AdsZoom => adsZoom;
        /// <summary>Spread multiplier from aiming (1 = hip fire).</summary>
        protected float AimSpreadMultiplier => Mathf.Lerp(1f, adsSpreadMultiplier, adsWeight);
        /// <summary>Weapons that can be turned on yourself (not throwables).</summary>
        protected virtual bool SupportsSelfAim => true;
        public bool CanInspect => inspectClip != null && inspectTarget != null;
        public bool IsInspecting => inspectTime >= 0f;

        public event Action Fired;
        public event Action<HitResult> HitTarget;
        /// <summary>A reload got going; the argument is how many spent rounds came out (one-at-a-time reloads, after opening).</summary>
        public event Action<int> ReloadOpened;
        /// <summary>One round went in (one-at-a-time reloads).</summary>
        public event Action RoundLoaded;
        /// <summary>The player shot themselves (aim at self + fire).</summary>
        public event Action ShotSelf;

        protected virtual void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            look = GetComponentInParent<PlayerLook>();
            motor = GetComponentInParent<PlayerMotor>();
            stats = GetComponentInParent<PlayerStats>();
            roulette = GetComponent<RussianRoulette>();
            if (aimCamera == null) aimCamera = GetComponentInParent<Camera>();

            AutoWireParts();
            Ammo = MagazineSize;

            rootRestPosition = transform.localPosition;
            rootRestRotation = transform.localRotation;
            if (model != null)
            {
                modelRestPosition = model.localPosition;
                modelRestRotation = model.localRotation;
            }
            if (spinPart != null) spinRest = spinPart.localRotation;
            if (leverPart != null) leverRest = leverPart.localRotation;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        protected virtual void OnDisable()
        {
            // Switching away cancels a reload / inspect and resets the pose.
            CancelReload();
            StopInspect();
            selfAiming = false;
            selfAimWeight = 0f;
            adsWeight = 0f;
            kick = 0f;
            cycleTimer = 0f;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            ApplyPose();
        }

        void AutoWireParts()
        {
            if (model == null) return;
            if (muzzle == null) muzzle = FindPart("Muzzle");
            if (leverPart == null) leverPart = FindPart("Lever");
            if (spinPart == null)
            {
                spinPart = FindPart("Cylinder");
                // Spin around the barrel, whatever axes the model was exported with.
                if (spinPart != null) spinAxis = spinPart.InverseTransformDirection(transform.forward).normalized;
            }
        }

        Transform FindPart(string partName)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, partName, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        void Update()
        {
            if (data == null || input == null) return;

            bool handsBusy = motor != null && (motor.IsMantling || motor.IsRolling || motor.IsWallClimbing);
            if (handsBusy || Holster >= 0.05f)
            {
                StopInspect();
                selfAiming = false;
            }
            bool canAct = PlayerLook.CursorLocked && !handsBusy && Holster < 0.05f;
            if (canAct)
                HandleInput();

            // Aim down sights while Aim is held (not while reloading or with the gun at your head).
            bool aiming = canAct && input.AimHeld && !IsReloading && !selfAiming;
            if (aiming) StopInspect();
            adsWeight = Mathf.MoveTowards(adsWeight, aiming ? 1f : 0f, Time.deltaTime / Mathf.Max(adsBlendTime, 0.01f));

            UpdateInspect(Time.deltaTime);
            AnimateModel(Time.deltaTime);
        }

        void HandleInput()
        {
            // Aim at self: middle click raises the gun to your head (and lowers it again); right click fires.
            // Normal shooting is off while it's up; reloading lowers it.
            if (SupportsSelfAim && input.AimAtSelfPressed && !IsReloading)
            {
                selfAiming = !selfAiming;
                if (selfAiming) StopInspect();
            }
            if (selfAiming)
            {
                // With Russian roulette, R spins the cylinder instead of lowering the gun.
                if (input.ReloadPressed && roulette != null)
                {
                    if (selfAimWeight > 0.9f) roulette.Spin();
                    return;
                }
                if (input.ReloadPressed) selfAiming = false;
                else
                {
                    if (input.AttackPressed && selfAimWeight > 0.9f) PullTriggerOnSelf();
                    return;
                }
            }

            // Inspect toggles; shooting or reloading cancels it (and still happens).
            if (input.InspectPressed && CanInspect && !IsReloading)
            {
                if (IsInspecting) StopInspect();
                else inspectTime = 0f;
            }
            if (IsInspecting && (input.AttackPressed || input.ReloadPressed)) StopInspect();

            if (input.ReloadPressed) StartReload();

            bool trigger = data.automatic ? input.AttackHeld : input.AttackPressed;
            if (!trigger) return;

            // One-round-at-a-time reloads can be interrupted by shooting.
            if (IsReloading && data.reloadOneAtATime && Ammo > 0) CancelReload();

            if (IsReloading || Time.time < nextFireTime) return;
            if (Ammo > 0) Fire();
            else StartReload();
        }

        void Fire()
        {
            Ammo--;
            bool deadEye = motor != null && motor.IsDeadEye;
            nextFireTime = Time.time + FireInterval * (deadEye ? data.deadEyeFireIntervalMultiplier : 1f);
            kick = 1f;
            kickRollSign = UnityEngine.Random.value < 0.8f ? 1f : -0.6f; // mostly one way (torque), occasionally the other
            cycleTimer = data.cycleTime;
            spinTarget += spinPerShot;

            look?.AddRecoil(data.recoilPitch, UnityEngine.Random.Range(-data.recoilYawRandom, data.recoilYawRandom));
            if (muzzleFlash != null) StartCoroutine(Flash());

            Ray aim = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (data.recoilHopMultiplier > 0f) motor?.TryRecoilHop(aim.direction, data.recoilHopMultiplier);

            Shoot(aim);
            Fired?.Invoke();

            if (Ammo == 0 && data.autoReloadWhenEmpty) StartReload();
        }

        /// <summary>What actually happens when this weapon fires.</summary>
        protected abstract void Shoot(Ray aim);

        void PullTriggerOnSelf()
        {
            // Russian roulette decides which chamber is under the hammer; otherwise it's live if there's any ammo.
            bool live = roulette != null ? roulette.PullTrigger() : Ammo > 0;
            if (live)
            {
                ShootSelf();
                return;
            }
            kick = 0.25f; // empty chamber: just a dry click
            if (roulette != null) spinTarget += spinPerShot; // the hammer advances the cylinder
        }

        /// <summary>Turns the spin part (cylinder) by extra degrees - used by the roulette spin.</summary>
        public void AddSpin(float degrees, float extraVelocity = 0f)
        {
            spinTarget += degrees;
            spinVelocity += extraVelocity;
        }

        void ShootSelf()
        {
            Ammo--;
            kick = 1f;
            if (muzzleFlash != null) StartCoroutine(Flash());
            look?.AddRecoil(-6f, 14f); // head jerks sideways
            selfAiming = false;
            selfAimWeight = 0f;
            ShotSelf?.Invoke();

            // Normal player death: respawn in the arena, run over in the roguelike.
            var health = GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(new DamageInfo
                {
                    Amount = health.Current + health.Max,
                    Point = aimCamera.transform.position,
                    Direction = -aimCamera.transform.right,
                    Source = gameObject,
                    IsCritical = true,
                });
            }
        }

        protected float StatDamageMultiplier => (stats != null ? stats.WeaponDamageMultiplier : 1f)
                                                * (Upgrades != null ? Upgrades.DamageMultiplier : 1f);

        /// <summary>Damages whatever was hit (hitbox multipliers and stats applied) and reports the hit.</summary>
        protected void ApplyDamage(Collider collider, Rigidbody body, Vector3 point, Vector3 direction, float baseDamage, float magicDamage = 0f)
        {
            if (body != null && !body.isKinematic)
                body.AddForceAtPosition(direction * data.impactForce, point, ForceMode.Impulse);

            IDamageable target = DamageTarget(collider);
            var hitbox = collider.GetComponent<Hitbox>();
            float multiplier = hitbox != null ? hitbox.DamageMultiplier : 1f;
            if (target == null) return;
            if (target is Health health && health.IsDead) return;

            var info = new DamageInfo
            {
                Amount = multiplier * (baseDamage * StatDamageMultiplier
                    + magicDamage * (stats != null ? stats.MagicDamageMultiplier : 1f)),
                Point = point,
                Direction = direction,
                Source = gameObject,
                IsCritical = multiplier > 1f,
            };
            bool killed = target.TakeDamage(info);
            ReportHit(new HitResult { Point = point, Damage = info.Amount, IsCritical = info.IsCritical, Killed = killed });
        }

        protected static IDamageable DamageTarget(Collider collider)
        {
            var hitbox = collider.GetComponent<Hitbox>();
            return hitbox != null ? hitbox.Owner : collider.GetComponentInParent<IDamageable>();
        }

        protected void ReportHit(HitResult hit)
        {
            if (hit.Killed) Upgrades?.HealOnGunKill();
            HitTarget?.Invoke(hit);
        }

        /// <summary>Restocks between rooms / on a fresh run, including weapons not yet equipped.</summary>
        public void Restock()
        {
            CancelReload();
            if (stats == null) stats = GetComponentInParent<PlayerStats>();
            Ammo = MagazineSize;
            nextFireTime = 0f;
        }

        // ---------------------------------------------------------------- reloading

        void StartReload()
        {
            if (IsReloading || Ammo >= MagazineSize) return;
            reloadRoutine = StartCoroutine(Reload());
        }

        void CancelReload()
        {
            if (reloadRoutine != null) StopCoroutine(reloadRoutine);
            reloadRoutine = null;
            IsReloading = false;
            ReloadProgress = 0f;
            ReloadPhase = ReloadPhase.None;
        }

        IEnumerator Reload()
        {
            IsReloading = true;
            float speed = ReloadTimeMultiplier;
            if (data.reloadOneAtATime)
            {
                int spent = MagazineSize - Ammo;
                if (data.reloadOpenTime > 0f)
                {
                    ReloadPhase = ReloadPhase.Opening;
                    yield return Progress(data.reloadOpenTime * speed);
                }
                ReloadOpened?.Invoke(spent);

                ReloadPhase = ReloadPhase.Loading;
                while (Ammo < MagazineSize)
                {
                    yield return Progress(data.roundReloadTime * speed);
                    Ammo++;
                    roundJolt = 1f;
                    if (data.reloadStyle == ReloadStyle.LoadingGate) spinTarget += spinPerShot; // index to the next chamber
                    RoundLoaded?.Invoke();
                }

                if (data.reloadCloseTime > 0f)
                {
                    ReloadPhase = ReloadPhase.Closing;
                    yield return Progress(data.reloadCloseTime * speed);
                }
            }
            else
            {
                ReloadPhase = ReloadPhase.Loading;
                yield return Progress(data.reloadTime * speed);
                Ammo = MagazineSize;
            }
            ReloadProgress = 0f;
            ReloadPhase = ReloadPhase.None;
            IsReloading = false;
            reloadRoutine = null;
        }

        IEnumerator Progress(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                ReloadProgress = t / duration;
                yield return null;
            }
            ReloadProgress = 1f;
        }

        IEnumerator Flash()
        {
            muzzleFlash.enabled = true;
            yield return new WaitForSeconds(0.04f);
            muzzleFlash.enabled = false;
        }

        // ---------------------------------------------------------------- inspect

        void UpdateInspect(float dt)
        {
            if (!IsInspecting) return;
            inspectTime += dt;
            if (inspectTime >= inspectClip.length) StopInspect();
            else inspectClip.SampleAnimation(inspectTarget, inspectTime);
        }

        void StopInspect()
        {
            if (!IsInspecting) return;
            inspectTime = -1f;
            inspectClip.SampleAnimation(inspectTarget, 0f); // back to the rest pose
        }

        /// <summary>0..1, eases the inspect offset in at the start and out at the end.</summary>
        float InspectWeight
        {
            get
            {
                if (!IsInspecting) return 0f;
                float edge = Mathf.Min(inspectTime, inspectClip.length - inspectTime);
                return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / Mathf.Max(inspectBlendTime, 0.01f)));
            }
        }

        // ---------------------------------------------------------------- animation

        void AnimateModel(float dt)
        {
            kick = Mathf.MoveTowards(kick, 0f, data.modelRecoverySpeed * dt * Mathf.Max(kick, 0.1f));
            if (cycleTimer > 0f) cycleTimer -= dt;
            // Spring the spin part to its target: you see it turn, overshoot slightly and settle.
            spinVelocity += ((spinTarget - spinAngle) * spinStiffness - spinVelocity * spinDamping) * dt;
            spinAngle += spinVelocity * dt;

            // Gate pose eases in while opening/loading and back out when closing or interrupted.
            bool gateUp = IsReloading && ReloadPhase != ReloadPhase.Closing && data.reloadStyle == ReloadStyle.LoadingGate;
            float gateTime = Mathf.Max(gateUp ? data.reloadOpenTime : data.reloadCloseTime, 0.12f) * ReloadTimeMultiplier;
            gateOpen = Mathf.MoveTowards(gateOpen, gateUp ? 1f : 0f, dt / gateTime);
            roundJolt = Mathf.MoveTowards(roundJolt, 0f, dt * 6f);
            selfAimWeight = Mathf.MoveTowards(selfAimWeight, selfAiming ? 1f : 0f, dt / Mathf.Max(selfAimBlendTime, 0.01f));
            ApplyPose();
        }

        void ApplyPose()
        {
            // Whole weapon: lowered while switching.
            float holster = Mathf.SmoothStep(0f, 1f, Holster);
            transform.localPosition = rootRestPosition + new Vector3(0f, -0.35f * holster, -0.1f * holster);
            transform.localRotation = rootRestRotation * Quaternion.Euler(40f * holster, 0f, 0f);

            if (model == null || data == null) return;

            // Lever/pump cycle: 0 -> 1 -> 0 over the cycle time.
            float cycle = cycleTimer > 0f && data.cycleTime > 0f ? Mathf.Sin((1f - cycleTimer / data.cycleTime) * Mathf.PI) : 0f;

            ReloadPose(out Vector3 reloadOffset, out Quaternion reloadRotation);
            float selfAim = Mathf.SmoothStep(0f, 1f, selfAimWeight);
            model.localPosition = modelRestPosition + new Vector3(0f, 0f, -data.modelKickDistance * kick) + reloadOffset
                                  + inspectOffset * InspectWeight
                                  + selfAimOffset * selfAim
                                  + adsOffset * Mathf.SmoothStep(0f, 1f, adsWeight);
            model.localRotation = modelRestRotation
                                  * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(selfAimRotation), selfAim)
                                  * Quaternion.Euler(-data.modelKickAngle * kick + 8f * cycle, 0f, data.modelKickRoll * kickRollSign * kick)
                                  * reloadRotation;

            if (spinPart != null) spinPart.localRotation = spinRest * Quaternion.AngleAxis(spinAngle, spinAxis);
            if (leverPart != null) leverPart.localRotation = leverRest * Quaternion.AngleAxis(leverAngle * cycle, leverAxis);
        }

        void ReloadPose(out Vector3 offset, out Quaternion rotation)
        {
            offset = Vector3.zero;
            rotation = Quaternion.identity;

            if (data.reloadStyle == ReloadStyle.LoadingGate)
            {
                // Roll onto the left side so the loading gate (right side) faces up, muzzle tipped up,
                // pulled towards the middle of the screen. Each round pushed in gives a small jolt.
                float open = Mathf.SmoothStep(0f, 1f, gateOpen);
                float push = Mathf.Sin(roundJolt * Mathf.PI) * open;
                offset = new Vector3(-0.07f, 0.035f, -0.03f) * open + new Vector3(0f, -0.004f, 0.008f) * push;
                rotation = Quaternion.Euler(-18f * open + 4f * push, -10f * open, 60f * open);
                return;
            }

            if (!IsReloading) return;

            float p = ReloadProgress;
            float arc = Mathf.Sin(p * Mathf.PI);
            switch (data.reloadStyle)
            {
                case ReloadStyle.Spin:
                    offset = new Vector3(0f, -0.12f * arc, 0f);
                    rotation = Quaternion.Euler(25f * arc, 0f, 0f) * Quaternion.Euler(0f, 0f, p * 360f);
                    break;
                case ReloadStyle.BreakOpen:
                    float tilt = Mathf.Clamp01(arc * 1.6f);
                    offset = new Vector3(0f, -0.08f * tilt, 0f);
                    rotation = Quaternion.Euler(35f * tilt, 0f, -10f * tilt);
                    break;
                case ReloadStyle.Dip:
                    offset = new Vector3(0f, -0.04f * arc - 0.03f, 0f);
                    rotation = Quaternion.Euler(10f * arc, 0f, 18f);
                    break;
                case ReloadStyle.Lower:
                    offset = new Vector3(0f, -0.4f * arc, 0f);
                    rotation = Quaternion.Euler(50f * arc, 0f, 0f);
                    break;
            }
        }
    }
}
