using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestLevelGameplayTriggerStartupState
    {
        Disabled = 0,
        Ready = 1,
        FailedClosed = 2
    }

    internal enum CharacterTestLevelGameplayTriggerStartupFailure
    {
        None = 0,
        CacheSchemaMismatch = 1,
        CacheSourceHashMismatch = 2,
        CachePayloadInvalid = 3,
        CompileRejected = 4,
        ActionBootstrapRejected = 5,
        SensorCatalogRejected = 6,
        SensorCatalogHashMismatch = 7,
        CombatPhysicsUnavailable = 8,
        SensorCompositionRejected = 9,
        CompositionRejected = 10,
        AttributeThresholdCatalogRejected = 11,
        AttributeThresholdCatalogHashMismatch = 12,
        SourceCatalogHashMismatch = 13
    }

    internal readonly struct CharacterTestLevelGameplayTriggerRuntimeSnapshot
    {
        internal CharacterTestLevelGameplayTriggerRuntimeSnapshot(
            CharacterTestLevelGameplayTriggerStartupState startupState,
            CharacterTestLevelGameplayTriggerStartupFailure startupFailure,
            string startupMessage,
            long triggerCatalogHash,
            long sensorCatalogHash,
            long attributeThresholdCatalogHash,
            long combinedCatalogHash,
            CharacterTestGameplayTriggerSensorCacheRow cacheRow,
            CharacterTestGameplayTriggerAttributeThresholdCacheRow
                attributeThresholdCacheRow,
            bool hasSensorResult,
            GameplayTriggerCombatSensorBindingTickResult sensorResult,
            GameplayTriggerCombatSensorTickSummary sensorSummary,
            bool hasAttributeThresholdResult,
            CharacterTestGameplayTriggerAttributeThresholdTickResult
                attributeThresholdResult,
            bool hasOccurrence,
            GameplayTriggerRuntimeEvent occurrence,
            bool hasActionDispatch,
            GameplayTriggerRuntimeEvent actionDispatch,
            bool hasActionResult,
            GameplayTriggerRuntimeEvent actionResult,
            long droppedEventCount)
        {
            StartupState = startupState;
            StartupFailure = startupFailure;
            StartupMessage = startupMessage ?? string.Empty;
            TriggerCatalogHash = triggerCatalogHash;
            SensorCatalogHash = sensorCatalogHash;
            AttributeThresholdCatalogHash =
                attributeThresholdCatalogHash;
            CombinedCatalogHash = combinedCatalogHash;
            CacheRow = cacheRow;
            AttributeThresholdCacheRow = attributeThresholdCacheRow;
            HasSensorResult = hasSensorResult;
            SensorResult = sensorResult;
            SensorSummary = sensorSummary;
            HasAttributeThresholdResult = hasAttributeThresholdResult;
            AttributeThresholdResult = attributeThresholdResult;
            HasOccurrence = hasOccurrence;
            Occurrence = occurrence;
            HasActionDispatch = hasActionDispatch;
            ActionDispatch = actionDispatch;
            HasActionResult = hasActionResult;
            ActionResult = actionResult;
            DroppedEventCount = droppedEventCount;
        }

        internal CharacterTestLevelGameplayTriggerStartupState StartupState { get; }
        internal CharacterTestLevelGameplayTriggerStartupFailure StartupFailure { get; }
        internal string StartupMessage { get; }
        internal long TriggerCatalogHash { get; }
        internal long SensorCatalogHash { get; }
        internal long AttributeThresholdCatalogHash { get; }
        internal long CombinedCatalogHash { get; }
        internal CharacterTestGameplayTriggerSensorCacheRow CacheRow { get; }
        internal CharacterTestGameplayTriggerAttributeThresholdCacheRow
            AttributeThresholdCacheRow { get; }
        internal bool HasSensorResult { get; }
        internal GameplayTriggerCombatSensorBindingTickResult SensorResult { get; }
        internal GameplayTriggerCombatSensorTickSummary SensorSummary { get; }
        internal bool HasAttributeThresholdResult { get; }
        internal CharacterTestGameplayTriggerAttributeThresholdTickResult
            AttributeThresholdResult { get; }
        internal bool HasOccurrence { get; }
        internal GameplayTriggerRuntimeEvent Occurrence { get; }
        internal bool HasActionDispatch { get; }
        internal GameplayTriggerRuntimeEvent ActionDispatch { get; }
        internal bool HasActionResult { get; }
        internal GameplayTriggerRuntimeEvent ActionResult { get; }
        internal long DroppedEventCount { get; }
    }

    internal sealed class CharacterTestLevelGameplayTriggerRuntimeStatus
    {
        private readonly CharacterTestGameplayTriggerSceneCache _cache;
        private readonly SourceState[] _sourceStates;
        private readonly ActivationSource[] _activationSources;
        private GameplayTriggerCombatSensorSystem _sensor;
        private CharacterTestGameplayTriggerAttributeThresholdSystem
            _attributeThreshold;
        private GameplayTriggerRuntimeEventObserverBuffer _eventObserver;

        private CharacterTestLevelGameplayTriggerRuntimeStatus(
            CharacterTestGameplayTriggerSceneCache cache,
            CharacterTestLevelGameplayTriggerStartupState startupState,
            CharacterTestLevelGameplayTriggerStartupFailure startupFailure,
            string startupMessage,
            int activationCapacity)
        {
            _cache = cache;
            StartupState = startupState;
            StartupFailure = startupFailure;
            StartupMessage = startupMessage ?? string.Empty;
            int sensorCount = cache?.SensorRowCount ?? 0;
            int attributeThresholdCount =
                cache?.AttributeThresholdRowCount ?? 0;
            int sourceCount = sensorCount + attributeThresholdCount;
            _sourceStates = new SourceState[sourceCount];
            for (int i = 0; i < sensorCount; i++)
            {
                CharacterTestGameplayTriggerSensorCacheRow row =
                    cache.SensorRows[i];
                _sourceStates[i].SourceBindingId =
                    row?.SourceBindingId ?? 0;
            }
            for (int i = 0; i < attributeThresholdCount; i++)
            {
                CharacterTestGameplayTriggerAttributeThresholdCacheRow row =
                    cache.AttributeThresholdRows[i];
                _sourceStates[sensorCount + i].SourceBindingId =
                    row?.SourceBindingId ?? 0;
            }
            _activationSources =
                new ActivationSource[Math.Max(1, activationCapacity)];
        }

        internal CharacterTestLevelGameplayTriggerStartupState StartupState { get; private set; }
        internal CharacterTestLevelGameplayTriggerStartupFailure StartupFailure { get; private set; }
        internal string StartupMessage { get; private set; }
        internal long TriggerCatalogHash { get; private set; }
        internal long SensorCatalogHash { get; private set; }
        internal long AttributeThresholdCatalogHash { get; private set; }
        internal long CombinedCatalogHash { get; private set; }

        internal static CharacterTestLevelGameplayTriggerRuntimeStatus Disabled(
            CharacterTestGameplayTriggerSceneCache cache)
        {
            return new CharacterTestLevelGameplayTriggerRuntimeStatus(
                cache,
                CharacterTestLevelGameplayTriggerStartupState.Disabled,
                CharacterTestLevelGameplayTriggerStartupFailure.None,
                string.Empty,
                activationCapacity: 1);
        }

        internal static CharacterTestLevelGameplayTriggerRuntimeStatus Failed(
            CharacterTestGameplayTriggerSceneCache cache,
            CharacterTestLevelGameplayTriggerStartupFailure failure,
            string message)
        {
            return new CharacterTestLevelGameplayTriggerRuntimeStatus(
                cache,
                CharacterTestLevelGameplayTriggerStartupState.FailedClosed,
                failure,
                message,
                activationCapacity: 1);
        }

        internal static CharacterTestLevelGameplayTriggerRuntimeStatus Ready(
            CharacterTestGameplayTriggerSceneCache cache,
            GameplayTriggerCombatSensorSystem sensor,
            CharacterTestGameplayTriggerAttributeThresholdSystem
                attributeThreshold,
            GameplayTriggerRuntimeEventObserverBuffer observer,
            int activationCapacity,
            long triggerCatalogHash,
            long sensorCatalogHash,
            long attributeThresholdCatalogHash,
            long combinedCatalogHash)
        {
            var status = new CharacterTestLevelGameplayTriggerRuntimeStatus(
                cache,
                CharacterTestLevelGameplayTriggerStartupState.Ready,
                CharacterTestLevelGameplayTriggerStartupFailure.None,
                string.Empty,
                activationCapacity);
            status._sensor = sensor;
            status._attributeThreshold = attributeThreshold;
            status._eventObserver = observer;
            status.TriggerCatalogHash = triggerCatalogHash;
            status.SensorCatalogHash = sensorCatalogHash;
            status.AttributeThresholdCatalogHash =
                attributeThresholdCatalogHash;
            status.CombinedCatalogHash = combinedCatalogHash;
            return status;
        }

        /// <remarks>NoAlloc.</remarks>
        internal bool TryCapture(
            int sourceBindingId,
            out CharacterTestLevelGameplayTriggerRuntimeSnapshot snapshot)
        {
            CharacterTestGameplayTriggerSensorCacheRow cacheRow = null;
            _cache?.TryGetSensorRow(sourceBindingId, out cacheRow);
            CharacterTestGameplayTriggerAttributeThresholdCacheRow
                attributeThresholdCacheRow = null;
            _cache?.TryGetAttributeThresholdRow(
                sourceBindingId,
                out attributeThresholdCacheRow);
            int stateIndex = FindSourceState(sourceBindingId);
            SourceState state =
                stateIndex < 0 ? default : _sourceStates[stateIndex];

            bool hasSensorResult = TryGetSensorResult(
                sourceBindingId,
                out GameplayTriggerCombatSensorBindingTickResult sensorResult);
            bool hasAttributeThresholdResult =
                TryGetAttributeThresholdResult(
                    sourceBindingId,
                    out
                        CharacterTestGameplayTriggerAttributeThresholdTickResult
                        attributeThresholdResult);
            snapshot = new CharacterTestLevelGameplayTriggerRuntimeSnapshot(
                StartupState,
                StartupFailure,
                StartupMessage,
                TriggerCatalogHash,
                SensorCatalogHash,
                AttributeThresholdCatalogHash,
                CombinedCatalogHash,
                cacheRow,
                attributeThresholdCacheRow,
                hasSensorResult,
                sensorResult,
                _sensor?.LastTickSummary ?? default,
                hasAttributeThresholdResult,
                attributeThresholdResult,
                state.HasOccurrence,
                state.Occurrence,
                state.HasActionDispatch,
                state.ActionDispatch,
                state.HasActionResult,
                state.ActionResult,
                _eventObserver?.DroppedCount ?? 0L);
            return cacheRow != null
                || attributeThresholdCacheRow != null
                || StartupState
                != CharacterTestLevelGameplayTriggerStartupState.Disabled;
        }

        /// <remarks>NoAlloc.</remarks>
        internal void DrainEvents()
        {
            if (_eventObserver == null)
                return;

            while (_eventObserver.TryDequeue(
                       out GameplayTriggerRuntimeEvent evt))
            {
                RecordEvent(in evt);
            }
        }

        /// <remarks>NoAlloc. Called only after an exclusive authority restore.</remarks>
        internal void ResetAfterRestore()
        {
            _eventObserver?.Clear();
            Array.Clear(_sourceStates, 0, _sourceStates.Length);
            int sensorCount = _cache?.SensorRowCount ?? 0;
            for (int i = 0; i < sensorCount; i++)
            {
                _sourceStates[i].SourceBindingId =
                    _cache.SensorRows[i]?.SourceBindingId ?? 0;
            }
            int thresholdCount =
                _cache?.AttributeThresholdRowCount ?? 0;
            for (int i = 0; i < thresholdCount; i++)
            {
                _sourceStates[sensorCount + i].SourceBindingId =
                    _cache.AttributeThresholdRows[i]?.SourceBindingId ?? 0;
            }
            Array.Clear(
                _activationSources,
                0,
                _activationSources.Length);
            _sensor?.ResetSchedulingState();
            _attributeThreshold?.ResetDiagnostics();
        }

        private void RecordEvent(in GameplayTriggerRuntimeEvent evt)
        {
            int sourceBindingId = ResolveSourceBindingId(in evt);
            if (sourceBindingId <= 0)
                return;

            int stateIndex = FindSourceState(sourceBindingId);
            if (stateIndex < 0)
                return;

            SourceState state = _sourceStates[stateIndex];
            switch (evt.Kind)
            {
                case GameplayTriggerRuntimeEventKind.Occurrence:
                    state.Occurrence = evt;
                    state.HasOccurrence = true;
                    break;
                case GameplayTriggerRuntimeEventKind.ActionDispatched:
                    state.ActionDispatch = evt;
                    state.HasActionDispatch = true;
                    RememberActivationSource(
                        evt.ActivationId,
                        evt.ActionIndex,
                        sourceBindingId);
                    break;
                case GameplayTriggerRuntimeEventKind.ActionResult:
                case GameplayTriggerRuntimeEventKind.CommandResult:
                case GameplayTriggerRuntimeEventKind.InternalDeferredResult:
                case GameplayTriggerRuntimeEventKind.ReliableTransferResult:
                    state.ActionResult = evt;
                    state.HasActionResult = true;
                    break;
            }
            _sourceStates[stateIndex] = state;
        }

        private int ResolveSourceBindingId(
            in GameplayTriggerRuntimeEvent evt)
        {
            if (evt.SourceBindingId.IsValid)
                return evt.SourceBindingId.Value;
            if (evt.TargetSourceBindingId.IsValid)
                return evt.TargetSourceBindingId.Value;
            if (evt.ActivationId <= 0L)
                return 0;

            for (int i = 0; i < _activationSources.Length; i++)
            {
                ActivationSource correlation = _activationSources[i];
                if (correlation.ActivationId == evt.ActivationId
                    && correlation.ActionIndex == evt.ActionIndex)
                {
                    return correlation.SourceBindingId;
                }
            }
            return 0;
        }

        private void RememberActivationSource(
            long activationId,
            int actionIndex,
            int sourceBindingId)
        {
            if (activationId <= 0L)
                return;

            int replaceIndex = 0;
            long oldestActivationId = long.MaxValue;
            for (int i = 0; i < _activationSources.Length; i++)
            {
                ActivationSource existing = _activationSources[i];
                if (existing.ActivationId == activationId
                    && existing.ActionIndex == actionIndex)
                {
                    replaceIndex = i;
                    oldestActivationId = long.MinValue;
                    break;
                }
                if (existing.ActivationId == 0L)
                {
                    replaceIndex = i;
                    oldestActivationId = long.MinValue;
                    break;
                }
                if (existing.ActivationId < oldestActivationId)
                {
                    replaceIndex = i;
                    oldestActivationId = existing.ActivationId;
                }
            }

            _activationSources[replaceIndex] = new ActivationSource(
                activationId,
                actionIndex,
                sourceBindingId);
        }

        private bool TryGetSensorResult(
            int sourceBindingId,
            out GameplayTriggerCombatSensorBindingTickResult result)
        {
            if (_sensor != null)
            {
                for (int i = 0; i < _sensor.BindingResultCount; i++)
                {
                    GameplayTriggerCombatSensorBindingTickResult candidate =
                        _sensor.GetLastBindingResult(i);
                    if (candidate.SourceBindingId.Value == sourceBindingId)
                    {
                        result = candidate;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private bool TryGetAttributeThresholdResult(
            int sourceBindingId,
            out CharacterTestGameplayTriggerAttributeThresholdTickResult
                result)
        {
            if (_attributeThreshold != null)
            {
                for (int i = 0;
                     i < _attributeThreshold.BindingResultCount;
                     i++)
                {
                    CharacterTestGameplayTriggerAttributeThresholdTickResult
                        candidate =
                            _attributeThreshold.GetLastBindingResult(i);
                    if (candidate.SourceBindingId.Value == sourceBindingId)
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
            result = default;
            return false;
        }

        private int FindSourceState(int sourceBindingId)
        {
            for (int i = 0; i < _sourceStates.Length; i++)
            {
                if (_sourceStates[i].SourceBindingId == sourceBindingId)
                    return i;
            }
            return -1;
        }

        private struct SourceState
        {
            internal int SourceBindingId;
            internal bool HasOccurrence;
            internal GameplayTriggerRuntimeEvent Occurrence;
            internal bool HasActionDispatch;
            internal GameplayTriggerRuntimeEvent ActionDispatch;
            internal bool HasActionResult;
            internal GameplayTriggerRuntimeEvent ActionResult;
        }

        private readonly struct ActivationSource
        {
            internal ActivationSource(
                long activationId,
                int actionIndex,
                int sourceBindingId)
            {
                ActivationId = activationId;
                ActionIndex = actionIndex;
                SourceBindingId = sourceBindingId;
            }

            internal long ActivationId { get; }
            internal int ActionIndex { get; }
            internal int SourceBindingId { get; }
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerDiagnosticsSystem :
        IGameplaySystem
    {
        internal const string StableSystemId =
            "character_test.level_gameplay_triggers.diagnostics";

        private readonly CharacterTestLevelGameplayTriggerRuntimeStatus _status;

        internal CharacterTestLevelGameplayTriggerDiagnosticsSystem(
            CharacterTestLevelGameplayTriggerRuntimeStatus status)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
        }

        public string SystemId => StableSystemId;
        public GameplaySystemPhase Phase => GameplaySystemPhase.Diagnostics;
        public int Priority => 1100;
        public bool IsEnabled { get; private set; } = true;

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            _status.DrainEvents();
        }
    }
}
