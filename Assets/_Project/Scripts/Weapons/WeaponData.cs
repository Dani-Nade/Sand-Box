using UnityEngine;

namespace SandHouse.Weapons
{
    public enum ReloadStyle
    {
        /// <summary>Gun dips and spins around the barrel (revolver).</summary>
        Spin,
        /// <summary>Gun tips down like breaking open a shotgun.</summary>
        BreakOpen,
        /// <summary>Small dip per round loaded (tube-fed rifle).</summary>
        Dip,
        /// <summary>Hand drops out of view to grab more (throwables).</summary>
        Lower,
    }

    /// <summary>
    /// All the numbers that define a weapon. One asset per gun (Assets/_Project/Data/Weapons).
    /// Edit these in the Inspector to tune guns without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Sand House/Weapon Data", fileName = "WD_NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Info")]
        public string displayName = "Weapon";
        [TextArea] public string description;

        [Header("Firing")]
        [Tooltip("Hold to keep firing. Off = one shot per click.")]
        public bool automatic;
        [Tooltip("Minimum seconds between shots.")]
        public float fireInterval = 0.22f;
        [Tooltip("Damage per pellet (per shot for single-pellet guns, per explosion for throwables).")]
        public float damage = 25f;
        [Min(1)] public int pellets = 1;
        [Tooltip("Random spread cone in degrees (0 = perfectly accurate).")]
        public float spreadAngle;
        public float range = 200f;
        [Tooltip("Push applied to physics objects that get hit.")]
        public float impactForce = 6f;

        [Header("Damage Falloff (0 = none)")]
        [Tooltip("Damage starts dropping at this distance.")]
        public float falloffStart;
        [Tooltip("Damage reaches its minimum at this distance.")]
        public float falloffEnd;
        [Range(0f, 1f)] public float minDamageMultiplier = 0.3f;

        [Header("Ammo")]
        [Min(1)] public int magazineSize = 6;
        [Tooltip("Load one round at a time (you can shoot to interrupt). Off = whole magazine at once.")]
        public bool reloadOneAtATime;
        [Tooltip("Whole-magazine reload time.")]
        public float reloadTime = 1.2f;
        [Tooltip("Time per round when reloading one at a time.")]
        public float roundReloadTime = 0.45f;
        public bool autoReloadWhenEmpty = true;
        public ReloadStyle reloadStyle = ReloadStyle.Spin;

        [Header("Cycling")]
        [Tooltip("Lever/pump action: seconds after each shot spent cycling (animates the 'Lever' part). 0 = none.")]
        public float cycleTime;

        [Header("Recoil")]
        public float recoilPitch = 2.2f;
        public float recoilYawRandom = 0.6f;
        public float modelKickDistance = 0.07f;
        public float modelKickAngle = 12f;
        public float modelRecoverySpeed = 14f;

        [Header("Movement")]
        [Tooltip("Recoil hop strength when firing in the air (0 = none, 2 = shotgun jump).")]
        public float recoilHopMultiplier = 1f;

        [Header("Dead Eye")]
        [Tooltip("Fire interval multiplier during Dead Eye (0.5 = twice as fast).")]
        [Range(0.1f, 1f)] public float deadEyeFireIntervalMultiplier = 0.5f;

        [Header("Throwable (only used by throwable weapons)")]
        public float throwSpeed = 18f;
        public float throwUpSpeed = 4f;
        [Tooltip("Seconds before it explodes.")]
        public float fuseTime = 1.6f;
        public float explosionRadius = 5f;
        [Tooltip("Push given to physics objects in the blast.")]
        public float explosionKnockback = 14f;
        [Tooltip("Push given to YOU if you're in your own blast (dynamite jumping). No self-damage.")]
        public float selfKnockback = 13f;
    }
}
