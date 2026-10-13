using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        internal sealed partial class RuntimeBridge : ICharacterTestFixedBehaviorActionContext
        {
            internal TacticalFixedCardPaymentCommit FixedPayment { get; set; }
            internal void PublishDoubleEdgeTerminal(RuntimeFrame frame)
            {
                if (FixedPayment?.DoubleEdgeState == null || _executionEvaluator == null
                    || (_executionEvaluator.State != CharacterTestRuntimeAiLabLogicRuntimeState.Completed
                        && _executionEvaluator.State != CharacterTestRuntimeAiLabLogicRuntimeState.Failed)) return;
                FixedPayment.DoubleEdgeState.CompleteHost(FixedPayment.Record.DoubleEdgeKey,
                    _executionEvaluator.NaturallyCompleted && _entity != null && _entity.CombatState.IsAlive,
                    frame.Value, _entity?.Transform.XMillimeters ?? 0,
                    _entity?.Transform.YMillimeters ?? 0, _entity?.Transform.ZMillimeters ?? 0);
            }
            public bool TryGetFixedActionPayment(RuntimeFrame frame, int entity, int resource,
                out TacticalFixedCardPaymentCommit payment, out CharacterTestRuntimeAiPlannerFactState facts)
            {
                payment = FixedPayment; facts = null;
                return payment != null && payment.IsPaid && payment.ProgramIdentity == _program.Execution.Identity
                    && _orchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                    && TryGetActionFacts(frame, entity, resource, out facts) && CanCommitAction(resource);
            }
        }
    }
}
