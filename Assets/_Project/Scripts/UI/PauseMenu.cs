using System;
using System.Collections.Generic;
using SandHouse.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandHouse.UI
{
    /// <summary>
    /// Esc / Start pauses the game: Resume, Settings (sensitivity, invert Y, FOV, camera motion,
    /// volume, key bindings) and Exit. Pausing freezes time and disables the Player input map,
    /// which is also what lets the key-binding screen rebind actions.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        struct BindableAction
        {
            public string Label, Action, CompositePart;
            public BindableAction(string label, string action, string compositePart = null)
            {
                Label = label;
                Action = action;
                CompositePart = compositePart;
            }
        }

        static readonly BindableAction[] Bindables =
        {
            new BindableAction("Move Forward", "Move", "up"),
            new BindableAction("Move Back", "Move", "down"),
            new BindableAction("Move Left", "Move", "left"),
            new BindableAction("Move Right", "Move", "right"),
            new BindableAction("Jump", "Jump"),
            new BindableAction("Dash", "Dash"),
            new BindableAction("Crouch / Slide", "Crouch"),
            new BindableAction("Shoot", "Attack"),
            new BindableAction("Reload", "Reload"),
            new BindableAction("Lasso", "Lasso"),
            new BindableAction("Dead Eye", "DeadEye"),
            new BindableAction("Stats (hold)", "Stats"),
            new BindableAction("Weapon 1", "Weapon1"),
            new BindableAction("Weapon 2", "Weapon2"),
            new BindableAction("Weapon 3", "Weapon3"),
            new BindableAction("Weapon 4", "Weapon4"),
        };

        class KeybindRow
        {
            public InputAction Action;
            public int BindingIndex;
            public Text KeyText;
        }

        InputAction pauseAction;
        InputActionMap playerMap;
        float savedTimeScale = 1f;

        GameObject root, mainPage, settingsPage, keybindsPage;
        readonly List<KeybindRow> keybindRows = new List<KeybindRow>();
        InputActionRebindingExtensions.RebindingOperation rebinding;

        Slider sensitivitySlider, fovSlider, motionSlider, volumeSlider;
        Toggle invertToggle;
        Text sensitivityValue, fovValue, motionValue, volumeValue;

        void Awake()
        {
            playerMap = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;

            // Its own action (not in the Player map) so it still works while the Player map is disabled.
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");

            BuildUI();
            root.SetActive(false);
        }

        void OnEnable() => pauseAction.Enable();

        void OnDisable()
        {
            pauseAction.Disable();
            if (UIState.IsPaused) Resume();
        }

        void OnDestroy()
        {
            rebinding?.Dispose();
            pauseAction.Dispose();
        }

        void Update()
        {
            if (UIState.IsRunMenuOpen) return;
            if (rebinding != null || !pauseAction.WasPressedThisFrame()) return;

            if (!UIState.IsPaused) Pause();
            else if (keybindsPage.activeSelf) ShowPage(settingsPage);
            else if (settingsPage.activeSelf) ShowPage(mainPage);
            else Resume();
        }

        public void Pause()
        {
            if (UIState.IsRunMenuOpen) return;
            UIState.IsPaused = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            playerMap?.Disable();
            UIState.SetOpen(this, true);
            root.SetActive(true);
            ShowPage(mainPage);
        }

        public void Resume()
        {
            CancelRebind();
            UIState.IsPaused = false;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
            playerMap?.Enable();
            UIState.SetOpen(this, false);
            root.SetActive(false);
        }

        static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowPage(GameObject page)
        {
            CancelRebind();
            mainPage.SetActive(page == mainPage);
            settingsPage.SetActive(page == settingsPage);
            keybindsPage.SetActive(page == keybindsPage);
            if (page == settingsPage) RefreshSettings();
            if (page == keybindsPage) RefreshKeybinds();
        }

        // ---------------------------------------------------------------- settings

        void RefreshSettings()
        {
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            invertToggle.SetIsOnWithoutNotify(GameSettings.InvertY);
            fovSlider.SetValueWithoutNotify(GameSettings.FieldOfView);
            motionSlider.SetValueWithoutNotify(GameSettings.CameraMotion);
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00");
            fovValue.text = $"{GameSettings.FieldOfView:0}°";
            motionValue.text = $"{GameSettings.CameraMotion * 100f:0}%";
            volumeValue.text = $"{GameSettings.MasterVolume * 100f:0}%";
        }

        void OnSettingChanged(Action apply)
        {
            apply();
            RefreshSettings();
        }

        // ---------------------------------------------------------------- key bindings

        void RefreshKeybinds()
        {
            foreach (var row in keybindRows)
                row.KeyText.text = row.BindingIndex >= 0 ? row.Action.GetBindingDisplayString(row.BindingIndex) : "-";
        }

        void StartRebind(KeybindRow row)
        {
            if (row.BindingIndex < 0) return;
            CancelRebind();
            row.KeyText.text = "Press a key...  (Esc to cancel)";

            rebinding = row.Action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ =>
                {
                    FinishRebind();
                    GameSettings.SaveBindings();
                })
                .OnCancel(_ => FinishRebind())
                .Start();
        }

        void FinishRebind()
        {
            rebinding?.Dispose();
            rebinding = null;
            RefreshKeybinds();
        }

        void CancelRebind()
        {
            if (rebinding == null) return;
            rebinding.Cancel(); // OnCancel tidies up
            rebinding = null;
        }

        /// <summary>Finds the keyboard/mouse binding to rebind (for composites like Move, the named part).</summary>
        static int FindKeyboardBinding(InputAction action, string compositePart)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                bool keyboardOrMouse = binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>");
                if (!keyboardOrMouse) continue;

                if (compositePart != null)
                {
                    if (binding.isPartOfComposite && binding.name == compositePart) return i;
                }
                else if (!binding.isComposite && !binding.isPartOfComposite)
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- UI construction

        void BuildUI()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("Pause Canvas", transform, sortingOrder: 50, interactive: true);

            // Dim the game behind the menu (and block clicks reaching anything underneath).
            var dim = UIFactory.CreateImage(canvas.transform, "Pause Menu", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            dim.rectTransform.Stretch();
            root = dim.gameObject;

            mainPage = BuildMainPage(root.transform);
            settingsPage = BuildSettingsPage(root.transform);
            keybindsPage = BuildKeybindsPage(root.transform);
        }

        static RectTransform CreatePage(Transform parent, string title, float width)
        {
            var page = UIFactory.CreatePanel(parent, title).rectTransform;
            page.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0f));
            UIFactory.VerticalLayout(page.gameObject, spacing: 10f, padding: 28);
            page.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFactory.CreateText(page, "Title", title, 40, TextAnchor.MiddleCenter, UIFactory.AccentColor).Size(height: 64f);
            return page;
        }

        GameObject BuildMainPage(Transform parent)
        {
            var page = CreatePage(parent, "PAUSED", 460f);
            UIFactory.CreateButton(page, "Resume", Resume).Size(height: 56f);
            UIFactory.CreateButton(page, "Settings", () => ShowPage(settingsPage)).Size(height: 56f);
            if (Application.CanStreamedLevelBeLoaded("RoguelikeRun"))
                UIFactory.CreateButton(page, "Start New Run", () =>
                {
                    Resume();
                    SceneManager.LoadScene("RoguelikeRun");
                }).Size(height: 56f);
            UIFactory.CreateButton(page, "Exit", ExitGame).Size(height: 56f);
            return page.gameObject;
        }

        GameObject BuildSettingsPage(Transform parent)
        {
            var page = CreatePage(parent, "SETTINGS", 760f);

            (sensitivitySlider, sensitivityValue) = SliderRow(page, "Mouse Sensitivity",
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity,
                v => OnSettingChanged(() => GameSettings.SetMouseSensitivity(v)));

            var invertRow = SettingRow(page, "Invert Mouse Y");
            invertToggle = UIFactory.CreateToggle(invertRow, GameSettings.InvertY,
                v => OnSettingChanged(() => GameSettings.SetInvertY(v))).Size(width: 36f);

            (fovSlider, fovValue) = SliderRow(page, "Field of View",
                GameSettings.MinFieldOfView, GameSettings.MaxFieldOfView,
                v => OnSettingChanged(() => GameSettings.SetFieldOfView(Mathf.Round(v))));

            (motionSlider, motionValue) = SliderRow(page, "Camera Motion", 0f, 1f,
                v => OnSettingChanged(() => GameSettings.SetCameraMotion(v)));

            (volumeSlider, volumeValue) = SliderRow(page, "Master Volume", 0f, 1f,
                v => OnSettingChanged(() => GameSettings.SetMasterVolume(v)));

            UIFactory.CreateRect(page, "Spacer").Size(height: 6f);
            UIFactory.CreateButton(page, "Key Bindings", () => ShowPage(keybindsPage)).Size(height: 52f);

            var bottom = UIFactory.CreateRow(page, "Bottom", 52f, spacing: 12f);
            bottom.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            UIFactory.CreateButton(bottom, "Reset Defaults", () => OnSettingChanged(GameSettings.ResetToDefaults));
            UIFactory.CreateButton(bottom, "Back", () => ShowPage(mainPage));
            return page.gameObject;
        }

        GameObject BuildKeybindsPage(Transform parent)
        {
            var page = CreatePage(parent, "KEY BINDINGS", 720f);
            page.GetComponent<VerticalLayoutGroup>().spacing = 5f;

            foreach (var bindable in Bindables)
            {
                InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(bindable.Action) : null;
                if (action == null) continue;

                var row = SettingRow(page, bindable.Label, height: 38f);
                var keybind = new KeybindRow { Action = action, BindingIndex = FindKeyboardBinding(action, bindable.CompositePart) };
                var button = UIFactory.CreateButton(row, "", () => StartRebind(keybind), 22).Size(width: 320f);
                keybind.KeyText = button.GetComponentInChildren<Text>();
                keybindRows.Add(keybind);
            }

            UIFactory.CreateRect(page, "Spacer").Size(height: 6f);
            var bottom = UIFactory.CreateRow(page, "Bottom", 52f, spacing: 12f);
            bottom.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            UIFactory.CreateButton(bottom, "Reset Bindings", () =>
            {
                CancelRebind();
                GameSettings.ResetBindings();
                RefreshKeybinds();
            });
            UIFactory.CreateButton(bottom, "Back", () => ShowPage(settingsPage));
            return page.gameObject;
        }

        static RectTransform SettingRow(Transform parent, string label, float height = 48f)
        {
            var row = UIFactory.CreateRow(parent, label, height, spacing: 16f);
            UIFactory.CreateText(row, "Label", label, 24).Size(flexibleWidth: 1f);
            return row;
        }

        static (Slider, Text) SliderRow(Transform parent, string label, float min, float max, Action<float> onChanged)
        {
            var row = SettingRow(parent, label);
            var slider = UIFactory.CreateSlider(row, min, max, min, onChanged).Size(width: 300f);
            var value = UIFactory.CreateText(row, "Value", "", 22, TextAnchor.MiddleRight).Size(width: 80f);
            return (slider, value);
        }
    }
}
