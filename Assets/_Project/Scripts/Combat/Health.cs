using System;
using UnityEngine;

namespace SandHouse.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        void Awake() => Current = maxHealth;

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead) return false;

            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(info);

            if (!IsDead) return false;
            Died?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void ResetHealth() => Current = maxHealth;
    }
}
