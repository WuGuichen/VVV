using System;
using System.Collections.Generic;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal delegate ITriggerTestFixture TriggerTestFixtureFactory(
        TriggerTestFunctionActionSink functionSink);

    internal sealed class TriggerTestScenarioBuilder
    {
        private readonly TriggerTestDescriptor _descriptor;
        private readonly List<FunctionEntry> _functions = new List<FunctionEntry>();
        private readonly List<TriggerTestFunctionCallSpec> _calls =
            new List<TriggerTestFunctionCallSpec>();
        private readonly List<TriggerTestScheduledStep> _steps =
            new List<TriggerTestScheduledStep>();
        private readonly List<TriggerTestScheduledAssertion> _assertions =
            new List<TriggerTestScheduledAssertion>();
        private TriggerTestFixtureFactory _fixtureFactory;
        private int _nextSequence;

        internal TriggerTestScenarioBuilder(TriggerTestDescriptor descriptor)
        {
            _descriptor = descriptor ??
                throw new ArgumentNullException(nameof(descriptor));
        }

        internal void UseFixture(TriggerTestFixtureFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (_fixtureFactory != null)
                throw new InvalidOperationException(
                    "A Trigger test scenario can register only one fixture factory.");

            _fixtureFactory = factory;
        }

        internal void RegisterFunction(ITriggerTestFunction function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            if (!function.FunctionId.IsValid)
                throw new InvalidOperationException(
                    "Trigger test function id is invalid: " + function.FunctionId + ".");

            for (int i = 0; i < _functions.Count; i++)
            {
                if (_functions[i].Function.FunctionId.Equals(function.FunctionId))
                {
                    throw new InvalidOperationException(
                        "Trigger test function id is already registered: " +
                        function.FunctionId + ".");
                }
            }

            if (_descriptor.ExecutionProfile == TriggerTestExecutionProfile.NoEngine &&
                function.Capability == TriggerTestFunctionCapability.UnityAdapter)
            {
                throw new InvalidOperationException(
                    "NoEngine Trigger test scenario cannot register Unity function: " +
                    function.FunctionId + ".");
            }

            _functions.Add(new FunctionEntry(function));
        }

        internal void RegisterCall(
            int callId,
            TriggerTestFunctionId functionId,
            ITriggerTestFunctionArguments arguments)
        {
            if (callId <= 0)
                throw new ArgumentOutOfRangeException(nameof(callId));
            if (!functionId.IsValid)
                throw new InvalidOperationException(
                    "Trigger test call function id is invalid: " + functionId + ".");
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            for (int i = 0; i < _calls.Count; i++)
            {
                if (_calls[i].CallId == callId)
                {
                    throw new InvalidOperationException(
                        "Trigger test call id is already registered: " + callId + ".");
                }
            }

            _calls.Add(new TriggerTestFunctionCallSpec(
                callId,
                functionId,
                arguments));
        }

        internal void AtFrame(long frame, ITriggerTestFrameStep step)
        {
            ValidateFrame(frame);
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (string.IsNullOrWhiteSpace(step.StepId))
                throw new InvalidOperationException(
                    "Trigger test step id cannot be empty.");

            _steps.Add(new TriggerTestScheduledStep(
                frame,
                _nextSequence++,
                step));
        }

        internal void AssertAtFrame(long frame, ITriggerTestAssertion assertion)
        {
            ValidateFrame(frame);
            if (assertion == null)
                throw new ArgumentNullException(nameof(assertion));
            if (string.IsNullOrWhiteSpace(assertion.AssertionId))
                throw new InvalidOperationException(
                    "Trigger test assertion id cannot be empty.");

            _assertions.Add(new TriggerTestScheduledAssertion(
                frame,
                _nextSequence++,
                assertion));
        }

        internal TriggerTestScenarioPlan BuildPlan()
        {
            ValidateDescriptor();
            if (_fixtureFactory == null)
                throw new InvalidOperationException(
                    "Trigger test scenario did not register a fixture factory.");

            _functions.Sort(CompareFunctions);
            _calls.Sort(CompareCalls);
            _steps.Sort(CompareSteps);
            _assertions.Sort(CompareAssertions);

            for (int i = 0; i < _calls.Count; i++)
            {
                if (FindFunction(_calls[i].FunctionId) < 0)
                {
                    throw new InvalidOperationException(
                        "Trigger test call " + _calls[i].CallId +
                        " references unregistered function " +
                        _calls[i].FunctionId + ".");
                }
            }

            return new TriggerTestScenarioPlan(
                _descriptor,
                _fixtureFactory,
                CopyFunctions(),
                _calls.ToArray(),
                _steps.ToArray(),
                _assertions.ToArray());
        }

        private void ValidateFrame(long frame)
        {
            if (frame < 0L || frame >= _descriptor.MaxFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frame),
                    "Trigger test frame must be inside the descriptor frame range.");
            }
        }

        private void ValidateDescriptor()
        {
            if (!_descriptor.ScenarioId.IsValid)
                throw new InvalidOperationException(
                    "Trigger test scenario id is invalid: " +
                    _descriptor.ScenarioId + ".");
            if (string.IsNullOrWhiteSpace(_descriptor.DisplayName))
                throw new InvalidOperationException(
                    "Trigger test display name cannot be empty.");
            if (!TriggerTestIdentity.IsModule(_descriptor.Module))
                throw new InvalidOperationException(
                    "Trigger test module is invalid: " +
                    _descriptor.Module + ".");
            if (_descriptor.MaxFrames <= 0)
                throw new InvalidOperationException(
                    "Trigger test maxFrames must be positive.");
            if (_descriptor.MaxPendingFunctionCalls <= 0)
                throw new InvalidOperationException(
                    "Trigger test maxPendingFunctionCalls must be positive.");
            if (_descriptor.ExecutionProfile < TriggerTestExecutionProfile.NoEngine ||
                _descriptor.ExecutionProfile > TriggerTestExecutionProfile.PlayMode)
            {
                throw new InvalidOperationException(
                    "Trigger test execution profile is invalid.");
            }

            for (int i = 0; i < _descriptor.ExternalChecks.Count; i++)
            {
                TriggerTestExternalCheck check = _descriptor.ExternalChecks[i];
                if (string.IsNullOrWhiteSpace(check.CheckId) ||
                    string.IsNullOrWhiteSpace(check.ExpectedEvidence))
                {
                    throw new InvalidOperationException(
                        "Trigger test external check id and evidence cannot be empty.");
                }
                if (check.Kind == TriggerTestExternalCheckKind.CustomToolEvidence &&
                    string.IsNullOrWhiteSpace(check.CapabilityId))
                {
                    throw new InvalidOperationException(
                        "Custom tool evidence requires a stable capability id.");
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(
                            _descriptor.ExternalChecks[previous].CheckId,
                            check.CheckId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Trigger test external check id is duplicated: " +
                            check.CheckId + ".");
                    }
                }
            }
        }

        private int FindFunction(TriggerTestFunctionId id)
        {
            int low = 0;
            int high = _functions.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison =
                    _functions[middle].Function.FunctionId.CompareTo(id);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return -1;
        }

        private ITriggerTestFunction[] CopyFunctions()
        {
            var functions = new ITriggerTestFunction[_functions.Count];
            for (int i = 0; i < functions.Length; i++)
                functions[i] = _functions[i].Function;
            return functions;
        }

        private static int CompareFunctions(FunctionEntry left, FunctionEntry right) =>
            left.Function.FunctionId.CompareTo(right.Function.FunctionId);

        private static int CompareCalls(
            TriggerTestFunctionCallSpec left,
            TriggerTestFunctionCallSpec right) =>
            left.CallId.CompareTo(right.CallId);

        private static int CompareSteps(
            TriggerTestScheduledStep left,
            TriggerTestScheduledStep right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            return frame != 0 ? frame : left.Sequence.CompareTo(right.Sequence);
        }

        private static int CompareAssertions(
            TriggerTestScheduledAssertion left,
            TriggerTestScheduledAssertion right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            return frame != 0 ? frame : left.Sequence.CompareTo(right.Sequence);
        }

        private readonly struct FunctionEntry
        {
            internal FunctionEntry(ITriggerTestFunction function)
            {
                Function = function;
            }

            internal ITriggerTestFunction Function { get; }
        }
    }

    internal sealed class TriggerTestScenarioPlan
    {
        private readonly ITriggerTestFunction[] _functions;
        private readonly TriggerTestFunctionCallSpec[] _calls;
        private readonly TriggerTestScheduledStep[] _steps;
        private readonly TriggerTestScheduledAssertion[] _assertions;

        internal TriggerTestScenarioPlan(
            TriggerTestDescriptor descriptor,
            TriggerTestFixtureFactory fixtureFactory,
            ITriggerTestFunction[] functions,
            TriggerTestFunctionCallSpec[] calls,
            TriggerTestScheduledStep[] steps,
            TriggerTestScheduledAssertion[] assertions)
        {
            Descriptor = descriptor;
            FixtureFactory = fixtureFactory;
            _functions = functions;
            _calls = calls;
            _steps = steps;
            _assertions = assertions;
        }

        internal TriggerTestDescriptor Descriptor { get; }
        internal TriggerTestFixtureFactory FixtureFactory { get; }
        internal int StepCount => _steps.Length;
        internal int AssertionCount => _assertions.Length;

        internal int[] CopyCallIds()
        {
            var callIds = new int[_calls.Length];
            for (int i = 0; i < callIds.Length; i++)
                callIds[i] = _calls[i].CallId;
            return callIds;
        }

        internal bool TryGetCall(
            int callId,
            out TriggerTestFunctionCallSpec call)
        {
            int low = 0;
            int high = _calls.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _calls[middle].CallId.CompareTo(callId);
                if (comparison == 0)
                {
                    call = _calls[middle];
                    return true;
                }
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            call = default;
            return false;
        }

        internal bool TryGetFunction(
            TriggerTestFunctionId id,
            out ITriggerTestFunction function)
        {
            int low = 0;
            int high = _functions.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _functions[middle].FunctionId.CompareTo(id);
                if (comparison == 0)
                {
                    function = _functions[middle];
                    return true;
                }
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            function = null;
            return false;
        }

        internal TriggerTestScheduledStep GetStep(int index) => _steps[index];

        internal TriggerTestScheduledAssertion GetAssertion(int index) =>
            _assertions[index];
    }

    internal readonly struct TriggerTestFunctionCallSpec
    {
        internal TriggerTestFunctionCallSpec(
            int callId,
            TriggerTestFunctionId functionId,
            ITriggerTestFunctionArguments arguments)
        {
            CallId = callId;
            FunctionId = functionId;
            Arguments = arguments;
        }

        internal int CallId { get; }
        internal TriggerTestFunctionId FunctionId { get; }
        internal ITriggerTestFunctionArguments Arguments { get; }
    }

    internal readonly struct TriggerTestScheduledStep
    {
        internal TriggerTestScheduledStep(
            long frame,
            int sequence,
            ITriggerTestFrameStep step)
        {
            Frame = frame;
            Sequence = sequence;
            Step = step;
        }

        internal long Frame { get; }
        internal int Sequence { get; }
        internal ITriggerTestFrameStep Step { get; }
    }

    internal readonly struct TriggerTestScheduledAssertion
    {
        internal TriggerTestScheduledAssertion(
            long frame,
            int sequence,
            ITriggerTestAssertion assertion)
        {
            Frame = frame;
            Sequence = sequence;
            Assertion = assertion;
        }

        internal long Frame { get; }
        internal int Sequence { get; }
        internal ITriggerTestAssertion Assertion { get; }
    }
}
