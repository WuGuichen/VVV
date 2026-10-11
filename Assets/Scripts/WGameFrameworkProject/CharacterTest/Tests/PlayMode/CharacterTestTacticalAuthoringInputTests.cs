using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestTacticalAuthoringInputTests
    {
        [Test]
        public void FocusedFairyGuiTextInputBlocksSetupCameraFitAndPan()
        {
            Assert.IsTrue(Application.isPlaying, "FairyGUI Stage requires a real Play Mode context.");
            GameObject gameObject = null;
            Fgui.GRoot root = null;
            Fgui.GTextInput input = null;
            Fgui.GObject previousFocus = null;
            Keyboard keyboard = null;
            Keyboard previousKeyboard = Keyboard.current;
            var cursorLock = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            float timeScale = Time.timeScale;
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                gameObject = new GameObject("Task04 camera focus fixture") { hideFlags = HideFlags.HideAndDontSave };
                var controller = gameObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var camera = gameObject.AddComponent<CharacterTestRuntimeAiLabSetupCameraController>();
                Assert.AreEqual(CharacterTestRuntimeAiPlannerDevScenePhase.Setup, controller.Phase);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = camera.GetType();
                type.GetField("_yaw", flags).SetValue(camera, 17f);
                type.GetField("_distance", flags).SetValue(camera, 30f);
                var focus = new Vector3(3, 0, 4);
                type.GetField("_focus", flags).SetValue(camera, focus);
                root = Fgui.GRoot.inst;
                previousFocus = root.focus;
                input = new Fgui.GTextInput { width = 200, height = 32, editable = true };
                root.AddChild(input);
                input.RequestFocus();
                Assert.IsTrue(input.focused);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F, Key.W));
                InputSystem.Update();
                Assert.IsTrue(CharacterTestRuntimeAiLabInput.IsKeyDown(CharacterTestRuntimeAiLabKey.F));
                Assert.IsTrue(CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.W));
                Assert.IsTrue(CharacterTestRuntimeAiLabInput.IsKeyboardBlockedByUi());
                type.GetMethod("Update", flags).Invoke(camera, null);
                Assert.AreEqual(17f, camera.Yaw, "Typing F must not reset the view.");
                Assert.AreEqual(30f, camera.Distance);
                Assert.AreEqual(focus, type.GetField("_focus", flags).GetValue(camera), "Typing WASD must not pan the camera.");
            }
            finally
            {
                input?.Dispose();
                if (root != null) root.focus = previousFocus;
                if (gameObject != null) Object.DestroyImmediate(gameObject);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                Time.timeScale = timeScale;
                Cursor.lockState = cursorLock;
                Cursor.visible = cursorVisible;
            }
        }
    }
}

