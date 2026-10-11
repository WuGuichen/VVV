using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.VoiceInput
{
    public enum VoiceInputStatus
    {
        Unknown = 0,
        Unavailable = 1,
        Idle = 2,
        PermissionRequired = 3,
        PermissionDenied = 4,
        Starting = 5,
        Listening = 6,
        Stopping = 7,
        Error = 8
    }

    public enum VoiceInputEventKind
    {
        Unknown = 0,
        SessionStarted = 10,
        SessionStopped = 11,
        AudioActivityStarted = 20,
        AudioActivityStopped = 21,
        AudioLevel = 22,
        PartialTranscript = 30,
        FinalTranscript = 31,
        Error = 40
    }

    public enum VoiceInputErrorCode
    {
        None = 0,
        ProviderUnavailable = 10,
        PermissionDenied = 20,
        NoDevice = 30,
        StartFailed = 40,
        CaptureFailed = 50,
        InvalidOperation = 60,
        Unknown = 100
    }

    public sealed class VoiceInputOptions
    {
        public const int DefaultSourceId = 710000;
        public const int DefaultInitialEventCapacity = 16;

        public int SourceId { get; set; } = DefaultSourceId;

        public int InitialEventCapacity { get; set; } = DefaultInitialEventCapacity;
    }

    public readonly struct VoiceInputResult
    {
        public VoiceInputResult(bool success, VoiceInputErrorCode errorCode, string message)
        {
            Success = success;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public VoiceInputErrorCode ErrorCode { get; }
        public string Message { get; }

        public static VoiceInputResult Ok()
        {
            return new VoiceInputResult(true, VoiceInputErrorCode.None, string.Empty);
        }

        public static VoiceInputResult Fail(VoiceInputErrorCode errorCode, string message = "")
        {
            return new VoiceInputResult(false, errorCode == VoiceInputErrorCode.None ? VoiceInputErrorCode.Unknown : errorCode, message);
        }
    }

    public readonly struct VoiceInputSnapshot : IEquatable<VoiceInputSnapshot>
    {
        public VoiceInputSnapshot(
            VoiceInputStatus status,
            bool isListening,
            bool isAudioActive,
            float level,
            RuntimeFrame lastEventFrame = default,
            int totalEventCount = 0,
            VoiceInputErrorCode lastErrorCode = VoiceInputErrorCode.None,
            string lastErrorMessage = "",
            string lastTranscript = "",
            float lastTranscriptConfidence = 0f)
        {
            Status = status;
            IsListening = isListening;
            IsAudioActive = isAudioActive;
            Level = level;
            LastEventFrame = lastEventFrame;
            TotalEventCount = totalEventCount;
            LastErrorCode = lastErrorCode;
            LastErrorMessage = lastErrorMessage ?? string.Empty;
            LastTranscript = lastTranscript ?? string.Empty;
            LastTranscriptConfidence = lastTranscriptConfidence;
        }

        public VoiceInputStatus Status { get; }
        public bool IsListening { get; }
        public bool IsAudioActive { get; }
        public float Level { get; }
        public RuntimeFrame LastEventFrame { get; }
        public int TotalEventCount { get; }
        public VoiceInputErrorCode LastErrorCode { get; }
        public string LastErrorMessage { get; }
        public string LastTranscript { get; }
        public float LastTranscriptConfidence { get; }

        public static VoiceInputSnapshot Idle => new VoiceInputSnapshot(VoiceInputStatus.Idle, false, false, 0f);

        public VoiceInputSnapshot WithRuntimeSummary(
            RuntimeFrame lastEventFrame,
            int totalEventCount,
            string lastTranscript,
            float lastTranscriptConfidence,
            VoiceInputErrorCode lastErrorCode,
            string lastErrorMessage)
        {
            return new VoiceInputSnapshot(
                Status,
                IsListening,
                IsAudioActive,
                Level,
                lastEventFrame,
                totalEventCount,
                lastErrorCode,
                lastErrorMessage,
                lastTranscript,
                lastTranscriptConfidence);
        }

        public bool Equals(VoiceInputSnapshot other)
        {
            return Status == other.Status
                && IsListening == other.IsListening
                && IsAudioActive == other.IsAudioActive
                && Level.Equals(other.Level)
                && LastEventFrame.Equals(other.LastEventFrame)
                && TotalEventCount == other.TotalEventCount
                && LastErrorCode == other.LastErrorCode
                && LastErrorMessage == other.LastErrorMessage
                && LastTranscript == other.LastTranscript
                && LastTranscriptConfidence.Equals(other.LastTranscriptConfidence);
        }

        public override bool Equals(object obj)
        {
            return obj is VoiceInputSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Status;
                hashCode = (hashCode * 397) ^ IsListening.GetHashCode();
                hashCode = (hashCode * 397) ^ IsAudioActive.GetHashCode();
                hashCode = (hashCode * 397) ^ Level.GetHashCode();
                hashCode = (hashCode * 397) ^ LastEventFrame.GetHashCode();
                hashCode = (hashCode * 397) ^ TotalEventCount;
                hashCode = (hashCode * 397) ^ (int)LastErrorCode;
                hashCode = (hashCode * 397) ^ LastErrorMessage.GetHashCode();
                hashCode = (hashCode * 397) ^ LastTranscript.GetHashCode();
                hashCode = (hashCode * 397) ^ LastTranscriptConfidence.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct VoiceInputEvent : IEquatable<VoiceInputEvent>
    {
        public VoiceInputEvent(
            RuntimeFrame frame,
            int sourceId,
            VoiceInputEventKind kind,
            VoiceInputStatus status = VoiceInputStatus.Unknown,
            VoiceInputErrorCode errorCode = VoiceInputErrorCode.None,
            float level = 0f,
            string text = "",
            float confidence = 0f,
            string traceId = "")
        {
            Frame = frame;
            SourceId = sourceId;
            Kind = kind;
            Status = status;
            ErrorCode = errorCode;
            Level = level;
            Text = text ?? string.Empty;
            Confidence = confidence;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public VoiceInputEventKind Kind { get; }
        public VoiceInputStatus Status { get; }
        public VoiceInputErrorCode ErrorCode { get; }
        public float Level { get; }
        public string Text { get; }
        public float Confidence { get; }
        public string TraceId { get; }

        public bool Equals(VoiceInputEvent other)
        {
            return Frame.Equals(other.Frame)
                && SourceId == other.SourceId
                && Kind == other.Kind
                && Status == other.Status
                && ErrorCode == other.ErrorCode
                && Level.Equals(other.Level)
                && Text == other.Text
                && Confidence.Equals(other.Confidence)
                && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is VoiceInputEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Frame.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceId;
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ (int)Status;
                hashCode = (hashCode * 397) ^ (int)ErrorCode;
                hashCode = (hashCode * 397) ^ Level.GetHashCode();
                hashCode = (hashCode * 397) ^ Text.GetHashCode();
                hashCode = (hashCode * 397) ^ Confidence.GetHashCode();
                hashCode = (hashCode * 397) ^ TraceId.GetHashCode();
                return hashCode;
            }
        }
    }

    public interface IVoiceInputEventSink
    {
        /// <remarks>NoAlloc after initialization when the destination queue has enough capacity.</remarks>
        void Enqueue(in VoiceInputEvent evt);
    }

    public interface IVoiceInputProvider : IDisposable
    {
        VoiceInputSnapshot Snapshot { get; }

        /// <remarks>LowFreqAlloc at provider or platform boundary.</remarks>
        VoiceInputResult Start();

        /// <remarks>LowFreqAlloc at provider or platform boundary.</remarks>
        VoiceInputResult Stop();

        /// <remarks>NoAlloc after initialization for providers with stable buffers and no emitted text/error events.</remarks>
        void Tick(RuntimeFrame frame, double deltaTime, IVoiceInputEventSink sink);
    }

    public interface IVoiceInputService : IDisposable
    {
        VoiceInputSnapshot Snapshot { get; }

        /// <remarks>LowFreqAlloc at provider or platform boundary.</remarks>
        VoiceInputResult Start();

        /// <remarks>LowFreqAlloc at provider or platform boundary.</remarks>
        VoiceInputResult Stop();

        /// <remarks>NoAlloc after initialization when the provider and event queue stay within their configured capacities.</remarks>
        void Tick(RuntimeFrame frame, double deltaTime);

        /// <remarks>NoAlloc when the caller-owned destination has enough capacity.</remarks>
        int DrainEvents(RuntimeFrame frame, List<VoiceInputEvent> destination);

        /// <remarks>NoAlloc.</remarks>
        RuntimeEventQueueSnapshot CreateEventQueueSnapshot();

        /// <remarks>NoAlloc.</remarks>
        void ClearEvents();
    }
}
