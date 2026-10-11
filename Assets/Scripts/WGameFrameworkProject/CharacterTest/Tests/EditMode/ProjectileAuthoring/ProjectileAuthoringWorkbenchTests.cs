using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Editor.ProjectileAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace MxFramework.Tests.ProjectileAuthoring
{
    public sealed class ProjectileAuthoringWorkbenchTests
    {
        [Test]
        public void M2_E01_E02_E12_MenuHierarchyAndVisualTreeAreStable()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            Assert.IsTrue(EditorApplication.ExecuteMenuItem(ProjectileAuthoringWindow.MenuPath), "The registered MenuItem must execute, not merely expose a matching constant.");
            Assert.AreEqual("投射物创作", window.titleContent.text); Assert.AreEqual(new Vector2(1280, 720), window.minSize);
            Assert.NotNull(window.rootVisualElement.Q<TwoPaneSplitView>("projectile-authoring-outer-split")); Assert.NotNull(window.rootVisualElement.Q<TwoPaneSplitView>("projectile-authoring-inspector-split"));
            Assert.NotNull(window.rootVisualElement.Q("preview-placeholder")); Assert.NotNull(window.rootVisualElement.Q("timeline-placeholder")); Assert.NotNull(window.rootVisualElement.Q("problems-drawer"));
            Assert.AreEqual("定义 / 请求变体", window.rootVisualElement.Q<Label>("definition-pane-title").text);
            Assert.AreEqual("预览 / 生命周期", window.rootVisualElement.Q<Label>("preview-title").text);
            Assert.That(window.rootVisualElement.Q<Foldout>("problems-drawer").text, Does.StartWith("问题"));
            Assert.IsFalse(window.rootVisualElement.Q<Foldout>("problems-drawer").value);
            PopupField<string> motionFilter = window.rootVisualElement.Q<PopupField<string>>("motion-filter");
            Assert.AreEqual("全部运动类型", motionFilter.formatSelectedValueCallback("All Motion"));
            Assert.AreEqual("All Motion", motionFilter.value, "中文化只能影响显示文本，不能改变协议枚举值。");
            CollectionAssert.AreEqual(new[] { "character_test.meteor_barrage.projectile", "m.0", "m.1", "m.2", "m.3", "m.4", "m.5" }, window.VisibleStableIdsForTests.Take(7));
            Assert.That(window.VisibleBadgesForTests[0], Does.Contain("Source")); Assert.That(window.VisibleBadgesForTests[0], Does.Contain("Shared")); Assert.That(window.VisibleBadgesForTests[1], Does.Contain("Request"));
            window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0); window.InvokeActionForTests("toggle-expanded");
            Assert.That(window.VisibleStableIdsForTests, Does.Contain("character_test.meteor_barrage.projectile")); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("m.0"));
        }

        [Test]
        public void M6_TimelineAlwaysShowsEveryLifecycleSlot_EvenBeforeAValidPreviewCompile()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0);
            Assert.AreEqual(9, window.PreviewTimelineTrackCountForTests);
            Assert.NotNull(window.rootVisualElement.Q<ScrollView>("projectile-preview-timeline-scroll"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-spawn"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-flightBody"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-trail"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-directImpact"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-areaImpact"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-blockedImpact"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-expire"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-effect"));
            Assert.NotNull(window.rootVisualElement.Q("projectile-preview-track-diagnostics"));
            Assert.AreEqual(7, window.rootVisualElement.Query<Button>(className: "pa-timeline-unconfigured").ToList().Count);
        }

        [Test]
        public void M6_TimelineTreatsEmptySlotsAsUnconfiguredAndResolvedAreaImpactAsAvailable()
        {
            using WindowFixture fixture = Open();
            fixture.Service.Workspace.bindings = new[]
            {
                new ProjectilePresentationBindingListItem
                {
                    projectileDefinitionId = 1,
                    definitionStableId = "character_test.meteor_barrage.projectile",
                    detail = new ProjectilePresentationBindingView
                    {
                        slots = new ProjectilePresentationSlotsView
                        {
                            spawn = new ProjectilePresentationSlotView(),
                            areaImpact = new ProjectilePresentationSlotView
                            {
                                resource = new ProjectileResourceKeyView
                                {
                                    id = "vfx.magic.fireball_burst.impact",
                                    typeId = "GameObject",
                                    packageId = "mxframework.samples"
                                },
                                orientation = "ImpactNormalOrWorldUp",
                                localOffset = new ProjectileVector3View(),
                                lifetimeFramesOverride = 30,
                                vfxDefinitionId = 12002
                            }
                        }
                    }
                }
            };
            fixture.Window.InvokeActionForTests("refresh-button");
            fixture.Window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0);

            Assert.AreEqual(6, fixture.Window.rootVisualElement.Query<Button>(className: "pa-timeline-unconfigured").ToList().Count);
            Assert.AreEqual(0, fixture.Window.rootVisualElement.Query<Button>(className: "pa-timeline-invalid").ToList().Count);
            Assert.IsTrue(fixture.Window.rootVisualElement.Q("projectile-preview-track-areaImpact")
                .Q<Button>(className: "pa-timeline-inactive") != null);
        }

        [Test]
        public void M6_TimelineCanSwitchBetweenCurrentScenarioAndAllBranches()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            PopupField<string> mode = window.rootVisualElement.Q<PopupField<string>>("projectile-preview-timeline-mode");
            Assert.AreEqual("Current Scenario", mode.value);
            Assert.AreEqual("当前场景", mode.formatSelectedValueCallback(mode.value));
            window.SetPreviewTimelineModeForTests("All Branches");
            Assert.AreEqual("All Branches", mode.value);
            Assert.AreEqual("全部分支", mode.formatSelectedValueCallback(mode.value));
            Assert.AreEqual(9, window.PreviewTimelineTrackCountForTests);
        }

        [Test]
        public void M6_LifecycleCardsUseHorizontalStripLayout()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            Assert.NotNull(window.rootVisualElement.Q("timeline-placeholder"));
            Assert.NotNull(window.rootVisualElement.Q<ScrollView>("projectile-lifecycle-scroll"));
            Assert.NotNull(window.rootVisualElement.Q<Label>("projectile-lifecycle-title"));
            VisualElement cards = window.rootVisualElement.Q("projectile-lifecycle-cards");
            Assert.NotNull(cards);
            Assert.IsTrue(cards.ClassListContains("pa-lifecycle-grid"));
            Assert.AreEqual(8, cards.Query<Button>(className: "pa-lifecycle-card").ToList().Count);
            Button missingSpawn = cards.Q<Button>("lifecycle-card-spawn");
            Assert.IsTrue(missingSpawn.ClassListContains("pa-lifecycle-missing"));
            Assert.IsFalse(missingSpawn.ClassListContains("pa-lifecycle-incompatible"), "未配置 ResourceKey 时不应执行 VFX 契约兼容性判定。");
            Assert.That(missingSpawn.Q<Label>("lifecycle-card-state").text, Does.Contain("缺失"));
        }

        [Test]
        public void M6_LifecycleStageSwitchesToFocusedPresentationInspector()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0);
            window.SelectPresentationSlotForTests("spawn");

            Assert.IsTrue(window.rootVisualElement.Q<Button>("inspector-tab-presentation").ClassListContains("pa-inspector-tab-selected"));
            Assert.AreEqual(DisplayStyle.Flex, window.rootVisualElement.Q("inspector-presentation-scroll").resolvedStyle.display);
            Assert.NotNull(window.rootVisualElement.Q("presentation-slot-spawn"));
            Assert.IsNull(window.rootVisualElement.Q("presentation-slot-flightBody"));
            Assert.IsTrue(window.rootVisualElement.Q<Button>("lifecycle-card-spawn").ClassListContains("pa-lifecycle-selected"));
        }

        [Test]
        public void M6_LifecycleAreaCardUsesIndependentVfxPreviewInsteadOfChangingGameplayScenario()
        {
            using WindowFixture fixture = Open();
            ProjectileDefinitionListItem definition = fixture.Service.Workspace.definitions[0];
            definition.detail = CreateAreaPreviewDetail(definition.definitionId, definition.stableId);
            fixture.Service.Workspace.bindings = new[]
            {
                new ProjectilePresentationBindingListItem
                {
                    projectileDefinitionId = definition.definitionId,
                    definitionStableId = definition.stableId,
                    detail = new ProjectilePresentationBindingView
                    {
                        projectileDefinitionId = definition.definitionId,
                        slots = new ProjectilePresentationSlotsView
                        {
                            areaImpact = new ProjectilePresentationSlotView
                            {
                                resource = new ProjectileResourceKeyView
                                {
                                    id = "vfx.test.area-impact",
                                    typeId = "GameObject",
                                    packageId = "mxframework.tests"
                                },
                                orientation = "ImpactNormalOrWorldUp",
                                localOffset = new ProjectileVector3View(),
                                lifetimeFramesOverride = 30,
                                vfxDefinitionId = 19001
                            }
                        }
                    }
                }
            };

            fixture.Window.InvokeActionForTests("refresh-button");
            fixture.Window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0);
            fixture.Window.PreviewPresentationSlotForTests("areaImpact");

            Assert.AreEqual("VFX Preview", fixture.Window.PreviewModeForTests);
            PopupField<string> mode = fixture.Window.rootVisualElement.Q<PopupField<string>>("projectile-preview-mode");
            Assert.AreEqual("特效预览", mode.formatSelectedValueCallback(mode.value));
            Assert.AreEqual(ProjectilePreviewScenario.Miss, fixture.Window.PreviewScenarioForTests);
            Assert.AreEqual(0, fixture.Window.PreviewPresentationTraceCountForTests,
                "The Miss scenario has no area event; slot audition must remain separate from this trace.");
            Assert.IsFalse(fixture.Window.PreviewIsPlayingForTests,
                "VFX audition must not start or depend on the projectile gameplay timeline.");

            mode.value = "Runtime Validation";
            fixture.Window.PreviewPresentationSlotForTests("areaImpact");

            Assert.AreEqual(ProjectilePreviewScenario.AreaHit, fixture.Window.PreviewScenarioForTests);
            Assert.Greater(fixture.Window.PreviewPresentationTraceCountForTests, 0);
            Assert.IsTrue(fixture.Window.PreviewIsPlayingForTests);
        }

        [Test]
        public void M2_E03_FiltersBadgesAndSelectionUseStableIdentity()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window;
            ListView list = window.rootVisualElement.Q<ListView>("definition-request-tree"); list.SetSelection(0); string selected = window.SessionForTests.SelectedStableId;
            PopupField<string> motion = window.rootVisualElement.Q<PopupField<string>>("motion-filter"); motion.value = "Ballistic";
            Assert.That(window.VisibleStableIdsForTests, Does.Contain(selected)); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("definition.1"));
            window.InvokeActionForTests("shared-filter"); Assert.AreEqual(7, window.VisibleStableIdsForTests.Count);
            PopupField<string> status = window.rootVisualElement.Q<PopupField<string>>("status-filter"); status.value = "Generated-only"; Assert.AreEqual(0, window.VisibleStableIdsForTests.Count);
            motion.value = "All Motion"; window.InvokeActionForTests("shared-filter"); Assert.That(window.VisibleStableIdsForTests, Does.Contain("generated.complete"));
            status.value = "All Status"; window.SessionForTests.Push("exact-dirty", new[] { "config/projectiles/definition.10.json" }); window.InvokeActionForTests("rebuild");
            int similar = window.VisibleStableIdsForTests.ToList().IndexOf("definition.1"); int exact = window.VisibleStableIdsForTests.ToList().IndexOf("definition.10"); Assert.That(window.VisibleBadgesForTests[similar], Does.Not.Contain("Dirty")); Assert.That(window.VisibleBadgesForTests[exact], Does.Contain("Dirty"));
        }

        [Test]
        public void M7_DeepLinkLocatesRequestDefinitionGeneratedOnlyAndReportsMissing()
        {
            using WindowFixture fixture = Open();
            ProjectileAuthoringWindow window = fixture.Window;

            window.ApplyDeepLinkForTests("m.0");
            Assert.AreEqual("m.0", window.SessionForTests.SelectedStableId);
            window.ApplyDeepLinkForTests("definition.1");
            Assert.AreEqual("definition.1", window.SessionForTests.SelectedStableId);
            window.ApplyDeepLinkForTests("generated.complete");
            Assert.AreEqual("generated.complete", window.SessionForTests.SelectedStableId);

            window.ApplyDeepLinkForTests("missing.projectile.request");
            Assert.That(window.StatusForTests, Does.Contain("深层链接目标不存在"));
            Assert.That(window.VisibleProblemCodesForTests, Does.Contain("PROJAUTH027"));
        }

        [Test]
        public void M2_E04_DirtyUndoRedoCapsAndReturnsToBaseClean()
        {
            var session = new ProjectileAuthoringDocumentSession(); session.Reset("base"); session.Push("draft", new[] { "config/projectiles/a.json" });
            Assert.IsTrue(session.CanSave); session.Undo(); Assert.AreEqual(0, session.DirtySources.Count); Assert.IsFalse(session.CanSave); session.Redo(); Assert.AreEqual(1, session.DirtySources.Count);
            for (int i = 0; i < 80; i++) session.Push("draft-" + i, new[] { "config/projectiles/" + i + ".json" });
            int undos = 0; while (session.CanUndo) { session.Undo(); undos++; } Assert.LessOrEqual(undos, 63);
        }

        [Test]
        public void M2_E04_ConsecutiveMutationsProduceCompositeDraftAndUndoRedoReplaysSequence()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; FakeService fake = fixture.Service;
            window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0);
            window.rootVisualElement.Q<TextField>("identity-input").value = "created.first"; window.InvokeActionForTests("create-definition"); window.InvokeActionForTests("reference-impact-confirm");
            window.rootVisualElement.Q<TextField>("identity-input").value = "created.second"; window.InvokeActionForTests("create-definition"); window.InvokeActionForTests("reference-impact-confirm");
            Assert.AreEqual(2, window.SessionForTests.PendingCommands.Count); Assert.AreEqual(2, fake.MutationSequences.Last().Length); Assert.AreEqual(2, window.SessionForTests.DirtySources.Count);
            window.InvokeActionForTests("undo"); Assert.AreEqual(1, window.SessionForTests.PendingCommands.Count); Assert.AreEqual(1, fake.MutationSequences.Last().Length);
            window.InvokeActionForTests("redo"); Assert.AreEqual(2, window.SessionForTests.PendingCommands.Count); Assert.AreEqual(2, fake.MutationSequences.Last().Length);
        }

        [Test]
        public void M2_E02_E04_ProjectedDraftWorkspaceDrivesCreateRequestRenameDeleteUndoAndBlockedIsolation()
        {
            string stage = "open";
            try
            {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; FakeService fake = fixture.Service; ListView list = window.rootVisualElement.Q<ListView>("definition-request-tree"); TextField identity = window.rootVisualElement.Q<TextField>("identity-input");
            stage = "create-definition";
            list.SetSelection(0); identity.value = "draft.definition"; window.InvokeActionForTests("create-definition"); window.InvokeActionForTests("reference-impact-confirm");
            Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.definition")); Assert.AreEqual(23, fake.Workspace.definitionCount, "The disk workspace fixture must remain unchanged."); Assert.AreEqual(23, window.DiskDefinitionCountForTests); Assert.AreEqual(24, window.CurrentDefinitionCountForTests);

            stage = "create-request"; list.SetSelection(window.VisibleStableIdsForTests.ToList().IndexOf("draft.definition")); Assert.That(window.rootVisualElement.Q<Label>("inspector-summary").text, Does.Contain("config/projectiles/draft.definition.json")); identity.value = "draft.request"; window.InvokeActionForTests("create-request"); window.InvokeActionForTests("reference-impact-confirm");
            int definitionIndex = window.VisibleStableIdsForTests.ToList().IndexOf("draft.definition"); Assert.GreaterOrEqual(definitionIndex, 0); Assert.AreEqual("draft.request", window.VisibleStableIdsForTests[definitionIndex + 1], "The staged request must render as the draft definition child."); Assert.AreEqual(2, fake.MutationSequences.Last().Length);

            stage = "rename"; list.SetSelection(definitionIndex); identity.value = "draft.renamed"; window.InvokeActionForTests("rename"); window.InvokeActionForTests("reference-impact-confirm");
            Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("draft.definition")); Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.renamed"));
            int renamedIndex = window.VisibleStableIdsForTests.ToList().IndexOf("draft.renamed"); Assert.AreEqual("draft.request", window.VisibleStableIdsForTests[renamedIndex + 1]);

            stage = "blocked"; list.SetSelection(renamedIndex); identity.value = "must.not.appear"; fake.BlockImpact = true; window.InvokeActionForTests("rename");
            Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.renamed")); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("must.not.appear")); window.InvokeActionForTests("reference-impact-cancel"); fake.BlockImpact = false;
            stage = "failed"; identity.value = "failed.must.not.appear"; fake.MutationFails = true; LogAssert.Expect(LogType.Error, "[Projectile Authoring] projected failure"); window.InvokeActionForTests("rename"); fake.MutationFails = false;
            Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.renamed")); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("failed.must.not.appear"));

            stage = "delete-request"; list.SetSelection(window.VisibleStableIdsForTests.ToList().IndexOf("draft.request")); window.InvokeActionForTests("delete"); window.InvokeActionForTests("reference-impact-confirm"); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("draft.request"));
            window.InvokeActionForTests("undo"); Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.request")); window.InvokeActionForTests("redo"); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("draft.request"));
            stage = "delete-definition"; list.SetSelection(window.VisibleStableIdsForTests.ToList().IndexOf("draft.renamed")); window.InvokeActionForTests("delete"); window.InvokeActionForTests("reference-impact-confirm"); Assert.That(window.VisibleStableIdsForTests, Does.Not.Contain("draft.renamed"));
            window.InvokeActionForTests("undo"); Assert.That(window.VisibleStableIdsForTests, Does.Contain("draft.renamed"));
            }
            catch (Exception ex) { Assert.Fail(stage + ": " + ex); }
        }

        [Test]
        public void M2_E05_ImpactRequiresPreviewAuthorizationAndBlockingCannotConfirm()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; FakeService fake = fixture.Service;
            ListView list = window.rootVisualElement.Q<ListView>("definition-request-tree"); list.SetSelection(0); window.rootVisualElement.Q<TextField>("identity-input").value = "renamed.definition";
            fake.BlockImpact = true; window.InvokeActionForTests("rename"); Assert.IsTrue(fake.MutationRequests.Last().planOnly); Assert.IsFalse(window.rootVisualElement.Q<Button>("reference-impact-confirm").enabledSelf);
            window.InvokeActionForTests("reference-impact-cancel"); fake.BlockImpact = false; window.InvokeActionForTests("rename");
            Button confirm = window.rootVisualElement.Q<Button>("reference-impact-confirm"); Assert.IsTrue(confirm.enabledSelf); window.InvokeActionForTests("reference-impact-confirm");
            Assert.IsFalse(fake.MutationRequests.Last().planOnly); CollectionAssert.AreEqual(new[] { "config/abilities/consumer.json" }, fake.MutationRequests.Last().authorizedConsumerPaths); Assert.That(window.SessionForTests.DirtySources, Does.Contain("config/projectiles/renamed.definition.json"));
        }

        [TestCase("external-reload")]
        [TestCase("external-compare")]
        [TestCase("external-cancel")]
        public void M2_E06_ExternalConflictActionsHaveExplicitTransitions(string action)
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; fixture.Service.UseExternalWorkspace = true; window.SessionForTests.Push("same-draft", new[] { "config/projectiles/a.json" });
            window.InvokeActionForTests("refresh-button"); Assert.IsFalse(window.SessionForTests.CanSave); window.InvokeActionForTests(action);
            if (action == "external-reload") Assert.AreEqual(0, window.SessionForTests.DirtySources.Count); else Assert.IsTrue(window.SessionForTests.HasExternalChanges);
            if (action == "external-compare") { Assert.That(window.StatusForTests, Does.Contain("已更改=config/projectiles/a.json")); Assert.That(window.StatusForTests, Does.Contain("新增=config/projectiles/c.json")); Assert.That(window.StatusForTests, Does.Contain("删除=config/projectiles/b.json")); Assert.AreEqual("same-draft", window.SessionForTests.DraftPath); }
            if (action == "external-cancel") Assert.AreEqual("same-draft", window.SessionForTests.DraftPath);
        }

        [Test]
        public void M2_E06_ReapplyUsesLatestWorkspaceAndReplansMutation()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; FakeService fake = fixture.Service;
            window.SessionForTests.Reset("hash"); var first = new ProjectileAuthoringMutationRequestView { kind = "CreateDefinition", stableId = "one", definitionStableId = "one", createRequest = false, confirmed = true }; var second = new ProjectileAuthoringMutationRequestView { kind = "CreateRequestForExistingDefinition", stableId = "two", definitionStableId = "one", confirmed = true };
            window.SessionForTests.Push("draft-1", new[] { "config/projectiles/one.json" }, first); window.SessionForTests.Push("draft-2", new[] { "config/projectiles/one.json", "config/projectiles/two.json" }, second);
            fake.UseExternalWorkspace = true; window.InvokeActionForTests("refresh-button"); int before = fake.MutationRequests.Count; window.InvokeActionForTests("external-reapply"); Assert.Greater(fake.MutationRequests.Count, before);
            Assert.AreEqual(2, fake.MutationSequences.Last().Length); Assert.AreEqual(2, window.SessionForTests.PendingCommands.Count); Assert.That(window.SessionForTests.DirtySources, Has.Count.EqualTo(2));
        }

        [Test]
        public void M2_E07_SlowServiceCompletesOnceWithoutBlockingTree()
        {
            using WindowFixture fixture = Open(); FakeService fake = fixture.Service; fake.DelayMutations = true;
            fixture.Window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0); fixture.Window.rootVisualElement.Q<TextField>("identity-input").value = "delayed"; fixture.Window.InvokeActionForTests("create-definition");
            Assert.NotNull(fixture.Window.rootVisualElement.Q("projectile-authoring-workspace")); Assert.AreEqual(1, fake.PendingCallbackCount); fake.CompletePending(); fake.CompletePending();
            Assert.AreEqual(1, fake.CompletedCallbackCount); Assert.AreEqual(DisplayStyle.Flex, fixture.Window.rootVisualElement.Q("reference-impact-panel").resolvedStyle.display);
        }

        [TestCase("before-reload", true)]
        [TestCase("playmode", true)]
        [TestCase("disable", false)]
        public void M2_E11_LifecycleCancelsJobAndUnregistersWhenDisabled(string transition, bool remainsRegistered)
        {
            using WindowFixture fixture = Open(); FakeService fake = fixture.Service; fake.DelayMutations = true;
            fixture.Window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0); fixture.Window.rootVisualElement.Q<TextField>("identity-input").value = "delayed"; fixture.Window.InvokeActionForTests("create-definition");
            fixture.Window.InvokeLifecycleForTests(transition); Assert.AreEqual(0, fake.PendingCallbackCount); Assert.GreaterOrEqual(fake.CancelCount, 1); Assert.AreEqual(remainsRegistered, fixture.Window.LifecycleRegisteredForTests);
        }

        [Test]
        public void M2_E04_E11_SessionRestoresCommandsDirtyUndoFiltersExpansionAndPaneSizes()
        {
            var original = new ProjectileAuthoringDocumentSession { Filter = "fire", MotionFilter = "Linear", ImpactFilter = "Area", StatusFilter = "Source", UsedOnly = true, SharedOnly = true, DirtyOnly = true, InvalidOnly = true, NavigationPaneSize = 412f, InspectorPaneSize = 468f, ExpandedStableIds = new[] { "definition.1" } };
            original.Reset("hash"); original.Push("draft-1", new[] { "config/projectiles/one.json" }, new ProjectileAuthoringMutationRequestView { kind = "CreateDefinition", stableId = "one", definitionStableId = "one", createRequest = false }); original.Push("draft-2", new[] { "config/projectiles/one.json", "config/projectiles/two.json" }, new ProjectileAuthoringMutationRequestView { kind = "CreateRequestForExistingDefinition", stableId = "two", definitionStableId = "one" });
            var restored = new ProjectileAuthoringDocumentSession(); restored.RestoreState(original.CaptureState());
            Assert.AreEqual(2, restored.PendingCommands.Count); Assert.AreEqual(2, restored.DirtySources.Count); Assert.IsTrue(restored.CanUndo); Assert.AreEqual("fire", restored.Filter); Assert.AreEqual("Linear", restored.MotionFilter); Assert.AreEqual("Area", restored.ImpactFilter); Assert.AreEqual("Source", restored.StatusFilter);
            Assert.IsTrue(restored.UsedOnly && restored.SharedOnly && restored.DirtyOnly && restored.InvalidOnly); Assert.AreEqual(412f, restored.NavigationPaneSize); Assert.AreEqual(468f, restored.InspectorPaneSize); CollectionAssert.AreEqual(new[] { "definition.1" }, restored.ExpandedStableIds);
            restored.Undo(); Assert.AreEqual(1, restored.PendingCommands.Count); restored.Redo(); Assert.AreEqual(2, restored.PendingCommands.Count); Assert.IsEmpty(restored.DraftPath, "Temporary draft paths must never be restored across lifecycle cleanup.");
        }

        [Test]
        public void M2_E11_BeforeReloadRestoresCompositeDraftWithoutRetainedTempFile()
        {
            string stage = "open";
            try
            {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow firstWindow = fixture.Window;
            stage = "seed";
            firstWindow.SessionForTests.Reset("hash"); firstWindow.SessionForTests.Push("deleted-temp-1", new[] { "config/projectiles/one.json" }, new ProjectileAuthoringMutationRequestView { kind = "CreateDefinition", stableId = "one", definitionStableId = "one", createRequest = false, confirmed = true }); firstWindow.SessionForTests.Push("deleted-temp-2", new[] { "config/projectiles/one.json", "config/projectiles/two.json" }, new ProjectileAuthoringMutationRequestView { kind = "CreateRequestForExistingDefinition", stableId = "two", definitionStableId = "one", confirmed = true });
            stage = "before-reload";
            firstWindow.InvokeLifecycleForTests("before-reload");
            stage = "create-restored";
            ProjectileAuthoringWindow restoredWindow = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>(); var restoredService = new FakeService(); restoredWindow.SetServiceForTests(restoredService);
            try
            {
                stage = "create-gui";
                restoredWindow.CreateGUI(); Assert.AreEqual(2, restoredWindow.SessionForTests.PendingCommands.Count); Assert.AreEqual(2, restoredService.MutationSequences.Last().Length); Assert.AreEqual(2, restoredWindow.SessionForTests.DirtySources.Count); Assert.IsTrue(restoredWindow.SessionForTests.CanUndo); Assert.That(restoredWindow.SessionForTests.DraftPath, Does.StartWith("draft-"));
            }
            finally { restoredWindow.DiscardChanges(); UnityEngine.Object.DestroyImmediate(restoredWindow); }
            }
            catch (Exception ex) { Assert.Fail(stage + ": " + ex); }
        }

        [Test]
        public void M2_E08_SaveFailureKeepsDirtyAndSuccessRefreshesExactlyOnce()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; FakeService fake = fixture.Service; int refreshes = 0; window.SetAssetRefreshForTests(() => refreshes++);
            window.SessionForTests.Push("draft", new[] { "config/projectiles/a.json" }); fake.ApplyFails = true; LogAssert.Expect(LogType.Error, "[Projectile Authoring] failed"); window.InvokeActionForTests("save-button"); Assert.AreEqual(1, window.SessionForTests.DirtySources.Count); Assert.AreEqual(0, refreshes);
            fake.ApplyFails = false; window.InvokeActionForTests("save-button"); Assert.AreEqual(1, refreshes); Assert.AreEqual(0, window.SessionForTests.DirtySources.Count); Assert.AreEqual(2, fake.ApplyCount);
        }

        [Test]
        public void M2_E09_ProblemsFilterLocateAndCopyPreserveValidationCount()
        {
            using WindowFixture fixture = Open(); ProjectileAuthoringWindow window = fixture.Window; window.InvokeActionForTests("validate-button");
            CollectionAssert.AreEqual(new[] { "ERR", "WARN" }, window.ValidationProblemCodesForTests);
            ScrollView list = window.rootVisualElement.Q<ScrollView>("problems-list"); Assert.AreEqual(2, list.Query<VisualElement>(className: "pa-row").ToList().Count); PopupField<string> severity = window.rootVisualElement.Q<PopupField<string>>("problem-severity"); severity.value = "Error"; Assert.AreEqual(1, list.Query<VisualElement>(className: "pa-row").ToList().Count);
            window.CopyProblemForTests(0); Assert.That(EditorGUIUtility.systemCopyBuffer, Does.Contain("ERR")); window.LocateProblemForTests(0); severity.value = "All"; CollectionAssert.AreEqual(new[] { "ERR", "WARN" }, window.ValidationProblemCodesForTests); Assert.That(window.VisibleProblemCodesForTests, Does.Contain("PROJAUTH021"));
        }

        [Test]
        public void M2_E10_SceneIsUnchangedByOpenValidateCrudAndClose()
        {
            Scene scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty; int roots = scene.rootCount;
            using (WindowFixture fixture = Open()) { fixture.Window.InvokeActionForTests("validate-button"); fixture.Window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(0); fixture.Window.rootVisualElement.Q<TextField>("identity-input").value = "new.definition"; fixture.Window.InvokeActionForTests("create-definition"); }
            Assert.AreEqual(dirty, scene.isDirty); Assert.AreEqual(roots, scene.rootCount);
        }

        private static WindowFixture Open()
        {
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Selected"); SessionState.EraseString("MxFramework.ProjectileAuthoring.Filter"); SessionState.EraseString("MxFramework.ProjectileAuthoring.Draft"); SessionState.EraseString("MxFramework.ProjectileAuthoring.Dirty"); SessionState.EraseString("MxFramework.ProjectileAuthoring.Mutation");
            ProjectileAuthoringWindow window = EditorWindow.GetWindow<ProjectileAuthoringWindow>(true, "Projectile Authoring Test", true); var fake = new FakeService(); window.SetServiceForTests(fake); window.CreateGUI(); return new WindowFixture(window, fake);
        }

        private static ProjectileDefinitionDetailView CreateAreaPreviewDetail(int definitionId, string stableId)
        {
            var hit = new CombatHitEventView
            {
                targetTypes = new[] { "HostileCharacter" },
                environmentHitPolicy = "None",
                stableEventId = "preview.hit",
                maxAcceptedHits = 8,
                hitSameTargetPolicy = "AllowRepeat"
            };
            return new ProjectileDefinitionDetailView
            {
                definitionId = definitionId,
                stableId = stableId,
                motion = new ProjectileMotionView
                {
                    kind = "Linear",
                    speedPerFrame = .5f,
                    lifetimeFrames = 30,
                    speedProfile = new ProjectileSpeedProfileView { kind = "Constant" },
                    homing = new ProjectileHomingView()
                },
                collision = new ProjectileCollisionView
                {
                    radius = .1f,
                    layerMask = 1 << 1,
                    stopOnFirstAcceptedImpact = true,
                    stopOnBlockedImpact = true,
                    maxAcceptedImpacts = 1,
                    hitSameTargetPolicy = "AllowRepeat"
                },
                impact = new ProjectileImpactView
                {
                    stableEventId = "preview.impact",
                    policyKind = "AreaPulseOnImpact",
                    areaRadius = 1.5f,
                    areaLayerMask = 1 << 1,
                    hitEvent = hit,
                    areaHitEvent = hit
                }
            };
        }

        private sealed class WindowFixture : IDisposable
        {
            public WindowFixture(ProjectileAuthoringWindow window, FakeService service) { Window = window; Service = service; }
            public ProjectileAuthoringWindow Window { get; } public FakeService Service { get; }
            public void Dispose() { if (Window != null) { Window.DiscardChanges(); Window.Close(); } }
        }

        private sealed class FakeService : IProjectileAuthoringService
        {
            public int CancelCount { get; private set; } public int ApplyCount { get; private set; } public bool IsBusyValue; public bool IsBusy => IsBusyValue || delayedCallbacks.Count > 0; public bool BlockImpact; public bool ApplyFails; public bool MutationFails; public bool UseExternalWorkspace; public bool DelayMutations; public int CompletedCallbackCount { get; private set; } public int PendingCallbackCount => delayedCallbacks.Count;
            private readonly Queue<Action> delayedCallbacks = new Queue<Action>();
            public readonly List<ProjectileAuthoringMutationRequestView> MutationRequests = new List<ProjectileAuthoringMutationRequestView>();
            public ProjectileAuthoringWorkspaceView Workspace { get; } = BuildWorkspace("hash"); private ProjectileAuthoringWorkspaceView ExternalWorkspace { get; } = BuildWorkspace("external-hash");
            public void LoadWorkspace(string packagePath, Action<ProjectileAuthoringWorkspaceView, string> completed) => completed(UseExternalWorkspace ? ExternalWorkspace : Workspace, null);
            public void Validate(string packagePath, Action<ProjectileAuthoringProblemView[], string> completed) => completed(Workspace.diagnostics, null);
            public void Mutate(string packagePath, ProjectileAuthoringMutationRequestView request, Action<ProjectileAuthoringMutationResultView, string, string> completed)
                => MutateSequence(packagePath, new[] { request }, completed);
            public readonly List<ProjectileAuthoringMutationRequestView[]> MutationSequences = new List<ProjectileAuthoringMutationRequestView[]>();
            public void MutateSequence(string packagePath, ProjectileAuthoringMutationRequestView[] commands, Action<ProjectileAuthoringMutationResultView, string, string> completed)
            {
                ProjectileAuthoringMutationRequestView request = commands.Last(); MutationSequences.Add(commands);
                if (MutationFails) { completed(null, string.Empty, "projected failure"); return; }
                MutationRequests.Add(request); var impact = new ProjectileAuthoringReferenceImpactView { summary = "impact", consumerPaths = new[] { "config/abilities/consumer.json" }, authorizationRequiredPaths = new[] { "config/abilities/consumer.json" }, blocking = BlockImpact, blockingReasons = BlockImpact ? new[] { "unknown consumer" } : Array.Empty<string>(), confirmationToken = "token" };
                ProjectileAuthoringDirtySourceView[] dirty = commands.Where(value => !value.planOnly).Select((value, index) => new ProjectileAuthoringDirtySourceView { relativePath = "config/projectiles/" + (string.IsNullOrEmpty(value.stableId) ? "mutation-" + index : value.stableId) + ".json", operation = "Upsert" }).ToArray();
                ProjectileAuthoringWorkspaceView projected = ProjectWorkspace(UseExternalWorkspace ? ExternalWorkspace : Workspace, commands);
                Action callback = () => { CompletedCallbackCount++; completed(new ProjectileAuthoringMutationResultView { status = BlockImpact ? "Blocked" : "Ready", impact = impact, dirtySources = dirty, projectedWorkspace = projected }, request.planOnly ? string.Empty : "draft-" + MutationSequences.Count + ".json", null); };
                if (DelayMutations) delayedCallbacks.Enqueue(callback); else callback();
            }
            public void Apply(string packagePath, string draftPath, Action<ProjectileAuthoringApplyResult, string> completed) { ApplyCount++; if (ApplyFails) completed(null, "failed"); else { UseExternalWorkspace = false; completed(new ProjectileAuthoringApplyResult { status = "Committed", playerMirrorFreshness = "UpdatedAtomically" }, null); } }
            public void CompletePending() { if (delayedCallbacks.Count > 0) delayedCallbacks.Dequeue()(); }
            public void Cancel() { CancelCount++; delayedCallbacks.Clear(); IsBusyValue = false; } public void Dispose() => Cancel();
            private static ProjectileAuthoringWorkspaceView BuildWorkspace(string hash)
            {
                var definitions = new List<ProjectileDefinitionListItem>(); var requests = new List<ProjectileRequestVariantListItem>();
                definitions.Add(new ProjectileDefinitionListItem { stableId = "character_test.meteor_barrage.projectile", definitionId = 1, motion = "Ballistic", impact = "Area", shared = true, sourcePaths = new[] { "config/projectiles/meteor.json" }, requestIds = new[] { "m.0", "m.1", "m.2", "m.3", "m.4", "m.5" } });
                for (int i = 0; i < 6; i++) requests.Add(new ProjectileRequestVariantListItem { stableId = "m." + i, definitionStableId = definitions[0].stableId, sourcePath = "config/projectiles/m." + i + ".json" });
                for (int i = 1; i < 23; i++) { string requestId = i < 22 ? "request." + i : null; definitions.Add(new ProjectileDefinitionListItem { stableId = "definition." + i, definitionId = i + 1, motion = "Linear", impact = "DirectHit", sourcePaths = new[] { "config/projectiles/definition." + i + ".json" }, requestIds = requestId == null ? Array.Empty<string>() : new[] { requestId } }); if (requestId != null) requests.Add(new ProjectileRequestVariantListItem { stableId = requestId, definitionStableId = "definition." + i, sourcePath = "config/projectiles/" + requestId + ".json" }); }
                ProjectileAuthoringSourceView[] sources = hash == "hash"
                    ? new[] { new ProjectileAuthoringSourceView { relativePath = "config/projectiles/a.json", expectedSha256 = "a-1" }, new ProjectileAuthoringSourceView { relativePath = "config/projectiles/b.json", expectedSha256 = "b-1" } }
                    : new[] { new ProjectileAuthoringSourceView { relativePath = "config/projectiles/a.json", expectedSha256 = "a-2" }, new ProjectileAuthoringSourceView { relativePath = "config/projectiles/c.json", expectedSha256 = "c-1" } };
                return new ProjectileAuthoringWorkspaceView { workspaceHash = hash, sourceCount = 29, definitionCount = 23, requestCount = 28, presentationBindingCount = 23, sources = sources, definitions = definitions.ToArray(), requests = requests.ToArray(), generatedOnly = new[] { new ProjectileGeneratedOnlyListItem { stableId = "generated.complete", definitionId = 999, complete = true, status = "Complete", motion = "Linear", impact = "DirectHit" } }, diagnostics = new[] { new ProjectileAuthoringProblemView { severity = "Error", code = "ERR", message = "broken m.0", sourcePath = "config/projectiles/m.0.json" }, new ProjectileAuthoringProblemView { severity = "Warning", code = "WARN", message = "warning", sourcePath = "config/projectiles/m.1.json" } } };
            }

            private static ProjectileAuthoringWorkspaceView ProjectWorkspace(ProjectileAuthoringWorkspaceView source, ProjectileAuthoringMutationRequestView[] commands)
            {
                ProjectileAuthoringWorkspaceView projected = JsonUtility.FromJson<ProjectileAuthoringWorkspaceView>(JsonUtility.ToJson(source));
                var definitions = (projected.definitions ?? Array.Empty<ProjectileDefinitionListItem>()).ToList(); var requests = (projected.requests ?? Array.Empty<ProjectileRequestVariantListItem>()).ToList();
                foreach (ProjectileAuthoringMutationRequestView command in commands ?? Array.Empty<ProjectileAuthoringMutationRequestView>())
                {
                    switch (command.kind)
                    {
                        case "CreateDefinition":
                            definitions.Add(new ProjectileDefinitionListItem { stableId = command.definitionStableId, definitionId = 1000 + definitions.Count, motion = "Linear", impact = "DirectHit", sourcePaths = new[] { "config/projectiles/" + command.stableId + ".json" }, requestIds = Array.Empty<string>() });
                            if (command.createRequest) AddRequest(definitions, requests, command.stableId, command.definitionStableId); break;
                        case "CreateRequestForExistingDefinition": AddRequest(definitions, requests, command.stableId, command.definitionStableId); break;
                        case "RenameDefinition":
                            ProjectileDefinitionListItem definition = definitions.First(value => value.stableId == command.sourceStableId); definition.stableId = command.definitionStableId;
                            foreach (ProjectileRequestVariantListItem item in requests.Where(value => value.definitionStableId == command.sourceStableId)) item.definitionStableId = command.definitionStableId; break;
                        case "RenameRequest":
                            ProjectileRequestVariantListItem renamed = requests.First(value => value.stableId == command.sourceStableId); renamed.stableId = command.stableId; renamed.sourcePath = "config/projectiles/" + command.stableId + ".json";
                            foreach (ProjectileDefinitionListItem item in definitions) item.requestIds = (item.requestIds ?? Array.Empty<string>()).Select(id => id == command.sourceStableId ? command.stableId : id).ToArray(); break;
                        case "DeleteRequest":
                            requests.RemoveAll(value => value.stableId == command.sourceStableId); foreach (ProjectileDefinitionListItem item in definitions) item.requestIds = (item.requestIds ?? Array.Empty<string>()).Where(id => id != command.sourceStableId).ToArray(); break;
                        case "DeleteDefinition": definitions.RemoveAll(value => value.stableId == command.sourceStableId); break;
                    }
                }
                projected.definitions = definitions.ToArray(); projected.requests = requests.ToArray(); projected.definitionCount = definitions.Count; projected.requestCount = requests.Count; projected.workspaceHash = "projected-" + commands.Length; return projected;
            }

            private static void AddRequest(List<ProjectileDefinitionListItem> definitions, List<ProjectileRequestVariantListItem> requests, string requestId, string definitionId)
            {
                ProjectileDefinitionListItem definition = definitions.First(value => value.stableId == definitionId); definition.requestIds = (definition.requestIds ?? Array.Empty<string>()).Concat(new[] { requestId }).ToArray(); definition.shared = definition.requestIds.Length > 1;
                requests.Add(new ProjectileRequestVariantListItem { stableId = requestId, definitionStableId = definitionId, sourcePath = "config/projectiles/" + requestId + ".json" });
            }
        }
    }
}
