using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestTacticalAuthoringTemplates
    {
        internal const string OrdinaryGuardDefinitionId = "tactical.shared.ordinary-guard";

        // Duration comes from the equipped finite Guard action. This factory neither
        // grants an action nor owns direction/type rules or presentation.
        internal static CharacterTestTacticalSharedDefinition CreateOrdinaryGuardDefinition(int maximumGuardFrames = 120)
        {
            if (maximumGuardFrames < 1) throw new ArgumentOutOfRangeException(nameof(maximumGuardFrames));
            var source = CreateBlank("普通防御");
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear();
            source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "guard", Resolver = "ordinaryGuard",
                Description = "装配角色的普通防御动作；有效格挡窗口由该动作拥有。" });
            var timeline = source.Timelines[0]; timeline.Title = source.Name;
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "guard", Title = "普通防御姿态",
                Start = CharacterTestTacticalBlockEdits.Local(0),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "guard" } });
            var block = new CharacterTestTacticalEventBlock { BlockId = "ordinary-guard", Title = source.Name,
                Start = Absolute(0), Completion = "Action", CompletionEventId = "guard", MaximumGuardFrames = maximumGuardFrames,
                PreparationTimeoutFrames = 2, CompletionTimeoutFrames = 300 };
            block.EventIds.Add("guard"); timeline.EventBlocks.Add(block);
            return new CharacterTestTacticalSharedDefinition { DefinitionId = OrdinaryGuardDefinitionId, Revision = 2, Source = source };
        }

        internal const string FacingTargetDefinitionId = "tactical.shared.facing-target";
        internal static CharacterTestTacticalSharedDefinition CreateFacingTargetDefinition(float thresholdDegrees = 5f, int maximumFrames = 30)
            => new() { DefinitionId = FacingTargetDefinitionId, Revision = 1, Source = CreateFacingTarget(thresholdDegrees, maximumFrames) };

        internal static CharacterTestTacticalAuthoringSource CreateFacingTarget(float thresholdDegrees = 5f, int maximumFrames = 30)
        {
            if (float.IsNaN(thresholdDegrees) || float.IsInfinity(thresholdDegrees) || thresholdDegrees <= 0f || thresholdDegrees > 180f)
                throw new ArgumentOutOfRangeException(nameof(thresholdDegrees));
            if (maximumFrames < 1) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            var source = CreateBlank("面向目标"); source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear(); var timeline = source.Timelines[0]; timeline.Title = source.Name;
            const string blockId = "facing-target";
            var angle = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "aligned.angle",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "aligned.angle",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual, Value = thresholdDegrees } };
            var below = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "aligned.below",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not }; below.Children.Add(angle);
            // A negated comparison alone would accept an unavailable direction (for example coincident targets).
            var valid = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "aligned.valid",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "aligned.valid",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between, Value = 0, MaxValue = 180 } };
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "face", Title = "转向当前目标",
                Start = CharacterTestTacticalBlockEdits.Local(0), End = VisualEnd(blockId), ConditionRoot = Alive("face.alive"),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "aligned", Title = "等待朝向夹角小于阈值",
                TimingKind = "Instant", Start = CharacterTestTacticalBlockEdits.Local(0),
                ConditionRoot = All("aligned.ready", Alive("aligned.alive"), TargetScope("aligned.valid.scope", valid), TargetScope("aligned.scope", below)),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" } });
            var block = new CharacterTestTacticalEventBlock { BlockId = blockId, Title = source.Name, Start = Absolute(0),
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = maximumFrames },
                Completion = "Facing", CompletionEventId = "aligned", RequireLivingTarget = true };
            block.EventIds.AddRange(new[] { "face", "aligned" }); timeline.EventBlocks.Add(block); return source;
        }

        internal static CharacterTestTacticalAuthoringSource CreateFacingForwardStep()
        {
            var source = CreateBlank("面向目标向前垫步");
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear(); source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "step", Resolver = "stepForward" });
            var timeline = source.Timelines[0]; timeline.Title = source.Name;
            const string blockId = "facing-forward-step";
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Facing(string id) => TargetScope(id + ".scope", new() { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = id,
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, Value = 5 } });
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "face", Title = "面向目标",
                Start = CharacterTestTacticalBlockEdits.Local(0), End = Anchor("step", "Start"), ConditionRoot = Alive("face.alive"),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "aligned", Title = "等待面向到位", TimingKind = "Instant",
                Start = CharacterTestTacticalBlockEdits.Local(0), Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" },
                ConditionRoot = All("aligned.all", Alive("aligned.alive"), Facing("aligned.angle")) });
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "step", Title = "向前垫步",
                Start = Anchor("aligned", "End"), Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "step" },
                ConditionRoot = null });
            var block = new CharacterTestTacticalEventBlock { BlockId = blockId, Title = source.Name, Start = Absolute(0),
                Completion = "Action", CompletionEventId = "step", PreparationTimeoutFrames = 30,
                CompletionTimeoutFrames = 300, RequireLivingTarget = true, ContinueToActionOnPreparationTimeout = true };
            block.EventIds.AddRange(new[] { "face", "aligned", "step" }); timeline.EventBlocks.Add(block);
            return source;
        }

        internal static CharacterTestTacticalAuthoringSource CreateRetreatToDistance()
        {
            var source = CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Name = CharacterTestTacticalDistanceArrivalHint.RetreatTitle; source.Slots.Clear();
            var timeline = source.Timelines[0]; timeline.Title = source.Name;
            var block = new CharacterTestTacticalEventBlock { BlockId = "retreat-distance-block", Title = source.Name,
                Start = Absolute(0), Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = 120 } };
            foreach (var ev in timeline.Events)
            {
                ev.Start = CharacterTestTacticalBlockEdits.Local(0); ev.End = VisualEnd(block.BlockId);
                if (ev.EventId == "move") ev.Title = "后撤移动";
                block.EventIds.Add(ev.EventId);
            }
            timeline.EventBlocks.Add(block);
            return source;
        }

        internal static CharacterTestTacticalAuthoringSource CreateVisualAttack(string name = "常规移动接敌出招")
        {
            var source = CreateSingleAttack(name);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            var timeline = source.Timelines[0];
            var block = new CharacterTestTacticalEventBlock { BlockId = "attack-block", Title = "常规移动接敌出招",
                Start = Absolute(0), Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 61, DurationFrames = 61 } };
            block.EventIds.AddRange(timeline.Events.Select(e => e.EventId)); timeline.EventBlocks.Add(block);
            var move = timeline.Events.First(e => e.EventId == "approach");
            move.Title = "接近"; move.Operation = new CharacterTestTacticalAuthoringOperation { Kind = "Movement",
                Movement = new CharacterTestTacticalMovement { Strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                    DistanceSource = "ActionOptimal", ActionSlotId = "primary" } };
            move.Start = CharacterTestTacticalBlockEdits.Local(0); move.End = Anchor("attack", "Start");
            var face = timeline.Events.First(e => e.EventId == "facing"); face.Title = "面向目标";
            face.Start = CharacterTestTacticalBlockEdits.Local(0); face.End = VisualEnd(block.BlockId);
            var gate = timeline.Events.First(e => e.EventId == "release"); gate.Title = "等待出手机会"; gate.Start = CharacterTestTacticalBlockEdits.Local(0);
            var action = timeline.Events.First(e => e.EventId == "attack"); action.Title = "主攻击槽"; action.Start = Anchor("release", "End");
            CharacterTestTacticalApproachActionSplit.Apply(source);
            CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(source);
            return source;
        }
        // Two modes share this maker-only factory. All unapproved spatial/time values are mandatory inputs.
        internal static CharacterTestTacticalSharedDefinition CreateBehindTargetDefinition(bool maintain,
            float halfAngle, float resumeHalfAngle, float minimum, float maximum, float tolerance,
            float resumeTolerance, int maximumFrames, float radialWeight, float speed, float directionSwitchHysteresis,
            string distanceSource = "Manual", string actionSlotId = "")
        {
            if (maximumFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            var rear = new CharacterTestTacticalBehindTarget { Maintain = maintain, HalfAngleDegrees = halfAngle,
                ResumeHalfAngleDegrees = resumeHalfAngle, DistanceTolerance = tolerance,
                ResumeDistanceTolerance = resumeTolerance, DirectionSwitchHysteresisDegrees = directionSwitchHysteresis };
            rear.Validate(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, 0);
            var source = CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4; source.Slots.Clear();
            source.Name = maintain ? "绕至并保持在目标背后" : "绕至目标背后";
            var timeline = source.Timelines[0]; timeline.Title = source.Name;
            var move = timeline.Events.Find(e => e.EventId == "move"); move.Title = source.Name; move.Operation.Speed = speed;
            move.Operation.Movement = new CharacterTestTacticalMovement { Strategy = CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                Target = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, Minimum = minimum,
                Maximum = maximum, RadialWeight = radialWeight, DistanceSource = distanceSource, ActionSlotId = actionSlotId,
                BehindTarget = rear };
            if (distanceSource != "Manual")
                source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = actionSlotId });
            const string blockId = "behind-target-block";
            var block = new CharacterTestTacticalEventBlock { BlockId = blockId, Title = source.Name, Start = Absolute(0),
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = maximumFrames },
                Completion = maintain ? "Timed" : "Arrived", CompletionEventId = maintain ? "" : "move" };
            foreach (var ev in timeline.Events)
            { ev.Start = CharacterTestTacticalBlockEdits.Local(0); ev.End = VisualEnd(blockId); block.EventIds.Add(ev.EventId); }
            timeline.EventBlocks.Add(block);
            return new CharacterTestTacticalSharedDefinition { DefinitionId = "tactical.shared.behind-target."
                + (maintain ? "maintain" : "arrive"), Revision = 1, Source = source };
        }

        internal static CharacterTestTacticalFrameReference VisualEnd(string block, int offset = 0)
            => new() { Kind = "Anchor", TargetKind = "Builtin", SourceEventId = block, Anchor = "SimulationEnd", OffsetFrames = offset };

        internal static CharacterTestTacticalAuthoringSource CreateVisualCard(string name)
        {
            if (name == "横切脱困" || name == "无路侧切" || name == "攻后换位")
                throw new ArgumentException("横移相关战术卡模板已删除，请使用环绕封装。", nameof(name));
            var source = CreateVisualAttack(name);
            bool onlyMove = name == "守距观望" || name == "脱离目标" || name == "贴身逼近" || name == "残血撤离";
            bool before = name == "撤步还击" || name == "绕侧进攻";
            bool after = name == "攻后脱离";
            if (!onlyMove && !before && !after) return source;
            var timeline = source.Timelines[0];
            if (onlyMove) { timeline.Events.Clear(); timeline.EventBlocks.Clear(); source.PredicateBindings.Clear(); source.Slots.Clear(); }
            string id = "movement-block";
            var strategy = name switch {
                "守距观望" => CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance,
                "绕侧进攻" => CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                "贴身逼近" => CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                _ => CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard };
            int frames = before ? 12 : 30;
            var block = new CharacterTestTacticalEventBlock { BlockId = id, Title = strategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard ? CharacterTestTacticalRetreatHint.Title
                    : name == "绕侧进攻" ? CharacterTestTacticalOrbitHint.Title
                    : name == "守距观望" ? CharacterTestTacticalKeepDistanceHint.Title : CharacterTestTacticalMovement.Label(strategy),
                Start = after ? CharacterTestTacticalBlockEdits.BlockEnd("attack-block") : Absolute(0),
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = frames, DurationFrames = frames } };
            if (name == "贴身逼近") block.Title = CharacterTestTacticalApproachActionSplit.ApproachTitle;
            var move = new CharacterTestTacticalAuthoringEvent { EventId = "move", Title = strategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard ? "后撤移动"
                    : name == "绕侧进攻" ? "环绕移动" : name == "守距观望" ? "守距移动" : block.Title, Start = CharacterTestTacticalBlockEdits.Local(0), End = VisualEnd(id),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "Movement", Movement = new CharacterTestTacticalMovement { Strategy = strategy, Minimum = 1.5f, Maximum = 3f } } };
            var facing = new CharacterTestTacticalAuthoringEvent { EventId = "move-facing", Title = "面向目标", Start = CharacterTestTacticalBlockEdits.Local(0), End = VisualEnd(id),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } };
            timeline.Events.Add(move); timeline.Events.Add(facing); block.EventIds.Add(move.EventId); block.EventIds.Add(facing.EventId);
            timeline.EventBlocks.Add(block);
            if (before) timeline.EventBlocks.Single(b => b.BlockId == "attack-block.approach").Start = CharacterTestTacticalBlockEdits.BlockEnd(id);
            CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(source);
            return source;
        }
    }
}
