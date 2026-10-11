using System;
using System.Collections;
using MxFramework.Demo.GameplayTriggers;
using MxFramework.Gameplay.Triggers;
using MxFramework.Input;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.PlayMode.GameplayTriggers
{
#if UNITY_EDITOR
    public sealed class GameplayTriggerPlayableSceneTests : InputTestFixture
    {
        private const string ScenePath =
            "Assets/Scenes/GameplayTriggerPlayable.unity";
        private const double InitializationTimeoutSeconds = 5d;
        private const double BehaviorTimeoutSeconds = 5d;
        private const double LogicalFrameTimeoutSeconds = 2d;
        private const float ShowcaseCameraDepth = 2f;

        [UnityTest]
        public IEnumerator SceneBootstrapsExplicitInputAndVisibleHud()
        {
            InputSystem.AddDevice<Keyboard>();
            var context = new PlayableContext();
            yield return LoadPlayable(context);

            Assert.That(context.Root.IsInitialized, Is.True);
            Assert.That(context.InputService, Is.Not.Null);
            Assert.That(
                context.Root.GetComponent<DefaultInputService>(),
                Is.Null,
                "The playable must not bypass its serialized InputActionAsset.");
            Assert.That(context.InputService.Actions, Is.Not.Null);
            Assert.That(
                context.InputService.Actions.FindAction("Gameplay/Move", false),
                Is.Not.Null);
            Assert.That(
                context.InputService.Actions.FindAction("Debug/DebugSecondary", false),
                Is.Not.Null);
            Assert.That(
                context.InputService.IsContextEnabled(InputContext.Gameplay),
                Is.True);
            Assert.That(
                context.InputService.IsContextEnabled(InputContext.Debug),
                Is.True);
            Assert.That(context.InputService.Commands.PendingCount, Is.Zero);

            Transform showcaseCameraTransform = FindUniqueSceneTransform(
                context.Root.gameObject.scene,
                "Main Camera",
                out int showcaseCameraCount);
            Assert.That(
                showcaseCameraCount,
                Is.EqualTo(1),
                "Showcase scene must contain exactly one named Main Camera.");
            Assert.That(showcaseCameraTransform, Is.Not.Null, "Showcase Main Camera.");
            UnityEngine.Camera showcaseCamera =
                showcaseCameraTransform.GetComponent<UnityEngine.Camera>();
            Assert.That(showcaseCamera, Is.Not.Null, "Showcase Main Camera component.");
            Assert.That(
                showcaseCamera.depth,
                Is.EqualTo(ShowcaseCameraDepth),
                "Showcase Main Camera must render after depth-zero test listeners " +
                "and the FairyGUI Stage Camera.");
            GameObject stageCameraObject = GameObject.Find("Stage Camera");
            if (stageCameraObject != null)
            {
                UnityEngine.Camera stageCamera =
                    stageCameraObject.GetComponent<UnityEngine.Camera>();
                if (stageCamera != null && stageCamera.enabled)
                {
                    Assert.That(
                        showcaseCamera.depth,
                        Is.GreaterThan(stageCamera.depth),
                        "Showcase Main Camera must render after the enabled " +
                        "FairyGUI Stage Camera.");
                }
            }
            Assert.That(
                showcaseCamera.depth,
                Is.GreaterThan(0f),
                "Showcase Main Camera depth must remain explicitly positive.");

            string[] requiredElements =
            {
                "gameplay-trigger-root",
                "frame-label",
                "position-label",
                "checkpoint-label",
                "door-label",
                "trap-label",
                "event-label",
                "hash-label",
                "save-label",
                "move-left-button",
                "move-right-button",
                "move-up-button",
                "move-down-button",
                "capture-button",
                "restore-button",
                "reset-button"
            };
            for (int i = 0; i < requiredElements.Length; i++)
            {
                VisualElement element =
                    context.Document.rootVisualElement.Q(requiredElements[i]);
                Assert.That(element, Is.Not.Null, requiredElements[i]);
                Assert.That(
                    element.resolvedStyle.display,
                    Is.Not.EqualTo(DisplayStyle.None),
                    requiredElements[i]);
                Assert.That(
                    element.resolvedStyle.visibility,
                    Is.EqualTo(Visibility.Visible),
                    requiredElements[i]);
                Assert.That(
                    element.resolvedStyle.opacity,
                    Is.GreaterThan(0f),
                    requiredElements[i]);
                if (element is Label label)
                    Assert.That(label.text, Is.Not.Empty, requiredElements[i]);
                if (element is Button button)
                {
                    Assert.That(button.enabledInHierarchy, Is.True, requiredElements[i]);
                    Assert.That(button.worldBound.width, Is.GreaterThan(1f), requiredElements[i]);
                    Assert.That(button.worldBound.height, Is.GreaterThan(1f), requiredElements[i]);
                }
            }

        }

        [UnityTest]
        public IEnumerator KeyboardTraversalRoutesTriggersAndF6Restore()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var context = new PlayableContext();
            yield return LoadPlayable(context);
            GameplayTriggerPlayableSnapshot initial = context.Root.Snapshot;

            Press(keyboard.dKey);
            yield return WaitForSnapshot(
                context.Root,
                snapshot => snapshot.CheckpointAccepted && snapshot.DoorOpen,
                "Keyboard traversal did not acquire the checkpoint and open the door.");
            Release(keyboard.dKey);
            yield return SettleInput(context.Root);
            AssertProjectionConsistent(context, "checkpoint and door traversal");

            yield return Tap(context.Root, keyboard.eKey);
            yield return WaitForSnapshot(
                context.Root,
                snapshot => snapshot.HasCapture,
                "E did not capture the completed logical frame.");
            GameplayTriggerPlayableSnapshot captured = context.Root.Snapshot;
            Assert.That(captured.CapturedFrame, Is.GreaterThanOrEqualTo(0L));
            Assert.That(captured.CheckpointAccepted, Is.True);
            Assert.That(captured.DoorUnlocked, Is.True);
            Assert.That(captured.DoorOpen, Is.True);
            Assert.That(captured.TrapActivations, Is.Zero);
            Assert.That(captured.SlowFramesRemaining, Is.Zero);

            Press(keyboard.dKey);
            yield return WaitForSnapshot(
                context.Root,
                snapshot => snapshot.PlayerXRaw >= captured.PlayerXRaw + 400000L,
                "Player did not move away from the captured state.");
            Release(keyboard.dKey);
            yield return SettleInput(context.Root);
            long beforeF5 = context.Root.Snapshot.PlayerXRaw;
            Assert.That(beforeF5, Is.Not.EqualTo(captured.PlayerXRaw));

            yield return Tap(context.Root, keyboard.f5Key);
            yield return SettleInput(context.Root);
            Assert.That(
                context.Root.Snapshot.PlayerXRaw,
                Is.EqualTo(beforeF5),
                "F5/DebugPrimary must not restore the playable.");

            Press(keyboard.dKey);
            yield return WaitForSnapshot(
                context.Root,
                snapshot => snapshot.TrapActivations > 0,
                "Keyboard traversal did not activate the trap Trigger.");
            Release(keyboard.dKey);
            yield return SettleInput(context.Root);
            Assert.That(context.Root.Snapshot.PlayerXRaw, Is.GreaterThan(captured.PlayerXRaw));
            Assert.That(context.Root.Snapshot.TrapActivations, Is.GreaterThan(0));

            yield return Tap(context.Root, keyboard.f6Key);
            yield return WaitForSnapshot(
                context.Root,
                snapshot =>
                    SavedAuthorityMatches(snapshot, captured) &&
                    snapshot.FirstOccurrenceKindThisTick ==
                        GameplayTriggerOccurrenceKind.Stayed &&
                    snapshot.OccurrenceCountThisTick == 1,
                "F6/DebugSecondary did not restore the full captured authority state " +
                "and continue with one Stayed occurrence.");
            GameplayTriggerPlayableSnapshot restored = context.Root.Snapshot;
            Assert.That(
                restored.LastOccurrenceKind,
                Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));
            Assert.That(
                restored.FirstOccurrenceKindThisTick,
                Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));
            Assert.That(restored.OccurrenceCountThisTick, Is.EqualTo(1));
            Assert.That(restored.TrapActivations, Is.Zero);
            Assert.That(restored.HasCapture, Is.True);
            Assert.That(restored.CapturedFrame, Is.EqualTo(captured.CapturedFrame));
            AssertProjectionConsistent(context, "F6 restore");

            yield return Tap(context.Root, keyboard.f4Key);
            yield return WaitForSnapshot(
                context.Root,
                snapshot =>
                    snapshot.PlayerXRaw == initial.PlayerXRaw &&
                    snapshot.PlayerZRaw == initial.PlayerZRaw &&
                    !snapshot.CheckpointAccepted &&
                    !snapshot.DoorUnlocked &&
                    !snapshot.DoorOpen &&
                    snapshot.TrapActivations == 0 &&
                    snapshot.SlowFramesRemaining == 0 &&
                    snapshot.CombatBodyCount == initial.CombatBodyCount &&
                    snapshot.CombatColliderCount == initial.CombatColliderCount &&
                    !snapshot.HasCapture,
                "F4 did not publish a fresh default composition.");
            AssertProjectionConsistent(context, "F4 reset");
            Assert.That(context.InputService.Commands.PendingCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HudDirectionsSupportHeldReleaseCaptureOutAndSubmitPulse()
        {
            InputSystem.AddDevice<Keyboard>();
            var context = new PlayableContext();
            yield return LoadPlayable(context);

            Button right = RequireButton(context, "move-right-button");
            Button left = RequireButton(context, "move-left-button");
            Button up = RequireButton(context, "move-up-button");
            Button down = RequireButton(context, "move-down-button");

            yield return VerifyPointerHeldMove(context.Root, right, useX: true, 1);
            yield return VerifyPointerHeldMove(context.Root, left, useX: true, -1);
            yield return VerifyPointerHeldMove(context.Root, up, useX: false, 1);
            yield return VerifyPointerHeldMove(context.Root, down, useX: false, -1);

            yield return VerifyPointerCaptureOutStopsMove(
                context.Root,
                right,
                useX: true,
                direction: 1);

            long beforeSubmit = context.Root.Snapshot.PlayerXRaw;
            long beforeSubmitFrame = context.Root.Snapshot.Frame;
            SendNavigationSubmitAndAssertDispatch(right);
            yield return WaitForLogicalFrameChange(
                context.Root,
                beforeSubmitFrame,
                "NavigationSubmit was dispatched but no logical Tick advanced.");
            long movedBySubmit = context.Root.Snapshot.PlayerXRaw;
            AssertDirectionalDelta(
                beforeSubmit,
                movedBySubmit,
                direction: 1,
                "NavigationSubmit did not enqueue a consumable move pulse.");
            yield return AssertSingleAxisPulse(
                context.Root,
                useX: true,
                movedBySubmit);
            Assert.That(context.InputService.Commands.PendingCount, Is.Zero);
        }

        private static IEnumerator LoadPlayable(PlayableContext context)
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            Assert.That(scene.handle, Is.Not.Zero);

            double deadline = Time.realtimeSinceStartupAsDouble +
                InitializationTimeoutSeconds;
            string reason = "The returned scene has not completed loading.";
            bool bound = false;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (TryBindPlayableContext(scene, context, out reason))
                {
                    yield return null;
                    if (TryBindPlayableContext(scene, context, out reason))
                    {
                        bound = true;
                        break;
                    }
                }

                yield return null;
            }

            if (!bound)
                bound = TryBindPlayableContext(scene, context, out reason);
            Assert.That(bound, Is.True, reason);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(context.Root.gameObject.scene.handle, Is.EqualTo(scene.handle));
            GameplayTriggerPlayableShowcaseRoot finalRoot =
                FindUniqueSceneRoot(scene, out int rootCount);
            Assert.That(rootCount, Is.EqualTo(1));
            Assert.That(context.Root, Is.SameAs(finalRoot));
            Assert.That(context.Root.IsInitialized, Is.True);
            Assert.That(context.Document.rootVisualElement, Is.Not.Null);
            Assert.That(context.InputService, Is.Not.Null);
            Assert.That(context.PlayerView, Is.Not.Null);
            Assert.That(context.DoorView, Is.Not.Null);
        }

        private static bool TryBindPlayableContext(
            Scene scene,
            PlayableContext context,
            out string reason)
        {
            if (!scene.isLoaded)
            {
                reason = "The returned Gameplay Trigger scene is not loaded.";
                return false;
            }

            GameplayTriggerPlayableShowcaseRoot root =
                FindUniqueSceneRoot(scene, out int rootCount);
            if (rootCount != 1 || root == null)
            {
                reason = "Expected one Gameplay Trigger root in returned scene handle " +
                    scene.handle + ", found " + rootCount + ".";
                return false;
            }
            if (!root.IsInitialized)
            {
                reason = "Gameplay Trigger root in returned scene is not initialized.";
                return false;
            }

            UIDocument document = root.GetComponent<UIDocument>();
            InputService inputService = root.GetComponent<InputService>();
            Transform playerView = FindUniqueSceneTransform(
                scene,
                "GameplayTriggerPlayer",
                out int playerCount);
            Transform doorView = FindUniqueSceneTransform(
                scene,
                "GameplayTriggerDoor",
                out int doorCount);
            if (document == null || document.rootVisualElement == null)
            {
                reason = "Gameplay Trigger UIDocument is not ready in returned scene.";
                return false;
            }
            if (inputService == null)
            {
                reason = "Gameplay Trigger InputService is missing in returned scene.";
                return false;
            }
            if (playerCount != 1 || playerView == null ||
                doorCount != 1 || doorView == null)
            {
                reason = "Gameplay Trigger scene views are not uniquely available: player=" +
                    playerCount + ", door=" + doorCount + ".";
                return false;
            }

            context.Root = root;
            context.Document = document;
            context.InputService = inputService;
            context.PlayerView = playerView;
            context.DoorView = doorView;
            reason = string.Empty;
            return true;
        }

        private static GameplayTriggerPlayableShowcaseRoot FindUniqueSceneRoot(
            Scene scene,
            out int count)
        {
            GameplayTriggerPlayableShowcaseRoot match = null;
            count = 0;
            GameplayTriggerPlayableShowcaseRoot[] roots =
                Object.FindObjectsByType<GameplayTriggerPlayableShowcaseRoot>(
                    FindObjectsInactive.Include);
            for (int i = 0; i < roots.Length; i++)
            {
                GameplayTriggerPlayableShowcaseRoot candidate = roots[i];
                if (candidate.gameObject.scene.handle != scene.handle)
                    continue;
                match = candidate;
                count++;
            }
            return count == 1 ? match : null;
        }

        private static Transform FindUniqueSceneTransform(
            Scene scene,
            string name,
            out int count)
        {
            Transform match = null;
            count = 0;
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate.gameObject.scene.handle != scene.handle ||
                    !string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    continue;
                }
                match = candidate;
                count++;
            }
            return count == 1 ? match : null;
        }

        private IEnumerator Tap(
            GameplayTriggerPlayableShowcaseRoot root,
            KeyControl key)
        {
            long beforePressFrame = root.Snapshot.Frame;
            Press(key);
            yield return WaitForLogicalFrameChange(
                root,
                beforePressFrame,
                key.name + " press was not sampled by a logical Tick.");
            long beforeReleaseFrame = root.Snapshot.Frame;
            Release(key);
            yield return WaitForLogicalFrameChange(
                root,
                beforeReleaseFrame,
                key.name + " release was not followed by a logical Tick.");
        }

        private static IEnumerator WaitForSnapshot(
            GameplayTriggerPlayableShowcaseRoot root,
            Func<GameplayTriggerPlayableSnapshot, bool> predicate,
            string failure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble +
                BehaviorTimeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (predicate(root.Snapshot))
                    yield break;
                yield return null;
            }

            Assert.That(
                predicate(root.Snapshot),
                Is.True,
                failure + " Last logical frame: " + root.Snapshot.Frame + ".");
        }

        private static IEnumerator VerifyPointerHeldMove(
            GameplayTriggerPlayableShowcaseRoot root,
            Button button,
            bool useX,
            int direction)
        {
            long beforeAxis = Axis(root.Snapshot, useX);
            long beforeFrame = root.Snapshot.Frame;
            bool released = false;
            SendPointerDownAndAssertDispatch(button);
            try
            {
                yield return WaitForLogicalFrameChange(
                    root,
                    beforeFrame,
                    button.name +
                    " PointerDown dispatched but no logical Tick advanced.");
                long firstAxis = Axis(root.Snapshot, useX);
                AssertDirectionalDelta(
                    beforeAxis,
                    firstAxis,
                    direction,
                    button.name + " did not produce held movement.");

                long firstFrame = root.Snapshot.Frame;
                yield return WaitForLogicalFrameChange(
                    root,
                    firstFrame,
                    button.name + " held state did not reach a second logical Tick.");
                long secondAxis = Axis(root.Snapshot, useX);
                AssertDirectionalDelta(
                    firstAxis,
                    secondAxis,
                    direction,
                    button.name + " did not remain held for a second logical Tick.");

                SendPointerUpAndAssertDispatchAndCaptureOut(button);
                released = true;
                yield return AssertReleaseAllowsAtMostOnePulse(
                    root,
                    useX,
                    secondAxis,
                    direction,
                    button.name + " continued moving after PointerUp.");
            }
            finally
            {
                if (!released &&
                    button.HasPointerCapture(PointerId.mousePointerId))
                    button.ReleasePointer(PointerId.mousePointerId);
            }
        }

        private static IEnumerator VerifyPointerCaptureOutStopsMove(
            GameplayTriggerPlayableShowcaseRoot root,
            Button button,
            bool useX,
            int direction)
        {
            long beforeAxis = Axis(root.Snapshot, useX);
            long beforeFrame = root.Snapshot.Frame;
            bool released = false;
            SendPointerDownAndAssertDispatch(button);
            try
            {
                yield return WaitForLogicalFrameChange(
                    root,
                    beforeFrame,
                    "PointerDown dispatched before capture-out but no logical Tick advanced.");
                long movedAxis = Axis(root.Snapshot, useX);
                AssertDirectionalDelta(
                    beforeAxis,
                    movedAxis,
                    direction,
                    "PointerDown did not begin held movement before capture-out.");

                yield return ReleasePointerAndAssertCaptureOut(button);
                released = true;
                yield return AssertAxisStableForLogicalFrames(
                    root,
                    useX,
                    movedAxis,
                    logicalFrameCount: 2,
                    "PointerCaptureOut did not stop held movement.");
            }
            finally
            {
                if (!released &&
                    button.HasPointerCapture(PointerId.mousePointerId))
                    button.ReleasePointer(PointerId.mousePointerId);
            }
        }

        private static IEnumerator AssertAxisStableForLogicalFrames(
            GameplayTriggerPlayableShowcaseRoot root,
            bool useX,
            long expected,
            int logicalFrameCount,
            string failure)
        {
            for (int i = 0; i < logicalFrameCount; i++)
            {
                long beforeFrame = root.Snapshot.Frame;
                yield return WaitForLogicalFrameChange(root, beforeFrame, failure);
                Assert.That(Axis(root.Snapshot, useX), Is.EqualTo(expected), failure);
            }
        }

        private static IEnumerator AssertReleaseAllowsAtMostOnePulse(
            GameplayTriggerPlayableShowcaseRoot root,
            bool useX,
            long beforeRelease,
            int direction,
            string failure)
        {
            long beforeFrame = root.Snapshot.Frame;
            yield return WaitForLogicalFrameChange(root, beforeFrame, failure);
            long afterOptionalPulse = Axis(root.Snapshot, useX);
            long delta = afterOptionalPulse - beforeRelease;
            Assert.That(
                delta == 0L || (direction > 0 ? delta > 0L : delta < 0L),
                Is.True,
                "PointerUp produced movement opposite to its optional click pulse.");
            yield return AssertAxisStableForLogicalFrames(
                root,
                useX,
                afterOptionalPulse,
                logicalFrameCount: 2,
                failure);
        }

        private static IEnumerator AssertSingleAxisPulse(
            GameplayTriggerPlayableShowcaseRoot root,
            bool useX,
            long moved)
        {
            yield return AssertAxisStableForLogicalFrames(
                root,
                useX,
                moved,
                logicalFrameCount: 2,
                "NavigationSubmit produced movement after its first consumed pulse.");
        }

        private static IEnumerator WaitForLogicalFrameChange(
            GameplayTriggerPlayableShowcaseRoot root,
            long previousFrame,
            string failure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble +
                LogicalFrameTimeoutSeconds;
            while (root.Snapshot.Frame == previousFrame &&
                   Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.That(
                root.Snapshot.Frame,
                Is.Not.EqualTo(previousFrame),
                failure + " Frame remained " + previousFrame + ".");
        }

        private static void AssertDirectionalDelta(
            long before,
            long after,
            int direction,
            string failure)
        {
            long delta = after - before;
            Assert.That(direction > 0 ? delta > 0L : delta < 0L, Is.True, failure);
        }

        private static bool SavedAuthorityMatches(
            GameplayTriggerPlayableSnapshot actual,
            GameplayTriggerPlayableSnapshot captured)
        {
            return actual.PlayerXRaw == captured.PlayerXRaw &&
                   actual.PlayerZRaw == captured.PlayerZRaw &&
                   actual.CheckpointAccepted == captured.CheckpointAccepted &&
                   actual.DoorUnlocked == captured.DoorUnlocked &&
                   actual.DoorOpen == captured.DoorOpen &&
                   actual.TrapActivations == captured.TrapActivations &&
                   actual.SlowFramesRemaining == captured.SlowFramesRemaining &&
                   actual.CombatBodyCount == captured.CombatBodyCount &&
                   actual.CombatColliderCount == captured.CombatColliderCount;
        }

        private static void AssertProjectionConsistent(
            PlayableContext context,
            string phase)
        {
            GameplayTriggerPlayableSnapshot snapshot = context.Root.Snapshot;
            Assert.That(
                context.PlayerView.position.x,
                Is.EqualTo((float)snapshot.PlayerXRaw / 1000000f).Within(0.0001f),
                phase + " player X projection");
            Assert.That(
                context.PlayerView.position.z,
                Is.EqualTo((float)snapshot.PlayerZRaw / 1000000f).Within(0.0001f),
                phase + " player Z projection");
            Assert.That(
                context.DoorView.gameObject.activeSelf,
                Is.EqualTo(!snapshot.DoorOpen),
                phase + " door projection");

            AssertLabel(
                context,
                "checkpoint-label",
                snapshot.CheckpointAccepted ? "ACQUIRED" : "PENDING",
                phase);
            AssertLabel(
                context,
                "door-label",
                snapshot.DoorOpen
                    ? "OPEN"
                    : snapshot.DoorUnlocked ? "UNLOCKED" : "LOCKED",
                phase);
            AssertLabel(
                context,
                "trap-label",
                "Activations " + snapshot.TrapActivations +
                "  Slow " + snapshot.SlowFramesRemaining + "f",
                phase);
            AssertLabel(
                context,
                "save-label",
                snapshot.HasCapture
                    ? "Captured end-F " + snapshot.CapturedFrame +
                      (snapshot.LastControlSucceeded
                          ? " / ready"
                          : " / last control rejected")
                    : snapshot.LastControlSucceeded
                        ? "No capture"
                        : "Capture required / rejected",
                phase);
        }

        private static void AssertLabel(
            PlayableContext context,
            string name,
            string expected,
            string phase)
        {
            Label label = context.Document.rootVisualElement.Q<Label>(name);
            Assert.That(label, Is.Not.Null, phase + " " + name);
            Assert.That(label.text, Is.EqualTo(expected), phase + " " + name);
        }

        private static IEnumerator SettleInput(
            GameplayTriggerPlayableShowcaseRoot root)
        {
            long expectedX = root.Snapshot.PlayerXRaw;
            long expectedZ = root.Snapshot.PlayerZRaw;
            for (int i = 0; i < 2; i++)
            {
                long beforeFrame = root.Snapshot.Frame;
                yield return WaitForLogicalFrameChange(
                    root,
                    beforeFrame,
                    "Released keyboard input was not followed by a logical Tick.");
                Assert.That(
                    root.Snapshot.PlayerXRaw,
                    Is.EqualTo(expectedX),
                    "Released keyboard input continued moving on X.");
                Assert.That(
                    root.Snapshot.PlayerZRaw,
                    Is.EqualTo(expectedZ),
                    "Released keyboard input continued moving on Z.");
            }
        }

        private static long Axis(
            in GameplayTriggerPlayableSnapshot snapshot,
            bool useX)
        {
            return useX ? snapshot.PlayerXRaw : snapshot.PlayerZRaw;
        }

        private static Button RequireButton(PlayableContext context, string name)
        {
            Button button = context.Document.rootVisualElement.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            return button;
        }

        private static void SendPointerDown(Button button)
        {
            VisualElement visualTree = RequirePanelVisualTree(button);
            VisualElement picked = RequirePickedButtonTarget(button);
            var systemEvent = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = button.worldBound.center
            };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(systemEvent))
            {
                evt.target = picked;
                visualTree.SendEvent(evt);
            }
        }

        private static void SendPointerDownAndAssertDispatch(Button button)
        {
            bool observed = false;
            int observedButton = -1;
            object observedCurrentTarget = null;
            EventCallback<PointerDownEvent> probe = evt =>
            {
                observed = true;
                observedButton = evt.button;
                observedCurrentTarget = evt.currentTarget;
            };
            button.RegisterCallback(probe, TrickleDown.TrickleDown);
            try
            {
                SendPointerDown(button);
                Assert.That(
                    observed,
                    Is.True,
                    button.name +
                    " PointerDown was not dispatched at the target button.");
                Assert.That(observedButton, Is.Zero, button.name + " PointerDown button.");
                Assert.That(
                    observedCurrentTarget,
                    Is.SameAs(button),
                    button.name + " PointerDown currentTarget.");
                Assert.That(
                    button.HasPointerCapture(PointerId.mousePointerId),
                    Is.True,
                    button.name + " did not capture the mouse pointer on PointerDown.");
            }
            finally
            {
                button.UnregisterCallback(probe, TrickleDown.TrickleDown);
            }
        }

        private static void SendPointerUp(Button button)
        {
            VisualElement visualTree = RequirePanelVisualTree(button);
            VisualElement picked = RequirePickedButtonTarget(button);
            var systemEvent = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = button.worldBound.center
            };
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(systemEvent))
            {
                evt.target = picked;
                visualTree.SendEvent(evt);
            }
        }

        private static void SendPointerMove(Button button)
        {
            VisualElement visualTree = RequirePanelVisualTree(button);
            VisualElement picked = RequirePickedButtonTarget(button);
            var systemEvent = new Event
            {
                type = EventType.MouseMove,
                mousePosition = button.worldBound.center
            };
            using (PointerMoveEvent evt = PointerMoveEvent.GetPooled(systemEvent))
            {
                evt.target = picked;
                visualTree.SendEvent(evt);
            }
        }

        private static void SendPointerUpAndAssertDispatchAndCaptureOut(
            Button button)
        {
            bool upObserved = false;
            int observedButton = -1;
            object upCurrentTarget = null;
            bool captureOutObserved = false;
            object captureOutCurrentTarget = null;
            EventCallback<PointerUpEvent> upProbe = evt =>
            {
                upObserved = true;
                observedButton = evt.button;
                upCurrentTarget = evt.currentTarget;
            };
            EventCallback<PointerCaptureOutEvent> captureOutProbe = evt =>
            {
                captureOutObserved = true;
                captureOutCurrentTarget = evt.currentTarget;
            };
            button.RegisterCallback(upProbe);
            button.RegisterCallback(captureOutProbe);
            try
            {
                SendPointerUp(button);
                Assert.That(
                    upObserved,
                    Is.True,
                    button.name +
                    " PointerUp was not dispatched at the target button.");
                Assert.That(observedButton, Is.Zero, button.name + " PointerUp button.");
                Assert.That(
                    upCurrentTarget,
                    Is.SameAs(button),
                    button.name + " PointerUp currentTarget.");
                Assert.That(
                    captureOutObserved,
                    Is.True,
                    button.name +
                    " normal PointerUp did not produce PointerCaptureOut.");
                Assert.That(
                    captureOutCurrentTarget,
                    Is.SameAs(button),
                    button.name + " PointerCaptureOut currentTarget.");
                Assert.That(
                    button.HasPointerCapture(PointerId.mousePointerId),
                    Is.False,
                    button.name + " retained mouse capture after PointerUp.");
            }
            finally
            {
                button.UnregisterCallback(captureOutProbe);
                button.UnregisterCallback(upProbe);
            }
        }

        private static IEnumerator ReleasePointerAndAssertCaptureOut(Button button)
        {
            bool observed = false;
            object observedCurrentTarget = null;
            EventCallback<PointerCaptureOutEvent> probe = evt =>
            {
                observed = true;
                observedCurrentTarget = evt.currentTarget;
            };
            button.RegisterCallback(probe);
            try
            {
                Assert.That(
                    button.HasPointerCapture(PointerId.mousePointerId),
                    Is.True,
                    button.name + " had no mouse pointer capture to release.");
                button.ReleasePointer(PointerId.mousePointerId);
                SendPointerMove(button);
                double deadline = Time.realtimeSinceStartupAsDouble +
                    BehaviorTimeoutSeconds;
                while (!observed &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }
                Assert.That(
                    observed,
                    Is.True,
                    button.name +
                    " ReleasePointer did not produce PointerCaptureOut.");
                Assert.That(
                    observedCurrentTarget,
                    Is.SameAs(button),
                    button.name + " released PointerCaptureOut currentTarget.");
                Assert.That(
                    button.HasPointerCapture(PointerId.mousePointerId),
                    Is.False,
                    button.name + " retained mouse capture after ReleasePointer.");
            }
            finally
            {
                button.UnregisterCallback(probe);
            }
        }

        private static void SendNavigationSubmitAndAssertDispatch(Button button)
        {
            bool observed = false;
            object observedCurrentTarget = null;
            EventCallback<NavigationSubmitEvent> probe = evt =>
            {
                observed = true;
                observedCurrentTarget = evt.currentTarget;
            };
            button.RegisterCallback(probe);
            try
            {
                VisualElement visualTree = RequirePanelVisualTree(button);
                button.Focus();
                Assert.That(
                    button.panel.focusController.focusedElement,
                    Is.SameAs(button),
                    button.name + " did not receive focus before NavigationSubmit.");
                using (NavigationSubmitEvent evt =
                       NavigationSubmitEvent.GetPooled())
                {
                    evt.target = button;
                    visualTree.SendEvent(evt);
                }
                Assert.That(
                    observed,
                    Is.True,
                    button.name +
                    " NavigationSubmit was not dispatched at the target button.");
                Assert.That(
                    observedCurrentTarget,
                    Is.SameAs(button),
                    button.name + " NavigationSubmit currentTarget.");
            }
            finally
            {
                button.UnregisterCallback(probe);
            }
        }

        private static VisualElement RequirePanelVisualTree(Button button)
        {
            Assert.That(button.panel, Is.Not.Null, button.name + " panel.");
            Assert.That(
                button.panel.visualTree,
                Is.Not.Null,
                button.name + " panel visual tree.");
            return button.panel.visualTree;
        }

        private static VisualElement RequirePickedButtonTarget(Button button)
        {
            VisualElement picked = button.panel.Pick(button.worldBound.center);
            Assert.That(
                picked,
                Is.Not.Null,
                button.name + " was not picked at its world-bound center.");
            Assert.That(
                ReferenceEquals(picked, button) || button.Contains(picked),
                Is.True,
                button.name + " pick did not resolve to the button or a descendant.");
            return picked;
        }

        private sealed class PlayableContext
        {
            internal GameplayTriggerPlayableShowcaseRoot Root;
            internal UIDocument Document;
            internal InputService InputService;
            internal Transform PlayerView;
            internal Transform DoorView;
        }
    }
#endif
}
