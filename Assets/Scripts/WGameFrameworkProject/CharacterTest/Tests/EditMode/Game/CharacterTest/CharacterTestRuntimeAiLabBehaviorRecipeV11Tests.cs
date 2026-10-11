using System;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeV11Tests
    {
        private const uint Existing = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.ExistingPhaseMask;

        [Test]
        public void TargetStateMask_IsCanonicalAndLegacyProjectionsAreDerived()
        {
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            Assert.AreEqual(CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, facts.TargetStateMask);
            Assert.IsFalse(facts.TargetExists);
            Assert.IsFalse(facts.TargetDead);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerTargetStatePolicy.HasSinglePhase(facts.TargetStateMask));

            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetExists, true);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible, true);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable, true);
            facts.SetValue(
                CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState,
                CharacterTestRuntimeAiPlannerTargetLifeState.Dying);

            Assert.IsTrue(facts.TargetExists);
            Assert.IsTrue(facts.TargetDead);
            Assert.IsTrue(facts.TargetVisible);
            Assert.IsTrue(facts.MovePathAvailable);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerTargetLifeState.Dying, facts.TargetLifeState);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerTargetStatePolicy.HasSinglePhase(facts.TargetStateMask));

            facts.SetNoTarget(selfActionRunning: false);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, facts.TargetStateMask);
            Assert.IsFalse(facts.TargetVisible);
            Assert.IsFalse(facts.MovePathAvailable);
            facts.SetTargetAttackThreat(default, 1, 2, true, true, true);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, facts.TargetStateMask,
                "No-target frames must reject every orthogonal target-state bit.");
        }

        [Test]
        public void TargetStateMask_MatchesAllowedRequiredAndForbiddenBits()
        {
            CharacterTestRuntimeAiPlannerTargetStateMask state =
                CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                | CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable
                | CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange;
            Assert.IsTrue(CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                state,
                (uint)(CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                    | CharacterTestRuntimeAiPlannerTargetStateMask.Dying),
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                state,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dead,
                0u,
                0u));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                state,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive,
                0u,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange));
        }

        [Test]
        public void TargetStateMask_WarmedEvaluationIsNoAlloc()
        {
            CharacterTestRuntimeAiPlannerTargetStateMask state =
                CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                | CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable;
            uint phases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive;
            uint required = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable;
            uint forbidden = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked;
            Assert.IsTrue(CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                state,
                phases,
                required,
                forbidden));
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool matched = false;
            for (int i = 0; i < 10000; i++)
            {
                matched ^= CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                    state,
                    phases,
                    required,
                    forbidden);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(before, after);
            Assert.IsFalse(matched);
        }

        [Test]
        public void SelfActionInterruptible_NoActionOrAnyCancelWindowIsTrue()
        {
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetSelfActionState(running: false, interruptible: false);
            Assert.IsTrue(facts.SelfActionInterruptible);
            facts.SetSelfActionState(running: true, interruptible: false);
            Assert.IsFalse(facts.SelfActionInterruptible);
            facts.SetSelfActionState(running: true, interruptible: true);
            Assert.IsTrue(facts.SelfActionInterruptible);

            var rules = new[]
            {
                new CharacterCancelRule(
                    5,
                    9,
                    CharacterActionRequestType.Skill,
                    CharacterCancelInputMask.Skill)
            };
            Assert.IsFalse(CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(rules, 4));
            Assert.IsTrue(CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(rules, 5));
            Assert.IsTrue(CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(rules, 9));
            Assert.IsFalse(CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(rules, 10));
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool sampled = false;
            for (int i = 0; i < 10000; i++)
                sampled ^= CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(rules, 7);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(before, after);
            Assert.IsFalse(sampled);
        }

        [Test]
        public void Migration_DeprecatedActionRunningFalseBecomesInterruptibleTrue()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode leaf = Leaf(
                "action",
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning,
                false);
            payload.ActivationConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "self.scope",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self
            };
            payload.ActivationConditionRoot.Children.Add(leaf);

            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);

            CharacterTestRuntimeAiLabCardCondition migrated =
                payload.ActivationConditionRoot.Children[0].Predicate;
            Assert.AreEqual("self.scope", payload.ActivationConditionRoot.NodeId);
            Assert.AreEqual("action", payload.ActivationConditionRoot.Children[0].NodeId);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible,
                migrated.Fact);
            Assert.IsTrue(migrated.BoolValue);
        }

        [Test]
        public void TargetStateConditionPresentation_SeparatesRequirementFromRuntimeEvidence()
        {
            Assert.AreEqual(
                "要求：存活 · 必须：可感知",
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .FormatTargetStateRequirement("阶段：存活 · 必须：可感知"));
            var row = new BehaviorRecipeTriggerConditionViewModel
            {
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                ObservationState = BehaviorRecipeConditionObservationState.Satisfied,
                ObservedValueText = "存活 · 可感知",
                ObservationReasonText = "阶段、必须状态和排除状态均匹配。"
            };
            Assert.AreEqual(
                "✓ 当前状态满足要求",
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .FormatConditionObservation(row));

            row.ObservationState = BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservationReasonText = "缺少必须状态：可感知。";
            Assert.AreEqual(
                "✕ 当前状态不满足要求 · 缺少必须状态：可感知。",
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .FormatConditionObservation(row));
        }

        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true,
            Existing, 0u, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, false,
            (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, 0u, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetDead, true,
            (uint)(CharacterTestRuntimeAiPlannerTargetStateMask.Dying
                | CharacterTestRuntimeAiPlannerTargetStateMask.Dead
                | CharacterTestRuntimeAiPlannerTargetStateMask.Reviving), 0u, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetDead, false,
            (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, 0u, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded, true,
            Existing, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded, 0u)]
        [TestCase(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded, false,
            Existing, 0u, (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded)]
        public void Migration_V10BooleanFactsMapOneToOne(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            bool value,
            uint phases,
            uint required,
            uint forbidden)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 10);
            payload.ActivationConditionRoot = Leaf("leaf", fact, value);

            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.UpgradeFromV10(payload);

            CharacterTestRuntimeAiLabCardCondition migrated = payload.ActivationConditionRoot.Predicate;
            Assert.AreEqual(11, payload.BehaviorRecipeVersion);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardConditionFact.TargetState, migrated.Fact);
            Assert.AreEqual(phases, migrated.AllowedTargetPhases);
            Assert.AreEqual(required, migrated.RequiredTargetFlags);
            Assert.AreEqual(forbidden, migrated.ForbiddenTargetFlags);
            Assert.AreEqual("leaf", payload.ActivationConditionRoot.NodeId);
        }

        [Test]
        public void Migration_V10RootDistanceBecomesHurtboxDistanceWithoutChangingIdentity()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 10);
            payload.ActivationConditionRoot = Scope(
                "scope",
                Leaf(
                    "distance",
                    CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                    true));

            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.UpgradeFromV10(payload);

            Assert.AreEqual(11, payload.BehaviorRecipeVersion);
            Assert.AreEqual("scope", payload.ActivationConditionRoot.NodeId);
            Assert.AreEqual("distance", payload.ActivationConditionRoot.Children[0].NodeId);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                payload.ActivationConditionRoot.Children[0].Predicate.Fact);
        }

        [Test]
        public void Mutation_NoTargetMovesSoleLeafOutsideScopeAndNotPathIsRejected()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode leaf =
                Leaf("state", CharacterTestRuntimeAiLabCardConditionFact.TargetState, true);
            leaf.Predicate.AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive;
            payload.ActivationConditionRoot = Scope("scope", leaf);

            bool applied = ApplyMasks(
                payload,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget,
                out string diagnostic);
            Assert.IsTrue(applied, diagnostic);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                payload.ActivationConditionRoot.Kind);
            Assert.AreEqual("state", payload.ActivationConditionRoot.NodeId);

            var not = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "not",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
            };
            leaf = Leaf("state.2", CharacterTestRuntimeAiLabCardConditionFact.TargetState, true);
            leaf.Predicate.AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive;
            not.Children.Add(leaf);
            payload.ActivationConditionRoot = Scope("scope.2", not);
            applied = ApplyMasks(
                payload,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget,
                out diagnostic);
            Assert.IsFalse(applied);
            StringAssert.Contains("Not", diagnostic);
        }

        [Test]
        public void Mutation_NoTargetSplitsAndRestoresAllScopeWithoutChangingTreeIdentity()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode state = Leaf(
                "state",
                CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                true);
            var distance = Leaf(
                "distance",
                CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                true);
            distance.Predicate.Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual;
            distance.Predicate.Value = 3f;
            var all = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "all",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            all.Children.Add(state);
            all.Children.Add(distance);
            payload.ActivationConditionRoot = Scope("scope", all);

            Assert.IsTrue(ApplyMasks(
                payload,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget,
                out string diagnostic), diagnostic);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All,
                payload.ActivationConditionRoot.Kind);
            Assert.AreEqual("all", payload.ActivationConditionRoot.NodeId);
            Assert.AreEqual("state", payload.ActivationConditionRoot.Children[0].NodeId);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                payload.ActivationConditionRoot.Children[1].Kind);
            Assert.AreEqual("distance",
                payload.ActivationConditionRoot.Children[1].Children[0].NodeId);

            Assert.IsTrue(ApplyMasks(
                payload,
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive,
                out diagnostic), diagnostic);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                payload.ActivationConditionRoot.Kind);
            Assert.AreEqual("scope", payload.ActivationConditionRoot.NodeId);
            Assert.AreEqual("all", payload.ActivationConditionRoot.Children[0].NodeId);
            Assert.AreEqual("state", payload.ActivationConditionRoot.Children[0].Children[0].NodeId);
            Assert.AreEqual("distance", payload.ActivationConditionRoot.Children[0].Children[1].NodeId);
        }

        [Test]
        public void SemanticHash_ContainsAllThreeTargetStateMasks()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            payload.ActivationConditionRoot = Leaf(
                "state",
                CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                true);
            long baseline = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(payload);

            payload.ActivationConditionRoot.Predicate.RequiredTargetFlags =
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable;
            long required = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(payload);
            payload.ActivationConditionRoot.Predicate.RequiredTargetFlags = 0u;
            payload.ActivationConditionRoot.Predicate.ForbiddenTargetFlags =
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked;
            long forbidden = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(payload);
            payload.ActivationConditionRoot.Predicate.ForbiddenTargetFlags = 0u;
            payload.ActivationConditionRoot.Predicate.AllowedTargetPhases =
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dying;
            long phases = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(payload);

            Assert.AreNotEqual(baseline, required);
            Assert.AreNotEqual(baseline, forbidden);
            Assert.AreNotEqual(baseline, phases);
        }

        [TestCase(0u, 0u, 0u, CharacterTestRuntimeAiLabCardConditionOperator.Equals,
            "CARD_V11_TARGET_PHASE_EMPTY")]
        [TestCase(1u << 7, 0u, 0u, CharacterTestRuntimeAiLabCardConditionOperator.Equals,
            "CARD_V11_TARGET_PHASE_UNKNOWN")]
        [TestCase((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, 1u << 31, 0u,
            CharacterTestRuntimeAiLabCardConditionOperator.Equals, "CARD_V11_TARGET_REQUIRED_UNKNOWN")]
        [TestCase((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, 0u, 1u << 31,
            CharacterTestRuntimeAiLabCardConditionOperator.Equals, "CARD_V11_TARGET_FORBIDDEN_UNKNOWN")]
        [TestCase((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive,
            (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable,
            (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable,
            CharacterTestRuntimeAiLabCardConditionOperator.Equals, "CARD_V11_TARGET_FLAGS_OVERLAP")]
        [TestCase((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, 0u, 0u,
            CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan, "CARD_V11_TARGET_OPERATOR_INVALID")]
        public void Compiler_RejectsInvalidTargetStateMasksWithStableDiagnostic(
            uint phases,
            uint required,
            uint forbidden,
            CharacterTestRuntimeAiLabCardConditionOperator op,
            string expectedCode)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            payload.ActivationConditionRoot = Leaf(
                "state",
                CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                true);
            payload.ActivationConditionRoot.Predicate.AllowedTargetPhases = phases;
            payload.ActivationConditionRoot.Predicate.RequiredTargetFlags = required;
            payload.ActivationConditionRoot.Predicate.ForbiddenTargetFlags = forbidden;
            payload.ActivationConditionRoot.Predicate.Operator = op;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload, null);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(HasDiagnostic(result, expectedCode), FirstDiagnostic(result));
        }

        [Test]
        public void Compiler_RejectsNoTargetInsideTargetScope()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Payload(version: 11);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode leaf = Leaf(
                "state",
                CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                true);
            leaf.Predicate.AllowedTargetPhases =
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget;
            payload.ActivationConditionRoot = Scope("scope", leaf);

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload, null);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(HasDiagnostic(result, "CARD_V11_NO_TARGET_INSIDE_SCOPE"), FirstDiagnostic(result));
        }

        private static bool ApplyMasks(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            uint phases,
            out string diagnostic) =>
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionTargetStateMasks,
                0,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    allowedTargetPhases: phases),
                out _,
                out diagnostic);

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload Payload(int version)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = version,
                EntryTimelineId = "main"
            };
            payload.Timelines.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "main",
                Title = "Main"
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Leaf(
            string id,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            bool value) =>
            new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = id + ".predicate",
                    Fact = fact,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    BoolValue = value,
                    AllowedTargetPhases = fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState
                        ? (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                        : 0u
                }
            };

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Scope(
            string id,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child)
        {
            var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
            };
            scope.Children.Add(child);
            return scope;
        }

        private static bool HasDiagnostic(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result,
            string code)
        {
            for (int i = 0; result?.Diagnostics != null && i < result.Diagnostics.Count; i++)
                if (result.Diagnostics[i].Code == code)
                    return true;
            return false;
        }

        private static string FirstDiagnostic(CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result) =>
            result?.Diagnostics != null && result.Diagnostics.Count > 0
                ? result.Diagnostics[0].Code + "@" + result.Diagnostics[0].Path
                : "No diagnostics.";
    }
}
