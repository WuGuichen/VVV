using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Input
{
    public sealed class InputCoreTests
    {
        [Test]
        public void ContextStack_OverlayKeepsLowerContexts()
        {
            var stack = new InputContextStack();
            var enabled = new List<InputContext>();

            stack.Set(InputContext.Gameplay);
            using (stack.Push(InputContext.UI, InputContextPolicy.Overlay))
            {
                stack.FillEnabledContexts(enabled);

                Assert.AreEqual(2, enabled.Count);
                Assert.AreEqual(InputContext.UI, enabled[0]);
                Assert.AreEqual(InputContext.Gameplay, enabled[1]);
                Assert.IsTrue(stack.IsContextEnabled(InputContext.UI));
                Assert.IsTrue(stack.IsContextEnabled(InputContext.Gameplay));
                Assert.IsTrue(stack.Contains(InputContext.UI));
                Assert.IsTrue(stack.Contains(InputContext.Gameplay));
            }

            stack.FillEnabledContexts(enabled);
            Assert.AreEqual(1, enabled.Count);
            Assert.AreEqual(InputContext.Gameplay, enabled[0]);
            Assert.IsTrue(stack.IsContextEnabled(InputContext.Gameplay));
            Assert.IsFalse(stack.IsContextEnabled(InputContext.UI));
            Assert.IsFalse(stack.Contains(InputContext.UI));
        }

        [Test]
        public void ContextStack_ExclusiveSuppressesLowerContexts()
        {
            var stack = new InputContextStack();
            var enabled = new List<InputContext>();

            stack.Set(InputContext.Gameplay);
            using (stack.Push(InputContext.UI))
            {
                stack.FillEnabledContexts(enabled);

                Assert.AreEqual(1, enabled.Count);
                Assert.AreEqual(InputContext.UI, enabled[0]);
                Assert.IsTrue(stack.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(stack.IsContextEnabled(InputContext.Gameplay));
                Assert.IsTrue(stack.Contains(InputContext.UI));
                Assert.IsTrue(stack.Contains(InputContext.Gameplay));
            }
        }

        [Test]
        public void ContextStack_ExclusiveUiSuppressesLowerDebugOverlay()
        {
            var stack = new InputContextStack();
            var enabled = new List<InputContext>();

            stack.Set(InputContext.Gameplay);
            using IDisposable debug = stack.Push(InputContext.Debug, InputContextPolicy.Overlay);
            using (stack.Push(InputContext.UI, InputContextPolicy.Exclusive))
            {
                stack.FillEnabledContexts(enabled);

                Assert.AreEqual(1, enabled.Count);
                Assert.AreEqual(InputContext.UI, enabled[0]);
                Assert.IsTrue(stack.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(stack.IsContextEnabled(InputContext.Gameplay));
                Assert.IsFalse(stack.IsContextEnabled(InputContext.Debug));
                Assert.IsTrue(stack.Contains(InputContext.Debug));
            }

            Assert.IsTrue(stack.IsContextEnabled(InputContext.Gameplay));
            Assert.IsTrue(stack.IsContextEnabled(InputContext.Debug));
        }

        [Test]
        public void ActionCache_ApplyContextsOnlyChangesMapStateWhenEnabledSetChanges()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                AddMap(asset, InputActionNames.GameplayMap, InputActionNames.Move);
                AddMap(asset, InputActionNames.UIMap, InputActionNames.Submit);
                AddMap(asset, InputActionNames.DebugMap, InputActionNames.ToggleHud);
                var stack = new InputContextStack();
                Type cacheType = typeof(InputService).Assembly.GetType(
                    "MxFramework.Input.InputActionCache",
                    throwOnError: true);
                object cache = Activator.CreateInstance(cacheType, nonPublic: true);
                MethodInfo bind = cacheType.GetMethod("Bind", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo applyContexts = cacheType.GetMethod(
                    "ApplyContexts",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo disableAllMaps = cacheType.GetMethod(
                    "DisableAllMaps",
                    BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo stateChangeCount = cacheType.GetProperty(
                    "MapStateChangeCountForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                bind.Invoke(cache, new object[] { asset });
                stack.Set(InputContext.Gameplay);
                applyContexts.Invoke(cache, new object[] { stack });

                Assert.AreEqual(1, stateChangeCount.GetValue(cache));
                Assert.IsTrue(asset.FindActionMap(InputActionNames.GameplayMap).enabled);
                applyContexts.Invoke(cache, new object[] { stack });
                Assert.AreEqual(1, stateChangeCount.GetValue(cache));

                IDisposable debug = stack.Push(InputContext.Debug, InputContextPolicy.Overlay);
                applyContexts.Invoke(cache, new object[] { stack });
                Assert.AreEqual(2, stateChangeCount.GetValue(cache));
                Assert.IsTrue(asset.FindActionMap(InputActionNames.GameplayMap).enabled);
                Assert.IsTrue(asset.FindActionMap(InputActionNames.DebugMap).enabled);
                applyContexts.Invoke(cache, new object[] { stack });
                Assert.AreEqual(2, stateChangeCount.GetValue(cache));

                debug.Dispose();
                applyContexts.Invoke(cache, new object[] { stack });
                Assert.AreEqual(3, stateChangeCount.GetValue(cache));
                Assert.IsTrue(asset.FindActionMap(InputActionNames.GameplayMap).enabled);
                Assert.IsFalse(asset.FindActionMap(InputActionNames.DebugMap).enabled);

                disableAllMaps.Invoke(cache, null);
                Assert.AreEqual(4, stateChangeCount.GetValue(cache));
                Assert.IsFalse(asset.FindActionMap(InputActionNames.GameplayMap).enabled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CommandQueue_DrainsDueCommandsInStableOrder()
        {
            var queue = new InputCommandQueue();
            queue.Enqueue(new InputCommand(1, 2, InputIntent.Cancel));
            queue.Enqueue(new InputCommand(0, 2, InputIntent.AttackPrimary));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));

            IReadOnlyList<InputCommand> drained = queue.DrainForFrame(0);

            Assert.AreEqual(3, drained.Count);
            Assert.AreEqual(InputIntent.Jump, drained[0].Intent);
            Assert.AreEqual(InputIntent.AttackPrimary, drained[1].Intent);
            Assert.AreEqual(1, drained[1].SourceId);
            Assert.AreEqual(InputIntent.AttackPrimary, drained[2].Intent);
            Assert.AreEqual(2, drained[2].SourceId);
            Assert.AreEqual(1, queue.PendingCount);
        }

        [Test]
        public void CommandQueue_RejectsLateCommands()
        {
            var queue = new InputCommandQueue();
            queue.DrainForFrame(3);

            bool accepted = queue.TryEnqueue(new InputCommand(2, 1, InputIntent.Jump), out _);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void CommandQueue_PeekForFrameDoesNotDrainOrAdvanceFrame()
        {
            var queue = new InputCommandQueue();
            queue.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
            var peeked = new List<InputCommand>();

            int count = queue.PeekForFrame(0, peeked, command => command.Intent == InputIntent.ToggleHud);

            Assert.AreEqual(1, count);
            Assert.AreEqual(InputIntent.ToggleHud, peeked[0].Intent);
            Assert.AreEqual(2, queue.PendingCount);
            Assert.AreEqual(0, queue.CurrentFrame);
        }

        private static void AddMap(InputActionAsset asset, string mapName, string actionName)
        {
            var map = new InputActionMap(mapName);
            map.AddAction(actionName);
            asset.AddActionMap(map);
        }

        [Test]
        public void CursorController_DefaultsToReleased()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);

            Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
            Assert.AreEqual("default", controller.Current.Owner);
            Assert.IsFalse(controller.IsCaptured);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(CursorLockMode.None, driver.LockState);
            Assert.AreEqual(1, driver.ApplyCount);
        }

        [Test]
        public void CursorController_CapturedScopeDisposesBackToReleased()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);

            IDisposable scope = controller.Push(MxCursorRequest.Captured("gameplay"));

            Assert.AreEqual(MxCursorMode.Captured, controller.Current.Mode);
            Assert.AreEqual("gameplay", controller.Current.Owner);
            Assert.IsTrue(controller.IsCaptured);
            Assert.IsFalse(driver.Visible);
            Assert.AreEqual(CursorLockMode.Locked, driver.LockState);

            scope.Dispose();

            Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
            Assert.AreEqual("default", controller.Current.Owner);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(CursorLockMode.None, driver.LockState);
        }

        [Test]
        public void CursorController_RefreshRestoresCapturedStateAfterExternalUnlock()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);
            using IDisposable scope = controller.Push(MxCursorRequest.Captured("gameplay"));
            int applyCountBeforeRefresh = driver.ApplyCount;

            driver.OverrideState(visible: false, CursorLockMode.None);

            Assert.IsFalse(controller.IsCaptured);

            controller.Refresh();

            Assert.IsTrue(controller.IsCaptured);
            Assert.IsFalse(driver.Visible);
            Assert.AreEqual(CursorLockMode.Locked, driver.LockState);
            Assert.AreEqual(applyCountBeforeRefresh + 1, driver.ApplyCount);
        }

        [Test]
        public void CursorController_ModalReleasedScopeOverridesGameplayCapturedScope()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);

            using IDisposable gameplay = controller.Push(MxCursorRequest.Captured("camera"));
            using (controller.Push(MxCursorRequest.Released("modal")))
            {
                Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
                Assert.AreEqual("modal", controller.Current.Owner);
                Assert.IsTrue(driver.Visible);
                Assert.AreEqual(CursorLockMode.None, driver.LockState);
            }

            Assert.AreEqual(MxCursorMode.Captured, controller.Current.Mode);
            Assert.AreEqual("camera", controller.Current.Owner);
            Assert.IsFalse(driver.Visible);
            Assert.AreEqual(CursorLockMode.Locked, driver.LockState);
        }

        [Test]
        public void CursorController_ModalReleasedScopeOverridesLaterGameplayCapturedScope()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);

            using IDisposable modal = controller.Push(MxCursorRequest.Released("modal"));
            using (controller.Push(MxCursorRequest.Captured("camera")))
            {
                Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
                Assert.AreEqual("modal", controller.Current.Owner);
                Assert.IsTrue(driver.Visible);
                Assert.AreEqual(CursorLockMode.None, driver.LockState);
            }

            Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
            Assert.AreEqual("modal", controller.Current.Owner);
        }

        [Test]
        public void CursorController_DisposeOutOfOrderRestoresCorrectOwner()
        {
            var controller = new MxCursorController(new RecordingCursorDriver());

            IDisposable gameplay = controller.Push(MxCursorRequest.Captured("camera"));
            IDisposable modal = controller.Push(MxCursorRequest.Released("modal"));
            IDisposable tooltip = controller.Push(MxCursorRequest.Confined("tooltip"));

            Assert.AreEqual(MxCursorMode.Confined, controller.Current.Mode);
            Assert.AreEqual("tooltip", controller.Current.Owner);

            modal.Dispose();

            Assert.AreEqual(MxCursorMode.Confined, controller.Current.Mode);
            Assert.AreEqual("tooltip", controller.Current.Owner);

            tooltip.Dispose();

            Assert.AreEqual(MxCursorMode.Captured, controller.Current.Mode);
            Assert.AreEqual("camera", controller.Current.Owner);

            gameplay.Dispose();

            Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
            Assert.AreEqual("default", controller.Current.Owner);
        }

        [Test]
        public void CursorController_ScopeDisposeIsIdempotent()
        {
            var driver = new RecordingCursorDriver();
            var controller = new MxCursorController(driver);
            IDisposable scope = controller.Push(MxCursorRequest.Captured("camera"));

            scope.Dispose();
            int applyCountAfterDispose = driver.ApplyCount;
            scope.Dispose();

            Assert.AreEqual(MxCursorMode.Released, controller.Current.Mode);
            Assert.AreEqual(applyCountAfterDispose, driver.ApplyCount);
        }

        [Test]
        public void FakeInputProvider_CanDriveSnapshotWithoutDevices()
        {
            var provider = new FakeInputProvider();
            var snapshot = new InputSnapshot(
                move: new Vector2(1f, 0f),
                look: Vector2.zero,
                navigate: Vector2.zero,
                point: Vector2.zero,
                scroll: Vector2.zero,
                throttle: 0f,
                jumpPressed: true,
                jumpHeld: true,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);

            provider.SetContext(InputContext.Gameplay);
            provider.SetSnapshot(snapshot);
            InputCommand accepted = provider.Enqueue(new InputCommand(0, 10, InputIntent.Jump));

            Assert.AreEqual(InputContext.Gameplay, provider.CurrentContext);
            Assert.AreEqual(snapshot, provider.Snapshot);
            Assert.AreEqual(0, accepted.Sequence);
        }

        [Test]
        public void RecordedInputProvider_ReplaysSnapshotsAndRewinds()
        {
            var snapshots = new[]
            {
                InputSnapshot.Empty,
                new InputSnapshot(
                    move: new Vector2(0f, 1f),
                    look: Vector2.zero,
                    navigate: Vector2.zero,
                    point: Vector2.zero,
                    scroll: Vector2.zero,
                    throttle: 0f,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: true,
                    attackPrimaryHeld: true,
                    attackSecondaryPressed: false,
                    interactPressed: false,
                    dodgePressed: false,
                    sprintHeld: false,
                    submitPressed: false,
                    cancelPressed: false,
                    pausePressed: false,
                    debugTogglePressed: false)
            };

            var provider = new RecordedInputProvider(snapshots);

            Assert.IsTrue(provider.Advance());
            Assert.AreEqual(InputSnapshot.Empty, provider.Snapshot);
            Assert.IsTrue(provider.Advance());
            Assert.IsTrue(provider.Snapshot.AttackPrimaryPressed);
            Assert.IsFalse(provider.Advance());

            provider.Rewind();
            Assert.AreEqual(0, provider.Index);
            Assert.AreEqual(InputSnapshot.Empty, provider.Snapshot);
        }

        private sealed class RecordingCursorDriver : IMxCursorDriver
        {
            public bool Visible { get; private set; }
            public CursorLockMode LockState { get; private set; }
            public int ApplyCount { get; private set; }

            public void Apply(MxCursorState state)
            {
                ApplyCount++;
                Visible = state.Visible;
                LockState = state.LockState;
            }

            public void OverrideState(bool visible, CursorLockMode lockState)
            {
                Visible = visible;
                LockState = lockState;
            }
        }
    }
}
