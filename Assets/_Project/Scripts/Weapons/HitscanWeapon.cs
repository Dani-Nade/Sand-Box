using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Instant-hit guns: revolver, rifle, shotgun. Fires <see cref="WeaponData.pellets"/> rays inside a
    /// spread cone, with optional damage falloff over distance.
    /// </summary>
    public class HitscanWeapon : Weapon
    {
        protected override void Shoot(Ray aim)
        {
            Transform cam = aimCamera.transform;
            float spread = Mathf.Tan(data.spreadAngle * Mathf.Deg2Rad);

            for (int i = 0; i < data.pellets; i++)
            {
                Vector3 direction = aim.direction;
                if (spread > 0f)
                {
                    Vector2 offset = Random.insideUnitCircle * spread;
                    direction = (aim.direction + cam.right * offset.x + cam.up * offset.y).normalized;
                }

                Vector3 end = aim.origin + direction * data.range;
                if (Physics.Raycast(aim.origin, direction, out RaycastHit hit, data.range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    ApplyDamage(hit.collider, hit.rigidbody, hit.point, direction, data.damage * Falloff(hit.distance));
                    ShotEffects.SpawnImpact(hit, impactMaterial);
                }

                if (muzzle != null)
                    ShotEffects.SpawnTracer(muzzle.position, end, tracerMaterial);
            }
        }

        float Falloff(float distance)
        {
            if (data.falloffStart <= 0f || data.falloffEnd <= data.falloffStart) return 1f;
            float t = Mathf.InverseLerp(data.falloffStart, data.falloffEnd, distance);
            return Mathf.Lerp(1f, data.minDamageMultiplier, t);
        }
    }
}
