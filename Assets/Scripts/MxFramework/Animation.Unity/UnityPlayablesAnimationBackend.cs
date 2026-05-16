using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.Animation.Unity
{
    public sealed class UnityPlayablesAnimationBackend : IMxAnimationBackend
    {
        private const int MaxRecentItems = 16;

        private readonly Animator _animator;
        private readonly IResourceManager _resourceManager;
        private readonly MxAnimationSetDefinition _definition;
        private readonly string _actorId;
        private readonly Dictionary<MxAnimationLayerId, LayerRuntime> _layers = new Dictionary<MxAnimationLayerId, LayerRuntime>();
        private readonly List<PendingLoad> _pendingLoads = new List<PendingLoad>();
        private readonly List<PendingMaskLoad> _pendingMaskLoads = new List<PendingMaskLoad>();
        private readonly Queue<MxAnimationRequestDiagnostic> _recentRequests = new Queue<MxAnimationRequestDiagnostic>();
        private readonly Queue<ResourceError> _recentResourceErrors = new Queue<ResourceError>();

        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _rootMixer;
        private AnimationPlayableOutput _output;
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

            CreateGraph();
            _defaultClip = LoadResidentClip("default", _definition.DefaultClip);
            _fallbackClip = LoadResidentClip("fallback", _definition.FallbackClip);
            InitializeConfiguredLayers();
            ProcessPendingLoads();
            ProcessPendingMaskLoads();
        }

        public string BackendName => "UnityPlayables";

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

            float fade = request != null ? Math.Max(0f, request.FadeOutDurationSeconds) : 0f;
            if (fade <= 0f)
            {
                ReleaseLayerSlots(layer);
                layer.Status = MxAnimationLayerStatus.Stopped;
            }
            else
            {
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
                layer.Current.PlaybackSpeed = resolved.PlaybackSpeed;
                layer.Current.Loop = resolved.Loop;
                layer.Current.Playable.SetSpeed(resolved.PlaybackSpeed);
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

        public void Tick(float deltaTime)
        {
            if (_released)
                return;

            if (deltaTime < 0f)
                deltaTime = 0f;

            ProcessPendingLoads();
            ProcessPendingMaskLoads();
            foreach (LayerRuntime layer in _layers.Values)
                TickLayer(layer, deltaTime);

            if (_graph.IsValid())
                _graph.Evaluate(deltaTime);
        }

        public MxAnimationDiagnosticSnapshot CreateSnapshot()
        {
            var layers = new List<MxAnimationLayerDiagnostic>();
            var fades = new List<MxAnimationFadeDiagnostic>();
            foreach (LayerRuntime layer in _layers.Values)
            {
                MxAnimationFadeDiagnostic fade = CreateFadeDiagnostic(layer);
                if (fade != null)
                    fades.Add(fade);

                layers.Add(new MxAnimationLayerDiagnostic(
                    layer.LayerId,
                    layer.Status,
                    layer.Current != null ? layer.Current.Key : default,
                    layer.NextClipKey,
                    layer.Current != null && layer.Current.IsFallback,
                    layer.Current != null ? layer.Current.Weight : 0f,
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
                    CreateLayerSyncState(layer)));
            }

            return new MxAnimationDiagnosticSnapshot(
                BackendName,
                _actorId,
                _definition.SetId,
                _released ? 0 : 1,
                _graph.IsValid(),
                _released,
                CreateResourceDiagnostic(_defaultClip),
                CreateResourceDiagnostic(_fallbackClip),
                layers,
                fades,
                _recentRequests,
                _recentResourceErrors);
        }

        public void Release()
        {
            if (_released)
                return;

            _released = true;
            for (int i = 0; i < _pendingLoads.Count; i++)
                _pendingLoads[i].Operation.Cancel();
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

            if (_graph.IsValid())
                _graph.Destroy();

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

        private void CreateGraph()
        {
            _graph = PlayableGraph.Create("MxFramework.Animation.Unity");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _rootMixer = AnimationLayerMixerPlayable.Create(_graph, 0);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            _output.SetSourcePlayable(_rootMixer);
            _graph.Play();
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
                return ApplyLoadedSlot(request, kind, residentSlot, fallbackAttempt);

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
                null);

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

        private void ProcessPendingLoads()
        {
            for (int i = _pendingLoads.Count - 1; i >= 0; i--)
            {
                PendingLoad pending = _pendingLoads[i];
                if (!pending.Operation.IsDone)
                    continue;

                _pendingLoads.RemoveAt(i);
                if (pending.Purpose == LoadPurpose.Resident)
                    CompleteResidentLoad(pending);
                else
                    CompleteRequestLoad(pending);
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
            if (_rootMixer.IsValid())
                _rootMixer.SetLayerMaskFromAvatarMask((uint)layer.RootInputIndex, result.Value.Value);
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
            if (!fallbackKey.IsValid || fallbackKey == failed.LoadKey)
            {
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
                    "Clip failed and no fallback clip is available."));
                return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.LoadFailed, failed.LoadKey, error, "Clip failed and no fallback clip is available.");
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

        private ClipSlot CreateSlot(
            ResourceKey key,
            ResourceHandle<AnimationClip> handle,
            bool ownsHandle,
            bool isFallback,
            float playbackSpeed,
            bool loop,
            float startOffsetSeconds)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, handle.Value);
            playable.SetTime(Math.Max(0f, startOffsetSeconds));
            playable.SetSpeed(playbackSpeed);
            return new ClipSlot(key, handle, playable, ownsHandle, isFallback, playbackSpeed, loop);
        }

        private void PlaySlot(LayerRuntime layer, ClipSlot slot)
        {
            ReleaseLayerSlots(layer);
            ConnectSlot(layer, slot, 1f);
            layer.Current = slot;
            layer.Status = MxAnimationLayerStatus.Playing;
        }

        private void CrossFadeToSlot(LayerRuntime layer, ClipSlot slot, float fadeDuration)
        {
            fadeDuration = Math.Max(0f, fadeDuration);
            if (fadeDuration <= 0f || layer.Current == null)
            {
                ReleaseLayerSlots(layer);
                ConnectSlot(layer, slot, 1f);
                layer.Current = slot;
                layer.Status = MxAnimationLayerStatus.Playing;
                return;
            }

            MoveCurrentToOutgoing(layer, fadeDuration);
            ConnectSlot(layer, slot, 0f);
            slot.FadeDurationSeconds = fadeDuration;
            layer.Current = slot;
            layer.Status = MxAnimationLayerStatus.CrossFading;
        }

        private void MoveCurrentToOutgoing(LayerRuntime layer, float fadeDuration)
        {
            if (layer.Current == null)
                return;

            ClipSlot outgoing = layer.Current;
            outgoing.FadeDurationSeconds = Math.Max(0f, fadeDuration);
            outgoing.FadeElapsedSeconds = 0f;
            outgoing.Weight = Math.Max(0f, outgoing.Weight);
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
                SetSlotWeight(layer, outgoing, 1f - progress);
                if (outgoing.Weight > 0f)
                    continue;

                layer.Outgoing.RemoveAt(i);
                DetachAndReleaseSlot(layer, outgoing);
            }

            if (layer.Current != null && layer.Outgoing.Count == 0 && layer.Status != MxAnimationLayerStatus.Loading)
            {
                SetSlotWeight(layer, layer.Current, 1f);
                layer.Status = MxAnimationLayerStatus.Playing;
            }
            else if (layer.Current == null && layer.Outgoing.Count == 0 && layer.Status != MxAnimationLayerStatus.Loading && layer.Status != MxAnimationLayerStatus.Failed)
            {
                layer.Status = MxAnimationLayerStatus.Stopped;
            }
        }

        private LayerRuntime GetOrCreateLayer(MxAnimationLayerId layerId)
        {
            if (_layers.TryGetValue(layerId, out LayerRuntime layer))
                return layer;

            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(_graph, 0);
            int inputIndex = _rootMixer.GetInputCount();
            _rootMixer.SetInputCount(inputIndex + 1);
            _graph.Connect(mixer, 0, _rootMixer, inputIndex);

            MxAnimationLayerDefinition definition = ResolveLayerDefinition(layerId);
            float weight = definition != null ? definition.DefaultWeight : 1f;
            layer = new LayerRuntime(
                layerId,
                mixer,
                inputIndex,
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
            if (_rootMixer.IsValid())
                _rootMixer.SetInputWeight(layer.RootInputIndex, layer.Weight);
        }

        private void ApplyLayerBlendMode(LayerRuntime layer)
        {
            if (!_rootMixer.IsValid())
                return;

            _rootMixer.SetLayerAdditive((uint)layer.RootInputIndex, layer.BlendMode == MxAnimationLayerBlendMode.Additive);
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
            int inputIndex = layer.Mixer.GetInputCount();
            layer.Mixer.SetInputCount(inputIndex + 1);
            _graph.Connect(slot.Playable, 0, layer.Mixer, inputIndex);
            slot.InputIndex = inputIndex;
            SetSlotWeight(layer, slot, weight);
        }

        private void SetSlotWeight(LayerRuntime layer, ClipSlot slot, float weight)
        {
            slot.Weight = Clamp01(weight);
            if (slot.InputIndex >= 0 && layer.Mixer.IsValid())
                layer.Mixer.SetInputWeight(slot.InputIndex, slot.Weight);
        }

        private void ReleaseLayerSlots(LayerRuntime layer)
        {
            if (layer.Current != null)
            {
                DetachAndReleaseSlot(layer, layer.Current);
                layer.Current = null;
            }

            for (int i = layer.Outgoing.Count - 1; i >= 0; i--)
                DetachAndReleaseSlot(layer, layer.Outgoing[i]);
            layer.Outgoing.Clear();
            layer.NextClipKey = default;
        }

        private void DetachAndReleaseSlot(LayerRuntime layer, ClipSlot slot)
        {
            if (slot == null)
                return;

            if (slot.InputIndex >= 0 && layer.Mixer.IsValid())
            {
                layer.Mixer.SetInputWeight(slot.InputIndex, 0f);
                layer.Mixer.DisconnectInput(slot.InputIndex);
                slot.InputIndex = -1;
            }

            if (slot.Playable.IsValid() && _graph.IsValid())
                _graph.DestroyPlayable(slot.Playable);

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
            if (!clipKey.IsValid && _definition.TryFindBinding(request.BindingId, request.ActionKey, out MxAnimationActionBinding binding))
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
            if (!clipKey.IsValid && _definition.TryFindBinding(request.BindingId, request.ActionKey, out MxAnimationActionBinding binding))
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
                error = "Crossfade request did not resolve to a valid clip key.";
                return false;
            }

            resolved = new ClipRequest(layerId, clipKey, clipKey, Math.Max(0f, request.FadeDurationSeconds), speed, loop, request.TargetStartOffsetSeconds, request.CorrelationId);
            error = string.Empty;
            return true;
        }

        private MxAnimationLayerId ResolveStopLayer(MxAnimationStopRequest request)
        {
            if (request == null)
                return MxAnimationLayerId.Base;

            if (!string.IsNullOrWhiteSpace(request.BindingId) && _definition.TryFindBinding(request.BindingId, string.Empty, out MxAnimationActionBinding binding))
                return binding.Layer;

            return request.LayerId;
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

            _recentResourceErrors.Enqueue(error);
            while (_recentResourceErrors.Count > MaxRecentItems)
                _recentResourceErrors.Dequeue();
        }

        private void AddRequest(MxAnimationRequestDiagnostic diagnostic)
        {
            _recentRequests.Enqueue(diagnostic);
            while (_recentRequests.Count > MaxRecentItems)
                _recentRequests.Dequeue();
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

        private static int CountActiveSlots(LayerRuntime layer)
        {
            return (layer.Current != null ? 1 : 0) + layer.Outgoing.Count;
        }

        private static float SumOutgoingWeight(LayerRuntime layer)
        {
            float weight = 0f;
            for (int i = 0; i < layer.Outgoing.Count; i++)
                weight += layer.Outgoing[i].Weight;
            return weight;
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
                string correlationId)
            {
                LayerId = layerId;
                RequestedClipKey = requestedClipKey;
                ClipKey = clipKey;
                FadeDurationSeconds = fadeDurationSeconds;
                PlaybackSpeed = playbackSpeed;
                Loop = loop;
                StartOffsetSeconds = startOffsetSeconds;
                CorrelationId = correlationId ?? string.Empty;
            }

            public MxAnimationLayerId LayerId { get; }
            public ResourceKey RequestedClipKey { get; }
            public ResourceKey ClipKey { get; }
            public float FadeDurationSeconds { get; }
            public float PlaybackSpeed { get; }
            public bool Loop { get; }
            public float StartOffsetSeconds { get; }
            public string CorrelationId { get; }
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
                ResidentClip resident)
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
            }

            public LoadPurpose Purpose { get; }
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
            public LayerRuntime(
                MxAnimationLayerId layerId,
                AnimationMixerPlayable mixer,
                int rootInputIndex,
                float weight,
                string profileId,
                MxAnimationLayerBlendMode blendMode,
                ResourceKey maskKey)
            {
                LayerId = layerId;
                Mixer = mixer;
                RootInputIndex = rootInputIndex;
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
            public AnimationMixerPlayable Mixer { get; }
            public int RootInputIndex { get; }
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
                InputIndex = -1;
            }

            public ResourceKey Key { get; }
            public ResourceHandle<AnimationClip> Handle { get; }
            public AnimationClipPlayable Playable { get; }
            public bool OwnsHandle { get; }
            public bool IsFallback { get; }
            public float PlaybackSpeed { get; set; }
            public bool Loop { get; set; }
            public int InputIndex { get; set; }
            public float Weight { get; set; }
            public float FadeElapsedSeconds { get; set; }
            public float FadeDurationSeconds { get; set; }
        }
    }
}
