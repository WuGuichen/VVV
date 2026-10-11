using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct
        CharacterTestGameplayTriggerAttributeThresholdDescriptor
    {
        internal CharacterTestGameplayTriggerAttributeThresholdDescriptor(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSourceProviderId providerId,
            int attributeId)
        {
            SourceBindingId = sourceBindingId;
            ProviderId = providerId;
            AttributeId = attributeId;
        }

        internal GameplayTriggerSourceBindingId SourceBindingId { get; }
        internal GameplayTriggerSourceProviderId ProviderId { get; }
        internal int AttributeId { get; }
    }

    internal sealed class
        CharacterTestGameplayTriggerAttributeThresholdCatalog
    {
        internal const int CurrentHashSchemaVersion = 1;
        internal const string LocalPlayerHpStableSourceKey =
            "characterTest.attribute.localPlayerHp";

        private readonly
            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
            _descriptors;

        private CharacterTestGameplayTriggerAttributeThresholdCatalog(
            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
                descriptors,
            long catalogHash)
        {
            _descriptors = descriptors;
            CatalogHash = catalogHash;
        }

        /// <remarks>NoAlloc.</remarks>
        internal int Count => _descriptors.Length;

        /// <remarks>NoAlloc.</remarks>
        internal long CatalogHash { get; }

        /// <remarks>NoAlloc.</remarks>
        internal CharacterTestGameplayTriggerAttributeThresholdDescriptor Get(
            int index)
        {
            return _descriptors[index];
        }

        /// <remarks>
        /// AllocByDesign at composition/cache-build time. Copies, validates and
        /// canonicalizes trusted CharacterTest attribute source descriptors.
        /// </remarks>
        internal static bool TryCreate(
            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
                descriptors,
            GameplayTriggerCompiledCatalogPlan triggerPlan,
            out CharacterTestGameplayTriggerAttributeThresholdCatalog catalog,
            out string error)
        {
            catalog = null;
            if (descriptors == null
                || descriptors.Length == 0
                || triggerPlan == null)
            {
                error = "Attribute Threshold catalog inputs are missing.";
                return false;
            }

            var copy =
                (CharacterTestGameplayTriggerAttributeThresholdDescriptor[])
                descriptors.Clone();
            Array.Sort(
                copy,
                (left, right) =>
                    left.SourceBindingId.CompareTo(right.SourceBindingId));
            for (int i = 0; i < copy.Length; i++)
            {
                CharacterTestGameplayTriggerAttributeThresholdDescriptor row =
                    copy[i];
                if (!row.SourceBindingId.IsValid
                    || row.ProviderId.Value
                        != CharacterTestLevelGameplayTriggerAuthoringAdapter
                            .PlayerAttributeThresholdProviderId
                    || row.AttributeId
                        != CharacterTestCharacterAttributeIds.Hp)
                {
                    error =
                        "Attribute Threshold descriptor is not a supported current-player HP source.";
                    return false;
                }
                if (i > 0
                    && copy[i - 1].SourceBindingId
                        == row.SourceBindingId)
                {
                    error =
                        "Attribute Threshold source binding IDs must be unique.";
                    return false;
                }
                if (!TryFindRequirement(
                        triggerPlan,
                        row.SourceBindingId,
                        out GameplayTriggerCompiledSourceRequirement
                            requirement)
                    || requirement.SourceKind
                        != GameplayTriggerSourceKind.Threshold
                    || requirement.ProviderId.Value
                        != row.ProviderId.Value
                    || !requirement.CompleteBatchPerEvaluation
                    || requirement.MaxInputItemsPerEvaluation != 1
                    || requirement.MaxActiveRelations != 1
                    || requirement.EntityCapabilities
                        != (GameplayTriggerSourceEntityCapability
                                .SubjectPossible
                            | GameplayTriggerSourceEntityCapability
                                .SubjectGuaranteed
                            | GameplayTriggerSourceEntityCapability
                                .PresenceEpoch))
                {
                    error =
                        "Attribute Threshold descriptor does not match its compiled source requirement.";
                    return false;
                }
            }

            catalog =
                new CharacterTestGameplayTriggerAttributeThresholdCatalog(
                    copy,
                    ComputeCatalogHash(copy));
            error = string.Empty;
            return true;
        }

        private static bool TryFindRequirement(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerSourceBindingId sourceBindingId,
            out GameplayTriggerCompiledSourceRequirement requirement)
        {
            for (int i = 0; i < plan.SourceRequirementCount; i++)
            {
                GameplayTriggerCompiledSourceRequirement candidate =
                    plan.GetSourceRequirement(i);
                if (candidate.SourceBindingId == sourceBindingId)
                {
                    requirement = candidate;
                    return true;
                }
            }
            requirement = default;
            return false;
        }

        private static long ComputeCatalogHash(
            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
                descriptors)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt(
                "characterTest.trigger.attributeThreshold.schema",
                CurrentHashSchemaVersion);
            accumulator.AddStringStable(
                "characterTest.trigger.attributeThreshold.sourceKey",
                LocalPlayerHpStableSourceKey);
            accumulator.AddInt(
                "characterTest.trigger.attributeThreshold.count",
                descriptors.Length);
            for (int i = 0; i < descriptors.Length; i++)
            {
                CharacterTestGameplayTriggerAttributeThresholdDescriptor row =
                    descriptors[i];
                accumulator.AddInt(
                    "characterTest.trigger.attributeThreshold.sourceBindingId",
                    row.SourceBindingId.Value);
                accumulator.AddInt(
                    "characterTest.trigger.attributeThreshold.providerId",
                    row.ProviderId.Value);
                accumulator.AddInt(
                    "characterTest.trigger.attributeThreshold.attributeId",
                    row.AttributeId);
            }
            return accumulator.ToHash();
        }
    }

    internal readonly struct
        CharacterTestGameplayTriggerAttributeThresholdTickResult
    {
        internal CharacterTestGameplayTriggerAttributeThresholdTickResult(
            GameplayTriggerSourceBindingId sourceBindingId,
            bool hasSubject,
            GameplayEntityId subject,
            int sampledValue,
            GameplayTriggerResult submitResult)
        {
            SourceBindingId = sourceBindingId;
            HasSubject = hasSubject;
            Subject = subject;
            SampledValue = sampledValue;
            SubmitResult = submitResult;
        }

        internal GameplayTriggerSourceBindingId SourceBindingId { get; }
        internal bool HasSubject { get; }
        internal GameplayEntityId Subject { get; }
        internal int SampledValue { get; }
        internal GameplayTriggerResult SubmitResult { get; }
    }

    internal sealed class
        CharacterTestGameplayTriggerAttributeThresholdSystem :
            IGameplaySystem
    {
        internal const string StableSystemId =
            "character_test.level_gameplay_triggers.attribute_threshold";
        internal const int SystemPriority = 960;

        private readonly
            CharacterTestGameplayTriggerAttributeThresholdCatalog _catalog;
        private readonly GameplayTriggerRuntime _runtime;
        private readonly CharacterTestCharacterRuntimeWorld _world;
        private readonly GameplayTriggerScalarSample[] _sample;
        private readonly
            CharacterTestGameplayTriggerAttributeThresholdTickResult[]
            _lastResults;

        /// <remarks>
        /// AllocByDesign at composition. Tick is NoAlloc after initialization.
        /// </remarks>
        internal CharacterTestGameplayTriggerAttributeThresholdSystem(
            CharacterTestGameplayTriggerAttributeThresholdCatalog catalog,
            GameplayTriggerRuntime runtime,
            CharacterTestCharacterRuntimeWorld world)
        {
            _catalog = catalog ?? throw new ArgumentNullException(
                nameof(catalog));
            _runtime = runtime ?? throw new ArgumentNullException(
                nameof(runtime));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _sample = new GameplayTriggerScalarSample[1];
            _lastResults =
                new CharacterTestGameplayTriggerAttributeThresholdTickResult[
                    catalog.Count];
        }

        public string SystemId => StableSystemId;
        public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
        public int Priority => SystemPriority;
        public bool IsEnabled { get; private set; } = true;

        /// <remarks>NoAlloc.</remarks>
        internal int BindingResultCount => _lastResults.Length;

        /// <remarks>NoAlloc.</remarks>
        internal CharacterTestGameplayTriggerAttributeThresholdTickResult
            GetLastBindingResult(int index)
        {
            return _lastResults[index];
        }

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            CharacterTestCharacterEntity localPlayer = FindLocalPlayer();
            bool hasSubject =
                localPlayer != null && localPlayer.GameplayEntityId.IsValid;
            GameplayEntityId subject = hasSubject
                ? localPlayer.GameplayEntityId
                : default;
            int hp = hasSubject
                ? _world.GetCurrentAttribute(
                    subject,
                    CharacterTestCharacterAttributeIds.Hp)
                : 0;
            if (hasSubject)
            {
                _sample[0] = new GameplayTriggerScalarSample(
                    subject,
                    Fix64.FromInt(hp));
            }

            for (int i = 0; i < _catalog.Count; i++)
            {
                CharacterTestGameplayTriggerAttributeThresholdDescriptor row =
                    _catalog.Get(i);
                _runtime.TrySubmitScalarBatch(
                    context.Frame,
                    row.SourceBindingId,
                    hasSubject
                        ? new ReadOnlySpan<GameplayTriggerScalarSample>(
                            _sample,
                            0,
                            1)
                        : ReadOnlySpan<GameplayTriggerScalarSample>.Empty,
                    out GameplayTriggerResult result,
                    isComplete: true);
                _lastResults[i] =
                    new CharacterTestGameplayTriggerAttributeThresholdTickResult(
                        row.SourceBindingId,
                        hasSubject,
                        subject,
                        hp,
                        result);
            }
        }

        /// <remarks>NoAlloc.</remarks>
        internal void ResetDiagnostics()
        {
            Array.Clear(_lastResults, 0, _lastResults.Length);
        }

        private CharacterTestCharacterEntity FindLocalPlayer()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities =
                _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (candidate?.Control.Owner
                    == CharacterTestCharacterControlOwner.LocalPlayer)
                {
                    return candidate;
                }
            }
            return null;
        }
    }

    internal readonly struct CharacterTestGameplayTriggerBattleVitalsDescriptor
    {
        internal CharacterTestGameplayTriggerBattleVitalsDescriptor(GameplayTriggerSourceBindingId sourceBindingId,
            GameplayEntityId owner)
        { SourceBindingId = sourceBindingId; Owner = owner; }
        internal GameplayTriggerSourceBindingId SourceBindingId { get; }
        internal GameplayEntityId Owner { get; }
        internal int ProviderId => CharacterTestLevelGameplayTriggerAuthoringAdapter.BattleVitalsSignalProviderId;
    }

    /// <summary>Cold projection of the compiled Signal contracts; no second attribute or outcome authority.</summary>
    internal sealed class CharacterTestGameplayTriggerBattleVitalsCatalog
    {
        internal const int BattleVitalsSignalId = 450501;
        internal const GameplayTriggerSourceEntityCapability EntityCapabilities =
            GameplayTriggerSourceEntityCapability.SubjectPossible | GameplayTriggerSourceEntityCapability.SubjectGuaranteed |
            GameplayTriggerSourceEntityCapability.InstigatorPossible | GameplayTriggerSourceEntityCapability.InstigatorGuaranteed;
        private readonly CharacterTestGameplayTriggerBattleVitalsDescriptor[] _rows;
        internal long TriggerCatalogHash { get; }
        internal int Count => _rows.Length;
        internal CharacterTestGameplayTriggerBattleVitalsDescriptor Get(int index) => _rows[index];
        private CharacterTestGameplayTriggerBattleVitalsCatalog(CharacterTestGameplayTriggerBattleVitalsDescriptor[] rows, long hash)
        { _rows = rows; TriggerCatalogHash = hash; }

        internal static bool TryCreate(GameplayTriggerCompiledCatalogPlan plan,
            out CharacterTestGameplayTriggerBattleVitalsCatalog catalog, out string error)
        {
            catalog = null;
            error = "Battle vitals requires exactly two matching schema 1 unordered Signal contracts with a current roster owner.";
            if (plan == null) return false;
            var rows = new List<CharacterTestGameplayTriggerBattleVitalsDescriptor>(2);
            for (int i = 0; i < plan.SourceRequirementCount; i++)
            {
                var requirement = plan.GetSourceRequirement(i);
                if (requirement.ProviderId.Value != CharacterTestLevelGameplayTriggerAuthoringAdapter.BattleVitalsSignalProviderId) continue;
                if (requirement.SourceKind != GameplayTriggerSourceKind.Signal || requirement.EntityCapabilities != EntityCapabilities ||
                    !requirement.CompleteBatchPerEvaluation || requirement.MaxInputItemsPerEvaluation != 1 ||
                    requirement.MaxActiveRelations != 0 || requirement.SignalEnvelopeSchemaVersion != 1 ||
                    requirement.SignalOrdering != GameplayTriggerSignalOrdering.UnorderedMultiset || requirement.PreservesOrderedSequence) return false;
                GameplayEntityId owner = default;
                for (int j = 0; j < plan.BindingCount; j++)
                    if (plan.GetBinding(j).SourceBindingId == requirement.SourceBindingId) owner = plan.GetBinding(j).Owner;
                if (!owner.IsValid) return false;
                rows.Add(new CharacterTestGameplayTriggerBattleVitalsDescriptor(requirement.SourceBindingId, owner));
            }
            if (rows.Count != 2 || rows[0].Owner != rows[1].Owner) return false;
            rows.Sort((left, right) => left.SourceBindingId.CompareTo(right.SourceBindingId));
            catalog = new CharacterTestGameplayTriggerBattleVitalsCatalog(rows.ToArray(), plan.GameplayTriggerCatalogHash);
            error = string.Empty;
            return true;
        }
    }

    /// <summary>Run after damage/attribute writes and before Trigger evaluation, including ingress.</summary>
    internal sealed class CharacterTestGameplayTriggerBattleVitalsSystem : IGameplaySystem
    {
        private readonly CharacterTestGameplayTriggerBattleVitalsCatalog _catalog;
        private readonly GameplayTriggerRuntime _runtime;
        private readonly GameplayComponentWorld _world;
        private readonly TacticalStageFlowCoordinator _stage;
        private readonly int _stageDefinitionId;
        private readonly GameplayTriggerSignal[] _sample = new GameplayTriggerSignal[1];
        private long _lastFrame = -1;
        internal bool Armed { get; private set; }
        internal GameplayTriggerSignal LastSample { get; private set; }
        internal long LastSampleFrame { get; private set; } = -1;
        internal TacticalStageRejection Fault { get; private set; }
        internal GameplayTriggerResult LastSubmitResult { get; private set; }
        public string SystemId => "character_test.level_gameplay_triggers.battle_vitals";
        public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
        public int Priority => CharacterTestGameplayTriggerAttributeThresholdSystem.SystemPriority + 1;
        public bool IsEnabled { get; private set; } = true;
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        internal CharacterTestGameplayTriggerBattleVitalsSystem(CharacterTestGameplayTriggerBattleVitalsCatalog catalog,
            GameplayTriggerRuntime runtime, GameplayComponentWorld world, TacticalStageFlowCoordinator stage, int stageDefinitionId)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            if (stageDefinitionId <= 0) throw new ArgumentOutOfRangeException(nameof(stageDefinitionId));
            if (catalog.TriggerCatalogHash != runtime.GameplayTriggerCatalogHash) throw new ArgumentException("Battle source catalog/runtime mismatch.");
            _stageDefinitionId = stageDefinitionId;
        }

        public void Tick(GameplaySystemContext context) => Sample(context.Frame);

        /// <remarks>NoAlloc. Missing entities or missing HP fail closed; HP zero is valid only on a present entity.</remarks>
        internal void Sample(RuntimeFrame frame)
        {
            if (!IsEnabled || Fault != TacticalStageRejection.None || _stage.IsReleased ||
                _stage.Phase == TacticalStagePhase.StageCompleted || _stage.Health != TacticalStageHealth.Running) return;
            if (frame.Value <= _lastFrame) { Fail(TacticalStageRejection.StaleFrame); return; }
            _lastFrame = frame.Value;
            bool enabled = false;
            for (int i = 0; i < _catalog.Count; i++)
            {
                if (!_runtime.TryGetSourceEnabled(_catalog.Get(i).SourceBindingId, out bool current))
                { Fail(TacticalStageRejection.CriticalTriggerRejected); return; }
                enabled |= current;
            }
            if (!enabled) return;
            if (!_stage.TryGetRoster(out var player, out var opponent))
            {
                if (Armed) Fail(TacticalStageRejection.InvalidSpawnBinding);
                return;
            }
            Armed = true;
            if (player != _catalog.Get(0).Owner || !_world.IsAlive(player) || !_world.IsAlive(opponent) ||
                !_world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store) ||
                !store.TryGet(player, out var playerAttributes) || !store.TryGet(opponent, out var opponentAttributes) ||
                !playerAttributes.TryGet(CharacterTestCharacterAttributeIds.Hp, out var playerHp) ||
                !opponentAttributes.TryGet(CharacterTestCharacterAttributeIds.Hp, out var opponentHp))
            { Fail(TacticalStageRejection.InvalidBattleVitals); return; }
            var sample = new GameplayTriggerSignal(CharacterTestGameplayTriggerBattleVitalsCatalog.BattleVitalsSignalId,
                player, opponent, playerHp.CurrentValue, opponentHp.CurrentValue, _stageDefinitionId, sequence: 0L);
            _sample[0] = sample;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var source = _catalog.Get(i).SourceBindingId;
                _runtime.TryGetSourceEnabled(source, out bool current);
                if (!current) continue;
                bool accepted = _runtime.TrySubmitSignals(frame, source, GameplayTriggerSignalOrdering.UnorderedMultiset,
                    _sample, out var result, isComplete: true);
                LastSubmitResult = result;
                if (!accepted) { Fail(TacticalStageRejection.CriticalTriggerRejected); return; }
            }
            LastSample = sample;
            LastSampleFrame = frame.Value;
        }

        private void Fail(TacticalStageRejection fault) { Fault = fault; }
    }

    internal static class CharacterTestGameplayTriggerSourceCatalogHash
    {
        internal const int CurrentHashSchemaVersion = 1;

        /// <remarks>LowFreqAlloc: scene compilation/session preparation content fingerprint.</remarks>
        internal static long Compute(
            long triggerCatalogHash,
            long sensorCatalogHash,
            long attributeThresholdCatalogHash)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt(
                "characterTest.trigger.sourceCatalog.schema",
                CurrentHashSchemaVersion);
            accumulator.AddLong(
                "characterTest.trigger.sourceCatalog.trigger",
                triggerCatalogHash);
            accumulator.AddLong(
                "characterTest.trigger.sourceCatalog.combatSensor",
                sensorCatalogHash);
            accumulator.AddLong(
                "characterTest.trigger.sourceCatalog.attributeThreshold",
                attributeThresholdCatalogHash);
            return accumulator.ToHash();
        }
    }
}
