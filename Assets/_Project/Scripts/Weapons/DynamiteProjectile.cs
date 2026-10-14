using System;
using System.Collections.Generic;
using SandHouse.Combat;
using SandHouse.Player;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// A lit stick of dynamite: bounces around, then explodes when the fuse runs out.
    /// The blast damages everything with Health in range (not the thrower), shoves physics objects,
    /// and knocks the thrower around if they're close - dynamite jumping - without hurting them.
    /// </summary>
    public class DynamiteProjectile : MonoBehaviour
    {
        public struct Settings
        {
            public Vector3 Position, Velocity;
            public WeaponData Data;
            public GameObject Source;
            public PlayerMotor Owner;
            public float DamageMultiplier;
            public Material StickMaterial, FuseMaterial, ExplosionMaterial;
            public Action<HitResult> OnHit;
        }

        static readonly Collider[] BlastHits = new Collider[64];
        static readonly HashSet<Health> Damaged = new HashSet<Health>();

        Settings settings;
        Rigidbody body;
        Light spark;
        float fuse;

        public static void Spawn(Settings settings)
        {
            var go = new GameObject("Dynamite");
            go.transform.SetPositionAndRotation(settings.Position, UnityEngine.Random.rotation);

            // Placeholder visual: red stick with a dark fuse.
            AddPart(go.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.12f, 0.07f), settings.StickMaterial);
            AddPart(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.012f, 0.03f, 0.012f), settings.FuseMaterial);

            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = 0.045f;
            collider.height = 0.26f;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            body.linearVelocity = settings.Velocity;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 10f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (settings.Owner != null)
                foreach (var ownerCollider in settings.Owner.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(collider, ownerCollider);

            var spark = new GameObject("Spark").AddComponent<Light>();
            spark.transform.SetParent(go.transform, false);
            spark.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            spark.type = LightType.Point;
            spark.color = new Color(1f, 0.6f, 0.2f);
            spark.range = 2.5f;
            spark.shadows = LightShadows.None;

            var dynamite = go.AddComponent<DynamiteProjectile>();
            dynamite.settings = settings;
            dynamite.body = body;
            dynamite.spark = spark;
            dynamite.fuse = settings.Data.fuseTime;
        }

        static void AddPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            spark.intensity = 1.5f + 2f * Mathf.PerlinNoise(Time.time * 25f, 0f); // flickering fuse
            if (fuse <= 0f) Explode();
        }

        void Explode()
        {
            WeaponData data = settings.Data;
            Vector3 center = transform.position;
            float radius = data.explosionRadius;
            Health ownerHealth = settings.Owner != null ? settings.Owner.GetComponent<Health>() : null;

            int count = Physics.OverlapSphereNonAlloc(center, radius, BlastHits, ~0, QueryTriggerInteraction.Ignore);
            Damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider col = BlastHits[i];
                Rigidbody hitBody = col.attachedRigidbody;
                if (hitBody != null && hitBody != body && !hitBody.isKinematic)
                    hitBody.AddExplosionForce(data.explosionKnockback, center, radius, 1f, ForceMode.VelocityChange);

                Health health = col.GetComponentInParent<Health>();
                if (health == null || health == ownerHealth || !Damaged.Add(health)) continue;

                Vector3 point = col.ClosestPoint(center);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, point) / radius);
                float damage = data.damage * Mathf.Lerp(0.3f, 1f, falloff) * settings.DamageMultiplier;
                bool critical = falloff > 0.8f;
                bool killed = health.TakeDamage(new DamageInfo
                {
                    Amount = damage,
                    Point = point + Vector3.up * 0.5f,
                    Direction = (point - center).normalized,
                    Source = settings.Source,
                    IsCritical = critical,
                });
                settings.OnHit?.Invoke(new HitResult { Point = point, Damage = damage, IsCritical = critical, Killed = killed });
            }

            KnockBackOwner(center, radius);
            ExplosionEffect.Spawn(center, radius, settings.ExplosionMaterial);
            Destroy(gameObject);
        }

        void KnockBackOwner(Vector3 center, float radius)
        {
            PlayerMotor owner = settings.Owner;
            if (owner == null) return;

            Vector3 offset = owner.transform.position + Vector3.up * 0.9f - center;
            float distance = offset.magnitude;
            if (distance >= radius) return;

            float strength = Mathf.Lerp(0.4f, 1f, 1f - distance / radius);
            Vector3 direction = (offset.normalized + Vector3.up * 0.6f).normalized;
            owner.ApplyKnockback(direction * settings.Data.selfKnockback * strength);
        }
    }
}
