using UnityEngine;
using UnityEngine.InputSystem;

namespace SandHouse.Player
{
    /// <summary>
    /// Reads the project-wide input actions (Assets/InputSystem_Actions) so other
    /// player scripts don't touch the Input System directly.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        InputActionMap map;
        InputAction move, look, jump, dash, crouch, attack, reload, lasso, deadEye, stats, nextWeapon, previousWeapon;
        readonly InputAction[] weaponSlots = new InputAction[4];

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool LookIsGamepad => look.activeControl?.device is Gamepad;

        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool JumpHeld => jump.IsPressed();
        public bool DashPressed => dash.WasPressedThisFrame();
        public bool CrouchHeld => crouch.IsPressed();
        public bool CrouchPressed => crouch.WasPressedThisFrame();
        public bool CrouchReleased => crouch.WasReleasedThisFrame();
        public bool AttackPressed => attack.WasPressedThisFrame();
        public bool AttackHeld => attack.IsPressed();
        public bool ReloadPressed => reload.WasPressedThisFrame();
        public bool LassoPressed => lasso.WasPressedThisFrame();
        public bool LassoHeld => lasso.IsPressed();
        public bool DeadEyePressed => deadEye.WasPressedThisFrame();
        public bool StatsHeld => stats.IsPressed();
        public bool NextWeaponPressed => nextWeapon.WasPressedThisFrame();
        public bool PreviousWeaponPressed => previousWeapon.WasPressedThisFrame();
        public int WeaponSlotCount => weaponSlots.Length;

        /// <summary>True on the frame the key for weapon slot <paramref name="index"/> (0-based) is pressed.</summary>
        public bool WeaponSlotPressed(int index) => weaponSlots[index].WasPressedThisFrame();

        void Awake()
        {
            Settings.GameSettings.LoadBindings(); // apply the player's saved key bindings
            map = InputSystem.actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            dash = map.FindAction("Dash", true);
            crouch = map.FindAction("Crouch", true);
            attack = map.FindAction("Attack", true);
            reload = map.FindAction("Reload", true);
            lasso = map.FindAction("Lasso", true);
            deadEye = map.FindAction("DeadEye", true);
            stats = map.FindAction("Stats", true);
            nextWeapon = map.FindAction("Next", true);
            previousWeapon = map.FindAction("Previous", true);
            for (int i = 0; i < weaponSlots.Length; i++)
                weaponSlots[i] = map.FindAction($"Weapon{i + 1}", true);
        }

        void OnEnable() => map?.Enable();
    }
}
