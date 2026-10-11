using System;
using System.Linq;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    // Cold authoring data over existing published execution blocks. Owns no planner, costs or action state.
    internal static class CharacterTestRuntimeAiPlannerSwordPreset
    {
        internal const string Name = "单手剑 · 攻后脱离";
        internal const int CharacterId = 668367834, AttackActionId = 455;
        internal const float EngageDistance = 3f, ReactionHorizonFrames = 24f;
        internal static readonly string[] CardIds = { "bc_fe8w87d52r", "bc_7hj69v6kd6", "bc_3205xjgcw8", "bc_g6a5fw319a" };
        internal static readonly string[] EntryIds = { "fa36bd5f-a06a-4cb3-a63b-90d206496142", "277caecd-7dbb-46d8-bcc8-3e466b4973f9",
            "c7d169b4-97c6-47ae-9c65-9d40e93c40ba", "0f755ffb-6089-4ec6-a58a-a62df1b8c97d" };
        private static readonly string[] Sources = { "bc_nyy27xm44e", "bc_er3x1b5pzf", "bc_q3d2dvmret" };
        internal static string CardName(int kind) => kind == 0 ? "单手剑 · 主动接敌" : kind == 1 ? "单手剑 · 攻后脱离" : "单手剑 · 威胁撤距";

        internal static CharacterTestTacticalAuthoringSource CreateSource(int kind,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            if (kind < 0 || kind > 2) throw new ArgumentOutOfRangeException(nameof(kind));
            if (catalog == null || !catalog.TryResolveLatestCompatible(Sources[kind],
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var published)
                || published.TacticalSource == null) throw new InvalidOperationException("Sword preset source unavailable.");
            var definitions = published.TacticalSource.CloneDefinitions();
            if (definitions.Length != 1) throw new InvalidOperationException("Sword preset requires one existing execution source.");
            var source = definitions[0].Source.Clone();
            source.Name = CardName(kind);
            var alive = State("sword.target", CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable,
                CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked);
            var imminent = All("sword.threat", State("will-hit", CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf),
                Numeric("impact", CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames,
                    CharacterTestRuntimeAiLabCardConditionOperator.Between, 0, ReactionHorizonFrames));
            var activation = kind == 2 ? All("sword.reaction", alive, imminent)
                : All("sword.engagement", alive, Not("no-imminent-threat", imminent),
                    Numeric("engage-distance", CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                        kind == 0 ? CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan : CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual,
                        EngageDistance));
            source.ActivationConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {
                NodeId = "sword.target.scope", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget };
            source.ActivationConditionRoot.Children.Add(activation);
            if(kind!=2) AddThreatYields(source);
            // Preserve the existing movement, approach completion, real action and withdrawal timing.
            // 3m is a selection preference; the attack source still uses the real bound action's measured range.
            return CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(source));
        }

        private static void AddThreatYields(CharacterTestTacticalAuthoringSource source)
        {
            foreach(var timeline in source.Timelines) foreach(var ev in timeline.Events.ToArray()) {
                bool action=ev.Operation.Kind=="CharacterAction";
                if(!action && ev.Operation.Kind!="Movement" && ev.Operation.Kind!="ApproachOptimalRange" && ev.Operation.Kind!="TurnToTarget") continue;
                string id="sword.yield."+ev.EventId;
                var target=new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {NodeId=id+".target.scope",
                    Kind=CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                    Subject=CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget};
                target.Children.Add(All(id+".threat",State(id+".will-hit",CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf),
                    Numeric(id+".impact",CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames,
                        CharacterTestRuntimeAiLabCardConditionOperator.Between,0,ReactionHorizonFrames)));
                var self=new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {NodeId=id+".self.scope",
                    Kind=CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                    Subject=CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self};
                self.Children.Add(Predicate(new CharacterTestRuntimeAiLabCardCondition {ConditionId=id+".interruptible",
                    Fact=CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible,BoolValue=true}));
                var start=Copy(ev.Start); var end=Copy(action?ev.Start:ev.End);
                if(action) {if(end.Kind=="Absolute")end.Frame++;else end.OffsetFrames++;}
                timeline.Events.Add(new CharacterTestTacticalAuthoringEvent {EventId=id,Title="威胁出现时交回规划器",
                    Start=start,End=end,Priority=1000,ConditionRoot=All(id+".allowed",self,target),
                    Operation=new CharacterTestTacticalAuthoringOperation {Kind="ReturnToPlanner"}});
                var block=timeline.EventBlocks.SingleOrDefault(b=>b.EventIds.Contains(ev.EventId));
                if(block!=null)block.EventIds.Add(id);
            }
        }
        private static CharacterTestTacticalFrameReference Copy(CharacterTestTacticalFrameReference value)
            =>JsonConvert.DeserializeObject<CharacterTestTacticalFrameReference>(JsonConvert.SerializeObject(value));

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode All(string id,
            params CharacterTestRuntimeAiLabBehaviorRecipeConditionNode[] children)
        {
            var result = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All };
            result.Children.AddRange(children); return result;
        }
        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Not(string id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child)
        {
            var result = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not };
            result.Children.Add(child); return result;
        }
        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode State(string id,
            CharacterTestRuntimeAiPlannerTargetStateMask required, CharacterTestRuntimeAiPlannerTargetStateMask forbidden = 0)
            => Predicate(new CharacterTestRuntimeAiLabCardCondition {ConditionId=id,Fact=CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                AllowedTargetPhases=(uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive,
                RequiredTargetFlags=(uint)required,ForbiddenTargetFlags=(uint)forbidden});
        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Numeric(string id, CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabCardConditionOperator op, float value, float maximum = 0)
            => Predicate(new CharacterTestRuntimeAiLabCardCondition {ConditionId=id,Fact=fact,Operator=op,Value=value,MaxValue=maximum});
        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Predicate(CharacterTestRuntimeAiLabCardCondition condition)
            => new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId=condition.ConditionId,
                Kind=CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,Predicate=condition };
    }
}
