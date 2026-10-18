using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SandHouse.Player;
using SandHouse.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SandHouse.Tests
{
    public class PlayerInputTests
    {
        InputActionAsset original, actions;
        Keyboard keyboard;
        Gamepad gamepad;
        Joystick joystick;
        GameObject player;
        PlayerInputReader reader;
        bool originalEnabled, bindingsLoaded;
        InputSettings.BackgroundBehavior backgroundBehavior;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode editorInputBehavior;
#endif
        static readonly FieldInfo Loaded = typeof(GameSettings).GetField("bindingsLoaded", BindingFlags.Static | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            original = InputSystem.actions;
            originalEnabled = original.enabled;
            original.Disable();
            actions = InputActionAsset.FromJson(original.ToJson());
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            joystick = InputSystem.AddDevice<Joystick>();
            actions.devices = new InputDevice[] { keyboard, gamepad, joystick };
            // Keep the user's saved bindings intact; this fixture tests only project defaults.
            bindingsLoaded = (bool)Loaded.GetValue(null); Loaded.SetValue(null, true);
            player = new GameObject("Input regression player");
            player.SetActive(false);
            reader = player.AddComponent<PlayerInputReader>();
            typeof(PlayerInputReader).GetField("actionAsset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reader, actions);
            player.SetActive(true);
            yield return null;
            Focus(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            actions.Disable();
            if (originalEnabled) original.Enable();
            Loaded.SetValue(null, bindingsLoaded);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad); InputSystem.RemoveDevice(joystick);
            Object.DestroyImmediate(actions);
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
#endif
        }

        [UnityTest]
        public IEnumerator NoInputStartsAtRest()
        {
            yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move);
        }

        [UnityTest]
        public IEnumerator IdleGenericJoystickCannotWalkPlayerBackward()
        {
            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.down);
            yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move);
        }

        [UnityTest]
        public IEnumerator NoisyJoystickCannotOverrideKeyboardForward()
        {
            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.down);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            Assert.AreEqual(Vector2.up, reader.Move);
        }

        [UnityTest]
        public IEnumerator WASDAndArrowDirectionsRemainCorrect()
        {
            foreach (var key in new[] { Key.W, Key.S, Key.A, Key.D, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
                Vector2 expected = key == Key.W || key == Key.UpArrow ? Vector2.up :
                    key == Key.S || key == Key.DownArrow ? Vector2.down :
                    key == Key.A || key == Key.LeftArrow ? Vector2.left : Vector2.right;
                Assert.AreEqual(expected, reader.Move, key.ToString());
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move);
        }

        [UnityTest]
        public IEnumerator GamepadStillSupportsDeliberateMovementAndNeutralDeadzone()
        {
            InputSystem.QueueDeltaStateEvent(gamepad.leftStick, Vector2.down); yield return null;
            Assert.AreEqual(Vector2.down, reader.Move);
            InputSystem.QueueDeltaStateEvent(gamepad.leftStick, new Vector2(0f, -0.05f)); yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move);
        }

        void Focus(bool focused) => typeof(PlayerInputReader).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, new object[] { focused });

        [UnityTest]
        public IEnumerator StaleBackwardKeyOnEnableRequiresReleaseBeforeMovement()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); yield return null;
            Assert.AreEqual(Vector2.down, reader.Move);
            player.SetActive(false); player.SetActive(true);
            Focus(true); yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move, "Starting with a latched direction must not auto-walk");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return null;
            Assert.AreEqual(Vector2.up, reader.Move);
        }

        [UnityTest]
        public IEnumerator FocusLossCannotLatchBackwardMovementOnReturn()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); yield return null;
            Focus(false); Assert.AreEqual(Vector2.zero, reader.Move);
            Focus(true); yield return null;
            Assert.AreEqual(Vector2.zero, reader.Move);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); yield return null;
            Assert.AreEqual(Vector2.down, reader.Move);
        }
    }
}
