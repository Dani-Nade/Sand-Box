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
        [Tooltip("Optional action asset override. Leave empty to use the project's input actions.")]
        [SerializeField] InputActionAsset actionAsset;
        InputActionMap map;
        InputAction move, look, jump, dash, crouch, attack, reload, lasso, deadEye, stats, nextWeapon, previousWeapon;
        readonly InputAction[] weaponSlots = new InputAction[4];
        bool movementNeedsNeutral = true;
        bool hasInputFocus = true;

        public Vector2 Move
        {
            get
            {
                if (move == null || !isActiveAndEnabled || !hasInputFocus) return Vector2.zero;
                Vector2 value = move.ReadValue<Vector2>();
                if (movementNeedsNeutral)
                {
                    if (value.sqrMagnitude > 0.0001f) return Vector2.zero;
                    movementNeedsNeutral = false;
                }
                return value;
            }
        }
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
            var actions = actionAsset != null ? actionAsset : InputSystem.actions;
            map = actions.FindActionMap("Player", true);
            // The template also binds every generic joystick / XR axis. Unrelated HID devices
            // can report a resting full-back axis and win over WASD's input magnitude.
            // This desktop game supports keyboard/mouse and gamepads, not those template inputs.
            map.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
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

        void OnEnable()
        {
            // A held/stale direction from the editor or a previous session must not start walking.
            movementNeedsNeutral = true;
            map?.Enable();
        }

        void Update()
        {
            // Observe neutral even when the motor is paused or no movement getter runs this frame.
            if (movementNeedsNeutral && hasInputFocus && move != null && move.ReadValue<Vector2>().sqrMagnitude <= 0.0001f)
                movementNeedsNeutral = false;
        }

        void OnDisable() => movementNeedsNeutral = true;

        void OnApplicationFocus(bool focused)
        {
            hasInputFocus = focused;
            movementNeedsNeutral = true;
        }
    }
}
