using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.VoiceInput
{
    public readonly struct VoiceInputScriptedEvent
    {
        public VoiceInputScriptedEvent(
            RuntimeFrame frame,
            VoiceInputEventKind kind,
            VoiceInputStatus status = VoiceInputStatus.Unknown,
            VoiceInputErrorCode errorCode = VoiceInputErrorCode.None,
            float level = 0f,
            string text = "",
            float confidence = 0f,
            int sourceId = 0)
        {
            Frame = frame;
            Kind = kind;
            Status = status;
            ErrorCode = errorCode;
            Level = level;
            Text = text ?? string.Empty;
            Confidence = confidence;
            SourceId = sourceId;
        }

        public RuntimeFrame Frame { get; }
        public VoiceInputEventKind Kind { get; }
        public VoiceInputStatus Status { get; }
        public VoiceInputErrorCode ErrorCode { get; }
        public float Level { get; }
        public string Text { get; }
        public float Confidence { get; }
        public int SourceId { get; }
    }

    public sealed class ScriptedVoiceInputProvider : IVoiceInputProvider
    {
        private readonly Entry[] _entries;
        private readonly int _sourceId;
        private int _nextIndex;
        private VoiceInputStatus _status = VoiceInputStatus.Idle;
        private bool _isListening;
        private bool _isAudioActive;
        private float _level;
        private string _lastTranscript = string.Empty;
        private float _lastTranscriptConfidence;
        private VoiceInputErrorCode _lastErrorCode;
        private string _lastErrorMessage = string.Empty;

        public ScriptedVoiceInputProvider(
            IReadOnlyList<VoiceInputScriptedEvent> events,
            int sourceId = VoiceInputOptions.DefaultSourceId)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            _sourceId = sourceId;
            _entries = new Entry[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                _entries[i] = new Entry(events[i], i);
            }

            Array.Sort(_entries, EntryComparer.Instance);
            Snapshot = VoiceInputSnapshot.Idle;
        }

        public VoiceInputSnapshot Snapshot { get; private set; }

        public VoiceInputResult Start()
        {
            _isListening = true;
            _status = VoiceInputStatus.Listening;
            RefreshSnapshot();
            return VoiceInputResult.Ok();
        }

        public VoiceInputResult Stop()
        {
            _isListening = false;
            _isAudioActive = false;
            _level = 0f;
            _status = VoiceInputStatus.Idle;
            RefreshSnapshot();
            return VoiceInputResult.Ok();
        }

        /// <remarks>NoAlloc after construction.</remarks>
        public void Tick(RuntimeFrame frame, double deltaTime, IVoiceInputEventSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            while (_nextIndex < _entries.Length && _entries[_nextIndex].Event.Frame <= frame)
            {
                VoiceInputScriptedEvent scripted = _entries[_nextIndex].Event;
                VoiceInputEvent evt = ToEvent(scripted);
                ApplyEvent(evt);
                sink.Enqueue(in evt);
                _nextIndex++;
            }

            RefreshSnapshot();
        }

        public void Dispose()
        {
        }

        private VoiceInputEvent ToEvent(VoiceInputScriptedEvent scripted)
        {
            int sourceId = scripted.SourceId != 0 ? scripted.SourceId : _sourceId;
            return new VoiceInputEvent(
                scripted.Frame,
                sourceId,
                scripted.Kind,
                scripted.Status,
                scripted.ErrorCode,
                scripted.Level,
                scripted.Text,
                scripted.Confidence);
        }

        private void ApplyEvent(VoiceInputEvent evt)
        {
            if (evt.Status != VoiceInputStatus.Unknown)
            {
                _status = evt.Status;
                _isListening = evt.Status == VoiceInputStatus.Listening;
            }

            if (evt.Kind == VoiceInputEventKind.SessionStarted)
            {
                _status = VoiceInputStatus.Listening;
                _isListening = true;
            }
            else if (evt.Kind == VoiceInputEventKind.SessionStopped)
            {
                _status = VoiceInputStatus.Idle;
                _isListening = false;
                _isAudioActive = false;
            }
            else if (evt.Kind == VoiceInputEventKind.AudioActivityStarted)
            {
                _isAudioActive = true;
            }
            else if (evt.Kind == VoiceInputEventKind.AudioActivityStopped)
            {
                _isAudioActive = false;
            }
            else if (evt.Kind == VoiceInputEventKind.PartialTranscript || evt.Kind == VoiceInputEventKind.FinalTranscript)
            {
                _lastTranscript = evt.Text;
                _lastTranscriptConfidence = evt.Confidence;
            }
            else if (evt.Kind == VoiceInputEventKind.Error)
            {
                _status = VoiceInputStatus.Error;
                _isListening = false;
                _lastErrorCode = evt.ErrorCode;
                _lastErrorMessage = evt.Text;
            }

            if (evt.Kind == VoiceInputEventKind.AudioLevel || evt.Kind == VoiceInputEventKind.AudioActivityStarted || evt.Kind == VoiceInputEventKind.AudioActivityStopped)
            {
                _level = evt.Level;
            }
        }

        private void RefreshSnapshot()
        {
            Snapshot = new VoiceInputSnapshot(
                _status,
                _isListening,
                _isAudioActive,
                _level,
                lastErrorCode: _lastErrorCode,
                lastErrorMessage: _lastErrorMessage,
                lastTranscript: _lastTranscript,
                lastTranscriptConfidence: _lastTranscriptConfidence);
        }

        private readonly struct Entry
        {
            public Entry(VoiceInputScriptedEvent evt, int sequence)
            {
                Event = evt;
                Sequence = sequence;
            }

            public VoiceInputScriptedEvent Event { get; }
            public int Sequence { get; }
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            public static readonly EntryComparer Instance = new EntryComparer();

            public int Compare(Entry x, Entry y)
            {
                int frame = x.Event.Frame.CompareTo(y.Event.Frame);
                return frame != 0 ? frame : x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
