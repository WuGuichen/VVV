using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl.Animation
{
    public enum CharacterControlAnimationLocomotionMode
    {
        Blend1D = 0,
        Blend2D = 1
    }

    public enum CharacterControlAnimationReactionRequestKind
    {
        Play = 0,
        CrossFade = 1
    }

    public enum CharacterControlAnimationRequestKind
    {
        None = 0,
        LocomotionBlend1D = 1,
        LocomotionBlend2D = 2,
        ReactionPlay = 3,
        ReactionCrossFade = 4,
        ActionDelegated = 5
    }

    public enum CharacterControlAnimationMissingBindingKind
    {
        None = 0,
        Actor = 1,
        LocomotionBlend1D = 2,
        LocomotionBlend2D = 3,
        Reaction = 4
    }

    public sealed class CharacterControlMxAnimationAdapterOptions
    {
        public CharacterControlAnimationLocomotionMode LocomotionMode { get; set; } =
            CharacterControlAnimationLocomotionMode.Blend2D;

        public string GroundedBlend1DId { get; set; } = "locomotion.speed";

        public string AirborneBlend1DId { get; set; } = "locomotion.air.speed";

        public string GroundedBlend2DId { get; set; } = "locomotion.direction";

        public string AirborneBlend2DId { get; set; } = "locomotion.air.direction";

        public string SpeedParameterId { get; set; } = "locomotion.speed";

        public string DirectionXParameterId { get; set; } = "locomotion.x";

        public string DirectionYParameterId { get; set; } = "locomotion.y";

        public int ParameterScale { get; set; } = 1000;

        public Fix64 MaxSpeedRatio { get; set; } = Fix64.FromInt(2);

        public float LocomotionFadeDurationSeconds { get; set; } = -1f;

        public bool FallbackToBlend1DWhenBlend2DMissing { get; set; } = true;

        public CharacterControlAnimationReactionRequestKind ReactionRequestKind { get; set; } =
            CharacterControlAnimationReactionRequestKind.CrossFade;

        public string ReactionActionKeyPrefix { get; set; } = "reaction:";

        public float ReactionCrossFadeDurationSeconds { get; set; } = 0.1f;

        public int MaxRecentDiagnostics { get; set; } = 32;
    }

    public readonly struct CharacterControlAnimationLocomotionSample
    {
        public CharacterControlAnimationLocomotionSample(
            CharacterControlEntityRef entity,
            RuntimeFrame frame,
            Fix64 speedRatio,
            Fix64 directionX,
            Fix64 directionY,
            bool grounded)
        {
            Entity = entity;
            Frame = frame;
            SpeedRatio = speedRatio;
            DirectionX = directionX;
            DirectionY = directionY;
            Grounded = grounded;
        }

        public CharacterControlEntityRef Entity { get; }

        public RuntimeFrame Frame { get; }

        public Fix64 SpeedRatio { get; }

        public Fix64 DirectionX { get; }

        public Fix64 DirectionY { get; }

        public bool Grounded { get; }

        public static CharacterControlAnimationLocomotionSample FromMotionResult(CharacterMotionResult result)
        {
            FixVector3 direction = result.MotionInput.MoveDirection;
            if (!direction.TryNormalize(out FixVector3 normalized))
            {
                normalized = FixVector3.Zero;
            }

            return new CharacterControlAnimationLocomotionSample(
                result.Command.Entity,
                result.Command.Frame,
                result.MotionInput.MoveSpeedScale,
                normalized.X,
                normalized.Z,
                result.Grounded);
        }
    }

    public readonly struct CharacterControlAnimationRequestRecord
    {
        public CharacterControlAnimationRequestRecord(
            CharacterControlAnimationRequestKind kind,
            string targetActorId,
            string bindingId,
            string actionKey,
            string blendId,
            MxAnimationLayerId layerId,
            ResourceKey clipKey,
            MxAnimationQuantizedParameter parameter,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY,
            string correlationId)
        {
            Kind = kind;
            TargetActorId = targetActorId ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            LayerId = layerId;
            ClipKey = clipKey;
            Parameter = parameter;
            ParameterX = parameterX;
            ParameterY = parameterY;
            CorrelationId = correlationId ?? string.Empty;
        }

        public CharacterControlAnimationRequestKind Kind { get; }

        public string TargetActorId { get; }

        public string BindingId { get; }

        public string ActionKey { get; }

        public string BlendId { get; }

        public MxAnimationLayerId LayerId { get; }

        public ResourceKey ClipKey { get; }

        public MxAnimationQuantizedParameter Parameter { get; }

        public MxAnimationQuantizedParameter ParameterX { get; }

        public MxAnimationQuantizedParameter ParameterY { get; }

        public string CorrelationId { get; }
    }

    public readonly struct CharacterControlAnimationAdapterResult
    {
        private CharacterControlAnimationAdapterResult(
            bool success,
            CharacterControlEntityRef entity,
            RuntimeFrame frame,
            CharacterControlAnimationRequestRecord request,
            MxAnimationBackendResult backendResult,
            CharacterControlAnimationMissingBindingKind missingBindingKind,
            string missingBindingId,
            string fallbackReason,
            string message)
        {
            Success = success;
            Entity = entity;
            Frame = frame;
            Request = request;
            BackendResult = backendResult;
            MissingBindingKind = missingBindingKind;
            MissingBindingId = missingBindingId ?? string.Empty;
            FallbackReason = fallbackReason ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public CharacterControlEntityRef Entity { get; }

        public RuntimeFrame Frame { get; }

        public CharacterControlAnimationRequestRecord Request { get; }

        public MxAnimationBackendResult BackendResult { get; }

        public CharacterControlAnimationMissingBindingKind MissingBindingKind { get; }

        public bool HasMissingBinding => MissingBindingKind != CharacterControlAnimationMissingBindingKind.None;

        public string MissingBindingId { get; }

        public string FallbackReason { get; }

        public string Message { get; }

        public static CharacterControlAnimationAdapterResult Requested(
            CharacterControlEntityRef entity,
            RuntimeFrame frame,
            CharacterControlAnimationRequestRecord request,
            MxAnimationBackendResult backendResult,
            string fallbackReason,
            string message)
        {
            return new CharacterControlAnimationAdapterResult(
                backendResult.Success,
                entity,
                frame,
                request,
                backendResult,
                CharacterControlAnimationMissingBindingKind.None,
                string.Empty,
                fallbackReason,
                message);
        }

        public static CharacterControlAnimationAdapterResult MissingBinding(
            CharacterControlEntityRef entity,
            RuntimeFrame frame,
            CharacterControlAnimationMissingBindingKind kind,
            string bindingId,
            string fallbackReason,
            string message)
        {
            return new CharacterControlAnimationAdapterResult(
                false,
                entity,
                frame,
                default,
                default,
                kind,
                bindingId,
                fallbackReason,
                message);
        }

        public static CharacterControlAnimationAdapterResult Recorded(
            CharacterControlEntityRef entity,
            RuntimeFrame frame,
            CharacterControlAnimationRequestRecord request,
            string fallbackReason,
            string message)
        {
            return new CharacterControlAnimationAdapterResult(
                true,
                entity,
                frame,
                request,
                default,
                CharacterControlAnimationMissingBindingKind.None,
                string.Empty,
                fallbackReason,
                message);
        }
    }

    public sealed class CharacterControlAnimationDiagnosticSnapshot
    {
        private readonly List<CharacterControlAnimationAdapterResult> _recentResults;

        public CharacterControlAnimationDiagnosticSnapshot(
            CharacterControlAnimationRequestRecord lastRequest,
            MxAnimationBackendResult lastBackendResult,
            CharacterControlAnimationMissingBindingKind missingBindingKind,
            string missingBindingId,
            string fallbackReason,
            IEnumerable<CharacterControlAnimationAdapterResult> recentResults)
        {
            LastRequest = lastRequest;
            LastBackendResult = lastBackendResult;
            MissingBindingKind = missingBindingKind;
            MissingBindingId = missingBindingId ?? string.Empty;
            FallbackReason = fallbackReason ?? string.Empty;
            _recentResults = recentResults != null
                ? new List<CharacterControlAnimationAdapterResult>(recentResults)
                : new List<CharacterControlAnimationAdapterResult>();
        }

        public CharacterControlAnimationRequestRecord LastRequest { get; }

        public MxAnimationBackendResult LastBackendResult { get; }

        public CharacterControlAnimationMissingBindingKind MissingBindingKind { get; }

        public string MissingBindingId { get; }

        public string FallbackReason { get; }

        public IReadOnlyList<CharacterControlAnimationAdapterResult> RecentResults => _recentResults;
    }

    public sealed class CharacterControlMxAnimationAdapter
    {
        private readonly CharacterControlMxAnimationAdapterOptions _options;
        private readonly Dictionary<CharacterControlEntityRef, ActorRuntime> _actors =
            new Dictionary<CharacterControlEntityRef, ActorRuntime>();
        private readonly Queue<CharacterControlAnimationAdapterResult> _recentResults =
            new Queue<CharacterControlAnimationAdapterResult>();
        private CharacterControlAnimationRequestRecord _lastRequest;
        private MxAnimationBackendResult _lastBackendResult;
        private CharacterControlAnimationMissingBindingKind _lastMissingBindingKind;
        private string _lastMissingBindingId = string.Empty;
        private string _lastFallbackReason = string.Empty;

        public CharacterControlMxAnimationAdapter(CharacterControlMxAnimationAdapterOptions options = null)
        {
            _options = options ?? new CharacterControlMxAnimationAdapterOptions();
            ValidateOptions(_options);
        }

        public int ActorCount => _actors.Count;

        public void RegisterActor(
            CharacterControlEntityRef entity,
            IMxAnimationBackend backend,
            MxAnimationSetDefinition animationSet,
            string targetActorId = "")
        {
            if (!entity.IsValid)
            {
                throw new ArgumentException("Character control animation actor entity must be valid.", nameof(entity));
            }

            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            _actors[entity] = new ActorRuntime(
                entity,
                string.IsNullOrWhiteSpace(targetActorId) ? BuildDefaultActorId(entity) : targetActorId,
                backend,
                animationSet ?? new MxAnimationSetDefinition(string.Empty, 0, default, default));
        }

        public bool UnregisterActor(CharacterControlEntityRef entity)
        {
            return _actors.Remove(entity);
        }

        public CharacterControlAnimationAdapterResult ApplyLocomotion(CharacterMotionResult result)
        {
            return ApplyLocomotion(CharacterControlAnimationLocomotionSample.FromMotionResult(result));
        }

        public CharacterControlAnimationAdapterResult ApplyLocomotion(CharacterControlAnimationLocomotionSample sample)
        {
            if (!TryFindActor(sample.Entity, out ActorRuntime actor))
            {
                return Record(CharacterControlAnimationAdapterResult.MissingBinding(
                    sample.Entity,
                    sample.Frame,
                    CharacterControlAnimationMissingBindingKind.Actor,
                    sample.Entity.ToString(),
                    string.Empty,
                    "No registered animation actor for character control entity."));
            }

            if (_options.LocomotionMode == CharacterControlAnimationLocomotionMode.Blend2D)
            {
                CharacterControlAnimationAdapterResult result = TryApplyLocomotionBlend2D(actor, sample);
                if (result.HasMissingBinding && _options.FallbackToBlend1DWhenBlend2DMissing)
                {
                    return ApplyLocomotionBlend1D(
                        actor,
                        sample,
                        "2D locomotion blend missing; fallback to 1D speed blend.");
                }

                return result.HasMissingBinding ? Record(result) : result;
            }

            return ApplyLocomotionBlend1D(actor, sample, string.Empty);
        }

        public CharacterControlAnimationAdapterResult ApplyReaction(CharacterPressureReactionEvent evt)
        {
            if (evt.Type != CharacterPressureReactionEventType.ReactionStarted)
            {
                var request = new CharacterControlAnimationRequestRecord(
                    CharacterControlAnimationRequestKind.None,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    default,
                    default,
                    default,
                    default,
                    default,
                    BuildCorrelationId(evt.Entity, evt.Frame, "reaction:recorded:" + evt.Kind));
                return Record(CharacterControlAnimationAdapterResult.Recorded(
                    evt.Entity,
                    evt.Frame,
                    request,
                    string.Empty,
                    "Pressure reaction event did not start a reaction animation."));
            }

            if (!TryFindActor(evt.Entity, out ActorRuntime actor))
            {
                return Record(CharacterControlAnimationAdapterResult.MissingBinding(
                    evt.Entity,
                    evt.Frame,
                    CharacterControlAnimationMissingBindingKind.Actor,
                    evt.Entity.ToString(),
                    string.Empty,
                    "No registered animation actor for character control entity."));
            }

            string actionKey = BuildReactionActionKey(evt.Kind);
            if (!actor.AnimationSet.TryFindBinding(string.Empty, actionKey, out MxAnimationActionBinding binding))
            {
                return Record(CharacterControlAnimationAdapterResult.MissingBinding(
                    evt.Entity,
                    evt.Frame,
                    CharacterControlAnimationMissingBindingKind.Reaction,
                    actionKey,
                    string.Empty,
                    "Reaction animation binding is missing."));
            }

            string correlationId = BuildCorrelationId(evt.Entity, evt.Frame, actionKey);
            if (_options.ReactionRequestKind == CharacterControlAnimationReactionRequestKind.Play)
            {
                var request = new MxAnimationPlayRequest
                {
                    TargetActorId = actor.TargetActorId,
                    BindingId = binding.BindingId,
                    ActionKey = actionKey,
                    ClipKey = binding.Clip,
                    LayerId = binding.Layer,
                    PlaybackSpeed = binding.PlaybackSpeed,
                    Loop = binding.Loop,
                    AlignmentPolicy = binding.AlignmentPolicy,
                    CorrelationId = correlationId
                };
                MxAnimationBackendResult backendResult = actor.Backend.Play(request);
                var record = new CharacterControlAnimationRequestRecord(
                    CharacterControlAnimationRequestKind.ReactionPlay,
                    request.TargetActorId,
                    request.BindingId,
                    request.ActionKey,
                    string.Empty,
                    request.LayerId,
                    request.ClipKey,
                    default,
                    default,
                    default,
                    request.CorrelationId);
                return Record(CharacterControlAnimationAdapterResult.Requested(
                    evt.Entity,
                    evt.Frame,
                    record,
                    backendResult,
                    string.Empty,
                    backendResult.Message));
            }

            var crossFade = new MxAnimationCrossFadeRequest
            {
                TargetActorId = actor.TargetActorId,
                BindingId = binding.BindingId,
                ActionKey = actionKey,
                ClipKey = binding.Clip,
                LayerId = binding.Layer,
                FadeDurationSeconds = Math.Max(0f, _options.ReactionCrossFadeDurationSeconds),
                PlaybackSpeed = binding.PlaybackSpeed,
                Loop = binding.Loop,
                AlignmentPolicy = binding.AlignmentPolicy,
                CorrelationId = correlationId
            };
            MxAnimationBackendResult result = actor.Backend.CrossFade(crossFade);
            var crossFadeRecord = new CharacterControlAnimationRequestRecord(
                CharacterControlAnimationRequestKind.ReactionCrossFade,
                crossFade.TargetActorId,
                crossFade.BindingId,
                crossFade.ActionKey,
                string.Empty,
                crossFade.LayerId,
                crossFade.ClipKey,
                default,
                default,
                default,
                crossFade.CorrelationId);
            return Record(CharacterControlAnimationAdapterResult.Requested(
                evt.Entity,
                evt.Frame,
                crossFadeRecord,
                result,
                string.Empty,
                result.Message));
        }

        public CharacterControlAnimationAdapterResult ApplyActionEvent(CharacterActionEvent evt)
        {
            CharacterControlEntityRef entity = evt.Request.Entity;
            RuntimeFrame frame = evt.Request.Frame;
            var request = new CharacterControlAnimationRequestRecord(
                CharacterControlAnimationRequestKind.ActionDelegated,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                default,
                default,
                default,
                default,
                default,
                BuildCorrelationId(entity, frame, "action:delegated:" + evt.Type));
            return Record(CharacterControlAnimationAdapterResult.Recorded(
                entity,
                frame,
                request,
                "Action animation is delegated to CombatMxAnimationUnityBridge.",
                "Character action animation was not forwarded to the backend by CharacterControlMxAnimationAdapter."));
        }

        public CharacterControlAnimationDiagnosticSnapshot CreateSnapshot()
        {
            return new CharacterControlAnimationDiagnosticSnapshot(
                _lastRequest,
                _lastBackendResult,
                _lastMissingBindingKind,
                _lastMissingBindingId,
                _lastFallbackReason,
                _recentResults);
        }

        private CharacterControlAnimationAdapterResult TryApplyLocomotionBlend2D(
            ActorRuntime actor,
            CharacterControlAnimationLocomotionSample sample)
        {
            string blendId = ResolveBlendId(sample.Grounded, _options.GroundedBlend2DId, _options.AirborneBlend2DId);
            if (!TryFindBlend2DById(actor.AnimationSet, blendId, out MxAnimationBlend2DDefinition definition))
            {
                return CharacterControlAnimationAdapterResult.MissingBinding(
                    sample.Entity,
                    sample.Frame,
                    CharacterControlAnimationMissingBindingKind.LocomotionBlend2D,
                    blendId,
                    string.Empty,
                    "2D locomotion blend definition is missing.");
            }

            MxAnimationQuantizedParameter x = QuantizeDirectionalSpeed(_options.DirectionXParameterId, sample.DirectionX, sample.SpeedRatio);
            MxAnimationQuantizedParameter y = QuantizeDirectionalSpeed(_options.DirectionYParameterId, sample.DirectionY, sample.SpeedRatio);
            string correlationId = BuildCorrelationId(sample.Entity, sample.Frame, blendId);
            var request = new MxAnimationBlend2DRequest
            {
                TargetActorId = actor.TargetActorId,
                BlendId = definition.BlendId,
                ParameterX = x,
                ParameterY = y,
                FadeDurationSeconds = ResolveLocomotionFade(definition.FadeDurationSeconds),
                CorrelationId = correlationId
            };
            MxAnimationBackendResult result = actor.Backend.SetBlend2D(request);
            var record = new CharacterControlAnimationRequestRecord(
                CharacterControlAnimationRequestKind.LocomotionBlend2D,
                request.TargetActorId,
                string.Empty,
                string.Empty,
                request.BlendId,
                definition.LayerId,
                default,
                default,
                request.ParameterX,
                request.ParameterY,
                request.CorrelationId);
            return Record(CharacterControlAnimationAdapterResult.Requested(
                sample.Entity,
                sample.Frame,
                record,
                result,
                string.Empty,
                result.Message));
        }

        private CharacterControlAnimationAdapterResult ApplyLocomotionBlend1D(
            ActorRuntime actor,
            CharacterControlAnimationLocomotionSample sample,
            string fallbackReason)
        {
            string blendId = ResolveBlendId(sample.Grounded, _options.GroundedBlend1DId, _options.AirborneBlend1DId);
            if (!TryFindBlend1DById(actor.AnimationSet, blendId, out MxAnimationBlend1DDefinition definition))
            {
                return Record(CharacterControlAnimationAdapterResult.MissingBinding(
                    sample.Entity,
                    sample.Frame,
                    CharacterControlAnimationMissingBindingKind.LocomotionBlend1D,
                    blendId,
                    fallbackReason,
                    "1D locomotion blend definition is missing."));
            }

            MxAnimationQuantizedParameter speed = QuantizeSpeed(_options.SpeedParameterId, sample.SpeedRatio);
            string correlationId = BuildCorrelationId(sample.Entity, sample.Frame, blendId);
            var request = new MxAnimationBlend1DRequest
            {
                TargetActorId = actor.TargetActorId,
                BlendId = definition.BlendId,
                Parameter = speed,
                FadeDurationSeconds = ResolveLocomotionFade(definition.FadeDurationSeconds),
                CorrelationId = correlationId
            };
            MxAnimationBackendResult result = actor.Backend.SetBlend1D(request);
            var record = new CharacterControlAnimationRequestRecord(
                CharacterControlAnimationRequestKind.LocomotionBlend1D,
                request.TargetActorId,
                string.Empty,
                string.Empty,
                request.BlendId,
                definition.LayerId,
                default,
                request.Parameter,
                default,
                default,
                request.CorrelationId);
            return Record(CharacterControlAnimationAdapterResult.Requested(
                sample.Entity,
                sample.Frame,
                record,
                result,
                fallbackReason,
                result.Message));
        }

        private CharacterControlAnimationAdapterResult Record(CharacterControlAnimationAdapterResult result)
        {
            _lastRequest = result.Request;
            _lastBackendResult = result.BackendResult;
            _lastMissingBindingKind = result.MissingBindingKind;
            _lastMissingBindingId = result.MissingBindingId;
            _lastFallbackReason = result.FallbackReason;

            int max = Math.Max(1, _options.MaxRecentDiagnostics);
            while (_recentResults.Count >= max)
            {
                _recentResults.Dequeue();
            }

            _recentResults.Enqueue(result);
            return result;
        }

        private static bool TryFindBlend1DById(
            MxAnimationSetDefinition animationSet,
            string blendId,
            out MxAnimationBlend1DDefinition definition)
        {
            if (animationSet != null && !string.IsNullOrWhiteSpace(blendId))
            {
                for (int i = 0; i < animationSet.Blend1DDefinitions.Count; i++)
                {
                    MxAnimationBlend1DDefinition candidate = animationSet.Blend1DDefinitions[i];
                    if (candidate != null && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        private static bool TryFindBlend2DById(
            MxAnimationSetDefinition animationSet,
            string blendId,
            out MxAnimationBlend2DDefinition definition)
        {
            if (animationSet != null && !string.IsNullOrWhiteSpace(blendId))
            {
                for (int i = 0; i < animationSet.Blend2DDefinitions.Count; i++)
                {
                    MxAnimationBlend2DDefinition candidate = animationSet.Blend2DDefinitions[i];
                    if (candidate != null && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        private bool TryFindActor(CharacterControlEntityRef entity, out ActorRuntime actor)
        {
            if (_actors.TryGetValue(entity, out actor))
            {
                return true;
            }

            foreach (KeyValuePair<CharacterControlEntityRef, ActorRuntime> pair in _actors)
            {
                if (MatchesEntity(pair.Key, entity))
                {
                    actor = pair.Value;
                    return true;
                }
            }

            actor = default;
            return false;
        }

        private static bool MatchesEntity(CharacterControlEntityRef registered, CharacterControlEntityRef requested)
        {
            if (registered.StableId > 0 && requested.StableId > 0)
            {
                return registered.StableId == requested.StableId;
            }

            if (registered.HasCombatEntity && requested.HasCombatEntity)
            {
                return registered.CombatEntityId.Equals(requested.CombatEntityId);
            }

            if (registered.HasGameplayEntity && requested.HasGameplayEntity)
            {
                return registered.GameplayEntityId.Equals(requested.GameplayEntityId);
            }

            return registered.Equals(requested);
        }

        private MxAnimationQuantizedParameter QuantizeSpeed(string parameterId, Fix64 value)
        {
            Fix64 clamped = Fix64.Clamp(value, Fix64.Zero, _options.MaxSpeedRatio);
            return new MxAnimationQuantizedParameter(parameterId, Quantize(clamped), _options.ParameterScale);
        }

        private MxAnimationQuantizedParameter QuantizeDirectionalSpeed(string parameterId, Fix64 direction, Fix64 speedRatio)
        {
            Fix64 clampedDirection = Fix64.Clamp(direction, -Fix64.One, Fix64.One);
            Fix64 clampedSpeed = Fix64.Clamp(speedRatio, Fix64.Zero, _options.MaxSpeedRatio);
            Fix64 value = clampedDirection * clampedSpeed;
            Fix64 max = _options.MaxSpeedRatio;
            Fix64 min = -max;
            return new MxAnimationQuantizedParameter(parameterId, Quantize(Fix64.Clamp(value, min, max)), _options.ParameterScale);
        }

        private int Quantize(Fix64 value)
        {
            return checked((int)(value.RawValue * _options.ParameterScale / Fix64.Scale));
        }

        private float ResolveLocomotionFade(float definitionFadeDurationSeconds)
        {
            return _options.LocomotionFadeDurationSeconds >= 0f
                ? _options.LocomotionFadeDurationSeconds
                : definitionFadeDurationSeconds;
        }

        private string BuildReactionActionKey(CharacterPressureReactionKind kind)
        {
            return (_options.ReactionActionKeyPrefix ?? string.Empty) + kind;
        }

        private static string ResolveBlendId(bool grounded, string groundedBlendId, string airborneBlendId)
        {
            if (!grounded && !string.IsNullOrWhiteSpace(airborneBlendId))
            {
                return airborneBlendId;
            }

            return groundedBlendId ?? string.Empty;
        }

        private static string BuildDefaultActorId(CharacterControlEntityRef entity)
        {
            if (entity.StableId > 0)
            {
                return "character:" + entity.StableId.ToString(CultureInfo.InvariantCulture);
            }

            if (entity.HasCombatEntity)
            {
                return "combat:" + entity.CombatEntityId;
            }

            if (entity.HasGameplayEntity)
            {
                return "gameplay:" + entity.GameplayEntityId;
            }

            return "character:unknown";
        }

        private static string BuildCorrelationId(CharacterControlEntityRef entity, RuntimeFrame frame, string reason)
        {
            return BuildDefaultActorId(entity)
                + "|frame:" + frame.Value.ToString(CultureInfo.InvariantCulture)
                + "|reason:" + (reason ?? string.Empty);
        }

        private static void ValidateOptions(CharacterControlMxAnimationAdapterOptions options)
        {
            if (options.ParameterScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.ParameterScale));
            if (options.MaxSpeedRatio < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(options.MaxSpeedRatio));
            if (options.MaxRecentDiagnostics <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaxRecentDiagnostics));
        }

        private readonly struct ActorRuntime
        {
            public ActorRuntime(
                CharacterControlEntityRef entity,
                string targetActorId,
                IMxAnimationBackend backend,
                MxAnimationSetDefinition animationSet)
            {
                Entity = entity;
                TargetActorId = targetActorId ?? string.Empty;
                Backend = backend;
                AnimationSet = animationSet;
            }

            public CharacterControlEntityRef Entity { get; }

            public string TargetActorId { get; }

            public IMxAnimationBackend Backend { get; }

            public MxAnimationSetDefinition AnimationSet { get; }
        }
    }
}
