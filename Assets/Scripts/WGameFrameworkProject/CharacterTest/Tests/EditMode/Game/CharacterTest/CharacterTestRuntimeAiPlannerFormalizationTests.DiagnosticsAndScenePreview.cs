using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {

        [Test]
        public void RuntimeAiPlannerEditorRegistry_RegistersMockTargetForPlayMonitor()
        {
            CharacterTestRuntimeAiPlannerEditorDebugRegistry.Register(
                "mock",
                () => new CharacterTestRuntimeAiPlannerSnapshot(
                    available: true,
                    profileId: "mock-profile",
                    agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                    validationReport: "ok"));

            Assert.AreEqual(1, CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets.Count);
            CharacterTestRuntimeAiPlannerEditorDebugTarget target =
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets[0];
            Assert.AreEqual("mock", target.Name);
            Assert.IsTrue(target.IsAvailable);
            Assert.AreEqual("mock-profile", target.SnapshotProvider().ProfileId);
        }

        [Test]
        public void RuntimeAiPlannerEditorRegistry_ExposesRuntimeBehaviorLoadoutControls()
        {
            bool unlocked = false;
            bool panelOpen = false;
            int unlockCalls = 0;
            int openCalls = 0;
            int closeCalls = 0;
            CharacterTestRuntimeAiPlannerEditorDebugRegistry.Register(
                "mock",
                () => new CharacterTestRuntimeAiPlannerSnapshot(
                    available: true,
                    profileId: "mock-profile",
                    agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                    validationReport: "ok"),
                behaviorCompositionHotUpdateProvider: null,
                runtimeAiBehaviorSelectionUnlockedProvider: () => unlocked,
                setRuntimeAiBehaviorSelectionUnlocked: value =>
                {
                    unlocked = value;
                    unlockCalls++;
                },
                openRuntimeAiBehaviorLoadoutPanel: () =>
                {
                    panelOpen = true;
                    openCalls++;
                    return true;
                },
                closeRuntimeAiBehaviorLoadoutPanel: () =>
                {
                    panelOpen = false;
                    closeCalls++;
                    return true;
                },
                runtimeAiBehaviorLoadoutPanelOpenProvider: () => panelOpen,
                runtimeAiBehaviorLoadoutPanelStatusProvider: () => panelOpen ? "open" : "closed");

            CharacterTestRuntimeAiPlannerEditorDebugTarget target =
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets[0];

            Assert.IsTrue(target.HasRuntimeAiBehaviorSelectionControls);
            Assert.IsFalse(target.RuntimeAiBehaviorSelectionUnlocked);
            target.SetRuntimeAiBehaviorSelectionUnlocked(true);
            Assert.AreEqual(1, unlockCalls);
            Assert.IsTrue(target.RuntimeAiBehaviorSelectionUnlocked);

            Assert.IsTrue(target.CanOpenRuntimeAiBehaviorLoadoutPanel);
            Assert.IsTrue(target.OpenRuntimeAiBehaviorLoadoutPanel());
            Assert.AreEqual(1, openCalls);
            Assert.IsTrue(target.RuntimeAiBehaviorLoadoutPanelOpen);
            Assert.AreEqual("open", target.RuntimeAiBehaviorLoadoutPanelStatus);

            Assert.IsTrue(target.CanCloseRuntimeAiBehaviorLoadoutPanel);
            Assert.IsTrue(target.CloseRuntimeAiBehaviorLoadoutPanel());
            Assert.AreEqual(1, closeCalls);
            Assert.IsFalse(target.RuntimeAiBehaviorLoadoutPanelOpen);
            Assert.AreEqual("closed", target.RuntimeAiBehaviorLoadoutPanelStatus);
        }

        [Test]
        public void RuntimeAiPlannerPlayMonitorSnapshotCache_CapturesEachTargetOnce()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePlayMonitorAgentSnapshot();
            var targets = new[]
            {
                new CharacterTestRuntimeAiPlannerEditorDebugTarget(
                    "first",
                    () =>
                    {
                        firstCalls++;
                        return new CharacterTestRuntimeAiPlannerSnapshot(
                            available: true,
                            profileId: "profile-a",
                            agents: new[] { agent },
                            validationReport: "ok");
                    }),
                new CharacterTestRuntimeAiPlannerEditorDebugTarget(
                    "second",
                    () =>
                    {
                        secondCalls++;
                        return new CharacterTestRuntimeAiPlannerSnapshot(
                            available: true,
                            profileId: "profile-b",
                            agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                            validationReport: "ok");
                    })
            };
            var snapshotEntries = new List<CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry>();
            var agentEntries = new List<CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry>();

            CharacterTestRuntimeAiPlannerWindow.FillPlayMonitorSnapshotCache(
                targets,
                snapshotEntries,
                agentEntries);

            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
            Assert.AreEqual(2, snapshotEntries.Count);
            Assert.AreEqual("first", snapshotEntries[0].TargetName);
            Assert.AreEqual(1, snapshotEntries[0].AgentCount);
            Assert.AreEqual("second", snapshotEntries[1].TargetName);
            Assert.AreEqual(0, snapshotEntries[1].AgentCount);
            Assert.AreEqual(1, agentEntries.Count);
            Assert.AreEqual("first", agentEntries[0].TargetName);
            Assert.AreEqual("Agent 7", agentEntries[0].ViewModel.AgentText);
            Assert.AreEqual("#3", agentEntries[0].ViewModel.TargetText);
        }

        [Test]
        public void RuntimeAiPlannerPlayMonitorViewModel_FormatsAgentSummaryRow()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePlayMonitorAgentSnapshot();

            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel =
                CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel.Create("mock", agent);

            Assert.AreEqual("mock", viewModel.SourceName);
            Assert.AreEqual("Agent 7", viewModel.AgentText);
            Assert.AreEqual("root/cast", viewModel.StateText);
            Assert.AreEqual("#3", viewModel.TargetText);
            StringAssert.Contains("Cast ability #120", viewModel.ActionText);
            Assert.AreEqual("2.05", viewModel.DistanceText);
            Assert.AreEqual("可见 / 通畅", viewModel.VisibilityText);
            Assert.AreEqual("路径可用 / 直线", viewModel.PathText);
            Assert.AreEqual("9.947 / 1.2", viewModel.ScoreCostText);
            Assert.AreEqual("SetControlIntent", viewModel.CommandText);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Normal, viewModel.Severity);
            Assert.IsFalse(viewModel.HasIssue);
            StringAssert.Contains("Utility 选择 Cast ability", viewModel.DecisionHeadline);
        }

        [Test]
        public void RuntimeAiPlannerPlayMonitorViewModel_FormatsInspectorSectionsAndRawFallback()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePlayMonitorAgentSnapshot(
                targetEntityId: -1,
                targetExists: false,
                targetVisible: false,
                targetLineOfSightBlocked: true,
                targetInAttackRange: false,
                movePathAvailable: false,
                moveDirectPathClear: false,
                selectedActionId: 0,
                selectedActionName: string.Empty,
                selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.Wait,
                lastCommand: string.Empty,
                rejectReason: "TargetMissing");

            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel =
                CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel.Create("mock", agent);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Error, viewModel.Severity);
            Assert.IsTrue(viewModel.HasIssue);
            StringAssert.Contains("命令被拒绝", viewModel.DecisionHeadline);
            StringAssert.Contains("TargetMissing", viewModel.IssueText);
            StringAssert.Contains("目标: none", viewModel.TargetDetailText);
            StringAssert.Contains("路径可用: false", viewModel.MovementDetailText);
            StringAssert.Contains("reject=TargetMissing", viewModel.CommandDetailText);
            StringAssert.Contains("target.exists=false", viewModel.RawFactsText);
            StringAssert.Contains("move.pathAvailable=false", viewModel.RawFactsText);
        }

        [Test]
        public void RuntimeAiPlannerAgentSnapshot_DefaultScenePreviewGeometryDisabled()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePlayMonitorAgentSnapshot();

            Assert.IsFalse(agent.HasScenePreviewGeometry);
            Assert.AreEqual(0f, agent.ScenePreviewSourceCenterX, 0.0001f);
            Assert.AreEqual(0f, agent.ScenePreviewTargetCenterX, 0.0001f);
            Assert.AreEqual(0f, agent.ScenePreviewTargetDirectionX, 0.0001f);
            Assert.AreEqual(0f, agent.ScenePreviewDecisionMoveX, 0.0001f);
            Assert.AreEqual(0f, agent.ScenePreviewAttackRange, 0.0001f);
        }

        [Test]
        public void RuntimeAiPlannerScenePreview_PreferencesDefaultOffAndRestoreMode()
        {
            CharacterTestRuntimeAiPlannerScenePreview.ResetPreferencesForTests();
            try
            {
                Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.Enabled);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent,
                    CharacterTestRuntimeAiPlannerScenePreview.Mode);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected,
                    CharacterTestRuntimeAiPlannerScenePreview.LabelMode);
                Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShowLegend);
                Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShowTargetLine);
                Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShowAttackRange);
                Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShowDecisionMove);

                CharacterTestRuntimeAiPlannerScenePreview.Enabled = true;
                CharacterTestRuntimeAiPlannerScenePreview.Mode =
                    CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents;
                CharacterTestRuntimeAiPlannerScenePreview.LabelMode =
                    CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues;
                CharacterTestRuntimeAiPlannerScenePreview.ShowLegend = false;
                CharacterTestRuntimeAiPlannerScenePreview.ShowTargetLine = false;
                CharacterTestRuntimeAiPlannerScenePreview.ShowAttackRange = false;
                CharacterTestRuntimeAiPlannerScenePreview.ShowDecisionMove = false;

                Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.Enabled);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                    CharacterTestRuntimeAiPlannerScenePreview.Mode);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues,
                    CharacterTestRuntimeAiPlannerScenePreview.LabelMode);
                Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShowLegend);
                Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShowTargetLine);
                Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShowAttackRange);
                Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShowDecisionMove);
            }
            finally
            {
                CharacterTestRuntimeAiPlannerScenePreview.ResetPreferencesForTests();
            }
        }

        [Test]
        public void RuntimeAiPlannerDevPreview_CalculatesDistanceAndFormatting()
        {
            var item = new CharacterTestRuntimeAiPlannerDevPreviewItem(
                available: true,
                CharacterTestRuntimeAiPlannerDevPreviewSeverity.Neutral,
                frame: 10,
                agentEntityId: 1,
                targetEntityId: 2,
                targetExists: true,
                hasGeometry: true,
                hasTargetLine: true,
                hasAttackRange: true,
                hasMoveArrow: false,
                targetVisible: true,
                targetBlocked: false,
                targetInAttackRange: false,
                pathAvailable: true,
                directPathClear: true,
                nearHazard: false,
                sourceCenter: new Vector3(0f, 0f, 0f),
                targetCenter: new Vector3(3f, 0f, 4f),
                moveEnd: Vector3.zero,
                attackRange: 2f,
                targetLineColor: Color.red,
                attackRangeColor: Color.yellow,
                moveArrowColor: Color.blue,
                headline: "test",
                detail: "test",
                statusLabel: "test",
                targetDead: false,
                targetDistance: 5f);

            Assert.AreEqual(5f, item.TargetDistance, 0.01f);
            Assert.AreEqual("Hurtbox gap 5m", item.FormattedDistance);
        }

        [Test]
        public void RuntimeAiPlannerScenePreview_BuildsSelectedAndAllAgentItems()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot first = CreatePlayMonitorAgentSnapshot(
                entityId: 7,
                targetEntityId: 3,
                selectedActionName: "CastAbility",
                hasScenePreviewGeometry: true,
                scenePreviewSourceCenterX: 1f,
                scenePreviewSourceCenterY: 1f,
                scenePreviewSourceCenterZ: 2f,
                scenePreviewTargetCenterX: 4f,
                scenePreviewTargetCenterY: 1f,
                scenePreviewTargetCenterZ: 6f,
                scenePreviewTargetDirectionX: 0.6f,
                scenePreviewTargetDirectionZ: 0.8f,
                scenePreviewDecisionMoveX: 0.25f,
                scenePreviewDecisionMoveZ: 0.75f,
                scenePreviewAttackRange: 2.2f);
            CharacterTestRuntimeAiPlannerAgentSnapshot second = CreatePlayMonitorAgentSnapshot(
                entityId: 9,
                targetEntityId: 4,
                selectedActionName: "Chase",
                targetLineOfSightBlocked: true,
                moveDirectPathClear: false,
                moveCandidateIndex: 2,
                hasScenePreviewGeometry: true,
                scenePreviewSourceCenterX: -1f,
                scenePreviewSourceCenterY: 1f,
                scenePreviewSourceCenterZ: -2f,
                scenePreviewTargetCenterX: -4f,
                scenePreviewTargetCenterY: 1f,
                scenePreviewTargetCenterZ: -6f,
                scenePreviewTargetDirectionX: -0.6f,
                scenePreviewTargetDirectionZ: -0.8f,
                scenePreviewDecisionMoveX: -0.25f,
                scenePreviewDecisionMoveZ: -0.75f,
                scenePreviewAttackRange: 2.2f);
            var targets = new[]
            {
                new CharacterTestRuntimeAiPlannerEditorDebugTarget(
                    "mock",
                    () => new CharacterTestRuntimeAiPlannerSnapshot(
                        available: true,
                        profileId: "profile",
                        agents: new[] { first, second },
                        validationReport: "ok"))
            };
            var items = new List<CharacterTestRuntimeAiPlannerScenePreviewItem>();

            CharacterTestRuntimeAiPlannerScenePreview.BuildPreviewItemsForTests(
                targets,
                selectedAgentEntityId: 9,
                CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent,
                items);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(9, items[0].AgentEntityId);
            Assert.AreEqual("mock", items[0].SourceName);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning, items[0].Severity);
            Assert.AreEqual(new Vector3(-1f, 1f, -2f), items[0].SourceCenter);
            Assert.AreEqual(new Vector2(-0.25f, -0.75f), items[0].DecisionMove);
            Assert.IsFalse(string.IsNullOrWhiteSpace(items[0].DecisionHeadline));
            Assert.IsFalse(string.IsNullOrWhiteSpace(items[0].ConditionSummary));
            Assert.IsFalse(string.IsNullOrWhiteSpace(items[0].IssueText));
            StringAssert.Contains("Chase", items[0].ActionText);
            Assert.AreEqual("range=in", items[0].RangeText);
            Assert.AreEqual("move=reroute", items[0].MoveText);

            CharacterTestRuntimeAiPlannerScenePreview.BuildPreviewItemsForTests(
                targets,
                selectedAgentEntityId: 0,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                items);

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(7, items[0].AgentEntityId);
            Assert.AreEqual(9, items[1].AgentEntityId);
            Assert.AreEqual("move=direct", items[0].MoveText);
        }

        [Test]
        public void RuntimeAiPlannerScenePreview_LabelModeFiltersSelectedAndIssues()
        {
            CharacterTestRuntimeAiPlannerScenePreviewItem normal =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests(
                    "mock",
                    CreatePlayMonitorAgentSnapshot(
                        entityId: 7,
                        hasScenePreviewGeometry: true));
            CharacterTestRuntimeAiPlannerScenePreviewItem warning =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests(
                    "mock",
                    CreatePlayMonitorAgentSnapshot(
                        entityId: 9,
                        targetLineOfSightBlocked: true,
                        hasScenePreviewGeometry: true));

            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Off,
                CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent,
                selectedAgentEntityId: 0));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.All,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                selectedAgentEntityId: 0));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                selectedAgentEntityId: 0));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                warning,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                selectedAgentEntityId: 0));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected,
                CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent,
                selectedAgentEntityId: 0));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                selectedAgentEntityId: 7));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldDrawLabelForTests(
                normal,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                selectedAgentEntityId: 9));
        }

        [Test]
        public void RuntimeAiPlannerScenePreview_SeverityLabelAndColorRulesAreStable()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot normal = CreatePlayMonitorAgentSnapshot(
                selectedActionName: "CastAbility",
                hasScenePreviewGeometry: true,
                scenePreviewSourceCenterX: 1f,
                scenePreviewTargetCenterX: 2f,
                scenePreviewAttackRange: 1.5f);
            CharacterTestRuntimeAiPlannerAgentSnapshot blocked = CreatePlayMonitorAgentSnapshot(
                targetLineOfSightBlocked: true,
                moveDirectPathClear: false,
                moveCandidateIndex: 2,
                hasScenePreviewGeometry: true,
                scenePreviewDecisionMoveX: 1f,
                scenePreviewAttackRange: 1.5f);
            CharacterTestRuntimeAiPlannerAgentSnapshot directMove = CreatePlayMonitorAgentSnapshot(
                hasScenePreviewGeometry: true,
                scenePreviewDecisionMoveX: 1f,
                scenePreviewAttackRange: 1.5f);
            CharacterTestRuntimeAiPlannerAgentSnapshot rejected = CreatePlayMonitorAgentSnapshot(
                rejectReason: "CommandRejected",
                hasScenePreviewGeometry: true);
            CharacterTestRuntimeAiPlannerAgentSnapshot noPath = CreatePlayMonitorAgentSnapshot(
                movePathAvailable: false,
                hasScenePreviewGeometry: true,
                scenePreviewDecisionMoveX: 1f);
            CharacterTestRuntimeAiPlannerAgentSnapshot hazard = CreatePlayMonitorAgentSnapshot(
                environmentNearHazard: true,
                hasScenePreviewGeometry: true);
            CharacterTestRuntimeAiPlannerAgentSnapshot inactive = CreatePlayMonitorAgentSnapshot(
                targetExists: false,
                hasScenePreviewGeometry: false);
            CharacterTestRuntimeAiPlannerAgentSnapshot outOfRange = CreatePlayMonitorAgentSnapshot(
                targetInAttackRange: false,
                hasScenePreviewGeometry: true,
                scenePreviewAttackRange: 1.5f);

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Normal,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(normal));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(blocked));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(rejected));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(noPath));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(hazard));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive,
                CharacterTestRuntimeAiPlannerScenePreview.ResolveSeverityForTests(inactive));

            CharacterTestRuntimeAiPlannerScenePreviewItem item =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", normal);
            string label = CharacterTestRuntimeAiPlannerScenePreview.FormatLabelForTests(item);

            StringAssert.Contains("Agent 7 | state=root/cast", label);
            StringAssert.Contains("action=CastAbility", label);
            StringAssert.Contains("score/cost=9.947/1.2", label);
            StringAssert.Contains("target=3 dist=2.05", label);
            StringAssert.Contains("range=in", label);
            Assert.AreEqual(
                new Color(0.18f, 0.85f, 0.36f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveColorForTests(item.Severity));

            CharacterTestRuntimeAiPlannerScenePreviewItem blockedItem =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", blocked);
            CharacterTestRuntimeAiPlannerScenePreviewItem directMoveItem =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", directMove);
            CharacterTestRuntimeAiPlannerScenePreviewItem noPathItem =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", noPath);
            CharacterTestRuntimeAiPlannerScenePreviewItem inactiveItem =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", inactive);
            CharacterTestRuntimeAiPlannerScenePreviewItem outOfRangeItem =
                CharacterTestRuntimeAiPlannerScenePreview.CreateItemForTests("mock", outOfRange);

            Assert.AreEqual(
                new Color(0.18f, 0.85f, 0.36f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveTargetColorForTests(item));
            Assert.AreEqual(
                new Color(0.95f, 0.72f, 0.18f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveTargetColorForTests(blockedItem));
            Assert.AreEqual(
                new Color(0.5f, 0.5f, 0.5f, 0.9f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveTargetColorForTests(inactiveItem));
            Assert.AreEqual(
                new Color(0.18f, 0.85f, 0.36f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveLineColorForTests(item));
            Assert.AreEqual(
                new Color(0.95f, 0.72f, 0.18f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveLineColorForTests(blockedItem));
            Assert.AreEqual(
                new Color(0.9f, 0.2f, 0.16f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveLineColorForTests(noPathItem));
            Assert.AreEqual(
                new Color(0.18f, 0.85f, 0.36f, 0.85f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveAttackRangeColorForTests(item));
            Assert.AreEqual(
                new Color(0.5f, 0.5f, 0.5f, 0.45f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveAttackRangeColorForTests(outOfRangeItem));
            Assert.AreEqual(
                new Color(0.18f, 0.85f, 0.36f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveMoveColorForTests(directMoveItem));
            Assert.AreEqual(
                new Color(0.9f, 0.2f, 0.16f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveMoveColorForTests(noPathItem));
            Assert.AreEqual(
                new Color(0.95f, 0.72f, 0.18f, 1f),
                CharacterTestRuntimeAiPlannerScenePreview.ResolveMoveColorForTests(blockedItem));
        }

        [Test]
        public void RuntimeAiPlannerScenePreview_LegendAndRepaintRulesAreStable()
        {
            string legend = CharacterTestRuntimeAiPlannerScenePreview.FormatLegendForTests(
                2,
                CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents,
                CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues);

            StringAssert.Contains("Runtime AI Planner", legend);
            StringAssert.Contains("mode=AllAgents", legend);
            StringAssert.Contains("labels=Issues", legend);
            StringAssert.Contains("agents=2", legend);
            StringAssert.Contains("refresh=5Hz", legend);

            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldRequestRepaintForTests(
                enabled: false,
                isPlaying: true,
                targetCount: 1));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldRequestRepaintForTests(
                enabled: true,
                isPlaying: false,
                targetCount: 1));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerScenePreview.ShouldRequestRepaintForTests(
                enabled: true,
                isPlaying: true,
                targetCount: 0));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerScenePreview.ShouldRequestRepaintForTests(
                enabled: true,
                isPlaying: true,
                targetCount: 1));
        }

    }
}
