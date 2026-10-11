using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.CombatBridge
{
    public enum GameplayTriggerCombatSensorCatalogErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        DuplicateSourceBinding = 2,
        SourceRequirementMissing = 3,
        ProviderMismatch = 4,
        SourceKindMismatch = 5,
        IncompleteBatchContract = 6,
        EntityCapabilityMismatch = 7,
        CapacityMismatch = 8,
        UnsupportedShape = 9,
        QueryHeaderMismatch = 10,
        EmptyLayerMask = 11,
        InvalidGeometry = 12,
        ReplayMetadataMismatch = 13,
        UnsupportedFilter = 14
    }

    /// <summary>Typed immutable result for sensor catalog creation.</summary>
    public readonly struct GameplayTriggerCombatSensorCatalogResult
    {
        internal GameplayTriggerCombatSensorCatalogResult(
            bool success,
            GameplayTriggerCombatSensorCatalogErrorCode errorCode,
            int descriptorIndex = -1,
            int detail0 = 0,
            int detail1 = 0)
        {
            Success = success;
            ErrorCode = errorCode;
            DescriptorIndex = descriptorIndex;
            Detail0 = detail0;
            Detail1 = detail1;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Success { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorCatalogErrorCode ErrorCode { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DescriptorIndex { get; }
        /// <remarks>NoAlloc.</remarks>
        public int Detail0 { get; }
        /// <remarks>NoAlloc.</remarks>
        public int Detail1 { get; }

        internal static GameplayTriggerCombatSensorCatalogResult Succeeded =>
            new GameplayTriggerCombatSensorCatalogResult(true, GameplayTriggerCombatSensorCatalogErrorCode.None);
    }

    public enum GameplayTriggerCombatSensorInitializationErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        RuntimeCatalogMismatch = 2,
        UnknownSourceBinding = 3,
        InvalidSystemId = 4,
        InvalidPriority = 5
    }

    /// <summary>Typed immutable result for sensor system initialization.</summary>
    public readonly struct GameplayTriggerCombatSensorInitializationResult
    {
        internal GameplayTriggerCombatSensorInitializationResult(
            bool success,
            GameplayTriggerCombatSensorInitializationErrorCode errorCode,
            int descriptorIndex = -1,
            long expected = 0L,
            long actual = 0L)
        {
            Success = success;
            ErrorCode = errorCode;
            DescriptorIndex = descriptorIndex;
            Expected = expected;
            Actual = actual;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Success { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorInitializationErrorCode ErrorCode { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DescriptorIndex { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Expected { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Actual { get; }

        internal static GameplayTriggerCombatSensorInitializationResult Succeeded =>
            new GameplayTriggerCombatSensorInitializationResult(
                true,
                GameplayTriggerCombatSensorInitializationErrorCode.None);
    }

    public enum GameplayTriggerCombatSensorTickStatus
    {
        None = 0,
        Submitted = 1,
        Disabled = 2,
        ContextMismatch = 3,
        DuplicateOrRegressedFrame = 4,
        QueryCapacityExceeded = 5,
        InvalidMappedGameplayEntity = 6,
        MappedSubjectCapacityExceeded = 7,
        TriggerSubmitRejected = 8
    }

    /// <summary>Stable diagnostic row for one configured sensor binding and one Tick.</summary>
    public readonly struct GameplayTriggerCombatSensorBindingTickResult
    {
        internal GameplayTriggerCombatSensorBindingTickResult(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerCombatSensorTickStatus status,
            GameplayTriggerErrorCode triggerErrorCode,
            int queryHitCount,
            int submittedSubjectCount,
            int duplicateCombatHitCount,
            int duplicateGameplaySubjectCount,
            int unmappedCombatEntityCount,
            int staleGameplayEntityCount,
            int filteredSubjectCount,
            GameplayTargetRejectReason firstFilterRejectReason,
            int firstFilterRejectDetailId)
        {
            SourceBindingId = sourceBindingId;
            Status = status;
            TriggerErrorCode = triggerErrorCode;
            QueryHitCount = queryHitCount;
            SubmittedSubjectCount = submittedSubjectCount;
            DuplicateCombatHitCount = duplicateCombatHitCount;
            DuplicateGameplaySubjectCount = duplicateGameplaySubjectCount;
            UnmappedCombatEntityCount = unmappedCombatEntityCount;
            StaleGameplayEntityCount = staleGameplayEntityCount;
            FilteredSubjectCount = filteredSubjectCount;
            FirstFilterRejectReason = firstFilterRejectReason;
            FirstFilterRejectDetailId = firstFilterRejectDetailId;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorTickStatus Status { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerErrorCode TriggerErrorCode { get; }
        /// <remarks>NoAlloc.</remarks>
        public int QueryHitCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SubmittedSubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DuplicateCombatHitCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DuplicateGameplaySubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int UnmappedCombatEntityCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int StaleGameplayEntityCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int FilteredSubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTargetRejectReason FirstFilterRejectReason { get; }
        /// <remarks>NoAlloc.</remarks>
        public int FirstFilterRejectDetailId { get; }
    }

    /// <summary>Aggregate diagnostics for the last sensor Tick.</summary>
    public readonly struct GameplayTriggerCombatSensorTickSummary
    {
        internal GameplayTriggerCombatSensorTickSummary(
            int bindingCount,
            int queryCount,
            int submittedBatchCount,
            int submittedSubjectCount,
            int duplicateCombatHitCount,
            int duplicateGameplaySubjectCount,
            int unmappedCombatEntityCount,
            int staleGameplayEntityCount,
            int filteredSubjectCount,
            int failedBindingCount)
        {
            BindingCount = bindingCount;
            QueryCount = queryCount;
            SubmittedBatchCount = submittedBatchCount;
            SubmittedSubjectCount = submittedSubjectCount;
            DuplicateCombatHitCount = duplicateCombatHitCount;
            DuplicateGameplaySubjectCount = duplicateGameplaySubjectCount;
            UnmappedCombatEntityCount = unmappedCombatEntityCount;
            StaleGameplayEntityCount = staleGameplayEntityCount;
            FilteredSubjectCount = filteredSubjectCount;
            FailedBindingCount = failedBindingCount;
        }

        /// <remarks>NoAlloc.</remarks>
        public int BindingCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int QueryCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SubmittedBatchCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SubmittedSubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DuplicateCombatHitCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DuplicateGameplaySubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int UnmappedCombatEntityCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int StaleGameplayEntityCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int FilteredSubjectCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int FailedBindingCount { get; }
    }
}
