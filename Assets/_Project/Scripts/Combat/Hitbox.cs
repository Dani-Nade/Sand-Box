using UnityEngine;

namespace SandHouse.Combat
{
    /// <summary>
    /// Put on a collider to route hits to a Health with a damage multiplier
    /// (e.g. 2x on a head collider for headshots).
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] Health owner;
        [SerializeField, Min(0)] float damageMultiplier = 1f;

        public Health Owner => owner != null ? owner : owner = GetComponentInParent<Health>();
        public float DamageMultiplier => damageMultiplier;
        public bool IsCritical => damageMultiplier > 1f;

        public void Configure(Health health, float multiplier)
        {
            owner = health;
            damageMultiplier = multiplier;
        }
    }
}
