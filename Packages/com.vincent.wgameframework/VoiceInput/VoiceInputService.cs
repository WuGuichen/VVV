using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.VoiceInput
{
    public sealed class VoiceInputService : IVoiceInputService, IVoiceInputEventSink
    {
        private readonly IVoiceInputProvider _provider;
        private readonly VoiceInputOptions _options;
        private readonly RuntimeEventQueue<VoiceInputEvent> _events;
        private readonly bool _disposeProvider;

        private VoiceInputSnapshot _snapshot;
        private RuntimeFrame _lastEventFrame;
        private int _totalEventCount;
        private string _lastTranscript = string.Empty;
        private float _lastTranscriptConfidence;
        private VoiceInputErrorCode _lastErrorCode;
        private string _lastErrorMessage = string.Empty;

        public VoiceInputService(
            IVoiceInputProvider provider,
            VoiceInputOptions options = null,
            bool disposeProvider = false)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _options = options ?? new VoiceInputOptions();
            int initialCapacity = _options.InitialEventCapacity < 0 ? 0 : _options.InitialEventCapacity;
            _events = new RuntimeEventQueue<VoiceInputEvent>(initialCapacity);
            _disposeProvider = disposeProvider;
            _snapshot = _provider.Snapshot;
        }

        public VoiceInputSnapshot Snapshot => _snapshot;

        public VoiceInputResult Start()
        {
            VoiceInputResult result = _provider.Start();
            RefreshSnapshot();
            return result;
        }

        public VoiceInputResult Stop()
        {
            VoiceInputResult result = _provider.Stop();
            RefreshSnapshot();
            return result;
        }

        /// <remarks>NoAlloc after initialization when the provider and event queue stay within their configured capacities.</remarks>
        public void Tick(RuntimeFrame frame, double deltaTime)
        {
            _provider.Tick(frame, deltaTime, this);
            RefreshSnapshot();
        }

        /// <remarks>NoAlloc when the caller-owned destination has enough capacity.</remarks>
        public int DrainEvents(RuntimeFrame frame, List<VoiceInputEvent> destination)
        {
            return _events.Drain(frame, destination);
        }

        public RuntimeEventQueueSnapshot CreateEventQueueSnapshot()
        {
            return _events.CreateSnapshot();
        }

        public void ClearEvents()
        {
            _events.Clear();
            _lastEventFrame = RuntimeFrame.Zero;
            _totalEventCount = 0;
            _lastTranscript = string.Empty;
            _lastTranscriptConfidence = 0f;
            _lastErrorCode = VoiceInputErrorCode.None;
            _lastErrorMessage = string.Empty;
            RefreshSnapshot();
        }

        public void Enqueue(in VoiceInputEvent evt)
        {
            VoiceInputEvent accepted = evt.SourceId != 0
                ? evt
                : new VoiceInputEvent(
                    evt.Frame,
                    _options.SourceId,
                    evt.Kind,
                    evt.Status,
                    evt.ErrorCode,
                    evt.Level,
                    evt.Text,
                    evt.Confidence,
                    evt.TraceId);

            _events.Enqueue(accepted.Frame, accepted);
            _lastEventFrame = accepted.Frame;
            _totalEventCount++;

            if (accepted.Kind == VoiceInputEventKind.PartialTranscript || accepted.Kind == VoiceInputEventKind.FinalTranscript)
            {
                _lastTranscript = accepted.Text;
                _lastTranscriptConfidence = accepted.Confidence;
            }

            if (accepted.Kind == VoiceInputEventKind.Error || accepted.ErrorCode != VoiceInputErrorCode.None)
            {
                _lastErrorCode = accepted.ErrorCode == VoiceInputErrorCode.None ? VoiceInputErrorCode.Unknown : accepted.ErrorCode;
                _lastErrorMessage = accepted.Text;
            }
        }

        public void Dispose()
        {
            if (_disposeProvider)
            {
                _provider.Dispose();
            }
        }

        private void RefreshSnapshot()
        {
            _snapshot = _provider.Snapshot.WithRuntimeSummary(
                _lastEventFrame,
                _totalEventCount,
                _lastTranscript,
                _lastTranscriptConfidence,
                _lastErrorCode,
                _lastErrorMessage);
        }
    }
}
