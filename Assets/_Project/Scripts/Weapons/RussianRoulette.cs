using System;
using SandHouse.Stats;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Russian roulette for the revolver, on top of the Weapon's aim-at-self.
    /// With the gun at your head: Reload spins the cylinder (your loaded rounds end up in random
    /// chambers), Self Shot pulls the trigger. An empty chamber clicks, you survive and - if you spun
    /// first - earn a stat point. A loaded one kills you as normal.
    /// Firing normally or reloading puts the rounds back "in order", so until you spin, the chamber
    /// under the hammer is simply loaded whenever there's ammo.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class RussianRoulette : MonoBehaviour
    {
        [Tooltip("Stat points for surviving a pull right after a spin.")]
        [SerializeField] int pointsPerSurvival = 1;
        [Tooltip("Extra full turns of the cylinder when spinning (for show).")]
        [SerializeField] int spinTurns = 3;
        [SerializeField] float spinKick = 2400f;

        Weapon weapon;
        PlayerStats stats;
        bool[] chambers = Array.Empty<bool>();
        int hammer;          // chamber under the hammer
        bool spun;           // chambers are randomised
        bool rewardReady;    // a fresh spin since the last pull

        public bool IsSpun => spun;
        public int Chambers => weapon != null ? weapon.MagazineSize : 0;
        public int LoadedRounds => weapon != null ? weapon.Ammo : 0;

        /// <summary>Spun the cylinder (loaded rounds, chambers).</summary>
        public event Action<int, int> Spun;
        /// <summary>Survived a pull (true if it paid out a stat point).</summary>
        public event Action<bool> Survived;
        /// <summary>Tried to spin with nothing loaded.</summary>
        public event Action NeedsAmmo;

        void Awake()
        {
            weapon = GetComponent<Weapon>();
            stats = GetComponentInParent<PlayerStats>();
        }

        void OnEnable()
        {
            weapon.Fired += Unspin;
            weapon.ReloadOpened += OnReloadOpened;
        }

        void OnDisable()
        {
            weapon.Fired -= Unspin;
            weapon.ReloadOpened -= OnReloadOpened;
            Unspin();
        }

        void OnReloadOpened(int spent) => Unspin();

        void Unspin()
        {
            spun = false;
            rewardReady = false;
        }

        public void Spin()
        {
            int size = weapon.MagazineSize, loaded = weapon.Ammo;
            if (loaded <= 0)
            {
                NeedsAmmo?.Invoke();
                return;
            }

            // Scatter the loaded rounds across the chambers and land on a random one.
            chambers = new bool[size];
            for (int i = 0; i < loaded && i < size; i++) chambers[i] = true;
            for (int i = size - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (chambers[i], chambers[j]) = (chambers[j], chambers[i]);
            }
            int landed = UnityEngine.Random.Range(0, size);
            hammer = landed;
            spun = true;
            rewardReady = true;

            weapon.AddSpin(360f * spinTurns + landed * (360f / size), spinKick);
            Spun?.Invoke(loaded, size);
        }

        /// <summary>Returns true if the chamber under the hammer is loaded (the Weapon then fires).</summary>
        public bool PullTrigger()
        {
            if (!spun) return weapon.Ammo > 0;

            bool live = chambers.Length > 0 && chambers[hammer];
            if (live)
            {
                chambers[hammer] = false;
                Unspin();
                return true;
            }

            bool paid = rewardReady && stats != null && pointsPerSurvival > 0;
            if (paid) stats.AddPoints(pointsPerSurvival);
            rewardReady = false;
            hammer = (hammer + 1) % chambers.Length; // next chamber comes round
            Survived?.Invoke(paid);
            return false;
        }

        /// <summary>Chance (0..1) the next pull is live, as far as the player can know.</summary>
        public float Odds
        {
            get
            {
                if (weapon == null || weapon.MagazineSize == 0) return 0f;
                return spun ? (float)weapon.Ammo / weapon.MagazineSize : weapon.Ammo > 0 ? 1f : 0f;
            }
        }
    }
}
