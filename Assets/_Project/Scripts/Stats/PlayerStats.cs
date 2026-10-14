using System;
using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Stats
{
    public enum StatType
    {
        WeaponDamage,
        MagicDamage,
        Health,
    }

    /// <summary>
    /// Roguelite stat investment: earn points (per kill), spend them to level up
    /// weapon damage, magic damage and max health.
    /// Other systems read the multipliers: weapons use <see cref="WeaponDamageMultiplier"/>,
    /// abilities use <see cref="MagicDamageMultiplier"/> (the ground slam, until we add real magic).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Points")]
        [SerializeField] int startingPoints = 3;
        [SerializeField] int pointsPerKill = 1;
        [SerializeField, Min(1)] int maxLevel = 10;

        [Header("Per Level")]
        [Tooltip("+10% weapon damage per level.")]
        [SerializeField] float weaponDamagePerLevel = 0.1f;
        [Tooltip("+15% magic/ability damage per level.")]
        [SerializeField] float magicDamagePerLevel = 0.15f;
        [Tooltip("Max health added per level.")]
        [SerializeField] float healthPerLevel = 20f;

        static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

        readonly int[] levels = new int[StatCount];
        Health health;
        float baseMaxHealth;

        public int UnspentPoints { get; private set; }
        public int MaxLevel => maxLevel;
        public float WeaponDamageMultiplier => 1f + GetLevel(StatType.WeaponDamage) * weaponDamagePerLevel;
        public float MagicDamageMultiplier => 1f + GetLevel(StatType.MagicDamage) * magicDamagePerLevel;
        public float BonusHealth => GetLevel(StatType.Health) * healthPerLevel;

        public event Action Changed;

        void Awake()
        {
            health = GetComponent<Health>();
            UnspentPoints = startingPoints;
        }

        void Start() => baseMaxHealth = health.Max;

        void OnEnable() => Health.AnyKilled += OnAnyKilled;
        void OnDisable() => Health.AnyKilled -= OnAnyKilled;

        void OnAnyKilled(Health victim, DamageInfo killingBlow)
        {
            if (victim == health || killingBlow.Source == null) return;
            if (killingBlow.Source.transform.IsChildOf(transform))
                AddPoints(pointsPerKill);
        }

        public int GetLevel(StatType stat) => levels[(int)stat];

        public bool CanInvest(StatType stat) => UnspentPoints > 0 && GetLevel(stat) < maxLevel;

        public bool Invest(StatType stat)
        {
            if (!CanInvest(stat)) return false;

            levels[(int)stat]++;
            UnspentPoints--;
            if (stat == StatType.Health)
                health.SetMaxHealth(baseMaxHealth + BonusHealth);

            Changed?.Invoke();
            return true;
        }

        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            UnspentPoints += amount;
            Changed?.Invoke();
        }

        /// <summary>Short description of what the stat currently gives, for the UI.</summary>
        public string Describe(StatType stat)
        {
            switch (stat)
            {
                case StatType.WeaponDamage: return $"+{(WeaponDamageMultiplier - 1f) * 100f:0}%";
                case StatType.MagicDamage: return $"+{(MagicDamageMultiplier - 1f) * 100f:0}%";
                case StatType.Health: return $"{health.Max:0} HP";
                default: return "";
            }
        }
    }
}
