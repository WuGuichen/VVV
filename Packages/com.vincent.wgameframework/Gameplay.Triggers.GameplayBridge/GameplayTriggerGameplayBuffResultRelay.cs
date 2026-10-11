using System;
using MxFramework.Gameplay;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Relays target-owned numeric Buff terminal results into Trigger presentation events.</summary>
    public sealed class GameplayTriggerGameplayBuffResultRelay : IGameplayBuffApplicationResultSink
    {
        private readonly GameplayTriggerRuntimeEventSource _eventSource;

        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerGameplayBuffResultRelay(
            GameplayTriggerRuntimeEventSource eventSource)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
        }

        /// <remarks>NoAlloc, no-throw and no-retry. Event-source overflow remains presentation-only.</remarks>
        public void Publish(in GameplayBuffApplicationResult result)
        {
            GameplayBuffRequestCorrelation correlation = result.Correlation;
            if (!correlation.IsValid ||
                !result.TargetEntityId.IsValid ||
                !TryResolveActionKind(
                    result.Kind,
                    correlation.OperationKindId,
                    out GameplayTriggerActionKindId actionKindId) ||
                !TryResolveStatus(
                    result.Status,
                    result.ReasonCode,
                    out GameplayTriggerRuntimeEventStatus status,
                    out long payload0,
                    out long payload1,
                    out long payload2,
                    in result))
            {
                return;
            }

            GameplayTriggerTargetRef target =
                GameplayTriggerTargetRef.FromGameplayEntity(result.TargetEntityId);
            _eventSource.TryPublishActionResult(
                result.Frame,
                correlation.RequestId,
                correlation.OperationIndex,
                actionKindId,
                target,
                status,
                (int)result.ReasonCode,
                payload0,
                payload1,
                payload2);
        }

        private static bool TryResolveActionKind(
            GameplayBuffApplicationRequestKind requestKind,
            int operationKindId,
            out GameplayTriggerActionKindId actionKindId)
        {
            if (requestKind == GameplayBuffApplicationRequestKind.Apply &&
                operationKindId == GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuffValue)
            {
                actionKindId = GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff;
                return true;
            }
            if (requestKind == GameplayBuffApplicationRequestKind.Remove &&
                operationKindId == GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuffValue)
            {
                actionKindId = GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff;
                return true;
            }

            actionKindId = default;
            return false;
        }

        private static bool TryResolveStatus(
            GameplayBuffApplicationResultStatus resultStatus,
            GameplayBuffApplicationResultReasonCode reasonCode,
            out GameplayTriggerRuntimeEventStatus status,
            out long payload0,
            out long payload1,
            out long payload2,
            in GameplayBuffApplicationResult result)
        {
            payload0 = 0L;
            payload1 = 0L;
            payload2 = 0L;
            if (resultStatus == GameplayBuffApplicationResultStatus.Succeeded)
            {
                status = GameplayTriggerRuntimeEventStatus.Succeeded;
                switch (reasonCode)
                {
                    case GameplayBuffApplicationResultReasonCode.Applied:
                    case GameplayBuffApplicationResultReasonCode.Refreshed:
                        payload0 = result.OldStackCount;
                        payload1 = result.NewStackCount;
                        payload2 = result.NewEndFrame;
                        return true;
                    case GameplayBuffApplicationResultReasonCode.Removed:
                        payload0 = result.OldStackCount;
                        return true;
                    case GameplayBuffApplicationResultReasonCode.RemoveNoOp:
                        return true;
                    default:
                        return false;
                }
            }

            if (resultStatus == GameplayBuffApplicationResultStatus.Rejected)
            {
                status = GameplayTriggerRuntimeEventStatus.Rejected;
                return reasonCode == GameplayBuffApplicationResultReasonCode.MissingComponentWorld ||
                       reasonCode == GameplayBuffApplicationResultReasonCode.MissingComponentEntity ||
                       reasonCode == GameplayBuffApplicationResultReasonCode.MissingBuffDefinition ||
                       reasonCode == GameplayBuffApplicationResultReasonCode.DurationOverflow;
            }

            status = default;
            return false;
        }
    }
}
