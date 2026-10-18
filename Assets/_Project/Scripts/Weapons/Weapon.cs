using System;
using System.Collections;
using SandHouse.Combat;
using SandHouse.Player;
using SandHouse.Stats;
using UnityEngine;

namespace SandHouse.Weapons
{
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
        [Tooltip("Local axis the spin part turns around (the barrel direction).")]
        [SerializeField] Vector3 spinAxis = Vector3.forward;
        [Tooltip("Swings out and back while cycling. Auto-found: a child named 'Lever'.")]
        [SerializeField] Transform leverPart;
        [Tooltip("Negative swings the back of the lever down (pivot at the front, like a real lever-action).")]
        [SerializeField] float leverAngle = -55f;
        [SerializeField] Vector3 leverAxis = Vector3.right;

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
        float kick, cycleTimer, spinAngle, spinTarget;

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
        /// <summary>0 = in hand, 1 = lowered out of view. Set by WeaponInventory while switching.</summary>
        public float Holster { get; set; }

        public event Action Fired;
        public event Action<HitResult> HitTarget;

        protected virtual void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            look = GetComponentInParent<PlayerLook>();
            motor = GetComponentInParent<PlayerMotor>();
            stats = GetComponentInParent<PlayerStats>();
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
            // Switching away cancels a reload and resets the pose.
            CancelReload();
            kick = 0f;
            cycleTimer = 0f;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            ApplyPose();
        }

        void AutoWireParts()
        {
            if (model == null) return;
            if (muzzle == null) muzzle = FindPart("Muzzle");
            if (spinPart == null) spinPart = FindPart("Cylinder");
            if (leverPart == null) leverPart = FindPart("Lever");
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
            if (PlayerLook.CursorLocked && !handsBusy && Holster < 0.05f)
                HandleInput();

            AnimateModel(Time.deltaTime);
        }

        void HandleInput()
        {
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
        }

        IEnumerator Reload()
        {
            IsReloading = true;
            if (data.reloadOneAtATime)
            {
                while (Ammo < MagazineSize)
                {
                    yield return Progress(data.roundReloadTime * ReloadTimeMultiplier);
                    Ammo++;
                }
            }
            else
            {
                yield return Progress(data.reloadTime * ReloadTimeMultiplier);
                Ammo = MagazineSize;
            }
            ReloadProgress = 0f;
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

        // ---------------------------------------------------------------- animation

        void AnimateModel(float dt)
        {
            kick = Mathf.MoveTowards(kick, 0f, data.modelRecoverySpeed * dt * Mathf.Max(kick, 0.1f));
            if (cycleTimer > 0f) cycleTimer -= dt;
            spinAngle = Mathf.Lerp(spinAngle, spinTarget, 1f - Mathf.Exp(-20f * dt));
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
            model.localPosition = modelRestPosition + new Vector3(0f, 0f, -data.modelKickDistance * kick) + reloadOffset;
            model.localRotation = modelRestRotation
                                  * Quaternion.Euler(-data.modelKickAngle * kick + 8f * cycle, 0f, 0f)
                                  * reloadRotation;

            if (spinPart != null) spinPart.localRotation = spinRest * Quaternion.AngleAxis(spinAngle, spinAxis);
            if (leverPart != null) leverPart.localRotation = leverRest * Quaternion.AngleAxis(leverAngle * cycle, leverAxis);
        }

        void ReloadPose(out Vector3 offset, out Quaternion rotation)
        {
            offset = Vector3.zero;
            rotation = Quaternion.identity;
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
