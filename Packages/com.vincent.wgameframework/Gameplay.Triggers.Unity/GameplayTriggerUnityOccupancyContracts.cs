using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.Unity
{
    public enum GameplayTriggerUnitySubmissionMode
    {
        LiveExternal = 1,
        ReplayDisabled = 2
    }

    public readonly struct GameplayTriggerUnityOccupancyOptions
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityOccupancyOptions(
            int maxSubjectBindings,
            int maxSubjectColliders,
            int maxActiveContactPairs)
        {
            MaxSubjectBindings = maxSubjectBindings;
            MaxSubjectColliders = maxSubjectColliders;
            MaxActiveContactPairs = maxActiveContactPairs;
        }

        public int MaxSubjectBindings { get; }
        public int MaxSubjectColliders { get; }
        public int MaxActiveContactPairs { get; }
    }

    public readonly struct GameplayTriggerUnityZoneHandle
    {
        internal GameplayTriggerUnityZoneHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0 && Generation > 0;
    }

    public readonly struct GameplayTriggerUnitySubjectHandle
    {
        internal GameplayTriggerUnitySubjectHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0 && Generation > 0;
    }

    public enum GameplayTriggerUnityCatalogErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        DuplicateSourceBinding = 2,
        SourceRequirementMissing = 3,
        ProviderMismatch = 4,
        SourceKindMismatch = 5,
        IncompleteBatchContract = 6,
        EntityCapabilityMismatch = 7,
        InputCapacityMismatch = 8,
        ActiveRelationCapacityMismatch = 9,
        ReplayMetadataMismatch = 10,
        CapacityArithmeticOverflow = 11
    }

    public readonly struct GameplayTriggerUnityCatalogResult
    {
        internal GameplayTriggerUnityCatalogResult(
            bool success,
            GameplayTriggerUnityCatalogErrorCode errorCode,
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

        public bool Success { get; }
        public GameplayTriggerUnityCatalogErrorCode ErrorCode { get; }
        public int DescriptorIndex { get; }
        public long Expected { get; }
        public long Actual { get; }
    }

    public enum GameplayTriggerUnityInitializationErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        InvalidOptions = 2,
        CatalogPlanMismatch = 3,
        RuntimeCatalogMismatch = 4,
        GameplayModuleScheduleMismatch = 5,
        CommandBufferUnbounded = 6,
        CommandSystemScheduleMismatch = 7,
        CommandSystemDisabled = 8,
        CommandSystemRuntimeMismatch = 9,
        CommandSystemNotRegistered = 10,
        ReconstructionCapacityInsufficient = 11,
        InvalidCommandSourceId = 12,
        SourceRegistrationFailed = 13
    }

    public readonly struct GameplayTriggerUnityInitializationResult
    {
        internal GameplayTriggerUnityInitializationResult(
            bool success,
            GameplayTriggerUnityInitializationErrorCode errorCode,
            long expected = 0L,
            long actual = 0L,
            RuntimeCommandSourceRegistrationErrorCode sourceErrorCode =
                RuntimeCommandSourceRegistrationErrorCode.None)
        {
            Success = success;
            ErrorCode = errorCode;
            Expected = expected;
            Actual = actual;
            SourceErrorCode = sourceErrorCode;
        }

        public bool Success { get; }
        public GameplayTriggerUnityInitializationErrorCode ErrorCode { get; }
        public long Expected { get; }
        public long Actual { get; }
        public RuntimeCommandSourceRegistrationErrorCode SourceErrorCode { get; }
    }

    public enum GameplayTriggerUnityZoneRegistrationStatus
    {
        None = 0,
        Registered = 1,
        InvalidArgument = 2,
        UnknownSourceBinding = 3,
        SourceBindingBusy = 4
    }

    public readonly struct GameplayTriggerUnityZoneRegistrationResult
    {
        internal GameplayTriggerUnityZoneRegistrationResult(
            GameplayTriggerUnityZoneRegistrationStatus status,
            GameplayTriggerSourceBindingId sourceBindingId = default)
        {
            Status = status;
            SourceBindingId = sourceBindingId;
        }

        public GameplayTriggerUnityZoneRegistrationStatus Status { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public bool Success => Status == GameplayTriggerUnityZoneRegistrationStatus.Registered;
    }

    public enum GameplayTriggerUnitySubjectBindingStatus
    {
        None = 0,
        Bound = 1,
        Rebound = 2,
        InvalidArgument = 3,
        InvalidEntity = 4,
        SubjectCapacityExceeded = 5,
        ColliderCapacityExceeded = 6,
        DuplicateCollider = 7,
        StaleHandle = 8
    }

    public readonly struct GameplayTriggerUnitySubjectBindingResult
    {
        internal GameplayTriggerUnitySubjectBindingResult(
            GameplayTriggerUnitySubjectBindingStatus status,
            GameplayEntityId entityId = default,
            int detail = 0)
        {
            Status = status;
            EntityId = entityId;
            Detail = detail;
        }

        public GameplayTriggerUnitySubjectBindingStatus Status { get; }
        public GameplayEntityId EntityId { get; }
        public int Detail { get; }
        public bool Success => Status == GameplayTriggerUnitySubjectBindingStatus.Bound ||
            Status == GameplayTriggerUnitySubjectBindingStatus.Rebound;
    }

    public enum GameplayTriggerUnityLifecycleStatus
    {
        None = 0,
        RetirementScheduled = 1,
        SubjectUnbound = 2,
        AlreadyScheduled = 3,
        StaleHandle = 4
    }

    public readonly struct GameplayTriggerUnityLifecycleResult
    {
        internal GameplayTriggerUnityLifecycleResult(GameplayTriggerUnityLifecycleStatus status)
        {
            Status = status;
        }

        public GameplayTriggerUnityLifecycleStatus Status { get; }
        public bool Success => Status == GameplayTriggerUnityLifecycleStatus.RetirementScheduled ||
            Status == GameplayTriggerUnityLifecycleStatus.SubjectUnbound ||
            Status == GameplayTriggerUnityLifecycleStatus.AlreadyScheduled;
    }

    public enum GameplayTriggerUnityContactStatus
    {
        None = 0,
        Activated = 1,
        Deactivated = 2,
        DuplicateEnter = 3,
        UnknownExit = 4,
        UnknownCollider = 5,
        StaleZone = 6,
        ZoneRetiring = 7,
        ContactCapacityExceeded = 8
    }

    public readonly struct GameplayTriggerUnityContactResult
    {
        internal GameplayTriggerUnityContactResult(
            GameplayTriggerUnityContactStatus status,
            GameplayTriggerSourceBindingId sourceBindingId = default)
        {
            Status = status;
            SourceBindingId = sourceBindingId;
        }

        public GameplayTriggerUnityContactStatus Status { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public bool Mutated => Status == GameplayTriggerUnityContactStatus.Activated ||
            Status == GameplayTriggerUnityContactStatus.Deactivated;
    }

    public enum GameplayTriggerUnityZoneFlushStatus
    {
        None = 0,
        Submitted = 1,
        SourceDisabled = 2,
        ReplaySuppressed = 3,
        RetiredWhileSourceDisabled = 4,
        ReplayRetiredWithoutSubmission = 5,
        ZoneNotRegistered = 6,
        UnknownSourceBinding = 7,
        SubjectCapacityExceeded = 8,
        CodecRejected = 9,
        AdmissionRejected = 10,
        DuplicateOrRegressedFrame = 11
    }

    public readonly struct GameplayTriggerUnityZoneFlushResult
    {
        internal GameplayTriggerUnityZoneFlushResult(
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerUnityZoneFlushStatus status,
            int subjectCount = 0,
            int commandCount = 0,
            int staleSubjectCount = 0,
            GameplayTriggerOccupancyCommandCodecStatus codecStatus =
                GameplayTriggerOccupancyCommandCodecStatus.None,
            RuntimeCommandErrorCode commandErrorCode = RuntimeCommandErrorCode.None,
            int failedCommandIndex = -1)
        {
            Frame = frame;
            SourceBindingId = sourceBindingId;
            Status = status;
            SubjectCount = subjectCount;
            CommandCount = commandCount;
            StaleSubjectCount = staleSubjectCount;
            CodecStatus = codecStatus;
            CommandErrorCode = commandErrorCode;
            FailedCommandIndex = failedCommandIndex;
        }

        public RuntimeFrame Frame { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public GameplayTriggerUnityZoneFlushStatus Status { get; }
        public int SubjectCount { get; }
        public int CommandCount { get; }
        public int StaleSubjectCount { get; }
        public GameplayTriggerOccupancyCommandCodecStatus CodecStatus { get; }
        public RuntimeCommandErrorCode CommandErrorCode { get; }
        public int FailedCommandIndex { get; }
    }

    public readonly struct GameplayTriggerUnityFlushSummary
    {
        internal GameplayTriggerUnityFlushSummary(
            RuntimeFrame frame,
            int configuredZoneCount,
            int submittedZoneCount,
            int failedZoneCount,
            bool duplicateOrRegressedFrame)
        {
            Frame = frame;
            ConfiguredZoneCount = configuredZoneCount;
            SubmittedZoneCount = submittedZoneCount;
            FailedZoneCount = failedZoneCount;
            DuplicateOrRegressedFrame = duplicateOrRegressedFrame;
        }

        public RuntimeFrame Frame { get; }
        public int ConfiguredZoneCount { get; }
        public int SubmittedZoneCount { get; }
        public int FailedZoneCount { get; }
        public bool DuplicateOrRegressedFrame { get; }
    }
}
