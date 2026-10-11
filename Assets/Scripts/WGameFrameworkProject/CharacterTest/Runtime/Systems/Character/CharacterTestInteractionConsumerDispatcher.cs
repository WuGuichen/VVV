using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public interface ICharacterTestInteractionConsumer
    {
        string ConsumerId { get; }
        int ConsumerOrder { get; }
        void Consume(CharacterTestInteractionConsumerContext context);
    }

    public readonly struct CharacterTestInteractionConsumerContext
    {
        public CharacterTestInteractionConsumerContext(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            IReadOnlyList<CharacterTestInteractionEvent> interactions,
            RuntimeFrame frame,
            int tickRate)
        {
            World = world;
            PhysicsRuntime = physicsRuntime;
            Interactions = interactions ?? Array.Empty<CharacterTestInteractionEvent>();
            Frame = frame;
            TickRate = tickRate > 0 ? tickRate : CharacterTestDeterministicUnits.DefaultTickRate;
        }

        public CharacterTestCharacterRuntimeWorld World { get; }
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime { get; }
        public IReadOnlyList<CharacterTestInteractionEvent> Interactions { get; }
        public RuntimeFrame Frame { get; }
        public int TickRate { get; }
    }

    public sealed class CharacterTestInteractionConsumerDispatcher
    {
        private readonly List<ICharacterTestInteractionConsumer> _consumers =
            new List<ICharacterTestInteractionConsumer>();
        private readonly HashSet<string> _consumerIds =
            new HashSet<string>(StringComparer.Ordinal);

        public CharacterTestInteractionConsumerDispatcher(
            params ICharacterTestInteractionConsumer[] consumers)
        {
            if (consumers == null)
                throw new ArgumentNullException(nameof(consumers));

            for (int i = 0; i < consumers.Length; i++)
                AddConsumer(consumers[i]);

            _consumers.Sort(CompareConsumers);
        }

        public CharacterTestInteractionConsumerDispatchSummary LastSummary { get; private set; } =
            CharacterTestInteractionConsumerDispatchSummary.Empty;

        public int ConsumerCount => _consumers.Count;

        public void Consume(CharacterTestInteractionConsumerContext context)
        {
            var state = new DispatchState(context.Frame, _consumers.Count);
            for (int i = 0; i < _consumers.Count; i++)
            {
                ICharacterTestInteractionConsumer consumer = _consumers[i];
                if (consumer == null)
                {
                    state.SkippedConsumerCount++;
                    continue;
                }

                try
                {
                    consumer.Consume(context);
                    state.ExecutedConsumerCount++;
                    state.LastConsumerId = consumer.ConsumerId;
                }
                catch
                {
                    state.FailedConsumerCount++;
                    state.LastConsumerId = consumer.ConsumerId;
                    LastSummary = state.ToSummary();
                    throw;
                }
            }

            LastSummary = state.ToSummary();
        }

        private void AddConsumer(ICharacterTestInteractionConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentException("Interaction consumer cannot be null.", nameof(consumer));

            string consumerId = consumer.ConsumerId;
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Interaction consumer id cannot be empty.", nameof(consumer));

            if (!_consumerIds.Add(consumerId))
                throw new InvalidOperationException("Duplicate interaction consumer id: " + consumerId);

            _consumers.Add(consumer);
        }

        private static int CompareConsumers(
            ICharacterTestInteractionConsumer left,
            ICharacterTestInteractionConsumer right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int order = left.ConsumerOrder.CompareTo(right.ConsumerOrder);
            if (order != 0)
                return order;

            return string.Compare(left.ConsumerId, right.ConsumerId, StringComparison.Ordinal);
        }

        private struct DispatchState
        {
            public DispatchState(RuntimeFrame frame, int registeredConsumerCount)
            {
                Frame = frame;
                RegisteredConsumerCount = registeredConsumerCount < 0 ? 0 : registeredConsumerCount;
                ExecutedConsumerCount = 0;
                SkippedConsumerCount = 0;
                FailedConsumerCount = 0;
                LastConsumerId = string.Empty;
            }

            public RuntimeFrame Frame { get; }
            public int RegisteredConsumerCount { get; }
            public int ExecutedConsumerCount;
            public int SkippedConsumerCount;
            public int FailedConsumerCount;
            public string LastConsumerId;

            public CharacterTestInteractionConsumerDispatchSummary ToSummary()
            {
                return new CharacterTestInteractionConsumerDispatchSummary(
                    Frame,
                    RegisteredConsumerCount,
                    ExecutedConsumerCount,
                    SkippedConsumerCount,
                    FailedConsumerCount,
                    LastConsumerId);
            }
        }
    }

    public readonly struct CharacterTestInteractionConsumerDispatchSummary
    {
        public static readonly CharacterTestInteractionConsumerDispatchSummary Empty =
            new CharacterTestInteractionConsumerDispatchSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                string.Empty);

        public CharacterTestInteractionConsumerDispatchSummary(
            RuntimeFrame frame,
            int registeredConsumerCount,
            int executedConsumerCount,
            int skippedConsumerCount,
            int failedConsumerCount,
            string lastConsumerId)
        {
            Frame = frame;
            RegisteredConsumerCount = registeredConsumerCount < 0 ? 0 : registeredConsumerCount;
            ExecutedConsumerCount = executedConsumerCount < 0 ? 0 : executedConsumerCount;
            SkippedConsumerCount = skippedConsumerCount < 0 ? 0 : skippedConsumerCount;
            FailedConsumerCount = failedConsumerCount < 0 ? 0 : failedConsumerCount;
            LastConsumerId = lastConsumerId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public int RegisteredConsumerCount { get; }
        public int ExecutedConsumerCount { get; }
        public int SkippedConsumerCount { get; }
        public int FailedConsumerCount { get; }
        public string LastConsumerId { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " registered=" + RegisteredConsumerCount.ToString(CultureInfo.InvariantCulture)
                + " executed=" + ExecutedConsumerCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedConsumerCount.ToString(CultureInfo.InvariantCulture)
                + " failed=" + FailedConsumerCount.ToString(CultureInfo.InvariantCulture)
                + " last=" + (string.IsNullOrWhiteSpace(LastConsumerId) ? "none" : LastConsumerId);
        }
    }
}
