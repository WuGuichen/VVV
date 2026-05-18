using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.CharacterControl;
using MxFramework.Demo;
using MxFramework.Input;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Combat
{
    public sealed class RuntimeCombatShowcaseRunnerTests
    {
        private GameObject _runnerObject;
        private GameObject _playerMarker;
        private GameObject _enemyMarker;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_runnerObject);
            UnityEngine.Object.DestroyImmediate(_playerMarker);
            UnityEngine.Object.DestroyImmediate(_enemyMarker);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ApplyAuthoringPreviewConfig_UpdatesSummaryAndEventLogWithoutEditorDependency()
        {
            _playerMarker = new GameObject("Test_Player_Marker");
            _enemyMarker = new GameObject("Test_Enemy_Marker");
            _enemyMarker.transform.position = new Vector3(2f, 0f, 0f);
            _runnerObject = new GameObject("RuntimeCombatShowcaseRunnerTest");
            RuntimeCombatShowcaseRunner runner = _runnerObject.AddComponent<RuntimeCombatShowcaseRunner>();

            var config = new RuntimeCombatShowcaseAuthoringConfig(
                "Authoring Preview: ActionAsset / BindingAsset",
                actionId: 9001,
                traceId: 77,
                playerMarker: _playerMarker.transform,
                enemyMarker: _enemyMarker.transform,
                "validation: 通过，有 warning=1",
                "Player marker: Test_Player_Marker");

            runner.ApplyAuthoringPreviewConfig(config);

            Assert.That(runner.AuthoringPreviewSummary, Does.Contain("ActionId=9001"));
            Assert.That(runner.AuthoringPreviewSummary, Does.Contain("TraceId=77"));
            Assert.That(runner.EventLog, Has.Some.Contains("Authoring Preview: ActionAsset / BindingAsset"));
            Assert.That(runner.EventLog, Has.Some.Contains("validation: 通过"));
            Assert.That(runner.PlayerMarker, Is.EqualTo(_playerMarker.transform));
            Assert.That(runner.EnemyMarker, Is.EqualTo(_enemyMarker.transform));
            Assert.That(runner.LastSnapshot, Is.Not.Null);
        }

        [Test]
        public void PhysicsPlayground_AttackProbeShapeAndRound_ArePlayable()
        {
            _playerMarker = new GameObject("Combat_Player_Marker");
            _enemyMarker = new GameObject("Combat_Enemy_Marker");
            _enemyMarker.transform.position = new Vector3(2f, 0f, 0f);
            _runnerObject = new GameObject("RuntimeCombatShowcaseRunnerTest");
            RuntimeCombatShowcaseRunner runner = _runnerObject.AddComponent<RuntimeCombatShowcaseRunner>();
            runner.ApplyAuthoringPreviewConfig(new RuntimeCombatShowcaseAuthoringConfig(
                "Physics Playground Test",
                actionId: 400001,
                traceId: 7,
                playerMarker: _playerMarker.transform,
                enemyMarker: _enemyMarker.transform,
                "validation: test",
                "markers: test"));

            Assert.AreEqual("Capsule", runner.QueryShapeName);
            Assert.That(runner.PhysicsPlaygroundSummary, Does.Contain("Round 1"));

            runner.ProbeFromSelected();
            Assert.Greater(runner.QueryCount, 0);
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("Capsule"));
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit="));

            int hpBefore = runner.EnemyHp;
            runner.AttackFromSelected();
            Assert.Less(runner.EnemyHp, hpBefore);
            Assert.Greater(runner.Score, 0);
            Assert.AreEqual(1, runner.Streak);

            runner.CycleQueryShape();
            Assert.AreEqual("Ray", runner.QueryShapeName);
            runner.ProbeFromSelected();
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("Ray"));

            for (int i = 0; i < 8 && runner.Round == 1; i++)
                runner.AttackFromSelected();

            Assert.GreaterOrEqual(runner.Round, 2);
            Assert.That(runner.EventLog, Has.Some.Contains("Round 2 started"));
        }

        [Test]
        public void PhysicsPlayground_MoveThenProbeAndAttack_UsesUpdatedBodyPosition()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(5f, 0f, 0f));

            SetQueryShape(runner, RuntimeCombatQueryShapeMode.Sphere);

            runner.ProbeFromSelected();
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("Sphere"));
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit=0"));
            Assert.That(runner.InteractionSummary, Does.Contain("miss"));

            runner.MoveSelectedTo(new Vector3(2f, 0f, 0f));
            Assert.That(runner.PhysicsBindingSummary, Does.Contain("P("));

            runner.ProbeFromSelected();
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("Sphere"));
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit=1"));
            Assert.That(runner.InteractionSummary, Does.Contain("hit"));

            int hpBefore = runner.EnemyHp;
            runner.AttackFromSelected();

            Assert.Less(runner.EnemyHp, hpBefore);
            Assert.Greater(runner.Score, 0);
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit=1"));
        }

        [Test]
        public void ResetShowcase_ReplaysSameMiniGameSequenceDeterministically()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(5f, 0f, 0f));

            ShowcaseReplayState first = RunDeterministicMiniGameSequence(runner);
            ShowcaseReplayState second = RunDeterministicMiniGameSequence(runner);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void MotionShowcaseApi_ExposesStepHudSummaryAndResetDeterminismContracts()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(5f, 0f, 0f));

            Type runnerType = typeof(RuntimeCombatShowcaseRunner);
            MethodInfo stepMotion = FindMotionStepMethod(runnerType, "StepPlayerMotion", "StepMotion", "StepMotionFrame", "StepMotionShowcase", "ApplyMotionStep");
            PropertyInfo summary = FindProperty(runnerType, "MotionSummary", "MotionPlaygroundSummary", "MotionShowcaseSummary");

            var missing = new List<string>();
            if (stepMotion == null)
                missing.Add("StepPlayerMotion/StepMotion/StepMotionFrame/StepMotionShowcase/ApplyMotionStep with zero args or (Vector3,bool)");
            if (summary == null)
                missing.Add("MotionSummary/MotionPlaygroundSummary/MotionShowcaseSummary");

            if (missing.Count > 0)
            {
                Assert.Ignore("Combat Motion Showcase runner API is not exposed yet. Missing: " + string.Join(", ", missing));
            }

            SetQueryShape(runner, RuntimeCombatQueryShapeMode.Sphere);
            runner.ProbeFromSelected();
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit=0"));

            RunMotionSteps(runner, stepMotion, Vector3.right, jumpPressed: false, count: 60);

            string summaryText = Convert.ToString(summary.GetValue(runner));
            Assert.That(summaryText, Does.Contain("capsule").IgnoreCase);
            Assert.That(summaryText, Does.Contain("pos").IgnoreCase);
            Assert.That(summaryText, Does.Contain("vel").IgnoreCase);
            Assert.That(summaryText, Does.Contain("grounded").IgnoreCase);
            Assert.That(summaryText, Does.Contain("flags").IgnoreCase);
            Assert.That(summaryText, Does.Contain("body").IgnoreCase);

            runner.ProbeFromSelected();
            Assert.That(runner.LastQueryDebugSummary, Does.Contain("hit=1"));

            int hpBefore = runner.EnemyHp;
            runner.AttackFromSelected();
            Assert.Less(runner.EnemyHp, hpBefore);

            MotionApiSnapshot first = RunDeterministicMotionSequence(runner, stepMotion, summary);
            runner.ResetShowcase();
            MotionApiSnapshot second = RunDeterministicMotionSequence(runner, stepMotion, summary);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void ResetShowcase_ReusesSceneAuthoredMotionObstacles()
        {
            _runnerObject = new GameObject("RuntimeCombatShowcaseRunnerTest");
            var existingRoot = new GameObject("Combat_Motion_Obstacles");
            existingRoot.transform.SetParent(_runnerObject.transform, worldPositionStays: false);

            RuntimeCombatShowcaseRunner runner = _runnerObject.AddComponent<RuntimeCombatShowcaseRunner>();
            runner.ResetShowcase();

            int obstacleRootCount = 0;
            Transform reusedRoot = null;
            for (int i = 0; i < _runnerObject.transform.childCount; i++)
            {
                Transform child = _runnerObject.transform.GetChild(i);
                if (child.name == "Combat_Motion_Obstacles")
                {
                    obstacleRootCount++;
                    reusedRoot = child;
                }
            }

            Assert.AreEqual(1, obstacleRootCount, "Runtime motion visuals should reuse the scene-authored obstacle root instead of creating a duplicate ground set.");
            Assert.AreSame(existingRoot.transform, reusedRoot);
            Assert.IsNotNull(reusedRoot.Find("Motion_Ground"));
            Assert.IsNotNull(reusedRoot.Find("Motion_Wall_X"));
            Assert.IsNotNull(reusedRoot.Find("Motion_Ceiling"));
        }

        [Test]
        public void StepPlayerMotion_ZeroMoveAfterJump_StillAppliesGravityAndLands()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(5f, 0f, 0f));

            runner.StepPlayerMotion(Vector3.zero, jumpPressed: true);
            Assert.That(runner.MotionSummary, Does.Contain("grounded=False").Or.Contain("grounded=false"));

            for (int i = 0; i < 180 && !runner.MotionSummary.Contains("grounded=True") && !runner.MotionSummary.Contains("grounded=true"); i++)
                runner.StepPlayerMotion(Vector3.zero, jumpPressed: false);

            Assert.That(runner.MotionSummary, Does.Contain("grounded=True").Or.Contain("grounded=true"));
            Assert.That(runner.MotionSummary, Does.Contain("vel=(0.00,0.00,0.00)"));
        }

        [Test]
        public void CharacterControlInput_MovesJumpsAttacksAndRecordsDebugSnapshot()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));
            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            input.SetSnapshot(CreateInputSnapshot(move: Vector2.right, jumpPressed: true));

            Assert.IsTrue(runner.StepCharacterControlFromInput(input));
            Assert.That(runner.CharacterControlSummary, Does.Contain("Local Input"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("jumpPressed: true"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("Motion"));

            int hpBefore = runner.EnemyHp;
            input.SetSnapshot(InputSnapshot.Empty);
            input.Commands.Enqueue(new InputCommand(
                runner.CurrentFrame.Value + 1L,
                sourceId: 0,
                InputIntent.DebugPrimary,
                traceId: "test-debug-primary"));

            Assert.IsTrue(runner.StepCharacterControlFromInput(input));

            Assert.Less(runner.EnemyHp, hpBefore);
            Assert.That(runner.CharacterControlSummary, Does.Contain("action success=True"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("Action"));
            Assert.That(runner.InteractionSummary, Does.Contain("CharacterControl Attack"));
        }

        [Test]
        public void CharacterControlInput_AllowsGameplayWhileDebugOverlayIsActive()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));
            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            input.PushContext(InputContext.Debug, InputContextPolicy.Overlay);
            input.SetSnapshot(CreateInputSnapshot(move: Vector2.right, jumpPressed: true));

            Assert.AreEqual(InputContext.Debug, input.CurrentContext);
            Assert.IsTrue(input.IsContextEnabled(InputContext.Gameplay));
            Assert.IsTrue(runner.StepCharacterControlFromInput(input));
            Assert.That(runner.CharacterControlSummary, Does.Contain("Local Input"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("jumpPressed: true"));

            int hpBefore = runner.EnemyHp;
            input.SetSnapshot(InputSnapshot.Empty);
            input.Commands.Enqueue(new InputCommand(
                runner.CurrentFrame.Value + 1L,
                sourceId: 0,
                InputIntent.DebugPrimary,
                traceId: "test-debug-overlay-primary"));

            Assert.IsTrue(runner.StepCharacterControlFromInput(input));
            Assert.Less(runner.EnemyHp, hpBefore);
            Assert.That(runner.InteractionSummary, Does.Contain("CharacterControl Attack"));
        }

        [Test]
        public void CharacterControlInput_ConsumesRuntimeAlignedBufferedJumpCommand()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));
            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            input.SetSnapshot(CreateInputSnapshot(move: Vector2.zero, jumpPressed: false));
            input.Commands.Enqueue(new InputCommand(
                runner.CurrentFrame.Value + 1L,
                sourceId: 0,
                InputIntent.Jump,
                traceId: "test-buffered-jump"));

            Assert.IsTrue(runner.StepCharacterControlFromInput(input));

            Assert.That(runner.CharacterControlDebugReport, Does.Contain("jumpPressed: true"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("jumpStarted: true"));
        }

        [Test]
        public void CharacterControlInput_IgnoresNonGameplayContext()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));
            var input = new FakeInputProvider();
            input.SetContext(InputContext.UI);
            input.SetSnapshot(CreateInputSnapshot(move: Vector2.right, jumpPressed: true));
            input.Commands.Enqueue(new InputCommand(
                runner.CurrentFrame.Value + 1L,
                sourceId: 0,
                InputIntent.DebugPrimary,
                traceId: "test-ui-debug-primary"));

            int hpBefore = runner.EnemyHp;

            Assert.IsFalse(runner.StepCharacterControlFromInput(input));

            Assert.AreEqual(hpBefore, runner.EnemyHp);
            Assert.That(runner.CharacterControlSummary, Does.Contain("command=none"));
            Assert.That(runner.CharacterControlGameplayCommandCount, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeAiPlannerCommand_AttacksAndRecordsCharacterControlDiagnostics()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));

            int hpBefore = runner.EnemyHp;

            Assert.IsTrue(runner.RunRuntimeAiPlannerCommand());

            Assert.Less(runner.EnemyHp, hpBefore);
            Assert.That(runner.CharacterControlSummary, Does.Contain("Runtime AI Planner"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("sourceId: 20002"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("showcase-attack"));
        }

        [Test]
        public void PressureBreakReaction_UsesStateMachineAnimationAndDebugSnapshot()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));

            CharacterPressureReactionResult result = runner.TriggerCharacterPressureBreak();

            Assert.IsTrue(result.ReactionStarted, result.Message);
            Assert.That(runner.CharacterControlSummary, Does.Contain("state=Reaction"));
            Assert.That(runner.CharacterControlSummary, Does.Contain("crossFade=1"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("PressureBreak"));
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("Critical -> Broken"));

            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            input.SetSnapshot(InputSnapshot.Empty);
            for (int i = 0; i < 13; i++)
                runner.StepCharacterControlFromInput(input);

            Assert.That(runner.CharacterControlSummary, Does.Contain("state=Locomotion"));
        }

        [Test]
        public void GameplayAbilityInput_DrainsRuntimeCommandBufferThroughSliceHost()
        {
            RuntimeCombatShowcaseRunner runner = CreateRunnerWithMarkers(
                playerPosition: Vector3.zero,
                enemyPosition: new Vector3(2f, 0f, 0f));
            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            input.Commands.Enqueue(new InputCommand(
                runner.CurrentFrame.Value + 1L,
                sourceId: 5,
                InputIntent.AttackSecondary,
                traceId: "test-gameplay-ability"));

            Assert.IsTrue(runner.StepCharacterControlFromInput(input));

            Assert.AreEqual(1, runner.CharacterControlGameplayCommandCount);
            Assert.That(runner.CharacterControlDebugReport, Does.Contain("GameplayCommandEnqueued"));
            Assert.That(runner.CharacterControlSummary, Does.Contain("gameplayCommands=1"));
        }

        [Test]
        public void RuntimeHudButtons_DoNotKeepKeyboardFocusForSpaceJump()
        {
            _runnerObject = new GameObject("RuntimeCombatShowcaseHudFocusTest");
            MxRuntimeHudController hud = _runnerObject.AddComponent<MxRuntimeHudController>();
            hud.ConfigureAssets(
                null,
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml"),
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/MxFramework/Showcase/GameplayShowcase.uss"));

            hud.SetHudCollapsed(false);

            UIDocument document = _runnerObject.GetComponent<UIDocument>();
            Assert.IsNotNull(document);

            var buttons = new List<Button>();
            document.rootVisualElement.Query<Button>().ForEach(buttons.Add);
            Assert.Greater(buttons.Count, 0, "HUD test expects the runtime HUD to create buttons.");
            for (int i = 0; i < buttons.Count; i++)
                Assert.IsFalse(buttons[i].focusable, buttons[i].name + " should not take focus; Space is reserved for combat jump.");

            UnityEngine.Object.DestroyImmediate(_runnerObject);
            _runnerObject = null;
        }

        private RuntimeCombatShowcaseRunner CreateRunnerWithMarkers(Vector3 playerPosition, Vector3 enemyPosition)
        {
            _playerMarker = new GameObject("Combat_Player_Marker");
            _playerMarker.transform.position = playerPosition;
            _enemyMarker = new GameObject("Combat_Enemy_Marker");
            _enemyMarker.transform.position = enemyPosition;
            _runnerObject = new GameObject("RuntimeCombatShowcaseRunnerTest");
            RuntimeCombatShowcaseRunner runner = _runnerObject.AddComponent<RuntimeCombatShowcaseRunner>();
            runner.ApplyAuthoringPreviewConfig(new RuntimeCombatShowcaseAuthoringConfig(
                "Physics Playground Test",
                actionId: 400001,
                traceId: 7,
                playerMarker: _playerMarker.transform,
                enemyMarker: _enemyMarker.transform,
                "validation: test",
                "markers: test"));

            return runner;
        }

        private static ShowcaseReplayState RunDeterministicMiniGameSequence(RuntimeCombatShowcaseRunner runner)
        {
            runner.PlayerMarker.position = Vector3.zero;
            runner.EnemyMarker.position = new Vector3(5f, 0f, 0f);
            runner.ResetShowcase();
            SetQueryShape(runner, RuntimeCombatQueryShapeMode.Sphere);
            runner.ProbeFromSelected();
            runner.MoveSelectedTo(new Vector3(2f, 0f, 0f));
            runner.ProbeFromSelected();
            runner.AttackFromSelected();
            runner.StepFrame();

            return new ShowcaseReplayState(
                runner.CurrentFrame.Value,
                runner.EnemyHp,
                runner.Score,
                runner.Round,
                runner.Streak,
                runner.QueryCount,
                runner.HitCount,
                runner.PhysicsPlaygroundSummary,
                runner.LastQueryDebugSummary,
                runner.InteractionSummary,
                runner.LastSnapshot != null ? runner.LastSnapshot.FrameHash.ToString() : string.Empty);
        }

        private static void SetQueryShape(RuntimeCombatShowcaseRunner runner, RuntimeCombatQueryShapeMode shapeMode)
        {
            for (int i = 0; i < 5 && runner.QueryShapeMode != shapeMode; i++)
                runner.CycleQueryShape();

            Assert.AreEqual(shapeMode, runner.QueryShapeMode);
        }

        private static MethodInfo FindMotionStepMethod(Type type, params string[] names)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;
            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo method = type.GetMethod(names[i], Flags);
                if (method == null)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    return method;

                if (parameters.Length == 2
                    && parameters[0].ParameterType == typeof(Vector3)
                    && parameters[1].ParameterType == typeof(bool))
                    return method;
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, params string[] names)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo property = type.GetProperty(names[i], Flags);
                if (property != null)
                    return property;
            }

            return null;
        }

        private static void InvokeMotionStep(
            RuntimeCombatShowcaseRunner runner,
            MethodInfo stepMotion,
            Vector3 moveDirection,
            bool jumpPressed)
        {
            ParameterInfo[] parameters = stepMotion.GetParameters();
            if (parameters.Length == 0)
            {
                stepMotion.Invoke(runner, Array.Empty<object>());
                return;
            }

            stepMotion.Invoke(runner, new object[] { moveDirection, jumpPressed });
        }

        private static void RunMotionSteps(
            RuntimeCombatShowcaseRunner runner,
            MethodInfo stepMotion,
            Vector3 moveDirection,
            bool jumpPressed,
            int count)
        {
            for (int i = 0; i < count; i++)
                InvokeMotionStep(runner, stepMotion, moveDirection, jumpPressed && i == 0);
        }

        private static MotionApiSnapshot RunDeterministicMotionSequence(
            RuntimeCombatShowcaseRunner runner,
            MethodInfo stepMotion,
            PropertyInfo summary)
        {
            runner.PlayerMarker.position = Vector3.zero;
            runner.EnemyMarker.position = new Vector3(5f, 0f, 0f);
            runner.ResetShowcase();
            SetQueryShape(runner, RuntimeCombatQueryShapeMode.Sphere);
            RunMotionSteps(runner, stepMotion, Vector3.right, jumpPressed: false, count: 60);
            runner.ProbeFromSelected();

            return new MotionApiSnapshot(
                NormalizeMotionSummary(Convert.ToString(summary.GetValue(runner))),
                runner.PhysicsPlaygroundSummary,
                runner.LastQueryDebugSummary,
                runner.InteractionSummary,
                runner.CurrentFrame.Value,
                runner.QueryCount,
                runner.LastSnapshot != null ? runner.LastSnapshot.FrameHash.ToString() : string.Empty);
        }

        private static string NormalizeMotionSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
                return summary;

            int revisionIndex = summary.LastIndexOf(" rev=", StringComparison.Ordinal);
            return revisionIndex < 0 ? summary : summary.Substring(0, revisionIndex);
        }

        private static InputSnapshot CreateInputSnapshot(
            Vector2 move = default,
            bool jumpPressed = false)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed,
                jumpHeld: jumpPressed,
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
        }

        private readonly struct ShowcaseReplayState : IEquatable<ShowcaseReplayState>
        {
            public ShowcaseReplayState(
                int frame,
                int enemyHp,
                int score,
                int round,
                int streak,
                int queryCount,
                int hitCount,
                string summary,
                string queryDebugSummary,
                string interactionSummary,
                string frameHash)
            {
                Frame = frame;
                EnemyHp = enemyHp;
                Score = score;
                Round = round;
                Streak = streak;
                QueryCount = queryCount;
                HitCount = hitCount;
                Summary = summary;
                QueryDebugSummary = queryDebugSummary;
                InteractionSummary = interactionSummary;
                FrameHash = frameHash;
            }

            private int Frame { get; }
            private int EnemyHp { get; }
            private int Score { get; }
            private int Round { get; }
            private int Streak { get; }
            private int QueryCount { get; }
            private int HitCount { get; }
            private string Summary { get; }
            private string QueryDebugSummary { get; }
            private string InteractionSummary { get; }
            private string FrameHash { get; }

            public bool Equals(ShowcaseReplayState other)
            {
                return Frame == other.Frame
                    && EnemyHp == other.EnemyHp
                    && Score == other.Score
                    && Round == other.Round
                    && Streak == other.Streak
                    && QueryCount == other.QueryCount
                    && HitCount == other.HitCount
                    && string.Equals(Summary, other.Summary, StringComparison.Ordinal)
                    && string.Equals(QueryDebugSummary, other.QueryDebugSummary, StringComparison.Ordinal)
                    && string.Equals(InteractionSummary, other.InteractionSummary, StringComparison.Ordinal)
                    && string.Equals(FrameHash, other.FrameHash, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ShowcaseReplayState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Frame;
                    hash = (hash * 397) ^ EnemyHp;
                    hash = (hash * 397) ^ Score;
                    hash = (hash * 397) ^ Round;
                    hash = (hash * 397) ^ Streak;
                    hash = (hash * 397) ^ QueryCount;
                    hash = (hash * 397) ^ HitCount;
                    hash = (hash * 397) ^ (Summary != null ? Summary.GetHashCode() : 0);
                    hash = (hash * 397) ^ (QueryDebugSummary != null ? QueryDebugSummary.GetHashCode() : 0);
                    hash = (hash * 397) ^ (InteractionSummary != null ? InteractionSummary.GetHashCode() : 0);
                    hash = (hash * 397) ^ (FrameHash != null ? FrameHash.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        private readonly struct MotionApiSnapshot : IEquatable<MotionApiSnapshot>
        {
            public MotionApiSnapshot(
                string motionSummary,
                string playgroundSummary,
                string queryDebugSummary,
                string interactionSummary,
                int frame,
                int queryCount,
                string frameHash)
            {
                MotionSummary = motionSummary;
                PlaygroundSummary = playgroundSummary;
                QueryDebugSummary = queryDebugSummary;
                InteractionSummary = interactionSummary;
                Frame = frame;
                QueryCount = queryCount;
                FrameHash = frameHash;
            }

            private string MotionSummary { get; }
            private string PlaygroundSummary { get; }
            private string QueryDebugSummary { get; }
            private string InteractionSummary { get; }
            private int Frame { get; }
            private int QueryCount { get; }
            private string FrameHash { get; }

            public bool Equals(MotionApiSnapshot other)
            {
                return string.Equals(MotionSummary, other.MotionSummary, StringComparison.Ordinal)
                    && string.Equals(PlaygroundSummary, other.PlaygroundSummary, StringComparison.Ordinal)
                    && string.Equals(QueryDebugSummary, other.QueryDebugSummary, StringComparison.Ordinal)
                    && string.Equals(InteractionSummary, other.InteractionSummary, StringComparison.Ordinal)
                    && Frame == other.Frame
                    && QueryCount == other.QueryCount
                    && string.Equals(FrameHash, other.FrameHash, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is MotionApiSnapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = MotionSummary != null ? MotionSummary.GetHashCode() : 0;
                    hash = (hash * 397) ^ (PlaygroundSummary != null ? PlaygroundSummary.GetHashCode() : 0);
                    hash = (hash * 397) ^ (QueryDebugSummary != null ? QueryDebugSummary.GetHashCode() : 0);
                    hash = (hash * 397) ^ (InteractionSummary != null ? InteractionSummary.GetHashCode() : 0);
                    hash = (hash * 397) ^ Frame;
                    hash = (hash * 397) ^ QueryCount;
                    hash = (hash * 397) ^ (FrameHash != null ? FrameHash.GetHashCode() : 0);
                    return hash;
                }
            }
        }
    }
}
