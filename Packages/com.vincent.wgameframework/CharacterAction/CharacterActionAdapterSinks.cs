using MxFramework.Runtime;
using System;
using System.Collections.Generic;

namespace MxFramework.CharacterAction
{
    /// <summary>Caller-owned diagnostics storage for CharacterAction track dispatch.</summary>
    /// <remarks>Allocates only during construction or capacity growth; Clear and indexed reads are NoAlloc.</remarks>
    public sealed class CharacterActionDispatchBuffer
    {
        private CharacterActionDiagnostic[] _diagnostics;

        public CharacterActionDispatchBuffer(int diagnosticCapacity = 8)
        {
            _diagnostics = diagnosticCapacity > 0
                ? new CharacterActionDiagnostic[diagnosticCapacity]
                : Array.Empty<CharacterActionDiagnostic>();
        }

        public int Count { get; private set; }
        public CharacterActionDiagnostic this[int index] =>
            index >= 0 && index < Count
                ? _diagnostics[index]
                : throw new ArgumentOutOfRangeException(nameof(index));

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public void Clear()
        {
            Count = 0;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= _diagnostics.Length)
                return;

            int next = Math.Max(capacity, Math.Max(4, _diagnostics.Length * 2));
            Array.Resize(ref _diagnostics, next);
        }

        internal void Add(CharacterActionDiagnostic diagnostic)
        {
            EnsureCapacity(Count + 1);
            _diagnostics[Count++] = diagnostic;
        }

        internal void AddRange(CharacterActionDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return;

            EnsureCapacity(Count + diagnostics.Length);
            for (int i = 0; i < diagnostics.Length; i++)
                _diagnostics[Count++] = diagnostics[i];
        }

        internal void AddRange(in CharacterActionAdapterSinkResult result)
        {
            int diagnosticCount = result.DiagnosticCount;
            EnsureCapacity(Count + diagnosticCount);
            for (int i = 0; i < diagnosticCount; i++)
                _diagnostics[Count++] = result[i];
        }

        /// <summary>Creates an owned diagnostic array for low-frequency diagnostics and tests.</summary>
        /// <remarks>AllocByDesign when Count is non-zero.</remarks>
        public CharacterActionDiagnostic[] ToArray()
        {
            if (Count == 0)
                return Array.Empty<CharacterActionDiagnostic>();

            var result = new CharacterActionDiagnostic[Count];
            Array.Copy(_diagnostics, result, Count);
            return result;
        }
    }

    public readonly struct CharacterActionAdapterSinkResult
    {
        private readonly CharacterActionDiagnostic _singleDiagnostic;
        private readonly CharacterActionDiagnostic[] _diagnostics;

        private CharacterActionAdapterSinkResult(bool accepted)
        {
            Accepted = accepted;
            _singleDiagnostic = default;
            _diagnostics = null;
            DiagnosticCount = 0;
        }

        private CharacterActionAdapterSinkResult(CharacterActionDiagnostic diagnostic)
        {
            Accepted = false;
            _singleDiagnostic = diagnostic;
            _diagnostics = null;
            DiagnosticCount = 1;
        }

        private CharacterActionAdapterSinkResult(CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = false;
            _singleDiagnostic = default;
            _diagnostics = diagnostics;
            DiagnosticCount = diagnostics?.Length ?? 0;
        }

        public bool Accepted { get; }
        public int DiagnosticCount { get; }
        public CharacterActionDiagnostic this[int index]
        {
            get
            {
                if (index < 0 || index >= DiagnosticCount)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _diagnostics == null ? _singleDiagnostic : _diagnostics[index];
            }
        }

        /// <summary>Creates an owned diagnostic array for low-frequency diagnostics and tests.</summary>
        /// <remarks>AllocByDesign when the result contains the inline single diagnostic.</remarks>
        public CharacterActionDiagnostic[] Diagnostics
        {
            get
            {
                if (DiagnosticCount == 0)
                    return Array.Empty<CharacterActionDiagnostic>();
                if (_diagnostics != null)
                    return _diagnostics;
                return new[] { _singleDiagnostic };
            }
        }

        public static CharacterActionAdapterSinkResult AcceptedResult()
        {
            return new CharacterActionAdapterSinkResult(true);
        }

        public static CharacterActionAdapterSinkResult Rejected(CharacterActionDiagnostic diagnostic)
        {
            return new CharacterActionAdapterSinkResult(diagnostic);
        }

        public static CharacterActionAdapterSinkResult Rejected(params CharacterActionDiagnostic[] diagnostics)
        {
            return new CharacterActionAdapterSinkResult(diagnostics);
        }
    }

    public readonly struct CharacterActionAdapterResult
    {
        public CharacterActionAdapterResult(
            int motionRequestCount,
            int combatRequestCount,
            int gameplayRequestCount,
            int pressureOnlyReactionRequestCount,
            CharacterActionDispatchBuffer diagnostics,
            int debugRequestCount = 0)
        {
            if (motionRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(motionRequestCount), "Request count cannot be negative.");
            if (combatRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(combatRequestCount), "Request count cannot be negative.");
            if (gameplayRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(gameplayRequestCount), "Request count cannot be negative.");
            if (pressureOnlyReactionRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureOnlyReactionRequestCount), "Request count cannot be negative.");
            if (debugRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(debugRequestCount), "Request count cannot be negative.");

            MotionRequestCount = motionRequestCount;
            CombatRequestCount = combatRequestCount;
            GameplayRequestCount = gameplayRequestCount;
            PressureOnlyReactionRequestCount = pressureOnlyReactionRequestCount;
            DebugRequestCount = debugRequestCount;
            DiagnosticsBuffer = diagnostics;
        }

        public int MotionRequestCount { get; }
        public int CombatRequestCount { get; }
        public int GameplayRequestCount { get; }
        public int PressureOnlyReactionRequestCount { get; }
        public int DebugRequestCount { get; }
        public CharacterActionDispatchBuffer DiagnosticsBuffer { get; }
        public int DiagnosticCount => DiagnosticsBuffer?.Count ?? 0;
        public CharacterActionDiagnostic[] Diagnostics => DiagnosticsBuffer?.ToArray() ?? Array.Empty<CharacterActionDiagnostic>();
        public bool Accepted => DiagnosticsBuffer == null || !DiagnosticsBuffer.HasErrors;
    }

    public readonly struct CharacterActionPresentationAdapterResult
    {
        public CharacterActionPresentationAdapterResult(
            int animationRequestCount,
            int audioCueRequestCount,
            int vfxRequestCount,
            int cameraRequestCount,
            int uiFeedbackRequestCount,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (animationRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(animationRequestCount), "Request count cannot be negative.");
            if (audioCueRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(audioCueRequestCount), "Request count cannot be negative.");
            if (vfxRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(vfxRequestCount), "Request count cannot be negative.");
            if (cameraRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(cameraRequestCount), "Request count cannot be negative.");
            if (uiFeedbackRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(uiFeedbackRequestCount), "Request count cannot be negative.");

            AnimationRequestCount = animationRequestCount;
            AudioCueRequestCount = audioCueRequestCount;
            VfxRequestCount = vfxRequestCount;
            CameraRequestCount = cameraRequestCount;
            UiFeedbackRequestCount = uiFeedbackRequestCount;
            DiagnosticsBuffer = diagnostics;
        }

        public int AnimationRequestCount { get; }
        public int AudioCueRequestCount { get; }
        public int VfxRequestCount { get; }
        public int CameraRequestCount { get; }
        public int UiFeedbackRequestCount { get; }
        public CharacterActionDispatchBuffer DiagnosticsBuffer { get; }
        public int DiagnosticCount => DiagnosticsBuffer?.Count ?? 0;
        public CharacterActionDiagnostic[] Diagnostics => DiagnosticsBuffer?.ToArray() ?? Array.Empty<CharacterActionDiagnostic>();
        public bool Accepted => DiagnosticsBuffer == null || !DiagnosticsBuffer.HasErrors;
    }

    public interface ICharacterActionMotionRequestSink
    {
        CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request);
    }

    public interface ICharacterActionRootMotionRequestSink
    {
        CharacterActionAdapterSinkResult SubmitRootMotionRequest(CharacterActionRootMotionRequest request);
    }

    public interface ICharacterActionCombatRequestSink
    {
        CharacterActionAdapterSinkResult SubmitCombatRequest(CharacterActionCombatRequest request);
    }

    public interface ICharacterActionGameplayRequestSink
    {
        CharacterActionAdapterSinkResult SubmitGameplayRequest(CharacterActionGameplayRequest request);
    }

    public interface ICharacterActionPressureOnlyReactionRequestSink
    {
        CharacterActionAdapterSinkResult SubmitPressureOnlyReactionRequest(CharacterActionPressureOnlyReactionRequest request);
    }

    public interface ICharacterActionDebugRequestSink
    {
        CharacterActionAdapterSinkResult SubmitDebugRequest(CharacterActionDebugRequest request);
    }

    public interface ICharacterActionAnimationRequestSink
    {
        CharacterActionAdapterSinkResult SubmitAnimationRequest(CharacterActionAnimationRequest request);
    }

    public interface ICharacterActionAudioCueRequestSink
    {
        CharacterActionAdapterSinkResult SubmitAudioCueRequest(CharacterActionAudioCueRequest request);
    }

    public interface ICharacterActionVfxRequestSink
    {
        CharacterActionAdapterSinkResult SubmitVfxRequest(CharacterActionVfxRequest request);
    }

    public interface ICharacterActionCameraRequestSink
    {
        CharacterActionAdapterSinkResult SubmitCameraRequest(CharacterActionCameraRequest request);
    }

    public interface ICharacterActionUiFeedbackRequestSink
    {
        CharacterActionAdapterSinkResult SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request);
    }

    public readonly struct CharacterActionDownstreamTermination
    {
        public CharacterActionDownstreamTermination(
            long instanceId,
            CharacterActionId actionId,
            CharacterActionInstanceState terminalState,
            string reason,
            RuntimeTraceIdentity traceId)
        {
            InstanceId = instanceId;
            ActionId = actionId;
            TerminalState = terminalState;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
        }

        public long InstanceId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionInstanceState TerminalState { get; }
        public string Reason { get; }
        public RuntimeTraceIdentity TraceId { get; }
    }

    /// <summary>Outer-action lifecycle seam for downstream track runtimes such as CombatAction.</summary>
    public interface ICharacterActionDownstreamLifecycleSink
    {
        CharacterActionAdapterSinkResult TerminateDownstream(CharacterActionDownstreamTermination termination);
    }

    public sealed class CharacterActionAdapterRequestCollector :
        ICharacterActionMotionRequestSink,
        ICharacterActionRootMotionRequestSink,
        ICharacterActionCombatRequestSink,
        ICharacterActionGameplayRequestSink,
        ICharacterActionPressureOnlyReactionRequestSink,
        ICharacterActionDebugRequestSink
    {
        private readonly List<CharacterActionMotionRequest> _motionRequests = new List<CharacterActionMotionRequest>();
        private readonly List<CharacterActionRootMotionRequest> _rootMotionRequests =
            new List<CharacterActionRootMotionRequest>();
        private readonly List<CharacterActionCombatRequest> _combatRequests = new List<CharacterActionCombatRequest>();
        private readonly List<CharacterActionGameplayRequest> _gameplayRequests = new List<CharacterActionGameplayRequest>();
        private readonly List<CharacterActionPressureOnlyReactionRequest> _pressureOnlyReactionRequests =
            new List<CharacterActionPressureOnlyReactionRequest>();
        private readonly List<CharacterActionDebugRequest> _debugRequests = new List<CharacterActionDebugRequest>();

        public IReadOnlyList<CharacterActionMotionRequest> MotionRequests => _motionRequests;
        public IReadOnlyList<CharacterActionRootMotionRequest> RootMotionRequests => _rootMotionRequests;
        public IReadOnlyList<CharacterActionCombatRequest> CombatRequests => _combatRequests;
        public IReadOnlyList<CharacterActionGameplayRequest> GameplayRequests => _gameplayRequests;
        public IReadOnlyList<CharacterActionPressureOnlyReactionRequest> PressureOnlyReactionRequests => _pressureOnlyReactionRequests;
        public IReadOnlyList<CharacterActionDebugRequest> DebugRequests => _debugRequests;

        public void Clear()
        {
            _motionRequests.Clear();
            _rootMotionRequests.Clear();
            _combatRequests.Clear();
            _gameplayRequests.Clear();
            _pressureOnlyReactionRequests.Clear();
            _debugRequests.Clear();
        }

        public CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request)
        {
            _motionRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitRootMotionRequest(CharacterActionRootMotionRequest request)
        {
            _rootMotionRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitCombatRequest(CharacterActionCombatRequest request)
        {
            _combatRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitGameplayRequest(CharacterActionGameplayRequest request)
        {
            _gameplayRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitPressureOnlyReactionRequest(CharacterActionPressureOnlyReactionRequest request)
        {
            _pressureOnlyReactionRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitDebugRequest(CharacterActionDebugRequest request)
        {
            _debugRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }
    }

    public sealed class CharacterActionPresentationRequestCollector :
        ICharacterActionAnimationRequestSink,
        ICharacterActionAudioCueRequestSink,
        ICharacterActionVfxRequestSink,
        ICharacterActionCameraRequestSink,
        ICharacterActionUiFeedbackRequestSink
    {
        private readonly List<CharacterActionAnimationRequest> _animationRequests =
            new List<CharacterActionAnimationRequest>();
        private readonly List<CharacterActionAudioCueRequest> _audioCueRequests =
            new List<CharacterActionAudioCueRequest>();
        private readonly List<CharacterActionVfxRequest> _vfxRequests =
            new List<CharacterActionVfxRequest>();
        private readonly List<CharacterActionCameraRequest> _cameraRequests =
            new List<CharacterActionCameraRequest>();
        private readonly List<CharacterActionUiFeedbackRequest> _uiFeedbackRequests =
            new List<CharacterActionUiFeedbackRequest>();

        public IReadOnlyList<CharacterActionAnimationRequest> AnimationRequests => _animationRequests;
        public IReadOnlyList<CharacterActionAudioCueRequest> AudioCueRequests => _audioCueRequests;
        public IReadOnlyList<CharacterActionVfxRequest> VfxRequests => _vfxRequests;
        public IReadOnlyList<CharacterActionCameraRequest> CameraRequests => _cameraRequests;
        public IReadOnlyList<CharacterActionUiFeedbackRequest> UiFeedbackRequests => _uiFeedbackRequests;

        public void Clear()
        {
            _animationRequests.Clear();
            _audioCueRequests.Clear();
            _vfxRequests.Clear();
            _cameraRequests.Clear();
            _uiFeedbackRequests.Clear();
        }

        public CharacterActionAdapterSinkResult SubmitAnimationRequest(CharacterActionAnimationRequest request)
        {
            _animationRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitAudioCueRequest(CharacterActionAudioCueRequest request)
        {
            _audioCueRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitVfxRequest(CharacterActionVfxRequest request)
        {
            _vfxRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitCameraRequest(CharacterActionCameraRequest request)
        {
            _cameraRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request)
        {
            _uiFeedbackRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }
    }
}
