using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelGameplayTriggerAuthoringTests
    {
        [Test]
        public void EmptyPayload_RoundTripsAndPreservesUnknownEnvelopeModule()
        {
            var envelope = new MxLevelAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootTransformRef =
                    LevelTransformReference.FromPath("CharacterTestSceneRoot")
            };
            var unknownPayload = new JObject
            {
                ["keep"] = "unchanged",
                ["nested"] = new JObject
                {
                    ["value"] = 7
                }
            };
            envelope.UpsertModulePayload(
                "third.party.levelModule",
                7,
                unknownPayload,
                "Third Party");
            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    new CharacterTestLevelGameplayTriggerAuthoringDocument());

            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument reopened =
                MxLevelAuthoringJson.LoadFromJson(json);

            Assert.IsTrue(
                reopened.TryGetModule(
                    "third.party.levelModule",
                    out MxLevelAuthoringModuleDocument unknown));
            Assert.IsTrue(JToken.DeepEquals(unknownPayload, unknown.Payload));
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    reopened,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        trigger,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport loadReport),
                JoinIssues(loadReport));
            Assert.IsTrue(present);
            Assert.AreEqual(0, trigger.Entries.Count);

            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(
                        trigger,
                        CreateWorldDocument("region.start"),
                        out GameplayTriggerCompileResult compile);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.IsNull(compile);
        }

        [Test]
        public void ValidPayload_CanonicalRoundTripCompilesWithFormalCompiler()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.start");
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                CreateTriggerDocument(
                    "region.start",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            Assert.IsTrue(
                trigger.TryUpdateTemplatePayloads(
                    trigger.Entries[0].StableId,
                    CharacterTestGuardAuraIds.BuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId));
            Assert.IsTrue(
                trigger.TryUpdateSubjectRelation(
                    trigger.Entries[0].StableId,
                    GameplayTargetRelationFilter.Enemy,
                    referenceTeamId: 2));
            Assert.IsTrue(
                trigger.TryUpdateSubjectRequiredCapabilities(
                    trigger.Entries[0].StableId,
                    GameplayTargetCapabilities.Damageable
                        | GameplayTargetCapabilities.Movable));
            Assert.Throws<ArgumentException>(
                () => trigger.TryUpdateSubjectRequiredCapabilities(
                    trigger.Entries[0].StableId,
                    GameplayTargetCapabilities.Movable));
            var envelope = CharacterTestWorldObjectAuthoringAdapter
                .CreateEnvelopeFromDocument(world);
            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    trigger);

            string first = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument reopened =
                MxLevelAuthoringJson.LoadFromJson(first);
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    reopened,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        roundTrip,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport loadReport),
                JoinIssues(loadReport));
            Assert.IsTrue(present);
            Assert.IsTrue(
                roundTrip.TryGetTemplatePayloads(
                    roundTrip.Entries[0],
                    out _,
                    out long roundTripAttribution,
                    out _));
            Assert.AreEqual(
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                roundTripAttribution);
            Assert.IsTrue(
                roundTrip.TryGetSubjectFilter(
                    roundTrip.Entries[0],
                    out CharacterTestLevelGameplayTriggerSubjectFilter
                        roundTripFilter));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                roundTripFilter.AcceptedIdentityTypes);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                roundTripFilter.RequiredCapabilities);
            Assert.IsTrue(roundTripFilter.RequireAlive);
            Assert.AreEqual(
                GameplayTargetRelationFilter.Enemy,
                roundTripFilter.RelationFilter);
            Assert.AreEqual(2, roundTripFilter.ReferenceTeamId);
            reopened =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    reopened,
                    roundTrip);
            Assert.AreEqual(
                first,
                MxLevelAuthoringJson.SaveToJson(reopened));

            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(
                        roundTrip,
                        world,
                        out GameplayTriggerCompileResult compile);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.IsNotNull(compile);
            Assert.IsTrue(compile.Succeeded);
            Assert.AreEqual(1, compile.CompiledCatalog.SourceDefinitionCount);
            Assert.AreEqual(1, compile.CompiledCatalog.DefinitionCount);
            Assert.AreEqual(1, compile.CompiledCatalog.BindingCount);
        }

        [Test]
        public void BooleanConditionTree_RoundTripsAndUsesFormalCompiler()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.boolean");
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                CreateTriggerDocument(
                    "region.boolean",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.Entries[0];
            var root = new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.Any,
                children: new[]
                {
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.HasSubject,
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
                });
            Assert.IsTrue(
                trigger.TryUpdateConditionRoot(
                    entry.StableId,
                    root));

            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter
                    .CreateEnvelopeFromDocument(world);
            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    trigger);
            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument reopened =
                MxLevelAuthoringJson.LoadFromJson(json);
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    reopened,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        roundTrip,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport loadReport),
                JoinIssues(loadReport));
            Assert.IsTrue(present);
            Assert.IsTrue(
                roundTrip.TryGetConditionRoot(
                    roundTrip.Entries[0],
                    out GameplayTriggerConditionNodeAuthoring
                        roundTripRoot));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Any,
                roundTripRoot.Operation);
            Assert.That(
                roundTripRoot.Children,
                Has.Length.EqualTo(2));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Not,
                roundTripRoot.Children[1].Operation);

            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(
                        roundTrip,
                        world,
                        out GameplayTriggerCompileResult compile);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.IsNotNull(compile);
            Assert.IsTrue(compile.Succeeded);
        }

        [Test]
        public void LegacyV1Payload_MigratesTemplateSubjectFiltersAndSavesAsV2()
        {
            AssertLegacyV1SubjectFilterMigration(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                GameplayTargetCapabilities.Damageable);
            AssertLegacyV1SubjectFilterMigration(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                GameplayTargetCapabilities.None);
        }

        [Test]
        public void P3CommonReferences_UseFormalTargetsAndAttributionCatalogs()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.references");
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(world);

            CharacterTestLevelGameplayTriggerAuthoringDocument buff =
                CreateTriggerDocument(
                    "region.references",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            module.SetTriggerDocumentForTests(buff, markDirty: false);
            Assert.IsTrue(
                module.TryGetTriggerTargetReferenceForTests(
                    buff.Entries[0].StableId,
                    out GameplayTriggerTargetSelector selector,
                    out string targetName,
                    out int targetCount));
            Assert.AreEqual(GameplayTriggerTargetSelector.Subject, selector);
            Assert.AreEqual("进入区域的角色", targetName);
            Assert.AreEqual(1, targetCount);
            Assert.IsTrue(
                module.TryGetTriggerTargetSemanticsForTests(
                    buff.Entries[0].StableId,
                    out selector,
                    out GameplayTargetClassificationComponent
                        classification,
                    out GameplayTargetRelationFilter relationFilter,
                    out bool relationApplies,
                    out string targetSummary));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                classification.Capabilities);
            Assert.AreEqual(
                GameplayTargetRelationFilter.Any,
                relationFilter);
            Assert.IsTrue(relationApplies);
            Assert.AreEqual(
                "角色 · 可伤害/可移动 · 不限关系",
                targetSummary);

            CharacterTestLevelGameplayTriggerAuthoringDocument ownerTarget =
                CreateTriggerDocument(
                    "region.references",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue,
                    GameplayTriggerTargetSelector.Owner);
            module.SetTriggerDocumentForTests(
                ownerTarget,
                markDirty: false);
            Assert.IsFalse(
                module.TryGetTriggerTargetReferenceForTests(
                    ownerTarget.Entries[0].StableId,
                    out _,
                    out _,
                    out _),
                "Owner is not a valid target for the region Buff templates.");

            CharacterTestLevelGameplayTriggerAuthoringDocument
                instigatorTarget =
                    CreateTriggerDocument(
                        "region.references",
                        GameplayTriggerGameplayBuffActionKinds
                            .ApplyComponentBuffValue,
                        GameplayTriggerTargetSelector.Instigator);
            module.SetTriggerDocumentForTests(
                instigatorTarget,
                markDirty: false);
            Assert.IsFalse(
                module.TryGetTriggerTargetReferenceForTests(
                    instigatorTarget.Entries[0].StableId,
                    out _,
                    out _,
                    out _),
                "Instigator is not a valid target for the region Buff templates.");

            CharacterTestLevelGameplayTriggerAuthoringDocument story =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry storyEntry =
                story.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    "region.references",
                    payload0: 41L);
            module.SetTriggerDocumentForTests(story, markDirty: false);
            Assert.IsTrue(
                module.TryGetTriggerTargetReferenceForTests(
                    storyEntry.StableId,
                    out selector,
                    out targetName,
                    out targetCount));
            Assert.AreEqual(
                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                selector);
            Assert.AreEqual("当前区域", targetName);
            Assert.AreEqual(1, targetCount);
            Assert.IsTrue(
                module.TryGetTriggerTargetSemanticsForTests(
                    storyEntry.StableId,
                    out selector,
                    out classification,
                    out relationFilter,
                    out relationApplies,
                    out targetSummary));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.SpatialRegion,
                classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.None,
                classification.Capabilities);
            Assert.IsFalse(relationApplies);
            Assert.AreEqual(
                "区域",
                targetSummary);

            CollectionAssert.AreEqual(
                new long[]
                {
                    0L,
                    CharacterTestGuardAuraIds.AuraId,
                    CharacterTestAreaConfinementIds.MechanicSourceId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId
                },
                CharacterTestWorldObjectLevelEditorModule
                    .GetTriggerBuffAttributionIdsForTests());
            CollectionAssert.DoesNotContain(
                CharacterTestWorldObjectLevelEditorModule
                    .GetTriggerBuffAttributionIdsForTests(),
                (long)CharacterTestArcMineIds.MechanicSourceId);
            CollectionAssert.DoesNotContain(
                CharacterTestWorldObjectLevelEditorModule
                    .GetTriggerBuffAttributionIdsForTests(),
                (long)CharacterTestRuneTurretIds.MechanicSourceId);
        }

        [Test]
        public void Validator_BlocksMissingRegionDuplicateIdsUnknownActionAndCapacity()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.start");

            CharacterTestLevelGameplayTriggerAuthoringDocument missingRegion =
                CreateTriggerDocument(
                    "region.missing",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            AssertCode(
                Validate(missingRegion, world),
                "CT_TRIGGER_REGION_MISSING");

            CharacterTestLevelGameplayTriggerAuthoringDocument duplicate =
                CreateTriggerDocument(
                    "region.start",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            duplicate.Entries.Add(duplicate.Entries[0].Normalize());
            AssertCode(
                Validate(duplicate, world),
                "CT_TRIGGER_STABLE_ID_DUPLICATE");
            AssertCode(
                Validate(duplicate, world),
                "CT_TRIGGER_ENTRY_OWNERSHIP_DUPLICATE");

            CharacterTestLevelGameplayTriggerAuthoringDocument unknownAction =
                CreateTriggerDocument("region.start", 987654321);
            AssertCode(
                Validate(unknownAction, world),
                "CT_TRIGGER_COMPILE_UnknownActionContract");

            CharacterTestLevelGameplayTriggerAuthoringDocument badCapacity =
                CreateTriggerDocument(
                    "region.start",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            badCapacity.RegionBindings[0].MaxMappedSubjects =
                badCapacity.RegionBindings[0].MaxQueryHits + 1;
            AssertCode(
                Validate(badCapacity, world),
                "CT_TRIGGER_CAPACITY_INVALID");

            CharacterTestLevelGameplayTriggerAuthoringDocument
                unsupportedTemplate =
                    CreateTriggerDocument(
                        "region.start",
                        GameplayTriggerGameplayBuffActionKinds
                            .ApplyComponentBuffValue);
            unsupportedTemplate.Entries[0].TemplateId =
                "region.unsupported.v1";
            AssertCode(
                Validate(unsupportedTemplate, world),
                "CT_TRIGGER_TEMPLATE_UNSUPPORTED");

            var invalidPayload =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            invalidPayload.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                payload0: 0L);
            AssertCode(
                Validate(invalidPayload, world),
                "CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID");
        }

        [Test]
        public void RemovingEntry_RetiresEveryOwnedIdAndKeepsWorldRegion()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.start");
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                CreateTriggerDocument(
                    "region.start",
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            Assert.IsTrue(
                trigger.TryUpdateTemplatePayloads(
                    trigger.Entries[0].StableId,
                    CharacterTestGuardAuraIds.BuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId));

            Assert.IsTrue(trigger.TryRemoveEntry("trigger.region.start"));
            Assert.AreEqual(0, trigger.Entries.Count);
            Assert.AreEqual(0, trigger.RegionBindings.Count);
            Assert.AreEqual(0, trigger.Authoring.Sources.Length);
            Assert.AreEqual(0, trigger.Authoring.Conditions.Length);
            Assert.AreEqual(0, trigger.Authoring.Definitions.Length);
            Assert.AreEqual(0, trigger.Authoring.Bindings.Length);
            Assert.AreEqual(6, trigger.IdState.RetiredIds.Count);
            Assert.AreEqual(1, world.Regions.Count);
            Assert.AreEqual("region.start", world.Regions[0].StableId);
            Assert.IsFalse(Validate(trigger, world).HasErrors);
        }

        [Test]
        public void IdAllocator_IsMonotonicAndDoesNotReuseRetiredIds()
        {
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            Assert.AreEqual(
                1,
                trigger.AllocateId(
                    GameplayTriggerConfigIdKind.SourceDefinition));
            Assert.AreEqual(
                2,
                trigger.AllocateId(
                    GameplayTriggerConfigIdKind.SourceDefinition));
            Assert.AreEqual(
                1,
                trigger.AllocateId(GameplayTriggerConfigIdKind.Route));
            Assert.AreEqual(
                2,
                trigger.AllocateId(GameplayTriggerConfigIdKind.Route));
        }

        [Test]
        public void FixedTemplates_MapToExactGameplayTriggerDomainRows()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.start");

            var story =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry storyEntry =
                story.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    "region.start",
                    payload0: 41,
                    payload1: 2,
                    payload2: 3);
            GameplayTriggerDefinitionAuthoring storyDefinition =
                story.Authoring.Definitions[0];
            GameplayTriggerRouteAuthoring storyRoute =
                storyDefinition.Routes[0];
            GameplayTriggerActionAuthoring storyAction =
                storyRoute.Actions[0];
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Entered,
                storyRoute.OccurrenceMask);
            Assert.AreEqual(
                GameplayTriggerStateScope.PerInstance,
                storyRoute.StateScope);
            Assert.AreEqual(1, storyRoute.MaxActivations);
            Assert.AreEqual(
                GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue,
                storyAction.ActionKindId);
            Assert.AreEqual(
                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                storyAction.TargetSelector);
            Assert.AreEqual(41, storyAction.Payload0);
            Assert.AreEqual(2, storyAction.Payload1);
            Assert.AreEqual(3, storyAction.Payload2);
            Assert.AreEqual(
                storyEntry.SourceBindingId,
                story.Authoring.Bindings[0].ExplicitTargets[0].Value);
            Assert.IsFalse(Validate(story, world).HasErrors);

            var enterBuff =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            enterBuff.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                "region.start",
                payload0: 51,
                payload1: 6);
            GameplayTriggerRouteAuthoring buffRoute =
                enterBuff.Authoring.Definitions[0].Routes[0];
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Entered,
                buffRoute.OccurrenceMask);
            Assert.AreEqual(
                GameplayTriggerStateScope.PerSubject,
                buffRoute.StateScope);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.OncePerPresence,
                buffRoute.PresenceLimit);
            Assert.AreEqual(
                GameplayTriggerGameplayBuffActionKinds
                    .ApplyComponentBuffValue,
                buffRoute.Actions[0].ActionKindId);
            Assert.AreEqual(51, buffRoute.Actions[0].Payload0);
            Assert.AreEqual(6, buffRoute.Actions[0].Payload1);
            Assert.AreEqual(
                64,
                enterBuff.RegionBindings[0].MaxQueryHits,
                "Combat Physics query candidates and mapped gameplay subjects need separate capacities.");
            Assert.AreEqual(
                8,
                enterBuff.RegionBindings[0].MaxMappedSubjects);
            Assert.AreEqual(8, buffRoute.MaxStateKeys);
            Assert.IsFalse(Validate(enterBuff, world).HasErrors);

            var presence =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            presence.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyRemoveBuffPresenceTemplateId,
                "region.start",
                payload0: 61,
                payload1: 7);
            GameplayTriggerRouteAuthoring[] presenceRoutes =
                presence.Authoring.Definitions[0].Routes;
            Assert.AreEqual(2, presenceRoutes.Length);
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Entered
                | GameplayTriggerOccurrenceKind.Stayed,
                presenceRoutes[0].OccurrenceMask);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceRoutes[0].PresenceLimit);
            Assert.AreEqual(1L, presenceRoutes[0].StayIntervalFrames);
            Assert.AreEqual(0, presenceRoutes[0].MaxActivations);
            Assert.AreEqual(
                GameplayTriggerGameplayBuffActionKinds
                    .ApplyComponentBuffValue,
                presenceRoutes[0].Actions[0].ActionKindId);
            Assert.AreEqual(0, presenceRoutes[0].ExecutionOrder);
            Assert.AreEqual(
                GameplayTriggerOccurrenceKind.Exited,
                presenceRoutes[1].OccurrenceMask);
            Assert.AreEqual(
                GameplayTriggerGameplayBuffActionKinds
                    .RemoveComponentBuffValue,
                presenceRoutes[1].Actions[0].ActionKindId);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceRoutes[1].PresenceLimit);
            Assert.AreEqual(1, presenceRoutes[1].ExecutionOrder);
            Assert.AreEqual(61, presenceRoutes[1].Actions[0].Payload0);
            Assert.AreEqual(7, presenceRoutes[1].Actions[0].Payload1);
            Assert.AreEqual(8, presenceRoutes[0].MaxStateKeys);
            Assert.AreEqual(8, presenceRoutes[1].MaxStateKeys);
            Assert.IsFalse(Validate(presence, world).HasErrors);
        }

        [Test]
        public void PlayerHpThresholdTemplate_UsesFormalNonRegionSourceContract()
        {
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry entry =
                trigger.AddPlayerHpLowStoryTemplate(
                    CharacterTestStoryIds.Triggers.LevelStartRegion);

            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .PlayerHpLowStoryTemplateId,
                entry.TemplateId);
            Assert.That(trigger.RegionBindings, Is.Empty);
            Assert.IsTrue(
                trigger.TryGetPlayerHpThresholds(
                    entry,
                    out int enterThreshold,
                    out int exitThreshold));
            Assert.AreEqual(30, enterThreshold);
            Assert.AreEqual(35, exitThreshold);

            GameplayTriggerSourceAuthoring source =
                trigger.Authoring.Sources[0];
            Assert.AreEqual(
                GameplayTriggerSourceKind.Threshold,
                source.SourceKind);
            Assert.AreEqual(
                GameplayTriggerThresholdDirection.Low,
                source.ThresholdDirection);
            Assert.AreEqual(
                GameplayTriggerInitialSamplePolicy.PrimeSilently,
                source.InitialSamplePolicy);
            Assert.AreEqual(
                GameplayTriggerSubjectDeparturePolicy.EmitExited,
                source.DeparturePolicy);

            var world = new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot"
            };
            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(
                        trigger,
                        world,
                        out GameplayTriggerCompileResult compile);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.IsTrue(compile.Succeeded);
            Assert.AreEqual(
                1,
                compile.CompiledCatalog.SourceRequirementCount);
            GameplayTriggerCompiledSourceRequirement requirement =
                compile.CompiledCatalog.GetSourceRequirement(0);
            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .PlayerAttributeThresholdProviderId,
                requirement.ProviderId.Value);
            Assert.AreEqual(
                GameplayTriggerSourceKind.Threshold,
                requirement.SourceKind);
            Assert.IsTrue(requirement.CompleteBatchPerEvaluation);
            Assert.AreEqual(1, requirement.MaxInputItemsPerEvaluation);
            Assert.AreEqual(1, requirement.MaxActiveRelations);

            Assert.IsTrue(
                trigger.TryUpdatePlayerHpThresholds(
                    entry.StableId,
                    enterThreshold: 20,
                    exitThreshold: 28));
            Assert.IsTrue(
                trigger.TryGetPlayerHpThresholds(
                    entry,
                    out enterThreshold,
                    out exitThreshold));
            Assert.AreEqual(20, enterThreshold);
            Assert.AreEqual(28, exitThreshold);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => trigger.TryUpdatePlayerHpThresholds(
                    entry.StableId,
                    enterThreshold: 40,
                    exitThreshold: 35));
        }

        [Test]
        public void TemplateEditAndCopy_PreserveOrAllocateTypedIdentities()
        {
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry source =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    "region.start",
                    payload0: 70,
                    payload1: 8);
            int sourceDefinitionId = source.SourceDefinitionId;
            int conditionId = source.ConditionId;
            int definitionId = source.DefinitionId;
            int bindingId = source.BindingId;
            int sourceBindingId = source.SourceBindingId;
            int[] routeIds = source.RouteIds.ToArray();

            Assert.IsTrue(
                trigger.TryUpdateTemplatePayloads(
                    source.StableId,
                    71,
                    9));
            Assert.AreEqual(sourceDefinitionId, source.SourceDefinitionId);
            Assert.AreEqual(conditionId, source.ConditionId);
            Assert.AreEqual(definitionId, source.DefinitionId);
            Assert.AreEqual(bindingId, source.BindingId);
            Assert.AreEqual(sourceBindingId, source.SourceBindingId);
            CollectionAssert.AreEqual(routeIds, source.RouteIds);
            Assert.AreEqual(
                71,
                trigger.Authoring.Definitions[0]
                    .Routes[0].Actions[0].Payload0);

            Assert.IsTrue(
                trigger.TryDuplicateEntry(
                    source.StableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate));
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
            CollectionAssert.AreNotEquivalent(
                source.RouteIds,
                duplicate.RouteIds);
            Assert.IsTrue(
                trigger.TryGetTemplatePayloads(
                    duplicate,
                    out long payload0,
                    out long payload1,
                    out _));
            Assert.AreEqual(71, payload0);
            Assert.AreEqual(9, payload1);
            Assert.IsFalse(
                Validate(
                    trigger,
                    CreateWorldDocument("region.start")).HasErrors);
        }

        [Test]
        public void TemplateSettings_EditPayloadAndCopyPreserveRealPolicy()
        {
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            CharacterTestLevelGameplayTriggerEntry source =
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    "region.start",
                    payload0: 601012,
                    payload1: 8);

            Assert.IsTrue(
                trigger.TrySetTemplateEnabled(
                    source.StableId,
                    initiallyEnabled: false));
            Assert.IsTrue(
                trigger.TryUpdateTemplatePolicy(
                    source.StableId,
                    GameplayTriggerStateScope.PerSubject,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames: 30,
                    maxActivations: 4));
            Assert.IsTrue(
                trigger.TryUpdateTemplatePayloads(
                    source.StableId,
                    payload0: 601009,
                    payload1: 9));

            Assert.IsTrue(
                trigger.TryGetTemplateSettings(
                    source,
                    out bool enabled,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out long cooldownFrames,
                    out int maxActivations));
            Assert.IsFalse(enabled);
            Assert.AreEqual(
                GameplayTriggerStateScope.PerSubject,
                stateScope);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceLimit);
            Assert.AreEqual(30L, cooldownFrames);
            Assert.AreEqual(4, maxActivations);

            Assert.IsTrue(
                trigger.TryDuplicateEntry(
                    source.StableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate));
            Assert.IsTrue(
                trigger.TryGetTemplateSettings(
                    duplicate,
                    out enabled,
                    out stateScope,
                    out presenceLimit,
                    out cooldownFrames,
                    out maxActivations));
            Assert.IsFalse(enabled);
            Assert.AreEqual(
                GameplayTriggerStateScope.PerSubject,
                stateScope);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceLimit);
            Assert.AreEqual(30L, cooldownFrames);
            Assert.AreEqual(4, maxActivations);
        }

        [UnityTest]
        public IEnumerator WorldObjectModule_TriggerCardAndSelectedAabbAreScopedToTriggerRegion()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            string fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsNotEmpty(world.Regions);
            world.Regions[0].Enabled = true;
            world.Regions[0].RegionKind =
                LevelWorldObjectRegionKind.TriggerRegion;
            string regionStableId = world.Regions[0].StableId;

            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(world);
            module.SetTriggerDocumentForTests(
                new CharacterTestLevelGameplayTriggerAuthoringDocument(),
                markDirty: false);
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "regions");
            Assert.IsNotEmpty(rows);

            var regionInspector =
                new ScrollView(ScrollViewMode.Vertical);
            regionInspector.style.width = 360f;
            regionInspector.style.height = 220f;
            module.BuildInspector(
                context,
                rows[0],
                regionInspector);
            Assert.IsNotNull(
                regionInspector.Q<VisualElement>(
                    "character-test-trigger-card"));
            Assert.AreEqual(0, module.TriggerEntryCountForTests);
            Assert.AreEqual(
                "0 个",
                regionInspector.Q<Label>(
                    "character-test-trigger-count").text);

            CharacterTestLevelGameplayTriggerEntry entry =
                module.AddTriggerTemplateForTests(
                    regionStableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId);
            Assert.IsNotNull(entry);
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    entry.StableId,
                    out long initialBuffId,
                    out _,
                    out _));
            Assert.AreEqual(
                0L,
                initialBuffId,
                "New Buff triggers must require an explicit catalog selection.");
            Assert.IsTrue(
                module.HasTriggerValidationIssueForTests(
                    "CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID"),
                "An unselected Buff must keep the draft invalid until the user chooses one.");
            VisualElement inspector =
                module.CreateGameplayTriggerWorkspace(null);
            IntegerField buffIdField =
                inspector.Q<IntegerField>(
                    "character-test-trigger-buff-id");
            Assert.IsNotNull(buffIdField);
            Assert.IsTrue(buffIdField.isDelayed);
            IntegerField sourceIdField =
                inspector.Q<IntegerField>(
                    "character-test-trigger-buff-source-id");
            Assert.IsNotNull(sourceIdField);
            Assert.IsTrue(sourceIdField.isDelayed);
            VisualElement buffPickerRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-buff-picker");
            Assert.IsNotNull(buffPickerRoot);
            DropdownField buffPicker =
                buffPickerRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.IsNotNull(buffPicker);
            Assert.AreEqual("请选择 Buff", buffPicker.value);
            Assert.IsNotNull(
                inspector.Q<Label>(
                    "character-test-trigger-summary"));
            Assert.IsNotNull(
                inspector.Q<Toggle>(
                    "character-test-trigger-enabled"));
            PopupField<string> triggerScope =
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-strategy");
            Assert.IsNotNull(triggerScope);
            CollectionAssert.AreEqual(
                new[]
                {
                    "全区域共用",
                    "每个对象独立",
                    "每次进入一次"
                },
                triggerScope.choices);
            Assert.IsNotNull(
                inspector
                    .Query<Label>(
                        className:
                            "mx-level-editor__trigger-field-label")
                    .ToList()
                    .Find(label =>
                        label.text == "计数与冷却范围"));
            IntegerField cooldownField =
                inspector.Q<IntegerField>(
                    "character-test-trigger-cooldown-frames");
            Assert.IsNotNull(cooldownField);
            Assert.IsTrue(cooldownField.isDelayed);
            VisualElement cooldownCustomRow =
                inspector.Q<VisualElement>(
                    "character-test-trigger-cooldown-custom-row");
            Assert.IsNotNull(cooldownCustomRow);
            Assert.AreEqual(
                DisplayStyle.None,
                cooldownCustomRow.style.display.value);
            IntegerField maxActivationsField =
                inspector.Q<IntegerField>(
                    "character-test-trigger-max-activations");
            Assert.IsNotNull(maxActivationsField);
            Assert.IsTrue(maxActivationsField.isDelayed);
            VisualElement activationCustomRow =
                inspector.Q<VisualElement>(
                    "character-test-trigger-activation-custom-row");
            Assert.IsNotNull(activationCustomRow);
            Assert.AreEqual(
                DisplayStyle.None,
                activationCustomRow.style.display.value);
            PopupField<string> cooldownMode =
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-cooldown-mode");
            Assert.IsNotNull(cooldownMode);
            Assert.AreEqual("无冷却", cooldownMode.value);
            PopupField<string> activationMode =
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-activation-limit-mode");
            Assert.IsNotNull(activationMode);
            Assert.AreEqual("1 次", activationMode.value);
            VisualElement attributionRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-buff-attribution");
            Assert.IsNotNull(attributionRoot);
            DropdownField attributionPicker =
                attributionRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.IsNotNull(attributionPicker);
            CollectionAssert.AreEqual(
                new[]
                {
                    "未指定",
                    "501008 · 守护光环",
                    "501010 · 区域禁锢",
                    "501012 · 毒瓶池"
                },
                attributionPicker.choices);
            Assert.IsNull(
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-source-mode"));
            VisualElement targetRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-target-reference");
            Assert.IsNull(
                targetRoot,
                "The compact workspace must not rebuild a separate target card.");
            Assert.IsNull(
                inspector
                    .Query<Label>(
                        className:
                            "mx-level-editor__trigger-group-title")
                    .ToList()
                    .Find(label =>
                        label.text == "作用对象"));
            Assert.IsNotNull(
                inspector
                    .Query<Label>(
                        className:
                            "mx-level-editor__trigger-clause-value")
                    .ToList()
                    .Find(label =>
                        label.text == "触发角色"),
                "The action sentence must still expose its target.");
            VisualElement relationRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-subject-relation");
            Assert.IsNotNull(relationRoot);
            DropdownField relationField =
                relationRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.IsNotNull(relationField);
            Assert.AreEqual("阵营不限", relationField.value);
            CollectionAssert.AreEqual(
                new[]
                {
                    "阵营不限",
                    "仅同队",
                    "仅敌对",
                    "仅中立"
                },
                relationField.choices);
            Assert.IsNull(
                inspector.Q<VisualElement>(
                    "character-test-trigger-subject-reference-team"));

            Assert.IsTrue(
                module.UpdateTriggerSubjectRelationForTests(
                    entry.StableId,
                    GameplayTargetRelationFilter.Enemy,
                    1));
            Assert.IsTrue(
                module.TryGetTriggerSubjectFilterForTests(
                    entry.StableId,
                    out CharacterTestLevelGameplayTriggerSubjectFilter
                        subjectFilter));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                subjectFilter.AcceptedIdentityTypes);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable,
                subjectFilter.RequiredCapabilities);
            Assert.IsTrue(subjectFilter.RequireAlive);
            Assert.AreEqual(
                GameplayTargetRelationFilter.Enemy,
                subjectFilter.RelationFilter);
            Assert.AreEqual(1, subjectFilter.ReferenceTeamId);

            inspector =
                module.CreateGameplayTriggerWorkspace(null);
            relationRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-subject-relation");
            relationField =
                relationRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.AreEqual("仅敌对", relationField.value);
            VisualElement referenceTeamRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-subject-reference-team");
            Assert.IsNotNull(referenceTeamRoot);

            Assert.IsTrue(
                module.UpdateTriggerPayloadsForTests(
                    entry.StableId,
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                    0L));
            inspector =
                module.CreateGameplayTriggerWorkspace(null);
            buffPickerRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-buff-picker");
            Assert.AreEqual(
                "Buff",
                buffPickerRoot.Q<Label>(
                    MxAuthoringReferenceField.LabelElementName).text);
            buffPicker =
                buffPickerRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.AreEqual("601012 · 中毒", buffPicker.value);
            Label buffSummary =
                buffPickerRoot.Q<Label>(
                    MxAuthoringReferenceField.SummaryElementName);
            StringAssert.Contains("持续 6 秒", buffSummary.text);
            StringAssert.Contains(
                "重复施加刷新 · 不可叠加",
                buffSummary.text);
            StringAssert.Contains(
                "每 1 秒造成 2 点毒素伤害",
                buffSummary.text);
            StringAssert.Contains("“中毒”状态", buffSummary.text);
            StringAssert.Contains(
                "持续时间：360 逻辑帧",
                buffPicker.tooltip);
            StringAssert.Contains(
                "叠层策略：RefreshDuration",
                buffPicker.tooltip);
            StringAssert.Contains(
                "每次施加层数：1",
                buffPicker.tooltip);
            StringAssert.Contains(
                "最大层数：1",
                buffPicker.tooltip);

            attributionRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-buff-attribution");
            Assert.AreEqual(
                "来源机制",
                attributionRoot.Q<Label>(
                    MxAuthoringReferenceField.LabelElementName).text);
            attributionPicker =
                attributionRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.AreEqual(
                "501012 · 毒瓶池",
                attributionPicker.value);
            Label attributionSummary =
                attributionRoot.Q<Label>(
                    MxAuthoringReferenceField.SummaryElementName);
            Assert.AreEqual(
                "仅记录施加来源，不改变 Buff 的伤害、持续时间或叠层。",
                attributionSummary.text);
            StringAssert.DoesNotContain(
                "2 个效果",
                attributionSummary.text);
            StringAssert.Contains(
                "不会执行该来源定义中的其他效果",
                attributionPicker.tooltip);

            const long UnknownAttributionId = 777777L;
            Assert.IsTrue(
                module.UpdateTriggerPayloadsForTests(
                    entry.StableId,
                    CharacterTestGuardAuraIds.BuffId,
                    UnknownAttributionId,
                    0L));
            inspector =
                module.CreateGameplayTriggerWorkspace(null);
            attributionRoot =
                inspector.Q<VisualElement>(
                    "character-test-trigger-buff-attribution");
            attributionPicker =
                attributionRoot.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName);
            Assert.AreEqual(
                "未登记来源机制 777777",
                attributionPicker.value);
            Assert.IsNotNull(
                attributionRoot.Q<HelpBox>(
                    MxAuthoringReferenceField.DiagnosticElementName));
            inspector =
                module.CreateGameplayTriggerWorkspace(null);
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    entry.StableId,
                    out _,
                    out long preservedAttributionId,
                    out _));
            Assert.AreEqual(
                UnknownAttributionId,
                preservedAttributionId,
                "Rebuilding the inspector must not replace an unknown value.");
            Assert.AreEqual(
                "未登记来源机制 777777",
                inspector.Q<VisualElement>(
                        "character-test-trigger-buff-attribution")
                    .Q<DropdownField>(
                        MxAuthoringReferenceField.ValueElementName)
                    .value);
            cooldownMode =
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-cooldown-mode");
            activationMode =
                inspector.Q<PopupField<string>>(
                    "character-test-trigger-activation-limit-mode");

            Foldout advancedRules =
                inspector.Q<Foldout>(
                    "character-test-trigger-options");
            Assert.IsNotNull(advancedRules);
            Assert.AreEqual("高级规则", advancedRules.text);
            Assert.IsFalse(advancedRules.value);
            Assert.IsNotNull(
                inspector.Q<Foldout>(
                    "character-test-trigger-advanced"));
            Toggle requireMovable =
                inspector.Q<Toggle>(
                    "character-test-trigger-subject-require-movable");
            Assert.IsNotNull(requireMovable);
            Assert.IsFalse(requireMovable.value);
            string defenseBreakChoice = buffPicker.choices.Find(
                value => value.StartsWith(
                    "601009 ",
                    StringComparison.Ordinal));
            Assert.IsFalse(string.IsNullOrWhiteSpace(defenseBreakChoice));
            Label identityRules =
                inspector
                    .Query<Label>(
                        className:
                            "mx-level-editor__trigger-readonly")
                    .ToList()
                    .Find(label =>
                        label.text == "角色 · 可伤害 · 存活");
            Assert.IsNotNull(identityRules);
            Assert.AreEqual(
                "角色 · 可伤害 · 存活",
                identityRules.text);
            TriggerInspectorTestWindow inspectorWindow =
                ScriptableObject.CreateInstance<
                    TriggerInspectorTestWindow>();
            try
            {
                inspectorWindow.rootVisualElement.Add(inspector);
                inspectorWindow.position =
                    new Rect(100f, 100f, 420f, 720f);
                inspectorWindow.Show();
                yield return null;
                Assert.Greater(
                    identityRules.resolvedStyle.height,
                    0f);
                Assert.LessOrEqual(
                    identityRules.resolvedStyle.height,
                    24f,
                    "Subject identity rules should remain a compact summary.");
                advancedRules.value = true;
                yield return null;
                ScrollView inspectorScroll =
                    inspector.Q<ScrollView>(
                        "character-test-trigger-workspace-editor");
                Assert.IsNotNull(inspectorScroll);
                float requestedScrollY = Mathf.Min(
                    120f,
                    inspectorScroll.verticalScroller.highValue);
                Assert.Greater(
                    requestedScrollY,
                    0f,
                    "The focused inspector fixture must be scrollable.");
                inspectorScroll.scrollOffset =
                    new Vector2(0f, requestedScrollY);
                yield return null;
                float preservedScrollY =
                    inspectorScroll.scrollOffset.y;
                Assert.Greater(preservedScrollY, 0f);
                cooldownMode.Focus();
                yield return null;
                Assert.IsTrue(
                    IsFocusWithin(inspector, cooldownMode),
                    "The cooldown selector must accept focus before refresh.");
                cooldownMode.value = "自定义";
                yield return null;
                yield return null;
                inspectorScroll =
                    inspector.Q<ScrollView>(
                        "character-test-trigger-workspace-editor");
                Assert.AreEqual(
                    preservedScrollY,
                    inspectorScroll.scrollOffset.y,
                    0.5f,
                    "Changing a Trigger option must not jump the inspector to the top.");
                cooldownMode =
                    inspector.Q<PopupField<string>>(
                        "character-test-trigger-cooldown-mode");
                Assert.IsTrue(
                    IsFocusWithin(inspector, cooldownMode),
                    "Refreshing the Trigger inspector must restore the cooldown selector focus.");
                activationMode =
                    inspector.Q<PopupField<string>>(
                        "character-test-trigger-activation-limit-mode");
                Assert.IsNotNull(activationMode);
                activationMode.Focus();
                yield return null;
                Assert.IsTrue(
                    IsFocusWithin(inspector, activationMode),
                    "The activation selector must accept focus before refresh.");
                activationMode.value = "自定义";
                yield return null;
                yield return null;
                inspectorScroll =
                    inspector.Q<ScrollView>(
                        "character-test-trigger-workspace-editor");
                Assert.AreEqual(
                    preservedScrollY,
                    inspectorScroll.scrollOffset.y,
                    0.5f,
                    "Consecutive Trigger option changes must preserve the inspector scroll.");
                activationMode =
                    inspector.Q<PopupField<string>>(
                        "character-test-trigger-activation-limit-mode");
                Assert.IsTrue(
                    IsFocusWithin(inspector, activationMode),
                    "Refreshing the Trigger inspector must restore the activation selector focus.");
                Assert.IsTrue(
                    module.TryGetTriggerSettingsForTests(
                        entry.StableId,
                        out _,
                        out _,
                        out _,
                        out long selectedCooldownFrames,
                        out int selectedMaxActivations));
                Assert.AreEqual(
                    CharacterTestDeterministicUnits.DefaultTickRate,
                    selectedCooldownFrames);
                Assert.AreEqual(2, selectedMaxActivations);

                Assert.IsTrue(
                    inspector.Q<Foldout>(
                        "character-test-trigger-options").value,
                    "Advanced rules should stay open after a policy refresh.");
                buffPickerRoot =
                    inspector.Q<VisualElement>(
                        "character-test-trigger-buff-picker");
                buffPicker =
                    buffPickerRoot.Q<DropdownField>(
                        MxAuthoringReferenceField.ValueElementName);
                buffPicker.value = defenseBreakChoice;
                yield return null;
                attributionRoot =
                    inspector.Q<VisualElement>(
                        "character-test-trigger-buff-attribution");
                attributionPicker =
                    attributionRoot.Q<DropdownField>(
                        MxAuthoringReferenceField.ValueElementName);
                attributionPicker.value = "501012 · 毒瓶池";
                yield return null;
            }
            finally
            {
                inspector.RemoveFromHierarchy();
                inspectorWindow.Close();
            }
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    entry.StableId,
                    out long selectedBuffId,
                    out long selectedAttributionId,
                    out _));
            Assert.AreEqual(601009L, selectedBuffId);
            Assert.AreEqual(
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                selectedAttributionId);
            Assert.IsFalse(
                module.HasTriggerValidationIssueForTests(
                    "CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID"));
            Assert.IsFalse(
                module.HasTriggerValidationIssueForTests(
                    "CT_TRIGGER_COMPILE_UnknownActionContract"));

            var sceneContext = new MxLevelEditorSceneViewContext(
                context,
                "regions",
                rows[0],
                rows);
            Assert.IsTrue(
                module.TryBuildSelectedTriggerAabbForTests(
                    sceneContext,
                    out CharacterTestWorldObjectLevelEditorModule
                        .CompiledTriggerAabbPreview preview));
            Assert.AreEqual(rows[0].Key, preview.RowKey);
            IReadOnlyList<
                CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem>
                overlayItems =
                    module.BuildSceneOverlayItemsForTests(sceneContext);
            CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem
                regionOverlay = null;
            for (int i = 0; i < overlayItems.Count; i++)
            {
                if (string.Equals(
                        overlayItems[i].RowKey,
                        rows[0].Key,
                        StringComparison.Ordinal))
                {
                    regionOverlay = overlayItems[i];
                    break;
                }
            }
            Assert.IsNotNull(regionOverlay);
            Assert.AreEqual(regionOverlay.Center, preview.Center);
            Assert.AreEqual(regionOverlay.HalfExtents, preview.HalfExtents);

            var unselectedContext = new MxLevelEditorSceneViewContext(
                context,
                "regions",
                null,
                rows);
            Assert.IsFalse(
                module.TryBuildSelectedTriggerAabbForTests(
                    unselectedContext,
                    out _));

            Assert.IsTrue(
                module.TryResolveRowObject(
                    context,
                    rows[0],
                    out Object regionObject));
            Transform regionTransform =
                regionObject is GameObject regionGameObject
                    ? regionGameObject.transform
                    : regionObject as Transform;
            Assert.IsNotNull(regionTransform);
            Quaternion originalRotation = regionTransform.rotation;
            try
            {
                regionTransform.rotation = Quaternion.Euler(0f, 15f, 0f);
                Assert.IsTrue(
                    module.HasTriggerValidationIssueForTests(
                        "CT_TRIGGER_REGION_ROTATION_UNSUPPORTED"));
                inspector =
                    module.CreateGameplayTriggerWorkspace(null);
                Assert.IsNotNull(
                    inspector.Q<HelpBox>(
                        "character-test-trigger-diagnostic"));
                Assert.IsFalse(
                    module.TryBuildSelectedTriggerAabbForTests(
                        sceneContext,
                        out _));
                Assert.IsFalse(
                    ((IMxLevelEditorSaveParticipant)module)
                    .TryPrepareSave(out string failureMessage));
                StringAssert.Contains("保存", failureMessage);
            }
            finally
            {
                regionTransform.rotation = originalRotation;
                module.HasTriggerValidationIssueForTests(
                    "CT_TRIGGER_REGION_ROTATION_UNSUPPORTED");
            }

            world.Regions[0].RegionKind =
                LevelWorldObjectRegionKind.TestRegion;
            module.SetDocumentForTests(world);
            rows = module.BuildRows(context, "regions");
            module.BuildInspector(context, rows[0], inspector);
            Assert.IsNull(
                inspector.Q<VisualElement>("character-test-trigger-card"));
            yield return null;
        }

        private sealed class TriggerInspectorTestWindow : EditorWindow
        {
        }

        [UnityTest]
        public IEnumerator WorldObjectModule_SelectedAabbRegionCanResizeInSceneWithUndoRedo()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            string fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsNotEmpty(world.Regions);
            world.Regions[0].Enabled = true;
            world.Regions[0].ScaleSource =
                LevelWorldObjectScaleSource.Definition;

            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(world);
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "regions");
            Assert.IsNotEmpty(rows);
            var sceneContext = new MxLevelEditorSceneViewContext(
                context,
                "regions",
                rows[0],
                rows);
            CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem before =
                FindOverlay(module, sceneContext, rows[0].Key);
            Assert.IsNotNull(before);
            Assert.IsTrue(before.IsRegion);
            Assert.IsTrue(
                module.TryResolveRowObject(
                    context,
                    rows[0],
                    out Object regionObject));
            Transform transform =
                regionObject is GameObject gameObject
                    ? gameObject.transform
                    : regionObject as Transform;
            Assert.IsNotNull(transform);

            Vector3 originalPosition = transform.position;
            Vector3 originalLocalScale = transform.localScale;
            Vector3 desiredCenter =
                before.Center + new Vector3(0.75f, 0.25f, -0.5f);
            Vector3 desiredSize =
                before.HalfExtents * 2f + new Vector3(1.5f, 0.5f, 2f);
            try
            {
                Assert.IsTrue(
                    module.TryApplySelectedRegionBoundsForTests(
                        sceneContext,
                        desiredCenter,
                        desiredSize,
                        out string failureMessage),
                    failureMessage);
                yield return null;

                rows = module.BuildRows(context, "regions");
                sceneContext = new MxLevelEditorSceneViewContext(
                    context,
                    "regions",
                    rows[0],
                    rows);
                CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem applied =
                    FindOverlay(module, sceneContext, rows[0].Key);
                AssertVector3(desiredCenter, applied.Center);
                AssertVector3(desiredSize * 0.5f, applied.HalfExtents);
                Assert.IsTrue(module.IsDirty);

                Undo.PerformUndo();
                yield return null;
                rows = module.BuildRows(context, "regions");
                sceneContext = new MxLevelEditorSceneViewContext(
                    context,
                    "regions",
                    rows[0],
                    rows);
                CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem undone =
                    FindOverlay(module, sceneContext, rows[0].Key);
                AssertVector3(before.Center, undone.Center);
                AssertVector3(before.HalfExtents, undone.HalfExtents);
                AssertVector3(originalPosition, transform.position);
                AssertVector3(originalLocalScale, transform.localScale);

                Undo.PerformRedo();
                yield return null;
                rows = module.BuildRows(context, "regions");
                sceneContext = new MxLevelEditorSceneViewContext(
                    context,
                    "regions",
                    rows[0],
                    rows);
                CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem redone =
                    FindOverlay(module, sceneContext, rows[0].Key);
                AssertVector3(desiredCenter, redone.Center);
                AssertVector3(desiredSize * 0.5f, redone.HalfExtents);

                Undo.PerformUndo();
                yield return null;
            }
            finally
            {
                transform.position = originalPosition;
                transform.localScale = originalLocalScale;
                EditorSceneManager.OpenScene(
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    OpenSceneMode.Single);
            }
        }

        [UnityTest]
        public IEnumerator WorldObjectModule_TriggerAddEditAndCopyEachUseOneUndoStep()
        {
            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument("region.undo");
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(world);
            module.SetTriggerDocumentForTests(
                new CharacterTestLevelGameplayTriggerAuthoringDocument(),
                markDirty: false);

            CharacterTestLevelGameplayTriggerEntry entry =
                module.AddTriggerTemplateForTests(
                    "region.undo",
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId);
            Assert.IsNotNull(entry);
            string stableId = entry.StableId;
            Assert.AreEqual(1, module.TriggerEntryCountForTests);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(0, module.TriggerEntryCountForTests);
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(1, module.TriggerEntryCountForTests);

            Assert.IsTrue(
                module.UpdateTriggerPayloadsForTests(
                    stableId,
                    CharacterTestGuardAuraIds.BuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId));
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    stableId,
                    out long payload0,
                    out long payload1,
                    out _));
            Assert.AreEqual(CharacterTestGuardAuraIds.BuffId, payload0);
            Assert.AreEqual(
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                payload1);

            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    stableId,
                    out payload0,
                    out payload1,
                    out _));
            Assert.AreEqual(0, payload0);
            Assert.AreEqual(0, payload1);
            Undo.PerformRedo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerPayloadsForTests(
                    stableId,
                    out payload0,
                    out payload1,
                    out _));
            Assert.AreEqual(CharacterTestGuardAuraIds.BuffId, payload0);
            Assert.AreEqual(
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                payload1);

            Assert.IsTrue(
                module.SetTriggerEnabledForTests(
                    stableId,
                    enabled: false));
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out bool enabled,
                    out _,
                    out _,
                    out _,
                    out _));
            Assert.IsFalse(enabled);
            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out enabled,
                    out _,
                    out _,
                    out _,
                    out _));
            Assert.IsTrue(enabled);
            Undo.PerformRedo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out enabled,
                    out _,
                    out _,
                    out _,
                    out _));
            Assert.IsFalse(enabled);

            Assert.IsTrue(
                module.UpdateTriggerPolicyForTests(
                    stableId,
                    GameplayTriggerStateScope.PerSubject,
                    GameplayTriggerPresenceLimit.Ignore,
                    cooldownFrames: 45,
                    maxActivations: 3));
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out _,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out long cooldownFrames,
                    out int maxActivations));
            Assert.AreEqual(
                GameplayTriggerStateScope.PerSubject,
                stateScope);
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceLimit);
            Assert.AreEqual(45L, cooldownFrames);
            Assert.AreEqual(3, maxActivations);
            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out _,
                    out stateScope,
                    out presenceLimit,
                    out cooldownFrames,
                    out maxActivations));
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.OncePerPresence,
                presenceLimit);
            Assert.AreEqual(0L, cooldownFrames);
            Assert.AreEqual(1, maxActivations);
            Undo.PerformRedo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSettingsForTests(
                    stableId,
                    out _,
                    out stateScope,
                    out presenceLimit,
                    out cooldownFrames,
                    out maxActivations));
            Assert.AreEqual(
                GameplayTriggerPresenceLimit.Ignore,
                presenceLimit);
            Assert.AreEqual(45L, cooldownFrames);
            Assert.AreEqual(3, maxActivations);

            Assert.IsTrue(
                module.DuplicateTriggerEntryForTests(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate));
            Assert.AreNotEqual(stableId, duplicate.StableId);
            Assert.AreEqual(2, module.TriggerEntryCountForTests);
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(2, module.TriggerEntryCountForTests);

            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
            Undo.PerformUndo();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldObjectModule_TriggerSubjectRelationUsesOneUndoStep()
        {
            const string RegionStableId = "region.subject-filter-undo";
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument(RegionStableId));
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                CreateTriggerDocument(
                    RegionStableId,
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue);
            module.SetTriggerDocumentForTests(trigger, markDirty: false);
            string stableId = trigger.Entries[0].StableId;

            Assert.IsTrue(
                module.UpdateTriggerSubjectRelationForTests(
                    stableId,
                    GameplayTargetRelationFilter.Enemy,
                    referenceTeamId: 2));
            Assert.IsTrue(
                module.TryGetTriggerSubjectFilterForTests(
                    stableId,
                    out CharacterTestLevelGameplayTriggerSubjectFilter
                        changed));
            Assert.AreEqual(
                GameplayTargetRelationFilter.Enemy,
                changed.RelationFilter);
            Assert.AreEqual(2, changed.ReferenceTeamId);
            Assert.IsTrue(module.IsDirty);

            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSubjectFilterForTests(
                    stableId,
                    out CharacterTestLevelGameplayTriggerSubjectFilter
                        undone));
            Assert.AreEqual(
                GameplayTargetRelationFilter.Any,
                undone.RelationFilter);
            Assert.AreEqual(0, undone.ReferenceTeamId);

            Undo.PerformRedo();
            yield return null;
            Assert.IsTrue(
                module.TryGetTriggerSubjectFilterForTests(
                    stableId,
                    out CharacterTestLevelGameplayTriggerSubjectFilter
                        redone));
            Assert.AreEqual(
                GameplayTargetRelationFilter.Enemy,
                redone.RelationFilter);
            Assert.AreEqual(2, redone.ReferenceTeamId);

            Undo.PerformUndo();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldObjectModule_TriggerDeleteUndoAndRegionProtectionShareOneSnapshot()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            string fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport
                            worldReport),
                JoinIssues(worldReport));
            Assert.IsNotEmpty(world.Regions);
            world.Regions[0].Enabled = true;
            world.Regions[0].RegionKind =
                LevelWorldObjectRegionKind.TriggerRegion;
            string regionStableId = world.Regions[0].StableId;

            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(world);
            module.SetTriggerDocumentForTests(
                CreateTriggerDocument(
                    regionStableId,
                    GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue),
                markDirty: false);
            var context =
                new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "regions");
            Assert.IsNotEmpty(rows);
            Assert.IsTrue(
                module.TryResolveRowObject(
                    context,
                    rows[0],
                    out Object regionObject));
            Assert.IsNotNull(regionObject);

            int removed =
                module.DeleteRowForTests(rows[0], out GameObject deleted);
            Assert.AreEqual(0, removed);
            Assert.IsNull(deleted);
            Assert.IsTrue(
                module.HasTriggerReferenceForTests(regionStableId));

            Assert.IsTrue(
                module.RemoveTriggerEntryForTests(
                    "trigger." + regionStableId));
            Assert.AreEqual(0, module.TriggerEntryCountForTests);
            Assert.AreEqual(6, module.TriggerRetiredIdCountForTests);
            yield return null;

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(1, module.TriggerEntryCountForTests);
            Assert.AreEqual(0, module.TriggerRetiredIdCountForTests);
            Assert.IsTrue(
                module.HasTriggerReferenceForTests(regionStableId));

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(0, module.TriggerEntryCountForTests);
            Assert.AreEqual(6, module.TriggerRetiredIdCountForTests);

            Undo.PerformUndo();
            yield return null;
        }

        private static LevelWorldObjectAuthoringValidationReport Validate(
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger,
            LevelWorldObjectAuthoringDocument world)
        {
            return CharacterTestLevelGameplayTriggerAuthoringAdapter
                .ValidateAndCompile(trigger, world, out _);
        }

        private static CharacterTestLevelGameplayTriggerAuthoringDocument
            CreateTriggerDocument(
                string regionStableId,
                int actionKindId,
                GameplayTriggerTargetSelector targetSelector =
                    GameplayTriggerTargetSelector.Subject)
        {
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.IdState.NextSourceDefinitionId = 2;
            trigger.IdState.NextConditionId = 2;
            trigger.IdState.NextDefinitionId = 2;
            trigger.IdState.NextRouteId = 2;
            trigger.IdState.NextBindingId = 2;
            trigger.IdState.NextSourceBindingId = 2;
            trigger.Authoring = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[]
                {
                    new GameplayTriggerSourceAuthoring(
                        1,
                        GameplayTriggerSourceKind.Occupancy,
                        departurePolicy:
                            GameplayTriggerSubjectDeparturePolicy.EmitExited)
                },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        1,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.PushTrue))
                },
                new[]
                {
                    new GameplayTriggerDefinitionAuthoring(
                        1,
                        1,
                        1,
                        new[]
                        {
                            new GameplayTriggerRouteAuthoring(
                                1,
                                GameplayTriggerOccurrenceKind.Entered,
                                1,
                                new[]
                                {
                                    new GameplayTriggerActionAuthoring(
                                        actionKindId,
                                        targetSelector,
                                        payload0: 1)
                                })
                        })
                },
                new[]
                {
                    new GameplayTriggerBindingAuthoring(
                        1,
                        1,
                        1)
                });
            trigger.Entries.Add(
                new CharacterTestLevelGameplayTriggerEntry
                {
                    StableId = "trigger." + regionStableId,
                    DisplayName = "进入区域添加 Buff",
                    TemplateId =
                        "region.gameplay.apply_buff.enter_once.v1",
                    Notes = string.Empty,
                    SourceDefinitionId = 1,
                    ConditionId = 1,
                    DefinitionId = 1,
                    RouteIds = new List<int> { 1 },
                    BindingId = 1,
                    SourceBindingId = 1
                });
            trigger.RegionBindings.Add(
                new CharacterTestLevelGameplayTriggerRegionBinding
                {
                    SourceBindingId = 1,
                    RegionStableId = regionStableId,
                    ProviderId =
                        CharacterTestLevelGameplayTriggerAuthoringAdapter
                            .CombatRegionProviderId,
                    LayerMask = 1,
                    MaxQueryHits = 8,
                    MaxMappedSubjects = 8
                });
            return trigger;
        }

        private static void AssertLegacyV1SubjectFilterMigration(
            string templateId,
            GameplayTargetCapabilities expectedCapabilities)
        {
            const string RegionStableId = "region.legacy-filter";
            var legacy =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            legacy.AddTemplate(templateId, RegionStableId, payload0: 1L);
            MxLevelAuthoringDocument envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    new MxLevelAuthoringDocument(),
                    legacy);
            Assert.IsTrue(
                envelope.TryGetModule(
                    CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId,
                    out MxLevelAuthoringModuleDocument module));
            module.SchemaVersion =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .LegacyModuleSchemaVersion;
            module.Payload["format"] =
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .LegacyV1Format;
            module.Payload["schemaVersion"] =
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .LegacyV1SchemaVersion;
            if (module.Payload["regionBindings"] is JArray regionBindings)
            {
                foreach (JObject binding in
                         regionBindings.Children<JObject>())
                {
                    binding.Remove("subjectFilter");
                }
            }

            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        migrated,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsTrue(present);
            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .CurrentFormat,
                migrated.Format);
            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .CurrentSchemaVersion,
                migrated.SchemaVersion);
            Assert.IsTrue(
                migrated.TryGetSubjectFilter(
                    migrated.Entries[0],
                    out CharacterTestLevelGameplayTriggerSubjectFilter filter));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                filter.AcceptedIdentityTypes);
            Assert.AreEqual(
                expectedCapabilities,
                filter.RequiredCapabilities);
            Assert.IsTrue(filter.RequireAlive);
            Assert.AreEqual(
                GameplayTargetRelationFilter.Any,
                filter.RelationFilter);
            Assert.AreEqual(0, filter.ReferenceTeamId);

            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    migrated);
            Assert.IsTrue(
                envelope.TryGetModule(
                    CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId,
                    out module));
            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ModuleSchemaVersion,
                module.SchemaVersion);
            Assert.AreEqual(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .CurrentFormat,
                (string)module.Payload["format"]);
            Assert.IsNotNull(
                module.Payload["regionBindings"]?[0]?["subjectFilter"]);
        }

        private static LevelWorldObjectAuthoringDocument CreateWorldDocument(
            string regionStableId)
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
                        StableId = regionStableId,
                        RegionKind =
                            LevelWorldObjectRegionKind.TriggerRegion,
                        TransformRef =
                            LevelTransformReference.FromPath(
                                "Regions/TriggerRegion")
                    }
                }
            };
        }

        private static CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem
            FindOverlay(
                CharacterTestWorldObjectLevelEditorModule module,
                MxLevelEditorSceneViewContext context,
                string rowKey)
        {
            IReadOnlyList<
                CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items =
                    module.BuildSceneOverlayItemsForTests(context);
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(
                        items[i].RowKey,
                        rowKey,
                        StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            return null;
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static bool IsFocusWithin(
            VisualElement inspector,
            VisualElement expectedRoot)
        {
            VisualElement focusedElement =
                inspector?.panel?.focusController?.focusedElement
                    as VisualElement;
            while (focusedElement != null)
            {
                if (ReferenceEquals(focusedElement, expectedRoot))
                    return true;
                focusedElement = focusedElement.parent;
            }

            return false;
        }

        private static void AssertCode(
            LevelWorldObjectAuthoringValidationReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (string.Equals(
                        report.Issues[i].Code,
                        code,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            Assert.Fail("Missing code " + code + ". " + JoinIssues(report));
        }

        private static string JoinIssues(
            LevelWorldObjectAuthoringValidationReport report)
        {
            if (report == null)
                return "No validation report.";
            var messages = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                messages[i] = report.Issues[i].Code
                    + " "
                    + report.Issues[i].Path
                    + ": "
                    + report.Issues[i].Message;
            }
            return string.Join(Environment.NewLine, messages);
        }
    }
}
