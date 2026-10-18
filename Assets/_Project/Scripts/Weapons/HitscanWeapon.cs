using System;
using System.Collections.Generic;
using SandHouse.Combat;
using SandHouse.Stats;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Instant-hit guns: revolver, rifle, shotgun. Fires <see cref="WeaponData.pellets"/> rays inside a
    /// spread cone, with optional damage falloff over distance.
    /// </summary>
    public class HitscanWeapon : Weapon
    {
        const float SurfaceOffset = 0.01f;
        protected override bool SupportsGunUpgrades => true;

        protected override void Shoot(Ray aim)
        {
            Transform cam = aimCamera.transform;
            int extra = Upgrades != null ? Upgrades.ExtraBullets : 0;
            float spread = Mathf.Tan((data.spreadAngle + extra * 2f) * Mathf.Deg2Rad);

            for (int i = 0; i < Mathf.Max(1, data.pellets) + extra; i++)
            {
                Vector3 direction = aim.direction;
                if (spread > 0f)
                {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * spread;
                    direction = (aim.direction + cam.right * offset.x + cam.up * offset.y).normalized;
                }

                TraceBullet(aim.origin, direction);
            }
        }

        void TraceBullet(Vector3 origin, Vector3 direction)
        {
            int bounces = Upgrades != null ? Upgrades.Bounces : 0;
            int pierces = Upgrades != null ? Upgrades.Pierces : 0;
            float remaining = data.range, travelled = 0f, retained = 1f;
            Vector3 tracerStart = muzzle != null ? muzzle.position : origin;
            // One damage application per enemy per bullet, including enemies with multiple hitboxes.
            var damaged = new HashSet<IDamageable>();

            while (remaining > SurfaceOffset)
            {
                var hits = Physics.RaycastAll(origin, direction, remaining, hitMask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool reflected = false;
                foreach (var hit in hits)
                {
                    if (IsOwner(hit.collider)) continue;
                    var target = DamageTarget(hit.collider);
                    if (target != null && !damaged.Add(target)) continue;

                    float bulletDamage = data.damage * Falloff(travelled + hit.distance) * retained;
                    if (target != null)
                    {
                        float magic = Upgrades != null ? bulletDamage * Upgrades.MagicDamageFraction : 0f;
                        ApplyDamage(hit.collider, hit.rigidbody, hit.point, direction, bulletDamage, magic);
                    }
                    else
                    {
                        // Keep physics impact force on non-damageable props too.
                        ApplyDamage(hit.collider, hit.rigidbody, hit.point, direction, bulletDamage);
                    }
                    ShotEffects.SpawnImpact(hit, impactMaterial);
                    ShotEffects.SpawnTracer(tracerStart, hit.point, tracerMaterial);
                    tracerStart = hit.point;

                    if (target != null && pierces > 0)
                    {
                        pierces--;
                        continue;
                    }

                    // A single non-chaining blast at the bullet's terminal impact (not every pierced hit).
                    if (target != null || bounces <= 0)
                    {
                        ExplodeBullet(hit.point + hit.normal * SurfaceOffset, bulletDamage);
                        return;
                    }

                    bounces--;
                    retained *= GunUpgrades.RicochetRetention;
                    float step = hit.distance + SurfaceOffset;
                    travelled += step;
                    remaining -= step;
                    direction = Vector3.Reflect(direction, hit.normal).normalized;
                    origin = hit.point + hit.normal * SurfaceOffset;
                    tracerStart = origin;
                    reflected = true;
                    break;
                }
                if (reflected) continue;
                ShotEffects.SpawnTracer(tracerStart, origin + direction * remaining, tracerMaterial);
                return;
            }
        }

        bool IsOwner(Collider collider) => stats != null ? collider.transform.IsChildOf(stats.transform)
            : motor != null && collider.transform.IsChildOf(motor.transform);

        void ExplodeBullet(Vector3 center, float bulletDamage)
        {
            if (Upgrades == null || Upgrades.ExplosionDamageFraction <= 0f) return;
            float radius = GunUpgrades.ExplosionRadius;
            var damaged = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (IsOwner(col)) continue;
                var target = DamageTarget(col);
                if (target == null || damaged.Contains(target)) continue;
                Vector3 point = col.ClosestPoint(center);
                Vector3 offset = point - center;
                if (!BlastVisible(center, point, target)) continue;
                damaged.Add(target);
                // Splash is body damage, not a second headshot multiplier. No arcane/splash recursion.
                float falloff = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(offset.magnitude / radius));
                float damage = bulletDamage * Upgrades.ExplosionDamageFraction * StatDamageMultiplier * falloff;
                if (target is Health health && health.IsDead) continue;
                bool killed = target.TakeDamage(new DamageInfo
                {
                    Amount = damage, Point = point, Direction = offset.normalized, Source = gameObject,
                });
                ReportHit(new HitResult { Point = point, Damage = damage, Killed = killed });
            }
            ExplosionEffect.Spawn(center, radius, impactMaterial);
        }

        bool BlastVisible(Vector3 center, Vector3 point, IDamageable target)
        {
            Vector3 offset = point - center;
            if (offset.sqrMagnitude < 0.0001f) return true;
            foreach (var hit in Physics.RaycastAll(center, offset.normalized, offset.magnitude, hitMask, QueryTriggerInteraction.Ignore))
                if (!IsOwner(hit.collider) && DamageTarget(hit.collider) != target) return false;
            return true;
        }

        float Falloff(float distance)
        {
            if (data.falloffStart <= 0f || data.falloffEnd <= data.falloffStart) return 1f;
            float t = Mathf.InverseLerp(data.falloffStart, data.falloffEnd, distance);
            return Mathf.Lerp(1f, data.minDamageMultiplier, t);
        }
    }
}
