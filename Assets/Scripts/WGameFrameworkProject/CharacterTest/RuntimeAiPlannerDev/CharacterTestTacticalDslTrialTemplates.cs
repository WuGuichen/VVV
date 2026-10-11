using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalDslTrialTemplates
    {
        internal static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode LowHealth(float ratio = 0.3f)
            => new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "health.self",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                Children = { new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "health.low",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "health.value",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, Value = ratio } } } };

        internal static CharacterTestTacticalDslSource Create()
        {
            var attack = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            attack.ActivationConditionRoot = null;
            var face = CharacterTestTacticalAuthoringTemplates.CreateBlank("继续转向");
            face.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "face-tail",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(12),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            var retreat = CharacterTestTacticalAuthoringTemplates.CreateBlank("后撤");
            retreat.Schema = CharacterTestTacticalAuthoringSource.EventSetTrialSchema; retreat.Version = 1;
            retreat.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "retreat",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(30),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "RetreatFromTarget", Target = "currentCombatTarget" } });
            return new CharacterTestTacticalDslSource { CardId = "trial.assault", Name = "进攻与后撤",
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.attack", Source = attack },
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.face", Source = face },
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.retreat", Source = retreat } },
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "assault", Kind = "Sequence", EndCondition = LowHealth(),
                        Children = new List<CharacterTestTacticalDslNode> { Reference("attack", "shared.attack"), Reference("tail", "shared.face") } },
                    new CharacterTestTacticalDslNode { NodeId = "retreat-entry", Kind = "If",
                        Condition = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack().ActivationConditionRoot,
                        Children = new List<CharacterTestTacticalDslNode> { Reference("retreat", "shared.retreat") } },
                    new CharacterTestTacticalDslNode { NodeId = "exit", Kind = "Exit" } } } };
        }

        internal static CharacterTestTacticalDslNode Reference(string id, string definition)
            => new CharacterTestTacticalDslNode { NodeId = id, Kind = "Reference", DefinitionId = definition };
    }
}
