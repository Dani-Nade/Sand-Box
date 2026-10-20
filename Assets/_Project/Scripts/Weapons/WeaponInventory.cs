using System;
using System.Collections.Generic;
using SandHouse.Player;
using SandHouse.UI;
using UnityEngine;

namespace SandHouse.Weapons
{
    /// <summary>
    /// Holds the player's weapons and switches between them (1-4, scroll wheel, d-pad).
    /// The old weapon lowers out of view, then the new one raises. Re-broadcasts the current
    /// weapon's events so the HUD and weapon sway only need to listen here.
    /// </summary>
    public class WeaponInventory : MonoBehaviour
    {
        [SerializeField] List<Weapon> weapons = new List<Weapon>();
        [Tooltip("Seconds to lower the old weapon and raise the new one.")]
        [SerializeField] float switchTime = 0.3f;

        PlayerInputReader input;
        int currentIndex = -1;
        int targetIndex = -1;
        float holster;

        public IReadOnlyList<Weapon> Weapons => weapons;
        public int CurrentIndex => currentIndex;
        public Weapon Current => currentIndex >= 0 && currentIndex < weapons.Count ? weapons[currentIndex] : null;
        public bool IsSwitching => targetIndex != currentIndex;

        public event Action Fired;
        public event Action<HitResult> HitTarget;
        public event Action<Weapon> Switched;
        public event Action ShotSelf;

        void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].Fired += () => Fired?.Invoke();
                weapons[i].HitTarget += hit => HitTarget?.Invoke(hit);
                weapons[i].ShotSelf += () => ShotSelf?.Invoke();
                weapons[i].gameObject.SetActive(i == 0);
            }
            currentIndex = targetIndex = weapons.Count > 0 ? 0 : -1;
        }

        void Update()
        {
            if (weapons.Count == 0) return;
            if (input != null && !UIState.AnyMenuOpen) ReadInput();
            UpdateSwitch(Time.deltaTime);
        }

        void ReadInput()
        {
            for (int i = 0; i < input.WeaponSlotCount && i < weapons.Count; i++)
                if (input.WeaponSlotPressed(i)) Select(i);

            if (input.NextWeaponPressed) Select((targetIndex + 1) % weapons.Count);
            if (input.PreviousWeaponPressed) Select((targetIndex - 1 + weapons.Count) % weapons.Count);
        }

        public void Select(int index)
        {
            if (index >= 0 && index < weapons.Count) targetIndex = index;
        }

        void UpdateSwitch(float dt)
        {
            float speed = 2f / Mathf.Max(switchTime, 0.01f); // half the time lowering, half raising

            if (targetIndex != currentIndex)
            {
                holster = Mathf.MoveTowards(holster, 1f, speed * dt);
                if (holster >= 1f)
                {
                    weapons[currentIndex].gameObject.SetActive(false);
                    currentIndex = targetIndex;
                    weapons[currentIndex].Holster = 1f;
                    weapons[currentIndex].gameObject.SetActive(true);
                    Switched?.Invoke(weapons[currentIndex]);
                }
            }
            else
            {
                holster = Mathf.MoveTowards(holster, 0f, speed * dt);
            }

            if (Current != null) Current.Holster = holster;
        }
    }
}
