using System;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestTacticalAuthoringTemplates
    {
        internal const string WaitTwoSecondsDefinitionId = "tactical.shared.wait-two-seconds";
        internal const string WaitTargetDistanceReferenceDefinitionId = "tactical.shared.wait-target-distance-reference";
        internal const string WaitTargetDistanceDefinitionId = "tactical.shared.wait-target-distance";
        internal static CharacterTestTacticalSharedDefinition CreateWaitTwoSecondsDefinition()
            => new CharacterTestTacticalSharedDefinition { DefinitionId = WaitTwoSecondsDefinitionId, Source = CreateWaitTwoSeconds() };

        internal static CharacterTestTacticalAuthoringSource CreateWaitTwoSeconds()
        {
            var source = CreatePurposeWait("原地等待两秒", "Delay", CharacterTestDeterministicUnits.DefaultTickRate * 2);
            source.Timelines[0].EventBlocks[0].Timing.MinimumFrames = CharacterTestDeterministicUnits.DefaultTickRate * 2;
            return source;
        }

        // The author supplies reviewed design data. No default distance or timeout is inferred.
        internal static CharacterTestTacticalAuthoringSource CreateWaitTargetDistance(CharacterTestTacticalDistanceData fixedDistance, int maximumFrames)
        {
            if (fixedDistance == null || fixedDistance.Kind != "Fixed") throw new ArgumentException("Independent distance wait needs explicit fixed configuration.");
            var source = CreatePurposeWait("等待目标进入指定距离", "Distance", maximumFrames);
            source.Timelines[0].EventBlocks[0].DistanceData = fixedDistance;
            SetDistanceWaitGate(source, "purpose-wait");
            CharacterTestTacticalAuthoringCompiler.ValidateDistanceData(source, source.Timelines[0].EventBlocks[0]);
            return source.Clone();
        }

        internal static CharacterTestTacticalSharedDefinition CreateWaitTargetDistanceDefinition(CharacterTestTacticalDistanceData distance, int maximumFrames)
            => new CharacterTestTacticalSharedDefinition { DefinitionId = WaitTargetDistanceDefinitionId, Source = CreateWaitTargetDistance(distance, maximumFrames) };

        internal static CharacterTestTacticalSharedDefinition CreateWaitTargetDistanceReferenceDefinition(int maximumFrames)
            => new CharacterTestTacticalSharedDefinition { DefinitionId = WaitTargetDistanceReferenceDefinitionId, Source = CreateWaitTargetDistanceReference(maximumFrames) };

        internal static CharacterTestTacticalAuthoringSource CreateWaitTargetDistanceReference(int maximumFrames)
        {
            var source = CreatePurposeWait("等待目标进入指定距离", "Distance", maximumFrames);
            SetDistanceWaitGate(source, null);
            return source;
        }

        private static CharacterTestTacticalAuthoringSource CreatePurposeWait(string name, string completion, int duration)
        {
            if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration));
            var source = CreateBlank(name); source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4; source.Slots.Clear();
            var timeline = source.Timelines[0]; timeline.Title = name;
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "wait", Title = name, TimingKind = "Instant",
                Start = CharacterTestTacticalBlockEdits.Local(0), Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" } });
            var block = new CharacterTestTacticalEventBlock { BlockId = "purpose-wait", Title = name, Start = Absolute(0), Completion = completion,
                CompletionEventId = "wait", PassiveWait = true, RequireLivingTarget = completion == "Distance",
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = duration } };
            block.EventIds.Add("wait"); timeline.EventBlocks.Add(block); return source;
        }

        private static void SetDistanceWaitGate(CharacterTestTacticalAuthoringSource source, string blockId)
        {
            const string leaf = "wait.distance";
            source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding { NodeId = leaf, Kind = "IntentDistanceRange", SourceBlockId = blockId });
            source.Timelines[0].Events[0].ConditionRoot = All("wait.ready", Alive("wait.alive"), TargetScope(leaf + ".scope",
                new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = leaf, Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate }));
        }

        internal const string WaitOpportunityDefinitionId = "tactical.shared.wait-opportunity";
        internal static CharacterTestTacticalSharedDefinition CreateWaitOpportunityDefinition(int maximumFrames = 60)
            => new CharacterTestTacticalSharedDefinition { DefinitionId = WaitOpportunityDefinitionId,
                Revision = 2, Source = CreateWaitOpportunity(maximumFrames) };

        internal static CharacterTestTacticalAuthoringSource CreateWaitOpportunity(int maximumFrames = 60)
        {
            if (maximumFrames < 1) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            var source = CreateBlank("等待条件");
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear();
            var timeline = source.Timelines[0]; timeline.Title = source.Name;
            const string blockId = "wait-opportunity";
            var ended = TargetScope("wait.window.scope", new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {
                NodeId = "wait.window", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "wait.window",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals, BoolValue = true } });
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "wait", Title = "等待进入时捕获的目标攻击判定全部退出",
                TimingKind = "Instant", Start = CharacterTestTacticalBlockEdits.Local(0),
                ConditionRoot = All("wait.ready", Alive("wait.alive"), ended),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" } });
            var block = new CharacterTestTacticalEventBlock { BlockId = blockId, Title = source.Name, Start = Absolute(0),
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = maximumFrames },
                Completion = "Condition", CompletionEventId = "wait", RequireLivingTarget = true, PassiveWait = true };
            block.EventIds.Add("wait"); timeline.EventBlocks.Add(block);
            return source;
        }
    }
}
