using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SandHouse.Settings
{
    /// <summary>
    /// Player-facing settings, saved between sessions (PlayerPrefs).
    /// Gameplay code reads these instead of hard-coding values; listen to <see cref="Changed"/> to react.
    /// Key bindings are stored as Input System binding overrides.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultMouseSensitivity = 0.1f;
        public const float MinMouseSensitivity = 0.01f;
        public const float MaxMouseSensitivity = 0.5f;
        public const float DefaultFieldOfView = 80f;
        public const float MinFieldOfView = 60f;
        public const float MaxFieldOfView = 110f;

        const string Prefix = "sandhouse.settings.";
        const string SensitivityKey = Prefix + "mouseSensitivity";
        const string InvertYKey = Prefix + "invertY";
        const string FovKey = Prefix + "fieldOfView";
        const string CameraMotionKey = Prefix + "cameraMotion";
        const string VolumeKey = Prefix + "masterVolume";
        const string BindingsKey = Prefix + "bindings";

        static bool bindingsLoaded;

        public static float MouseSensitivity { get; private set; } = DefaultMouseSensitivity;
        public static bool InvertY { get; private set; }
        public static float FieldOfView { get; private set; } = DefaultFieldOfView;
        /// <summary>0..1 scale for head bob, tilt, flips and FOV kicks (motion-sickness option).</summary>
        public static float CameraMotion { get; private set; } = 1f;
        public static float MasterVolume { get; private set; } = 1f;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            bindingsLoaded = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultMouseSensitivity);
            InvertY = PlayerPrefs.GetInt(InvertYKey, 0) == 1;
            FieldOfView = PlayerPrefs.GetFloat(FovKey, DefaultFieldOfView);
            CameraMotion = PlayerPrefs.GetFloat(CameraMotionKey, 1f);
            MasterVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            AudioListener.volume = MasterVolume;
        }

        public static void SetMouseSensitivity(float value) { MouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity); Save(); }
        public static void SetInvertY(bool value) { InvertY = value; Save(); }
        public static void SetFieldOfView(float value) { FieldOfView = Mathf.Clamp(value, MinFieldOfView, MaxFieldOfView); Save(); }
        public static void SetCameraMotion(float value) { CameraMotion = Mathf.Clamp01(value); Save(); }
        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); Save(); }

        public static void ResetToDefaults()
        {
            MouseSensitivity = DefaultMouseSensitivity;
            InvertY = false;
            FieldOfView = DefaultFieldOfView;
            CameraMotion = 1f;
            MasterVolume = 1f;
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
            PlayerPrefs.SetInt(InvertYKey, InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(FovKey, FieldOfView);
            PlayerPrefs.SetFloat(CameraMotionKey, CameraMotion);
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.Save();
            AudioListener.volume = MasterVolume;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- key bindings

        /// <summary>Applies saved key bindings to the project-wide actions (once per session).</summary>
        public static void LoadBindings()
        {
            if (bindingsLoaded || InputSystem.actions == null) return;
            bindingsLoaded = true;
            string json = PlayerPrefs.GetString(BindingsKey, "");
            if (!string.IsNullOrEmpty(json))
                InputSystem.actions.LoadBindingOverridesFromJson(json);
        }

        public static void SaveBindings()
        {
            if (InputSystem.actions == null) return;
            PlayerPrefs.SetString(BindingsKey, InputSystem.actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetBindings()
        {
            if (InputSystem.actions == null) return;
            InputSystem.actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingsKey);
            PlayerPrefs.Save();
        }
    }
}
