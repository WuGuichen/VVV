using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestActionKindIds
    {
        internal const int FunctionCallValue = 2_000_000_001;

        internal static readonly GameplayTriggerActionKindId FunctionCall =
            new GameplayTriggerActionKindId(FunctionCallValue);
    }

    internal enum TriggerTestExecutionProfile
    {
        NoEngine = 0,
        EditMode = 1,
        PlayMode = 2
    }

    internal enum TriggerTestFunctionCapability
    {
        PureRead = 0,
        RuntimeMutation = 1,
        UnityAdapter = 2
    }

    internal enum TriggerTestRunStatus
    {
        Passed = 0,
        Failed = 1,
        PendingExternalEvidence = 2
    }

    internal enum TriggerTestExternalCheckKind
    {
        UnityCompile = 0,
        ConsoleNoErrors = 1,
        FocusedEditMode = 2,
        FocusedPlayMode = 3,
        SceneState = 4,
        ScreenshotReview = 5,
        CustomToolEvidence = 6
    }

    internal readonly struct TriggerTestScenarioId :
        IComparable<TriggerTestScenarioId>,
        IEquatable<TriggerTestScenarioId>
    {
        internal TriggerTestScenarioId(string value)
        {
            Value = value ?? string.Empty;
        }

        internal string Value { get; }
        internal bool IsValid => TriggerTestIdentity.IsScenarioId(Value);

        public int CompareTo(TriggerTestScenarioId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(TriggerTestScenarioId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is TriggerTestScenarioId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }

    internal readonly struct TriggerTestFunctionId :
        IComparable<TriggerTestFunctionId>,
        IEquatable<TriggerTestFunctionId>
    {
        internal TriggerTestFunctionId(string value)
        {
            Value = value ?? string.Empty;
        }

        internal string Value { get; }
        internal bool IsValid => TriggerTestIdentity.IsFunctionId(Value);

        public int CompareTo(TriggerTestFunctionId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(TriggerTestFunctionId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is TriggerTestFunctionId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
    }

    internal readonly struct TriggerTestExternalCheck
    {
        internal TriggerTestExternalCheck(
            string checkId,
            TriggerTestExternalCheckKind kind,
            string expectedEvidence,
            string capabilityId = "")
        {
            CheckId = checkId ?? string.Empty;
            Kind = kind;
            ExpectedEvidence = expectedEvidence ?? string.Empty;
            CapabilityId = capabilityId ?? string.Empty;
        }

        internal string CheckId { get; }
        internal TriggerTestExternalCheckKind Kind { get; }
        internal string ExpectedEvidence { get; }
        internal string CapabilityId { get; }
    }

    internal sealed class TriggerTestDescriptor
    {
        private readonly string[] _tags;
        private readonly TriggerTestExternalCheck[] _externalChecks;

        internal TriggerTestDescriptor(
            string scenarioId,
            string displayName,
            string module,
            TriggerTestExecutionProfile executionProfile,
            int maxFrames,
            IReadOnlyList<string> tags = null,
            IReadOnlyList<TriggerTestExternalCheck> externalChecks = null,
            int maxPendingFunctionCalls = 16)
        {
            ScenarioId = new TriggerTestScenarioId(scenarioId);
            DisplayName = displayName ?? string.Empty;
            Module = module ?? string.Empty;
            ExecutionProfile = executionProfile;
            MaxFrames = maxFrames;
            MaxPendingFunctionCalls = maxPendingFunctionCalls;
            _tags = Copy(tags);
            _externalChecks = Copy(externalChecks);
        }

        internal TriggerTestScenarioId ScenarioId { get; }
        internal string DisplayName { get; }
        internal string Module { get; }
        internal TriggerTestExecutionProfile ExecutionProfile { get; }
        internal int MaxFrames { get; }
        internal int MaxPendingFunctionCalls { get; }
        internal IReadOnlyList<string> Tags => _tags;
        internal IReadOnlyList<TriggerTestExternalCheck> ExternalChecks => _externalChecks;

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i] ?? string.Empty;
            return copy;
        }

        private static TriggerTestExternalCheck[] Copy(
            IReadOnlyList<TriggerTestExternalCheck> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<TriggerTestExternalCheck>();

            var copy = new TriggerTestExternalCheck[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class TriggerTestScenarioAttribute : Attribute
    {
        internal TriggerTestScenarioAttribute(string scenarioId)
        {
            ScenarioId = scenarioId ?? string.Empty;
        }

        internal string ScenarioId { get; }
    }

    internal interface ITriggerTestScenario
    {
        TriggerTestDescriptor Descriptor { get; }
        void Build(TriggerTestScenarioBuilder builder);
    }

    internal interface ITriggerTestFixture : IDisposable
    {
        GameplayTriggerRuntime TriggerRuntime { get; }
        TriggerTestServiceRegistry Services { get; }
        void Initialize();
        void Tick(long frame, double deltaTime);
    }

    internal interface ITriggerTestFrameStep
    {
        string StepId { get; }
        TriggerTestOperationResult Execute(in TriggerTestStepContext context);
    }

    internal interface ITriggerTestAssertion
    {
        string AssertionId { get; }
        TriggerTestOperationResult Evaluate(in TriggerTestAssertionContext context);
    }

    internal interface ITriggerTestFunctionArguments
    {
    }

    internal readonly struct TriggerTestFunctionPreview
    {
        private readonly string[] _preconditions;

        internal TriggerTestFunctionPreview(
            string behavior,
            string technicalDetails,
            params string[] preconditions)
        {
            Behavior = behavior ?? string.Empty;
            TechnicalDetails = technicalDetails ?? string.Empty;
            _preconditions =
                preconditions == null || preconditions.Length == 0
                    ? Array.Empty<string>()
                    : (string[])preconditions.Clone();
        }

        internal string Behavior { get; }
        internal string TechnicalDetails { get; }
        internal IReadOnlyList<string> Preconditions =>
            _preconditions ?? Array.Empty<string>();
    }

    internal interface ITriggerTestFunctionPreviewProvider
    {
        TriggerTestFunctionPreview DescribePreview(
            ITriggerTestFunctionArguments arguments);
    }

    internal interface ITriggerTestFunction
    {
        TriggerTestFunctionId FunctionId { get; }
        TriggerTestFunctionCapability Capability { get; }
        TriggerTestOperationResult Execute(
            in TriggerTestFunctionContext context,
            ITriggerTestFunctionArguments arguments);
    }

    internal abstract class TriggerTestFunction<TArguments> : ITriggerTestFunction
        where TArguments : class, ITriggerTestFunctionArguments
    {
        public abstract TriggerTestFunctionId FunctionId { get; }
        public abstract TriggerTestFunctionCapability Capability { get; }

        public TriggerTestOperationResult Execute(
            in TriggerTestFunctionContext context,
            ITriggerTestFunctionArguments arguments)
        {
            if (!(arguments is TArguments typed))
            {
                return TriggerTestOperationResult.Failed(
                    "Function arguments do not match " + typeof(TArguments).FullName + ".");
            }

            return ExecuteTyped(in context, typed);
        }

        protected abstract TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TArguments arguments);
    }

    internal readonly struct TriggerTestOperationResult
    {
        private TriggerTestOperationResult(bool success, string summary)
        {
            Success = success;
            Summary = summary ?? string.Empty;
        }

        internal bool Success { get; }
        internal string Summary { get; }

        internal static TriggerTestOperationResult Succeeded(string summary = "") =>
            new TriggerTestOperationResult(true, summary);

        internal static TriggerTestOperationResult Failed(string summary) =>
            new TriggerTestOperationResult(false, summary);
    }

    internal readonly struct TriggerTestStepContext
    {
        internal TriggerTestStepContext(
            RuntimeFrame frame,
            ITriggerTestFixture fixture)
        {
            Frame = frame;
            Fixture = fixture;
        }

        internal RuntimeFrame Frame { get; }
        internal ITriggerTestFixture Fixture { get; }
    }

    internal readonly struct TriggerTestAssertionContext
    {
        internal TriggerTestAssertionContext(
            RuntimeFrame frame,
            ITriggerTestFixture fixture)
        {
            Frame = frame;
            Fixture = fixture;
        }

        internal RuntimeFrame Frame { get; }
        internal ITriggerTestFixture Fixture { get; }
    }

    internal readonly struct TriggerTestFunctionContext
    {
        internal TriggerTestFunctionContext(
            int callId,
            RuntimeFrame frame,
            ITriggerTestFixture fixture,
            GameplayTriggerActionIntent intent)
        {
            CallId = callId;
            Frame = frame;
            Fixture = fixture;
            Intent = intent;
        }

        internal int CallId { get; }
        internal RuntimeFrame Frame { get; }
        internal ITriggerTestFixture Fixture { get; }
        internal GameplayTriggerActionIntent Intent { get; }
    }

    internal sealed class TriggerTestRunResult
    {
        private readonly TriggerTestExternalCheck[] _externalChecks;

        internal TriggerTestRunResult(
            TriggerTestDescriptor descriptor,
            TriggerTestRunStatus status,
            FrameworkSimulationScenarioResult simulationResult)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Status = status;
            SimulationResult = simulationResult ??
                throw new ArgumentNullException(nameof(simulationResult));
            _externalChecks = new TriggerTestExternalCheck[descriptor.ExternalChecks.Count];
            for (int i = 0; i < _externalChecks.Length; i++)
                _externalChecks[i] = descriptor.ExternalChecks[i];
        }

        internal TriggerTestDescriptor Descriptor { get; }
        internal TriggerTestRunStatus Status { get; }
        internal FrameworkSimulationScenarioResult SimulationResult { get; }
        internal IReadOnlyList<TriggerTestExternalCheck> ExternalChecks => _externalChecks;
        internal bool InternalSucceeded => SimulationResult.Success;
    }

    internal static class TriggerTestIdentity
    {
        private const string ScenarioPrefix = "mx.trigger.test.";
        private const string FunctionPrefix = "mx.trigger.test.function.";

        internal static bool IsScenarioId(string value) =>
            IsStableId(value, ScenarioPrefix);

        internal static bool IsFunctionId(string value) =>
            IsStableId(value, FunctionPrefix);

        internal static bool IsModule(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool valid =
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '-';
                if (!valid)
                    return false;
            }

            return true;
        }

        internal static string GetScenarioSlug(TriggerTestScenarioId id)
        {
            string value = id.Value ?? string.Empty;
            int separator = value.LastIndexOf('.');
            return separator >= 0 && separator + 1 < value.Length
                ? value.Substring(separator + 1)
                : string.Empty;
        }

        private static bool IsStableId(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length == prefix.Length)
            {
                return false;
            }

            bool hasSegmentCharacter = false;
            bool previousWasSeparator = true;
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char character = value[i];
                bool isSeparator = character == '.';
                bool isValidCharacter =
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '-' ||
                    isSeparator;
                if (!isValidCharacter || isSeparator && previousWasSeparator)
                    return false;

                if (!isSeparator)
                    hasSegmentCharacter = true;
                previousWasSeparator = isSeparator;
            }

            return hasSegmentCharacter && !previousWasSeparator;
        }
    }
}
