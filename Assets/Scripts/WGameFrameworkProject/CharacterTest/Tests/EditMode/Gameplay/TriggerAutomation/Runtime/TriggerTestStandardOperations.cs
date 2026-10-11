using System;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestSubmitSignalsStep : ITriggerTestFrameStep
    {
        private readonly GameplayTriggerSourceBindingId _sourceBindingId;
        private readonly GameplayTriggerSignalOrdering _ordering;
        private readonly GameplayTriggerSignal[] _signals;

        internal TriggerTestSubmitSignalsStep(
            string stepId,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSignalOrdering ordering,
            GameplayTriggerSignal[] signals)
        {
            StepId = stepId ?? string.Empty;
            _sourceBindingId = sourceBindingId;
            _ordering = ordering;
            _signals = signals == null
                ? Array.Empty<GameplayTriggerSignal>()
                : (GameplayTriggerSignal[])signals.Clone();
        }

        public string StepId { get; }

        public TriggerTestOperationResult Execute(
            in TriggerTestStepContext context)
        {
            bool accepted = context.Fixture.TriggerRuntime.TrySubmitSignals(
                context.Frame,
                _sourceBindingId,
                _ordering,
                _signals,
                out GameplayTriggerResult result);
            return accepted
                ? TriggerTestOperationResult.Succeeded(
                    "submitted " + _signals.Length + " signal(s)")
                : TriggerTestOperationResult.Failed(
                    "signal submission failed: " + result.ErrorCode +
                    " detail0=" + result.Detail0 +
                    " detail1=" + result.Detail1);
        }
    }

    internal enum TriggerTestRuntimeCountKind
    {
        Occurrence = 0,
        Activation = 1,
        Intent = 2,
        Diagnostic = 3
    }

    internal sealed class TriggerTestRuntimeCountAssertion :
        ITriggerTestAssertion
    {
        private readonly TriggerTestRuntimeCountKind _kind;
        private readonly int _expected;

        internal TriggerTestRuntimeCountAssertion(
            string assertionId,
            TriggerTestRuntimeCountKind kind,
            int expected)
        {
            AssertionId = assertionId ?? string.Empty;
            _kind = kind;
            _expected = expected;
        }

        public string AssertionId { get; }

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            GameplayTriggerRuntime runtime = context.Fixture.TriggerRuntime;
            int actual;
            switch (_kind)
            {
                case TriggerTestRuntimeCountKind.Occurrence:
                    actual = runtime.OccurrenceCount;
                    break;
                case TriggerTestRuntimeCountKind.Activation:
                    actual = runtime.ActivationCount;
                    break;
                case TriggerTestRuntimeCountKind.Intent:
                    actual = runtime.IntentCount;
                    break;
                case TriggerTestRuntimeCountKind.Diagnostic:
                    actual = runtime.DiagnosticCount;
                    break;
                default:
                    return TriggerTestOperationResult.Failed(
                        "unknown runtime count kind");
            }

            return actual == _expected
                ? TriggerTestOperationResult.Succeeded(
                    _kind + "=" + actual)
                : TriggerTestOperationResult.Failed(
                    _kind + " expected " + _expected + " but was " + actual);
        }
    }

    internal sealed class TriggerTestCounterAssertion : ITriggerTestAssertion
    {
        private readonly long _expected;

        internal TriggerTestCounterAssertion(
            string assertionId,
            long expected)
        {
            AssertionId = assertionId ?? string.Empty;
            _expected = expected;
        }

        public string AssertionId { get; }

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(out TriggerTestCounter counter))
            {
                return TriggerTestOperationResult.Failed(
                    "TriggerTestCounter service is missing.");
            }

            return counter.Value == _expected
                ? TriggerTestOperationResult.Succeeded(
                    "counter=" + counter.Value)
                : TriggerTestOperationResult.Failed(
                    "counter expected " + _expected + " but was " +
                    counter.Value);
        }
    }
}
