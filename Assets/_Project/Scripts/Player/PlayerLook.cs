using UnityEngine;
using UnityEngine.InputSystem;

namespace SandHouse.Player
{
    /// <summary>
    /// Mouse / right-stick look. Yaw rotates the player body, pitch rotates the camera pivot.
    /// Also owns weapon recoil so it can be applied on top of the player's aim.
    /// Esc frees the cursor, clicking the game view locks it again.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.1f;
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

        void Start() => LockCursor(true);

        void Update()
        {
            HandleCursor();
            if (!CursorLocked) return;

            Vector2 delta = input.Look;
            // Unscaled so aiming stays at normal speed during Dead Eye slow motion.
            delta *= input.LookIsGamepad ? gamepadSensitivity * Time.unscaledDeltaTime : mouseSensitivity;

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

        void HandleCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            else if (!CursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
