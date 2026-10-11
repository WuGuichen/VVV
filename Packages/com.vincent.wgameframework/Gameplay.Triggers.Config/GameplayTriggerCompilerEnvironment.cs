using System;
using System.Collections.Generic;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>Typed stable authority-provider identity. Default is invalid.</summary>
    public readonly struct GameplayTriggerSourceProviderId : IComparable<GameplayTriggerSourceProviderId>, IEquatable<GameplayTriggerSourceProviderId>
    {
        /// <remarks>NoAlloc. Invalid values throw on programmer-created typed IDs; config rows use TryCreate registries.</remarks>
        public GameplayTriggerSourceProviderId(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        /// <remarks>NoAlloc.</remarks>
        public int Value { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool IsValid => Value > 0;
        /// <remarks>NoAlloc.</remarks>
        public int CompareTo(GameplayTriggerSourceProviderId other) => Value.CompareTo(other.Value);
        /// <remarks>NoAlloc.</remarks>
        public bool Equals(GameplayTriggerSourceProviderId other) => Value == other.Value;
        /// <remarks>NoAlloc.</remarks>
        public override bool Equals(object obj) => obj is GameplayTriggerSourceProviderId other && Equals(other);
        /// <remarks>NoAlloc.</remarks>
        public override int GetHashCode() => Value;
    }

    /// <summary>Typed ID namespace used by the trusted retirement registry.</summary>
    public enum GameplayTriggerConfigIdKind
    {
        SourceDefinition = 1,
        Condition = 2,
        Definition = 3,
        Route = 4,
        Binding = 5,
        SourceBinding = 6,
        ActionKind = 7,
        Provider = 8
    }

    /// <summary>Trusted typed-ID tombstone.</summary>
    public readonly struct GameplayTriggerRetiredIdDescriptor
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRetiredIdDescriptor(GameplayTriggerConfigIdKind idKind, int primaryId, int secondaryId = 0)
        {
            IdKind = idKind;
            PrimaryId = primaryId;
            SecondaryId = secondaryId;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigIdKind IdKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public int PrimaryId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SecondaryId { get; }
    }

    /// <summary>Trusted immutable retired-ID registry.</summary>
    public sealed class GameplayTriggerRetiredIdRegistry
    {
        private readonly GameplayTriggerRetiredIdDescriptor[] _rows;

        private GameplayTriggerRetiredIdRegistry(GameplayTriggerRetiredIdDescriptor[] rows) { _rows = rows; }
        /// <remarks>NoAlloc.</remarks>
        public int Count => _rows.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRetiredIdDescriptor Get(int index) => _rows[index];

        /// <remarks>AllocByDesign. Copies, validates and canonicalizes registry rows.</remarks>
        public static bool TryCreate(GameplayTriggerRetiredIdDescriptor[] rows, out GameplayTriggerRetiredIdRegistry registry, out GameplayTriggerConfigRegistryResult result)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            GameplayTriggerRetiredIdDescriptor[] copy = rows == null ? Array.Empty<GameplayTriggerRetiredIdDescriptor>() : (GameplayTriggerRetiredIdDescriptor[])rows.Clone();
            Array.Sort(copy, Compare);
            for (int i = 0; i < copy.Length; i++)
            {
                GameplayTriggerRetiredIdDescriptor row = copy[i];
                bool validKind = row.IdKind >= GameplayTriggerConfigIdKind.SourceDefinition && row.IdKind <= GameplayTriggerConfigIdKind.Provider;
                bool validSecondary = row.IdKind == GameplayTriggerConfigIdKind.Route ? row.SecondaryId > 0 : row.SecondaryId == 0;
                string path = "$.environment.retiredIds[kind=" + (int)row.IdKind + ",primary=" + row.PrimaryId + ",secondary=" + row.SecondaryId + "]";
                if (!validKind || row.PrimaryId <= 0 || !validSecondary)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, path, row.PrimaryId, row.SecondaryId));
                if (i > 0 && Compare(copy[i - 1], row) == 0)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, path, row.PrimaryId, row.SecondaryId, 1));
            }
            result = new GameplayTriggerConfigRegistryResult(diagnostics);
            registry = result.Succeeded ? new GameplayTriggerRetiredIdRegistry(copy) : null;
            return result.Succeeded;
        }

        internal bool Contains(GameplayTriggerConfigIdKind kind, int primaryId, int secondaryId = 0)
        {
            var key = new GameplayTriggerRetiredIdDescriptor(kind, primaryId, secondaryId);
            return Array.BinarySearch(_rows, key, Comparer<GameplayTriggerRetiredIdDescriptor>.Create(Compare)) >= 0;
        }

        private static int Compare(GameplayTriggerRetiredIdDescriptor left, GameplayTriggerRetiredIdDescriptor right)
        {
            int value = left.IdKind.CompareTo(right.IdKind);
            if (value != 0) return value;
            value = left.PrimaryId.CompareTo(right.PrimaryId);
            return value != 0 ? value : left.SecondaryId.CompareTo(right.SecondaryId);
        }
    }

    /// <summary>Trusted source entity and presence capabilities.</summary>
    [Flags]
    public enum GameplayTriggerSourceEntityCapability
    {
        None = 0,
        SubjectPossible = 1,
        SubjectGuaranteed = 2,
        InstigatorPossible = 4,
        InstigatorGuaranteed = 8,
        PresenceEpoch = 16
    }

    /// <summary>Trusted provider/evaluation contract for one SourceBindingId.</summary>
    public readonly struct GameplayTriggerSourceBindingContractDescriptor
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingContractDescriptor(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSourceDefinitionId sourceDefinitionId,
            GameplayTriggerSourceKind sourceKind,
            GameplayTriggerSourceProviderId providerId,
            GameplayTriggerSourceEntityCapability entityCapabilities,
            bool completeBatchPerEvaluation,
            int maxInputItemsPerEvaluation,
            int maxActiveRelations,
            int signalEnvelopeSchemaVersion = 0,
            GameplayTriggerSignalOrdering signalOrdering = GameplayTriggerSignalOrdering.UnorderedMultiset,
            bool preservesOrderedSequence = false)
        {
            SourceBindingId = sourceBindingId;
            SourceDefinitionId = sourceDefinitionId;
            SourceKind = sourceKind;
            ProviderId = providerId;
            EntityCapabilities = entityCapabilities;
            CompleteBatchPerEvaluation = completeBatchPerEvaluation;
            MaxInputItemsPerEvaluation = maxInputItemsPerEvaluation;
            MaxActiveRelations = maxActiveRelations;
            SignalEnvelopeSchemaVersion = signalEnvelopeSchemaVersion;
            SignalOrdering = signalOrdering;
            PreservesOrderedSequence = preservesOrderedSequence;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceDefinitionId SourceDefinitionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceKind SourceKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceProviderId ProviderId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceEntityCapability EntityCapabilities { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool CompleteBatchPerEvaluation { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputItemsPerEvaluation { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActiveRelations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SignalEnvelopeSchemaVersion { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSignalOrdering SignalOrdering { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool PreservesOrderedSequence { get; }
    }

    /// <summary>Trusted immutable SourceBinding provider/capability registry.</summary>
    public sealed class GameplayTriggerSourceBindingContractRegistry
    {
        private readonly GameplayTriggerSourceBindingContractDescriptor[] _rows;
        private GameplayTriggerSourceBindingContractRegistry(GameplayTriggerSourceBindingContractDescriptor[] rows) { _rows = rows; }
        /// <remarks>NoAlloc.</remarks>
        public int Count => _rows.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingContractDescriptor Get(int index) => _rows[index];

        /// <remarks>AllocByDesign. ProviderId may repeat; SourceBindingId must be unique.</remarks>
        public static bool TryCreate(GameplayTriggerSourceBindingContractDescriptor[] rows, out GameplayTriggerSourceBindingContractRegistry registry, out GameplayTriggerConfigRegistryResult result)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            GameplayTriggerSourceBindingContractDescriptor[] copy = rows == null ? Array.Empty<GameplayTriggerSourceBindingContractDescriptor>() : (GameplayTriggerSourceBindingContractDescriptor[])rows.Clone();
            Array.Sort(copy, (a, b) => a.SourceBindingId.CompareTo(b.SourceBindingId));
            for (int i = 0; i < copy.Length; i++)
            {
                GameplayTriggerSourceBindingContractDescriptor row = copy[i];
                int id = row.SourceBindingId.Value;
                string path = "$.environment.sourceBindings[id=" + id + "]";
                if (!row.SourceBindingId.IsValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path + ".sourceBindingId", id));
                if (!row.SourceDefinitionId.IsValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path + ".sourceDefinitionId", id, row.SourceDefinitionId.Value));
                if (!row.ProviderId.IsValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceProviderMissing, path + ".provider", id, row.ProviderId.Value));
                if (i > 0 && copy[i - 1].SourceBindingId == row.SourceBindingId)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId, path, id));
                if (row.SourceKind < GameplayTriggerSourceKind.Signal || row.SourceKind > GameplayTriggerSourceKind.Threshold)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceKindContractMismatch, path + ".sourceKind", id, (int)row.SourceKind));
                GameplayTriggerSourceEntityCapability capabilities = row.EntityCapabilities;
                if ((capabilities & ~(GameplayTriggerSourceEntityCapability.SubjectPossible | GameplayTriggerSourceEntityCapability.SubjectGuaranteed | GameplayTriggerSourceEntityCapability.InstigatorPossible | GameplayTriggerSourceEntityCapability.InstigatorGuaranteed | GameplayTriggerSourceEntityCapability.PresenceEpoch)) != 0 ||
                    ((capabilities & GameplayTriggerSourceEntityCapability.SubjectGuaranteed) != 0 && (capabilities & GameplayTriggerSourceEntityCapability.SubjectPossible) == 0) ||
                    ((capabilities & GameplayTriggerSourceEntityCapability.InstigatorGuaranteed) != 0 && (capabilities & GameplayTriggerSourceEntityCapability.InstigatorPossible) == 0) ||
                    (row.SourceKind == GameplayTriggerSourceKind.Signal && (capabilities & GameplayTriggerSourceEntityCapability.PresenceEpoch) != 0))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceCapabilityMismatch, path + ".entityCapabilities", id, 0, (long)capabilities));
                if (!row.CompleteBatchPerEvaluation || row.MaxInputItemsPerEvaluation <= 0 ||
                    (row.SourceKind == GameplayTriggerSourceKind.Signal ? row.MaxActiveRelations != 0 : row.MaxActiveRelations <= 0))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceEvaluationContractInvalid, path + ".evaluation", id, 0, row.MaxInputItemsPerEvaluation, row.MaxActiveRelations));
                bool signalMetadataValid = row.SourceKind == GameplayTriggerSourceKind.Signal
                    ? row.SignalEnvelopeSchemaVersion == 1 &&
                      (row.SignalOrdering == GameplayTriggerSignalOrdering.Ordered || row.SignalOrdering == GameplayTriggerSignalOrdering.UnorderedMultiset) &&
                      (row.SignalOrdering == GameplayTriggerSignalOrdering.Ordered ? row.PreservesOrderedSequence : !row.PreservesOrderedSequence)
                    : row.SignalEnvelopeSchemaVersion == 0 &&
                      row.SignalOrdering == GameplayTriggerSignalOrdering.UnorderedMultiset &&
                      !row.PreservesOrderedSequence;
                if (!signalMetadataValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata, path + ".replay", id));
            }
            result = new GameplayTriggerConfigRegistryResult(diagnostics);
            registry = result.Succeeded ? new GameplayTriggerSourceBindingContractRegistry(copy) : null;
            return result.Succeeded;
        }

        internal bool TryGet(GameplayTriggerSourceBindingId id, out GameplayTriggerSourceBindingContractDescriptor descriptor)
        {
            int low = 0;
            int high = _rows.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _rows[middle].SourceBindingId.CompareTo(id);
                if (comparison == 0) { descriptor = _rows[middle]; return true; }
                if (comparison < 0) low = middle + 1; else high = middle - 1;
            }
            descriptor = default;
            return false;
        }
    }

    /// <summary>Trusted compile-time action requirement; actual composition facts are excluded.</summary>
    public readonly struct GameplayTriggerActionRequirementDescriptor
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionRequirementDescriptor(GameplayTriggerActionKindId actionKindId, GameplayTriggerDeliveryContract deliveryContract, GameplayTriggerInternalDeferredOperationKind internalOperationKind = GameplayTriggerInternalDeferredOperationKind.None, int signalId = 0)
        {
            ActionKindId = actionKindId;
            DeliveryContract = deliveryContract;
            InternalOperationKind = internalOperationKind;
            SignalId = signalId;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionKindId ActionKindId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerDeliveryContract DeliveryContract { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerInternalDeferredOperationKind InternalOperationKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SignalId { get; }
    }

    /// <summary>Trusted immutable action requirement registry.</summary>
    public sealed class GameplayTriggerActionRequirementRegistry
    {
        private readonly GameplayTriggerActionRequirementDescriptor[] _rows;
        private GameplayTriggerActionRequirementRegistry(GameplayTriggerActionRequirementDescriptor[] rows) { _rows = rows; }
        /// <remarks>NoAlloc.</remarks>
        public int Count => _rows.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionRequirementDescriptor Get(int index) => _rows[index];

        /// <remarks>AllocByDesign. Copies, validates and canonicalizes registry rows.</remarks>
        public static bool TryCreate(GameplayTriggerActionRequirementDescriptor[] rows, out GameplayTriggerActionRequirementRegistry registry, out GameplayTriggerConfigRegistryResult result)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            GameplayTriggerActionRequirementDescriptor[] copy = rows == null ? Array.Empty<GameplayTriggerActionRequirementDescriptor>() : (GameplayTriggerActionRequirementDescriptor[])rows.Clone();
            Array.Sort(copy, (a, b) => a.ActionKindId.CompareTo(b.ActionKindId));
            for (int i = 0; i < copy.Length; i++)
            {
                GameplayTriggerActionRequirementDescriptor row = copy[i];
                int id = row.ActionKindId.Value;
                string path = "$.environment.actionRequirements[kind=" + id + "]";
                if (!row.ActionKindId.IsValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path + ".actionKindId", id));
                if (row.DeliveryContract < GameplayTriggerDeliveryContract.SameFrameReserved || row.DeliveryContract > GameplayTriggerDeliveryContract.InternalDeferred)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidActionContract, path + ".deliveryContract", id));
                if (i > 0 && copy[i - 1].ActionKindId == row.ActionKindId)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.DuplicateActionKind, path, id));
                bool internalValid = row.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred
                    ? (row.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.SetEnabled || row.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Reset || row.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal) &&
                      (row.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal ? row.SignalId > 0 : row.SignalId == 0)
                    : row.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.None && row.SignalId == 0;
                if (!internalValid)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidActionContract, path + ".internalDescriptor", id));
            }
            result = new GameplayTriggerConfigRegistryResult(diagnostics);
            registry = result.Succeeded ? new GameplayTriggerActionRequirementRegistry(copy) : null;
            return result.Succeeded;
        }

        internal bool TryGet(GameplayTriggerActionKindId id, out GameplayTriggerActionRequirementDescriptor descriptor)
        {
            for (int i = 0; i < _rows.Length; i++) if (_rows[i].ActionKindId == id) { descriptor = _rows[i]; return true; }
            descriptor = default;
            return false;
        }
    }

    /// <summary>Trusted imported/expected compiled catalog identity.</summary>
    public readonly struct GameplayTriggerExpectedCatalogIdentity
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerExpectedCatalogIdentity(string runtimeCatalogSchemaId, int runtimeCatalogSchemaVersion, bool hasExpectedGameplayTriggerCatalogHash = false, long expectedGameplayTriggerCatalogHash = 0L)
        {
            RuntimeCatalogSchemaId = runtimeCatalogSchemaId ?? string.Empty;
            RuntimeCatalogSchemaVersion = runtimeCatalogSchemaVersion;
            HasExpectedGameplayTriggerCatalogHash = hasExpectedGameplayTriggerCatalogHash;
            ExpectedGameplayTriggerCatalogHash = expectedGameplayTriggerCatalogHash;
        }

        /// <remarks>NoAlloc.</remarks>
        public string RuntimeCatalogSchemaId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int RuntimeCatalogSchemaVersion { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool HasExpectedGameplayTriggerCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long ExpectedGameplayTriggerCatalogHash { get; }
    }

    /// <summary>Trusted immutable compiler environment.</summary>
    public sealed class GameplayTriggerCompilerEnvironment
    {
        private GameplayTriggerCompilerEnvironment(GameplayTriggerRetiredIdRegistry retiredIds, GameplayTriggerSourceBindingContractRegistry sourceBindings, GameplayTriggerActionRequirementRegistry actionRequirements, GameplayTriggerExpectedCatalogIdentity expectedCatalogIdentity)
        {
            RetiredIds = retiredIds;
            SourceBindings = sourceBindings;
            ActionRequirements = actionRequirements;
            ExpectedCatalogIdentity = expectedCatalogIdentity;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRetiredIdRegistry RetiredIds { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingContractRegistry SourceBindings { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionRequirementRegistry ActionRequirements { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerExpectedCatalogIdentity ExpectedCatalogIdentity { get; }

        /// <remarks>AllocByDesign. Validates and freezes trusted registries and expected identity.</remarks>
        public static bool TryCreate(GameplayTriggerRetiredIdRegistry retiredIds, GameplayTriggerSourceBindingContractRegistry sourceBindings, GameplayTriggerActionRequirementRegistry actionRequirements, GameplayTriggerExpectedCatalogIdentity expectedCatalogIdentity, out GameplayTriggerCompilerEnvironment environment, out GameplayTriggerConfigRegistryResult result)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            if (retiredIds == null || sourceBindings == null || actionRequirements == null)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, "$.environment"));
            if (!string.Equals(expectedCatalogIdentity.RuntimeCatalogSchemaId, GameplayTriggerConfigSchemas.RuntimeCatalogId, StringComparison.Ordinal))
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogSchemaMismatch, "$.environment.expectedCatalogIdentity.schemaId"));
            if (expectedCatalogIdentity.RuntimeCatalogSchemaVersion != GameplayTriggerConfigSchemas.RuntimeCatalogVersion)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogVersionMismatch, "$.environment.expectedCatalogIdentity.schemaVersion", 0, 0, GameplayTriggerConfigSchemas.RuntimeCatalogVersion, expectedCatalogIdentity.RuntimeCatalogSchemaVersion));
            result = new GameplayTriggerConfigRegistryResult(diagnostics);
            environment = result.Succeeded ? new GameplayTriggerCompilerEnvironment(retiredIds, sourceBindings, actionRequirements, expectedCatalogIdentity) : null;
            return result.Succeeded;
        }
    }
}
