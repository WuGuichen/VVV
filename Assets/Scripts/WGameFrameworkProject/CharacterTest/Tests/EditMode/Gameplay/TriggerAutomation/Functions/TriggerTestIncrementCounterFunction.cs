using System;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestIncrementCounterArguments :
        ITriggerTestFunctionArguments
    {
        internal TriggerTestIncrementCounterArguments(long amount)
        {
            Amount = amount;
        }

        internal long Amount { get; }
    }

    internal sealed class TriggerTestIncrementCounterFunction :
        TriggerTestFunction<TriggerTestIncrementCounterArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.framework.increment-counter");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.RuntimeMutation;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestIncrementCounterArguments arguments)
        {
            if (!context.Fixture.Services.TryGet(out TriggerTestCounter counter))
            {
                return TriggerTestOperationResult.Failed(
                    "TriggerTestCounter service is missing.");
            }
            if (arguments.Amount > 0L &&
                counter.Value > long.MaxValue - arguments.Amount ||
                arguments.Amount < 0L &&
                counter.Value < long.MinValue - arguments.Amount)
            {
                return TriggerTestOperationResult.Failed(
                    "Counter increment would overflow.");
            }

            counter.Value += arguments.Amount;
            return TriggerTestOperationResult.Succeeded(
                "counter=" + counter.Value);
        }
    }
}
