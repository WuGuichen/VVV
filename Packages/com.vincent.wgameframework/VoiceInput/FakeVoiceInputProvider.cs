using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.VoiceInput
{
    public sealed class FakeVoiceInputProvider : IVoiceInputProvider
    {
        private readonly List<PendingEvent> _pendingEvents;
        private readonly int _sourceId;

        private bool _isListening;
        private bool _isAudioActive;
        private float _level;
        private VoiceInputStatus _status;
        private VoiceInputErrorCode _lastErrorCode;
        private string _lastErrorMessage = string.Empty;

        public FakeVoiceInputProvider(int sourceId = VoiceInputOptions.DefaultSourceId, int initialEventCapacity = 8)
        {
            _sourceId = sourceId;
            _pendingEvents = new List<PendingEvent>(Math.Max(0, initialEventCapacity));
            _status = VoiceInputStatus.Idle;
            Snapshot = VoiceInputSnapshot.Idle;
        }

        public VoiceInputSnapshot Snapshot { get; private set; }

        public VoiceInputErrorCode StartErrorCode { get; set; } = VoiceInputErrorCode.None;

        public string StartErrorMessage { get; set; } = string.Empty;

        public VoiceInputResult Start()
        {
            if (StartErrorCode != VoiceInputErrorCode.None)
            {
                SetError(StartErrorCode, StartErrorMessage);
                QueueError(StartErrorCode, StartErrorMessage);
                return VoiceInputResult.Fail(StartErrorCode, StartErrorMessage);
            }

            _isListening = true;
            _status = VoiceInputStatus.Listening;
            RefreshSnapshot();
            Queue(VoiceInputEventKind.SessionStarted, VoiceInputStatus.Listening);
            return VoiceInputResult.Ok();
        }

        public VoiceInputResult Stop()
        {
            bool wasListening = _isListening;
            _isListening = false;
            _isAudioActive = false;
            _level = 0f;
            _status = VoiceInputStatus.Idle;
            RefreshSnapshot();
            if (wasListening)
            {
                Queue(VoiceInputEventKind.SessionStopped, VoiceInputStatus.Idle);
            }

            return VoiceInputResult.Ok();
        }

        public void SetAudioActivity(bool isActive, float level)
        {
            _level = level;
            if (_isAudioActive != isActive)
            {
                _isAudioActive = isActive;
                Queue(
                    isActive ? VoiceInputEventKind.AudioActivityStarted : VoiceInputEventKind.AudioActivityStopped,
                    _status,
                    VoiceInputErrorCode.None,
                    level);
            }

            RefreshSnapshot();
        }

        public void QueueTranscript(string text, bool isFinal, float confidence = 0f)
        {
            Queue(
                isFinal ? VoiceInputEventKind.FinalTranscript : VoiceInputEventKind.PartialTranscript,
                _status,
                VoiceInputErrorCode.None,
                _level,
                text,
                confidence);
        }

        public void QueueError(VoiceInputErrorCode errorCode, string message = "")
        {
            Queue(VoiceInputEventKind.Error, VoiceInputStatus.Error, errorCode, _level, message);
        }

        /// <remarks>NoAlloc after initialization when pending event capacity is sufficient.</remarks>
        public void Tick(RuntimeFrame frame, double deltaTime, IVoiceInputEventSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            for (int i = 0; i < _pendingEvents.Count; i++)
            {
                VoiceInputEvent evt = _pendingEvents[i].ToEvent(frame, _sourceId);
                sink.Enqueue(in evt);
            }

            _pendingEvents.Clear();
        }

        public void Dispose()
        {
            _pendingEvents.Clear();
        }

        private void SetError(VoiceInputErrorCode errorCode, string message)
        {
            _isListening = false;
            _isAudioActive = false;
            _status = VoiceInputStatus.Error;
            _lastErrorCode = errorCode;
            _lastErrorMessage = message ?? string.Empty;
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            Snapshot = new VoiceInputSnapshot(
                _status,
                _isListening,
                _isAudioActive,
                _level,
                lastErrorCode: _lastErrorCode,
                lastErrorMessage: _lastErrorMessage);
        }

        private void Queue(
            VoiceInputEventKind kind,
            VoiceInputStatus status,
            VoiceInputErrorCode errorCode = VoiceInputErrorCode.None,
            float level = 0f,
            string text = "",
            float confidence = 0f)
        {
            _pendingEvents.Add(new PendingEvent(kind, status, errorCode, level, text, confidence));
        }

        private readonly struct PendingEvent
        {
            public PendingEvent(
                VoiceInputEventKind kind,
                VoiceInputStatus status,
                VoiceInputErrorCode errorCode,
                float level,
                string text,
                float confidence)
            {
                Kind = kind;
                Status = status;
                ErrorCode = errorCode;
                Level = level;
                Text = text ?? string.Empty;
                Confidence = confidence;
            }

            private VoiceInputEventKind Kind { get; }
            private VoiceInputStatus Status { get; }
            private VoiceInputErrorCode ErrorCode { get; }
            private float Level { get; }
            private string Text { get; }
            private float Confidence { get; }

            public VoiceInputEvent ToEvent(RuntimeFrame frame, int sourceId)
            {
                return new VoiceInputEvent(frame, sourceId, Kind, Status, ErrorCode, Level, Text, Confidence);
            }
        }
    }
}
