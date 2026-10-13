using UnityEngine;

namespace SandHouse.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        public bool IsCritical;
    }

    public interface IDamageable
    {
        /// <summary>Applies damage. Returns true if this hit killed the target.</summary>
        bool TakeDamage(DamageInfo info);
    }
}
