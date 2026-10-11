using System;
using MxFramework.Audio;

namespace MxFramework.UI.Audio
{
    /// <summary>Stable gameplay-facing Cue keys for the first UI feedback set.</summary>
    public static class MxAudioUiFeedbackCueKeys
    {
        public const string Click = "ui.click";
        public const string Confirm = "ui.confirm";
        public const string Back = "ui.back";
        public const string Error = "ui.error";
    }

    public enum MxAudioUiFeedbackDiagnosticCode
    {
        None = 0,
        AudioServiceMissing = 1,
        CueMissing = 2,
        CueContractInvalid = 3,
        UnsupportedFeedbackKind = 4,
        PlaybackRejected = 5,
        PlaybackException = 6
    }

    public readonly struct MxAudioUiFeedbackDebugSnapshot
    {
        public MxAudioUiFeedbackDebugSnapshot(
            int requestCount,
            int playedCount,
            int suppressedCount,
            MxAudioUiFeedbackDiagnosticCode lastDiagnostic,
            MxUiFeedbackKind lastKind,
            MxUiFeedbackPhase lastPhase,
            long lastInvocationId,
            AudioErrorCode lastAudioError)
        {
            RequestCount = requestCount;
            PlayedCount = playedCount;
            SuppressedCount = suppressedCount;
            LastDiagnostic = lastDiagnostic;
            LastKind = lastKind;
            LastPhase = lastPhase;
            LastInvocationId = lastInvocationId;
            LastAudioError = lastAudioError;
        }

        public int RequestCount { get; }
        public int PlayedCount { get; }
        public int SuppressedCount { get; }
        public MxAudioUiFeedbackDiagnosticCode LastDiagnostic { get; }
        public MxUiFeedbackKind LastKind { get; }
        public MxUiFeedbackPhase LastPhase { get; }
        public long LastInvocationId { get; }
        public AudioErrorCode LastAudioError { get; }
    }

    /// <summary>
    /// Resolves the stable UI Cue set during construction and plays cached 2D one-shots.
    /// </summary>
    /// <remarks>
    /// Construction is LowFreqAlloc through the supplied Cue provider.
    /// TryPlay and CaptureDebugSnapshot are NoAlloc after initialization.
    /// </remarks>
    public sealed class MxAudioUiFeedbackPlayer : IMxUiFeedbackPlayer
    {
        public const int Priority = 80;

        private static readonly AudioTraceIdentity ClickTraceId = new AudioTraceIdentity(AudioTraceKindIds.UiClick);
        private static readonly AudioTraceIdentity ConfirmTraceId = new AudioTraceIdentity(AudioTraceKindIds.UiConfirm);
        private static readonly AudioTraceIdentity BackTraceId = new AudioTraceIdentity(AudioTraceKindIds.UiBack);
        private static readonly AudioTraceIdentity ErrorTraceId = new AudioTraceIdentity(AudioTraceKindIds.UiError);

        private readonly IAudioService _audioService;
        private readonly CachedCue _click;
        private readonly CachedCue _confirm;
        private readonly CachedCue _back;
        private readonly CachedCue _error;

        private int _requestCount;
        private int _playedCount;
        private int _suppressedCount;
        private MxAudioUiFeedbackDiagnosticCode _lastDiagnostic;
        private MxUiFeedbackKind _lastKind;
        private MxUiFeedbackPhase _lastPhase;
        private long _lastInvocationId;
        private AudioErrorCode _lastAudioError;

        public MxAudioUiFeedbackPlayer(
            IAudioService audioService,
            IAudioCueKeyDefinitionProvider cueProvider)
        {
            _audioService = audioService;
            _click = ResolveCue(cueProvider, MxAudioUiFeedbackCueKeys.Click);
            _confirm = ResolveCue(cueProvider, MxAudioUiFeedbackCueKeys.Confirm);
            _back = ResolveCue(cueProvider, MxAudioUiFeedbackCueKeys.Back);
            _error = ResolveCue(cueProvider, MxAudioUiFeedbackCueKeys.Error);
        }

        /// <summary>Plays one cached UI feedback Cue without affecting UI command execution.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryPlay(
            MxUiFeedbackKind kind,
            in MxUiFeedbackContext context)
        {
            if (kind == MxUiFeedbackKind.None)
                return false;

            _requestCount++;
            CachedCue cached;
            AudioTraceIdentity traceId;
            switch (kind)
            {
                case MxUiFeedbackKind.Click:
                    cached = _click;
                    traceId = ClickTraceId;
                    break;
                case MxUiFeedbackKind.Confirm:
                    cached = _confirm;
                    traceId = ConfirmTraceId;
                    break;
                case MxUiFeedbackKind.Back:
                    cached = _back;
                    traceId = BackTraceId;
                    break;
                case MxUiFeedbackKind.Error:
                    cached = _error;
                    traceId = ErrorTraceId;
                    break;
                default:
                    RecordSuppressed(
                        MxAudioUiFeedbackDiagnosticCode.UnsupportedFeedbackKind,
                        kind,
                        in context,
                        AudioErrorCode.None);
                    return false;
            }

            if (_audioService == null)
            {
                RecordSuppressed(
                    MxAudioUiFeedbackDiagnosticCode.AudioServiceMissing,
                    kind,
                    in context,
                    AudioErrorCode.NotInitialized);
                return false;
            }
            if (!cached.Available)
            {
                RecordSuppressed(
                    cached.Diagnostic,
                    kind,
                    in context,
                    AudioErrorCode.InvalidEvent);
                return false;
            }

            try
            {
                AudioPlayResult result = _audioService.PlayOneShot(
                    AudioPlayRequest.Create2D(
                        cached.Definition.EventId,
                        cached.Definition.DefaultParameters,
                        Priority,
                        traceId: traceId));
                if (!result.Success)
                {
                    RecordSuppressed(
                        MxAudioUiFeedbackDiagnosticCode.PlaybackRejected,
                        kind,
                        in context,
                        result.ErrorCode);
                    return false;
                }
            }
            catch (Exception)
            {
                RecordSuppressed(
                    MxAudioUiFeedbackDiagnosticCode.PlaybackException,
                    kind,
                    in context,
                    AudioErrorCode.BackendFailed);
                return false;
            }

            _playedCount++;
            _lastDiagnostic = MxAudioUiFeedbackDiagnosticCode.None;
            _lastKind = kind;
            _lastPhase = context.Phase;
            _lastInvocationId = context.InvocationId;
            _lastAudioError = AudioErrorCode.None;
            return true;
        }

        /// <summary>Returns value-only diagnostics for low-frequency debug surfaces.</summary>
        /// <remarks>NoAlloc.</remarks>
        public MxAudioUiFeedbackDebugSnapshot CaptureDebugSnapshot()
        {
            return new MxAudioUiFeedbackDebugSnapshot(
                _requestCount,
                _playedCount,
                _suppressedCount,
                _lastDiagnostic,
                _lastKind,
                _lastPhase,
                _lastInvocationId,
                _lastAudioError);
        }

        private static CachedCue ResolveCue(
            IAudioCueKeyDefinitionProvider provider,
            string cueKey)
        {
            if (provider == null)
                return CachedCue.Missing;

            try
            {
                if (!provider.TryGetCue(cueKey, out AudioCueDefinition definition))
                    return CachedCue.Missing;
                if (definition.Id <= 0 ||
                    definition.EventId <= 0 ||
                    definition.PlayMode != AudioCuePlayMode.OneShot ||
                    definition.MissingPolicy != AudioCueMissingPolicy.Mute)
                {
                    return CachedCue.Invalid;
                }
                return new CachedCue(
                    definition,
                    true,
                    MxAudioUiFeedbackDiagnosticCode.None);
            }
            catch (Exception)
            {
                return CachedCue.Invalid;
            }
        }

        private void RecordSuppressed(
            MxAudioUiFeedbackDiagnosticCode diagnostic,
            MxUiFeedbackKind kind,
            in MxUiFeedbackContext context,
            AudioErrorCode audioError)
        {
            _suppressedCount++;
            _lastDiagnostic = diagnostic;
            _lastKind = kind;
            _lastPhase = context.Phase;
            _lastInvocationId = context.InvocationId;
            _lastAudioError = audioError;
        }

        private readonly struct CachedCue
        {
            public CachedCue(
                AudioCueDefinition definition,
                bool available,
                MxAudioUiFeedbackDiagnosticCode diagnostic)
            {
                Definition = definition;
                Available = available;
                Diagnostic = diagnostic;
            }

            public AudioCueDefinition Definition { get; }
            public bool Available { get; }
            public MxAudioUiFeedbackDiagnosticCode Diagnostic { get; }

            public static CachedCue Missing => new CachedCue(
                default,
                false,
                MxAudioUiFeedbackDiagnosticCode.CueMissing);

            public static CachedCue Invalid => new CachedCue(
                default,
                false,
                MxAudioUiFeedbackDiagnosticCode.CueContractInvalid);
        }
    }
}
