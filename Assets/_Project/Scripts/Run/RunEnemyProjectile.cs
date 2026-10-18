using System;
using SandHouse.Combat;
using SandHouse.UI;
using UnityEngine;

namespace SandHouse.Run
{
    /// <summary>Visible, dodgeable enemy bolt with swept collision. Cover blocks it.</summary>
    public class RunEnemyProjectile : MonoBehaviour
    {
        Vector3 velocity;
        float damage, life = 6f;

        public static void Spawn(Vector3 position, Vector3 target, Transform room, float speed, float damage, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Enemy Bolt"; go.transform.SetParent(room, true);
            go.transform.position = position; go.transform.localScale = Vector3.one * 0.23f;
            go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            var bolt = go.AddComponent<RunEnemyProjectile>();
            bolt.velocity = (target - position).normalized * speed; bolt.damage = damage;
        }

        void Update()
        {
            if (UIState.IsPaused || UIState.IsRunMenuOpen) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }
            Vector3 step = velocity * dt;
            var hits = Physics.RaycastAll(transform.position, velocity.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<RunEnemy>() != null) continue;
                var target = hit.collider.GetComponentInParent<Health>();
                if (target != null) target.TakeDamage(new DamageInfo { Amount = damage, Point = hit.point, Direction = velocity.normalized, Source = gameObject });
                Destroy(gameObject); return;
            }
            transform.position += step;
        }
    }
}
