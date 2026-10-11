using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Story.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Per-StageRun authority barrier and direct Story result sink. Observer delivery is presentation only.</summary>
    /// <remarks>Construct with the same Runtime/plan as Vitals, and install as the original Story module's result sink.
    /// An optional downstream sink preserves the existing presentation relay. No callbacks tick or drain a module.</remarks>
    internal sealed class TacticalStageEndTriggerBridge : IStoryRuntimeCommandResultSink, IDisposable
    {
        private readonly GameplayTriggerRuntime _runtime;
        private readonly CharacterTestGameplayTriggerBattleVitalsCatalog _catalog;
        private readonly TacticalStageFlowCoordinator _stage;
        private readonly IStoryRuntimeCommandResultSink _presentation;
        private readonly int _stageDefinitionId;
        private long _activationId, _sequence;
        private int _targetId, _triggerId;
        private bool _disposed;
        internal CharacterTestGameplayTriggerBattleVitalsSystem Vitals { get; }
        internal GameplayTriggerEvaluationSystem Evaluation { get; }
        internal StoryRuntimeCommandResult LastStoryResult { get; private set; }
        internal bool HasStoryResult { get; private set; }
        internal bool CanLeaveSession
        {
            get
            {
                if (_activationId != 0 && !HasStoryResult) return false;
                for (int i = 0; i < _runtime.ReliableOutboxRecordCount; i++)
                {
                    var record = _runtime.GetReliableOutboxRecord(i);
                    if (record.ActionKindId == GameplayTriggerStoryCommandActionKinds.RaiseTrigger &&
                        record.ResolvedTarget.DomainId == GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId &&
                        OwnsSource(record.ResolvedTarget.Value)) return false;
                }
                return true;
            }
        }

        internal TacticalStageEndTriggerBridge(GameplayTriggerRuntime runtime,
            CharacterTestGameplayTriggerBattleVitalsCatalog catalog, GameplayComponentWorld world,
            TacticalStageFlowCoordinator stage, int stageDefinitionId, IStoryRuntimeCommandResultSink presentation = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _stageDefinitionId = stageDefinitionId;
            _presentation = presentation;
            Vitals = new CharacterTestGameplayTriggerBattleVitalsSystem(catalog, runtime, world, stage, stageDefinitionId);
            Evaluation = new GameplayTriggerEvaluationSystem(runtime);
            stage.BindEndTriggers(this);
        }

        /// <remarks>NoAlloc. Invoked by Coordinator at the completed world boundary, before normal settling.</remarks>
        internal void ObserveCompletedTick(long frame)
        {
            if (_disposed || _stage.IsReleased || _stage.IsLeavingSession || !_stage.TryGetRoster(out var player, out var opponent)) return;
            for (int i = 0; i < _runtime.ReliableOutboxRecordCount; i++)
            {
                var record = _runtime.GetReliableOutboxRecord(i);
                if (record.ActionKindId != GameplayTriggerStoryCommandActionKinds.RaiseTrigger ||
                    !OwnsSource(record.ResolvedTarget.Value) || record.Owner != player || record.Subject != player ||
                    record.Instigator != opponent || record.OccurrenceSignalId != CharacterTestGameplayTriggerBattleVitalsCatalog.BattleVitalsSignalId ||
                    record.OccurrencePayload2 != _stageDefinitionId) continue;
                if (_activationId == 0)
                {
                    _activationId = record.ActivationId; _sequence = record.Sequence;
                    _targetId = (int)record.ResolvedTarget.Value; _triggerId = (int)record.ActionPayload0;
                    _stage.BeginStoryTransfer(frame);
                }
                else if (_activationId != record.ActivationId)
                    _stage.ReportFault(TacticalStageRejection.CriticalTriggerRejected);
            }
            // Successful activations are durable intents. Never substitute the observer's ActivationAccepted event.
            for (int i = 0; i < _runtime.IntentCount; i++)
            {
                var intent = _runtime.GetIntent(i);
                if (intent.Frame.Value == frame && OwnsSource(intent.Target.Value) &&
                    intent.Instruction.ActionKindId == GameplayTriggerStoryCommandActionKinds.RaiseTrigger && _activationId == 0)
                    _stage.ReportFault(TacticalStageRejection.CriticalTriggerRejected);
            }
            if (HasStoryResult && LastStoryResult.Status == StoryRuntimeCommandResultStatus.Rejected)
            { _stage.ReportFault(TacticalStageRejection.StoryTransferRejected); return; }
            if (_activationId != 0) return;
            if (Vitals.Fault != TacticalStageRejection.None) { _stage.ReportFault(Vitals.Fault); return; }
            if (Evaluation.LastEvaluatedFrame.Value != frame || !Evaluation.LastResult.Success ||
                (Vitals.Armed && Vitals.LastSampleFrame != frame))
            { _stage.ReportFault(TacticalStageRejection.CriticalTriggerRejected); return; }
            for (int i = 0; i < _runtime.DiagnosticCount; i++)
            {
                var diagnostic = _runtime.GetDiagnostic(i);
                if (diagnostic.Code == GameplayTriggerErrorCode.ConditionRejected) continue;
                if (diagnostic.Frame.Value == frame && (OwnsSource(diagnostic.SourceBindingId.Value) || !diagnostic.SourceBindingId.IsValid))
                { _stage.ReportFault(TacticalStageRejection.CriticalTriggerRejected); return; }
            }
        }

        public void Publish(in StoryRuntimeCommandResult result)
        {
            _presentation?.Publish(in result);
            if (_disposed || _stage.IsReleased || _activationId == 0 || HasStoryResult ||
                result.SourceId != StoryRuntimeCommandSources.GameplayBridge || result.CommandId != StoryRuntimeCommandIds.RaiseTrigger ||
                result.TargetId != _targetId || result.CommandPayload0 != _triggerId ||
                result.Envelope.TypeId != GameplayTriggerStoryCommandContracts.EnvelopeTypeId ||
                result.Envelope.SchemaVersion != GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion ||
                result.Envelope.Data0 != _activationId || result.Envelope.Data1 != 0 || result.Envelope.Data3 != _sequence ||
                (result.Status != StoryRuntimeCommandResultStatus.Succeeded && result.Status != StoryRuntimeCommandResultStatus.Rejected)) return;
            LastStoryResult = result;
            HasStoryResult = true;
        }

        internal bool AcceptsCompletion(in TacticalStoryStepToken step)
            => !_disposed && HasStoryResult && LastStoryResult.Status == StoryRuntimeCommandResultStatus.Succeeded &&
                step.StageRunId == _stage.StageRunId && step.GraphId == LastStoryResult.ResultPayload0 &&
                step.BeatInstanceId == LastStoryResult.ResultPayload2 &&
                step.ActionId == (_triggerId == CharacterTestStoryIds.Triggers.TacticalBattleVictory
                    ? CharacterTestStoryIds.Actions.TacticalBattleVictory : CharacterTestStoryIds.Actions.TacticalBattleDefeat);

        private bool OwnsSource(long value)
        {
            for (int i = 0; i < _catalog.Count; i++) if (_catalog.Get(i).SourceBindingId.Value == value) return true;
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Vitals.SetEnabled(false);
            _stage.ReleaseControl();
        }
    }
}
