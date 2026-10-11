using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Numeric runtime diagnostic; text formatting belongs to low-frequency tooling.</summary>
    public readonly struct GameplayTriggerDiagnostic
    {
        internal GameplayTriggerDiagnostic(RuntimeFrame frame, GameplayTriggerErrorCode code, GameplayTriggerSourceBindingId sourceBindingId, GameplayTriggerInstanceId instanceId, GameplayTriggerRouteId routeId, int detail)
        {
            Frame = frame;
            Code = code;
            SourceBindingId = sourceBindingId;
            InstanceId = instanceId;
            RouteId = routeId;
            Detail = detail;
        }

        public RuntimeFrame Frame { get; }
        public GameplayTriggerErrorCode Code { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public int Detail { get; }
    }

    /// <summary>Fixed-capacity, deterministic trigger rule kernel.</summary>
    public sealed partial class GameplayTriggerRuntime
    {
        private readonly GameplayTriggerCatalog _catalog;
        private readonly GameplayTriggerRuntimeOptions _options;
        private InstanceState[] _instances;
        private RelationState[] _relations;
        private RouteState[] _routeStates;
        private InputBatch[] _batches;
        private GameplayEntityId[] _occupancyInputs;
        private GameplayTriggerScalarSample[] _scalarInputs;
        private GameplayTriggerSignal[] _signalInputs;
        private GameplayTriggerOccurrence[] _occurrences;
        private Candidate[] _candidates;
        private GameplayTriggerActivation[] _activations;
        private GameplayTriggerActionIntent[] _intents;
        private PreparedAction[] _preparedActions;
        private bool[] _conditionStack;
        private GameplayTriggerDiagnostic[] _diagnostics;
        private readonly GameplayTriggerRuntimeEventSource _eventSource;

        private int _relationCount;
        private int _routeStateCount;
        private int _batchCount;
        private int _occupancyInputCount;
        private int _scalarInputCount;
        private int _signalInputCount;
        private int _occurrenceCount;
        private int _candidateCount;
        private int _candidateCapacityReservation;
        private int _activationCount;
        private int _intentCount;
        private int _diagnosticCount;
        private long _nextActivationId = 1L;
        private RuntimeFrame _lastEvaluatedFrame;
        private bool _hasEvaluated;

        /// <remarks>LowFreqAlloc. Construction allocates all fixed-capacity runtime buffers.</remarks>
        public GameplayTriggerRuntime(GameplayTriggerCatalog catalog, GameplayTriggerRuntimeOptions options)
            : this(catalog, options, null, false)
        {
        }

        /// <remarks>LowFreqAlloc. Construction allocates all fixed-capacity runtime buffers and binds one presentation producer.</remarks>
        public GameplayTriggerRuntime(
            GameplayTriggerCatalog catalog,
            GameplayTriggerRuntimeOptions options,
            GameplayTriggerRuntimeEventSource eventSource)
            : this(catalog, options, eventSource, true)
        {
        }

        private GameplayTriggerRuntime(
            GameplayTriggerCatalog catalog,
            GameplayTriggerRuntimeOptions options,
            GameplayTriggerRuntimeEventSource eventSource,
            bool bindEventSource)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (options.MaxConditionStackDepth < catalog.MaxConditionStackDepth)
                throw new ArgumentException("Runtime condition stack capacity is smaller than the catalog requirement.", nameof(options));
            if (bindEventSource)
            {
                if (eventSource == null) throw new ArgumentNullException(nameof(eventSource));
                eventSource.ValidateProducerUnbound();
            }
            _options = options;
            _instances = new InstanceState[catalog.BindingCount];
            _relations = new RelationState[options.MaxRelations];
            _routeStates = new RouteState[options.MaxRouteStates];
            _batches = new InputBatch[options.MaxInputBatches];
            _occupancyInputs = new GameplayEntityId[options.MaxInputItems];
            _scalarInputs = new GameplayTriggerScalarSample[options.MaxInputItems];
            _signalInputs = new GameplayTriggerSignal[options.MaxInputItems];
            _occurrences = new GameplayTriggerOccurrence[options.MaxOccurrences];
            _candidates = new Candidate[options.MaxCandidates];
            _activations = new GameplayTriggerActivation[options.MaxActivations];
            _intents = new GameplayTriggerActionIntent[options.MaxIntents];
            _preparedActions = new PreparedAction[options.MaxPreparedActions];
            _conditionStack = new bool[options.MaxConditionStackDepth];
            _diagnostics = new GameplayTriggerDiagnostic[options.MaxCandidates];

            for (int i = 0; i < _instances.Length; i++)
            {
                GameplayTriggerBinding binding = catalog.GetBinding(i);
                int definitionIndex = catalog.FindDefinition(binding.DefinitionId);
                int sourceIndex = catalog.FindSource(catalog.GetDefinition(definitionIndex).SourceDefinitionId);
                _instances[i] = new InstanceState(
                    new GameplayTriggerInstanceId(i + 1, 1),
                    i,
                    definitionIndex,
                    sourceIndex,
                    binding.InitiallyEnabled);
            }
            InitializeDeferredAuthority();
            InitializeReliableOutboxAuthority();
            _eventSource = eventSource;
            if (bindEventSource) eventSource.BindProducer(this);
        }

        public int InstanceCount => _instances.Length;
        public int RelationCount => _relationCount;
        public int RouteStateCount => _routeStateCount;
        public int OccurrenceCount => _occurrenceCount;
        /// <remarks>NoAlloc. Exposes the current-frame bounded candidate high-water evidence.</remarks>
        public int CandidateCount => _candidateCount;
        public int ActivationCount => _activationCount;
        public int IntentCount => _intentCount;
        public int DiagnosticCount => _diagnosticCount;
        public long NextActivationId => _nextActivationId;

        public GameplayTriggerOccurrence GetOccurrence(int index) => _occurrences[index];
        public GameplayTriggerActivation GetActivation(int index) => _activations[index];
        public GameplayTriggerActionIntent GetIntent(int index) => _intents[index];
        public GameplayTriggerDiagnostic GetDiagnostic(int index) => _diagnostics[index];
        public GameplayTriggerInstanceId GetInstanceId(int index) => _instances[index].InstanceId;

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TrySubmitOccupancy(RuntimeFrame frame, GameplayTriggerSourceBindingId source, ReadOnlySpan<GameplayEntityId> subjects, out GameplayTriggerResult result, bool isComplete = true)
        {
            if (!ValidateSubmission(frame, source, GameplayTriggerSourceKind.Occupancy, subjects.Length, isComplete, out int instanceIndex, out result))
                return false;
            if (_occupancyInputCount + subjects.Length > _occupancyInputs.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, subjects.Length, _occupancyInputs.Length - _occupancyInputCount);
                return false;
            }

            int start = _occupancyInputCount;
            for (int i = 0; i < subjects.Length; i++)
            {
                if (!subjects[i].IsValid)
                {
                    result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidArgument, i);
                    return false;
                }
            }
            for (int i = 0; i < subjects.Length; i++) _occupancyInputs[_occupancyInputCount + i] = subjects[i];
            _occupancyInputCount += subjects.Length;
            _batches[_batchCount++] = new InputBatch(frame, source, instanceIndex, GameplayTriggerSourceKind.Occupancy, start, subjects.Length);
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TrySubmitScalarBatch(RuntimeFrame frame, GameplayTriggerSourceBindingId source, ReadOnlySpan<GameplayTriggerScalarSample> samples, out GameplayTriggerResult result, bool isComplete = true)
        {
            if (!ValidateSubmission(frame, source, GameplayTriggerSourceKind.Threshold, samples.Length, isComplete, out int instanceIndex, out result))
                return false;
            if (_scalarInputCount + samples.Length > _scalarInputs.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, samples.Length, _scalarInputs.Length - _scalarInputCount);
                return false;
            }

            int start = _scalarInputCount;
            for (int i = 0; i < samples.Length; i++) _scalarInputs[_scalarInputCount++] = samples[i];
            _batches[_batchCount++] = new InputBatch(frame, source, instanceIndex, GameplayTriggerSourceKind.Threshold, start, samples.Length);
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TrySubmitSignals(RuntimeFrame frame, GameplayTriggerSourceBindingId source, GameplayTriggerSignalOrdering ordering, ReadOnlySpan<GameplayTriggerSignal> signals, out GameplayTriggerResult result, bool isComplete = true)
        {
            if (!ValidateSubmission(frame, source, GameplayTriggerSourceKind.Signal, signals.Length, isComplete, out int instanceIndex, out result))
                return false;
            GameplayTriggerSourceDefinition sourceDefinition = _catalog.GetSource(_instances[instanceIndex].SourceIndex);
            if (sourceDefinition.SignalOrdering != ordering)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidArgument, (int)ordering, (int)sourceDefinition.SignalOrdering);
                return false;
            }
            if (_signalInputCount + signals.Length > _signalInputs.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, signals.Length, _signalInputs.Length - _signalInputCount);
                return false;
            }

            int start = _signalInputCount;
            long previousSequence = -1L;
            for (int i = 0; i < signals.Length; i++)
            {
                if (ordering == GameplayTriggerSignalOrdering.Ordered && signals[i].Sequence <= previousSequence)
                {
                    result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidArgument, i);
                    return false;
                }
                previousSequence = signals[i].Sequence;
            }
            for (int i = 0; i < signals.Length; i++) _signalInputs[_signalInputCount + i] = signals[i];
            _signalInputCount += signals.Length;
            _batches[_batchCount++] = new InputBatch(frame, source, instanceIndex, GameplayTriggerSourceKind.Signal, start, signals.Length);
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        /// <summary>Evaluates every submitted source batch for one frame in stable source order.</summary>
        /// <remarks>NoAlloc after initialization, including capacity rejection paths.</remarks>
        public GameplayTriggerResult Evaluate(RuntimeFrame frame)
        {
            if (_hasEvaluated && frame <= _lastEvaluatedFrame)
                return new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidFrame);

            _occurrenceCount = 0;
            _candidateCount = 0;
            _candidateCapacityReservation = 0;
            _activationCount = 0;
            _intentCount = 0;
            _diagnosticCount = 0;

            GameplayTriggerResult firstFailure = GameplayTriggerResult.Succeeded;
            SortBatches();
            for (int i = 0; i < _batchCount; i++)
            {
                InputBatch batch = _batches[i];
                if (batch.Frame != frame)
                {
                    GameplayTriggerResult failure = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidFrame, checked((int)Math.Min(int.MaxValue, batch.Frame.Value)), checked((int)Math.Min(int.MaxValue, frame.Value)));
                    RecordDiagnostic(frame, failure.ErrorCode, batch.SourceBindingId, _instances[batch.InstanceIndex].InstanceId, default, i);
                    if (firstFailure.Success) firstFailure = failure;
                    continue;
                }

                GameplayTriggerResult result;
                switch (batch.Kind)
                {
                    case GameplayTriggerSourceKind.Occupancy:
                        result = ReduceOccupancy(batch);
                        break;
                    case GameplayTriggerSourceKind.Threshold:
                        result = ReduceThreshold(batch);
                        break;
                    case GameplayTriggerSourceKind.Signal:
                        result = ReduceSignals(batch);
                        break;
                    default:
                        result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.SourceKindMismatch);
                        break;
                }

                if (!result.Success)
                {
                    RecordDiagnostic(frame, result.ErrorCode, batch.SourceBindingId, _instances[batch.InstanceIndex].InstanceId, default, result.Detail0);
                    if (firstFailure.Success) firstFailure = result;
                }
            }

            SortOccurrencesAndAssignCanonicalOrdinals();
            PublishCanonicalOccurrences();
            AddCandidatesForAllOccurrences();
            SortCandidatesByIdentity();
            EvaluateCandidateEligibility();
            ResolveExclusiveWinners();
            SortCandidatesForExecution();
            CommitAcceptedCandidates(frame);

            _lastEvaluatedFrame = frame;
            _hasEvaluated = true;
            ClearInputs();
            return firstFailure.Success ? GameplayTriggerResult.Succeeded : firstFailure;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TrySetEnabled(GameplayTriggerInstanceId instanceId, bool enabled, out GameplayTriggerResult result)
        {
            int instanceIndex = FindInstance(instanceId);
            if (instanceIndex < 0)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidArgument);
                return false;
            }
            InstanceState state = _instances[instanceIndex];
            if (state.Enabled == enabled)
            {
                result = GameplayTriggerResult.Succeeded;
                return true;
            }
            state.Enabled = enabled;
            _instances[instanceIndex] = state;
            if (!enabled)
            {
                ClearPendingInputs(instanceIndex);
                ClearSourceState(instanceIndex);
            }
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryReset(GameplayTriggerInstanceId instanceId, out GameplayTriggerResult result)
        {
            int instanceIndex = FindInstance(instanceId);
            if (instanceIndex < 0)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidArgument);
                return false;
            }
            ClearPendingInputs(instanceIndex);
            ClearSourceState(instanceIndex);
            for (int i = 0; i < _routeStateCount; i++)
            {
                if (_routeStates[i].InstanceIndex == instanceIndex) _routeStates[i] = default;
            }
            CompactRouteStates();
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        private bool ValidateSubmission(RuntimeFrame frame, GameplayTriggerSourceBindingId source, GameplayTriggerSourceKind expectedKind, int count, bool isComplete, out int instanceIndex, out GameplayTriggerResult result)
        {
            instanceIndex = FindInstance(source);
            if (instanceIndex < 0)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.UnknownSourceBinding, source.Value);
                return false;
            }
            if (_hasEvaluated && frame <= _lastEvaluatedFrame)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InvalidFrame);
                return false;
            }
            if (!isComplete)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.BatchIncomplete);
                return false;
            }
            if (!_instances[instanceIndex].Enabled)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.Disabled);
                return false;
            }
            if (_catalog.GetSource(_instances[instanceIndex].SourceIndex).SourceKind != expectedKind)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.SourceKindMismatch);
                return false;
            }
            if (_batchCount >= _batches.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, count, 0);
                return false;
            }
            for (int i = 0; i < _batchCount; i++)
            {
                if (_batches[i].Frame == frame && _batches[i].SourceBindingId == source)
                {
                    result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.DuplicateSourceBatch, source.Value);
                    return false;
                }
            }
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        private GameplayTriggerResult ReduceOccupancy(InputBatch batch)
        {
            SortAndDeduplicateOccupancy(batch.Start, ref batch.Count);
            int newRelationCount = 0;
            for (int i = 0; i < batch.Count; i++) if (FindRelation(batch.InstanceIndex, _occupancyInputs[batch.Start + i]) < 0) newRelationCount++;
            if (_relationCount + newRelationCount > _relations.Length) return new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, newRelationCount, _relations.Length - _relationCount);

            int exitCount = 0;
            for (int i = 0; i < _relationCount; i++)
            {
                RelationState relation = _relations[i];
                if (relation.InstanceIndex != batch.InstanceIndex || !relation.Active) continue;
                if (!ContainsOccupancy(batch.Start, batch.Count, relation.Subject)) exitCount++;
            }
            int maxOccurrences = batch.Count + exitCount;
            GameplayTriggerDefinition definition = _catalog.GetDefinition(_instances[batch.InstanceIndex].DefinitionIndex);
            int exactCandidates = 0;
            for (int i = 0; i < batch.Count; i++)
            {
                int relationIndex = FindRelation(batch.InstanceIndex, _occupancyInputs[batch.Start + i]);
                GameplayTriggerOccurrenceKind kind =
                    relationIndex >= 0 && _relations[relationIndex].Active
                        ? GameplayTriggerOccurrenceKind.Stayed
                        : GameplayTriggerOccurrenceKind.Entered;
                exactCandidates += CountMatchingRoutes(definition, kind);
            }
            exactCandidates += exitCount * CountMatchingRoutes(definition, GameplayTriggerOccurrenceKind.Exited);
            if (!HasOccurrenceAndCandidateCapacity(maxOccurrences, exactCandidates, out GameplayTriggerResult capacityResult)) return capacityResult;

            long frame = batch.Frame.Value;
            int activeBefore = _instances[batch.InstanceIndex].ActiveCount;
            for (int i = 0; i < batch.Count; i++)
            {
                GameplayEntityId subject = _occupancyInputs[batch.Start + i];
                int relationIndex = FindRelation(batch.InstanceIndex, subject);
                if (relationIndex < 0) relationIndex = AddRelation(batch.InstanceIndex, subject);
                RelationState relation = _relations[relationIndex];
                relation.SeenFrame = frame;
                GameplayTriggerOccurrenceKind kind;
                if (!relation.Active)
                {
                    relation.Active = true;
                    relation.PresenceEpoch++;
                    relation.PresenceStartFrame = frame;
                    kind = GameplayTriggerOccurrenceKind.Entered;
                }
                else
                {
                    kind = GameplayTriggerOccurrenceKind.Stayed;
                }
                _relations[relationIndex] = relation;
                AddOccurrence(batch.Frame, batch.InstanceIndex, kind, subject, default, 0, default, default, 0L, 0L, 0L, i, relation.PresenceEpoch, relation.PresenceStartFrame);
            }

            for (int i = 0; i < _relationCount; i++)
            {
                RelationState relation = _relations[i];
                if (relation.InstanceIndex != batch.InstanceIndex || !relation.Active || relation.SeenFrame == frame) continue;
                relation.Active = false;
                _relations[i] = relation;
                AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Exited, relation.Subject, default, 0, default, default, 0L, 0L, 0L, batch.Count + i, relation.PresenceEpoch, relation.PresenceStartFrame);
            }

            RefreshActiveCountAndEpoch(batch.InstanceIndex, activeBefore);
            return GameplayTriggerResult.Succeeded;
        }

        private GameplayTriggerResult ReduceThreshold(InputBatch batch)
        {
            SortScalars(batch.Start, batch.Count);
            for (int i = 1; i < batch.Count; i++)
            {
                if (_scalarInputs[batch.Start + i - 1].Subject == _scalarInputs[batch.Start + i].Subject)
                    return new GameplayTriggerResult(false, GameplayTriggerErrorCode.DuplicateScalarSubject, i);
            }

            int newRelationCount = 0;
            for (int i = 0; i < batch.Count; i++) if (FindRelation(batch.InstanceIndex, _scalarInputs[batch.Start + i].Subject) < 0) newRelationCount++;
            if (_relationCount + newRelationCount > _relations.Length) return new GameplayTriggerResult(false, GameplayTriggerErrorCode.InputCapacityExceeded, newRelationCount, _relations.Length - _relationCount);

            GameplayTriggerDefinition definition = _catalog.GetDefinition(_instances[batch.InstanceIndex].DefinitionIndex);
            GameplayTriggerSourceDefinition source = _catalog.GetSource(_instances[batch.InstanceIndex].SourceIndex);
            int exactOccurrences = 0;
            int exactCandidates = 0;
            for (int i = 0; i < batch.Count; i++)
            {
                GameplayTriggerScalarSample sample = _scalarInputs[batch.Start + i];
                int relationIndex = FindRelation(batch.InstanceIndex, sample.Subject);
                RelationState relation = relationIndex >= 0 ? _relations[relationIndex] : default;
                bool active = relation.Active
                    ? RemainsThresholdActive(source, sample.Value)
                    : EntersThreshold(source, sample.Value);
                GameplayTriggerOccurrenceKind kind = GameplayTriggerOccurrenceKind.None;
                if (!relation.Primed)
                {
                    if (active && source.InitialSamplePolicy == GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive)
                        kind = GameplayTriggerOccurrenceKind.Entered;
                }
                else if (!relation.Active && active)
                {
                    kind = GameplayTriggerOccurrenceKind.Entered;
                }
                else if (relation.Active && !active)
                {
                    kind = GameplayTriggerOccurrenceKind.Exited;
                }
                else if (active)
                {
                    kind = GameplayTriggerOccurrenceKind.Stayed;
                }
                if (kind == GameplayTriggerOccurrenceKind.None) continue;
                exactOccurrences++;
                exactCandidates += CountMatchingRoutes(definition, kind);
            }
            if (source.DeparturePolicy == GameplayTriggerSubjectDeparturePolicy.EmitExited)
            {
                for (int i = 0; i < _relationCount; i++)
                {
                    RelationState relation = _relations[i];
                    if (relation.InstanceIndex != batch.InstanceIndex || !relation.Primed || !relation.Active) continue;
                    if (ContainsScalar(batch.Start, batch.Count, relation.Subject)) continue;
                    exactOccurrences++;
                    exactCandidates += CountMatchingRoutes(definition, GameplayTriggerOccurrenceKind.Exited);
                }
            }
            if (!HasOccurrenceAndCandidateCapacity(exactOccurrences, exactCandidates, out GameplayTriggerResult capacityResult)) return capacityResult;
            long frame = batch.Frame.Value;
            int activeBefore = _instances[batch.InstanceIndex].ActiveCount;
            for (int i = 0; i < batch.Count; i++)
            {
                GameplayTriggerScalarSample sample = _scalarInputs[batch.Start + i];
                int relationIndex = FindRelation(batch.InstanceIndex, sample.Subject);
                if (relationIndex < 0) relationIndex = AddRelation(batch.InstanceIndex, sample.Subject);
                RelationState relation = _relations[relationIndex];
                relation.SeenFrame = frame;
                Fix64 previous = relation.Scalar;
                bool wasPrimed = relation.Primed;
                bool wasActive = relation.Active;
                bool active = wasActive
                    ? RemainsThresholdActive(source, sample.Value)
                    : EntersThreshold(source, sample.Value);
                relation.Primed = true;
                relation.Scalar = sample.Value;

                if (!wasPrimed)
                {
                    relation.Active = active;
                    if (active)
                    {
                        relation.PresenceEpoch++;
                        relation.PresenceStartFrame = frame;
                        if (source.InitialSamplePolicy == GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive)
                            AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Entered, sample.Subject, default, 0, previous, sample.Value, 0L, 0L, 0L, i, relation.PresenceEpoch, relation.PresenceStartFrame);
                    }
                }
                else if (!wasActive && active)
                {
                    relation.Active = true;
                    relation.PresenceEpoch++;
                    relation.PresenceStartFrame = frame;
                    AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Entered, sample.Subject, default, 0, previous, sample.Value, 0L, 0L, 0L, i, relation.PresenceEpoch, relation.PresenceStartFrame);
                }
                else if (wasActive && !active)
                {
                    relation.Active = false;
                    AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Exited, sample.Subject, default, 0, previous, sample.Value, 0L, 0L, 0L, i, relation.PresenceEpoch, relation.PresenceStartFrame);
                }
                else if (active)
                {
                    AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Stayed, sample.Subject, default, 0, previous, sample.Value, 0L, 0L, 0L, i, relation.PresenceEpoch, relation.PresenceStartFrame);
                }
                _relations[relationIndex] = relation;
            }

            for (int i = 0; i < _relationCount; i++)
            {
                RelationState relation = _relations[i];
                if (relation.InstanceIndex != batch.InstanceIndex || !relation.Primed || relation.SeenFrame == frame) continue;
                if (relation.Active && source.DeparturePolicy == GameplayTriggerSubjectDeparturePolicy.EmitExited)
                    AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Exited, relation.Subject, default, 0, relation.Scalar, relation.Scalar, 0L, 0L, 0L, batch.Count + i, relation.PresenceEpoch, relation.PresenceStartFrame);
                relation.Active = false;
                relation.Primed = false;
                _relations[i] = relation;
            }

            RefreshActiveCountAndEpoch(batch.InstanceIndex, activeBefore);
            return GameplayTriggerResult.Succeeded;
        }

        private GameplayTriggerResult ReduceSignals(InputBatch batch)
        {
            GameplayTriggerSourceDefinition source = _catalog.GetSource(_instances[batch.InstanceIndex].SourceIndex);
            if (source.SignalOrdering == GameplayTriggerSignalOrdering.UnorderedMultiset) SortSignals(batch.Start, batch.Count);
            GameplayTriggerDefinition definition = _catalog.GetDefinition(_instances[batch.InstanceIndex].DefinitionIndex);
            int exactCandidates = batch.Count * CountMatchingRoutes(definition, GameplayTriggerOccurrenceKind.Pulsed);
            if (!HasOccurrenceAndCandidateCapacity(batch.Count, exactCandidates, out GameplayTriggerResult capacityResult)) return capacityResult;
            for (int i = 0; i < batch.Count; i++)
            {
                GameplayTriggerSignal signal = _signalInputs[batch.Start + i];
                long ordering = source.SignalOrdering == GameplayTriggerSignalOrdering.Ordered ? signal.Sequence : i;
                AddOccurrence(batch.Frame, batch.InstanceIndex, GameplayTriggerOccurrenceKind.Pulsed, signal.Subject, signal.Instigator, signal.SignalId, default, default, signal.Payload0, signal.Payload1, signal.Payload2, ordering, 0L, 0L);
            }
            return GameplayTriggerResult.Succeeded;
        }

        private bool HasOccurrenceAndCandidateCapacity(int exactOccurrences, int exactCandidates, out GameplayTriggerResult result)
        {
            if (_occurrenceCount + exactOccurrences > _occurrences.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.OccurrenceCapacityExceeded, exactOccurrences, _occurrences.Length - _occurrenceCount);
                return false;
            }
            if (_candidateCapacityReservation + exactCandidates > _candidates.Length)
            {
                result = new GameplayTriggerResult(false, GameplayTriggerErrorCode.CandidateCapacityExceeded, exactCandidates, _candidates.Length - _candidateCapacityReservation);
                return false;
            }
            _candidateCapacityReservation += exactCandidates;
            result = GameplayTriggerResult.Succeeded;
            return true;
        }

        private static int CountMatchingRoutes(GameplayTriggerDefinition definition, GameplayTriggerOccurrenceKind kind)
        {
            int count = 0;
            for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
            {
                if ((definition.GetRoute(routeIndex).OccurrenceMask & kind) != 0) count++;
            }
            return count;
        }

        private void AddCandidatesForAllOccurrences()
        {
            for (int occurrenceIndex = 0; occurrenceIndex < _occurrenceCount; occurrenceIndex++)
            {
                GameplayTriggerOccurrence occurrence = _occurrences[occurrenceIndex];
                int instanceIndex = occurrence.InstanceId.Index - 1;
                GameplayTriggerDefinition definition = _catalog.GetDefinition(_instances[instanceIndex].DefinitionIndex);
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    if ((route.OccurrenceMask & occurrence.Kind) == 0) continue;
                    _candidates[_candidateCount++] = new Candidate(instanceIndex, routeIndex, occurrenceIndex);
                }
            }
        }

        private void EvaluateCandidateEligibility()
        {
            for (int i = 0; i < _candidateCount; i++)
            {
                Candidate candidate = _candidates[i];
                GameplayTriggerDefinition definition = _catalog.GetDefinition(_instances[candidate.InstanceIndex].DefinitionIndex);
                GameplayTriggerRouteDefinition route = definition.GetRoute(candidate.RouteIndex);
                GameplayTriggerOccurrence occurrence = _occurrences[candidate.OccurrenceIndex];
                if (!route.ConditionProgram.Evaluate(occurrence, _conditionStack))
                {
                    candidate.Rejection = GameplayTriggerErrorCode.ConditionRejected;
                }
                else if (route.ExclusiveGroupId != 0 &&
                         !HasArbitrationScopeKey(route.ArbitrationScope, occurrence))
                {
                    candidate.Rejection = GameplayTriggerErrorCode.MissingScopeEntity;
                }
                else
                {
                    candidate.Rejection = CheckPolicy(candidate, route, occurrence, out _);
                }
                _candidates[i] = candidate;
            }
        }

        private void ResolveExclusiveWinners()
        {
            for (int i = 0; i < _candidateCount; i++)
            {
                Candidate candidate = _candidates[i];
                if (candidate.Rejection != GameplayTriggerErrorCode.None) continue;
                GameplayTriggerRouteDefinition route = GetRoute(candidate);
                if (route.ExclusiveGroupId == 0) continue;
                for (int j = 0; j < _candidateCount; j++)
                {
                    if (i == j || _candidates[j].Rejection != GameplayTriggerErrorCode.None) continue;
                    GameplayTriggerRouteDefinition otherRoute = GetRoute(_candidates[j]);
                    if (otherRoute.ExclusiveGroupId != route.ExclusiveGroupId || !SameArbitrationScope(route.ArbitrationScope, candidate, _candidates[j])) continue;
                    int winnerComparison = CompareWinner(_candidates[j], candidate);
                    if (winnerComparison < 0)
                    {
                        candidate.Rejection = GameplayTriggerErrorCode.ArbitrationSuppressed;
                        _candidates[i] = candidate;
                        break;
                    }
                }
            }
        }

        private void CommitAcceptedCandidates(RuntimeFrame frame)
        {
            for (int i = 0; i < _candidateCount; i++)
            {
                Candidate candidate = _candidates[i];
                GameplayTriggerRouteDefinition route = GetRoute(candidate);
                GameplayTriggerOccurrence occurrence = _occurrences[candidate.OccurrenceIndex];
                if (candidate.Rejection != GameplayTriggerErrorCode.None)
                {
                    RecordDiagnostic(frame, candidate.Rejection, _catalog.GetBinding(_instances[candidate.InstanceIndex].BindingIndex).SourceBindingId, occurrence.InstanceId, route.RouteId, 0);
                    continue;
                }

                GameplayTriggerErrorCode policy = CheckPolicy(candidate, route, occurrence, out int routeStateIndex);
                if (policy != GameplayTriggerErrorCode.None)
                {
                    RecordDiagnostic(frame, policy, _catalog.GetBinding(_instances[candidate.InstanceIndex].BindingIndex).SourceBindingId, occurrence.InstanceId, route.RouteId, 0);
                    continue;
                }
                bool needsRouteState = routeStateIndex < 0;
                if (needsRouteState && _routeStateCount >= _routeStates.Length)
                {
                    RecordDiagnostic(frame, GameplayTriggerErrorCode.RouteStateCapacityExceeded, _catalog.GetBinding(_instances[candidate.InstanceIndex].BindingIndex).SourceBindingId, occurrence.InstanceId, route.RouteId, 0);
                    continue;
                }
                if (_activationCount >= _activations.Length || _intentCount + route.ActionCount > _intents.Length || route.ActionCount > _preparedActions.Length)
                {
                    RecordDiagnostic(frame, GameplayTriggerErrorCode.IntentCapacityExceeded, _catalog.GetBinding(_instances[candidate.InstanceIndex].BindingIndex).SourceBindingId, occurrence.InstanceId, route.RouteId, route.ActionCount);
                    continue;
                }

                int preparedCount = 0;
                bool prepared = true;
                GameplayTriggerBinding binding = _catalog.GetBinding(_instances[candidate.InstanceIndex].BindingIndex);
                for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                {
                    if (TryResolveActionTarget(binding, occurrence, route.GetAction(actionIndex), out _)) continue;
                    prepared = false;
                    break;
                }
                if (!prepared)
                {
                    RecordDiagnostic(frame, GameplayTriggerErrorCode.MissingActionTarget, binding.SourceBindingId, occurrence.InstanceId, route.RouteId, 0);
                    continue;
                }

                for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                {
                    GameplayTriggerActionInstruction instruction = route.GetAction(actionIndex);
                    TryResolveActionTarget(binding, occurrence, instruction, out GameplayTriggerTargetRef target);
                    int sinkIndex = _catalog.FindSink(instruction.ActionKindId);
                    GameplayTriggerActionSinkEntry sinkEntry = _catalog.GetSinkEntry(sinkIndex);
                    var preparation = new GameplayTriggerActionPreparation(frame, binding.BindingId, binding.Owner, target, route.RouteId, actionIndex, occurrence, instruction);
                    GameplayTriggerErrorCode prepareError = GameplayTriggerErrorCode.None;
                    bool didPrepare;
                    int token;
                    if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                    {
                        didPrepare = TryPrepareInternalDeferred(
                            sinkEntry,
                            in preparation,
                            out token,
                            out prepareError);
                    }
                    else if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred)
                    {
                        didPrepare = TryPrepareReliableOutbox(
                            sinkEntry,
                            in preparation,
                            out token,
                            out prepareError);
                    }
                    else
                    {
                        didPrepare = sinkEntry.Sink.TryPrepare(preparation, out token);
                        if (!didPrepare) prepareError = GameplayTriggerErrorCode.ReservationRejected;
                    }
                    if (!didPrepare)
                    {
                        prepared = false;
                        candidate.Rejection = prepareError;
                        break;
                    }
                    _preparedActions[preparedCount++] = new PreparedAction(sinkIndex, token, preparation);
                }

                if (!prepared)
                {
                    for (int preparedIndex = preparedCount - 1; preparedIndex >= 0; preparedIndex--)
                    {
                        PreparedAction action = _preparedActions[preparedIndex];
                        GameplayTriggerActionSinkEntry sinkEntry = _catalog.GetSinkEntry(action.SinkIndex);
                        if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                            AbortInternalDeferred(action.Token);
                        else if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred)
                            AbortReliableOutbox(action.Token);
                        else
                            sinkEntry.Sink.Abort(action.Token);
                    }
                    RecordDiagnostic(
                        frame,
                        candidate.Rejection == GameplayTriggerErrorCode.None
                            ? GameplayTriggerErrorCode.ReservationRejected
                            : candidate.Rejection,
                        binding.SourceBindingId,
                        occurrence.InstanceId,
                        route.RouteId,
                        preparedCount);
                    continue;
                }

                if (needsRouteState)
                {
                    routeStateIndex = _routeStateCount++;
                    _routeStates[routeStateIndex] = CreateRouteState(candidate, route, occurrence);
                }

                long activationId = _nextActivationId++;
                CommitPolicy(routeStateIndex, route, occurrence, frame);
                InstanceState instance = _instances[candidate.InstanceIndex];
                GameplayTriggerDefinition definition = _catalog.GetDefinition(instance.DefinitionIndex);
                _activations[_activationCount++] = new GameplayTriggerActivation(activationId, definition.DefinitionId, binding.BindingId, route.RouteId, occurrence);
                PublishAcceptedActivation(in _activations[_activationCount - 1], in binding);
                for (int preparedIndex = 0; preparedIndex < preparedCount; preparedIndex++)
                {
                    PreparedAction action = _preparedActions[preparedIndex];
                    var intent = new GameplayTriggerActionIntent(activationId, action.Preparation);
                    _intents[_intentCount++] = intent;
                    GameplayTriggerActionSinkEntry sinkEntry = _catalog.GetSinkEntry(action.SinkIndex);
                    if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                        CommitInternalDeferred(action.Token, activationId, in intent);
                    else if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred)
                        CommitReliableOutbox(action.Token, activationId, in intent);
                    else
                        sinkEntry.Sink.Commit(action.Token, intent);
                    PublishActionDispatched(in intent, definition.DefinitionId, binding.SourceBindingId);
                }
            }
        }

        private GameplayTriggerErrorCode CheckPolicy(Candidate candidate, GameplayTriggerRouteDefinition route, GameplayTriggerOccurrence occurrence, out int routeStateIndex)
        {
            if (!TryGetScopeKey(route.ActivationPolicy.StateScope, occurrence, out GameplayEntityId subject, out GameplayEntityId instigator))
            {
                routeStateIndex = -1;
                return GameplayTriggerErrorCode.MissingScopeEntity;
            }
            routeStateIndex = FindRouteState(candidate.InstanceIndex, route.RouteId, subject, instigator);
            RouteState state = routeStateIndex >= 0 ? _routeStates[routeStateIndex] : default;
            GameplayTriggerActivationPolicy policy = route.ActivationPolicy;
            if (policy.MaxActivations > 0 && state.ActivationCount >= policy.MaxActivations) return GameplayTriggerErrorCode.PolicyRejected;
            if (state.HasActivated && occurrence.Frame.Value < AddFrames(state.LastActivationFrame, policy.CooldownFrames)) return GameplayTriggerErrorCode.PolicyRejected;
            if (occurrence.Kind == GameplayTriggerOccurrenceKind.Stayed)
            {
                long nextStay = state.HasStaySchedule ? state.NextStayFrame : AddFrames(occurrence.PresenceStartFrame, policy.StayIntervalFrames);
                if (occurrence.Frame.Value < nextStay) return GameplayTriggerErrorCode.PolicyRejected;
            }
            if (policy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence)
            {
                long epoch = policy.StateScope == GameplayTriggerStateScope.PerInstance
                    ? _instances[candidate.InstanceIndex].AggregatePresenceEpoch
                    : occurrence.PresenceEpoch;
                if (epoch <= 0L || state.LastActivatedPresenceEpoch == epoch) return GameplayTriggerErrorCode.PolicyRejected;
            }
            return GameplayTriggerErrorCode.None;
        }

        private RouteState CreateRouteState(Candidate candidate, GameplayTriggerRouteDefinition route, GameplayTriggerOccurrence occurrence)
        {
            TryGetScopeKey(route.ActivationPolicy.StateScope, occurrence, out GameplayEntityId subject, out GameplayEntityId instigator);
            return new RouteState(candidate.InstanceIndex, route.RouteId, subject, instigator);
        }

        private void CommitPolicy(int routeStateIndex, GameplayTriggerRouteDefinition route, GameplayTriggerOccurrence occurrence, RuntimeFrame frame)
        {
            RouteState state = _routeStates[routeStateIndex];
            state.ActivationCount++;
            state.HasActivated = true;
            state.LastActivationFrame = frame.Value;
            if (occurrence.Kind == GameplayTriggerOccurrenceKind.Stayed)
            {
                state.HasStaySchedule = true;
                state.NextStayFrame = AddFrames(frame.Value, route.ActivationPolicy.StayIntervalFrames);
            }
            if (route.ActivationPolicy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence)
            {
                state.LastActivatedPresenceEpoch = route.ActivationPolicy.StateScope == GameplayTriggerStateScope.PerInstance
                    ? _instances[occurrence.InstanceId.Index - 1].AggregatePresenceEpoch
                    : occurrence.PresenceEpoch;
            }
            _routeStates[routeStateIndex] = state;
        }

        private void AddOccurrence(RuntimeFrame frame, int instanceIndex, GameplayTriggerOccurrenceKind kind, GameplayEntityId subject, GameplayEntityId instigator, int signalId, Fix64 previous, Fix64 current, long payload0, long payload1, long payload2, long ordering, long presenceEpoch, long presenceStartFrame)
        {
            _occurrences[_occurrenceCount++] = new GameplayTriggerOccurrence(frame, _instances[instanceIndex].InstanceId, kind, subject, instigator, signalId, previous, current, payload0, payload1, payload2, ordering, presenceEpoch, presenceStartFrame);
        }

        private int AddRelation(int instanceIndex, GameplayEntityId subject)
        {
            int index = _relationCount++;
            _relations[index] = new RelationState(instanceIndex, subject);
            return index;
        }

        private int FindRelation(int instanceIndex, GameplayEntityId subject)
        {
            for (int i = 0; i < _relationCount; i++) if (_relations[i].InstanceIndex == instanceIndex && _relations[i].Subject == subject) return i;
            return -1;
        }

        private bool ContainsOccupancy(int start, int count, GameplayEntityId subject)
        {
            for (int i = 0; i < count; i++) if (_occupancyInputs[start + i] == subject) return true;
            return false;
        }

        private bool ContainsScalar(int start, int count, GameplayEntityId subject)
        {
            for (int i = 0; i < count; i++) if (_scalarInputs[start + i].Subject == subject) return true;
            return false;
        }

        private int FindRouteState(int instanceIndex, GameplayTriggerRouteId routeId, GameplayEntityId subject, GameplayEntityId instigator)
        {
            for (int i = 0; i < _routeStateCount; i++)
            {
                RouteState state = _routeStates[i];
                if (state.InstanceIndex == instanceIndex && state.RouteId == routeId && state.Subject == subject && state.Instigator == instigator) return i;
            }
            return -1;
        }

        private int FindInstance(GameplayTriggerSourceBindingId source)
        {
            for (int i = 0; i < _instances.Length; i++) if (_catalog.GetBinding(_instances[i].BindingIndex).SourceBindingId == source) return i;
            return -1;
        }

        private int FindInstance(GameplayTriggerInstanceId instanceId)
        {
            int index = instanceId.Index - 1;
            return instanceId.IsValid && index >= 0 && index < _instances.Length && _instances[index].InstanceId == instanceId ? index : -1;
        }

        private void RefreshActiveCountAndEpoch(int instanceIndex, int activeBefore)
        {
            int activeAfter = 0;
            for (int i = 0; i < _relationCount; i++) if (_relations[i].InstanceIndex == instanceIndex && _relations[i].Active) activeAfter++;
            InstanceState instance = _instances[instanceIndex];
            if (activeBefore == 0 && activeAfter > 0) instance.AggregatePresenceEpoch++;
            instance.ActiveCount = activeAfter;
            _instances[instanceIndex] = instance;
        }

        private void ClearSourceState(int instanceIndex)
        {
            for (int i = 0; i < _relationCount; i++)
            {
                if (_relations[i].InstanceIndex != instanceIndex) continue;
                RelationState relation = _relations[i];
                relation.Active = false;
                relation.Primed = false;
                _relations[i] = relation;
            }
            InstanceState instance = _instances[instanceIndex];
            instance.ActiveCount = 0;
            _instances[instanceIndex] = instance;
        }

        private void ClearPendingInputs(int instanceIndex)
        {
            int batchWrite = 0;
            int occupancyWrite = 0;
            int scalarWrite = 0;
            int signalWrite = 0;
            for (int read = 0; read < _batchCount; read++)
            {
                InputBatch batch = _batches[read];
                if (batch.InstanceIndex == instanceIndex)
                {
                    continue;
                }

                switch (batch.Kind)
                {
                    case GameplayTriggerSourceKind.Occupancy:
                        for (int i = 0; i < batch.Count; i++)
                        {
                            _occupancyInputs[occupancyWrite + i] =
                                _occupancyInputs[batch.Start + i];
                        }
                        batch.Start = occupancyWrite;
                        occupancyWrite += batch.Count;
                        break;
                    case GameplayTriggerSourceKind.Threshold:
                        for (int i = 0; i < batch.Count; i++)
                        {
                            _scalarInputs[scalarWrite + i] = _scalarInputs[batch.Start + i];
                        }
                        batch.Start = scalarWrite;
                        scalarWrite += batch.Count;
                        break;
                    case GameplayTriggerSourceKind.Signal:
                        for (int i = 0; i < batch.Count; i++)
                        {
                            _signalInputs[signalWrite + i] = _signalInputs[batch.Start + i];
                        }
                        batch.Start = signalWrite;
                        signalWrite += batch.Count;
                        break;
                }

                _batches[batchWrite++] = batch;
            }

            for (int i = batchWrite; i < _batchCount; i++)
            {
                _batches[i] = default;
            }
            _batchCount = batchWrite;
            _occupancyInputCount = occupancyWrite;
            _scalarInputCount = scalarWrite;
            _signalInputCount = signalWrite;
        }

        private void CompactRouteStates()
        {
            int write = 0;
            for (int read = 0; read < _routeStateCount; read++)
            {
                if (_routeStates[read].RouteId.IsValid) _routeStates[write++] = _routeStates[read];
            }
            for (int i = write; i < _routeStateCount; i++) _routeStates[i] = default;
            _routeStateCount = write;
        }

        private void ClearInputs()
        {
            _batchCount = 0;
            _occupancyInputCount = 0;
            _scalarInputCount = 0;
            _signalInputCount = 0;
        }

        private void RecordDiagnostic(RuntimeFrame frame, GameplayTriggerErrorCode code, GameplayTriggerSourceBindingId source, GameplayTriggerInstanceId instance, GameplayTriggerRouteId route, int detail)
        {
            if (_diagnosticCount < _diagnostics.Length) _diagnostics[_diagnosticCount++] = new GameplayTriggerDiagnostic(frame, code, source, instance, route, detail);
            PublishRuntimeDiagnostic(frame, code, source, instance, route, detail);
        }

        private GameplayTriggerRouteDefinition GetRoute(Candidate candidate) => _catalog.GetDefinition(_instances[candidate.InstanceIndex].DefinitionIndex).GetRoute(candidate.RouteIndex);

        private static bool TryGetScopeKey(GameplayTriggerStateScope scope, GameplayTriggerOccurrence occurrence, out GameplayEntityId subject, out GameplayEntityId instigator)
        {
            subject = default;
            instigator = default;
            switch (scope)
            {
                case GameplayTriggerStateScope.PerInstance: return true;
                case GameplayTriggerStateScope.PerSubject: subject = occurrence.Subject; return subject.IsValid;
                case GameplayTriggerStateScope.PerInstigator: instigator = occurrence.Instigator; return instigator.IsValid;
                case GameplayTriggerStateScope.PerPair: subject = occurrence.Subject; instigator = occurrence.Instigator; return subject.IsValid && instigator.IsValid;
                default: return false;
            }
        }

        private static bool TryResolveActionTarget(
            GameplayTriggerBinding binding,
            GameplayTriggerOccurrence occurrence,
            GameplayTriggerActionInstruction instruction,
            out GameplayTriggerTargetRef target)
        {
            switch (instruction.TargetSelector)
            {
                case GameplayTriggerTargetSelector.Owner:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(binding.Owner);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.Subject:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(occurrence.Subject);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.Instigator:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(occurrence.Instigator);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.ExplicitBindingTarget:
                    return binding.TryGetExplicitTarget(instruction.TargetSlotId, out target);
                default:
                    target = default;
                    return false;
            }
        }

        private bool SameArbitrationScope(GameplayTriggerArbitrationScope scope, Candidate left, Candidate right)
        {
            GameplayTriggerOccurrence a = _occurrences[left.OccurrenceIndex];
            GameplayTriggerOccurrence b = _occurrences[right.OccurrenceIndex];
            switch (scope)
            {
                case GameplayTriggerArbitrationScope.Global: return true;
                case GameplayTriggerArbitrationScope.PerSubject: return a.Subject.IsValid && a.Subject == b.Subject;
                case GameplayTriggerArbitrationScope.PerInstigator: return a.Instigator.IsValid && a.Instigator == b.Instigator;
                case GameplayTriggerArbitrationScope.PerPair: return a.Subject.IsValid && a.Subject == b.Subject && a.Instigator.IsValid && a.Instigator == b.Instigator;
                default: return false;
            }
        }

        private static bool HasArbitrationScopeKey(
            GameplayTriggerArbitrationScope scope,
            GameplayTriggerOccurrence occurrence)
        {
            switch (scope)
            {
                case GameplayTriggerArbitrationScope.Global:
                    return true;
                case GameplayTriggerArbitrationScope.PerSubject:
                    return occurrence.Subject.IsValid;
                case GameplayTriggerArbitrationScope.PerInstigator:
                    return occurrence.Instigator.IsValid;
                case GameplayTriggerArbitrationScope.PerPair:
                    return occurrence.Subject.IsValid && occurrence.Instigator.IsValid;
                default:
                    return false;
            }
        }

        private int CompareWinner(Candidate left, Candidate right)
        {
            GameplayTriggerRouteDefinition leftRoute = GetRoute(left);
            GameplayTriggerRouteDefinition rightRoute = GetRoute(right);
            int priority = rightRoute.WinnerPriority.CompareTo(leftRoute.WinnerPriority);
            return priority != 0 ? priority : CompareCandidateIdentity(left, right);
        }

        private int CompareCandidateIdentity(Candidate left, Candidate right)
        {
            GameplayTriggerDefinition leftDefinition = _catalog.GetDefinition(_instances[left.InstanceIndex].DefinitionIndex);
            GameplayTriggerDefinition rightDefinition = _catalog.GetDefinition(_instances[right.InstanceIndex].DefinitionIndex);
            int result = leftDefinition.DefinitionId.CompareTo(rightDefinition.DefinitionId);
            if (result != 0) return result;
            result = leftDefinition.GetRoute(left.RouteIndex).RouteId.CompareTo(rightDefinition.GetRoute(right.RouteIndex).RouteId);
            if (result != 0) return result;
            return CompareOccurrences(_occurrences[left.OccurrenceIndex], _occurrences[right.OccurrenceIndex]);
        }

        private static int CompareOccurrences(GameplayTriggerOccurrence left, GameplayTriggerOccurrence right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            if (result != 0) return result;
            result = left.Instigator.CompareTo(right.Instigator);
            if (result != 0) return result;
            result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0) return result;
            result = left.SignalId.CompareTo(right.SignalId);
            if (result != 0) return result;
            if (left.Kind == GameplayTriggerOccurrenceKind.Pulsed)
            {
                result = left.OrderingValue.CompareTo(right.OrderingValue);
                if (result != 0) return result;
            }
            result = left.CurrentScalar.RawValue.CompareTo(right.CurrentScalar.RawValue);
            if (result != 0) return result;
            result = left.Payload0.CompareTo(right.Payload0);
            if (result != 0) return result;
            result = left.Payload1.CompareTo(right.Payload1);
            if (result != 0) return result;
            result = left.Payload2.CompareTo(right.Payload2);
            return result != 0 ? result : left.OrderingValue.CompareTo(right.OrderingValue);
        }

        private void SortOccurrencesAndAssignCanonicalOrdinals()
        {
            for (int i = 1; i < _occurrenceCount; i++)
            {
                GameplayTriggerOccurrence value = _occurrences[i];
                int j = i - 1;
                while (j >= 0 && CompareOccurrences(value, _occurrences[j]) < 0)
                {
                    _occurrences[j + 1] = _occurrences[j];
                    j--;
                }
                _occurrences[j + 1] = value;
            }

            for (int i = 0; i < _occurrenceCount; i++)
            {
                GameplayTriggerOccurrence occurrence = _occurrences[i];
                int instanceIndex = occurrence.InstanceId.Index - 1;
                GameplayTriggerSourceDefinition source = _catalog.GetSource(_instances[instanceIndex].SourceIndex);
                bool preserveOrderedSignalSequence =
                    occurrence.Kind == GameplayTriggerOccurrenceKind.Pulsed &&
                    source.SourceKind == GameplayTriggerSourceKind.Signal &&
                    source.SignalOrdering == GameplayTriggerSignalOrdering.Ordered;
                if (!preserveOrderedSignalSequence)
                    _occurrences[i] = occurrence.WithOrderingValue(i);
            }
        }

        private void SortBatches()
        {
            for (int i = 1; i < _batchCount; i++)
            {
                InputBatch value = _batches[i];
                int j = i - 1;
                while (j >= 0 && CompareBatches(value, _batches[j]) < 0) { _batches[j + 1] = _batches[j]; j--; }
                _batches[j + 1] = value;
            }
        }

        private static int CompareBatches(InputBatch left, InputBatch right)
        {
            int result = left.Frame.CompareTo(right.Frame);
            return result != 0 ? result : left.SourceBindingId.CompareTo(right.SourceBindingId);
        }

        private void SortCandidatesByIdentity()
        {
            for (int i = 1; i < _candidateCount; i++)
            {
                Candidate value = _candidates[i];
                int j = i - 1;
                while (j >= 0 && CompareCandidateIdentity(value, _candidates[j]) < 0) { _candidates[j + 1] = _candidates[j]; j--; }
                _candidates[j + 1] = value;
            }
        }

        private void SortCandidatesForExecution()
        {
            for (int i = 1; i < _candidateCount; i++)
            {
                Candidate value = _candidates[i];
                int j = i - 1;
                while (j >= 0 && CompareExecution(value, _candidates[j]) < 0) { _candidates[j + 1] = _candidates[j]; j--; }
                _candidates[j + 1] = value;
            }
        }

        private int CompareExecution(Candidate left, Candidate right)
        {
            int result = GetRoute(left).ExecutionOrder.CompareTo(GetRoute(right).ExecutionOrder);
            return result != 0 ? result : CompareCandidateIdentity(left, right);
        }

        private void SortAndDeduplicateOccupancy(int start, ref int count)
        {
            for (int i = start + 1; i < start + count; i++)
            {
                GameplayEntityId value = _occupancyInputs[i];
                int j = i - 1;
                while (j >= start && value.CompareTo(_occupancyInputs[j]) < 0) { _occupancyInputs[j + 1] = _occupancyInputs[j]; j--; }
                _occupancyInputs[j + 1] = value;
            }
            int write = 0;
            for (int read = 0; read < count; read++)
            {
                GameplayEntityId value = _occupancyInputs[start + read];
                if (write == 0 || _occupancyInputs[start + write - 1] != value) _occupancyInputs[start + write++] = value;
            }
            count = write;
        }

        private void SortScalars(int start, int count)
        {
            for (int i = start + 1; i < start + count; i++)
            {
                GameplayTriggerScalarSample value = _scalarInputs[i];
                int j = i - 1;
                while (j >= start && value.Subject.CompareTo(_scalarInputs[j].Subject) < 0) { _scalarInputs[j + 1] = _scalarInputs[j]; j--; }
                _scalarInputs[j + 1] = value;
            }
        }

        private void SortSignals(int start, int count)
        {
            for (int i = start + 1; i < start + count; i++)
            {
                GameplayTriggerSignal value = _signalInputs[i];
                int j = i - 1;
                while (j >= start && CompareSignals(value, _signalInputs[j]) < 0) { _signalInputs[j + 1] = _signalInputs[j]; j--; }
                _signalInputs[j + 1] = value;
            }
        }

        private static int CompareSignals(GameplayTriggerSignal left, GameplayTriggerSignal right)
        {
            int result = left.SignalId.CompareTo(right.SignalId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            if (result != 0) return result;
            result = left.Instigator.CompareTo(right.Instigator);
            if (result != 0) return result;
            result = left.Payload0.CompareTo(right.Payload0);
            if (result != 0) return result;
            result = left.Payload1.CompareTo(right.Payload1);
            return result != 0 ? result : left.Payload2.CompareTo(right.Payload2);
        }

        private static bool EntersThreshold(GameplayTriggerSourceDefinition source, Fix64 value) => source.ThresholdDirection == GameplayTriggerThresholdDirection.High ? value >= source.EnterThreshold : value <= source.EnterThreshold;
        private static bool RemainsThresholdActive(GameplayTriggerSourceDefinition source, Fix64 value) => source.ThresholdDirection == GameplayTriggerThresholdDirection.High ? value >= source.ExitThreshold : value <= source.ExitThreshold;
        private static long AddFrames(long frame, long delta) => delta > long.MaxValue - frame ? long.MaxValue : frame + delta;

        private struct InstanceState
        {
            public InstanceState(GameplayTriggerInstanceId instanceId, int bindingIndex, int definitionIndex, int sourceIndex, bool enabled)
            {
                InstanceId = instanceId;
                BindingIndex = bindingIndex;
                DefinitionIndex = definitionIndex;
                SourceIndex = sourceIndex;
                Enabled = enabled;
                ActiveCount = 0;
                AggregatePresenceEpoch = 0L;
            }
            public GameplayTriggerInstanceId InstanceId;
            public int BindingIndex;
            public int DefinitionIndex;
            public int SourceIndex;
            public bool Enabled;
            public int ActiveCount;
            public long AggregatePresenceEpoch;
        }

        private struct RelationState
        {
            public RelationState(int instanceIndex, GameplayEntityId subject)
            {
                InstanceIndex = instanceIndex;
                Subject = subject;
                Active = false;
                Primed = false;
                Scalar = default;
                PresenceEpoch = 0L;
                PresenceStartFrame = 0L;
                SeenFrame = -1L;
            }
            public int InstanceIndex;
            public GameplayEntityId Subject;
            public bool Active;
            public bool Primed;
            public Fix64 Scalar;
            public long PresenceEpoch;
            public long PresenceStartFrame;
            public long SeenFrame;
        }

        private struct RouteState
        {
            public RouteState(int instanceIndex, GameplayTriggerRouteId routeId, GameplayEntityId subject, GameplayEntityId instigator)
            {
                InstanceIndex = instanceIndex;
                RouteId = routeId;
                Subject = subject;
                Instigator = instigator;
                ActivationCount = 0;
                HasActivated = false;
                LastActivationFrame = 0L;
                LastActivatedPresenceEpoch = 0L;
                HasStaySchedule = false;
                NextStayFrame = 0L;
            }
            public int InstanceIndex;
            public GameplayTriggerRouteId RouteId;
            public GameplayEntityId Subject;
            public GameplayEntityId Instigator;
            public int ActivationCount;
            public bool HasActivated;
            public long LastActivationFrame;
            public long LastActivatedPresenceEpoch;
            public bool HasStaySchedule;
            public long NextStayFrame;
        }

        private struct InputBatch
        {
            public InputBatch(RuntimeFrame frame, GameplayTriggerSourceBindingId sourceBindingId, int instanceIndex, GameplayTriggerSourceKind kind, int start, int count)
            {
                Frame = frame;
                SourceBindingId = sourceBindingId;
                InstanceIndex = instanceIndex;
                Kind = kind;
                Start = start;
                Count = count;
            }
            public RuntimeFrame Frame;
            public GameplayTriggerSourceBindingId SourceBindingId;
            public int InstanceIndex;
            public GameplayTriggerSourceKind Kind;
            public int Start;
            public int Count;
        }

        private struct Candidate
        {
            public Candidate(int instanceIndex, int routeIndex, int occurrenceIndex)
            {
                InstanceIndex = instanceIndex;
                RouteIndex = routeIndex;
                OccurrenceIndex = occurrenceIndex;
                Rejection = GameplayTriggerErrorCode.None;
            }
            public int InstanceIndex;
            public int RouteIndex;
            public int OccurrenceIndex;
            public GameplayTriggerErrorCode Rejection;
        }

        private readonly struct PreparedAction
        {
            public PreparedAction(int sinkIndex, int token, GameplayTriggerActionPreparation preparation) { SinkIndex = sinkIndex; Token = token; Preparation = preparation; }
            public int SinkIndex { get; }
            public int Token { get; }
            public GameplayTriggerActionPreparation Preparation { get; }
        }
    }
}
