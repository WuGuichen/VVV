using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestAudioIntentDrainSystem
    {
        private const int DefaultCommandCapacity = 256;

        private readonly CharacterTestAudioCuePlaybackSystem _playbackSystem;
        private readonly FixedAudioCommandBuffer _commandBuffer;
        private readonly AudioPresentationDrain _presentationDrain;
        private readonly AudioParameterValue[][][] _parameterStorage;

        public CharacterTestAudioIntentDrainSystem(
            CharacterTestAudioCuePlaybackSystem playbackSystem,
            int commandCapacity = DefaultCommandCapacity)
        {
            if (commandCapacity <= 0)
                commandCapacity = 1;
            _playbackSystem = playbackSystem;
            _commandBuffer = new FixedAudioCommandBuffer(commandCapacity);
            _presentationDrain = new AudioPresentationDrain(new AudioPresentationDrainOptions
            {
                ControlledHandleCapacity = commandCapacity,
                DedupeHistoryCapacity = commandCapacity * 2,
                GlobalVoiceLimit = commandCapacity,
                CategoryVoiceLimit = commandCapacity,
                EventVoiceLimit = commandCapacity,
                EmitterVoiceLimit = commandCapacity
            });
            _parameterStorage = CreateParameterStorage(commandCapacity);
            LastSummary = CharacterTestAudioIntentDrainSummary.Empty;
        }

        public CharacterTestAudioIntentDrainSummary LastSummary { get; private set; }
        internal AudioPresentationDrain PresentationDrain => _presentationDrain;

        /// <remarks>NoAlloc after initialization when the playback backend does not allocate for accepted requests.</remarks>
        public CharacterTestAudioIntentDrainSummary Tick(
            CharacterTestAudioIntentQueue queue,
            RuntimeFrame frame)
        {
            if (queue == null)
            {
                LastSummary = new CharacterTestAudioIntentDrainSummary(
                    frame,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
                return LastSummary;
            }

            _commandBuffer.Clear();
            _commandBuffer.ResetDroppedCount();
            var state = new CharacterTestAudioIntentDrainFrameState(frame, queue.Count, queue.DroppedCount);
            for (int i = 0; i < queue.Count; i++)
            {
                if (i >= _parameterStorage.Length)
                {
                    state.DroppedCount++;
                    state.SkippedCount++;
                    continue;
                }
                CharacterTestAudioIntent intent = queue[i];
                int parameterCount = ResolveParameterCount(intent);
                AudioParameterValue[] parameters = _parameterStorage[i][parameterCount];
                intent.CopyParametersTo(parameters);
                if (_playbackSystem != null
                    && _playbackSystem.TryCreateCommand(
                        intent,
                        parameterCount == 0 ? null : parameters,
                        parameterCount,
                        i,
                        out AudioCommand command))
                {
                    state.LastCueId = intent.CueId;
                    state.LastEventId = command.Request.EventId;
                    state.LastEmitterId = intent.EmitterId;
                    if (!_commandBuffer.TryEnqueue(command))
                    {
                        state.DroppedCount++;
                        state.SkippedCount++;
                    }
                }
                else
                {
                    state.FailedCount++;
                }
            }

            if (_playbackSystem?.AudioService != null)
            {
                _presentationDrain.Drain(
                    _commandBuffer,
                    _playbackSystem.AudioService,
                    isReplay: false,
                    isConfirmed: true);
                AudioPresentationDrainSnapshot snapshot = _presentationDrain.Snapshot;
                _playbackSystem.ApplyCommandDrainSnapshot(snapshot);
                state.PlayedCount += snapshot.Accepted;
                state.FailedCount += snapshot.Failed + snapshot.BudgetRejected;
                state.SkippedCount += snapshot.SuppressedReplay
                    + snapshot.AwaitingConfirmation
                    + snapshot.Deduped
                    + snapshot.BudgetRejected;
            }

            LastSummary = state.ToSummary();
            return LastSummary;
        }

        private static int ResolveParameterCount(in CharacterTestAudioIntent intent)
        {
            if (intent.ParameterCount <= 0)
                return 0;

            int count = intent.ParameterCount > CharacterTestAudioIntent.MaxParameterCount
                ? CharacterTestAudioIntent.MaxParameterCount
                : intent.ParameterCount;
            return count;
        }

        private static AudioParameterValue[][][] CreateParameterStorage(int commandCapacity)
        {
            var storage = new AudioParameterValue[commandCapacity][][];
            for (int i = 0; i < storage.Length; i++)
            {
                storage[i] = new AudioParameterValue[CharacterTestAudioIntent.MaxParameterCount + 1][];
                storage[i][0] = AudioPlayRequest.EmptyParameters;
                for (int count = 1; count < storage[i].Length; count++)
                    storage[i][count] = new AudioParameterValue[count];
            }
            return storage;
        }

        private struct CharacterTestAudioIntentDrainFrameState
        {
            public CharacterTestAudioIntentDrainFrameState(
                RuntimeFrame frame,
                int requestCount,
                int droppedCount)
            {
                Frame = frame;
                RequestCount = requestCount < 0 ? 0 : requestCount;
                PlayedCount = 0;
                FailedCount = 0;
                DroppedCount = droppedCount < 0 ? 0 : droppedCount;
                SkippedCount = DroppedCount;
                LastCueId = 0;
                LastEventId = 0;
                LastEmitterId = 0;
            }

            public RuntimeFrame Frame;
            public int RequestCount;
            public int PlayedCount;
            public int FailedCount;
            public int DroppedCount;
            public int SkippedCount;
            public int LastCueId;
            public int LastEventId;
            public int LastEmitterId;

            public CharacterTestAudioIntentDrainSummary ToSummary()
            {
                return new CharacterTestAudioIntentDrainSummary(
                    Frame,
                    RequestCount,
                    PlayedCount,
                    FailedCount,
                    DroppedCount,
                    SkippedCount,
                    LastCueId,
                    LastEventId,
                    LastEmitterId);
            }
        }
    }

    public readonly struct CharacterTestAudioIntentDrainSummary
    {
        public static readonly CharacterTestAudioIntentDrainSummary Empty =
            new CharacterTestAudioIntentDrainSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

        public CharacterTestAudioIntentDrainSummary(
            RuntimeFrame frame,
            int requestCount,
            int playedCount,
            int failedCount,
            int droppedCount,
            int skippedCount,
            int lastCueId,
            int lastEventId,
            int lastEmitterId)
        {
            Frame = frame;
            RequestCount = requestCount < 0 ? 0 : requestCount;
            PlayedCount = playedCount < 0 ? 0 : playedCount;
            FailedCount = failedCount < 0 ? 0 : failedCount;
            DroppedCount = droppedCount < 0 ? 0 : droppedCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            LastCueId = lastCueId;
            LastEventId = lastEventId < 0 ? 0 : lastEventId;
            LastEmitterId = lastEmitterId < 0 ? 0 : lastEmitterId;
        }

        public RuntimeFrame Frame { get; }
        public int RequestCount { get; }
        public int PlayedCount { get; }
        public int FailedCount { get; }
        public int DroppedCount { get; }
        public int SkippedCount { get; }
        public int LastCueId { get; }
        public int LastEventId { get; }
        public int LastEmitterId { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " requests=" + RequestCount.ToString(CultureInfo.InvariantCulture)
                + " played=" + PlayedCount.ToString(CultureInfo.InvariantCulture)
                + " failed=" + FailedCount.ToString(CultureInfo.InvariantCulture)
                + " dropped=" + DroppedCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedCount.ToString(CultureInfo.InvariantCulture)
                + " lastCue=" + (LastCueId == 0 ? "none" : LastCueId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                + " lastEvent=" + LastEventId.ToString(CultureInfo.InvariantCulture)
                + " lastEmitter=" + LastEmitterId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
