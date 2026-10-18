using SandHouse.Settings;
using SandHouse.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SandHouse.Player
{
    /// <summary>
    /// Mouse / right-stick look. Yaw rotates the player body, pitch rotates the camera pivot.
    /// Mouse sensitivity and invert Y come from <see cref="GameSettings"/>.
    /// Also owns weapon recoil so it can be applied on top of the player's aim.
    /// The cursor is locked during play and freed while any menu is open (see UIState).
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] float gamepadSensitivity = 200f;
        [SerializeField] float maxPitch = 89f;

        [Header("Recoil")]
        [Tooltip("How fast recoil returns to the original aim.")]
        [SerializeField] float recoilRecoverySpeed = 12f;

        PlayerInputReader input;
        float pitch;
        float recoilPitch;
        float recoilYaw;

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        void Awake() => input = GetComponent<PlayerInputReader>();

        void OnEnable() => UIState.Changed += OnMenusChanged;
        void OnDisable() => UIState.Changed -= OnMenusChanged;

        void Start() => LockCursor(!UIState.AnyMenuOpen);

        void OnMenusChanged() => LockCursor(!UIState.AnyMenuOpen);

        void Update()
        {
            if (UIState.AnyMenuOpen) return;

            // Clicking back into the game window re-captures the mouse (e.g. after alt-tabbing).
            if (!CursorLocked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
                return;
            }

            Vector2 delta = input.Look;
            if (input.LookIsGamepad)
            {
                // Unscaled so aiming stays at normal speed during Dead Eye slow motion.
                delta *= gamepadSensitivity * Time.unscaledDeltaTime;
            }
            else
            {
                delta *= GameSettings.MouseSensitivity;
            }
            if (GameSettings.InvertY) delta.y = -delta.y;

            transform.Rotate(0f, delta.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);

            float t = 1f - Mathf.Exp(-recoilRecoverySpeed * Time.deltaTime);
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, t);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, t);

            float finalPitch = Mathf.Clamp(pitch - recoilPitch, -maxPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(finalPitch, recoilYaw, 0f);
        }

        /// <summary>Kicks the view up (and optionally sideways). Recovers automatically.</summary>
        public void AddRecoil(float verticalDegrees, float horizontalDegrees = 0f)
        {
            recoilPitch += verticalDegrees;
            recoilYaw += horizontalDegrees;
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
