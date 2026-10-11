using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.VoiceInput.Unity
{
    public sealed class UnityMicrophoneVoiceInputOptions
    {
        public string DeviceName { get; set; } = string.Empty;

        public int SampleRate { get; set; } = 16000;

        public int ClipLengthSeconds { get; set; } = 1;

        public int SampleWindowSize { get; set; } = 512;

        public float ActivityThreshold { get; set; } = 0.02f;

        public float SilenceReleaseSeconds { get; set; } = 0.2f;

        public bool RequireUserAuthorization { get; set; } = true;
    }

    public interface IUnityMicrophoneGateway
    {
        string[] Devices { get; }
        bool HasMicrophoneAuthorization { get; }
        bool IsMicrophoneAuthorizationRequestDone { get; }
        void RequestMicrophoneAuthorization();
        AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency);
        void End(string deviceName);
        bool IsRecording(string deviceName);
        int GetPosition(string deviceName);
    }

    public sealed class UnityMicrophoneGateway : IUnityMicrophoneGateway
    {
        private AsyncOperation _authorizationRequest;

        public string[] Devices => Microphone.devices;

        public bool HasMicrophoneAuthorization => Application.HasUserAuthorization(UserAuthorization.Microphone);

        public bool IsMicrophoneAuthorizationRequestDone => _authorizationRequest == null || _authorizationRequest.isDone;

        public void RequestMicrophoneAuthorization()
        {
            if (_authorizationRequest == null)
            {
                _authorizationRequest = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
        }

        public AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency)
        {
            return Microphone.Start(deviceName, loop, lengthSec, frequency);
        }

        public void End(string deviceName)
        {
            Microphone.End(deviceName);
        }

        public bool IsRecording(string deviceName)
        {
            return Microphone.IsRecording(deviceName);
        }

        public int GetPosition(string deviceName)
        {
            return Microphone.GetPosition(deviceName);
        }
    }

    public sealed class UnityMicrophoneVoiceInputProvider : IVoiceInputProvider
    {
        private readonly UnityMicrophoneVoiceInputOptions _options;
        private readonly IUnityMicrophoneGateway _gateway;
        private readonly int _sourceId;
        private readonly List<PendingEvent> _pendingEvents = new List<PendingEvent>(4);

        private AudioClip _clip;
        private string _deviceName = string.Empty;
        private float[] _sampleBuffer;
        private bool _waitingForPermission;
        private bool _isListening;
        private bool _isAudioActive;
        private float _silenceSeconds;
        private float _level;
        private VoiceInputStatus _status = VoiceInputStatus.Idle;
        private VoiceInputErrorCode _lastErrorCode;
        private string _lastErrorMessage = string.Empty;

        public UnityMicrophoneVoiceInputProvider(
            UnityMicrophoneVoiceInputOptions options = null,
            IUnityMicrophoneGateway gateway = null,
            int sourceId = VoiceInputOptions.DefaultSourceId)
        {
            _options = options ?? new UnityMicrophoneVoiceInputOptions();
            _gateway = gateway ?? new UnityMicrophoneGateway();
            _sourceId = sourceId;
            Snapshot = VoiceInputSnapshot.Idle;
        }

        public VoiceInputSnapshot Snapshot { get; private set; }

        public VoiceInputResult Start()
        {
            if (_isListening || _waitingForPermission)
            {
                return VoiceInputResult.Ok();
            }

            if (!TryResolveDevice(out string deviceName, out VoiceInputResult deviceResult))
            {
                Fail(VoiceInputStatus.Unavailable, deviceResult.ErrorCode, deviceResult.Message);
                return deviceResult;
            }

            _deviceName = deviceName;
            if (_options.RequireUserAuthorization && !_gateway.HasMicrophoneAuthorization)
            {
                _waitingForPermission = true;
                _status = VoiceInputStatus.PermissionRequired;
                RefreshSnapshot();
                _gateway.RequestMicrophoneAuthorization();
                return VoiceInputResult.Ok();
            }

            return StartRecording();
        }

        public VoiceInputResult Stop()
        {
            bool wasActive = _isListening || _waitingForPermission;
            if (_clip != null)
            {
                _gateway.End(_deviceName);
            }

            _clip = null;
            _waitingForPermission = false;
            _isListening = false;
            _isAudioActive = false;
            _silenceSeconds = 0f;
            _level = 0f;
            _status = VoiceInputStatus.Idle;
            RefreshSnapshot();

            if (wasActive)
            {
                Queue(VoiceInputEventKind.SessionStopped, VoiceInputStatus.Idle);
            }

            return VoiceInputResult.Ok();
        }

        /// <remarks>NoAlloc after recording starts and the sample buffer is allocated.</remarks>
        public void Tick(RuntimeFrame frame, double deltaTime, IVoiceInputEventSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            EmitPending(frame, sink);
            if (_waitingForPermission && _gateway.IsMicrophoneAuthorizationRequestDone)
            {
                _waitingForPermission = false;
                if (!_gateway.HasMicrophoneAuthorization)
                {
                    Fail(VoiceInputStatus.PermissionDenied, VoiceInputErrorCode.PermissionDenied, "Microphone permission denied.");
                }
                else
                {
                    StartRecording();
                }
            }

            if (_isListening)
            {
                UpdateLevel(deltaTime);
            }

            EmitPending(frame, sink);
        }

        public void Dispose()
        {
            Stop();
            _pendingEvents.Clear();
            _sampleBuffer = null;
        }

        private bool TryResolveDevice(out string deviceName, out VoiceInputResult result)
        {
            string[] devices = _gateway.Devices;
            if (devices == null || devices.Length == 0)
            {
                deviceName = string.Empty;
                result = VoiceInputResult.Fail(VoiceInputErrorCode.NoDevice, "No microphone device is available.");
                return false;
            }

            string requested = _options.DeviceName ?? string.Empty;
            if (requested.Length == 0)
            {
                deviceName = devices[0];
                result = VoiceInputResult.Ok();
                return true;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i], requested, StringComparison.Ordinal))
                {
                    deviceName = requested;
                    result = VoiceInputResult.Ok();
                    return true;
                }
            }

            deviceName = string.Empty;
            result = VoiceInputResult.Fail(VoiceInputErrorCode.NoDevice, "Requested microphone device is not available.");
            return false;
        }

        private VoiceInputResult StartRecording()
        {
            int clipLengthSeconds = Math.Max(1, _options.ClipLengthSeconds);
            int sampleRate = Math.Max(8000, _options.SampleRate);
            _status = VoiceInputStatus.Starting;
            RefreshSnapshot();
            _clip = _gateway.Start(_deviceName, true, clipLengthSeconds, sampleRate);
            if (_clip == null)
            {
                Fail(VoiceInputStatus.Error, VoiceInputErrorCode.StartFailed, "Microphone start failed.");
                return VoiceInputResult.Fail(VoiceInputErrorCode.StartFailed, "Microphone start failed.");
            }

            int sampleWindowSize = Math.Max(1, _options.SampleWindowSize);
            if (_sampleBuffer == null || _sampleBuffer.Length != sampleWindowSize)
            {
                _sampleBuffer = new float[sampleWindowSize];
            }

            _isListening = true;
            _isAudioActive = false;
            _silenceSeconds = 0f;
            _level = 0f;
            _status = VoiceInputStatus.Listening;
            RefreshSnapshot();
            Queue(VoiceInputEventKind.SessionStarted, VoiceInputStatus.Listening);
            return VoiceInputResult.Ok();
        }

        private void UpdateLevel(double deltaTime)
        {
            if (_clip == null || !_gateway.IsRecording(_deviceName))
            {
                Fail(VoiceInputStatus.Error, VoiceInputErrorCode.CaptureFailed, "Microphone stopped recording.");
                return;
            }

            int position = _gateway.GetPosition(_deviceName);
            if (position < 0)
            {
                Fail(VoiceInputStatus.Error, VoiceInputErrorCode.CaptureFailed, "Microphone returned an invalid sample position.");
                return;
            }

            int maxOffset = Math.Max(0, _clip.samples - _sampleBuffer.Length);
            int offset = position - _sampleBuffer.Length;
            if (offset < 0)
            {
                offset = 0;
            }
            else if (offset > maxOffset)
            {
                offset = maxOffset;
            }

            if (!_clip.GetData(_sampleBuffer, offset))
            {
                Fail(VoiceInputStatus.Error, VoiceInputErrorCode.CaptureFailed, "Microphone sample read failed.");
                return;
            }

            double sum = 0d;
            for (int i = 0; i < _sampleBuffer.Length; i++)
            {
                float sample = _sampleBuffer[i];
                sum += sample * sample;
            }

            _level = (float)Math.Sqrt(sum / _sampleBuffer.Length);
            bool activeNow = _level >= _options.ActivityThreshold;
            if (activeNow)
            {
                _silenceSeconds = 0f;
                if (!_isAudioActive)
                {
                    _isAudioActive = true;
                    Queue(VoiceInputEventKind.AudioActivityStarted, VoiceInputStatus.Listening, VoiceInputErrorCode.None, _level);
                }
            }
            else if (_isAudioActive)
            {
                _silenceSeconds += (float)deltaTime;
                if (_silenceSeconds >= _options.SilenceReleaseSeconds)
                {
                    _isAudioActive = false;
                    Queue(VoiceInputEventKind.AudioActivityStopped, VoiceInputStatus.Listening, VoiceInputErrorCode.None, _level);
                }
            }

            RefreshSnapshot();
        }

        private void Fail(VoiceInputStatus status, VoiceInputErrorCode errorCode, string message)
        {
            _isListening = false;
            _isAudioActive = false;
            _waitingForPermission = false;
            _status = status;
            _lastErrorCode = errorCode;
            _lastErrorMessage = message ?? string.Empty;
            RefreshSnapshot();
            Queue(VoiceInputEventKind.Error, status, errorCode, _level, _lastErrorMessage);
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
            string text = "")
        {
            _pendingEvents.Add(new PendingEvent(kind, status, errorCode, level, text));
        }

        private void EmitPending(RuntimeFrame frame, IVoiceInputEventSink sink)
        {
            for (int i = 0; i < _pendingEvents.Count; i++)
            {
                VoiceInputEvent evt = _pendingEvents[i].ToEvent(frame, _sourceId);
                sink.Enqueue(in evt);
            }

            _pendingEvents.Clear();
        }

        private readonly struct PendingEvent
        {
            public PendingEvent(
                VoiceInputEventKind kind,
                VoiceInputStatus status,
                VoiceInputErrorCode errorCode,
                float level,
                string text)
            {
                Kind = kind;
                Status = status;
                ErrorCode = errorCode;
                Level = level;
                Text = text ?? string.Empty;
            }

            private VoiceInputEventKind Kind { get; }
            private VoiceInputStatus Status { get; }
            private VoiceInputErrorCode ErrorCode { get; }
            private float Level { get; }
            private string Text { get; }

            public VoiceInputEvent ToEvent(RuntimeFrame frame, int sourceId)
            {
                return new VoiceInputEvent(frame, sourceId, Kind, Status, ErrorCode, Level, Text);
            }
        }
    }
}
