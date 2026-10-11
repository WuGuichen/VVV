using System;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Explicit one-time authoring templates. Compilation never calls these factories.</summary>
    /// <remarks>LowFreqAlloc. Returned data belongs to the caller's editing transaction.</remarks>
    internal static partial class CharacterTestTacticalAuthoringTemplates
    {
        /// <remarks>Use the existing import/edit workspace transaction; caller supplies reviewed design values.</remarks>
        internal static CharacterTestTacticalAuthoringSource CreatePointMove(string name,
            MxFramework.Game.CharacterTest.TacticalCard.TacticalPointMoveAuthoring contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            contract.Freeze();
            var source = CreateBlank(name);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear(); source.ExecutionInput = contract;
            return source.Clone();
        }

        internal static CharacterTestTacticalAuthoringSource CreateBlank(string name)
        {
            var source = new CharacterTestTacticalAuthoringSource { Name = name ?? string.Empty, EntryTimelineId = "single-primary" };
            source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "primary", Resolver = "lightAttackOpener" });
            source.Timelines.Add(new CharacterTestTacticalAuthoringTimeline { TimelineId = "single-primary", Title = "单段攻击" });
            return source;
        }

        internal static CharacterTestTacticalAuthoringSource CreateSingleAttack(string name = "接近并普攻一次")
        {
            var source = CreateBlank(name);
            source.ActivationConditionRoot = Alive("activation.alive");
            var events = source.Timelines[0].Events;
            events.Add(Event("approach", "ApproachOptimalRange", Absolute(0), Anchor("attack", "ActionStartupEnd"),
                new CharacterTestTacticalAuthoringOperation { Kind = "ApproachOptimalRange", SourceActionEventId = "attack" }));
            events.Add(Event("facing", "TurnToTarget", Absolute(0), Anchor("attack", "ActionEnd"),
                new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" }));
            var gate = Event("release", "WaitForCondition", Absolute(0), null,
                new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" });
            gate.ConditionRoot = All("gate.all", Alive("gate.alive"),
                Bound(source, "gate.range", "RootDistanceRange"), Bound(source, "gate.facing", "TargetFacing"));
            events.Add(gate);
            var action = Event("attack", "CharacterAction", Absolute(1), null,
                new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "primary" });
            events.Add(action);
            return source;
        }

        internal static CharacterTestTacticalAuthoringSource CreateMovement(CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            var source = CreateBlank(CharacterTestTacticalMovement.Label(strategy));
            source.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema; source.Version = 1;
            source.Timelines[0].Title = source.Name;
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "move", Title = source.Name,
                Start = Absolute(0), End = Absolute(120), Operation = new CharacterTestTacticalAuthoringOperation {
                    Kind = "Movement", Movement = new CharacterTestTacticalMovement { Strategy = strategy } } });
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "face", Title = "面向目标",
                Start = Absolute(0), End = Anchor("move", "End"), Operation = new CharacterTestTacticalAuthoringOperation {
                    Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            return source;
        }

        internal static bool TryImportVersionOne(string json, out CharacterTestTacticalAuthoringSource source, out string issue)
        {
            source = null;
            if (!CharacterTestTacticalCardDefinition.TryRead(json, out var definition, out issue)) return false;
            source = CreateSingleAttack(definition.Name);
            if (definition.Approach == CharacterTestTacticalCardApproach.None)
                source.Timelines[0].Events.RemoveAll(evt => evt.EventId == "approach");
            return true;
        }

        internal static CharacterTestTacticalFrameReference Absolute(int frame) => new CharacterTestTacticalFrameReference { Kind = "Absolute", Frame = frame };
        internal static CharacterTestTacticalFrameReference Anchor(string id, string anchor, int offset = 0)
            => new CharacterTestTacticalFrameReference { Kind = "Anchor", SourceEventId = id, Anchor = anchor, OffsetFrames = offset };

        private static CharacterTestTacticalAuthoringEvent Event(string id, string kind, CharacterTestTacticalFrameReference start,
            CharacterTestTacticalFrameReference end, CharacterTestTacticalAuthoringOperation operation)
            => new CharacterTestTacticalAuthoringEvent { EventId = id, Title = id, TimingKind = kind == "WaitForCondition" ? "Instant" : "Duration",
                Start = start, End = end, Operation = operation };

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode All(string id, params CharacterTestRuntimeAiLabBehaviorRecipeConditionNode[] children)
        {
            var node = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id, Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All };
            node.Children.AddRange(children);
            return node;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode TargetScope(string id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child)
        {
            var node = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget };
            node.Children.Add(child);
            return node;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Alive(string id)
            => TargetScope(id + ".scope", new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = id, Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive } });

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Bound(CharacterTestTacticalAuthoringSource source, string id, string kind)
        {
            source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding { NodeId = id, Kind = kind, SourceActionEventId = "attack" });
            return TargetScope(id + ".scope", new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate });
        }
    }
}
