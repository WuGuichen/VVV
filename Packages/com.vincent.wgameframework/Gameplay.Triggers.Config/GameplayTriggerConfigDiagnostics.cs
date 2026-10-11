using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>Stable compiler/bootstrap diagnostic codes. Numeric values are part of the config contract.</summary>
    public enum GameplayTriggerConfigDiagnosticCode
    {
        NullDocument = 100,
        AuthoringSchemaMismatch = 101,
        AuthoringVersionMismatch = 102,
        RuntimeCatalogSchemaMismatch = 103,
        RuntimeCatalogVersionMismatch = 104,
        CompiledCatalogHashMismatch = 105,
        NullRow = 106,
        InvalidId = 110,
        RetiredId = 111,
        DuplicateSourceId = 112,
        DuplicateConditionId = 113,
        DuplicateDefinitionId = 114,
        DuplicateRouteId = 115,
        DuplicateBindingId = 116,
        DuplicateSourceBindingId = 117,
        DuplicateActionKind = 118,
        MissingSourceReference = 119,
        MissingConditionReference = 120,
        MissingDefinitionReference = 121,
        InvalidSourceKind = 200,
        InvalidSignalOrdering = 201,
        InvalidSignalReplayMetadata = 202,
        InvalidThresholdDirection = 203,
        InvalidHysteresis = 204,
        InvalidInitialSamplePolicy = 205,
        InvalidDeparturePolicy = 206,
        InvalidBindingCapacity = 207,
        MissingSourceBindingContract = 208,
        SourceDefinitionContractMismatch = 209,
        SourceKindContractMismatch = 210,
        SourceProviderMissing = 211,
        SourceCapabilityMismatch = 212,
        SourceEvaluationContractInvalid = 213,
        EmptyOccurrenceMask = 300,
        InvalidOccurrenceMask = 301,
        InvalidStateScope = 302,
        NegativeMaxActivations = 303,
        NegativeCooldown = 304,
        InvalidPresenceLimit = 305,
        NegativeStayInterval = 306,
        InvalidExclusiveGroup = 307,
        InvalidArbitrationScope = 308,
        OncePerPresenceUnsupported = 309,
        MissingSubjectCapability = 310,
        MissingInstigatorCapability = 311,
        ExclusiveScopeMismatch = 312,
        RouteCountExceeded = 313,
        ActionCountExceeded = 314,
        InvalidConditionNode = 400,
        ConditionCycle = 401,
        InvalidConditionArity = 402,
        InvalidConditionOperand = 403,
        InvalidComparison = 404,
        ConditionDepthExceeded = 405,
        ConditionInstructionExceeded = 406,
        ConditionStackExceeded = 407,
        /// <summary>Compiler invariant code used only if validated lowering unexpectedly fails the core postfix constructor.</summary>
        InvalidPostfixStack = 408,
        InvalidTargetSelector = 500,
        MissingExplicitTargetSlot = 501,
        DuplicateExplicitTargetSlot = 502,
        InvalidTargetReference = 503,
        UnknownActionContract = 504,
        InvalidActionContract = 505,
        /// <summary>Reserved v1 code. In v2 the sink interface itself carries the reservation protocol, so normal compiler/bootstrap flow does not emit this code.</summary>
        SameFrameReservationCapabilityMissing = 506,
        ReliableOutboxCapabilityMissing = 507,
        ReliableRequiresFailFast = 508,
        ReliableScheduleInvalid = 509,
        InternalDescriptorMismatch = 510,
        BootstrapActionMissing = 511,
        BootstrapActionDuplicate = 512,
        BootstrapDeliveryMismatch = 513,
        BootstrapCapabilityMismatch = 514,
        RuntimeCatalogRejected = 515,
        InvalidCompilerOption = 600,
        CapacityArithmeticOverflow = 601,
        /// <summary>Reserved v1 code. Constructible minimum clamping prevents a zero-required-capacity error in normal compiler flow.</summary>
        ZeroRequiredCapacity = 602,
        RuntimeProfileUndersized = 603,
        RuntimeProfileShapeInvalid = 604,
        RegistryInvalid = 605,
        UnboundedSignalCycle = 700
    }

    /// <summary>Typed deterministic compiler/bootstrap diagnostic.</summary>
    public readonly struct GameplayTriggerConfigDiagnostic
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode code, string path, int primaryId = 0, int secondaryId = 0, long detail0 = 0L, long detail1 = 0L)
        {
            Code = code;
            Path = path ?? string.Empty;
            PrimaryId = primaryId;
            SecondaryId = secondaryId;
            Detail0 = detail0;
            Detail1 = detail1;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnosticCode Code { get; }
        /// <remarks>NoAlloc.</remarks>
        public string Path { get; }
        /// <remarks>NoAlloc.</remarks>
        public int PrimaryId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SecondaryId { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Detail0 { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Detail1 { get; }
    }

    /// <summary>Typed result for trusted registry/environment creation.</summary>
    public sealed class GameplayTriggerConfigRegistryResult
    {
        private readonly GameplayTriggerConfigDiagnostic[] _diagnostics;

        internal GameplayTriggerConfigRegistryResult(List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            _diagnostics = GameplayTriggerConfigDiagnosticOrdering.Freeze(diagnostics);
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Succeeded => _diagnostics.Length == 0;
        /// <remarks>NoAlloc.</remarks>
        public int DiagnosticCount => _diagnostics.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnostic GetDiagnostic(int index) => _diagnostics[index];
    }

    internal static class GameplayTriggerConfigDiagnosticOrdering
    {
        internal static GameplayTriggerConfigDiagnostic[] Freeze(List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0) return Array.Empty<GameplayTriggerConfigDiagnostic>();
            GameplayTriggerConfigDiagnostic[] result = diagnostics.ToArray();
            Array.Sort(result, Compare);
            return result;
        }

        private static int Compare(GameplayTriggerConfigDiagnostic left, GameplayTriggerConfigDiagnostic right)
        {
            int value = left.Code.CompareTo(right.Code);
            if (value != 0) return value;
            value = string.Compare(left.Path, right.Path, StringComparison.Ordinal);
            if (value != 0) return value;
            value = left.PrimaryId.CompareTo(right.PrimaryId);
            if (value != 0) return value;
            value = left.SecondaryId.CompareTo(right.SecondaryId);
            if (value != 0) return value;
            value = left.Detail0.CompareTo(right.Detail0);
            return value != 0 ? value : left.Detail1.CompareTo(right.Detail1);
        }
    }
}
