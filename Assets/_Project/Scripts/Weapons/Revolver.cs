using System;
using System.Collections;
using SandHouse.Combat;
using SandHouse.Player;
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
    /// Semi-auto hitscan revolver. Fires from the centre of the camera so shots go
    /// exactly where the crosshair is; the model and tracer are visual only.
    /// </summary>
    public class Revolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Camera aimCamera;
        [SerializeField] Transform model;
        [SerializeField] Transform muzzle;
        [SerializeField] Light muzzleFlash;

        [Header("Stats")]
        [SerializeField] float damage = 25f;
        [SerializeField] float range = 200f;
        [Tooltip("Minimum seconds between shots.")]
        [SerializeField] float fireInterval = 0.22f;
        [SerializeField] int magazineSize = 6;
        [SerializeField] float reloadTime = 1.2f;
        [SerializeField] bool autoReloadWhenEmpty = true;
        [SerializeField] float impactForce = 6f;
        [Tooltip("Fire interval multiplier during Dead Eye (0.5 = twice as fast - fan the hammer).")]
        [SerializeField, Range(0.1f, 1f)] float deadEyeFireIntervalMultiplier = 0.5f;
        [SerializeField] LayerMask hitMask = ~(1 << 2);

        [Header("Feel")]
        [SerializeField] float recoilPitch = 2.2f;
        [SerializeField] float recoilYawRandom = 0.6f;
        [SerializeField] float modelKickDistance = 0.07f;
        [SerializeField] float modelKickAngle = 12f;
        [SerializeField] float modelRecoverySpeed = 14f;

        [Header("Effects")]
        [SerializeField] Material tracerMaterial;
        [SerializeField] Material impactMaterial;

        PlayerInputReader input;
        PlayerLook look;
        PlayerMotor motor;
        float nextFireTime;
        Vector3 modelRestPosition;
        Quaternion modelRestRotation;
        float kick; // 0..1

        public int Ammo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }

        public event Action Fired;
        public event Action<HitResult> HitTarget;

        void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            look = GetComponentInParent<PlayerLook>();
            motor = GetComponentInParent<PlayerMotor>();
            if (aimCamera == null) aimCamera = GetComponentInParent<Camera>();
            Ammo = magazineSize;

            if (model != null)
            {
                modelRestPosition = model.localPosition;
                modelRestRotation = model.localRotation;
            }
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        void Update()
        {
            bool handsBusy = motor != null && (motor.IsMantling || motor.IsRolling);
            if (PlayerLook.CursorLocked && !handsBusy)
            {
                if (input.ReloadPressed && Ammo < magazineSize && !IsReloading)
                    StartCoroutine(Reload());

                if (input.AttackPressed && Time.time >= nextFireTime && !IsReloading)
                {
                    if (Ammo > 0) Fire();
                    else StartCoroutine(Reload());
                }
            }

            AnimateModel();
        }

        void Fire()
        {
            Ammo--;
            bool deadEye = motor != null && motor.IsDeadEye;
            nextFireTime = Time.time + fireInterval * (deadEye ? deadEyeFireIntervalMultiplier : 1f);
            kick = 1f;

            look?.AddRecoil(recoilPitch, UnityEngine.Random.Range(-recoilYawRandom, recoilYawRandom));
            if (muzzleFlash != null) StartCoroutine(Flash());

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 endPoint = ray.origin + ray.direction * range;

            motor?.TryRecoilHop(ray.direction);

            if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                ApplyHit(hit, ray.direction);
                ShotEffects.SpawnImpact(hit, impactMaterial);
            }

            if (muzzle != null)
                ShotEffects.SpawnTracer(muzzle.position, endPoint, tracerMaterial);

            Fired?.Invoke();

            if (Ammo == 0 && autoReloadWhenEmpty)
                StartCoroutine(Reload());
        }

        void ApplyHit(RaycastHit hit, Vector3 direction)
        {
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(direction * impactForce, hit.point, ForceMode.Impulse);

            IDamageable target;
            float multiplier = 1f;
            var hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null)
            {
                target = hitbox.Owner;
                multiplier = hitbox.DamageMultiplier;
            }
            else
            {
                target = hit.collider.GetComponentInParent<IDamageable>();
            }
            if (target == null) return;

            var info = new DamageInfo
            {
                Amount = damage * multiplier,
                Point = hit.point,
                Direction = direction,
                Source = gameObject,
                IsCritical = multiplier > 1f,
            };
            bool killed = target.TakeDamage(info);

            HitTarget?.Invoke(new HitResult
            {
                Point = hit.point,
                Damage = info.Amount,
                IsCritical = info.IsCritical,
                Killed = killed,
            });
        }

        IEnumerator Reload()
        {
            if (IsReloading) yield break;
            IsReloading = true;

            for (float t = 0f; t < reloadTime; t += Time.deltaTime)
            {
                ReloadProgress = t / reloadTime;
                yield return null;
            }

            Ammo = magazineSize;
            ReloadProgress = 0f;
            IsReloading = false;
        }

        IEnumerator Flash()
        {
            muzzleFlash.enabled = true;
            yield return new WaitForSeconds(0.04f);
            muzzleFlash.enabled = false;
        }

        void AnimateModel()
        {
            if (model == null) return;

            kick = Mathf.MoveTowards(kick, 0f, modelRecoverySpeed * Time.deltaTime * Mathf.Max(kick, 0.1f));

            // Reload: dip the gun and spin it once around its barrel axis.
            float dip = IsReloading ? Mathf.Sin(ReloadProgress * Mathf.PI) : 0f;
            float spin = IsReloading ? ReloadProgress * 360f : 0f;

            model.localPosition = modelRestPosition + new Vector3(0f, -0.12f * dip, -modelKickDistance * kick);
            model.localRotation = modelRestRotation
                                  * Quaternion.Euler(-modelKickAngle * kick + 25f * dip, 0f, 0f)
                                  * Quaternion.Euler(0f, 0f, spin);
        }
    }
}
