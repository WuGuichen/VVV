using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestMechanicSourceShape
    {
        None = 0,
        Sphere = 1,
    }

    internal enum CharacterTestMechanicEffectKind
    {
        None = 0,
        ApplyBuffOnTick = 1,
        SpawnVfxOnCreate = 2,
    }

    internal readonly struct CharacterTestMechanicEffectDefinition :
        IEquatable<CharacterTestMechanicEffectDefinition>
    {
        private CharacterTestMechanicEffectDefinition(
            int effectId,
            CharacterTestMechanicEffectKind kind,
            int buffId,
            int vfxDefinitionId,
            long vfxLifetimeFrames,
            int traceId,
            string traceLabel = "")
        {
            if (effectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectId), "Mechanic effect id must be positive.");
            if (!Enum.IsDefined(typeof(CharacterTestMechanicEffectKind), kind)
                || kind == CharacterTestMechanicEffectKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Mechanic effect kind is not supported.");
            }

            if (kind == CharacterTestMechanicEffectKind.ApplyBuffOnTick && buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "ApplyBuffOnTick requires a positive buff id.");
            if (kind == CharacterTestMechanicEffectKind.SpawnVfxOnCreate && vfxDefinitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(vfxDefinitionId), "SpawnVfxOnCreate requires a positive VFX definition id.");
            if (kind == CharacterTestMechanicEffectKind.SpawnVfxOnCreate && vfxLifetimeFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(vfxLifetimeFrames), "SpawnVfxOnCreate requires a positive lifetime.");
            if (kind == CharacterTestMechanicEffectKind.SpawnVfxOnCreate && traceId <= 0)
                throw new ArgumentException("SpawnVfxOnCreate requires a stable trace id.", nameof(traceId));

            EffectId = effectId;
            Kind = kind;
            BuffId = buffId;
            VfxDefinitionId = vfxDefinitionId;
            VfxLifetimeFrames = vfxLifetimeFrames;
            TraceId = traceId;
            TraceLabel = traceLabel ?? string.Empty;
        }

        public string TraceLabel { get; }
        public int EffectId { get; }
        public CharacterTestMechanicEffectKind Kind { get; }
        public int BuffId { get; }
        public int VfxDefinitionId { get; }
        public long VfxLifetimeFrames { get; }
        public int TraceId { get; }

        public static CharacterTestMechanicEffectDefinition ApplyBuffOnTick(
            int effectId,
            int buffId)
        {
            return new CharacterTestMechanicEffectDefinition(
                effectId,
                CharacterTestMechanicEffectKind.ApplyBuffOnTick,
                buffId,
                0,
                0L,
                0);
        }

        public static CharacterTestMechanicEffectDefinition SpawnVfxOnCreate(
            int effectId,
            int vfxDefinitionId,
            long vfxLifetimeFrames,
            int traceId,
            string traceLabel = "")
        {
            return new CharacterTestMechanicEffectDefinition(
                effectId,
                CharacterTestMechanicEffectKind.SpawnVfxOnCreate,
                0,
                vfxDefinitionId,
                vfxLifetimeFrames,
                traceId, traceLabel);
        }

        public bool Equals(CharacterTestMechanicEffectDefinition other)
        {
            return EffectId == other.EffectId
                && Kind == other.Kind
                && BuffId == other.BuffId
                && VfxDefinitionId == other.VfxDefinitionId
                && VfxLifetimeFrames == other.VfxLifetimeFrames
                && TraceId == other.TraceId
                && string.Equals(TraceLabel, other.TraceLabel, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestMechanicEffectDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EffectId;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ BuffId;
                hash = (hash * 397) ^ VfxDefinitionId;
                hash = (hash * 397) ^ VfxLifetimeFrames.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TraceLabel);
                return hash;
            }
        }
    }

    internal readonly struct CharacterTestMechanicSourceDefinition :
        IEquatable<CharacterTestMechanicSourceDefinition>
    {
        private readonly CharacterTestMechanicEffectDefinition[] _effects;

        public CharacterTestMechanicSourceDefinition(
            int sourceId,
            string stableId,
            CharacterTestMechanicSourceShape shape,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            GameplayTargetRelationFilter relationFilter,
            long durationFrames,
            CharacterActionId queryActionId,
            int fallbackTraceStableId,
            bool includeSourceEntity,
            params CharacterTestMechanicEffectDefinition[] effects)
            : this(
                sourceId,
                stableId,
                shape,
                radius,
                layerMask,
                relationFilter,
                durationFrames,
                queryActionId,
                fallbackTraceStableId,
                includeSourceEntity,
                tickIntervalFrames: 1,
                effects)
        {
        }

        public CharacterTestMechanicSourceDefinition(
            int sourceId,
            string stableId,
            CharacterTestMechanicSourceShape shape,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            GameplayTargetRelationFilter relationFilter,
            long durationFrames,
            CharacterActionId queryActionId,
            int fallbackTraceStableId,
            bool includeSourceEntity,
            int tickIntervalFrames,
            params CharacterTestMechanicEffectDefinition[] effects)
            : this(sourceId, stableId, shape, radius, layerMask, relationFilter,
                durationFrames, queryActionId, fallbackTraceStableId, string.Empty,
                includeSourceEntity, tickIntervalFrames, effects)
        {
        }

        public CharacterTestMechanicSourceDefinition(
            int sourceId,
            string stableId,
            CharacterTestMechanicSourceShape shape,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            GameplayTargetRelationFilter relationFilter,
            long durationFrames,
            CharacterActionId queryActionId,
            int fallbackTraceStableId,
            string fallbackTraceStableLabel,
            bool includeSourceEntity,
            int tickIntervalFrames,
            params CharacterTestMechanicEffectDefinition[] effects)
        {
            if (sourceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Mechanic source id must be positive.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Mechanic source stable id cannot be null or empty.", nameof(stableId));
            if (!Enum.IsDefined(typeof(CharacterTestMechanicSourceShape), shape)
                || shape == CharacterTestMechanicSourceShape.None)
            {
                throw new ArgumentOutOfRangeException(nameof(shape), "Mechanic source shape is not supported.");
            }

            if (shape == CharacterTestMechanicSourceShape.Sphere && radius <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere mechanic source radius must be positive.");
            if (!Enum.IsDefined(typeof(GameplayTargetRelationFilter), relationFilter))
                throw new ArgumentOutOfRangeException(nameof(relationFilter), "Mechanic source relation filter is not supported.");
            if (durationFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Mechanic source duration must be positive.");
            if (tickIntervalFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickIntervalFrames), "Mechanic source tick interval must be positive.");
            if (!queryActionId.IsValid)
                throw new ArgumentException("Mechanic source CharacterAction id must be positive.", nameof(queryActionId));
            if (fallbackTraceStableId <= 0)
                throw new ArgumentException("Mechanic source fallback trace stable id cannot be null or empty.", nameof(fallbackTraceStableId));

            SourceId = sourceId;
            StableId = stableId;
            Shape = shape;
            Radius = radius;
            LayerMask = layerMask;
            RelationFilter = relationFilter;
            DurationFrames = durationFrames;
            QueryActionId = queryActionId;
            FallbackTraceStableId = fallbackTraceStableId;
            FallbackTraceStableLabel = fallbackTraceStableLabel ?? string.Empty;
            IncludeSourceEntity = includeSourceEntity;
            TickIntervalFrames = tickIntervalFrames;
            _effects = CopySortedEffects(effects);
        }

        public int SourceId { get; }
        public string StableId { get; }
        public CharacterTestMechanicSourceShape Shape { get; }
        public Fix64 Radius { get; }
        public CombatPhysicsLayerMask LayerMask { get; }
        public GameplayTargetRelationFilter RelationFilter { get; }
        public long DurationFrames { get; }
        public CharacterActionId QueryActionId { get; }
        public int FallbackTraceStableId { get; }
        public string FallbackTraceStableLabel { get; }
        public bool IncludeSourceEntity { get; }
        public int TickIntervalFrames { get; }
        public int EffectCount => _effects?.Length ?? 0;

        public CharacterTestMechanicEffectDefinition GetEffect(int index)
        {
            if (_effects == null || index < 0 || index >= _effects.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _effects[index];
        }

        public CharacterTestMechanicEffectDefinition[] GetEffects()
        {
            if (_effects == null || _effects.Length == 0)
                return Array.Empty<CharacterTestMechanicEffectDefinition>();

            var copy = new CharacterTestMechanicEffectDefinition[_effects.Length];
            Array.Copy(_effects, copy, _effects.Length);
            return copy;
        }

        public bool Equals(CharacterTestMechanicSourceDefinition other)
        {
            if (SourceId != other.SourceId
                || !string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                || Shape != other.Shape
                || !Radius.Equals(other.Radius)
                || !LayerMask.Equals(other.LayerMask)
                || RelationFilter != other.RelationFilter
                || DurationFrames != other.DurationFrames
                || QueryActionId != other.QueryActionId
                || FallbackTraceStableId != other.FallbackTraceStableId
                || !string.Equals(FallbackTraceStableLabel, other.FallbackTraceStableLabel, StringComparison.Ordinal)
                || IncludeSourceEntity != other.IncludeSourceEntity
                || TickIntervalFrames != other.TickIntervalFrames
                || EffectCount != other.EffectCount)
            {
                return false;
            }

            for (int i = 0; i < EffectCount; i++)
            {
                if (!_effects[i].Equals(other._effects[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestMechanicSourceDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ (int)Shape;
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ LayerMask.GetHashCode();
                hash = (hash * 397) ^ (int)RelationFilter;
                hash = (hash * 397) ^ DurationFrames.GetHashCode();
                hash = (hash * 397) ^ QueryActionId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(FallbackTraceStableLabel);
                hash = (hash * 397) ^ IncludeSourceEntity.GetHashCode();
                hash = (hash * 397) ^ TickIntervalFrames;
                for (int i = 0; i < EffectCount; i++)
                    hash = (hash * 397) ^ _effects[i].GetHashCode();
                return hash;
            }
        }

        private static CharacterTestMechanicEffectDefinition[] CopySortedEffects(
            CharacterTestMechanicEffectDefinition[] effects)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<CharacterTestMechanicEffectDefinition>();

            var copy = new CharacterTestMechanicEffectDefinition[effects.Length];
            Array.Copy(effects, copy, effects.Length);
            Array.Sort(copy, CompareEffects);
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].EffectId <= 0)
                    throw new ArgumentException("Mechanic source definition cannot contain default effects.", nameof(effects));
                if (i > 0 && copy[i - 1].EffectId == copy[i].EffectId)
                    throw new ArgumentException("Mechanic source definition cannot contain duplicate effect ids.", nameof(effects));
            }

            return copy;
        }

        private static int CompareEffects(
            CharacterTestMechanicEffectDefinition left,
            CharacterTestMechanicEffectDefinition right)
        {
            return left.EffectId.CompareTo(right.EffectId);
        }
    }

    internal sealed class CharacterTestMechanicSourceDefinitionRegistry
    {
        private readonly CharacterTestMechanicSourceDefinition[] _definitions;

        public CharacterTestMechanicSourceDefinitionRegistry(
            params CharacterTestMechanicSourceDefinition[] definitions)
        {
            _definitions = CopySorted(definitions);
        }

        public int Count => _definitions.Length;

        public bool TryGet(int sourceId, out CharacterTestMechanicSourceDefinition definition)
        {
            if (sourceId <= 0)
            {
                definition = default;
                return false;
            }

            int left = 0;
            int right = _definitions.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _definitions[mid].SourceId;
                if (id == sourceId)
                {
                    definition = _definitions[mid];
                    return true;
                }

                if (id < sourceId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            definition = default;
            return false;
        }

        public bool TryGetByStableId(
            string stableId,
            out CharacterTestMechanicSourceDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                definition = default;
                return false;
            }

            for (int i = 0; i < _definitions.Length; i++)
            {
                if (string.Equals(_definitions[i].StableId, stableId, StringComparison.Ordinal))
                {
                    definition = _definitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public CharacterTestMechanicSourceDefinition[] ToArray()
        {
            if (_definitions.Length == 0)
                return Array.Empty<CharacterTestMechanicSourceDefinition>();

            var copy = new CharacterTestMechanicSourceDefinition[_definitions.Length];
            Array.Copy(_definitions, copy, _definitions.Length);
            return copy;
        }

        public int ComputeStableHash()
        {
            unchecked
            {
                int hash = _definitions.Length;
                for (int i = 0; i < _definitions.Length; i++)
                    hash = (hash * 397) ^ _definitions[i].GetHashCode();
                return hash;
            }
        }

        private static CharacterTestMechanicSourceDefinition[] CopySorted(
            CharacterTestMechanicSourceDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return Array.Empty<CharacterTestMechanicSourceDefinition>();

            var copy = new CharacterTestMechanicSourceDefinition[definitions.Length];
            Array.Copy(definitions, copy, definitions.Length);
            Array.Sort(copy, CompareDefinitions);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].SourceId <= 0)
                    throw new ArgumentException("Mechanic source registry cannot contain default definitions.", nameof(definitions));
                if (i > 0 && copy[i - 1].SourceId == copy[i].SourceId)
                    throw new ArgumentException("Mechanic source registry cannot contain duplicate source ids.", nameof(definitions));
                if (!stableIds.Add(copy[i].StableId))
                    throw new ArgumentException("Mechanic source registry cannot contain duplicate stable ids.", nameof(definitions));
            }

            return copy;
        }

        private static int CompareDefinitions(
            CharacterTestMechanicSourceDefinition left,
            CharacterTestMechanicSourceDefinition right)
        {
            return left.SourceId.CompareTo(right.SourceId);
        }
    }

    internal readonly struct CharacterTestMechanicSourceCreateResult
    {
        private CharacterTestMechanicSourceCreateResult(
            bool success,
            string failureReason,
            int runtimeSourceId,
            FixVector3 center,
            long startFrame,
            long endFrame,
            CharacterTestMechanicSourceDefinition definition)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            RuntimeSourceId = runtimeSourceId;
            Center = center;
            StartFrame = startFrame;
            EndFrame = endFrame;
            Definition = definition;
        }

        public bool Success { get; }
        public string FailureReason { get; }
        public int RuntimeSourceId { get; }
        public FixVector3 Center { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public CharacterTestMechanicSourceDefinition Definition { get; }

        public static CharacterTestMechanicSourceCreateResult Succeeded(
            int runtimeSourceId,
            FixVector3 center,
            long startFrame,
            long endFrame,
            CharacterTestMechanicSourceDefinition definition)
        {
            return new CharacterTestMechanicSourceCreateResult(
                true,
                string.Empty,
                runtimeSourceId,
                center,
                startFrame,
                endFrame,
                definition);
        }

        public static CharacterTestMechanicSourceCreateResult Failed(string failureReason)
        {
            return new CharacterTestMechanicSourceCreateResult(
                false,
                failureReason,
                0,
                FixVector3.Zero,
                0L,
                0L,
                default);
        }
    }

    internal readonly struct CharacterTestMechanicSourceTickSummary
    {
        public CharacterTestMechanicSourceTickSummary(
            int activeSourceCount,
            int createdSourceCount,
            int expiredSourceCount,
            int queryCount,
            int queryHitCount,
            int appliedOrRefreshedCount,
            int rejectedTargetCount,
            int skippedMissingGameplayTargetCount)
        {
            ActiveSourceCount = activeSourceCount;
            CreatedSourceCount = createdSourceCount;
            ExpiredSourceCount = expiredSourceCount;
            QueryCount = queryCount;
            QueryHitCount = queryHitCount;
            AppliedOrRefreshedCount = appliedOrRefreshedCount;
            RejectedTargetCount = rejectedTargetCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
        }

        public int ActiveSourceCount { get; }
        public int CreatedSourceCount { get; }
        public int ExpiredSourceCount { get; }
        public int QueryCount { get; }
        public int QueryHitCount { get; }
        public int AppliedOrRefreshedCount { get; }
        public int RejectedTargetCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }
    }

    internal sealed class CharacterTestMechanicSourceRuntime
    {
        private static readonly IReadOnlyList<RuntimeCommand> EmptyRuntimeCommands =
            Array.Empty<RuntimeCommand>();

        private readonly CharacterTestMechanicSourceDefinitionRegistry _definitions;
        private readonly GameplayBuffDefinitionRegistry _buffDefinitions;
        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly GameplayComponentTargetingService _targetingService;
        private readonly GameplayWorld _gameplayWorld = new GameplayWorld();
        private readonly GameplayCommandExecutionState _commandState =
            new GameplayCommandExecutionState();
        private readonly GameplayComponentBuffCleanupSystem _buffCleanupSystem;
        private readonly GameplayBuffApplicationSystem _buffApplicationSystem;
        private readonly GameplayBuffEffectProjectionSystem _buffProjectionSystem;
        private readonly List<ActiveMechanicSource> _sources =
            new List<ActiveMechanicSource>();
        private readonly List<CombatQueryResult> _queryResults =
            new List<CombatQueryResult>();
        private readonly List<GameplayComponentTargetCandidate> _targetCandidates =
            new List<GameplayComponentTargetCandidate>();
        private readonly HashSet<GameplayEntityId> _seenTargetIds =
            new HashSet<GameplayEntityId>();
        private int _nextRuntimeSourceId = 1;
        private int _createdSinceLastTick;

        public CharacterTestMechanicSourceRuntime(
            CharacterTestMechanicSourceDefinitionRegistry definitions,
            GameplayBuffDefinitionRegistry buffDefinitions,
            GameplayBuffRequestBuffer buffRequests = null,
            GameplayComponentTargetingService targetingService = null)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _buffDefinitions = buffDefinitions ?? throw new ArgumentNullException(nameof(buffDefinitions));
            _buffRequests = buffRequests ?? new GameplayBuffRequestBuffer();
            _targetingService = targetingService ?? new GameplayComponentTargetingService();
            _buffCleanupSystem = new GameplayComponentBuffCleanupSystem(
                "character_test.mechanic_source.buff_cleanup");
            _buffApplicationSystem = new GameplayBuffApplicationSystem(
                _buffDefinitions,
                _buffRequests,
                "character_test.mechanic_source.buff_application");
            _buffProjectionSystem = new GameplayBuffEffectProjectionSystem(
                _buffDefinitions,
                "character_test.mechanic_source.buff_projection");
        }

        public CharacterTestMechanicSourceTickSummary LastTickSummary { get; private set; }
        public int ActiveSourceCount => _sources.Count;
        public CharacterTestMechanicSourceDefinitionRegistry Definitions => _definitions;

        private CharacterTestMechanicSourceRuntimeState _hashCharacterTestMechanicSourceRuntimeState;

        internal CharacterTestMechanicSourceRuntimeState ReadStateView(int moduleId = 0)
        {
            if (_hashCharacterTestMechanicSourceRuntimeState == null)
            {
                var view = new CharacterTestRuntimeStateView<CharacterTestMechanicSourceSnapshot>(() => _sources.Count, i => _sources[i].ToSnapshot());
            _hashCharacterTestMechanicSourceRuntimeState = new CharacterTestMechanicSourceRuntimeState(
                CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                moduleId <= 0
                    ? CharacterTestMechanicSourceRuntimeState.DefaultModuleId
                    : moduleId,
                _nextRuntimeSourceId,
                view, borrow: true);
            }
            _hashCharacterTestMechanicSourceRuntimeState.NextRuntimeSourceId = _nextRuntimeSourceId;
            _hashCharacterTestMechanicSourceRuntimeState.ModuleId = moduleId <= 0 ? CharacterTestMechanicSourceRuntimeState.DefaultModuleId : moduleId;
            return _hashCharacterTestMechanicSourceRuntimeState;
        }

        internal CharacterTestMechanicSourceRuntimeState CreateStateSnapshot(int moduleId = 0)
        {
            var snapshots = new CharacterTestMechanicSourceSnapshot[_sources.Count];
            for (int i = 0; i < _sources.Count; i++)
                snapshots[i] = _sources[i].ToSnapshot();

            return new CharacterTestMechanicSourceRuntimeState(
                CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                moduleId <= 0
                    ? CharacterTestMechanicSourceRuntimeState.DefaultModuleId
                    : moduleId,
                _nextRuntimeSourceId,
                snapshots);
        }

        internal bool ValidateStateSnapshot(CharacterTestMechanicSourceRuntimeState state, out string failure)
        {
            failure = string.Empty;
            if (state == null) return true;
            if (state.SchemaVersion != CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion
                || (state.ModuleId != 1100 && state.ModuleId != 1102 && state.ModuleId != 1103))
            {
                failure = "Unsupported mechanic source snapshot version.";
                return false;
            }
            var runtimeIds = new HashSet<int>();
            foreach (CharacterTestMechanicSourceSnapshot snapshot in state.ActiveSources)
            {
                if (snapshot.RuntimeSourceId <= 0 || !runtimeIds.Add(snapshot.RuntimeSourceId)
                    || snapshot.SourceGameplayEntityIndex <= 0 || snapshot.SourceGameplayEntityGeneration <= 0
                    || !_definitions.TryGet(snapshot.SourceDefinitionId, out _)
                    || snapshot.TraceId < 0 || (snapshot.TraceId > 0 && !CharacterTestAuthoredContentIds.IsActive(401, snapshot.TraceId)))
                {
                    failure = "Mechanic source snapshot contains an invalid or unknown integer identity.";
                    return false;
                }
            }
            return true;
        }

        internal bool TryApplyStateSnapshot(
            CharacterTestMechanicSourceRuntimeState state,
            out string failure)
        {
            if (!ValidateStateSnapshot(state, out failure)) return false;
            _sources.Clear();
            _queryResults.Clear();
            _targetCandidates.Clear();
            _seenTargetIds.Clear();
            _createdSinceLastTick = 0;

            if (state == null || state.ActiveCount == 0)
            {
                _nextRuntimeSourceId = state != null ? state.NextRuntimeSourceId : 1;
                LastTickSummary = new CharacterTestMechanicSourceTickSummary(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
                return true;
            }

            _nextRuntimeSourceId = state.NextRuntimeSourceId;
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> snapshots = state.ActiveSources;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestMechanicSourceSnapshot snapshot = snapshots[i];
                if (!_definitions.TryGet(
                        snapshot.SourceDefinitionId,
                        out CharacterTestMechanicSourceDefinition definition))
                {
                    failure = "Mechanic source definition is missing. sourceDefinitionId="
                        + snapshot.SourceDefinitionId;
                    _sources.Clear();
                    return false;
                }

                if (snapshot.SourceGameplayEntityIndex <= 0
                    || snapshot.SourceGameplayEntityGeneration <= 0)
                {
                    failure = "Mechanic source gameplay entity id is invalid. runtimeSourceId="
                        + snapshot.RuntimeSourceId;
                    _sources.Clear();
                    return false;
                }

                _sources.Add(new ActiveMechanicSource(
                    snapshot.RuntimeSourceId,
                    definition,
                    new GameplayEntityId(
                        snapshot.SourceGameplayEntityIndex,
                        snapshot.SourceGameplayEntityGeneration),
                    new CombatEntityId(snapshot.SourceCombatEntityId),
                    snapshot.Center,
                    snapshot.StartFrame,
                    snapshot.EndFrame,
                    snapshot.TraceId));
            }

            LastTickSummary = new CharacterTestMechanicSourceTickSummary(
                _sources.Count,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
            return true;
        }

        public CharacterTestMechanicSourceCreateResult CreateSource(
            int sourceDefinitionId,
            GameplayEntityId sourceGameplayEntityId,
            CombatEntityId sourceCombatEntityId,
            FixVector3 center,
            RuntimeFrame frame,
            int traceId)
        {
            if (!sourceGameplayEntityId.IsValid)
                return CharacterTestMechanicSourceCreateResult.Failed("Source gameplay entity is missing.");
            if (sourceCombatEntityId.IsNone)
                return CharacterTestMechanicSourceCreateResult.Failed("Source combat entity is missing.");
            if (!_definitions.TryGet(sourceDefinitionId, out CharacterTestMechanicSourceDefinition definition))
                return CharacterTestMechanicSourceCreateResult.Failed("Mechanic source definition is missing.");

            long endFrame;
            try
            {
                endFrame = checked(frame.Value + definition.DurationFrames);
            }
            catch (OverflowException)
            {
                return CharacterTestMechanicSourceCreateResult.Failed("Mechanic source duration overflowed.");
            }

            int runtimeSourceId = _nextRuntimeSourceId++;
            if (_nextRuntimeSourceId <= 0)
                _nextRuntimeSourceId = 1;

            _sources.Add(new ActiveMechanicSource(
                runtimeSourceId,
                definition,
                sourceGameplayEntityId,
                sourceCombatEntityId,
                center,
                frame.Value,
                endFrame,
                traceId));
            _createdSinceLastTick++;
            LastTickSummary = new CharacterTestMechanicSourceTickSummary(
                _sources.Count,
                1,
                0,
                0,
                0,
                0,
                0,
                0);
            return CharacterTestMechanicSourceCreateResult.Succeeded(
                runtimeSourceId,
                center,
                frame.Value,
                endFrame,
                definition);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            int expiredCount = RemoveExpired(frame);
            int queryCount = 0;
            int queryHitCount = 0;
            int appliedOrRefreshedCount = 0;
            int rejectedTargetCount = 0;
            int skippedMissingGameplayTargetCount = 0;
            int createdCount = _createdSinceLastTick;
            _createdSinceLastTick = 0;

            if (world != null && physicsRuntime?.World != null)
            {
                for (int i = 0; i < _sources.Count; i++)
                {
                    ActiveMechanicSource source = _sources[i];
                    if (source.Definition.Shape != CharacterTestMechanicSourceShape.Sphere)
                        continue;
                    if (!ShouldTickSource(source, frame))
                        continue;

                    _queryResults.Clear();
                    physicsRuntime.World.QuerySphere(
                        BuildSphereQuery(source, i),
                        _queryResults,
                        includeSourceEntity: source.Definition.IncludeSourceEntity);
                    queryCount++;
                    queryHitCount += _queryResults.Count;

                    GameplayComponentTargetingResult targetingResult = SelectTargets(
                        world,
                        source,
                        ref skippedMissingGameplayTargetCount);
                    rejectedTargetCount += targetingResult.RejectedCount;

                    IReadOnlyList<GameplayComponentTargetCandidate> selectedTargets =
                        targetingResult.SelectedTargets;
                    for (int targetIndex = 0; targetIndex < selectedTargets.Count; targetIndex++)
                    {
                        appliedOrRefreshedCount += ApplyTickEffects(
                            selectedTargets[targetIndex].EntityId,
                            source);
                    }
                }

                ApplyBuffRequestsAndProject(world, frame);
            }

            LastTickSummary = new CharacterTestMechanicSourceTickSummary(
                _sources.Count,
                createdCount,
                expiredCount,
                queryCount,
                queryHitCount,
                appliedOrRefreshedCount,
                rejectedTargetCount,
                skippedMissingGameplayTargetCount);
        }

        private int ApplyTickEffects(
            GameplayEntityId targetEntityId,
            ActiveMechanicSource source)
        {
            int appliedCount = 0;
            for (int effectIndex = 0; effectIndex < source.Definition.EffectCount; effectIndex++)
            {
                CharacterTestMechanicEffectDefinition effect = source.Definition.GetEffect(effectIndex);
                if (effect.Kind != CharacterTestMechanicEffectKind.ApplyBuffOnTick)
                    continue;

                _buffRequests.EnqueueApply(
                    targetEntityId,
                    effect.BuffId,
                    sourceId: source.Definition.SourceId,
                    traceId: new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, source.SourceGameplayEntityId.Index, source.RuntimeSourceId, source.TraceId),
                    commandId: 0);
                appliedCount++;
            }

            return appliedCount;
        }

        private GameplayComponentTargetingResult SelectTargets(
            CharacterTestCharacterRuntimeWorld world,
            ActiveMechanicSource source,
            ref int skippedMissingGameplayTargetCount)
        {
            _targetCandidates.Clear();
            _seenTargetIds.Clear();

            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult result = _queryResults[i];
                if (!world.CombatGameplayMap.TryGetGameplayId(result.TargetEntityId, out GameplayEntityId targetEntityId))
                {
                    skippedMissingGameplayTargetCount++;
                    continue;
                }

                if (!_seenTargetIds.Add(targetEntityId))
                    continue;

                if (!GameplayComponentTargetCandidates.TryCreateFromWorld(
                        world.GameplayComponentWorld,
                        targetEntityId,
                        out GameplayComponentTargetCandidate candidate))
                {
                    skippedMissingGameplayTargetCount++;
                    continue;
                }

                _targetCandidates.Add(candidate);
            }

            var query = new GameplayComponentTargetQuery(
                source.SourceGameplayEntityId,
                GetTeamId(world.GameplayComponentWorld, source.SourceGameplayEntityId),
                requireAlive: true,
                relationFilter: source.Definition.RelationFilter);
            return _targetingService.Select(query, _targetCandidates);
        }

        private void ApplyBuffRequestsAndProject(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            _commandState.Clear();
            var context = new GameplaySystemContext(
                frame,
                0d,
                0d,
                _gameplayWorld,
                EmptyRuntimeCommands,
                world.GameplayComponentWorld.Events,
                _commandState,
                componentWorld: world.GameplayComponentWorld);
            _buffCleanupSystem.Tick(context);
            _buffApplicationSystem.Tick(context);
            _buffProjectionSystem.Tick(context);
        }

        private int RemoveExpired(RuntimeFrame frame)
        {
            int removed = 0;
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                if (frame.Value < _sources[i].EndFrame)
                    continue;

                _sources.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private static CombatSphereQuery BuildSphereQuery(
            ActiveMechanicSource source,
            int sourceOrder)
        {
            CharacterTestMechanicSourceDefinition definition = source.Definition;
            int traceId = source.TraceId <= 0
                ? definition.FallbackTraceStableId
                : source.TraceId;
            var header = new CombatQueryHeader(
                queryId: source.RuntimeSourceId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: source.SourceCombatEntityId,
                traceId: traceId,
                actionId: CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    definition.QueryActionId),
                sourceOrder: sourceOrder,
                layerMask: definition.LayerMask);
            return new CombatSphereQuery(header, source.Center, definition.Radius);
        }

        private static int GetTeamId(GameplayComponentWorld componentWorld, GameplayEntityId entityId)
        {
            if (componentWorld.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams) &&
                teams.TryGet(entityId, out GameplayTeamComponent team))
            {
                return team.TeamId;
            }

            return 0;
        }

        private static bool ShouldTickSource(
            ActiveMechanicSource source,
            RuntimeFrame frame)
        {
            long elapsed = frame.Value - source.StartFrame;
            return elapsed >= 0L && elapsed % source.Definition.TickIntervalFrames == 0L;
        }

        private readonly struct ActiveMechanicSource
        {
            public ActiveMechanicSource(
                int runtimeSourceId,
                CharacterTestMechanicSourceDefinition definition,
                GameplayEntityId sourceGameplayEntityId,
                CombatEntityId sourceCombatEntityId,
                FixVector3 center,
                long startFrame,
                long endFrame,
                int traceId)
            {
                RuntimeSourceId = runtimeSourceId;
                Definition = definition;
                SourceGameplayEntityId = sourceGameplayEntityId;
                SourceCombatEntityId = sourceCombatEntityId;
                Center = center;
                StartFrame = startFrame;
                EndFrame = endFrame;
                TraceId = traceId;
            }

            public int RuntimeSourceId { get; }
            public CharacterTestMechanicSourceDefinition Definition { get; }
            public GameplayEntityId SourceGameplayEntityId { get; }
            public CombatEntityId SourceCombatEntityId { get; }
            public FixVector3 Center { get; }
            public long StartFrame { get; }
            public long EndFrame { get; }
            public int TraceId { get; }

            public CharacterTestMechanicSourceSnapshot ToSnapshot()
            {
                return CharacterTestMechanicSourceSnapshot.FromVector(
                    RuntimeSourceId,
                    Definition.SourceId,
                    SourceGameplayEntityId.Index,
                    SourceGameplayEntityId.Generation,
                    SourceCombatEntityId.Value,
                    Center,
                    StartFrame,
                    EndFrame,
                    TraceId);
            }
        }
    }
}
