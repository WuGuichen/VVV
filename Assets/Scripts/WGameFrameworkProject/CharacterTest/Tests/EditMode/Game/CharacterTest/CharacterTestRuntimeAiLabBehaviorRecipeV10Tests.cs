using System.Collections.Generic;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestRuntimeAiLabBehaviorRecipeV10Tests
    {
        [Test]
        public void Migration_WrapsSubjectFactsAndLeavesTargetExistenceOutside()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 9);
            var all = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "root",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            all.Children.Add(Fact("exists", CharacterTestRuntimeAiLabCardConditionFact.TargetExists));
            all.Children.Add(Fact("distance", CharacterTestRuntimeAiLabCardConditionFact.TargetDistance));
            all.Children.Add(Fact("hazard", CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard));
            payload.ActivationConditionRoot = all;

            CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.SynchronizeForRead(payload);

            Assert.AreEqual(10, payload.BehaviorRecipeVersion);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                payload.ActivationConditionRoot.Children[0].Kind);
            AssertScope(payload.ActivationConditionRoot.Children[1],
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            AssertScope(payload.ActivationConditionRoot.Children[2],
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment,
                CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard);
            StringAssert.StartsWith("condition.scope.1.distance",
                payload.ActivationConditionRoot.Children[1].NodeId);
        }

        [Test]
        public void ConditionCatalog_CoversEveryCurrentFactExactlyOnceIncludingTargetDead()
        {
            var seen = new HashSet<CharacterTestRuntimeAiLabCardConditionFact>();
            Assert.AreEqual(6, CharacterTestRuntimeAiLabBehaviorConditionCatalog.All.Count);
            for (int i = 0; i < CharacterTestRuntimeAiLabBehaviorConditionCatalog.All.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor =
                    CharacterTestRuntimeAiLabBehaviorConditionCatalog.All[i];
                Assert.IsTrue(seen.Add(descriptor.Fact));
                if (descriptor.Subject
                    == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget)
                {
                    StringAssert.DoesNotContain("当前", descriptor.DisplayName,
                        "目标条件的作者文案必须统一使用简称“目标”。");
                }
            }
            Assert.AreEqual(6, seen.Count);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio, out var health));
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self, health.Subject);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorConditionValueKind.Number, health.ValueKind);
            Assert.IsFalse(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning));
            Assert.IsTrue(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible));
            Assert.IsFalse(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction));
            Assert.IsFalse(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable));
            Assert.IsFalse(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady));
            Assert.IsFalse(seen.Contains(
                CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard));
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor rootDistance));
            Assert.AreEqual("目标根节点平面距离", rootDistance.DisplayName);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor hurtboxDistance));
            Assert.AreEqual("目标 Hurtbox 表面距离", hurtboxDistance.DisplayName);
            Assert.AreEqual(20, CharacterTestRuntimeAiLabFactCatalog.Count);
            Assert.AreEqual(
                "目标",
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.SubjectText(
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget));
            Assert.AreEqual(
                "character-test.runtime-ai-planner.target.state-mask",
                CharacterTestRuntimeAiLabFactCatalog.StableId(
                    CharacterTestRuntimeAiLabCardConditionFact.TargetState));
            Assert.AreEqual(
                "character-test.runtime-ai-planner.self.actionInterruptible",
                CharacterTestRuntimeAiLabFactCatalog.StableId(
                    CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible));
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .HasAuthorableConditionForSubject(
                        CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget));
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .HasAuthorableConditionForSubject(
                        CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self));
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .HasAuthorableConditionForSubject(
                        CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment));
        }

        [Test]
        public void SubjectAvailability_CurrentTargetRequiresTargetButSelfAndEnvironmentRemainAvailable()
        {
            var runtime = new RuntimeStub { TargetExists = false };
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                runtime));
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                runtime));
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment,
                runtime));

            runtime.TargetExists = true;
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                runtime));
        }

        [Test]
        public void Evaluator_TargetScopeFailsClosedBeforeReadingDefaultTargetDistance()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.target",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Fact("distance", CharacterTestRuntimeAiLabCardConditionFact.TargetDistance));
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(payload, null);
            Assert.IsTrue(compile.Success, FirstDiagnostic(compile));
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            var runtime = new RuntimeStub { TargetExists = false, TargetDistanceCondition = true };
            var evaluator = new CharacterTestRuntimeAiLabBehaviorRecipeV6Evaluator(
                compile.Program.RecipeV6,
                runtime);

            Assert.IsFalse(evaluator.Start(0, events));
            Assert.AreEqual(0, runtime.TargetDistanceReads,
                "A missing target must stop the scope before the default distance value is read.");

            runtime.TargetExists = true;
            Assert.IsTrue(evaluator.Start(0, events));
            Assert.AreEqual(1, runtime.TargetDistanceReads);
        }

        [Test]
        public void Json_RoundTripPreservesSubjectScopeAndWritesV10()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.self",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                Fact("self.action", CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning));

            string json = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(json);

            StringAssert.Contains("\"behaviorRecipeVersion\": 11", json);
            StringAssert.Contains("\"subject\": 2", json);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                restored.ActivationConditionRoot.Kind);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                restored.ActivationConditionRoot.Subject);
        }

        [Test]
        public void Mutation_AddSubjectConditionCreatesOneConfiguredScopeTransaction()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            bool applied = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddSubjectCondition,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabCardConditionFact.TargetDistance),
                out bool changed,
                out string diagnostic);

            Assert.IsTrue(applied, diagnostic);
            Assert.IsTrue(changed);
            AssertScope(
                payload.ActivationConditionRoot,
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            Assert.AreEqual(1.5f, payload.ActivationConditionRoot.Children[0].Predicate.Value, 0.0001f);
        }

        [Test]
        public void Mutation_ReplaceSubjectConditionPreservesLeafIdentityAndTreePosition()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.target",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Fact("distance", CharacterTestRuntimeAiLabCardConditionFact.TargetDistance));
            string conditionId = payload.ActivationConditionRoot.Children[0].Predicate.ConditionId;

            bool applied = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ReplaceSubjectCondition,
                0,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible),
                out bool changed,
                out string diagnostic);

            Assert.IsTrue(applied, diagnostic);
            Assert.IsTrue(changed);
            AssertScope(
                payload.ActivationConditionRoot,
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible);
            Assert.AreEqual("distance", payload.ActivationConditionRoot.Children[0].NodeId);
            Assert.AreEqual(conditionId, payload.ActivationConditionRoot.Children[0].Predicate.ConditionId);
        }

        [Test]
        public void Mutation_RemoveLastSubjectConditionPrunesEmptyScope()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.target",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Fact("distance", CharacterTestRuntimeAiLabCardConditionFact.TargetDistance));

            bool applied = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveConditionNode,
                0,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    stringValue: "distance"),
                out bool changed,
                out string diagnostic);

            Assert.IsTrue(applied, diagnostic);
            Assert.IsTrue(changed);
            Assert.IsNull(payload.ActivationConditionRoot,
                "Deleting the last property must remove its now-empty subject scope.");
        }

        [Test]
        public void ConditionOwnerLayout_GivesRemainingHeightToConditionList()
        {
            Assert.AreEqual(
                334f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveConditionOwnerListHeight(600f, 200f, hasValidation: false),
                0.001f);
            Assert.AreEqual(
                288f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveConditionOwnerListHeight(600f, 200f, hasValidation: true),
                0.001f);
            Assert.AreEqual(
                162f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveConditionOwnerListHeight(420f, 300f, hasValidation: true),
                0.001f);
        }

        [Test]
        public void UnifiedEventLayout_GivesRemainingHeightToConditionList()
        {
            Assert.AreEqual(
                298f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveUnifiedEventConditionListHeight(
                        600f,
                        118f,
                        optionalFooterRows: 0,
                        hasValidation: false),
                0.001f);
            Assert.AreEqual(
                214f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveUnifiedEventConditionListHeight(
                        600f,
                        118f,
                        optionalFooterRows: 1,
                        hasValidation: true),
                0.001f);
            Assert.AreEqual(
                108f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveUnifiedEventConditionListHeight(
                        600f,
                        118f,
                        optionalFooterRows: 5,
                        hasValidation: true),
                0.001f);
        }

        [Test]
        public void AuthoringText_UsesTargetAsTheLockedTargetShortName()
        {
            Assert.AreEqual(
                "接近目标",
                BehaviorRecipeTimelineProjection.BuildV6OperationSummary(
                    new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                    },
                    CreatePayload(version: 10)));
        }

        [Test]
        public void Compiler_RejectsFactPlacedInWrongSubjectScope()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.target",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Fact("hazard", CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard));

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result =
                CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(payload, null);

            Assert.IsFalse(result.Success);
            Assert.That(result.Diagnostics, Has.Some.Matches<CharacterTestRuntimeAiLabCardDiagnostic>(
                item => item.Code == "CARD_V10_FACT_SCOPE_MISMATCH"));
        }

        [Test]
        public void InspectorProjection_UsesObjectRowsAndMarksMissingRuntimeSampleAsNotObserved()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope.target",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Fact("distance", CharacterTestRuntimeAiLabCardConditionFact.TargetDistance));
            var timeline = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                CanEdit = true,
                CardTitleText = "测试卡",
                RecipePayload = payload
            };

            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                timeline,
                payload,
                null,
                null,
                42);

            Assert.NotNull(timeline.RecipeInspector);
            Assert.AreEqual(2, timeline.RecipeInspector.Conditions.Length);
            Assert.IsTrue(timeline.RecipeInspector.Conditions[0].SubjectScope);
            Assert.AreEqual("目标", timeline.RecipeInspector.Conditions[0].FactText);
            Assert.AreEqual(
                BehaviorRecipeConditionObservationState.NotObserved,
                timeline.RecipeInspector.Conditions[1].ObservationState);
            Assert.AreEqual("未采样", timeline.RecipeInspector.Conditions[1].ObservedValueText);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreatePayload(int version)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = version,
                EntryTimelineId = "main"
            };
            payload.Timelines.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "main",
                Order = 0,
                Title = "Main"
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Fact(
            string id,
            CharacterTestRuntimeAiLabCardConditionFact fact) =>
            new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(fact, out _)
                    ? CharacterTestRuntimeAiLabBehaviorConditionCatalog.CreateDefault(fact, id + ".predicate")
                    : new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = id + ".predicate",
                        Fact = fact,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
            };

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Scope(
            string id,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child)
        {
            var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = subject
            };
            scope.Children.Add(child);
            return scope;
        }

        private static void AssertScope(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope, node.Kind);
            Assert.AreEqual(subject, node.Subject);
            Assert.AreEqual(1, node.Children.Count);
            Assert.AreEqual(fact, node.Children[0].Predicate.Fact);
        }

        private static string FirstDiagnostic(CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result) =>
            result?.Diagnostics != null && result.Diagnostics.Count > 0
                ? result.Diagnostics[0].Code + ": " + result.Diagnostics[0].Message
                : string.Empty;

        private sealed class RuntimeStub : ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime
        {
            public bool TargetExists;
            public bool TargetDistanceCondition;
            public int TargetDistanceReads;
            public CharacterTestRuntimeAiLabLogicActionState PrimaryActionState =>
                CharacterTestRuntimeAiLabLogicActionState.None;

            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetExists)
                    return TargetExists == condition.BoolValue;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance)
                {
                    TargetDistanceReads++;
                    return TargetDistanceCondition;
                }
                return false;
            }

            public bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage) => true;
            public bool TryInterruptPrimaryAction() => true;
        }
    }
}
