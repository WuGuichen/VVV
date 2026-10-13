using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.Animation.Unity
{
    public sealed class UnityPlayablesAnimationBackend : IMxAnimationBackend, IMxAnimationRewindableBackend, IMxAnimationTimeSynchronizedBackend, IMxAnimationPoseSamplingBackend, IMxAnimationPoseEvaluationBackend
    {
        private const int MaxRecentItems = 16;
        private const double Epsilon = 0.000001d;

        private readonly Animator _animator;
        private readonly IResourceManager _resourceManager;
        private readonly MxAnimationSetDefinition _definition;
        private readonly string _actorId;
        private readonly Dictionary<MxAnimationLayerId, LayerRuntime> _layers = new Dictionary<MxAnimationLayerId, LayerRuntime>();
        private readonly List<PendingLoad> _pendingLoads = new List<PendingLoad>();
        private readonly List<PendingMaskLoad> _pendingMaskLoads = new List<PendingMaskLoad>();
        private readonly Dictionary<ResourceKey, float> _playbackSpeedOverrides = new Dictionary<ResourceKey, float>();
        private readonly Dictionary<string, Blend2DPointCoordinateOverride> _blend2DPointCoordinateOverrides =
            new Dictionary<string, Blend2DPointCoordinateOverride>();
        private readonly IMxAnimationPlayableDiagnostics _diagnostics;
        private readonly MxAnimationPlayableGraphRuntime _playables;
        private readonly MxAnimationPlayableStateCache _stateCache;
        private readonly MxAnimationBlend2DEvaluationBuffer _blend2DEvaluationBuffer =
            new MxAnimationBlend2DEvaluationBuffer();
        private readonly List<BlendClipWeight> _blendClipWeightScratch = new List<BlendClipWeight>();
        private readonly List<MxAnimationBlend1DWeight> _blend1DDiagnosticWeightScratch =
            new List<MxAnimationBlend1DWeight>();
        private readonly List<MxAnimationBlend2DWeight> _blend2DDiagnosticWeightScratch =
            new List<MxAnimationBlend2DWeight>();

        private ResidentClip _defaultClip;
        private ResidentClip _fallbackClip;
        private bool _released;

        public UnityPlayablesAnimationBackend(
            Animator animator,
            IResourceManager resourceManager,
            MxAnimationSetDefinition definition,
            string actorId = "")
        {
            _animator = animator != null ? animator : throw new ArgumentNullException(nameof(animator));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _definition = definition ?? new MxAnimationSetDefinition(string.Empty, 0, default, default);
            _actorId = actorId ?? string.Empty;
            _diagnostics = new MxAnimationPlayableDiagnosticBuffer(MaxRecentItems);
            _playables = new MxAnimationPlayableGraphRuntime(_animator);
            _stateCache = new MxAnimationPlayableStateCache();

            _defaultClip = LoadResidentClip("default", _definition.DefaultClip);
            _fallbackClip = LoadResidentClip("fallback", _definition.FallbackClip);
            InitializeConfiguredLayers();
            ProcessPendingLoads();
            ProcessPendingMaskLoads();
        }

        public string BackendName => "UnityPlayables";

        public long PresentationStateAdvances { get; private set; }
        public long PoseEvaluations { get; private set; }

        public bool CanDeferPoseEvaluation
        {
            get
            {
                if (_released || PoseEvaluations == 0 || _animator == null || _animator.applyRootMotion
                    || _pendingLoads.Count != 0 || _pendingMaskLoads.Count != 0) return false;
                foreach (LayerRuntime layer in _layers.Values)
                {
                    if (RequiresClipEvents(layer.Current)) return false;
                    for (int i = 0; i < layer.Outgoing.Count; i++)
                        if (RequiresClipEvents(layer.Outgoing[i])) return false;
                    for (int i = 0; i < layer.BlendSlots.Count; i++)
                        if (RequiresClipEvents(layer.BlendSlots[i])) return false;
                }
                return true;
            }
        }

        private bool RequiresClipEvents(ClipSlot slot) => _animator.fireEvents && slot != null && slot.HasEvents;

        public void AdvancePresentationState(float deltaTime)
        {
            if (_released) return;
            deltaTime = float.IsFinite(deltaTime) ? Math.Max(0f, deltaTime) : 0f;
            PresentationStateAdvances++;
            ProcessPendingLoads();
            ProcessPendingMaskLoads();
            foreach (LayerRuntime layer in _layers.Values)
            {
                TickLayer(layer, deltaTime);
                ClipSlot anchored = layer.TimeSynchronizedSlot;
                AdvanceSlotTime(layer.Current, deltaTime,
                    anchored == layer.Current ? layer.SynchronizedTimeSeconds : (double?)null);
                for (int i = 0; i < layer.Outgoing.Count; i++)
                    AdvanceSlotTime(layer.Outgoing[i], deltaTime, null);
                // SynchronizeBlendSlotPhases seeks each blend slot to the same time used by Tick.
                // Unity samples that seek on this evaluation; the next phase was already advanced in TickLayer.
                layer.TimeSynchronizedSlot = null;
            }
        }

        private static void AdvanceSlotTime(ClipSlot slot, float deltaTime, double? anchoredTime)
        {
            if (slot == null || !slot.Playable.IsValid()) return;
            // Blend phases already set their current time and speed in TickLayer.
            double time = anchoredTime ?? (slot.Playable.GetTime() + deltaTime * slot.Playable.GetSpeed());
            slot.Playable.SetTime(time);
            slot.Playable.SetDone(false);
        }

        public bool SetClipPlaybackSpeedOverride(ResourceKey clipKey, float playbackSpeed)
        {
            if (!clipKey.IsValid)
                return false;

            _playbackSpeedOverrides[clipKey] = NormalizeSpeed(playbackSpeed);
            return true;
        }

        public bool TryGetClipPlaybackSpeedOverride(ResourceKey clipKey, out float playbackSpeed)
        {
            return _playbackSpeedOverrides.TryGetValue(clipKey, out playbackSpeed);
        }

        public bool ClearClipPlaybackSpeedOverride(ResourceKey clipKey)
        {
            return clipKey.IsValid && _playbackSpeedOverrides.Remove(clipKey);
        }

        /// <summary>LowFreqAlloc: resolves one clip length without transferring resource ownership.</summary>
        public bool TryGetClipDuration(ResourceKey clipKey, out float durationSeconds)
        {
            durationSeconds = 0f;
            if (_released || !clipKey.IsValid)
                return false;

            ResourceLoadResult<ResourceHandle<AnimationClip>> result =
                _resourceManager.Load<AnimationClip>(clipKey);
            if (!result.Success || result.Value == null)
                return false;

            try
            {
                AnimationClip clip = result.Value.Value;
                if (clip == null || clip.length <= 0f || float.IsNaN(clip.length) || float.IsInfinity(clip.length))
                    return false;
                durationSeconds = clip.length;
                return true;
            }
            finally
            {
                _resourceManager.Release(result.Value);
            }
        }

        public bool SetBlend2DPointCoordinateOverride(
            string blendId,
            ResourceKey clipKey,
            int x,
            int y)
        {
            if (string.IsNullOrWhiteSpace(blendId) || !clipKey.IsValid)
                return false;

            _blend2DPointCoordinateOverrides[CreateBlend2DPointOverrideKey(blendId, clipKey)] =
                new Blend2DPointCoordinateOverride(x, y);
            return true;
        }

        public static UnityPlayablesAnimationBackend Create(
            Animator animator,
            IResourceManager resourceManager,
            MxAnimationSetDefinition definition,
            string actorId = "")
        {
            return new UnityPlayablesAnimationBackend(animator, resourceManager, definition, actorId);
        }

        public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            if (!TryResolvePlay(request, out ClipRequest resolved, out string error))
                return InvalidRequest(MxAnimationRequestKind.Play, request != null ? request.LayerId : MxAnimationLayerId.Base, default, error);

            LayerRuntime layer = GetOrCreateLayer(resolved.LayerId);
            if (TryReuseCurrentForPlay(layer, resolved, out MxAnimationBackendResult reuseResult))
                return reuseResult;

            layer.Status = MxAnimationLayerStatus.Loading;
            layer.NextClipKey = resolved.ClipKey;
            return BeginClipLoad(resolved, MxAnimationRequestKind.Play, false);
        }

        public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            MxAnimationLayerId layerId = ResolveStopLayer(request);
            if (!_layers.TryGetValue(layerId, out LayerRuntime layer))
            {
                AddRequest(new MxAnimationRequestDiagnostic(
                    MxAnimationRequestKind.Stop,
                    layerId,
                    default,
                    default,
                    false,
                    MxAnimationBackendResultCode.Success,
                    request != null ? request.CorrelationId : string.Empty,
                    "Layer was already stopped."));
                return MxAnimationBackendResult.Succeeded(default, "Layer was already stopped.");
            }

            InvalidatePendingClipLoads(layerId);
            layer.NextClipKey = default;
            float fade = request != null ? Math.Max(0f, request.FadeOutDurationSeconds) : 0f;
            if (fade <= 0f)
            {
                ReleaseLayerSlots(layer);
                layer.Status = MxAnimationLayerStatus.Stopped;
            }
            else
            {
                MoveBlendSlotsToOutgoing(layer, fade);
                MoveCurrentToOutgoing(layer, fade);
                layer.Status = MxAnimationLayerStatus.FadingOut;
            }

            AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.Stop,
                layerId,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                request != null ? request.CorrelationId : string.Empty,
                "Stop accepted."));
            return MxAnimationBackendResult.Succeeded(default, "Stop accepted.");
        }

        public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            if (!TryResolveCrossFade(request, out ClipRequest resolved, out string error))
                return InvalidRequest(MxAnimationRequestKind.CrossFade, request != null ? request.LayerId : MxAnimationLayerId.Base, default, error);

            LayerRuntime layer = GetOrCreateLayer(resolved.LayerId);
            if (layer.Current != null && layer.Current.Key == resolved.ClipKey)
            {
                UpdateSlotPlayback(layer.Current, resolved.PlaybackSpeed, resolved.Loop, resolved.StartOffsetSeconds, true);
                _stateCache.RecordHit();
                AddRequest(new MxAnimationRequestDiagnostic(
                    MxAnimationRequestKind.CrossFade,
                    resolved.LayerId,
                    resolved.ClipKey,
                    resolved.ClipKey,
                    false,
                    MxAnimationBackendResultCode.Success,
                    resolved.CorrelationId,
                    "Crossfade target is already current."));
                return MxAnimationBackendResult.Succeeded(resolved.ClipKey, "Crossfade target is already current.");
            }

            layer.Status = MxAnimationLayerStatus.Loading;
            layer.NextClipKey = resolved.ClipKey;
            return BeginClipLoad(resolved, MxAnimationRequestKind.CrossFade, false);
        }

        public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            if (request == null)
                return InvalidRequest(MxAnimationRequestKind.SetLayerWeight, MxAnimationLayerId.Base, default, "Layer weight request is null.");

            LayerRuntime layer = GetOrCreateLayer(request.LayerId);
            float targetWeight = Clamp01(request.Weight);
            float fadeDuration = Math.Max(0f, request.FadeDurationSeconds);
            if (fadeDuration <= 0f)
            {
                layer.Weight = targetWeight;
                layer.TargetWeight = targetWeight;
                layer.WeightTransitionElapsedSeconds = 0f;
                layer.WeightTransitionDurationSeconds = 0f;
                layer.WeightTransitionPolicyId = request.TransitionPolicyId ?? string.Empty;
                layer.WeightTransitionCorrelationId = request.CorrelationId ?? string.Empty;
                ApplyLayerWeight(layer);
            }
            else
            {
                layer.StartWeight = layer.Weight;
                layer.TargetWeight = targetWeight;
                layer.WeightTransitionElapsedSeconds = 0f;
                layer.WeightTransitionDurationSeconds = fadeDuration;
                layer.WeightTransitionPolicyId = request.TransitionPolicyId ?? string.Empty;
                layer.WeightTransitionCorrelationId = request.CorrelationId ?? string.Empty;
            }

            AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.SetLayerWeight,
                layer.LayerId,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                request.CorrelationId,
                "Layer weight accepted."));
            return MxAnimationBackendResult.Succeeded(default, "Layer weight accepted.");
        }

        public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            if (request == null)
                return InvalidRequest(MxAnimationRequestKind.SetBlend1D, MxAnimationLayerId.Base, default, "1D blend request is null.");

            if (!_definition.TryFindBlend1DDefinition(request.BlendId, request.Parameter.ParameterId, out MxAnimationBlend1DDefinition blend))
                return InvalidRequest(MxAnimationRequestKind.SetBlend1D, MxAnimationLayerId.Base, default, "1D blend definition was not found.");

            MxAnimationBlend1DWeights weights = MxAnimationBlend1DCalculator.Evaluate(blend, request.Parameter);
            if (weights.Weights.Count == 0)
                return InvalidRequest(MxAnimationRequestKind.SetBlend1D, blend.LayerId, default, "1D blend definition has no valid points.");

            List<BlendClipWeight> clipWeights = _blendClipWeightScratch;
            List<MxAnimationBlend1DWeight> diagnosticWeights = _blend1DDiagnosticWeightScratch;
            clipWeights.Clear();
            diagnosticWeights.Clear();
            for (int i = 0; i < weights.Weights.Count; i++)
            {
                MxAnimationBlend1DWeight weight = weights.Weights[i];
                float playbackSpeed = ResolvePlaybackSpeed(weight.ClipKey, weight.PlaybackSpeed);
                clipWeights.Add(new BlendClipWeight(weight.ClipKey, weight.Weight, playbackSpeed, weight.Loop));
                diagnosticWeights.Add(new MxAnimationBlend1DWeight(
                    weight.ClipKey,
                    weight.Threshold,
                    weight.Weight,
                    playbackSpeed,
                    weight.Loop));
            }

            return ApplyBlendWeights(
                MxAnimationBlendKind.Blend1D,
                request.BlendId,
                ResolveBlendFadeDuration(request.FadeDurationSeconds, blend.FadeDurationSeconds),
                request.CorrelationId,
                MxAnimationRequestKind.SetBlend1D,
                blend.LayerId,
                clipWeights,
                "1D blend clip failed to load.",
                "1D blend weights applied.",
                request.Parameter,
                default,
                default,
                diagnosticWeights,
                null);
        }

        public MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request)
        {
            if (_released)
                return BackendReleasedResult(default);

            if (request == null)
                return InvalidRequest(MxAnimationRequestKind.SetBlend2D, MxAnimationLayerId.Base, default, "2D blend request is null.");

            if (!_definition.TryFindBlend2DDefinition(request.BlendId, request.ParameterX.ParameterId, request.ParameterY.ParameterId, out MxAnimationBlend2DDefinition blend))
                return InvalidRequest(MxAnimationRequestKind.SetBlend2D, MxAnimationLayerId.Base, default, "2D blend definition was not found.");

            MxAnimationBlend2DDefinition resolvedBlend = ApplyBlend2DPointCoordinateOverrides(blend);
            MxAnimationBlend2DCalculator.Evaluate(resolvedBlend, request.ParameterX, request.ParameterY, _blend2DEvaluationBuffer);
            IReadOnlyList<MxAnimationBlend2DWeight> weights = _blend2DEvaluationBuffer.Weights;
            if (weights.Count == 0)
                return InvalidRequest(MxAnimationRequestKind.SetBlend2D, resolvedBlend.LayerId, default, "2D blend definition has no valid points.");

            List<BlendClipWeight> clipWeights = _blendClipWeightScratch;
            List<MxAnimationBlend2DWeight> diagnosticWeights = _blend2DDiagnosticWeightScratch;
            clipWeights.Clear();
            diagnosticWeights.Clear();
            float playbackSpeedMultiplier = NormalizePlaybackSpeedMultiplier(request.PlaybackSpeedMultiplier);
            for (int i = 0; i < weights.Count; i++)
            {
                MxAnimationBlend2DWeight weight = weights[i];
                float playbackSpeed = NormalizeSpeed(ResolvePlaybackSpeed(weight.ClipKey, weight.PlaybackSpeed) * playbackSpeedMultiplier);
                clipWeights.Add(new BlendClipWeight(weight.ClipKey, weight.Weight, playbackSpeed, weight.Loop));
                diagnosticWeights.Add(new MxAnimationBlend2DWeight(
                    weight.ClipKey,
                    weight.X,
                    weight.Y,
                    weight.Weight,
                    playbackSpeed,
                    weight.Loop));
            }

            return ApplyBlendWeights(
                MxAnimationBlendKind.Blend2D,
                request.BlendId,
                ResolveBlendFadeDuration(request.FadeDurationSeconds, resolvedBlend.FadeDurationSeconds),
                request.CorrelationId,
                MxAnimationRequestKind.SetBlend2D,
                resolvedBlend.LayerId,
                clipWeights,
                "2D blend clip failed to load.",
                "2D blend weights applied.",
                default,
                request.ParameterX,
                request.ParameterY,
                null,
                diagnosticWeights);
        }

        private MxAnimationBackendResult ApplyBlendWeights(
            MxAnimationBlendKind blendKind,
            string blendId,
            float fadeDurationSeconds,
            string correlationId,
            MxAnimationRequestKind requestKind,
            MxAnimationLayerId layerId,
            IReadOnlyList<BlendClipWeight> weights,
            string failureMessage,
            string successMessage,
            MxAnimationQuantizedParameter blendParameter,
            MxAnimationQuantizedParameter blend2DParameterX,
            MxAnimationQuantizedParameter blend2DParameterY,
            IReadOnlyList<MxAnimationBlend1DWeight> blend1DWeights,
            IReadOnlyList<MxAnimationBlend2DWeight> blend2DWeights)
        {
            LayerRuntime layer = GetOrCreateLayer(layerId);
            string resolvedBlendId = blendId ?? string.Empty;
            float blendFadeDuration = Math.Max(0f, fadeDurationSeconds);
            bool blendChanged = layer.ActiveBlendKind != blendKind
                || !string.Equals(layer.ActiveBlendId, resolvedBlendId, StringComparison.Ordinal);
            bool beginBlendFade = blendFadeDuration > 0f && layer.Current != null;
            if (blendChanged)
            {
                if (!beginBlendFade)
                    ReleaseOutgoingSlots(layer);
                ReleaseBlendSlots(layer);
                layer.BlendNormalizedPhase = 0d;
            }
            else if (layer.BlendFadeDurationSeconds <= 0f && !beginBlendFade)
            {
                ReleaseOutgoingSlots(layer);
            }

            if (beginBlendFade)
            {
                // An interrupted crossfade's pose includes all outgoing clips, not just Current.
                // Retain their sampled weights and fade the complete pose over the new duration.
                for (int i = layer.Outgoing.Count - 1; i >= 0; i--)
                {
                    ClipSlot outgoing = layer.Outgoing[i];
                    if (outgoing.Weight <= Epsilon)
                    {
                        layer.Outgoing.RemoveAt(i);
                        DetachAndReleaseSlot(layer, outgoing);
                        continue;
                    }
                    outgoing.TargetWeight = outgoing.Weight;
                    outgoing.FadeElapsedSeconds = 0f;
                    outgoing.FadeDurationSeconds = blendFadeDuration;
                }
                MoveCurrentToOutgoing(layer, blendFadeDuration);
                layer.BlendFadeElapsedSeconds = 0f;
                layer.BlendFadeDurationSeconds = blendFadeDuration;
            }
            else if (layer.BlendFadeDurationSeconds <= 0f)
            {
                ReleaseCurrentSlot(layer);
            }

            ReleaseStaleBlendSlots(layer, weights);
            layer.ActiveBlendId = resolvedBlendId;
            layer.ActiveBlendKind = blendKind;
            ApplyBlendDiagnostics(layer, blendKind, blendParameter, blend2DParameterX, blend2DParameterY, blend1DWeights, blend2DWeights);

            ResourceKey dominantKey = default;
            float dominantWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                BlendClipWeight weight = weights[i];
                if (weight.Weight > dominantWeight)
                {
                    dominantWeight = weight.Weight;
                    dominantKey = weight.ClipKey;
                }

                ClipSlot slot = FindBlendSlot(layer, weight.ClipKey);
                if (slot != null)
                {
                    UpdateSlotPlayback(slot, weight.PlaybackSpeed, weight.Loop, 0f, false);
                    _stateCache.RecordHit();
                }
                else
                {
                    if (weight.Weight <= 0f)
                        continue;

                    if (!TryCreateBlendSlot(weight, failureMessage, out slot, out ResourceError error))
                    {
                        layer.Status = MxAnimationLayerStatus.Failed;
                        layer.LastError = error;
                        TrackResourceError(error);
                        ReleaseLayerSlots(layer);
                        AddRequest(new MxAnimationRequestDiagnostic(
                            requestKind,
                            layerId,
                            weight.ClipKey,
                            weight.ClipKey,
                            false,
                            MxAnimationBackendResultCode.LoadFailed,
                            correlationId,
                            failureMessage));
                        return MxAnimationBackendResult.Failed(
                            MxAnimationBackendResultCode.LoadFailed,
                            weight.ClipKey,
                            error,
                            failureMessage);
                    }

                    ConnectSlot(layer, slot, weight.Weight);
                    layer.BlendSlots.Add(slot);
                    _stateCache.RecordMiss();
                }

                slot.TargetWeight = weight.Weight;
                if (layer.BlendFadeDurationSeconds > 0f)
                    SetSlotWeight(layer, slot, weight.Weight * CalculateProgress(layer.BlendFadeElapsedSeconds, layer.BlendFadeDurationSeconds));
                else
                    SetSlotWeight(layer, slot, weight.Weight);
            }

            layer.Status = MxAnimationLayerStatus.Playing;
            layer.LastError = ResourceError.None;
            layer.NextClipKey = default;
            AddRequest(new MxAnimationRequestDiagnostic(
                requestKind,
                layerId,
                dominantKey,
                dominantKey,
                false,
                MxAnimationBackendResultCode.Success,
                correlationId,
                successMessage));
            return MxAnimationBackendResult.Succeeded(dominantKey, successMessage);
        }

        private static void ApplyBlendDiagnostics(
            LayerRuntime layer,
            MxAnimationBlendKind blendKind,
            MxAnimationQuantizedParameter blendParameter,
            MxAnimationQuantizedParameter blend2DParameterX,
            MxAnimationQuantizedParameter blend2DParameterY,
            IReadOnlyList<MxAnimationBlend1DWeight> blend1DWeights,
            IReadOnlyList<MxAnimationBlend2DWeight> blend2DWeights)
        {
            layer.BlendParameter = blendKind == MxAnimationBlendKind.Blend1D ? blendParameter : default;
            layer.BlendWeights.Clear();
            if (blendKind == MxAnimationBlendKind.Blend1D && blend1DWeights != null)
            {
                for (int i = 0; i < blend1DWeights.Count; i++)
                    layer.BlendWeights.Add(blend1DWeights[i]);
            }

            layer.Blend2DParameterX = blendKind == MxAnimationBlendKind.Blend2D ? blend2DParameterX : default;
            layer.Blend2DParameterY = blendKind == MxAnimationBlendKind.Blend2D ? blend2DParameterY : default;
            layer.Blend2DWeights.Clear();
            if (blendKind == MxAnimationBlendKind.Blend2D && blend2DWeights != null)
            {
                for (int i = 0; i < blend2DWeights.Count; i++)
                    layer.Blend2DWeights.Add(blend2DWeights[i]);
            }
        }

        /// <remarks>LowFreqAlloc at the Unity or external-backend boundary; managed steady-state work avoids temporary collections with stable inputs.</remarks>
        public bool TrySynchronizeCurrentClipTime(MxAnimationLayerId layerId, ResourceKey expectedClipKey, double timeSeconds)
        {
            if (_released || double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d
                || !_layers.TryGetValue(layerId, out LayerRuntime layer)
                || layer.Current == null || layer.Current.Key != expectedClipKey)
                return false;
            layer.TimeSynchronizedSlot = layer.Current;
            layer.SynchronizedTimeSeconds = timeSeconds;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_released)
                return;

            if (deltaTime < 0f)
                deltaTime = 0f;

            PresentationStateAdvances++;

            ProcessPendingLoads();
            ProcessPendingMaskLoads();
            foreach (LayerRuntime layer in _layers.Values)
                TickLayer(layer, deltaTime);

            foreach (LayerRuntime layer in _layers.Values)
            {
                ClipSlot slot = layer.TimeSynchronizedSlot;
                if (slot != null && slot == layer.Current && slot.Playable.IsValid())
                {
                    slot.Playable.SetTime(layer.SynchronizedTimeSeconds);
                    // Evaluate the graph once. The anchored clip must not advance again by render delta.
                    slot.Playable.SetSpeed(0d);
                }
            }
            try
            {
                _playables.Evaluate(deltaTime);
                PoseEvaluations++;
            }
            finally
            {
                foreach (LayerRuntime layer in _layers.Values)
                {
                    ClipSlot slot = layer.TimeSynchronizedSlot;
                    if (slot != null && slot.Playable.IsValid())
                        slot.Playable.SetSpeed(slot.PlaybackSpeed);
                    layer.TimeSynchronizedSlot = null;
                }
            }
        }

        public MxAnimationDiagnosticSnapshot CreateSnapshot()
        {
            var layers = new List<MxAnimationLayerDiagnostic>();
            var fades = new List<MxAnimationFadeDiagnostic>();
            foreach (LayerRuntime layer in _layers.Values)
            {
                ResourceKey currentClipKey = layer.Current != null ? layer.Current.Key : FindDominantBlendKey(layer);
                float currentWeight = layer.Current != null ? layer.Current.Weight : FindDominantBlendWeight(layer);
                MxAnimationFadeDiagnostic fade = CreateFadeDiagnostic(layer);
                if (fade != null)
                    fades.Add(fade);

                layers.Add(new MxAnimationLayerDiagnostic(
                    layer.LayerId,
                    layer.Status,
                    currentClipKey,
                    layer.NextClipKey,
                    layer.Current != null && layer.Current.IsFallback,
                    currentWeight,
                    SumOutgoingWeight(layer),
                    CountActiveSlots(layer),
                    fade,
                    layer.LastError,
                    layer.Weight,
                    layer.TargetWeight,
                    layer.MaskStatus,
                    layer.MaskKey,
                    layer.ProfileId,
                    layer.BlendMode,
                    CreateLayerSyncState(layer),
                    layer.ActiveBlendKind == MxAnimationBlendKind.Blend1D ? layer.ActiveBlendId : string.Empty,
                    layer.BlendParameter,
                    layer.BlendWeights,
                    layer.ActiveBlendKind,
                    layer.ActiveBlendKind == MxAnimationBlendKind.Blend2D ? layer.ActiveBlendId : string.Empty,
                    layer.Blend2DParameterX,
                    layer.Blend2DParameterY,
                    layer.Blend2DWeights,
                    CreateActiveClipPlaybackDiagnostics(layer)));
            }

            return new MxAnimationDiagnosticSnapshot(
                BackendName,
                _actorId,
                _definition.SetId,
                _released ? 0 : 1,
                _playables.IsGraphValid,
                _released,
                CreateResourceDiagnostic(_defaultClip),
                CreateResourceDiagnostic(_fallbackClip),
                layers,
                fades,
                _diagnostics.RecentRequests,
                _diagnostics.RecentResourceErrors,
                CreateCacheDiagnostic());
        }

        public MxAnimationPlaybackState CapturePlaybackState()
        {
            var layers = new List<MxAnimationLayerPlaybackState>(_layers.Count);
            foreach (LayerRuntime layer in _layers.Values)
            {
                var slots = new List<MxAnimationPlaybackSlotState>(
                    (layer.Current != null ? 1 : 0) + layer.Outgoing.Count + layer.BlendSlots.Count);
                if (layer.Current != null)
                    slots.Add(CaptureSlot(layer.Current, MxAnimationPlaybackSlotRole.Current));
                for (int i = 0; i < layer.Outgoing.Count; i++)
                    slots.Add(CaptureSlot(layer.Outgoing[i], MxAnimationPlaybackSlotRole.Outgoing));
                for (int i = 0; i < layer.BlendSlots.Count; i++)
                    slots.Add(CaptureSlot(layer.BlendSlots[i], MxAnimationPlaybackSlotRole.Blend));

                layers.Add(new MxAnimationLayerPlaybackState(
                    layer.LayerId,
                    layer.Status,
                    layer.Weight,
                    layer.StartWeight,
                    layer.TargetWeight,
                    layer.WeightTransitionElapsedSeconds,
                    layer.WeightTransitionDurationSeconds,
                    layer.WeightTransitionPolicyId,
                    layer.WeightTransitionCorrelationId,
                    layer.ActiveBlendKind,
                    layer.ActiveBlendId,
                    layer.BlendParameter,
                    layer.Blend2DParameterX,
                    layer.Blend2DParameterY,
                    layer.BlendFadeElapsedSeconds,
                    layer.BlendFadeDurationSeconds,
                    slots,
                    layer.BlendWeights,
                    layer.Blend2DWeights,
                    layer.BlendNormalizedPhase));
            }

            layers.Sort((left, right) => string.CompareOrdinal(
                left != null ? left.LayerId.ToString() : string.Empty,
                right != null ? right.LayerId.ToString() : string.Empty));

            return new MxAnimationPlaybackState(BackendName, _actorId, _definition.SetId, layers);
        }

        public MxAnimationBackendResult RestorePlaybackState(MxAnimationPlaybackState state)
        {
            if (_released)
                return BackendReleasedResult(default);
            if (state == null)
                return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.InvalidRequest, default, "Playback state is null.");
            if (!string.Equals(state.BackendName, BackendName, StringComparison.Ordinal)
                || !string.Equals(state.ActorId, _actorId, StringComparison.Ordinal)
                || !string.Equals(state.AnimationSetId, _definition.SetId, StringComparison.Ordinal))
            {
                return MxAnimationBackendResult.Failed(
                    MxAnimationBackendResultCode.InvalidRequest,
                    default,
                    "Playback state backend, actor, or animation set does not match.");
            }
            if (!TryValidatePlaybackState(state, out ResourceKey invalidKey, out string validationError))
            {
                return MxAnimationBackendResult.Failed(
                    MxAnimationBackendResultCode.InvalidRequest,
                    invalidKey,
                    validationError);
            }

            for (int i = 0; i < _pendingLoads.Count; i++)
                _pendingLoads[i].Operation.Cancel();
            _pendingLoads.Clear();
            foreach (LayerRuntime existingLayer in _layers.Values)
            {
                ReleaseLayerSlots(existingLayer);
                existingLayer.Status = MxAnimationLayerStatus.Stopped;
                existingLayer.NextClipKey = default;
                existingLayer.LastError = ResourceError.None;
            }

            IReadOnlyList<MxAnimationLayerPlaybackState> layers = state.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                MxAnimationLayerPlaybackState saved = layers[i];
                if (saved == null)
                    continue;
                LayerRuntime layer = GetOrCreateLayer(saved.LayerId);
                layer.Weight = saved.Weight;
                layer.StartWeight = saved.StartWeight;
                layer.TargetWeight = saved.TargetWeight;
                layer.WeightTransitionElapsedSeconds = saved.WeightTransitionElapsedSeconds;
                layer.WeightTransitionDurationSeconds = saved.WeightTransitionDurationSeconds;
                layer.WeightTransitionPolicyId = saved.WeightTransitionPolicyId;
                layer.WeightTransitionCorrelationId = saved.WeightTransitionCorrelationId;
                layer.ActiveBlendKind = saved.ActiveBlendKind;
                layer.ActiveBlendId = saved.ActiveBlendId;
                layer.BlendParameter = saved.BlendParameter;
                layer.Blend2DParameterX = saved.Blend2DParameterX;
                layer.Blend2DParameterY = saved.Blend2DParameterY;
                layer.BlendFadeElapsedSeconds = saved.BlendFadeElapsedSeconds;
                layer.BlendFadeDurationSeconds = saved.BlendFadeDurationSeconds;
                layer.Status = saved.Status;
                layer.BlendWeights.Clear();
                for (int weightIndex = 0; weightIndex < saved.Blend1DWeights.Count; weightIndex++)
                    layer.BlendWeights.Add(saved.Blend1DWeights[weightIndex]);
                layer.Blend2DWeights.Clear();
                for (int weightIndex = 0; weightIndex < saved.Blend2DWeights.Count; weightIndex++)
                    layer.Blend2DWeights.Add(saved.Blend2DWeights[weightIndex]);
                ApplyLayerWeight(layer);

                IReadOnlyList<MxAnimationPlaybackSlotState> slots = saved.Slots;
                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    MxAnimationPlaybackSlotState slotState = slots[slotIndex];
                    if (!TryCreateRestoreSlot(slotState, out ClipSlot slot, out ResourceError error))
                    {
                        ReleaseLayerSlots(layer);
                        layer.Status = MxAnimationLayerStatus.Failed;
                        return MxAnimationBackendResult.Failed(
                            MxAnimationBackendResultCode.LoadFailed,
                            slotState.ClipKey,
                            error,
                            "Playback state clip failed to load.");
                    }

                    ConnectSlot(layer, slot, slotState.Weight);
                    slot.TargetWeight = slotState.TargetWeight;
                    slot.FadeElapsedSeconds = slotState.FadeElapsedSeconds;
                    slot.FadeDurationSeconds = slotState.FadeDurationSeconds;
                    switch (slotState.Role)
                    {
                        case MxAnimationPlaybackSlotRole.Current:
                            layer.Current = slot;
                            break;
                        case MxAnimationPlaybackSlotRole.Outgoing:
                            layer.Outgoing.Add(slot);
                            break;
                        case MxAnimationPlaybackSlotRole.Blend:
                            layer.BlendSlots.Add(slot);
                            break;
                    }
                }

                layer.BlendNormalizedPhase = ResolveRestoredBlendPhase(layer, saved.BlendNormalizedPhase);
            }

            EvaluateCurrentPose();
            return MxAnimationBackendResult.Succeeded(default, "Playback state restored.");
        }

        /// <remarks>NoAlloc; no Tick, pending load, fade, notification or authority mutation.</remarks>
        public bool TryEvaluateClipPose(MxAnimationLayerId layerId, ResourceKey expectedClipKey, double timeSeconds)
        {
            if (_released || double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d
                || !_layers.TryGetValue(layerId, out LayerRuntime layer) || layer.Current == null
                || layer.Current.Key != expectedClipKey || !layer.Current.Playable.IsValid()) return false;
            var playable = layer.Current.Playable;
            double originalTime = playable.GetTime();
            bool fireEvents = _animator != null && _animator.fireEvents;
            try
            {
                if (_animator != null) _animator.fireEvents = false;
                playable.SetTime(timeSeconds);
                _playables.Evaluate(0f);
                return true;
            }
            finally
            {
                playable.SetTime(originalTime);
                if (_animator != null) _animator.fireEvents = fireEvents;
            }
        }

        public void EvaluateCurrentPose()
        {
            if (!_released)
            {
                _playables.Evaluate(0f);
                PoseEvaluations++;
            }
        }

        private static double ResolveRestoredBlendPhase(LayerRuntime layer, double? savedPhase)
        {
            if (savedPhase.HasValue)
                return savedPhase.Value;

            // Legacy snapshots contain sampled times only. Rebuild that phase rather
            // than retaining the destination backend's unrelated continuation state.
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                ClipSlot slot = layer.BlendSlots[i];
                AnimationClip clip = slot.Handle != null ? slot.Handle.Value : null;
                if (clip == null || clip.length <= 0.0001f || !slot.Loop)
                    continue;

                double phase = slot.Playable.GetTime() / clip.length;
                if (!double.IsNaN(phase) && !double.IsInfinity(phase))
                    return phase - Math.Floor(phase);
            }

            return 0d;
        }

        private static bool TryValidatePlaybackState(
            MxAnimationPlaybackState state,
            out ResourceKey invalidKey,
            out string error)
        {
            invalidKey = default;
            error = string.Empty;
            IReadOnlyList<MxAnimationLayerPlaybackState> layers = state.Layers;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                MxAnimationLayerPlaybackState layer = layers[layerIndex];
                if (layer == null)
                {
                    error = "Playback state contains a null layer.";
                    return false;
                }

                if (layer.BlendNormalizedPhase.HasValue
                    && (double.IsNaN(layer.BlendNormalizedPhase.Value)
                        || double.IsInfinity(layer.BlendNormalizedPhase.Value)
                        || layer.BlendNormalizedPhase.Value < 0d))
                {
                    error = "Playback state contains an invalid Blend continuation phase.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < layerIndex; previousIndex++)
                {
                    if (layers[previousIndex] != null
                        && layers[previousIndex].LayerId == layer.LayerId)
                    {
                        error = "Playback state contains duplicate animation layers.";
                        return false;
                    }
                }

                int currentSlotCount = 0;
                IReadOnlyList<MxAnimationPlaybackSlotState> slots = layer.Slots;
                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    MxAnimationPlaybackSlotState slot = slots[slotIndex];
                    if (!slot.ClipKey.IsValid)
                    {
                        invalidKey = slot.ClipKey;
                        error = "Playback state contains an invalid clip key.";
                        return false;
                    }

                    switch (slot.Role)
                    {
                        case MxAnimationPlaybackSlotRole.Current:
                            currentSlotCount++;
                            if (currentSlotCount > 1)
                            {
                                invalidKey = slot.ClipKey;
                                error = "Playback state contains more than one current slot for a layer.";
                                return false;
                            }
                            break;
                        case MxAnimationPlaybackSlotRole.Outgoing:
                        case MxAnimationPlaybackSlotRole.Blend:
                            break;
                        default:
                            invalidKey = slot.ClipKey;
                            error = "Playback state contains an unknown slot role.";
                            return false;
                    }
                }
            }

            return true;
        }

        public void Release()
        {
            if (_released)
                return;

            _released = true;
            for (int i = 0; i < _pendingLoads.Count; i++)
            {
                PendingLoad pending = _pendingLoads[i];
                if (!pending.Operation.IsDone)
                    pending.Operation.Cancel();
                ReleaseCompletedClipLoad(pending);
            }
            _pendingLoads.Clear();

            for (int i = 0; i < _pendingMaskLoads.Count; i++)
                _pendingMaskLoads[i].Operation.Cancel();
            _pendingMaskLoads.Clear();

            foreach (LayerRuntime layer in _layers.Values)
            {
                ReleaseLayerSlots(layer);
                ReleaseLayerMask(layer);
            }
            _layers.Clear();

            ReleaseResidentClip(_defaultClip);
            ReleaseResidentClip(_fallbackClip);

            _playables.Destroy();

            AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.Release,
                MxAnimationLayerId.Base,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                string.Empty,
                "Backend released."));
        }

        public void Dispose()
        {
            Release();
        }

        private void InitializeConfiguredLayers()
        {
            for (int i = 0; i < _definition.Layers.Count; i++)
            {
                MxAnimationLayerDefinition layer = _definition.Layers[i];
                if (layer == null)
                    continue;

                GetOrCreateLayer(layer.LayerId);
            }
        }

        private ResidentClip LoadResidentClip(string role, ResourceKey key)
        {
            var resident = new ResidentClip(role, key);
            if (!key.IsValid)
                return resident;

            resident.Status = MxAnimationResourceLoadStatus.Loading;
            IResourceOperation<ResourceHandle<AnimationClip>> operation = _resourceManager.LoadAsync<AnimationClip>(key);
            var pending = new PendingLoad(LoadPurpose.Resident, MxAnimationRequestKind.Play, default, key, key, false, 0f, 1f, false, 0f, string.Empty, operation, resident);
            if (operation.IsDone)
            {
                CompleteResidentLoad(pending);
                return resident;
            }

            _pendingLoads.Add(pending);
            return resident;
        }

        private MxAnimationBackendResult BeginClipLoad(ClipRequest request, MxAnimationRequestKind kind, bool fallbackAttempt)
        {
            if (TryCreateResidentSlot(request, fallbackAttempt, out ClipSlot residentSlot))
            {
                _stateCache.RecordHit();
                return ApplyLoadedSlot(request, kind, residentSlot, fallbackAttempt);
            }

            _stateCache.RecordMiss();
            IResourceOperation<ResourceHandle<AnimationClip>> operation = _resourceManager.LoadAsync<AnimationClip>(request.ClipKey);
            var pending = new PendingLoad(
                LoadPurpose.Request,
                kind,
                request.LayerId,
                request.RequestedClipKey,
                request.ClipKey,
                fallbackAttempt,
                request.FadeDurationSeconds,
                request.PlaybackSpeed,
                request.Loop,
                request.StartOffsetSeconds,
                request.CorrelationId,
                operation,
                null,
                request.AllowFallback);

            if (operation.IsDone)
                return CompleteRequestLoad(pending);

            _pendingLoads.Add(pending);
            AddRequest(new MxAnimationRequestDiagnostic(
                kind,
                request.LayerId,
                request.RequestedClipKey,
                request.ClipKey,
                fallbackAttempt,
                MxAnimationBackendResultCode.Queued,
                request.CorrelationId,
                "Clip load queued."));
            return MxAnimationBackendResult.Queued(request.ClipKey, "Clip load queued.");
        }

        private void InvalidatePendingClipLoads(MxAnimationLayerId layerId)
        {
            for (int i = _pendingLoads.Count - 1; i >= 0; i--)
            {
                PendingLoad pending = _pendingLoads[i];
                if (pending.Purpose != LoadPurpose.Request || pending.LayerId != layerId)
                    continue;

                // Cancellation may be advisory. Keep unfinished operations so their
                // eventual successful handles can be reclaimed without applying playback.
                pending.Invalidated = true;
                if (!pending.Operation.IsDone)
                    pending.Operation.Cancel();
                if (!pending.Operation.IsDone)
                    continue;

                _pendingLoads.RemoveAt(i);
                ReleaseCompletedClipLoad(pending);
            }
        }

        private void ReleaseCompletedClipLoad(PendingLoad pending)
        {
            if (!pending.Operation.IsDone)
                return;

            ResourceLoadResult<ResourceHandle<AnimationClip>> result = pending.Operation.Result;
            if (result.Success && result.Value != null && !result.Value.IsReleased)
                _resourceManager.Release(result.Value);
        }

        private void ProcessPendingLoads()
        {
            for (int i = _pendingLoads.Count - 1; i >= 0; i--)
            {
                PendingLoad pending = _pendingLoads[i];
                if (!pending.Operation.IsDone)
                    continue;

                _pendingLoads.RemoveAt(i);
                if (pending.Invalidated)
                {
                    ReleaseCompletedClipLoad(pending);
                    continue;
                }
                if (pending.Purpose == LoadPurpose.Resident)
                    CompleteResidentLoad(pending);
                else
                {
                    MxAnimationBackendResult result = CompleteRequestLoad(pending);
                    // Immediate failures are returned to the caller. Queued required clips
                    // must also surface their eventual failure instead of silently staying idle.
                    if (!pending.AllowFallback && !result.Success)
                        Debug.LogError("[MxAnimation][RequiredClipMissing] actor=" + _actorId
                            + " clip=" + pending.RequestedKey + " correlation=" + pending.CorrelationId
                            + " error=" + result.ResourceError.Message);
                }
            }
        }

        private void ProcessPendingMaskLoads()
        {
            for (int i = _pendingMaskLoads.Count - 1; i >= 0; i--)
            {
                PendingMaskLoad pending = _pendingMaskLoads[i];
                if (!pending.Operation.IsDone)
                    continue;

                _pendingMaskLoads.RemoveAt(i);
                CompleteMaskLoad(pending);
            }
        }

        private void CompleteMaskLoad(PendingMaskLoad pending)
        {
            if (!_layers.TryGetValue(pending.LayerId, out LayerRuntime layer))
            {
                ResourceLoadResult<ResourceHandle<AvatarMask>> orphaned = pending.Operation.Result;
                if (orphaned.Success)
                    _resourceManager.Release(orphaned.Value);
                return;
            }

            ResourceLoadResult<ResourceHandle<AvatarMask>> result = pending.Operation.Result;
            if (!result.Success)
            {
                layer.MaskStatus = MxAnimationLayerMaskStatus.Failed;
                layer.LastError = result.Error;
                TrackResourceError(result.Error);
                return;
            }

            layer.MaskHandle = result.Value;
            layer.MaskStatus = MxAnimationLayerMaskStatus.Loaded;
            layer.LastError = ResourceError.None;
            _playables.SetLayerMask(layer.LayerMixer, result.Value.Value);
        }

        private void CompleteResidentLoad(PendingLoad pending)
        {
            ResourceLoadResult<ResourceHandle<AnimationClip>> result = pending.Operation.Result;
            ResidentClip resident = pending.Resident;
            if (result.Success)
            {
                resident.Handle = result.Value;
                resident.Status = MxAnimationResourceLoadStatus.Loaded;
                resident.LastError = ResourceError.None;
                return;
            }

            resident.Status = MxAnimationResourceLoadStatus.Failed;
            resident.LastError = result.Error;
            TrackResourceError(result.Error);
        }

        private MxAnimationBackendResult CompleteRequestLoad(PendingLoad pending)
        {
            ResourceLoadResult<ResourceHandle<AnimationClip>> result = pending.Operation.Result;
            if (result.Success)
            {
                var slot = CreateSlot(pending.LoadKey, result.Value, true, pending.FallbackAttempt, pending.PlaybackSpeed, pending.Loop, pending.StartOffsetSeconds);
                var request = new ClipRequest(
                    pending.LayerId,
                    pending.RequestedKey,
                    pending.LoadKey,
                    pending.FadeDurationSeconds,
                    pending.PlaybackSpeed,
                    pending.Loop,
                    pending.StartOffsetSeconds,
                    pending.CorrelationId);
                return ApplyLoadedSlot(request, pending.RequestKind, slot, pending.FallbackAttempt);
            }

            TrackResourceError(result.Error);
            LayerRuntime layer = GetOrCreateLayer(pending.LayerId);
            layer.LastError = result.Error;

            if (!pending.FallbackAttempt)
                return TryFallbackAfterFailure(pending, result.Error);

            layer.Status = MxAnimationLayerStatus.Failed;
            layer.NextClipKey = default;
            AddRequest(new MxAnimationRequestDiagnostic(
                pending.RequestKind,
                pending.LayerId,
                pending.RequestedKey,
                pending.LoadKey,
                true,
                MxAnimationBackendResultCode.FallbackFailed,
                pending.CorrelationId,
                "Fallback clip failed to load."));
            return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.FallbackFailed, pending.LoadKey, result.Error, "Fallback clip failed to load.");
        }

        private MxAnimationBackendResult TryFallbackAfterFailure(PendingLoad failed, ResourceError error)
        {
            ResourceKey fallbackKey = _definition.FallbackClip;
            if (!failed.AllowFallback || !fallbackKey.IsValid || fallbackKey == failed.LoadKey)
            {
                string message = failed.AllowFallback
                    ? "Clip failed and no fallback clip is available."
                    : "Required animation clip failed to load; fallback is disabled.";
                LayerRuntime layer = GetOrCreateLayer(failed.LayerId);
                layer.Status = MxAnimationLayerStatus.Failed;
                layer.NextClipKey = default;
                AddRequest(new MxAnimationRequestDiagnostic(
                    failed.RequestKind,
                    failed.LayerId,
                    failed.RequestedKey,
                    failed.LoadKey,
                    false,
                    MxAnimationBackendResultCode.LoadFailed,
                    failed.CorrelationId,
                    message));
                return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.LoadFailed, failed.LoadKey, error, message);
            }

            var fallbackRequest = new ClipRequest(
                failed.LayerId,
                failed.RequestedKey,
                fallbackKey,
                failed.FadeDurationSeconds,
                failed.PlaybackSpeed,
                failed.Loop,
                failed.StartOffsetSeconds,
                failed.CorrelationId);

            return BeginClipLoad(fallbackRequest, failed.RequestKind, true);
        }

        private MxAnimationBackendResult ApplyLoadedSlot(ClipRequest request, MxAnimationRequestKind kind, ClipSlot slot, bool usedFallback)
        {
            LayerRuntime layer = GetOrCreateLayer(request.LayerId);
            if (kind == MxAnimationRequestKind.CrossFade)
                CrossFadeToSlot(layer, slot, request.FadeDurationSeconds);
            else
                PlaySlot(layer, slot);

            layer.LastError = ResourceError.None;
            layer.NextClipKey = default;
            AddRequest(new MxAnimationRequestDiagnostic(
                kind,
                request.LayerId,
                request.RequestedClipKey,
                slot.Key,
                usedFallback,
                MxAnimationBackendResultCode.Success,
                request.CorrelationId,
                usedFallback ? "Fallback clip is playing." : "Clip is playing."));
            return MxAnimationBackendResult.Succeeded(slot.Key, usedFallback ? "Fallback clip is playing." : "Clip is playing.");
        }

        private bool TryCreateResidentSlot(ClipRequest request, bool fallbackAttempt, out ClipSlot slot)
        {
            ResidentClip resident = null;
            if (_defaultClip != null && _defaultClip.Status == MxAnimationResourceLoadStatus.Loaded && _defaultClip.Key == request.ClipKey)
                resident = _defaultClip;
            if (_fallbackClip != null && _fallbackClip.Status == MxAnimationResourceLoadStatus.Loaded && _fallbackClip.Key == request.ClipKey)
                resident = _fallbackClip;

            if (resident == null || resident.Handle == null || resident.Handle.IsReleased)
            {
                slot = null;
                return false;
            }

            slot = CreateSlot(request.ClipKey, resident.Handle, false, fallbackAttempt || resident == _fallbackClip, request.PlaybackSpeed, request.Loop, request.StartOffsetSeconds);
            return true;
        }

        private bool TryCreateBlendSlot(
            BlendClipWeight weight,
            string invalidKeyMessage,
            out ClipSlot slot,
            out ResourceError error)
        {
            slot = null;
            error = ResourceError.None;

            if (!weight.ClipKey.IsValid)
            {
                error = new ResourceError(ResourceErrorCode.InvalidKey, weight.ClipKey, string.Empty, invalidKeyMessage ?? "Blend point clip key is invalid.");
                return false;
            }

            var residentRequest = new ClipRequest(
                MxAnimationLayerId.Base,
                weight.ClipKey,
                weight.ClipKey,
                0f,
                weight.PlaybackSpeed,
                weight.Loop,
                0f,
                string.Empty);
            if (TryCreateResidentSlot(residentRequest, false, out slot))
                return true;

            ResourceLoadResult<ResourceHandle<AnimationClip>> result = _resourceManager.Load<AnimationClip>(weight.ClipKey);
            if (!result.Success)
            {
                error = result.Error;
                return false;
            }

            slot = CreateSlot(weight.ClipKey, result.Value, true, false, weight.PlaybackSpeed, weight.Loop, 0f);
            return true;
        }

        private ClipSlot CreateSlot(
            ResourceKey key,
            ResourceHandle<AnimationClip> handle,
            bool ownsHandle,
            bool isFallback,
            float playbackSpeed,
            bool loop,
            float startOffsetSeconds)
        {
            AnimationClipPlayable playable = _playables.CreateClipPlayable(handle.Value, playbackSpeed, startOffsetSeconds);
            return new ClipSlot(key, handle, playable, ownsHandle, isFallback, playbackSpeed, loop);
        }

        private static MxAnimationPlaybackSlotState CaptureSlot(
            ClipSlot slot,
            MxAnimationPlaybackSlotRole role)
        {
            return new MxAnimationPlaybackSlotState(
                role,
                slot.Key,
                slot.Playable.IsValid() ? slot.Playable.GetTime() : 0d,
                slot.PlaybackSpeed,
                slot.Loop,
                slot.IsFallback,
                slot.Weight,
                slot.TargetWeight,
                slot.FadeElapsedSeconds,
                slot.FadeDurationSeconds);
        }

        private bool TryCreateRestoreSlot(
            MxAnimationPlaybackSlotState state,
            out ClipSlot slot,
            out ResourceError error)
        {
            slot = null;
            error = ResourceError.None;
            if (!state.ClipKey.IsValid)
            {
                error = new ResourceError(
                    ResourceErrorCode.InvalidKey,
                    state.ClipKey,
                    string.Empty,
                    "Playback state clip key is invalid.");
                return false;
            }

            var request = new ClipRequest(
                MxAnimationLayerId.Base,
                state.ClipKey,
                state.ClipKey,
                0f,
                state.PlaybackSpeed,
                state.Loop,
                (float)state.TimeSeconds,
                string.Empty);
            if (TryCreateResidentSlot(request, state.Fallback, out slot))
                return true;

            ResourceLoadResult<ResourceHandle<AnimationClip>> result =
                _resourceManager.Load<AnimationClip>(state.ClipKey);
            if (!result.Success)
            {
                error = result.Error;
                return false;
            }

            slot = CreateSlot(
                state.ClipKey,
                result.Value,
                ownsHandle: true,
                isFallback: state.Fallback,
                playbackSpeed: state.PlaybackSpeed,
                loop: state.Loop,
                startOffsetSeconds: (float)state.TimeSeconds);
            return true;
        }

        private void PlaySlot(LayerRuntime layer, ClipSlot slot)
        {
            ReleaseLayerSlots(layer);
            ConnectSlot(layer, slot, 1f);
            layer.Current = slot;
            layer.Status = MxAnimationLayerStatus.Playing;
        }

        private bool TryReuseCurrentForPlay(LayerRuntime layer, ClipRequest request, out MxAnimationBackendResult result)
        {
            result = default;
            if (layer.Current == null || layer.Current.Key != request.ClipKey)
                return false;

            UpdateSlotPlayback(layer.Current, request.PlaybackSpeed, request.Loop, request.StartOffsetSeconds, true);
            ReleaseOutgoingSlots(layer);
            ReleaseBlendSlots(layer);
            SetSlotWeight(layer, layer.Current, 1f);
            layer.Status = MxAnimationLayerStatus.Playing;
            layer.LastError = ResourceError.None;
            layer.NextClipKey = default;
            _stateCache.RecordHit();
            AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.Play,
                request.LayerId,
                request.RequestedClipKey,
                request.ClipKey,
                false,
                MxAnimationBackendResultCode.Success,
                request.CorrelationId,
                "Clip is already current."));
            result = MxAnimationBackendResult.Succeeded(request.ClipKey, "Clip is already current.");
            return true;
        }

        private void CrossFadeToSlot(LayerRuntime layer, ClipSlot slot, float fadeDuration)
        {
            fadeDuration = Math.Max(0f, fadeDuration);
            if (fadeDuration <= 0f || (layer.Current == null && layer.BlendSlots.Count == 0))
            {
                ReleaseLayerSlots(layer);
                ConnectSlot(layer, slot, 1f);
                layer.Current = slot;
                layer.Status = MxAnimationLayerStatus.Playing;
                return;
            }

            if (layer.BlendSlots.Count > 0)
            {
                ReleaseOutgoingSlots(layer);
                MoveBlendSlotsToOutgoing(layer, fadeDuration);
            }
            MoveCurrentToOutgoing(layer, fadeDuration);
            ConnectSlot(layer, slot, 0f);
            slot.FadeDurationSeconds = fadeDuration;
            layer.Current = slot;
            layer.Status = MxAnimationLayerStatus.CrossFading;
        }

        private void MoveBlendSlotsToOutgoing(LayerRuntime layer, float fadeDuration)
        {
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                ClipSlot outgoing = layer.BlendSlots[i];
                if (outgoing.Weight <= Epsilon)
                {
                    DetachAndReleaseSlot(layer, outgoing);
                    continue;
                }

                outgoing.FadeDurationSeconds = fadeDuration;
                outgoing.FadeElapsedSeconds = 0f;
                outgoing.TargetWeight = outgoing.Weight;
                layer.Outgoing.Add(outgoing);
            }
            ClearBlendState(layer);
        }

        private void MoveCurrentToOutgoing(LayerRuntime layer, float fadeDuration)
        {
            if (layer.Current == null)
                return;

            ClipSlot outgoing = layer.Current;
            if (outgoing.Weight <= Epsilon)
            {
                DetachAndReleaseSlot(layer, outgoing);
                layer.Current = null;
                return;
            }
            outgoing.FadeDurationSeconds = Math.Max(0f, fadeDuration);
            outgoing.FadeElapsedSeconds = 0f;
            outgoing.Weight = Math.Max(0f, outgoing.Weight);
            outgoing.TargetWeight = outgoing.Weight;
            layer.Outgoing.Add(outgoing);
            layer.Current = null;
        }

        private void TickLayer(LayerRuntime layer, float deltaTime)
        {
            TickLayerWeight(layer, deltaTime);

            if (layer.Current != null && layer.Status == MxAnimationLayerStatus.CrossFading)
            {
                layer.Current.FadeElapsedSeconds += deltaTime;
                float progress = CalculateProgress(layer.Current.FadeElapsedSeconds, layer.Current.FadeDurationSeconds);
                SetSlotWeight(layer, layer.Current, progress);
            }

            for (int i = layer.Outgoing.Count - 1; i >= 0; i--)
            {
                ClipSlot outgoing = layer.Outgoing[i];
                outgoing.FadeElapsedSeconds += deltaTime;
                float progress = CalculateProgress(outgoing.FadeElapsedSeconds, outgoing.FadeDurationSeconds);
                float startWeight = outgoing.TargetWeight > Epsilon
                    ? outgoing.TargetWeight
                    : 1f; // Old playback snapshots stored no outgoing start weight.
                SetSlotWeight(layer, outgoing, startWeight * (1f - progress));
                if (outgoing.Weight > 0f)
                    continue;

                layer.Outgoing.RemoveAt(i);
                DetachAndReleaseSlot(layer, outgoing);
            }

            if (layer.BlendFadeDurationSeconds > 0f)
            {
                layer.BlendFadeElapsedSeconds += deltaTime;
                float progress = CalculateProgress(layer.BlendFadeElapsedSeconds, layer.BlendFadeDurationSeconds);
                for (int i = 0; i < layer.BlendSlots.Count; i++)
                    SetSlotWeight(layer, layer.BlendSlots[i], layer.BlendSlots[i].TargetWeight * progress);
                if (progress >= 1f)
                {
                    layer.BlendFadeDurationSeconds = 0f;
                    layer.BlendFadeElapsedSeconds = 0f;
                }
            }

            if (layer.Current != null && layer.Outgoing.Count == 0 && layer.Status != MxAnimationLayerStatus.Loading)
            {
                SetSlotWeight(layer, layer.Current, 1f);
                layer.Status = MxAnimationLayerStatus.Playing;
            }
            else if (layer.BlendSlots.Count > 0 && layer.Status != MxAnimationLayerStatus.Loading)
            {
                layer.Status = MxAnimationLayerStatus.Playing;
            }
            else if (layer.Current == null && layer.Outgoing.Count == 0 && layer.Status != MxAnimationLayerStatus.Loading && layer.Status != MxAnimationLayerStatus.Failed)
            {
                layer.Status = MxAnimationLayerStatus.Stopped;
            }

            SynchronizeBlendSlotPhases(layer, deltaTime);
        }

        private void SynchronizeBlendSlotPhases(LayerRuntime layer, float deltaTime)
        {
            if (layer == null || layer.BlendSlots.Count == 0)
                return;

            double weightedCycleRate = 0d;
            double totalWeight = 0d;
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                ClipSlot slot = layer.BlendSlots[i];
                AnimationClip clip = slot.Handle != null ? slot.Handle.Value : null;
                double weight = Math.Max(0f, slot.TargetWeight);
                if (clip == null || clip.length <= 0.0001f || weight <= 0d || !slot.Loop)
                    continue;

                weightedCycleRate += weight * (NormalizeSpeed(slot.PlaybackSpeed) / clip.length);
                totalWeight += weight;
            }

            if (totalWeight <= Epsilon || weightedCycleRate <= Epsilon)
                return;

            double cycleRate = weightedCycleRate / totalWeight;
            double phase = layer.BlendNormalizedPhase - Math.Floor(layer.BlendNormalizedPhase);
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                ClipSlot slot = layer.BlendSlots[i];
                AnimationClip clip = slot.Handle != null ? slot.Handle.Value : null;
                if (clip == null || clip.length <= 0.0001f || !slot.Loop)
                    continue;

                slot.Playable.SetTime(phase * clip.length);
                slot.Playable.SetSpeed(cycleRate * clip.length);
                slot.Playable.SetDone(false);
            }

            layer.BlendNormalizedPhase = phase + (cycleRate * Math.Max(0f, deltaTime));
        }


        private LayerRuntime GetOrCreateLayer(MxAnimationLayerId layerId)
        {
            if (_layers.TryGetValue(layerId, out LayerRuntime layer))
                return layer;

            MxAnimationLayerMixerHandle layerMixer = _playables.AddLayerMixer();

            MxAnimationLayerDefinition definition = ResolveLayerDefinition(layerId);
            float weight = definition != null ? definition.DefaultWeight : 1f;
            layer = new LayerRuntime(
                layerId,
                layerMixer,
                weight,
                definition != null ? definition.ProfileId : string.Empty,
                definition != null ? definition.BlendMode : MxAnimationLayerBlendMode.Override,
                definition != null ? definition.AvatarMaskKey : default);
            _layers.Add(layerId, layer);
            ApplyLayerWeight(layer);
            ApplyLayerBlendMode(layer);
            BeginMaskLoad(layer);
            return layer;
        }

        private MxAnimationLayerDefinition ResolveLayerDefinition(MxAnimationLayerId layerId)
        {
            return _definition.TryFindLayerDefinition(layerId, out MxAnimationLayerDefinition layer)
                ? layer
                : null;
        }

        private void TickLayerWeight(LayerRuntime layer, float deltaTime)
        {
            if (layer.WeightTransitionDurationSeconds <= 0f)
                return;

            layer.WeightTransitionElapsedSeconds += deltaTime;
            float progress = CalculateProgress(layer.WeightTransitionElapsedSeconds, layer.WeightTransitionDurationSeconds);
            layer.Weight = Mathf.Lerp(layer.StartWeight, layer.TargetWeight, progress);
            ApplyLayerWeight(layer);
            if (progress < 1f)
                return;

            layer.Weight = layer.TargetWeight;
            layer.WeightTransitionDurationSeconds = 0f;
            layer.WeightTransitionElapsedSeconds = 0f;
            ApplyLayerWeight(layer);
        }

        private void ApplyLayerWeight(LayerRuntime layer)
        {
            layer.Weight = Clamp01(layer.Weight);
            _playables.SetLayerWeight(layer.LayerMixer, layer.Weight);
        }

        private void ApplyLayerBlendMode(LayerRuntime layer)
        {
            _playables.SetLayerAdditive(layer.LayerMixer, layer.BlendMode == MxAnimationLayerBlendMode.Additive);
        }

        private void BeginMaskLoad(LayerRuntime layer)
        {
            if (!layer.MaskKey.IsValid)
            {
                layer.MaskStatus = MxAnimationLayerMaskStatus.NotConfigured;
                return;
            }

            if (!string.Equals(layer.MaskKey.TypeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal))
            {
                layer.MaskStatus = MxAnimationLayerMaskStatus.Failed;
                layer.LastError = new ResourceError(
                    ResourceErrorCode.TypeMismatch,
                    layer.MaskKey,
                    string.Empty,
                    "Animation layer AvatarMask key must use typeId " + ResourceTypeIds.AvatarMask + ".");
                TrackResourceError(layer.LastError);
                return;
            }

            layer.MaskStatus = MxAnimationLayerMaskStatus.Loading;
            IResourceOperation<ResourceHandle<AvatarMask>> operation = _resourceManager.LoadAsync<AvatarMask>(layer.MaskKey);
            var pending = new PendingMaskLoad(layer.LayerId, operation);
            if (operation.IsDone)
            {
                CompleteMaskLoad(pending);
                return;
            }

            _pendingMaskLoads.Add(pending);
        }

        private void ConnectSlot(LayerRuntime layer, ClipSlot slot, float weight)
        {
            slot.InputIndex = _playables.ConnectClip(layer.LayerMixer, slot.Playable, weight);
            SetSlotWeight(layer, slot, weight);
        }

        private void SetSlotWeight(LayerRuntime layer, ClipSlot slot, float weight)
        {
            slot.Weight = Clamp01(weight);
            _playables.SetClipWeight(layer.LayerMixer, slot.InputIndex, slot.Weight);
        }

        private static void UpdateSlotPlayback(ClipSlot slot, float playbackSpeed, bool loop, float startOffsetSeconds, bool resetTime)
        {
            if (slot == null)
                return;

            slot.PlaybackSpeed = playbackSpeed;
            slot.Loop = loop;
            slot.Playable.SetSpeed(playbackSpeed);
            if (resetTime)
                slot.Playable.SetTime(Math.Max(0f, startOffsetSeconds));
        }

        private void ReleaseLayerSlots(LayerRuntime layer)
        {
            ReleaseCurrentSlot(layer);
            ReleaseOutgoingSlots(layer);
            ReleaseBlendSlots(layer);
            layer.NextClipKey = default;
        }

        private void ReleaseCurrentSlot(LayerRuntime layer)
        {
            if (layer.Current == null)
                return;

            DetachAndReleaseSlot(layer, layer.Current);
            layer.Current = null;
        }

        private void ReleaseOutgoingSlots(LayerRuntime layer)
        {
            for (int i = layer.Outgoing.Count - 1; i >= 0; i--)
                DetachAndReleaseSlot(layer, layer.Outgoing[i]);
            layer.Outgoing.Clear();
        }

        private void ReleaseBlendSlots(LayerRuntime layer)
        {
            for (int i = layer.BlendSlots.Count - 1; i >= 0; i--)
                DetachAndReleaseSlot(layer, layer.BlendSlots[i]);
            ClearBlendState(layer);
        }

        private static void ClearBlendState(LayerRuntime layer)
        {
            layer.BlendSlots.Clear();
            layer.BlendWeights.Clear();
            layer.Blend2DWeights.Clear();
            layer.ActiveBlendId = string.Empty;
            layer.ActiveBlendKind = MxAnimationBlendKind.None;
            layer.BlendParameter = default;
            layer.Blend2DParameterX = default;
            layer.Blend2DParameterY = default;
            layer.BlendFadeElapsedSeconds = 0f;
            layer.BlendFadeDurationSeconds = 0f;
        }

        private static float ResolveBlendFadeDuration(float requestFadeDurationSeconds, float defaultFadeDurationSeconds)
        {
            return requestFadeDurationSeconds >= 0f
                ? requestFadeDurationSeconds
                : Math.Max(0f, defaultFadeDurationSeconds);
        }

        private void ReleaseStaleBlendSlots(LayerRuntime layer, IReadOnlyList<BlendClipWeight> weights)
        {
            for (int i = layer.BlendSlots.Count - 1; i >= 0; i--)
            {
                ClipSlot slot = layer.BlendSlots[i];
                if (ContainsBlendWeight(weights, slot.Key))
                    continue;

                layer.BlendSlots.RemoveAt(i);
                DetachAndReleaseSlot(layer, slot);
            }
        }

        private void DetachAndReleaseSlot(LayerRuntime layer, ClipSlot slot)
        {
            if (slot == null)
                return;

            if (slot.InputIndex >= 0)
            {
                _playables.DisconnectClip(layer.LayerMixer, slot.InputIndex);
                slot.InputIndex = -1;
            }

            _playables.DestroyClipPlayable(slot.Playable);

            if (slot.OwnsHandle && slot.Handle != null && !slot.Handle.IsReleased)
                _resourceManager.Release(slot.Handle);
        }

        private void ReleaseResidentClip(ResidentClip resident)
        {
            if (resident == null)
                return;

            if (resident.Handle != null && !resident.Handle.IsReleased)
                _resourceManager.Release(resident.Handle);
            resident.Handle = null;
            resident.Status = resident.Key.IsValid ? MxAnimationResourceLoadStatus.Released : MxAnimationResourceLoadStatus.None;
        }

        private void ReleaseLayerMask(LayerRuntime layer)
        {
            if (layer == null)
                return;

            if (layer.MaskHandle != null && !layer.MaskHandle.IsReleased)
                _resourceManager.Release(layer.MaskHandle);
            layer.MaskHandle = null;
            layer.MaskStatus = layer.MaskKey.IsValid ? MxAnimationLayerMaskStatus.Released : MxAnimationLayerMaskStatus.NotConfigured;
        }

        private bool TryResolvePlay(MxAnimationPlayRequest request, out ClipRequest resolved, out string error)
        {
            resolved = default;
            if (request == null)
            {
                error = "Play request is null.";
                return false;
            }

            ResourceKey clipKey = request.ClipKey;
            MxAnimationLayerId layerId = request.LayerId;
            float speed = NormalizeSpeed(request.PlaybackSpeed);
            bool loop = request.Loop;
            if (!clipKey.IsValid && _definition.TryFindBinding(request.BindingId, out MxAnimationActionBinding binding))
            {
                clipKey = binding.Clip;
                layerId = binding.Layer;
                speed = NormalizeSpeed(binding.PlaybackSpeed);
                loop = binding.Loop;
            }

            if (!clipKey.IsValid && _definition.DefaultClip.IsValid)
                clipKey = _definition.DefaultClip;

            if (!clipKey.IsValid)
            {
                error = "Play request did not resolve to a valid clip key.";
                return false;
            }

            resolved = new ClipRequest(layerId, clipKey, clipKey, 0f, speed, loop, request.StartOffsetSeconds, request.CorrelationId);
            error = string.Empty;
            return true;
        }

        private bool TryResolveCrossFade(MxAnimationCrossFadeRequest request, out ClipRequest resolved, out string error)
        {
            resolved = default;
            if (request == null)
            {
                error = "Crossfade request is null.";
                return false;
            }

            ResourceKey clipKey = request.ClipKey;
            MxAnimationLayerId layerId = request.LayerId;
            float speed = NormalizeSpeed(request.PlaybackSpeed);
            bool loop = request.Loop;
            if (!clipKey.IsValid && _definition.TryFindBinding(request.BindingId, out MxAnimationActionBinding binding))
            {
                clipKey = binding.Clip;
                layerId = binding.Layer;
                speed = NormalizeSpeed(binding.PlaybackSpeed);
                loop = binding.Loop;
            }

            if (!clipKey.IsValid && request.AllowFallback && _definition.DefaultClip.IsValid)
                clipKey = _definition.DefaultClip;

            if (!clipKey.IsValid)
            {
                error = "Crossfade request did not resolve to a valid clip key.";
                return false;
            }

            resolved = new ClipRequest(layerId, clipKey, clipKey, Math.Max(0f, request.FadeDurationSeconds), speed, loop, request.TargetStartOffsetSeconds, request.CorrelationId, request.AllowFallback);
            error = string.Empty;
            return true;
        }

        private MxAnimationLayerId ResolveStopLayer(MxAnimationStopRequest request)
        {
            if (request == null)
                return MxAnimationLayerId.Base;

            if (request.BindingId > 0 && _definition.TryFindBinding(request.BindingId, out MxAnimationActionBinding binding))
                return binding.Layer;

            return request.LayerId;
        }

        private MxAnimationBackendCacheDiagnostic CreateCacheDiagnostic()
        {
            return _stateCache.CreateDiagnostic(
                CountResidentClips(),
                CountCachedPlayableSlots(),
                CountActivePlayableSlots(),
                _resourceManager.CreateDebugSnapshot());
        }

        private MxAnimationBackendResult InvalidRequest(MxAnimationRequestKind kind, MxAnimationLayerId layerId, ResourceKey clipKey, string message)
        {
            AddRequest(new MxAnimationRequestDiagnostic(
                kind,
                layerId,
                clipKey,
                clipKey,
                false,
                MxAnimationBackendResultCode.InvalidRequest,
                string.Empty,
                message));
            return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.InvalidRequest, clipKey, message);
        }

        private MxAnimationBackendResult BackendReleasedResult(ResourceKey clipKey)
        {
            return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.BackendReleased, clipKey, "Backend is released.");
        }

        private void TrackResourceError(ResourceError error)
        {
            if (error.IsNone)
                return;

            _diagnostics.TrackResourceError(error);
        }

        private void AddRequest(MxAnimationRequestDiagnostic diagnostic)
        {
            _diagnostics.AddRequest(diagnostic);
        }

        private static float CalculateProgress(float elapsed, float duration)
        {
            if (duration <= 0f)
                return 1f;

            return Clamp01(elapsed / duration);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            if (value >= 1f)
                return 1f;
            return value;
        }

        private static float NormalizeSpeed(float speed)
        {
            return Math.Abs(speed) < 0.0001f ? 1f : speed;
        }

        private static float NormalizePlaybackSpeedMultiplier(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
                return 1f;
            return Math.Max(0f, speed);
        }

        private float ResolvePlaybackSpeed(ResourceKey clipKey, float fallback)
        {
            return _playbackSpeedOverrides.TryGetValue(clipKey, out float playbackSpeed)
                ? playbackSpeed
                : fallback;
        }

        private MxAnimationBlend2DDefinition ApplyBlend2DPointCoordinateOverrides(MxAnimationBlend2DDefinition blend)
        {
            if (blend == null || _blend2DPointCoordinateOverrides.Count == 0)
                return blend;

            var points = new List<MxAnimationBlend2DPoint>(blend.Points.Count);
            bool changed = false;
            for (int i = 0; i < blend.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = blend.Points[i];
                if (point == null)
                    continue;

                if (_blend2DPointCoordinateOverrides.TryGetValue(
                    CreateBlend2DPointOverrideKey(blend.BlendId, point.ClipKey),
                    out Blend2DPointCoordinateOverride coordinate))
                {
                    points.Add(new MxAnimationBlend2DPoint(
                        coordinate.X,
                        coordinate.Y,
                        point.ClipKey,
                        point.PlaybackSpeed,
                        point.Loop));
                    changed = true;
                    continue;
                }

                points.Add(point);
            }

            return changed
                ? new MxAnimationBlend2DDefinition(
                    blend.BlendId,
                    blend.ParameterXId,
                    blend.ParameterYId,
                    blend.LayerId,
                    points,
                    blend.ParameterXScale,
                    blend.ParameterYScale,
                    blend.FadeDurationSeconds)
                : blend;
        }

        private static string CreateBlend2DPointOverrideKey(string blendId, ResourceKey clipKey)
        {
            return (blendId ?? string.Empty) + "|" + clipKey.ToString();
        }

        private static int CountActiveSlots(LayerRuntime layer)
        {
            int count = layer.Current != null ? 1 : 0;
            count += layer.Outgoing.Count;
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                if (layer.BlendSlots[i].Weight > 0f)
                    count++;
            }

            return count;
        }

        private static List<MxAnimationClipPlaybackDiagnostic> CreateActiveClipPlaybackDiagnostics(LayerRuntime layer)
        {
            var diagnostics = new List<MxAnimationClipPlaybackDiagnostic>();
            if (layer == null)
                return diagnostics;

            if (layer.Current != null)
                AddClipPlaybackDiagnostic(diagnostics, layer.Current, blendSlot: false);
            for (int i = 0; i < layer.Outgoing.Count; i++)
                AddClipPlaybackDiagnostic(diagnostics, layer.Outgoing[i], blendSlot: false);
            for (int i = 0; i < layer.BlendSlots.Count; i++)
                AddClipPlaybackDiagnostic(diagnostics, layer.BlendSlots[i], blendSlot: true);
            return diagnostics;
        }

        private static void AddClipPlaybackDiagnostic(List<MxAnimationClipPlaybackDiagnostic> diagnostics, ClipSlot slot, bool blendSlot)
        {
            if (slot == null || !slot.Key.IsValid)
                return;

            float normalizedTime = 0f;
            AnimationClip clip = slot.Handle != null ? slot.Handle.Value : null;
            double duration = clip != null ? clip.length : slot.Playable.GetDuration();
            if (duration > 0.0001d && !double.IsNaN(duration) && !double.IsInfinity(duration))
                normalizedTime = (float)(slot.Playable.GetTime() / duration);

            diagnostics.Add(new MxAnimationClipPlaybackDiagnostic(
                slot.Key,
                slot.Weight,
                normalizedTime,
                slot.PlaybackSpeed,
                slot.Loop,
                blendSlot,
                slot.IsFallback));
        }

        private int CountResidentClips()
        {
            int count = 0;
            if (_defaultClip != null && _defaultClip.Status == MxAnimationResourceLoadStatus.Loaded)
                count++;
            if (_fallbackClip != null && _fallbackClip.Status == MxAnimationResourceLoadStatus.Loaded)
                count++;
            return count;
        }

        private int CountCachedPlayableSlots()
        {
            int count = 0;
            foreach (LayerRuntime layer in _layers.Values)
                count += (layer.Current != null ? 1 : 0) + layer.Outgoing.Count + layer.BlendSlots.Count;
            return count;
        }

        private int CountActivePlayableSlots()
        {
            int count = 0;
            foreach (LayerRuntime layer in _layers.Values)
                count += CountActiveSlots(layer);
            return count;
        }

        private static ClipSlot FindBlendSlot(LayerRuntime layer, ResourceKey key)
        {
            for (int i = 0; i < layer.BlendSlots.Count; i++)
            {
                ClipSlot slot = layer.BlendSlots[i];
                if (slot.Key == key)
                    return slot;
            }

            return null;
        }

        private static bool ContainsBlendWeight(IReadOnlyList<BlendClipWeight> weights, ResourceKey key)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].ClipKey == key)
                    return true;
            }

            return false;
        }

        private static float SumOutgoingWeight(LayerRuntime layer)
        {
            float weight = 0f;
            for (int i = 0; i < layer.Outgoing.Count; i++)
                weight += layer.Outgoing[i].Weight;
            return weight;
        }

        private static ResourceKey FindDominantBlendKey(LayerRuntime layer)
        {
            if (layer.ActiveBlendKind == MxAnimationBlendKind.Blend2D)
            {
                int index = FindDominantBlend2DIndex(layer);
                return index >= 0 ? layer.Blend2DWeights[index].ClipKey : default;
            }

            int index1D = FindDominantBlend1DIndex(layer);
            return index1D >= 0 ? layer.BlendWeights[index1D].ClipKey : default;
        }

        private static float FindDominantBlendWeight(LayerRuntime layer)
        {
            if (layer.ActiveBlendKind == MxAnimationBlendKind.Blend2D)
            {
                int index = FindDominantBlend2DIndex(layer);
                return index >= 0 ? layer.Blend2DWeights[index].Weight : 0f;
            }

            int index1D = FindDominantBlend1DIndex(layer);
            return index1D >= 0 ? layer.BlendWeights[index1D].Weight : 0f;
        }

        private static int FindDominantBlend1DIndex(LayerRuntime layer)
        {
            int bestIndex = -1;
            float bestWeight = 0f;
            for (int i = 0; i < layer.BlendWeights.Count; i++)
            {
                MxAnimationBlend1DWeight weight = layer.BlendWeights[i];
                if (weight.Weight <= bestWeight)
                    continue;

                bestWeight = weight.Weight;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static int FindDominantBlend2DIndex(LayerRuntime layer)
        {
            int bestIndex = -1;
            float bestWeight = 0f;
            for (int i = 0; i < layer.Blend2DWeights.Count; i++)
            {
                MxAnimationBlend2DWeight weight = layer.Blend2DWeights[i];
                if (weight.Weight <= bestWeight)
                    continue;

                bestWeight = weight.Weight;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static MxAnimationResourceDiagnostic CreateResourceDiagnostic(ResidentClip resident)
        {
            if (resident == null)
                return new MxAnimationResourceDiagnostic(string.Empty, default, MxAnimationResourceLoadStatus.None, false, ResourceError.None);

            return new MxAnimationResourceDiagnostic(resident.Role, resident.Key, resident.Status, resident.Key.IsValid, resident.LastError);
        }

        private static MxAnimationFadeDiagnostic CreateFadeDiagnostic(LayerRuntime layer)
        {
            if (layer.Status != MxAnimationLayerStatus.CrossFading && layer.Status != MxAnimationLayerStatus.FadingOut)
                return null;

            ResourceKey current = layer.Outgoing.Count > 0 ? layer.Outgoing[0].Key : default;
            ResourceKey next = layer.Current != null ? layer.Current.Key : default;
            float elapsed = layer.Current != null ? layer.Current.FadeElapsedSeconds : (layer.Outgoing.Count > 0 ? layer.Outgoing[0].FadeElapsedSeconds : 0f);
            float duration = layer.Current != null ? layer.Current.FadeDurationSeconds : (layer.Outgoing.Count > 0 ? layer.Outgoing[0].FadeDurationSeconds : 0f);
            float weight = layer.Current != null ? layer.Current.Weight : SumOutgoingWeight(layer);
            return new MxAnimationFadeDiagnostic(layer.LayerId, current, next, elapsed, duration, weight, layer.Status);
        }

        private static MxAnimationLayerSyncState CreateLayerSyncState(LayerRuntime layer)
        {
            if (layer.WeightTransitionDurationSeconds <= 0f)
            {
                return new MxAnimationLayerSyncState(
                    layer.LayerId,
                    layer.Weight,
                    layer.TargetWeight,
                    correlationId: layer.WeightTransitionCorrelationId);
            }

            int durationFrames = SecondsToPresentationFrames(layer.WeightTransitionDurationSeconds);
            int remainingFrames = SecondsToPresentationFrames(Math.Max(0f, layer.WeightTransitionDurationSeconds - layer.WeightTransitionElapsedSeconds));
            return new MxAnimationLayerSyncState(
                layer.LayerId,
                layer.Weight,
                layer.TargetWeight,
                transitionDurationFrames: durationFrames,
                transitionRemainingFrames: remainingFrames,
                transitionPolicyId: layer.WeightTransitionPolicyId,
                correlationId: layer.WeightTransitionCorrelationId);
        }

        private static int SecondsToPresentationFrames(float seconds)
        {
            if (seconds <= 0f)
                return 0;

            return Math.Max(1, Mathf.CeilToInt(seconds * 60f));
        }

        private enum LoadPurpose
        {
            Resident,
            Request
        }

        private readonly struct ClipRequest
        {
            public ClipRequest(
                MxAnimationLayerId layerId,
                ResourceKey requestedClipKey,
                ResourceKey clipKey,
                float fadeDurationSeconds,
                float playbackSpeed,
                bool loop,
                float startOffsetSeconds,
                string correlationId,
                bool allowFallback = true)
            {
                LayerId = layerId;
                RequestedClipKey = requestedClipKey;
                ClipKey = clipKey;
                FadeDurationSeconds = fadeDurationSeconds;
                PlaybackSpeed = playbackSpeed;
                Loop = loop;
                StartOffsetSeconds = startOffsetSeconds;
                CorrelationId = correlationId ?? string.Empty;
                AllowFallback = allowFallback;
            }

            public MxAnimationLayerId LayerId { get; }
            public ResourceKey RequestedClipKey { get; }
            public bool AllowFallback { get; }
            public ResourceKey ClipKey { get; }
            public float FadeDurationSeconds { get; }
            public float PlaybackSpeed { get; }
            public bool Loop { get; }
            public float StartOffsetSeconds { get; }
            public string CorrelationId { get; }
        }

        private readonly struct BlendClipWeight
        {
            public BlendClipWeight(ResourceKey clipKey, float weight, float playbackSpeed, bool loop)
            {
                ClipKey = clipKey;
                Weight = Clamp01(weight);
                PlaybackSpeed = NormalizeSpeed(playbackSpeed);
                Loop = loop;
            }

            public ResourceKey ClipKey { get; }
            public float Weight { get; }
            public float PlaybackSpeed { get; }
            public bool Loop { get; }
        }

        private sealed class PendingLoad
        {
            public PendingLoad(
                LoadPurpose purpose,
                MxAnimationRequestKind requestKind,
                MxAnimationLayerId layerId,
                ResourceKey requestedKey,
                ResourceKey loadKey,
                bool fallbackAttempt,
                float fadeDurationSeconds,
                float playbackSpeed,
                bool loop,
                float startOffsetSeconds,
                string correlationId,
                IResourceOperation<ResourceHandle<AnimationClip>> operation,
                ResidentClip resident,
                bool allowFallback = true)
            {
                Purpose = purpose;
                RequestKind = requestKind;
                LayerId = layerId;
                RequestedKey = requestedKey;
                LoadKey = loadKey;
                FallbackAttempt = fallbackAttempt;
                FadeDurationSeconds = fadeDurationSeconds;
                PlaybackSpeed = playbackSpeed;
                Loop = loop;
                StartOffsetSeconds = startOffsetSeconds;
                CorrelationId = correlationId ?? string.Empty;
                Operation = operation;
                Resident = resident;
                AllowFallback = allowFallback;
            }

            public LoadPurpose Purpose { get; }
            public bool Invalidated { get; set; }
            public bool AllowFallback { get; }
            public MxAnimationRequestKind RequestKind { get; }
            public MxAnimationLayerId LayerId { get; }
            public ResourceKey RequestedKey { get; }
            public ResourceKey LoadKey { get; }
            public bool FallbackAttempt { get; }
            public float FadeDurationSeconds { get; }
            public float PlaybackSpeed { get; }
            public bool Loop { get; }
            public float StartOffsetSeconds { get; }
            public string CorrelationId { get; }
            public IResourceOperation<ResourceHandle<AnimationClip>> Operation { get; }
            public ResidentClip Resident { get; }
        }

        private sealed class PendingMaskLoad
        {
            public PendingMaskLoad(
                MxAnimationLayerId layerId,
                IResourceOperation<ResourceHandle<AvatarMask>> operation)
            {
                LayerId = layerId;
                Operation = operation;
            }

            public MxAnimationLayerId LayerId { get; }
            public IResourceOperation<ResourceHandle<AvatarMask>> Operation { get; }
        }

        private sealed class ResidentClip
        {
            public ResidentClip(string role, ResourceKey key)
            {
                Role = role ?? string.Empty;
                Key = key;
                Status = key.IsValid ? MxAnimationResourceLoadStatus.Loading : MxAnimationResourceLoadStatus.None;
            }

            public string Role { get; }
            public ResourceKey Key { get; }
            public ResourceHandle<AnimationClip> Handle { get; set; }
            public MxAnimationResourceLoadStatus Status { get; set; }
            public ResourceError LastError { get; set; }
        }

        private sealed class LayerRuntime
        {
            public ClipSlot TimeSynchronizedSlot;
            public double SynchronizedTimeSeconds;

            public LayerRuntime(
                MxAnimationLayerId layerId,
                MxAnimationLayerMixerHandle layerMixer,
                float weight,
                string profileId,
                MxAnimationLayerBlendMode blendMode,
                ResourceKey maskKey)
            {
                LayerId = layerId;
                LayerMixer = layerMixer;
                Status = MxAnimationLayerStatus.Stopped;
                Weight = Clamp01(weight);
                StartWeight = Weight;
                TargetWeight = Weight;
                ProfileId = profileId ?? string.Empty;
                BlendMode = blendMode;
                MaskKey = maskKey;
                MaskStatus = maskKey.IsValid ? MxAnimationLayerMaskStatus.Loading : MxAnimationLayerMaskStatus.NotConfigured;
            }

            public MxAnimationLayerId LayerId { get; }
            public MxAnimationLayerMixerHandle LayerMixer { get; }
            public int RootInputIndex => LayerMixer.RootInputIndex;
            public float Weight { get; set; }
            public float StartWeight { get; set; }
            public float TargetWeight { get; set; }
            public float WeightTransitionElapsedSeconds { get; set; }
            public float WeightTransitionDurationSeconds { get; set; }
            public string WeightTransitionPolicyId { get; set; }
            public string WeightTransitionCorrelationId { get; set; }
            public string ProfileId { get; }
            public MxAnimationLayerBlendMode BlendMode { get; }
            public ResourceKey MaskKey { get; }
            public MxAnimationLayerMaskStatus MaskStatus { get; set; }
            public ResourceHandle<AvatarMask> MaskHandle { get; set; }
            public ClipSlot Current { get; set; }
            public List<ClipSlot> Outgoing { get; } = new List<ClipSlot>();
            public List<ClipSlot> BlendSlots { get; } = new List<ClipSlot>();
            public List<MxAnimationBlend1DWeight> BlendWeights { get; } = new List<MxAnimationBlend1DWeight>();
            public List<MxAnimationBlend2DWeight> Blend2DWeights { get; } = new List<MxAnimationBlend2DWeight>();
            public string ActiveBlendId { get; set; } = string.Empty;
            public MxAnimationBlendKind ActiveBlendKind { get; set; }
            public MxAnimationQuantizedParameter BlendParameter { get; set; }
            public MxAnimationQuantizedParameter Blend2DParameterX { get; set; }
            public MxAnimationQuantizedParameter Blend2DParameterY { get; set; }
            public float BlendFadeElapsedSeconds { get; set; }
            public float BlendFadeDurationSeconds { get; set; }
            public double BlendNormalizedPhase { get; set; }
            public MxAnimationLayerStatus Status { get; set; }
            public ResourceKey NextClipKey { get; set; }
            public ResourceError LastError { get; set; }
        }

        private sealed class ClipSlot
        {
            public ClipSlot(
                ResourceKey key,
                ResourceHandle<AnimationClip> handle,
                AnimationClipPlayable playable,
                bool ownsHandle,
                bool isFallback,
                float playbackSpeed,
                bool loop)
            {
                Key = key;
                Handle = handle;
                Playable = playable;
                OwnsHandle = ownsHandle;
                IsFallback = isFallback;
                PlaybackSpeed = playbackSpeed;
                Loop = loop;
                HasEvents = handle != null && handle.Value != null && handle.Value.events.Length != 0;
                InputIndex = -1;
            }

            public ResourceKey Key { get; }
            public ResourceHandle<AnimationClip> Handle { get; }
            public AnimationClipPlayable Playable { get; }
            public bool OwnsHandle { get; }
            public bool IsFallback { get; }
            public float PlaybackSpeed { get; set; }
            public bool Loop { get; set; }
            public bool HasEvents { get; }
            public int InputIndex { get; set; }
            public float Weight { get; set; }
            public float TargetWeight { get; set; }
            public float FadeElapsedSeconds { get; set; }
            public float FadeDurationSeconds { get; set; }
        }

        private readonly struct Blend2DPointCoordinateOverride
        {
            public Blend2DPointCoordinateOverride(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }
    }
}
