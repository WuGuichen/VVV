using System;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Gameplay.Triggers.StoryBridge
{
    /// <summary>Relays fully validated Story.RaiseTrigger results into Trigger presentation events.</summary>
    public sealed class GameplayTriggerStoryCommandResultRelay :
        IStoryRuntimeCommandResultSink
    {
        private readonly GameplayTriggerRuntimeEventSource _eventSource;

        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public GameplayTriggerStoryCommandResultRelay(
            GameplayTriggerRuntimeEventSource eventSource)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
        }

        /// <remarks>NoAlloc, no-throw and no-retry. Invalid input is ignored before presentation capacity is touched.</remarks>
        public void Publish(in StoryRuntimeCommandResult result)
        {
            if (!TryValidate(
                    in result,
                    out long activationId,
                    out int actionIndex,
                    out GameplayTriggerTargetRef target,
                    out GameplayTriggerRuntimeEventStatus status))
            {
                return;
            }

            _eventSource.TryPublishActionResult(
                result.Frame,
                activationId,
                actionIndex,
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                target,
                status,
                (int)result.DirectorResultCode,
                result.ResultPayload0,
                result.ResultPayload1,
                result.ResultPayload2);
        }

        private static bool TryValidate(
            in StoryRuntimeCommandResult result,
            out long activationId,
            out int actionIndex,
            out GameplayTriggerTargetRef target,
            out GameplayTriggerRuntimeEventStatus status)
        {
            activationId = 0L;
            actionIndex = 0;
            target = default;
            status = default;
            RuntimeCommandEnvelope envelope = result.Envelope;
            if (result.SourceId != StoryRuntimeCommandSources.GameplayBridge ||
                result.CommandId != StoryRuntimeCommandIds.RaiseTrigger ||
                result.TargetId <= 0 ||
                result.CommandPayload0 <= 0 ||
                result.CommandSequence < 0L ||
                !result.TraceIdIsEmpty ||
                envelope.TypeId != GameplayTriggerStoryCommandContracts.EnvelopeTypeId ||
                envelope.SchemaVersion != GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion ||
                envelope.Provenance != RuntimeCommandProvenanceKind.Derived ||
                envelope.Data0 <= 0L ||
                envelope.Data1 < 0L || envelope.Data1 > int.MaxValue ||
                envelope.Data2 != GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue ||
                envelope.Data3 <= 0L ||
                envelope.Data4 < 0L || envelope.Data4 == long.MaxValue ||
                envelope.Data5 != envelope.Data4 + 1L ||
                envelope.Data5 > result.Frame.Value ||
                envelope.Data6 != 0L || envelope.Data7 != 0L ||
                !TryValidateTerminalResult(in result, out status))
            {
                return false;
            }

            activationId = envelope.Data0;
            actionIndex = (int)envelope.Data1;
            target = new GameplayTriggerTargetRef(
                GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                result.TargetId,
                0);
            return true;
        }

        private static bool TryValidateTerminalResult(
            in StoryRuntimeCommandResult result,
            out GameplayTriggerRuntimeEventStatus status)
        {
            if (result.Status == StoryRuntimeCommandResultStatus.Succeeded)
            {
                status = GameplayTriggerRuntimeEventStatus.Succeeded;
                return result.DirectorResultCode == StoryDirectorResultCode.Success &&
                       result.ResultPayload0 > 0L &&
                       result.ResultPayload0 <= int.MaxValue &&
                       result.ResultPayload1 > 0L &&
                       result.ResultPayload1 <= int.MaxValue &&
                       result.ResultPayload2 > 0L &&
                       result.ResultPayload2 <= int.MaxValue;
            }

            if (result.Status != StoryRuntimeCommandResultStatus.Rejected ||
                (result.DirectorResultCode != StoryDirectorResultCode.TriggerNotFound &&
                 result.DirectorResultCode != StoryDirectorResultCode.DirectorGuardExceeded))
            {
                status = default;
                return false;
            }

            status = GameplayTriggerRuntimeEventStatus.Rejected;
            return result.ResultPayload0 == result.CommandPayload0 &&
                   result.ResultPayload1 == 0L &&
                   result.ResultPayload2 == 0L;
        }
    }
}
