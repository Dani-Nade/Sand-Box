using SandHouse.Combat;
using SandHouse.UI;
using UnityEngine;

namespace SandHouse.Run
{
    /// <summary>Basic melee pursuer or ranged skirmisher. No navigation bake is needed for the prototype rooms.</summary>
    [RequireComponent(typeof(Health), typeof(CharacterController))]
    public class RunEnemy : MonoBehaviour
    {
        Transform target;
        Health health, targetHealth;
        CharacterController controller;
        Renderer body;
        Material projectileMaterial;
        bool ranged;
        int difficulty;
        float nextAttack, windupUntil = -1f, movePhase;
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static RunEnemy Spawn(Transform room, Vector3 position, Transform player, int difficulty, bool ranged, Material bodyMaterial, Material headMaterial)
        {
            var go = new GameObject(ranged ? "Ranged Skirmisher" : "Melee Pursuer");
            go.SetActive(false); go.transform.SetParent(room, false); go.transform.localPosition = position;
            var enemy = go.AddComponent<RunEnemy>();
            enemy.target = player; enemy.targetHealth = player.GetComponent<Health>();
            enemy.difficulty = difficulty; enemy.ranged = ranged; enemy.projectileMaterial = headMaterial;
            enemy.movePhase = position.x;
            var health = go.GetComponent<Health>(); health.SetMaxHealth(50f + difficulty * 15f);
            var controller = go.GetComponent<CharacterController>();
            controller.height = 1.35f; controller.radius = 0.38f; controller.center = Vector3.up * 0.68f;
            controller.stepOffset = 0.25f; controller.skinWidth = 0.03f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.up * 0.75f; body.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
            body.GetComponent<Collider>().enabled = false; Destroy(body.GetComponent<Collider>());
            enemy.body = body.GetComponent<Renderer>();
            if (bodyMaterial != null) enemy.body.sharedMaterial = bodyMaterial;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(go.transform, false);
            head.transform.localPosition = Vector3.up * 1.5f; head.transform.localScale = Vector3.one * 0.45f;
            if (headMaterial != null) head.GetComponent<Renderer>().sharedMaterial = headMaterial;
            head.AddComponent<Hitbox>().Configure(health, 2f);
            go.SetActive(true);
            return enemy;
        }

        void Awake()
        {
            health = GetComponent<Health>(); controller = GetComponent<CharacterController>();
            block = new MaterialPropertyBlock();
            nextAttack = Time.time + 1.5f;
        }
        void OnEnable() { health.Died += OnDied; health.Damaged += OnDamaged; }
        void OnDisable() { health.Died -= OnDied; health.Damaged -= OnDamaged; }
        void OnDied() => gameObject.SetActive(false);
        void OnDamaged(DamageInfo info) => DamageNumber.Spawn(info.Point, info.Amount, info.IsCritical, health.IsDead);

        void Update()
        {
            if (UIState.IsPaused || UIState.IsRunMenuOpen || target == null || targetHealth == null || targetHealth.IsDead || health.IsDead) return;
            Vector3 offset = target.position - transform.position;
            Vector3 flat = new Vector3(offset.x, 0f, offset.z);
            float distance = flat.magnitude;
            Vector3 direction = flat.normalized;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(direction);

            if (windupUntil > 0f)
            {
                Tint(Color.yellow);
                if (Time.time >= windupUntil)
                {
                    windupUntil = -1f; nextAttack = Time.time + (ranged ? 2f : 1.2f);
                    if (ranged)
                        RunEnemyProjectile.Spawn(transform.position + Vector3.up * 1.2f + direction * 0.65f,
                            target.position + Vector3.up, transform.parent, 8f + difficulty, 8f + difficulty * 2f, projectileMaterial);
                    else if (distance < 2f && Mathf.Abs(offset.y) < 1.8f && CanSeePlayer())
                        targetHealth.TakeDamage(new DamageInfo { Amount = 10f + difficulty * 2f, Source = gameObject, Point = target.position, Direction = direction });
                }
            }
            else
            {
                Tint(ranged ? new Color(0.6f, 0.35f, 1f) : new Color(1f, 0.3f, 0.2f));
                bool canAttack = ranged ? distance < 25f : distance < 1.65f && Mathf.Abs(offset.y) < 1.8f;
                if (Time.time >= nextAttack && canAttack && CanSeePlayer()) windupUntil = Time.time + 0.45f;
                else
                {
                    Vector3 move = direction;
                    if (ranged && distance < 10f)
                        move = Vector3.Cross(Vector3.up, direction) * Mathf.Sign(Mathf.Sin(Time.time * 0.6f + movePhase));
                    if (Blocked(move))
                    {
                        Vector3 left = Quaternion.Euler(0f, -70f, 0f) * move;
                        Vector3 right = Quaternion.Euler(0f, 70f, 0f) * move;
                        move = !Blocked(left) ? left : !Blocked(right) ? right : Vector3.zero;
                    }
                    controller.Move((move * (ranged ? 2f : 3.2f + difficulty * 0.3f) + Vector3.down * 5f) * Time.deltaTime);
                }
            }
        }

        bool CanSeePlayer()
        {
            Vector3 start = transform.position + Vector3.up * 1.2f;
            Vector3 offset = target.position + Vector3.up - start;
            foreach (var hit in Physics.RaycastAll(start, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
            return true;
        }

        bool Blocked(Vector3 direction)
        {
            foreach (var hit in Physics.SphereCastAll(transform.position + Vector3.up * 0.7f, 0.4f, direction, 1.2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y < 0.5f) return true;
            return false;
        }

        void Tint(Color color)
        {
            if (body == null) return;
            block.SetColor(BaseColor, color); block.SetColor(ColorId, color);
            body.SetPropertyBlock(block);
        }
    }
}
