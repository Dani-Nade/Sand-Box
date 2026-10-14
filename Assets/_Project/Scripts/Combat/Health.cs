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

        /// <summary>Fires whenever anything with a Health dies, with the hit that killed it (used for kill rewards).</summary>
        public static event Action<Health, DamageInfo> AnyKilled;

        void Awake() => Current = maxHealth;

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead) return false;

            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(info);

            if (!IsDead) return false;
            Died?.Invoke();
            AnyKilled?.Invoke(this, info);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void ResetHealth() => Current = maxHealth;

        /// <summary>Changes max health. Raising it also heals by the amount gained.</summary>
        public void SetMaxHealth(float newMax)
        {
            newMax = Mathf.Max(1f, newMax);
            float gained = newMax - maxHealth;
            maxHealth = newMax;
            Current = Mathf.Clamp(Current + Mathf.Max(0f, gained), 0f, maxHealth);
        }
    }
}
