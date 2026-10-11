using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Relays fully validated Gameplay component command results into Trigger presentation events.</summary>
    public sealed class GameplayTriggerGameplayCommandResultRelay :
        IGameplayComponentCommandResultSink
    {
        private readonly GameplayTriggerRuntimeEventSource _eventSource;
        private readonly int _expectedSourceId;

        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public GameplayTriggerGameplayCommandResultRelay(
            GameplayTriggerRuntimeEventSource eventSource,
            int expectedSourceId)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            if (expectedSourceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSourceId));
            _expectedSourceId = expectedSourceId;
        }

        /// <remarks>NoAlloc, no-throw and no-retry. Invalid input is ignored before presentation capacity is touched.</remarks>
        public void Publish(in GameplayComponentCommandResult result)
        {
            if (!TryValidate(
                    in result,
                    out long activationId,
                    out int actionIndex,
                    out GameplayTriggerActionKindId actionKindId,
                    out GameplayTriggerTargetRef target,
                    out GameplayTriggerRuntimeEventStatus status))
            {
                return;
            }

            _eventSource.TryPublishActionResult(
                result.Frame,
                activationId,
                actionIndex,
                actionKindId,
                target,
                status,
                (int)result.ReasonCode,
                result.ResultPayload0,
                result.ResultPayload1,
                result.ResultPayload2);
        }

        private bool TryValidate(
            in GameplayComponentCommandResult result,
            out long activationId,
            out int actionIndex,
            out GameplayTriggerActionKindId actionKindId,
            out GameplayTriggerTargetRef target,
            out GameplayTriggerRuntimeEventStatus status)
        {
            activationId = 0L;
            actionIndex = 0;
            actionKindId = default;
            target = default;
            status = default;
            RuntimeCommandEnvelope envelope = result.Envelope;
            if (result.SourceId != _expectedSourceId ||
                result.CommandSequence < 0L ||
                !result.TraceIdIsEmpty ||
                envelope.TypeId != GameplayTriggerGameplayCommandContracts.EnvelopeTypeId ||
                envelope.SchemaVersion != GameplayTriggerGameplayCommandContracts.EnvelopeSchemaVersion ||
                envelope.Provenance != RuntimeCommandProvenanceKind.Derived ||
                envelope.Data0 <= 0L ||
                envelope.Data1 < 0L || envelope.Data1 > int.MaxValue ||
                envelope.Data2 <= 0L || envelope.Data2 > int.MaxValue ||
                envelope.Data3 <= 0L ||
                envelope.Data4 < 0L || envelope.Data4 == long.MaxValue ||
                envelope.Data5 != envelope.Data4 + 1L ||
                envelope.Data5 > result.Frame.Value ||
                envelope.Data6 != 0L || envelope.Data7 != 0L)
            {
                return false;
            }

            actionKindId = new GameplayTriggerActionKindId((int)envelope.Data2);
            if (!GameplayTriggerGameplayCommandContracts.IsSupportedActionKind(actionKindId) ||
                result.CommandId != GameplayTriggerGameplayCommandContracts.ResolveCommandId(actionKindId) ||
                !TryValidateCommandAndTarget(in result, actionKindId, out target) ||
                !TryValidateTerminalResult(in result, actionKindId, out status))
            {
                actionKindId = default;
                target = default;
                status = default;
                return false;
            }

            activationId = envelope.Data0;
            actionIndex = (int)envelope.Data1;
            return true;
        }

        private static bool TryValidateCommandAndTarget(
            in GameplayComponentCommandResult result,
            GameplayTriggerActionKindId actionKindId,
            out GameplayTriggerTargetRef target)
        {
            target = default;
            if (result.TargetId <= 0)
                return false;

            int generation;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
            {
                if (result.CommandPayload0 != result.TargetId ||
                    result.CommandPayload1 <= 0 ||
                    result.CommandPayload2 != 0)
                    return false;
                generation = result.CommandPayload1;
            }
            else if (actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                     actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
            {
                if (result.CommandPayload0 <= 0 || result.CommandPayload1 <= 0)
                    return false;
                generation = result.CommandPayload0;
            }
            else
            {
                if (result.CommandPayload0 <= 0 ||
                    result.CommandPayload1 <= 0 ||
                    result.CommandPayload2 != 0)
                    return false;
                generation = result.CommandPayload0;
            }

            target = new GameplayTriggerTargetRef(
                GameplayTriggerTargetRef.GameplayEntityDomainId,
                result.TargetId,
                generation);
            return true;
        }

        private static bool TryValidateTerminalResult(
            in GameplayComponentCommandResult result,
            GameplayTriggerActionKindId actionKindId,
            out GameplayTriggerRuntimeEventStatus status)
        {
            if (result.Status == GameplayComponentCommandResultStatus.Succeeded)
            {
                status = GameplayTriggerRuntimeEventStatus.Succeeded;
                if (actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
                {
                    return result.ReasonCode == GameplayComponentCommandResultReasonCode.ComponentEntityDestroyed &&
                           result.ResultPayload0 == 0L &&
                           result.ResultPayload1 == 0L &&
                           result.ResultPayload2 == 0L;
                }
                if (actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute)
                    return ValidateAttributeSuccess(in result, GameplayComponentCommandResultReasonCode.ComponentAttributeSet);
                if (actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
                    return ValidateAttributeSuccess(in result, GameplayComponentCommandResultReasonCode.ComponentAttributeAdded);
                return result.ReasonCode == GameplayComponentCommandResultReasonCode.ComponentAbilityCastSucceeded &&
                       result.ResultPayload0 == result.CommandPayload1 &&
                       result.ResultPayload1 == 0L &&
                       result.ResultPayload2 == 0L;
            }

            if (result.Status != GameplayComponentCommandResultStatus.Rejected)
            {
                status = default;
                return false;
            }

            status = GameplayTriggerRuntimeEventStatus.Rejected;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
                return ValidateDestroyRejection(in result);
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
                return ValidateAttributeRejection(in result);
            return ValidateAbilityRejection(in result);
        }

        private static bool ValidateAttributeSuccess(
            in GameplayComponentCommandResult result,
            GameplayComponentCommandResultReasonCode expectedReason)
        {
            return result.ReasonCode == expectedReason &&
                   result.ResultPayload0 >= int.MinValue &&
                   result.ResultPayload0 <= int.MaxValue &&
                   result.ResultPayload1 >= int.MinValue &&
                   result.ResultPayload1 <= int.MaxValue &&
                   result.ResultPayload2 == unchecked(
                       (int)result.ResultPayload1 - (int)result.ResultPayload0);
        }

        private static bool ValidateDestroyRejection(
            in GameplayComponentCommandResult result)
        {
            if (!IsCommonRejection(result.ReasonCode))
                return false;
            return result.ResultPayload0 == result.TargetId &&
                   result.ResultPayload1 == result.CommandPayload1 &&
                   result.ResultPayload2 == 0L;
        }

        private static bool ValidateAttributeRejection(
            in GameplayComponentCommandResult result)
        {
            if (!IsCommonRejection(result.ReasonCode) &&
                (result.ReasonCode < GameplayComponentCommandResultReasonCode.InvalidAttributeId ||
                 result.ReasonCode > GameplayComponentCommandResultReasonCode.AttributeUpdateFailed))
                return false;
            return result.ResultPayload0 == result.CommandPayload1 &&
                   result.ResultPayload1 == 0L &&
                   result.ResultPayload2 == 0L;
        }

        private static bool ValidateAbilityRejection(
            in GameplayComponentCommandResult result)
        {
            long abilityId = result.CommandPayload1;
            if (IsCommonRejection(result.ReasonCode) ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.InvalidComponentAbilityCommandPayload ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.MissingComponentAbility ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.MissingAbilityAttributeSet ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.MissingAbilityTarget ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.ComponentAbilityEffectFailed)
            {
                return result.ResultPayload0 == abilityId &&
                       result.ResultPayload1 == 0L &&
                       result.ResultPayload2 == 0L;
            }

            if (result.ReasonCode == GameplayComponentCommandResultReasonCode.ComponentAbilityOnCooldown)
            {
                return result.ResultPayload0 == abilityId &&
                       result.ResultPayload1 == abilityId &&
                       result.ResultPayload2 > 0L;
            }
            if (result.ReasonCode == GameplayComponentCommandResultReasonCode.ComponentAbilityInsufficientCost ||
                result.ReasonCode == GameplayComponentCommandResultReasonCode.InvalidComponentAbilityRule)
            {
                return result.ResultPayload0 == abilityId &&
                       result.ResultPayload1 >= 0L &&
                       result.ResultPayload1 <= int.MaxValue &&
                       result.ResultPayload2 == 0L;
            }
            if (result.ReasonCode == GameplayComponentCommandResultReasonCode.InvalidComponentAbilityResult)
            {
                long rawFailureCode = result.ResultPayload1;
                return result.ResultPayload0 == abilityId &&
                       (rawFailureCode <= 0L ||
                        rawFailureCode > (int)GameplayComponentAbilityFailureCode.InvalidAbilityRule) &&
                       result.ResultPayload2 == 0L;
            }
            return false;
        }

        private static bool IsCommonRejection(
            GameplayComponentCommandResultReasonCode reasonCode)
        {
            return reasonCode >= GameplayComponentCommandResultReasonCode.MissingComponentWorld &&
                   reasonCode <= GameplayComponentCommandResultReasonCode.MissingComponentEntity;
        }
    }
}
