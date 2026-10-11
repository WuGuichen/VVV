using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGameplayTriggerWorkspaceTests
    {
        private const string ScenePath =
            CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;

        [Test]
        public void WorkspaceRegistry_ReusesWorldObjectDocumentOwner()
        {
            IMxLevelEditorWorkspace workspace =
                MxLevelEditorWorkspaceRegistry.Find(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue);
            Assert.IsNotNull(workspace);
            Assert.AreEqual(
                CharacterTestWorldObjectAuthoringAdapter
                    .WorldObjectsModuleId,
                workspace.OwnerModuleId);
            Assert.IsFalse(
                workspace is IMxLevelEditorModule,
                "A workspace must not become another save participant.");

            int ownerCount = 0;
            for (int i = 0;
                 i < MxLevelEditorModuleRegistry.Modules.Count;
                 i++)
            {
                if (MxLevelEditorModuleRegistry.Modules[i].ModuleId
                    == workspace.OwnerModuleId)
                {
                    ownerCount++;
                }
            }
            Assert.AreEqual(1, ownerCount);
        }

        [Test]
        public void WorkspaceShell_ExposesTopLevelTriggerEntry()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            MxLevelEditorWindow window =
                MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                Assert.IsTrue(
                    window.TrySelectWorkspace(
                        CharacterTestGameplayTriggerWorkspace
                            .WorkspaceIdValue));
                Assert.AreEqual(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue,
                    window.CurrentWorkspaceId);
                Assert.AreEqual(
                    "玩法触发器",
                    window.rootVisualElement
                        .Q<PopupField<string>>(
                            "mx-level-editor-module-popup")
                        .value);
                Assert.IsNotNull(
                    window.rootVisualElement.Q(
                        "character-test-trigger-workspace"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q(
                        "character-test-trigger-workspace-list"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q(
                        "character-test-trigger-workspace-editor"));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void WorkspaceProjection_UsesDeterministicEffectSentence()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds
                        .PoisonBottlePoolSourceId);
            Assert.IsTrue(
                trigger.TryUpdateSubjectRelation(
                    entry.StableId,
                    GameplayTargetRelationFilter.Enemy,
                    1));
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            const string Expected =
                "效果：当与队伍 1 敌对的存活、可伤害的角色"
                + "进入或停留在“LevelRegion_StartArea”时，"
                + "为其施加“601012 · 中毒”，"
                + "效果归因于“501012 · 毒瓶池”；"
                + "离开时移除；每个对象独立。";
            Assert.AreEqual(
                Expected,
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId));
            Assert.AreEqual(
                Expected,
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId),
                "Rebuilding the projection must not change its text.");

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace.Q<Label>(
                    "character-test-trigger-summary"));
            Assert.IsNotNull(
                workspace.Query<Label>(
                        className:
                            "mx-level-editor__trigger-group-title")
                    .ToList()
                    .Find(label => label.text == "事件"));
            Assert.IsNotNull(
                workspace.Query<Label>(
                        className:
                            "mx-level-editor__trigger-group-title")
                    .ToList()
                    .Find(label => label.text == "条件"));
            Assert.IsNotNull(
                workspace.Query<Label>(
                        className:
                            "mx-level-editor__trigger-group-title")
                    .ToList()
                    .Find(label => label.text == "动作"));
        }

        [Test]
        public void WorkspaceEditor_UsesCompactClausesWithoutDuplicateTargetCards()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyRemoveBuffPresenceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            VisualElement eventSection = workspace.Q(
                "character-test-trigger-event-section");
            VisualElement conditionSection = workspace.Q(
                "character-test-trigger-condition-section");
            VisualElement actionSection = workspace.Q(
                "character-test-trigger-action-section");
            Assert.IsNotNull(eventSection);
            Assert.IsNotNull(conditionSection);
            Assert.IsNotNull(actionSection);
            Assert.AreEqual(
                2,
                eventSection.Query<VisualElement>(
                        className:
                            "mx-level-editor__trigger-clause-row")
                    .ToList()
                    .Count);
            Assert.AreEqual(
                2,
                conditionSection.Query<VisualElement>(
                        className:
                            "mx-level-editor__trigger-clause-row")
                    .ToList()
                    .Count);
            Assert.AreEqual(
                2,
                actionSection.Query<VisualElement>(
                        className:
                            "mx-level-editor__trigger-clause-row")
                    .ToList()
                    .Count);
            Assert.IsEmpty(
                eventSection.Query<Label>(
                        className:
                            "mx-level-editor__trigger-readonly")
                    .ToList(),
                "Read-only event facts must be plain clause text.");
            Assert.IsEmpty(
                conditionSection.Query<Label>(
                        className:
                            "mx-level-editor__trigger-readonly")
                    .ToList(),
                "Locked conditions must not look like disabled inputs.");
            Assert.IsTrue(
                workspace.Q<VisualElement>(
                        "character-test-trigger-buff-picker")
                    .ClassListContains(
                        "mx-level-editor__trigger-clause-control"));
            Assert.IsTrue(
                workspace.Q<VisualElement>(
                        "character-test-trigger-buff-attribution")
                    .ClassListContains(
                        "mx-level-editor__trigger-clause-control"));
            Assert.IsNotNull(
                conditionSection.Q<ToolbarMenu>(
                    "character-test-trigger-condition-add"));
            Assert.IsNull(
                conditionSection.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic"),
                "The AND/OR selector becomes meaningful after a second clause.");

            List<Label> titles =
                workspace.Query<Label>(
                        className:
                            "mx-level-editor__trigger-group-title")
                    .ToList();
            Assert.IsNull(
                titles.Find(label => label.text == "作用对象"));
            Assert.IsNull(
                titles.Find(label => label.text == "触发对象"));
            Assert.IsNull(
                titles.Find(label => label.text == "执行动作"));
        }

        [Test]
        public void PlayerHpThresholdWorkspace_UsesUnifiedNonRegionEditingLayout()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddPlayerHpLowStoryTemplate(
                    CharacterTestStoryIds.Triggers.LevelStartRegion);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            const string Expected =
                "效果：当当前玩家生命值降至 30 或以下时，"
                + "触发剧情 “450401 · 起始区域引导”；"
                + "恢复到 35 以上后可再次触发。";
            Assert.AreEqual(
                Expected,
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId));

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace.Q<IntegerField>(
                    "character-test-trigger-player-hp-enter-threshold"));
            Assert.IsNotNull(
                workspace.Q<IntegerField>(
                    "character-test-trigger-player-hp-exit-threshold"));
            Assert.IsNotNull(
                workspace.Q<VisualElement>(
                    "character-test-trigger-story-reference"));
            Assert.IsNull(
                workspace.Q<Button>(
                    "character-test-trigger-workspace-locate"),
                "A derived player attribute source has no Scene anchor.");
            Assert.IsNull(
                workspace.Q<DropdownField>(
                    "character-test-trigger-strategy"),
                "The threshold route semantics are fixed by the formal template.");
            Assert.AreEqual(
                1,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    "生命值",
                    "战斗状态"));
        }

        [Test]
        public void PlayerHpThresholdWorkspace_PreservesUnknownLegacyValue()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddPlayerHpLowStoryTemplate(
                CharacterTestStoryIds.Triggers.LevelStartRegion);
            GameplayTriggerAuthoringDocument source =
                trigger.Authoring;
            GameplayTriggerSourceAuthoring original =
                source.Sources[0];
            trigger.Authoring = new GameplayTriggerAuthoringDocument(
                source.SchemaId,
                source.SchemaVersion,
                new[]
                {
                    new GameplayTriggerSourceAuthoring(
                        original.SourceDefinitionId,
                        original.SourceKind,
                        original.SignalOrdering,
                        original.ThresholdDirection,
                        GameplayTriggerFix64Authoring
                            .FromInvariantDecimal(
                                "legacy.player.hp.threshold"),
                        original.ExitThreshold,
                        original.InitialSamplePolicy,
                        original.DeparturePolicy)
                },
                source.Conditions,
                source.Definitions,
                source.Bindings);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);
            string before =
                module.CaptureDocumentSnapshotForTests();

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace.Query<HelpBox>()
                    .ToList()
                    .Find(box =>
                        box.text.Contains(
                            "原值会保持不变")));
            Assert.IsNull(
                workspace.Q<IntegerField>(
                    "character-test-trigger-player-hp-enter-threshold"));
            Assert.AreEqual(
                before,
                module.CaptureDocumentSnapshotForTests(),
                "Opening the workspace must not normalize an unknown legacy threshold.");
            Assert.IsFalse(module.IsDirty);
        }

        [Test]
        public void PlayerHpBooleanCondition_UsesHealthUnitsAndFix64Storage()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddPlayerHpLowStoryTemplate(
                    CharacterTestStoryIds.Triggers.LevelStartRegion);
            Assert.IsTrue(
                trigger.TryUpdateConditionRoot(
                    entry.StableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.All,
                        children: new[]
                        {
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation
                                    .PreviousScalar,
                                Fix64.FromInt(40).RawValue,
                                GameplayTriggerComparison.Greater),
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation.Not,
                                children: new[]
                                {
                                    new GameplayTriggerConditionNodeAuthoring(
                                        GameplayTriggerConditionOperation
                                            .CurrentScalar,
                                        Fix64.FromInt(30).RawValue,
                                        GameplayTriggerComparison
                                            .LessOrEqual)
                                })
                        })));
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.AreEqual(
                "全部满足（且）",
                workspace.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic")
                    .value);
            DoubleField value = workspace.Q<DoubleField>(
                "character-test-trigger-condition-value-1");
            Assert.IsNotNull(value);
            Assert.AreEqual(30d, value.value);

            value.value = 25.5d;
            CharacterTestLevelGameplayTriggerAuthoringDocument projected =
                ExtractTriggerDocument(
                    module.CaptureDocumentSnapshotForTests());
            Assert.IsTrue(
                projected.TryGetConditionRoot(
                    projected.Entries[0],
                    out GameplayTriggerConditionNodeAuthoring root));
            GameplayTriggerConditionNodeAuthoring current =
                root.Children[1].Children[0];
            Assert.AreEqual(
                25500000L,
                current.Operand);
            StringAssert.Contains(
                "当前生命值 ≤ 25.5",
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId));
        }

        [Test]
        public void WorkspaceProjection_CoversAllFormalTemplatesAndPreservesRouteOrder()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry enter =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            CharacterTestLevelGameplayTriggerEntry presence =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            CharacterTestLevelGameplayTriggerEntry story =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    "region.start",
                    payload0: 42L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            Assert.AreEqual(
                "效果：当存活、可伤害的角色进入“LevelRegion_StartArea”时，"
                + "为其施加“601012 · 中毒”，效果归因于“501012 · 毒瓶池”；"
                + "每个对象独立，每次进入期间最多一次，最多触发 1 次。",
                module.GetTriggerEffectSentenceForTests(enter.StableId));
            Assert.AreEqual(
                "效果：当存活、可伤害的角色进入或停留在"
                + "“LevelRegion_StartArea”时，为其施加“601012 · 中毒”，"
                + "效果归因于“501012 · 毒瓶池”；离开时移除；每个对象独立。",
                module.GetTriggerEffectSentenceForTests(presence.StableId));
            Assert.AreEqual(
                "效果：当存活的角色进入“LevelRegion_StartArea”时，"
                + "触发剧情 “未登记剧情触发 42”；最多触发 1 次。",
                module.GetTriggerEffectSentenceForTests(story.StableId));

            string before = module.CaptureDocumentSnapshotForTests();
            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            string after = module.CaptureDocumentSnapshotForTests();
            Assert.AreEqual(
                before,
                after,
                "Building the workspace must not rewrite canonical authoring.");
            Assert.IsNotNull(workspace);
            Assert.IsFalse(module.IsDirty);

            CharacterTestLevelGameplayTriggerAuthoringDocument projected =
                ExtractTriggerDocument(after);
            Assert.IsTrue(
                projected.TryFindEntry(
                    presence.StableId,
                    out CharacterTestLevelGameplayTriggerEntry
                        projectedPresence));
            GameplayTriggerDefinitionAuthoring definition =
                FindDefinition(
                    projected.Authoring,
                    projectedPresence.DefinitionId);
            Assert.IsNotNull(definition);
            Assert.That(definition.Routes, Has.Length.EqualTo(2));
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Entered
                | GameplayTriggerOccurrenceKind.Stayed,
                definition.Routes[0].OccurrenceMask);
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Exited,
                definition.Routes[1].OccurrenceMask);
            Assert.AreEqual(0, definition.Routes[0].ExecutionOrder);
            Assert.AreEqual(1, definition.Routes[1].ExecutionOrder);
            Assert.AreEqual(
                GameplayTriggerGameplayBuffActionKinds
                    .ApplyComponentBuffValue,
                definition.Routes[0].Actions[0].ActionKindId);
            Assert.AreEqual(
                GameplayTriggerGameplayBuffActionKinds
                    .RemoveComponentBuffValue,
                definition.Routes[1].Actions[0].ActionKindId);
        }

        [Test]
        public void WorkspaceProjection_PreservesUnsupportedConditionAndReferences()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    "region.start",
                    payload0: 987654L,
                    payload1: 876543L);
            ReplaceConditionRoot(
                trigger,
                entry.ConditionId,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.All,
                    children: new[]
                    {
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.HasSubject),
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.Any,
                            children: new[]
                            {
                                new GameplayTriggerConditionNodeAuthoring(
                                    GameplayTriggerConditionOperation.Payload0,
                                    operand: 77L,
                                    comparison:
                                        GameplayTriggerComparison.Greater),
                                new GameplayTriggerConditionNodeAuthoring(
                                    GameplayTriggerConditionOperation.Not,
                                    children: new[]
                                    {
                                        new GameplayTriggerConditionNodeAuthoring(
                                            GameplayTriggerConditionOperation
                                                .PushFalse)
                                    })
                            })
                    }));
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            string before = module.CaptureDocumentSnapshotForTests();
            string sentence =
                module.GetTriggerEffectSentenceForTests(entry.StableId);
            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            string after = module.CaptureDocumentSnapshotForTests();

            Assert.AreEqual(before, after);
            StringAssert.Contains("未登记 Buff 987654", sentence);
            StringAssert.Contains("未登记来源 876543", sentence);
            Assert.IsFalse(module.IsDirty);
            HelpBox compatibility = workspace.Q<HelpBox>(
                "character-test-trigger-condition-compatibility");
            Assert.IsNotNull(compatibility);
            StringAssert.Contains(
                "全部满足(不存在触发对象，任一满足(参数 0 > 77，非(始终不成立)))",
                compatibility.text);

            CharacterTestLevelGameplayTriggerAuthoringDocument projected =
                ExtractTriggerDocument(after);
            GameplayTriggerConditionAuthoring condition =
                FindCondition(projected.Authoring, entry.ConditionId);
            Assert.IsNotNull(condition);
            GameplayTriggerConditionNodeAuthoring originalRoot =
                condition.Root;
            Assert.AreEqual(
                GameplayTriggerConditionOperation.All,
                condition.Root.Operation);
            Assert.That(condition.Root.Children, Has.Length.EqualTo(2));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.HasSubject,
                condition.Root.Children[0].Operation);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Any,
                condition.Root.Children[1].Operation);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Payload0,
                condition.Root.Children[1].Children[0].Operation);
            Assert.AreEqual(
                77L,
                condition.Root.Children[1].Children[0].Operand);

            Assert.IsTrue(
                module.UpdateTriggerDisplayNameForTests(
                    entry.StableId,
                    "复杂条件保真规则"));
            projected = ExtractTriggerDocument(
                module.CaptureDocumentSnapshotForTests());
            condition = FindCondition(
                projected.Authoring,
                entry.ConditionId);
            AssertConditionTreesEqual(
                originalRoot,
                condition.Root);
        }

        [Test]
        public void WorkspaceConditionBuilder_AddsFirstAndSecondClauseFromMenu()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            Assert.IsTrue(
                trigger.TryUpdateConditionRoot(
                    entry.StableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.PushTrue)));
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace.Q<Label>(
                    className:
                        "mx-level-editor__trigger-condition-empty"));
            Assert.IsNull(
                workspace.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic"));

            InvokeDropdownMenuAction(
                workspace.Q<ToolbarMenu>(
                    "character-test-trigger-condition-add"),
                "触发对象存在");

            workspace = module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace.Q<ToolbarToggle>(
                    "character-test-trigger-condition-negate-0"));
            Assert.IsNull(
                workspace.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic"));

            InvokeDropdownMenuAction(
                workspace.Q<ToolbarMenu>(
                    "character-test-trigger-condition-add"),
                "触发对象存在");

            workspace = module.CreateGameplayTriggerWorkspace(null);
            Assert.AreEqual(
                "全部满足（且）",
                workspace.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic")
                    .value);

            CharacterTestLevelGameplayTriggerAuthoringDocument projected =
                ExtractTriggerDocument(
                    module.CaptureDocumentSnapshotForTests());
            Assert.IsTrue(
                projected.TryGetConditionRoot(
                    projected.Entries[0],
                    out GameplayTriggerConditionNodeAuthoring root));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.All,
                root.Operation);
            Assert.That(root.Children, Has.Length.EqualTo(2));
        }

        [Test]
        public void WorkspaceConditionBuilder_ProjectsAndSwitchesAllAnyAndNot()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            Assert.IsTrue(
                trigger.TryUpdateConditionRoot(
                    entry.StableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.All,
                        children: new[]
                        {
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation
                                    .HasSubject,
                                operand: 1L),
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation.Not,
                                children: new[]
                                {
                                    new GameplayTriggerConditionNodeAuthoring(
                                        GameplayTriggerConditionOperation
                                            .HasSubject,
                                        operand: 1L)
                                })
                        })));
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            PopupField<string> logic = workspace.Q<PopupField<string>>(
                "character-test-trigger-condition-logic");
            Assert.IsNotNull(logic);
            Assert.AreEqual("全部满足（且）", logic.value);
            Assert.AreEqual(
                2,
                workspace.Query<ToolbarToggle>(
                    className:
                        "mx-level-editor__trigger-condition-negate")
                    .ToList()
                    .Count);
            Assert.IsFalse(
                workspace.Q<ToolbarToggle>(
                    "character-test-trigger-condition-negate-0")
                    .value);
            Assert.IsTrue(
                workspace.Q<ToolbarToggle>(
                    "character-test-trigger-condition-negate-1")
                    .value);
            Assert.IsNotNull(
                workspace.Q<ToolbarMenu>(
                    "character-test-trigger-condition-add"));

            logic.value = "任一满足（或）";
            CharacterTestLevelGameplayTriggerAuthoringDocument projected =
                ExtractTriggerDocument(
                    module.CaptureDocumentSnapshotForTests());
            GameplayTriggerConditionAuthoring condition =
                FindCondition(
                    projected.Authoring,
                    entry.ConditionId);
            Assert.IsNotNull(condition);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Any,
                condition.Root.Operation);
            StringAssert.Contains(
                "任一满足",
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId));
            StringAssert.Contains(
                "非(",
                module.GetTriggerEffectSentenceForTests(
                    entry.StableId));

            workspace =
                module.CreateGameplayTriggerWorkspace(null);
            ToolbarToggle firstNegate =
                workspace.Q<ToolbarToggle>(
                    "character-test-trigger-condition-negate-0");
            firstNegate.value = true;
            projected = ExtractTriggerDocument(
                module.CaptureDocumentSnapshotForTests());
            condition = FindCondition(
                projected.Authoring,
                entry.ConditionId);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Not,
                condition.Root.Children[0].Operation);

            workspace =
                module.CreateGameplayTriggerWorkspace(null);
            PressButton(
                workspace.Q<Button>(
                    "character-test-trigger-condition-remove-1"));
            workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNull(
                workspace.Q<PopupField<string>>(
                    "character-test-trigger-condition-logic"));
            PressButton(
                workspace.Q<Button>(
                    "character-test-trigger-condition-remove-0"));
            projected = ExtractTriggerDocument(
                module.CaptureDocumentSnapshotForTests());
            condition = FindCondition(
                projected.Authoring,
                entry.ConditionId);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.PushTrue,
                condition.Root.Operation);
        }

        [Test]
        public void WorkspaceBrowsing_PreservesCanonicalHashesAndSceneCache()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyRemoveBuffPresenceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                "region.start",
                payload0: 42L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            GameManager gameManager =
                Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            CharacterTestGameplayTriggerSceneCache liveCache =
                gameManager.GameplayTriggerSceneCache;
            string liveCanonical =
                liveCache.CanonicalTriggerEnvelopeJson;
            long liveTriggerHash = liveCache.TriggerCatalogHash;
            long liveSensorHash = liveCache.SensorCatalogHash;
            long liveCombinedHash = liveCache.CombinedCatalogHash;

            string beforeJson =
                module.CaptureDocumentSnapshotForTests();
            CharacterTestGameplayTriggerSceneCacheData before =
                CompileSnapshot(module, beforeJson);
            int undoGroup = Undo.GetCurrentGroup();

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            ToolbarSearchField search =
                workspace.Q<ToolbarSearchField>(
                    "character-test-trigger-workspace-search");
            Assert.IsNotNull(search);
            search.value = "601012";
            search.value = string.Empty;
            module.SetTriggerWorkspaceWidthForTests(880f);
            module.SetTriggerWorkspaceWidthForTests(1100f);

            string afterJson =
                module.CaptureDocumentSnapshotForTests();
            CharacterTestGameplayTriggerSceneCacheData after =
                CompileSnapshot(module, afterJson);

            Assert.AreEqual(beforeJson, afterJson);
            Assert.AreEqual(
                before.CanonicalTriggerEnvelopeJson,
                after.CanonicalTriggerEnvelopeJson);
            Assert.AreEqual(
                before.TriggerCatalogHash,
                after.TriggerCatalogHash);
            Assert.AreEqual(
                before.SensorCatalogHash,
                after.SensorCatalogHash);
            Assert.AreEqual(
                before.CombinedCatalogHash,
                after.CombinedCatalogHash);
            Assert.AreEqual(undoGroup, Undo.GetCurrentGroup());
            Assert.IsFalse(module.IsDirty);
            Assert.AreEqual(
                liveCanonical,
                liveCache.CanonicalTriggerEnvelopeJson);
            Assert.AreEqual(
                liveTriggerHash,
                liveCache.TriggerCatalogHash);
            Assert.AreEqual(
                liveSensorHash,
                liveCache.SensorCatalogHash);
            Assert.AreEqual(
                liveCombinedHash,
                liveCache.CombinedCatalogHash);
        }

        [Test]
        public void WorkspaceSearchAndCategories_AreDerivedOnly()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                "region.start",
                42L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            Assert.AreEqual(
                2,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    string.Empty,
                    "全部"));
            Assert.AreEqual(
                1,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    string.Empty,
                    "区域玩法"));
            Assert.AreEqual(
                1,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    string.Empty,
                    "关卡流程"));
            Assert.AreEqual(
                1,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    "601012",
                    "全部"));
            Assert.AreEqual(
                0,
                module.TriggerWorkspaceVisibleEntryCountForTests(
                    "时间",
                    "全部"),
                "Unimplemented source categories must not be synthesized.");
            Assert.IsFalse(
                module.IsDirty,
                "Search and derived category browsing must not dirty authoring.");
        }

        [Test]
        public void WorkspaceNavigation_PreservesSelectionAndScrollWhileBrowsing()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                "region.start",
                payload0: 42L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            string selected =
                module.TriggerWorkspaceSelectedEntryIdForTests;
            Assert.IsNotEmpty(selected);
            module.SetTriggerWorkspaceListScrollYForTests(37f);
            int undoGroup = Undo.GetCurrentGroup();

            ToolbarSearchField search =
                workspace.Q<ToolbarSearchField>(
                    "character-test-trigger-workspace-search");
            Assert.IsNotNull(search);
            search.value = "no-such-trigger";
            Assert.AreEqual(
                selected,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreEqual(
                37f,
                module.TriggerWorkspaceListScrollYForTests);
            search.value = string.Empty;
            Assert.AreEqual(
                selected,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreEqual(
                37f,
                module.TriggerWorkspaceListScrollYForTests);

            Button category = FindButtonStartingWith(
                workspace,
                "区域玩法 ");
            PressButton(category);
            Assert.AreEqual(
                selected,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreEqual(
                37f,
                module.TriggerWorkspaceListScrollYForTests);
            PressButton(FindButtonStartingWith(workspace, "全部 "));
            Assert.AreEqual(
                selected,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreEqual(
                37f,
                module.TriggerWorkspaceListScrollYForTests);
            Assert.AreEqual(undoGroup, Undo.GetCurrentGroup());
            Assert.IsFalse(module.IsDirty);
        }

        [Test]
        public void WorkspaceSceneNavigation_LocatesRegionAndRequiresExplicitMultiSelection()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(
                CreateWorkspaceWorldDocument(module.ManagedSceneRoot));
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                "region.start",
                payload0: 42L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Selection.activeObject = null;
            PressButton(
                workspace.Q<Button>(
                    "character-test-trigger-workspace-locate"));
            Assert.IsNotNull(Selection.activeTransform);
            Assert.AreEqual(
                "LevelRegion_StartArea",
                Selection.activeTransform.name);

            module.PrepareGameplayTriggerWorkspaceRegionForTests(
                "region.start");
            Assert.AreEqual(
                "region.start",
                module.TriggerWorkspaceRegionFilterForTests);
            Assert.IsTrue(
                module.TriggerWorkspaceRequiresExplicitSelectionForTests);
            Assert.IsEmpty(
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.IsFalse(module.IsDirty);
            Selection.activeObject = null;
        }

        [Test]
        public void WorkspaceNavigation_CollapsesOnlyAtNarrowWidth()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.CreateGameplayTriggerWorkspace(null);

            module.SetTriggerWorkspaceWidthForTests(880f);
            Assert.IsTrue(
                module.TriggerWorkspaceNavigationCollapsedForTests);
            module.SetTriggerWorkspaceWidthForTests(1100f);
            Assert.IsFalse(
                module.TriggerWorkspaceNavigationCollapsedForTests);
        }

        [UnityTest]
        public IEnumerator WorkspaceRename_UsesDocumentUndoRedo()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    "region.start",
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            string originalName = entry.DisplayName;
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            Assert.IsTrue(
                module.UpdateTriggerDisplayNameForTests(
                    entry.StableId,
                    "毒雾进入规则"));
            Assert.AreEqual(
                "毒雾进入规则",
                module.GetTriggerDisplayNameForTests(
                    entry.StableId));

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                originalName,
                module.GetTriggerDisplayNameForTests(
                    entry.StableId));

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                "毒雾进入规则",
                module.GetTriggerDisplayNameForTests(
                    entry.StableId));

            Undo.PerformUndo();
            yield return null;
        }

        [Test]
        public void WorkspaceList_ReflectsFormalValidationErrors()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                payload0: 0L);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Label errorStatus = workspace.Q<Label>(
                className:
                    "mx-level-editor__trigger-workspace-item-status--error");

            Assert.IsNotNull(errorStatus);
            Assert.AreEqual("!", errorStatus.text);
        }

        private static LevelWorldObjectAuthoringDocument
            CreateWorkspaceWorldDocument(Transform root)
        {
            Assert.IsNotNull(root);
            return new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = ScenePath,
                RootPath = root.name,
                Regions = new List<LevelWorldObjectRegionDefinition>
                {
                    new LevelWorldObjectRegionDefinition
                    {
                        Enabled = true,
                        DefinitionId =
                            CharacterTestWorldObjectDefinitionIds
                                .RegionStartArea,
                        StableId = "region.start",
                        RegionKind =
                            LevelWorldObjectRegionKind.TriggerRegion,
                        ScaleSource =
                            LevelWorldObjectScaleSource.SceneTransform,
                        TransformRef =
                            LevelTransformReference.FromPath(
                                "TestCourse_00_Start[2]/"
                                + "LevelRegion_StartArea[6]")
                    }
                }
            };
        }

        private static CharacterTestGameplayTriggerSceneCacheData
            CompileSnapshot(
                CharacterTestWorldObjectLevelEditorModule module,
                string json)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(json);
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport
                            worldReport),
                "World-object extraction failed. errors="
                + (worldReport?.ErrorCount ?? -1));
            Assert.IsTrue(
                CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                    envelope,
                    world,
                    module.ManagedSceneRoot,
                    CharacterTestWorldObjectDefinitionCatalog
                        .CreateCharacterTestSceneCatalog(),
                    out CharacterTestGameplayTriggerSceneCacheData data,
                    out LevelWorldObjectAuthoringValidationReport report),
                "Trigger cache compilation failed. errors="
                + (report?.ErrorCount ?? -1));
            return data;
        }

        private static CharacterTestLevelGameplayTriggerAuthoringDocument
            ExtractTriggerDocument(string json)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(json);
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        document,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport report),
                "Trigger extraction failed. errors="
                + (report?.ErrorCount ?? -1));
            Assert.IsTrue(present);
            return document;
        }

        private static GameplayTriggerDefinitionAuthoring FindDefinition(
            GameplayTriggerAuthoringDocument document,
            int definitionId)
        {
            GameplayTriggerDefinitionAuthoring[] definitions =
                document?.Definitions;
            if (definitions == null)
                return null;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i]?.DefinitionId == definitionId)
                    return definitions[i];
            }
            return null;
        }

        private static GameplayTriggerConditionAuthoring FindCondition(
            GameplayTriggerAuthoringDocument document,
            int conditionId)
        {
            GameplayTriggerConditionAuthoring[] conditions =
                document?.Conditions;
            if (conditions == null)
                return null;
            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i]?.ConditionId == conditionId)
                    return conditions[i];
            }
            return null;
        }

        private static void ReplaceConditionRoot(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            int conditionId,
            GameplayTriggerConditionNodeAuthoring root)
        {
            GameplayTriggerConditionAuthoring[] source =
                document.Authoring.Conditions;
            var conditions =
                new GameplayTriggerConditionAuthoring[source.Length];
            bool replaced = false;
            for (int i = 0; i < source.Length; i++)
            {
                GameplayTriggerConditionAuthoring condition = source[i];
                if (condition?.ConditionId != conditionId)
                {
                    conditions[i] = condition;
                    continue;
                }

                conditions[i] =
                    new GameplayTriggerConditionAuthoring(
                        conditionId,
                        root);
                replaced = true;
            }
            Assert.IsTrue(replaced);
            document.Authoring =
                new GameplayTriggerAuthoringDocument(
                    document.Authoring.SchemaId,
                    document.Authoring.SchemaVersion,
                    document.Authoring.Sources,
                    conditions,
                    document.Authoring.Definitions,
                    document.Authoring.Bindings);
        }

        private static Button FindButtonStartingWith(
            VisualElement root,
            string text)
        {
            Button result = null;
            root.Query<Button>().ForEach(
                button =>
                {
                    if (result == null
                        && button.text.StartsWith(
                            text,
                            StringComparison.Ordinal))
                    {
                        result = button;
                    }
                });
            Assert.IsNotNull(result);
            return result;
        }

        private static void PressButton(Button button)
        {
            Assert.IsNotNull(button);
            object clickable = typeof(Button)
                .GetProperty(
                    "clickable",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.IsNotNull(clickable);

            MethodInfo[] methods = clickable.GetType().GetMethods(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            MethodInfo invoke = null;
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(
                        methods[i].Name,
                        "Invoke",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    methods[i].GetParameters();
                if (parameters.Length == 0
                    || (parameters.Length == 1
                        && typeof(EventBase).IsAssignableFrom(
                            parameters[0].ParameterType)))
                {
                    invoke = methods[i];
                    break;
                }
            }

            Assert.IsNotNull(invoke);
            if (invoke.GetParameters().Length == 0)
            {
                invoke.Invoke(clickable, Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
                invoke.Invoke(clickable, new object[] { click });
        }

        private static void InvokeDropdownMenuAction(
            ToolbarMenu menu,
            string actionName)
        {
            Assert.IsNotNull(menu);
            IReadOnlyList<DropdownMenuItem> items =
                menu.menu.MenuItems();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is DropdownMenuAction action
                    && string.Equals(
                        action.name,
                        actionName,
                        StringComparison.Ordinal))
                {
                    action.Execute();
                    return;
                }
            }

            Assert.Fail(
                "Dropdown action was not found: " + actionName);
        }

        private static void AssertConditionTreesEqual(
            GameplayTriggerConditionNodeAuthoring expected,
            GameplayTriggerConditionNodeAuthoring actual)
        {
            if (expected == null || actual == null)
            {
                Assert.AreSame(expected, actual);
                return;
            }

            Assert.AreEqual(expected.Operation, actual.Operation);
            Assert.AreEqual(expected.Operand, actual.Operand);
            Assert.AreEqual(expected.Comparison, actual.Comparison);
            int expectedCount = expected.Children?.Length ?? 0;
            int actualCount = actual.Children?.Length ?? 0;
            Assert.AreEqual(expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                AssertConditionTreesEqual(
                    expected.Children[i],
                    actual.Children[i]);
            }
        }
    }
}
