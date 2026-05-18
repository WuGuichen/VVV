using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.Core.Math;
using MxFramework.Resources;

namespace MxFramework.CharacterControl.Animation
{
    public enum CharacterAnimationLocomotionBlendMode
    {
        Disabled = 0,
        Blend1D = 1,
        Blend2D = 2
    }

    public enum CharacterAnimationReactionRequestKind
    {
        Play = 0,
        CrossFade = 1
    }

    public enum CharacterAnimationPresentationEventKind
    {
        None = 0,
        LocomotionBlend1D = 1,
        LocomotionBlend2D = 2,
        ReactionPlay = 3,
        ReactionCrossFade = 4,
        ActionHandledByCombatBridge = 5,
        MissingReactionBinding = 6,
        MissingBackend = 7,
        BackendRejected = 8,
        Skipped = 9
    }

    public sealed class CharacterAnimationReactionBinding
    {
        public CharacterControlTransitionReason Reason { get; set; }

        public CharacterAnimationReactionRequestKind RequestKind { get; set; } =
            CharacterAnimationReactionRequestKind.CrossFade;

        public string BindingId { get; set; } = string.Empty;

        public ResourceKey ClipKey { get; set; }

        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;

        public float FadeDurationSeconds { get; set; } = 0.12f;

        public float PlaybackSpeed { get; set; } = 1f;

        public bool Loop { get; set; }

        public MxAnimationAlignmentPolicy AlignmentPolicy { get; set; } =
            MxAnimationAlignmentPolicy.StartAtZero;

        public bool Matches(CharacterControlTransitionReason reason)
        {
            return Reason == reason;
        }
    }

    public sealed class CharacterAnimationPresentationOptions
    {
        private readonly List<CharacterAnimationReactionBinding> _reactionBindings =
            new List<CharacterAnimationReactionBinding>();

        public string TargetActorId { get; set; } = string.Empty;

        public CharacterAnimationLocomotionBlendMode LocomotionBlendMode { get; set; } =
            CharacterAnimationLocomotionBlendMode.Blend2D;

        public string LocomotionBlend1DId { get; set; } = "locomotion";

        public string LocomotionBlend2DId { get; set; } = "locomotion2d";

        public string SpeedParameterId { get; set; } = "locomotion.speed";

        public string DirectionXParameterId { get; set; } = "locomotion.x";

        public string DirectionYParameterId { get; set; } = "locomotion.y";

        public int ParameterScale { get; set; } = 1000;

        public float LocomotionFadeDurationSeconds { get; set; } = -1f;

        public bool BlendLocomotionWhenAirborne { get; set; }

        public int MaxRecentDiagnostics { get; set; } = 32;

        public IList<CharacterAnimationReactionBinding> ReactionBindings => _reactionBindings;
    }

    public readonly struct CharacterAnimationPresentationResult
    {
        private CharacterAnimationPresentationResult(
            bool success,
            bool skipped,
            CharacterAnimationPresentationEventKind eventKind,
            MxAnimationBackendResult backendResult,
            CharacterAnimationPresentationDiagnosticEntry diagnostic)
        {
            Success = success;
            Skipped = skipped;
            EventKind = eventKind;
            BackendResult = backendResult;
            Diagnostic = diagnostic;
        }

        public bool Success { get; }

        public bool Skipped { get; }

        public CharacterAnimationPresentationEventKind EventKind { get; }

        public MxAnimationBackendResult BackendResult { get; }

        public CharacterAnimationPresentationDiagnosticEntry Diagnostic { get; }

        public static CharacterAnimationPresentationResult FromDiagnostic(
            CharacterAnimationPresentationDiagnosticEntry diagnostic,
            MxAnimationBackendResult backendResult = default)
        {
            bool success = diagnostic.Success;
            bool skipped = diagnostic.EventKind == CharacterAnimationPresentationEventKind.Skipped
                || diagnostic.EventKind == CharacterAnimationPresentationEventKind.ActionHandledByCombatBridge;
            return new CharacterAnimationPresentationResult(success, skipped, diagnostic.EventKind, backendResult, diagnostic);
        }
    }

    public readonly struct CharacterAnimationPresentationDiagnosticEntry
    {
        public CharacterAnimationPresentationDiagnosticEntry(
            CharacterAnimationPresentationEventKind eventKind,
            CharacterControlState controlState,
            CharacterControlTransitionReason reason,
            string targetActorId,
            MxAnimationRequestKind requestKind,
            MxAnimationBackendResultCode resultCode,
            bool success,
            string blendId,
            int speedParameter,
            int directionXParameter,
            int directionYParameter,
            string bindingId,
            ResourceKey clipKey,
            ResourceKey backendClipKey,
            ResourceError backendResourceError,
            string correlationId,
            string message)
        {
            EventKind = eventKind;
            ControlState = controlState;
            Reason = reason;
            TargetActorId = targetActorId ?? string.Empty;
            RequestKind = requestKind;
            ResultCode = resultCode;
            Success = success;
            BlendId = blendId ?? string.Empty;
            SpeedParameter = speedParameter;
            DirectionXParameter = directionXParameter;
            DirectionYParameter = directionYParameter;
            BindingId = bindingId ?? string.Empty;
            ClipKey = clipKey;
            BackendClipKey = backendClipKey;
            BackendResourceError = backendResourceError;
            CorrelationId = correlationId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterAnimationPresentationEventKind EventKind { get; }

        public CharacterControlState ControlState { get; }

        public CharacterControlTransitionReason Reason { get; }

        public string TargetActorId { get; }

        public MxAnimationRequestKind RequestKind { get; }

        public MxAnimationBackendResultCode ResultCode { get; }

        public bool Success { get; }

        public string BlendId { get; }

        public int SpeedParameter { get; }

        public int DirectionXParameter { get; }

        public int DirectionYParameter { get; }

        public string BindingId { get; }

        public ResourceKey ClipKey { get; }

        public ResourceKey BackendClipKey { get; }

        public ResourceError BackendResourceError { get; }

        public string CorrelationId { get; }

        public string Message { get; }
    }

    public sealed class CharacterAnimationPresentationDiagnosticSnapshot
    {
        private readonly List<CharacterAnimationPresentationDiagnosticEntry> _recentEntries;

        public CharacterAnimationPresentationDiagnosticSnapshot(
            string targetActorId,
            CharacterAnimationPresentationDiagnosticEntry lastEntry,
            IEnumerable<CharacterAnimationPresentationDiagnosticEntry> recentEntries)
        {
            TargetActorId = targetActorId ?? string.Empty;
            LastEntry = lastEntry;
            _recentEntries = recentEntries != null
                ? new List<CharacterAnimationPresentationDiagnosticEntry>(recentEntries)
                : new List<CharacterAnimationPresentationDiagnosticEntry>();
        }

        public string TargetActorId { get; }

        public CharacterAnimationPresentationDiagnosticEntry LastEntry { get; }

        public IReadOnlyList<CharacterAnimationPresentationDiagnosticEntry> RecentEntries => _recentEntries;
    }

    public sealed class CharacterAnimationPresentationController
    {
        private readonly IMxAnimationBackend _backend;
        private readonly CharacterAnimationPresentationOptions _options;
        private readonly List<CharacterAnimationPresentationDiagnosticEntry> _recentEntries =
            new List<CharacterAnimationPresentationDiagnosticEntry>();
        private CharacterAnimationPresentationDiagnosticEntry _lastEntry;

        public CharacterAnimationPresentationController(
            IMxAnimationBackend backend,
            CharacterAnimationPresentationOptions options = null)
        {
            _backend = backend;
            _options = options ?? new CharacterAnimationPresentationOptions();
        }

        public CharacterAnimationPresentationResult ApplyLocomotion(CharacterMotionResult motionResult)
        {
            if (_options.LocomotionBlendMode == CharacterAnimationLocomotionBlendMode.Disabled)
            {
                return Record(CreateEntry(
                    CharacterAnimationPresentationEventKind.Skipped,
                    motionResult.ControlState,
                    CharacterControlTransitionReason.None,
                    ResolveActorId(motionResult.Command.Entity),
                    MxAnimationRequestKind.SetBlend1D,
                    MxAnimationBackendResultCode.Success,
                    true,
                    string.Empty,
                    0,
                    0,
                    0,
                    string.Empty,
                    default,
                    BuildCorrelation(motionResult.Command.Entity, motionResult.Command.Frame.Value, "locomotion"),
                    "Locomotion presentation is disabled."));
            }

            if (_backend == null)
            {
                MxAnimationRequestKind requestKind = _options.LocomotionBlendMode == CharacterAnimationLocomotionBlendMode.Blend1D
                    ? MxAnimationRequestKind.SetBlend1D
                    : MxAnimationRequestKind.SetBlend2D;
                return RecordMissingBackend(
                    motionResult.Command.Entity,
                    motionResult.ControlState,
                    CharacterControlTransitionReason.None,
                    requestKind,
                    motionResult.Command.Frame.Value);
            }

            string actorId = ResolveActorId(motionResult.Command.Entity);
            bool usesLocomotionParameters = motionResult.StepResult.State.Grounded
                || _options.BlendLocomotionWhenAirborne;
            FixVector3 locomotionVector = usesLocomotionParameters
                ? GetHorizontalLocomotionVector(motionResult.MotionInput.MoveDirection)
                : FixVector3.Zero;
            int speed = usesLocomotionParameters
                ? QuantizeSpeed(locomotionVector, motionResult.MotionInput.MoveSpeedScale, _options.ParameterScale)
                : 0;
            int directionX = usesLocomotionParameters
                ? Quantize(locomotionVector.X, _options.ParameterScale)
                : 0;
            int directionY = usesLocomotionParameters
                ? Quantize(locomotionVector.Z, _options.ParameterScale)
                : 0;
            string correlationId = BuildCorrelation(motionResult.Command.Entity, motionResult.Command.Frame.Value, "locomotion");

            if (_options.LocomotionBlendMode == CharacterAnimationLocomotionBlendMode.Blend1D)
            {
                var request = new MxAnimationBlend1DRequest
                {
                    TargetActorId = actorId,
                    BlendId = _options.LocomotionBlend1DId ?? string.Empty,
                    Parameter = new MxAnimationQuantizedParameter(_options.SpeedParameterId, speed, _options.ParameterScale),
                    FadeDurationSeconds = _options.LocomotionFadeDurationSeconds,
                    CorrelationId = correlationId
                };
                MxAnimationBackendResult backendResult = _backend.SetBlend1D(request);
                return RecordBackendResult(
                    backendResult,
                    CharacterAnimationPresentationEventKind.LocomotionBlend1D,
                    motionResult.ControlState,
                    CharacterControlTransitionReason.None,
                    actorId,
                    MxAnimationRequestKind.SetBlend1D,
                    _options.LocomotionBlend1DId,
                    speed,
                    0,
                    0,
                    string.Empty,
                    default,
                    correlationId,
                    backendResult.Message);
            }

            var blend2DRequest = new MxAnimationBlend2DRequest
            {
                TargetActorId = actorId,
                BlendId = _options.LocomotionBlend2DId ?? string.Empty,
                ParameterX = new MxAnimationQuantizedParameter(_options.DirectionXParameterId, directionX, _options.ParameterScale),
                ParameterY = new MxAnimationQuantizedParameter(_options.DirectionYParameterId, directionY, _options.ParameterScale),
                FadeDurationSeconds = _options.LocomotionFadeDurationSeconds,
                CorrelationId = correlationId
            };
            MxAnimationBackendResult result = _backend.SetBlend2D(blend2DRequest);
            return RecordBackendResult(
                result,
                CharacterAnimationPresentationEventKind.LocomotionBlend2D,
                motionResult.ControlState,
                CharacterControlTransitionReason.None,
                actorId,
                MxAnimationRequestKind.SetBlend2D,
                _options.LocomotionBlend2DId,
                speed,
                directionX,
                directionY,
                string.Empty,
                default,
                correlationId,
                result.Message);
        }

        public CharacterAnimationPresentationResult ApplyStateChanged(CharacterStateChangedEvent evt)
        {
            if (evt.CurrentState != CharacterControlState.Reaction)
            {
                return Record(CreateEntry(
                    CharacterAnimationPresentationEventKind.Skipped,
                    evt.CurrentState,
                    evt.Reason,
                    ResolveActorId(evt.Entity),
                    MxAnimationRequestKind.Play,
                    MxAnimationBackendResultCode.Success,
                    true,
                    string.Empty,
                    0,
                    0,
                    0,
                    string.Empty,
                    default,
                    BuildCorrelation(evt.Entity, evt.Frame.Value, "state"),
                    "State change does not require a CharacterControl animation request."));
            }

            return ApplyReaction(evt.Entity, evt.Reason, evt.Frame.Value);
        }

        public CharacterAnimationPresentationResult RecordActionEvent(CharacterActionEvent evt)
        {
            if (evt.Type != CharacterActionEventType.Started
                && evt.Type != CharacterActionEventType.Finished
                && evt.Type != CharacterActionEventType.Canceled)
            {
                return Record(CreateEntry(
                    CharacterAnimationPresentationEventKind.Skipped,
                    CharacterControlState.Action,
                    CharacterControlTransitionReason.None,
                    ResolveActorId(evt.Request.Entity),
                    MxAnimationRequestKind.CrossFade,
                    evt.Type == CharacterActionEventType.Rejected
                        ? MxAnimationBackendResultCode.InvalidRequest
                        : MxAnimationBackendResultCode.Success,
                    evt.Type != CharacterActionEventType.Rejected,
                    string.Empty,
                    0,
                    0,
                    0,
                    string.Empty,
                    default,
                    BuildCorrelation(evt.Request.Entity, evt.Request.Frame.Value, "action-skipped:" + evt.Type),
                    "Character action event is not owned by the Combat to MxAnimation bridge: " + evt.Type + "."));
            }

            return Record(CreateEntry(
                CharacterAnimationPresentationEventKind.ActionHandledByCombatBridge,
                CharacterControlState.Action,
                ResolveActionReason(evt.Type),
                ResolveActorId(evt.Request.Entity),
                MxAnimationRequestKind.CrossFade,
                MxAnimationBackendResultCode.Success,
                true,
                string.Empty,
                0,
                0,
                0,
                string.Empty,
                default,
                BuildCorrelation(evt.Request.Entity, evt.Request.Frame.Value, "action-external"),
                "Combat action presentation is handled by CombatMxAnimationUnityBridge."));
        }

        public CharacterAnimationPresentationDiagnosticSnapshot CreateSnapshot()
        {
            return new CharacterAnimationPresentationDiagnosticSnapshot(_options.TargetActorId, _lastEntry, _recentEntries);
        }

        private CharacterAnimationPresentationResult ApplyReaction(
            CharacterControlEntityRef entity,
            CharacterControlTransitionReason reason,
            long frame)
        {
            if (_backend == null)
            {
                return RecordMissingBackend(entity, CharacterControlState.Reaction, reason, MxAnimationRequestKind.CrossFade, frame);
            }

            CharacterAnimationReactionBinding binding = FindReactionBinding(reason, out bool usedFallbackBinding);
            string actorId = ResolveActorId(entity);
            string correlationId = BuildCorrelation(entity, frame, "reaction:" + reason);
            if (binding == null)
            {
                return Record(CreateEntry(
                    CharacterAnimationPresentationEventKind.MissingReactionBinding,
                    CharacterControlState.Reaction,
                    reason,
                    actorId,
                    MxAnimationRequestKind.CrossFade,
                    MxAnimationBackendResultCode.InvalidRequest,
                    false,
                    string.Empty,
                    0,
                    0,
                    0,
                    string.Empty,
                    default,
                    correlationId,
                    "Missing reaction animation binding."));
            }

            if (binding.RequestKind == CharacterAnimationReactionRequestKind.Play)
            {
                var request = new MxAnimationPlayRequest
                {
                    TargetActorId = actorId,
                    BindingId = binding.BindingId ?? string.Empty,
                    ClipKey = binding.ClipKey,
                    LayerId = binding.LayerId,
                    PlaybackSpeed = binding.PlaybackSpeed,
                    Loop = binding.Loop,
                    AlignmentPolicy = binding.AlignmentPolicy,
                    CorrelationId = correlationId
                };
                MxAnimationBackendResult backendResult = _backend.Play(request);
                string diagnosticMessage = BuildBackendMessage(backendResult, usedFallbackBinding, reason);
                return RecordBackendResult(
                    backendResult,
                    CharacterAnimationPresentationEventKind.ReactionPlay,
                    CharacterControlState.Reaction,
                    reason,
                    actorId,
                    MxAnimationRequestKind.Play,
                    string.Empty,
                    0,
                    0,
                    0,
                    binding.BindingId,
                    binding.ClipKey,
                    correlationId,
                    diagnosticMessage);
            }

            var crossFadeRequest = new MxAnimationCrossFadeRequest
            {
                TargetActorId = actorId,
                BindingId = binding.BindingId ?? string.Empty,
                ClipKey = binding.ClipKey,
                LayerId = binding.LayerId,
                FadeDurationSeconds = binding.FadeDurationSeconds,
                PlaybackSpeed = binding.PlaybackSpeed,
                Loop = binding.Loop,
                AlignmentPolicy = binding.AlignmentPolicy,
                CorrelationId = correlationId
            };
            MxAnimationBackendResult result = _backend.CrossFade(crossFadeRequest);
            string message = BuildBackendMessage(result, usedFallbackBinding, reason);
            return RecordBackendResult(
                result,
                CharacterAnimationPresentationEventKind.ReactionCrossFade,
                CharacterControlState.Reaction,
                reason,
                actorId,
                MxAnimationRequestKind.CrossFade,
                string.Empty,
                0,
                0,
                0,
                binding.BindingId,
                binding.ClipKey,
                correlationId,
                message);
        }

        private CharacterAnimationReactionBinding FindReactionBinding(
            CharacterControlTransitionReason reason,
            out bool usedFallback)
        {
            CharacterAnimationReactionBinding fallback = null;
            usedFallback = false;
            for (int i = 0; i < _options.ReactionBindings.Count; i++)
            {
                CharacterAnimationReactionBinding binding = _options.ReactionBindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (binding.Matches(reason))
                {
                    return binding;
                }

                if (binding.Reason == CharacterControlTransitionReason.None && fallback == null)
                {
                    fallback = binding;
                }
            }

            usedFallback = fallback != null;
            return fallback;
        }

        private CharacterAnimationPresentationResult RecordMissingBackend(
            CharacterControlEntityRef entity,
            CharacterControlState state,
            CharacterControlTransitionReason reason,
            MxAnimationRequestKind requestKind,
            long frame)
        {
            return Record(CreateEntry(
                CharacterAnimationPresentationEventKind.MissingBackend,
                state,
                reason,
                ResolveActorId(entity),
                requestKind,
                MxAnimationBackendResultCode.BackendReleased,
                false,
                string.Empty,
                0,
                0,
                0,
                string.Empty,
                default,
                BuildCorrelation(entity, frame, "missing-backend"),
                "Missing MxAnimation backend."));
        }

        private CharacterAnimationPresentationResult RecordBackendResult(
            MxAnimationBackendResult backendResult,
            CharacterAnimationPresentationEventKind successKind,
            CharacterControlState state,
            CharacterControlTransitionReason reason,
            string actorId,
            MxAnimationRequestKind requestKind,
            string blendId,
            int speed,
            int directionX,
            int directionY,
            string bindingId,
            ResourceKey clipKey,
            string correlationId,
            string message)
        {
            CharacterAnimationPresentationEventKind eventKind = backendResult.Success
                ? successKind
                : CharacterAnimationPresentationEventKind.BackendRejected;
            return Record(
                CreateEntry(
                    eventKind,
                    state,
                    reason,
                    actorId,
                    requestKind,
                    backendResult.Code,
                    backendResult.Success,
                    blendId,
                    speed,
                    directionX,
                    directionY,
                    bindingId,
                    clipKey,
                    backendResult.ClipKey,
                    backendResult.ResourceError,
                    correlationId,
                    message),
                backendResult);
        }

        private static CharacterControlTransitionReason ResolveActionReason(CharacterActionEventType eventType)
        {
            switch (eventType)
            {
                case CharacterActionEventType.Started:
                    return CharacterControlTransitionReason.ActionStarted;
                case CharacterActionEventType.Finished:
                    return CharacterControlTransitionReason.ActionFinished;
                case CharacterActionEventType.Canceled:
                    return CharacterControlTransitionReason.ActionCanceled;
                default:
                    return CharacterControlTransitionReason.None;
            }
        }

        private static FixVector3 GetHorizontalLocomotionVector(FixVector3 direction)
        {
            var horizontal = new FixVector3(direction.X, Fix64.Zero, direction.Z);
            Fix64 lengthSquared = horizontal.LengthSquared();
            if (lengthSquared <= Fix64.One)
            {
                return horizontal;
            }

            return horizontal / lengthSquared.Sqrt();
        }

        private static string BuildBackendMessage(
            MxAnimationBackendResult backendResult,
            bool usedFallback,
            CharacterControlTransitionReason reason)
        {
            if (!usedFallback)
            {
                return backendResult.Message;
            }

            string fallbackMessage = "Using fallback reaction animation binding for reason " + reason + ".";
            if (string.IsNullOrEmpty(backendResult.Message))
            {
                return fallbackMessage;
            }

            return fallbackMessage + " " + backendResult.Message;
        }

        private CharacterAnimationPresentationResult Record(
            CharacterAnimationPresentationDiagnosticEntry entry,
            MxAnimationBackendResult backendResult = default)
        {
            _lastEntry = entry;
            _recentEntries.Add(entry);
            int max = Math.Max(1, _options.MaxRecentDiagnostics);
            while (_recentEntries.Count > max)
            {
                _recentEntries.RemoveAt(0);
            }

            return CharacterAnimationPresentationResult.FromDiagnostic(entry, backendResult);
        }

        private static CharacterAnimationPresentationDiagnosticEntry CreateEntry(
            CharacterAnimationPresentationEventKind eventKind,
            CharacterControlState state,
            CharacterControlTransitionReason reason,
            string actorId,
            MxAnimationRequestKind requestKind,
            MxAnimationBackendResultCode resultCode,
            bool success,
            string blendId,
            int speed,
            int directionX,
            int directionY,
            string bindingId,
            ResourceKey clipKey,
            string correlationId,
            string message)
        {
            return CreateEntry(
                eventKind,
                state,
                reason,
                actorId,
                requestKind,
                resultCode,
                success,
                blendId,
                speed,
                directionX,
                directionY,
                bindingId,
                clipKey,
                default,
                ResourceError.None,
                correlationId,
                message);
        }

        private static CharacterAnimationPresentationDiagnosticEntry CreateEntry(
            CharacterAnimationPresentationEventKind eventKind,
            CharacterControlState state,
            CharacterControlTransitionReason reason,
            string actorId,
            MxAnimationRequestKind requestKind,
            MxAnimationBackendResultCode resultCode,
            bool success,
            string blendId,
            int speed,
            int directionX,
            int directionY,
            string bindingId,
            ResourceKey clipKey,
            ResourceKey backendClipKey,
            ResourceError backendResourceError,
            string correlationId,
            string message)
        {
            return new CharacterAnimationPresentationDiagnosticEntry(
                eventKind,
                state,
                reason,
                actorId,
                requestKind,
                resultCode,
                success,
                blendId,
                speed,
                directionX,
                directionY,
                bindingId,
                clipKey,
                backendClipKey,
                backendResourceError,
                correlationId,
                message);
        }

        private string ResolveActorId(CharacterControlEntityRef entity)
        {
            if (!string.IsNullOrWhiteSpace(_options.TargetActorId))
            {
                return _options.TargetActorId;
            }

            if (entity.StableId > 0)
            {
                return "character:" + entity.StableId.ToString(CultureInfo.InvariantCulture);
            }

            if (entity.HasCombatEntity)
            {
                return "combat:" + entity.CombatEntityId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (entity.HasGameplayEntity)
            {
                return "gameplay:" + entity.GameplayEntityId.ToString();
            }

            return "character:unknown";
        }

        private static string BuildCorrelation(CharacterControlEntityRef entity, long frame, string suffix)
        {
            string id = entity.StableId > 0
                ? entity.StableId.ToString(CultureInfo.InvariantCulture)
                : entity.ToString();
            return "character:" + id + "|frame:" + frame.ToString(CultureInfo.InvariantCulture) + "|" + suffix;
        }

        private static int QuantizeSpeed(FixVector3 horizontalMoveDirection, Fix64 moveSpeedScale, int parameterScale)
        {
            if (horizontalMoveDirection.IsZero || moveSpeedScale <= Fix64.Zero)
            {
                return 0;
            }

            Fix64 directionMagnitude = horizontalMoveDirection.LengthSquared().Sqrt();
            return Quantize(directionMagnitude * moveSpeedScale, parameterScale);
        }

        private static int Quantize(Fix64 value, int parameterScale)
        {
            long scale = Math.Max(1, parameterScale);
            long raw = checked(value.RawValue * scale / Fix64.Scale);
            if (raw > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (raw < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)raw;
        }
    }
}
