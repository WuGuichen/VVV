using System;
using System.Collections;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Editor;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGameplayTriggerWorkspaceTransactionTests
    {
        private const string RegionStableId = "region.p3.3b.transactions";

        [UnityTest]
        public IEnumerator CreateDuplicateDelete_AreSingleUndoSteps()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);

            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument());
            module.SetTriggerDocumentForTests(
                new CharacterTestLevelGameplayTriggerAuthoringDocument(),
                markDirty: false);
            string baseline = module.CaptureDocumentSnapshotForTests();
            Assert.IsFalse(module.IsDirty);

            CharacterTestLevelGameplayTriggerEntry source =
                module.AddTriggerTemplateForTests(
                    RegionStableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId);
            Assert.IsNotNull(source);
            string sourceStableId = source.StableId;
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    sourceStableId,
                    out long defaultStoryTriggerId,
                    out _,
                    out _));
            Assert.AreEqual(
                CharacterTestStoryIds.Triggers.LevelStartRegion,
                defaultStoryTriggerId);
            string created = module.CaptureDocumentSnapshotForTests();
            Assert.AreNotEqual(baseline, created);
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            AssertHealthyProjection(module, sourceStableId);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                baseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(0, module.TriggerEntryCountForTests);
            Assert.IsFalse(module.IsDirty);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                created,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            AssertHealthyProjection(module, sourceStableId);

            Assert.IsTrue(
                module.DuplicateTriggerEntryForTests(
                    sourceStableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate));
            Assert.IsNotNull(duplicate);
            AssertEveryOwnedIdIsUnique(source, duplicate);
            string duplicateStableId = duplicate.StableId;
            string duplicated = module.CaptureDocumentSnapshotForTests();
            Assert.AreEqual(2, module.TriggerEntryCountForTests);
            AssertHealthyProjection(module, duplicateStableId);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                created,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(1, module.TriggerEntryCountForTests);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                duplicated,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(2, module.TriggerEntryCountForTests);
            AssertHealthyProjection(module, duplicateStableId);

            Assert.IsTrue(
                module.RemoveTriggerEntryForTests(duplicateStableId));
            string deleted = module.CaptureDocumentSnapshotForTests();
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            Assert.AreEqual(6, module.TriggerRetiredIdCountForTests);
            AssertWorldRegionStillExists(deleted);
            AssertHealthyProjection(module, sourceStableId);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                duplicated,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(2, module.TriggerEntryCountForTests);
            Assert.AreEqual(0, module.TriggerRetiredIdCountForTests);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                deleted,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            Assert.AreEqual(6, module.TriggerRetiredIdCountForTests);
            AssertWorldRegionStillExists(deleted);

            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                baseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);
        }

        [UnityTest]
        public IEnumerator PlayerHpThreshold_CreateEditDuplicateDelete_AreUndoable()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);

            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument());
            module.SetTriggerDocumentForTests(
                new CharacterTestLevelGameplayTriggerAuthoringDocument(),
                markDirty: false);
            string baseline = module.CaptureDocumentSnapshotForTests();

            CharacterTestLevelGameplayTriggerEntry source =
                module.AddPlayerHpLowStoryTriggerForTests();
            Assert.IsNotNull(source);
            string sourceStableId = source.StableId;
            string created = module.CaptureDocumentSnapshotForTests();
            AssertHealthyProjection(module, sourceStableId);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                baseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                created,
                module.CaptureDocumentSnapshotForTests());

            yield return AssertMutationUndoRedo(
                module,
                sourceStableId,
                () => module.UpdatePlayerHpThresholdsForTests(
                    sourceStableId,
                    enterThreshold: 24,
                    exitThreshold: 32),
                () =>
                {
                    StringAssert.Contains(
                        "降至 24 或以下",
                        module.GetTriggerEffectSentenceForTests(
                            sourceStableId));
                    StringAssert.Contains(
                        "恢复到 32 以上",
                        module.GetTriggerEffectSentenceForTests(
                            sourceStableId));
                },
                expectedSentenceFragment: "降至 24 或以下");
            string edited = module.CaptureDocumentSnapshotForTests();

            Assert.IsTrue(
                module.DuplicateTriggerEntryForTests(
                    sourceStableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate));
            AssertEveryOwnedIdIsUnique(source, duplicate);
            string duplicated =
                module.CaptureDocumentSnapshotForTests();
            AssertHealthyProjection(module, duplicate.StableId);
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                edited,
                module.CaptureDocumentSnapshotForTests());
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                duplicated,
                module.CaptureDocumentSnapshotForTests());

            Assert.IsTrue(
                module.RemoveTriggerEntryForTests(duplicate.StableId));
            string deleted = module.CaptureDocumentSnapshotForTests();
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                duplicated,
                module.CaptureDocumentSnapshotForTests());
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                deleted,
                module.CaptureDocumentSnapshotForTests());
        }

        [Test]
        public void StoryFixture_ProvidesFormalTriggerCatalogEntries()
        {
            StoryGraphDefinition graph =
                CharacterTestStoryFixture.CreateBootstrapGraph();
            StoryBeatDefinition start =
                FindStoryBeat(
                    graph,
                    CharacterTestStoryIds.Beats.LevelStartRegion);
            StoryBeatDefinition complete =
                FindStoryBeat(
                    graph,
                    CharacterTestStoryIds.Beats.TrainingComplete);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestStoryIds.Triggers.LevelStartRegion
                },
                start.TriggerIds);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestStoryIds.Triggers.TrainingComplete
                },
                complete.TriggerIds);

            CharacterTestStoryContent content =
                CharacterTestStoryFixture.CreateBootstrapContent();
            Assert.IsTrue(
                content.TryResolveStepText(
                    CharacterTestStoryIds.Steps.LevelStartRegionLine,
                    out string startText));
            Assert.AreEqual(
                CharacterTestStoryText.LevelStartRegionLine,
                startText);
            Assert.IsTrue(
                content.TryResolveStepText(
                    CharacterTestStoryIds.Steps.TrainingCompleteLine,
                    out string completeText));
            Assert.AreEqual(
                CharacterTestStoryText.TrainingCompleteLine,
                completeText);
        }

        [UnityTest]
        public IEnumerator StoryReference_UsesFormalCatalogAndOneUndoStep()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    RegionStableId,
                    payload0:
                        CharacterTestStoryIds.Triggers.LevelStartRegion);
            string stableId = entry.StableId;
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument());
            module.SetTriggerDocumentForTests(trigger, markDirty: false);
            string baseline = module.CaptureDocumentSnapshotForTests();

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            VisualElement reference =
                workspace.Q<VisualElement>(
                    "character-test-trigger-story-reference");
            Assert.IsNotNull(reference);
            DropdownField picker =
                reference.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.IsNotNull(picker);
            CollectionAssert.AreEqual(
                new[]
                {
                    "450401 · 起始区域引导",
                    "450402 · 训练完成"
                },
                picker.choices);
            Assert.AreEqual(
                "450401 · 起始区域引导",
                picker.value);
            Assert.IsNull(
                workspace.Q<IntegerField>(
                    "character-test-trigger-story-id"));

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPayloadsForTests(
                    stableId,
                    CharacterTestStoryIds.Triggers.TrainingComplete,
                    payload1: 0L,
                    payload2: 0L),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerPayloadsForTests(
                            stableId,
                            out long storyTriggerId,
                            out _,
                            out _));
                    Assert.AreEqual(
                        CharacterTestStoryIds.Triggers.TrainingComplete,
                        storyTriggerId);
                },
                expectedSentenceFragment:
                    "450402 · 训练完成");

            Assert.IsTrue(
                module.UpdateTriggerPayloadsForTests(
                    stableId,
                    payload0: 999999L,
                    payload1: 0L,
                    payload2: 0L));
            workspace =
                module.CreateGameplayTriggerWorkspace(null);
            reference =
                workspace.Q<VisualElement>(
                    "character-test-trigger-story-reference");
            picker =
                reference.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.AreEqual(
                "未登记剧情触发 999999",
                picker.value);
            Assert.IsNotNull(
                reference.Q<HelpBox>(
                    MxAuthoringReferenceField
                        .DiagnosticElementName));
            StringAssert.Contains(
                "未登记剧情触发 999999",
                module.GetTriggerEffectSentenceForTests(stableId));

            workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    stableId,
                    out long preservedStoryTriggerId,
                    out _,
                    out _));
            Assert.AreEqual(999999L, preservedStoryTriggerId);
            Assert.AreEqual(
                "未登记剧情触发 999999",
                workspace
                    .Q<VisualElement>(
                        "character-test-trigger-story-reference")
                    .Q<DropdownField>(
                        MxAuthoringReferenceField.ValueElementName)
                    .value);

            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                baseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);
        }

        [UnityTest]
        public IEnumerator EditableFields_AreIndependentUndoSteps()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);

            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    RegionStableId,
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId);
            string stableId = entry.StableId;

            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument());
            module.SetTriggerDocumentForTests(trigger, markDirty: false);
            string baseline = module.CaptureDocumentSnapshotForTests();
            Assert.IsFalse(module.IsDirty);

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerDisplayNameForTests(
                    stableId,
                    "毒雾规则"),
                () => Assert.AreEqual(
                    "毒雾规则",
                    module.GetTriggerDisplayNameForTests(stableId)));

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.SetTriggerEnabledForTests(
                    stableId,
                    enabled: false),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerSettingsForTests(
                            stableId,
                            out bool enabled,
                            out _,
                            out _,
                            out _,
                            out _));
                    Assert.IsFalse(enabled);
                });

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerSubjectCapabilitiesForTests(
                    stableId,
                    GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerSubjectFilterForTests(
                            stableId,
                            out CharacterTestLevelGameplayTriggerSubjectFilter
                                filter));
                    Assert.AreEqual(
                        GameplayTargetCapabilities.Damageable
                        | GameplayTargetCapabilities.Movable,
                        filter.RequiredCapabilities);
                    AssertLockedIdentityAndAlive(filter);
                },
                expectedSentenceFragment: "可移动");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerSubjectRelationForTests(
                    stableId,
                    GameplayTargetRelationFilter.Enemy,
                    referenceTeamId: 1),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerSubjectFilterForTests(
                            stableId,
                            out CharacterTestLevelGameplayTriggerSubjectFilter
                                filter));
                    Assert.AreEqual(
                        GameplayTargetRelationFilter.Enemy,
                        filter.RelationFilter);
                    Assert.AreEqual(1, filter.ReferenceTeamId);
                    AssertLockedIdentityAndAlive(filter);
                },
                expectedSentenceFragment: "与队伍 1 敌对");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerConditionRootForTests(
                    stableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.Any,
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
                        })),
                () => StringAssert.Contains(
                    "任一满足",
                    module.GetTriggerEffectSentenceForTests(
                        stableId)),
                expectedSentenceFragment: "非(");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPayloadsForTests(
                    stableId,
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerPayloadsForTests(
                            stableId,
                            out long buffId,
                            out long sourceId,
                            out _));
                    Assert.AreEqual(
                        CharacterTestGuardAuraIds
                            .OnHitDefenseBreakBuffId,
                        buffId);
                    Assert.AreEqual(
                        CharacterTestPoisonIds
                            .PoisonBottlePoolSourceId,
                        sourceId);
                },
                expectedSentenceFragment: "601009");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPayloadsForTests(
                    stableId,
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    CharacterTestAreaConfinementIds.MechanicSourceId),
                () =>
                {
                    Assert.IsTrue(
                        module.TryGetTriggerPayloadsForTests(
                            stableId,
                            out _,
                            out long sourceId,
                            out _));
                    Assert.AreEqual(
                        CharacterTestAreaConfinementIds.MechanicSourceId,
                        sourceId);
                },
                expectedSentenceFragment: "501010");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPolicyForTests(
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames: 0L,
                    maxActivations: 1),
                () => AssertPolicy(
                    module,
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames: 0L,
                    maxActivations: 1));

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPolicyForTests(
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames:
                        CharacterTestDeterministicUnits.DefaultTickRate,
                    maxActivations: 1),
                () => AssertPolicy(
                    module,
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    CharacterTestDeterministicUnits.DefaultTickRate,
                    maxActivations: 1),
                expectedSentenceFragment: "冷却 1 秒");

            yield return AssertMutationUndoRedo(
                module,
                stableId,
                () => module.UpdateTriggerPolicyForTests(
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames:
                        CharacterTestDeterministicUnits.DefaultTickRate,
                    maxActivations: 2),
                () => AssertPolicy(
                    module,
                    stableId,
                    GameplayTriggerStateScope.PerInstance,
                    GameplayTriggerPresenceLimit.Ignore,
                    CharacterTestDeterministicUnits.DefaultTickRate,
                    maxActivations: 2),
                expectedSentenceFragment: "最多触发 2 次");

            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsNotNull(
                workspace
                    .Query<Label>(
                        className:
                            "mx-level-editor__trigger-readonly")
                    .ToList()
                    .Find(label =>
                        label.text.Contains("角色")
                        && label.text.Contains("可伤害")
                        && label.text.Contains("可移动")
                        && label.text.Contains("存活")));
            Assert.IsNull(
                workspace.Q<Toggle>(
                    "character-test-trigger-subject-require-alive"));
            Assert.IsNull(
                workspace.Q<DropdownField>(
                    "character-test-trigger-subject-identity"));

            for (int i = 0; i < 9; i++)
            {
                Undo.PerformUndo();
                yield return null;
            }
            Assert.AreEqual(
                baseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);
        }

        private static IEnumerator AssertMutationUndoRedo(
            CharacterTestWorldObjectLevelEditorModule module,
            string stableId,
            Func<bool> apply,
            Action assertAfter,
            string expectedSentenceFragment = null)
        {
            string before = module.CaptureDocumentSnapshotForTests();
            string beforeSentence =
                module.GetTriggerEffectSentenceForTests(stableId);
            bool beforeDirty = module.IsDirty;

            Assert.IsTrue(apply());
            string after = module.CaptureDocumentSnapshotForTests();
            Assert.AreNotEqual(before, after);
            assertAfter();
            AssertHealthyProjection(module, stableId);
            string afterSentence =
                module.GetTriggerEffectSentenceForTests(stableId);
            if (!string.IsNullOrWhiteSpace(expectedSentenceFragment))
            {
                StringAssert.Contains(
                    expectedSentenceFragment,
                    afterSentence);
            }

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                before,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(
                beforeSentence,
                module.GetTriggerEffectSentenceForTests(stableId));
            Assert.AreEqual(beforeDirty, module.IsDirty);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                after,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(
                afterSentence,
                module.GetTriggerEffectSentenceForTests(stableId));
            assertAfter();
            AssertHealthyProjection(module, stableId);
        }

        private static void AssertLockedIdentityAndAlive(
            CharacterTestLevelGameplayTriggerSubjectFilter filter)
        {
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                filter.AcceptedIdentityTypes);
            Assert.IsTrue(filter.RequireAlive);
        }

        private static void AssertPolicy(
            CharacterTestWorldObjectLevelEditorModule module,
            string stableId,
            GameplayTriggerStateScope expectedScope,
            GameplayTriggerPresenceLimit expectedPresence,
            long cooldownFrames,
            int maxActivations)
        {
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out _,
                    out GameplayTriggerStateScope scope,
                    out GameplayTriggerPresenceLimit presence,
                    out long cooldown,
                    out int maximum));
            Assert.AreEqual(expectedScope, scope);
            Assert.AreEqual(expectedPresence, presence);
            Assert.AreEqual(cooldownFrames, cooldown);
            Assert.AreEqual(maxActivations, maximum);
        }

        private static StoryBeatDefinition FindStoryBeat(
            StoryGraphDefinition graph,
            int beatId)
        {
            Assert.IsNotNull(graph);
            for (int i = 0; i < graph.Beats.Count; i++)
            {
                StoryBeatDefinition beat = graph.Beats[i];
                if (beat?.BeatId == beatId)
                    return beat;
            }

            Assert.Fail("Story beat was not found: " + beatId);
            return null;
        }

        private static void AssertEveryOwnedIdIsUnique(
            CharacterTestLevelGameplayTriggerEntry source,
            CharacterTestLevelGameplayTriggerEntry duplicate)
        {
            Assert.AreNotEqual(source.StableId, duplicate.StableId);
            Assert.AreNotEqual(
                source.SourceDefinitionId,
                duplicate.SourceDefinitionId);
            Assert.AreNotEqual(source.ConditionId, duplicate.ConditionId);
            Assert.AreNotEqual(source.DefinitionId, duplicate.DefinitionId);
            Assert.AreNotEqual(source.BindingId, duplicate.BindingId);
            Assert.AreNotEqual(
                source.SourceBindingId,
                duplicate.SourceBindingId);
            Assert.IsNotNull(source.RouteIds);
            Assert.IsNotNull(duplicate.RouteIds);
            Assert.AreEqual(source.RouteIds.Count, duplicate.RouteIds.Count);
            for (int i = 0; i < source.RouteIds.Count; i++)
            {
                for (int j = 0; j < duplicate.RouteIds.Count; j++)
                {
                    Assert.AreNotEqual(
                        source.RouteIds[i],
                        duplicate.RouteIds[j]);
                }
            }
        }

        private static void AssertHealthyProjection(
            CharacterTestWorldObjectLevelEditorModule module,
            string stableId)
        {
            Assert.IsTrue(module.IsDirty);
            StringAssert.StartsWith(
                "效果：",
                module.GetTriggerEffectSentenceForTests(stableId));

            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(
                    module.CaptureDocumentSnapshotForTests());
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport
                            worldReport),
                JoinIssues(worldReport));
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        trigger,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport loadReport),
                JoinIssues(loadReport));
            Assert.IsTrue(modulePresent);
            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(trigger, world, out _);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
        }

        private static void AssertWorldRegionStillExists(string snapshot)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(snapshot);
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsNotNull(world.Regions);
            Assert.IsNotNull(
                world.Regions.Find(
                    region => region != null
                        && region.StableId == RegionStableId));
        }

        private static LevelWorldObjectAuthoringDocument
            CreateWorldDocument()
        {
            return new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot",
                Regions = new List<LevelWorldObjectRegionDefinition>
                {
                    new LevelWorldObjectRegionDefinition
                    {
                        Enabled = true,
                        DefinitionId =
                            CharacterTestWorldObjectDefinitionIds
                                .RegionStartArea,
                        StableId = RegionStableId,
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

        private static string JoinIssues(
            LevelWorldObjectAuthoringValidationReport report)
        {
            if (report?.Issues == null || report.Issues.Count == 0)
                return string.Empty;

            var rows = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                rows[i] = issue == null
                    ? "[null]"
                    : issue.Code + ": " + issue.Message;
            }
            return string.Join("\n", rows);
        }
    }
}
