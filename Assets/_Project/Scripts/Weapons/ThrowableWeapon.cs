using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Thrown explosives (dynamite). Each "shot" throws a lit stick that bounces around and explodes
    /// when its fuse runs out. Damage, radius and fuse come from the WeaponData's Throwable section.
    /// </summary>
    public class ThrowableWeapon : Weapon
    {
        protected override bool SupportsSelfAim => false;

        [Header("Throwable Visuals")]
        [SerializeField] Material stickMaterial;
        [SerializeField] Material fuseMaterial;
        [SerializeField] Material explosionMaterial;

        protected override void Shoot(Ray aim)
        {
            Vector3 spawn = aim.origin + aim.direction * 0.7f + Vector3.down * 0.1f;
            Vector3 velocity = aim.direction * data.throwSpeed + Vector3.up * data.throwUpSpeed;
            if (motor != null) velocity += motor.Velocity * 0.5f; // inherit some of your momentum

            DynamiteProjectile.Spawn(new DynamiteProjectile.Settings
            {
                Position = spawn,
                Velocity = velocity,
                Data = data,
                Source = gameObject,
                Owner = motor,
                DamageMultiplier = StatDamageMultiplier,
                StickMaterial = stickMaterial,
                FuseMaterial = fuseMaterial,
                ExplosionMaterial = explosionMaterial,
                OnHit = ReportHit,
            });
        }
    }
}
