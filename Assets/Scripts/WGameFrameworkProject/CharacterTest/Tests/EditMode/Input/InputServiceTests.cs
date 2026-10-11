using System;
using System.Collections.Generic;
using MxFramework.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Input
{
    public sealed class InputServiceTests : InputTestFixture
    {
        [Test]
        public void DefaultInputService_SnapshotReadsWasdWithoutWaitingForServiceUpdate()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var gameObject = new GameObject("InputServiceTest");

            try
            {
                var service = gameObject.AddComponent<DefaultInputService>();

                _ = service.Snapshot;
                Press(keyboard.wKey);
                InputSystem.Update();

                Assert.Greater(service.Snapshot.Move.y, 0.5f);
                Assert.AreEqual(0f, service.Snapshot.Move.x);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DefaultInputService_UsesUnifiedPauseAndDebugBindings()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var gameObject = new GameObject("InputServiceBindingTest");

            try
            {
                var service = gameObject.AddComponent<DefaultInputService>();
                _ = service.Snapshot;
                using (service.PushContext(InputContext.Debug, InputContextPolicy.Overlay))
                {
                    InputSystem.Update();
                    Assert.IsTrue(service.IsContextEnabled(InputContext.Gameplay), "Gameplay context is not enabled.");
                    Assert.IsTrue(service.IsContextEnabled(InputContext.Debug), "Debug context is not enabled.");
                    InputAction pauseAction = service.Actions.FindAction("Gameplay/Pause", false);
                    Assert.IsNotNull(pauseAction);
                    Assert.IsTrue(pauseAction.enabled, "Pause action is not enabled.");
                    Assert.Greater(pauseAction.controls.Count, 0, "Pause action did not resolve any controls.");

                    Press(keyboard.escapeKey);
                    Assert.IsTrue(service.Snapshot.PausePressed, "Escape did not produce PausePressed.");
                    Release(keyboard.escapeKey);

                    Press(keyboard.lKey);
                    Assert.IsTrue(service.Snapshot.TargetLockPressed, "L did not produce TargetLockPressed.");
                    Assert.IsFalse(service.Snapshot.AttackSecondaryPressed, "L should not produce AttackSecondaryPressed.");
                    Release(keyboard.lKey);

                    Press(keyboard.f5Key);
                    Assert.IsTrue(service.Snapshot.DebugPrimaryPressed, "F5 did not produce DebugPrimaryPressed.");
                    Assert.IsFalse(service.Snapshot.PausePressed, "F5 should not produce PausePressed.");
                    Release(keyboard.f5Key);

                    Press(keyboard.f6Key);
                    Assert.IsTrue(service.Snapshot.DebugSecondaryPressed, "F6 did not produce DebugSecondaryPressed.");
                    Assert.IsFalse(service.Snapshot.PausePressed, "F6 should not produce PausePressed.");
                    Release(keyboard.f6Key);

                    Press(keyboard.digit1Key);
                    Assert.AreEqual(1, service.Snapshot.SkillSlotPressedMask, "1 did not produce SkillSlot1.");
                    Assert.IsFalse(service.Snapshot.PausePressed, "1 should not produce PausePressed.");
                    Release(keyboard.digit1Key);

                    Press(keyboard.pKey);
                    Assert.IsFalse(service.Snapshot.PausePressed);
                    Assert.IsFalse(service.Snapshot.DebugSecondaryPressed);
                    Release(keyboard.pKey);

                    Press(keyboard.fKey);
                    Assert.IsFalse(service.Snapshot.AttackSecondaryPressed);
                    Release(keyboard.fKey);

                    Press(keyboard.hKey);
                    Assert.IsFalse(service.Snapshot.ToggleHudPressed);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DefaultInputService_BracketsEnqueueGuidanceNavigationCommands()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var gameObject = new GameObject("InputServiceGuidanceBindingTest");

            try
            {
                var service = gameObject.AddComponent<TestInputService>();
                _ = service.Snapshot;
                service.RunUpdateForTest();
                InputAction previousAction = service.Actions.FindAction("Gameplay/GuidancePrevious", false);
                InputAction nextAction = service.Actions.FindAction("Gameplay/GuidanceNext", false);
                Assert.IsNotNull(previousAction);
                Assert.IsNotNull(nextAction);
                Assert.IsTrue(previousAction.enabled);
                Assert.IsTrue(nextAction.enabled);

                Press(keyboard.leftBracketKey);
                Assert.IsTrue(previousAction.WasPressedThisFrame(), "Left bracket action was not pressed.");
                service.RunUpdateForTest();
                Release(keyboard.leftBracketKey);
                service.RunUpdateForTest();

                Press(keyboard.rightBracketKey);
                Assert.IsTrue(nextAction.WasPressedThisFrame(), "Right bracket action was not pressed.");
                service.RunUpdateForTest();

                var commands = new List<InputCommand>();
                service.Commands.PeekForFrame(long.MaxValue, commands);
                string observed = string.Join(", ", commands.ConvertAll(command => command.Intent.ToString()));
                Assert.IsTrue(commands.Exists(command =>
                    command.Intent == InputIntent.GuidancePrevious),
                    "Missing GuidancePrevious; observed: " + observed);
                Assert.IsTrue(commands.Exists(command =>
                    command.Intent == InputIntent.GuidanceNext),
                    "Missing GuidanceNext; observed: " + observed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InputService_TryChangeBindingPath_ChangesRuntimeBindingWithoutOverride()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var gameObject = new GameObject("InputServiceRuntimeBindingPathTest");

            try
            {
                var service = gameObject.AddComponent<DefaultInputService>();
                _ = service.Snapshot;
                long bindingRevision = service.BindingDisplay.Revision;
                Assert.IsTrue(service.TryChangeBindingPath(
                    "Gameplay/Pause",
                    0,
                    DefaultInputBindingCatalog.KeyboardPPath,
                    DefaultInputBindingCatalog.KeyboardEscapePath));

                InputAction pauseAction = service.Actions.FindAction("Gameplay/Pause", false);
                Assert.IsNotNull(pauseAction);
                Assert.AreEqual(DefaultInputBindingCatalog.KeyboardPPath, pauseAction.bindings[0].path);
                Assert.IsTrue(string.IsNullOrEmpty(pauseAction.bindings[0].overridePath));
                Assert.Greater(service.BindingDisplay.Revision, bindingRevision);

                Press(keyboard.escapeKey);
                Assert.IsFalse(service.Snapshot.PausePressed, "Transient path change should remove Escape from Pause.");
                Release(keyboard.escapeKey);

                Press(keyboard.pKey);
                Assert.IsTrue(service.Snapshot.PausePressed, "Transient path change should route P to Pause.");
                Release(keyboard.pKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InputService_UpdateDoesNotAllocateAfterActionCacheWarmup()
        {
            var gameObject = new GameObject("InputServiceUpdateAllocationTest");

            try
            {
                var service = gameObject.AddComponent<TestInputService>();
                _ = service.Snapshot;
                service.RunUpdateForTest();

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    service.RunUpdateForTest();
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InputService_SnapshotHotReadUsesCachedValueAfterUpdate()
        {
            var gameObject = new GameObject("InputServiceSnapshotHotReadAllocationTest");

            try
            {
                var service = gameObject.AddComponent<TestInputService>();
                _ = service.Snapshot;
                service.RunUpdateForTest();

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    _ = service.Snapshot;
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InputRebindingService_EditSessionRollsBackOrCommitsAsOneDraft()
        {
            string preferencesKey = "mxframework.input.rebinds.test." + Guid.NewGuid().ToString("N");
            InputActionAsset actions = DefaultInputActionAssetFactory.Create();
            try
            {
                InputAction jump = actions.FindAction(
                    InputActionNames.GameplayMap + "/" + InputActionNames.Jump,
                    true);
                jump.ApplyBindingOverride(0, "<Keyboard>/q");
                PlayerPrefs.SetString(preferencesKey, "persisted-before-draft");

                using (var service = new InputRebindingService(actions, preferencesKey))
                {
                    service.BeginEditSession();
                    service.ResetToDefault();

                    Assert.IsTrue(service.IsEditSessionActive);
                    Assert.IsTrue(service.HasPendingChanges);
                    Assert.IsTrue(string.IsNullOrEmpty(jump.bindings[0].overridePath));
                    Assert.AreEqual("persisted-before-draft", PlayerPrefs.GetString(preferencesKey));

                    service.RollbackEditSession();

                    Assert.IsFalse(service.IsEditSessionActive);
                    Assert.IsFalse(service.HasPendingChanges);
                    Assert.AreEqual("<Keyboard>/q", jump.bindings[0].overridePath);
                    Assert.AreEqual("persisted-before-draft", PlayerPrefs.GetString(preferencesKey));

                    service.BeginEditSession();
                    service.ResetToDefault();
                    string expectedCommittedJson = actions.SaveBindingOverridesAsJson();
                    service.CommitEditSession();

                    Assert.IsFalse(service.IsEditSessionActive);
                    Assert.IsFalse(service.HasPendingChanges);
                    Assert.AreEqual(expectedCommittedJson, PlayerPrefs.GetString(preferencesKey));
                }
            }
            finally
            {
                PlayerPrefs.DeleteKey(preferencesKey);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        private sealed class TestInputService : InputService
        {
            public void RunUpdateForTest()
            {
                Update();
            }

            protected override InputActionAsset CreateRuntimeActions(out bool ownsActions)
            {
                ownsActions = true;
                return DefaultInputActionAssetFactory.Create();
            }
        }
    }
}
