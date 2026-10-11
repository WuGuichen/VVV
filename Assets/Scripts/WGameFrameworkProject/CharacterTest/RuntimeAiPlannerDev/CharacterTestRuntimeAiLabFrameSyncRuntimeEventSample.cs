using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample
    {
        public CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
            int pendingCount,
            int oldestFrame,
            int newestFrame,
            long nextSequence,
            string eventTypeName,
            int recentCount,
            long recentHash,
            int lastFrame,
            int lastType,
            int lastCommandId,
            int lastSourceId,
            int lastTargetId,
            int lastEntityId,
            int lastPayload0,
            int lastPayload1,
            int lastPayload2,
            string lastMessage)
        {
            Available = true;
            PendingCount = pendingCount;
            OldestFrame = oldestFrame;
            NewestFrame = newestFrame;
            NextSequence = nextSequence;
            EventTypeName = eventTypeName ?? string.Empty;
            RecentCount = recentCount;
            RecentHash = recentHash;
            LastFrame = lastFrame;
            LastType = lastType;
            LastCommandId = lastCommandId;
            LastSourceId = lastSourceId;
            LastTargetId = lastTargetId;
            LastEntityId = lastEntityId;
            LastPayload0 = lastPayload0;
            LastPayload1 = lastPayload1;
            LastPayload2 = lastPayload2;
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool Available { get; }
        public int PendingCount { get; }
        public int OldestFrame { get; }
        public int NewestFrame { get; }
        public long NextSequence { get; }
        public string EventTypeName { get; }
        public int RecentCount { get; }
        public long RecentHash { get; }
        public int LastFrame { get; }
        public int LastType { get; }
        public int LastCommandId { get; }
        public int LastSourceId { get; }
        public int LastTargetId { get; }
        public int LastEntityId { get; }
        public int LastPayload0 { get; }
        public int LastPayload1 { get; }
        public int LastPayload2 { get; }
        public string LastMessage { get; }

        public static CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample Capture(
            RuntimeEventQueueSnapshot queue,
            IReadOnlyList<CharacterTestRuntimeEvent> recentEvents)
        {
            int recentCount = recentEvents != null ? recentEvents.Count : 0;
            CharacterTestRuntimeEvent lastEvent = recentCount > 0
                ? recentEvents[recentCount - 1]
                : default;
            return new CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
                queue.PendingCount,
                ToIntFrame(queue.OldestFrame),
                ToIntFrame(queue.NewestFrame),
                queue.NextSequence,
                queue.EventTypeName,
                recentCount,
                ComputeRecentHash(recentEvents),
                recentCount > 0 ? ToIntFrame(lastEvent.Frame) : 0,
                recentCount > 0 ? (int)lastEvent.Type : 0,
                recentCount > 0 ? lastEvent.CommandId : 0,
                recentCount > 0 ? lastEvent.SourceId : 0,
                recentCount > 0 ? lastEvent.TargetId : 0,
                recentCount > 0 ? lastEvent.EntityId : 0,
                recentCount > 0 ? lastEvent.Payload0 : 0,
                recentCount > 0 ? lastEvent.Payload1 : 0,
                recentCount > 0 ? lastEvent.Payload2 : 0,
                recentCount > 0 ? lastEvent.Message : string.Empty);
        }

        private static int ToIntFrame(RuntimeFrame frame)
        {
            if (frame.Value > int.MaxValue)
                return int.MaxValue;
            if (frame.Value < int.MinValue)
                return int.MinValue;
            return (int)frame.Value;
        }

        private static long ComputeRecentHash(IReadOnlyList<CharacterTestRuntimeEvent> events)
        {
            if (events == null || events.Count == 0)
                return 0L;

            unchecked
            {
                long hash = 1469598103934665603L;
                for (int i = 0; i < events.Count; i++)
                {
                    CharacterTestRuntimeEvent evt = events[i];
                    hash = AddHash(hash, evt.Frame.Value);
                    hash = AddHash(hash, (int)evt.Type);
                    hash = AddHash(hash, evt.CommandId);
                    hash = AddHash(hash, evt.SourceId);
                    hash = AddHash(hash, evt.TargetId);
                    hash = AddHash(hash, evt.EntityId);
                    hash = AddHash(hash, evt.Payload0);
                    hash = AddHash(hash, evt.Payload1);
                    hash = AddHash(hash, evt.Payload2);
                    hash = AddHash(hash, i);
                    hash = AddStringHash(hash, evt.Message);
                }

                return hash;
            }
        }

        private static long AddHash(long hash, long value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 1099511628211L;
            }
        }

        private static long AddStringHash(long hash, string value)
        {
            if (string.IsNullOrEmpty(value))
                return AddHash(hash, 0L);

            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                    hash = AddHash(hash, value[i]);
                return hash;
            }
        }
    }
}
