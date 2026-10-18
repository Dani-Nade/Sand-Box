using System;
using SandHouse.Combat;
using UnityEngine;

namespace SandHouse.Stats
{
    public enum GunUpgradeType
    {
        RapidFire, HeavyRounds, Multishot, Ricochet, PiercingRounds,
        ExtendedMagazine, QuickReload, ExplosiveRounds, ArcaneRounds, VampiricRounds,
    }

    /// <summary>Run-local, player-owned gun upgrades. Never modifies shared WeaponData assets.</summary>
    [DisallowMultipleComponent]
    public class GunUpgrades : MonoBehaviour
    {
        public const int Count = 10;
        public const float RicochetRetention = 0.75f;
        public const float ExplosionRadius = 2.5f;
        readonly int[] stacks = new int[Count];

        public event Action Changed;
        public float FireRateMultiplier => 1f + 0.15f * GetStacks(GunUpgradeType.RapidFire);
        public float DamageMultiplier => 1f + 0.2f * GetStacks(GunUpgradeType.HeavyRounds);
        public int ExtraBullets => GetStacks(GunUpgradeType.Multishot);
        public int Bounces => GetStacks(GunUpgradeType.Ricochet);
        public int Pierces => GetStacks(GunUpgradeType.PiercingRounds);
        public float MagazineMultiplier => 1f + 0.25f * GetStacks(GunUpgradeType.ExtendedMagazine);
        public float ReloadTimeMultiplier => Mathf.Pow(0.8f, GetStacks(GunUpgradeType.QuickReload));
        public float ExplosionDamageFraction => 0.3f * GetStacks(GunUpgradeType.ExplosiveRounds);
        public float MagicDamageFraction => 0.2f * GetStacks(GunUpgradeType.ArcaneRounds);
        public float KillHealFraction => 0.02f * GetStacks(GunUpgradeType.VampiricRounds);

        public int GetStacks(GunUpgradeType type) => IsValid(type) ? stacks[(int)type] : 0;
        static bool IsValid(GunUpgradeType type) => (int)type >= 0 && (int)type < Count;

        public static int MaxStacks(GunUpgradeType type)
        {
            switch (type)
            {
                case GunUpgradeType.Multishot:
                case GunUpgradeType.Ricochet:
                case GunUpgradeType.ExplosiveRounds: return 3;
                case GunUpgradeType.PiercingRounds:
                case GunUpgradeType.QuickReload:
                case GunUpgradeType.ArcaneRounds:
                case GunUpgradeType.VampiricRounds: return 5;
                case GunUpgradeType.ExtendedMagazine: return 8;
                case GunUpgradeType.RapidFire:
                case GunUpgradeType.HeavyRounds: return 10;
                default: return 0;
            }
        }

        public bool CanGrant(GunUpgradeType type) => IsValid(type) && GetStacks(type) < MaxStacks(type);
        /// <summary>Awarded by the level-clear draft, never bought with stat points.</summary>
        public bool TryGrant(GunUpgradeType type)
        {
            if (!CanGrant(type)) return false;
            stacks[(int)type]++;
            Changed?.Invoke();
            return true;
        }

        public void ResetRun()
        {
            Array.Clear(stacks, 0, stacks.Length);
            Changed?.Invoke();
        }

        public void HealOnGunKill()
        {
            var health = GetComponent<Health>();
            if (health != null && KillHealFraction > 0f)
                health.Heal(health.Max * KillHealFraction);
        }

        public static string DisplayName(GunUpgradeType type)
        {
            switch (type)
            {
                case GunUpgradeType.RapidFire: return "Rapid Fire";
                case GunUpgradeType.HeavyRounds: return "Heavy Rounds";
                case GunUpgradeType.Multishot: return "Multishot";
                case GunUpgradeType.Ricochet: return "Ricochet";
                case GunUpgradeType.PiercingRounds: return "Piercing Rounds";
                case GunUpgradeType.ExtendedMagazine: return "Extended Magazine";
                case GunUpgradeType.QuickReload: return "Quick Reload";
                case GunUpgradeType.ExplosiveRounds: return "Explosive Rounds";
                case GunUpgradeType.ArcaneRounds: return "Arcane Rounds";
                case GunUpgradeType.VampiricRounds: return "Vampiric Rounds";
                default: return "Unknown";
            }
        }

        public static string Description(GunUpgradeType type)
        {
            switch (type)
            {
                case GunUpgradeType.RapidFire: return "+15% fire rate per stack";
                case GunUpgradeType.HeavyRounds: return "+20% gun damage per stack";
                case GunUpgradeType.Multishot: return "+1 bullet per shot; wider spread; no extra ammo cost";
                case GunUpgradeType.Ricochet: return "+1 wall bounce; retains 75% damage each bounce";
                case GunUpgradeType.PiercingRounds: return "+1 enemy pierced per bullet";
                case GunUpgradeType.ExtendedMagazine: return "+25% magazine capacity per stack (rounded up)";
                case GunUpgradeType.QuickReload: return "20% shorter reloads per stack";
                case GunUpgradeType.ExplosiveRounds: return "Impact blast: 30% gun damage per stack; 2.5m radius";
                case GunUpgradeType.ArcaneRounds: return "+20% base bullet damage per stack, scaled by MAGIC DMG";
                case GunUpgradeType.VampiricRounds: return "Gun kills heal 2% of maximum health per stack";
                default: return "";
            }
        }
    }
}
