using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.Animation.Unity
{
    /// <summary>
    /// Owns PlayableGraph construction, manual evaluation, and graph shutdown.
    /// It does not load resources, resolve animation bindings, or decide backend request semantics.
    /// </summary>
    internal interface IMxAnimationPlayableGraphLifecycle
    {
        bool IsGraphValid { get; }
        int LayerCount { get; }
        void Evaluate(float deltaTime);
        void Destroy();
    }

    /// <summary>
    /// Creates and destroys clip playables from already-loaded AnimationClip instances.
    /// It does not own ResourceHandle lifetime or fallback selection.
    /// </summary>
    internal interface IMxAnimationClipPlayableFactory
    {
        AnimationClipPlayable CreateClipPlayable(AnimationClip clip, float playbackSpeed, float startOffsetSeconds);
        void DestroyClipPlayable(AnimationClipPlayable playable);
    }

    /// <summary>
    /// Manages root layer mixer inputs: layer creation, layer weight, additive mode, and AvatarMask wiring.
    /// It does not connect individual clip playables inside a layer mixer.
    /// </summary>
    internal interface IMxAnimationLayerMixerDriver
    {
        MxAnimationLayerMixerHandle AddLayerMixer();
        void SetLayerWeight(MxAnimationLayerMixerHandle layer, float weight);
        void SetLayerAdditive(MxAnimationLayerMixerHandle layer, bool additive);
        void SetLayerMask(MxAnimationLayerMixerHandle layer, AvatarMask mask);
    }

    /// <summary>
    /// Manages clip inputs inside one layer mixer, including weighted 1D blend slots.
    /// It does not calculate blend weights or load clips.
    /// </summary>
    internal interface IMxAnimationBlendMixerDriver
    {
        int ConnectClip(MxAnimationLayerMixerHandle layer, AnimationClipPlayable playable, float weight);
        void SetClipWeight(MxAnimationLayerMixerHandle layer, int inputIndex, float weight);
        void DisconnectClip(MxAnimationLayerMixerHandle layer, int inputIndex);
    }

    /// <summary>
    /// Stores backend diagnostics in bounded buffers for snapshots.
    /// It does not interpret request success or own graph/resource lifetimes.
    /// </summary>
    internal interface IMxAnimationPlayableDiagnostics
    {
        IReadOnlyList<MxAnimationRequestDiagnostic> RecentRequests { get; }
        IReadOnlyList<ResourceError> RecentResourceErrors { get; }
        void AddRequest(MxAnimationRequestDiagnostic diagnostic);
        void TrackResourceError(ResourceError error);
    }

    internal readonly struct MxAnimationLayerMixerHandle
    {
        public MxAnimationLayerMixerHandle(AnimationMixerPlayable mixer, int rootInputIndex)
        {
            Mixer = mixer;
            RootInputIndex = rootInputIndex;
        }

        public AnimationMixerPlayable Mixer { get; }
        public int RootInputIndex { get; }
        public bool IsValid => RootInputIndex >= 0 && Mixer.IsValid();
    }

    internal sealed class MxAnimationPlayableDiagnosticBuffer : IMxAnimationPlayableDiagnostics
    {
        private readonly List<MxAnimationRequestDiagnostic> _recentRequests;
        private readonly List<ResourceError> _recentResourceErrors;

        public MxAnimationPlayableDiagnosticBuffer(int maxRecentItems)
        {
            MaxRecentItems = Math.Max(1, maxRecentItems);
            _recentRequests = new List<MxAnimationRequestDiagnostic>(MaxRecentItems);
            _recentResourceErrors = new List<ResourceError>(MaxRecentItems);
        }

        public int MaxRecentItems { get; }
        public IReadOnlyList<MxAnimationRequestDiagnostic> RecentRequests => _recentRequests;
        public IReadOnlyList<ResourceError> RecentResourceErrors => _recentResourceErrors;

        public void AddRequest(MxAnimationRequestDiagnostic diagnostic)
        {
            AddBounded(_recentRequests, diagnostic);
        }

        public void TrackResourceError(ResourceError error)
        {
            if (error.IsNone)
                return;

            AddBounded(_recentResourceErrors, error);
        }

        private void AddBounded<T>(List<T> items, T item)
        {
            items.Add(item);
            while (items.Count > MaxRecentItems)
                items.RemoveAt(0);
        }
    }

    internal sealed class MxAnimationPlayableGraphRuntime :
        IMxAnimationPlayableGraphLifecycle,
        IMxAnimationClipPlayableFactory,
        IMxAnimationLayerMixerDriver,
        IMxAnimationBlendMixerDriver
    {
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _rootMixer;

        public MxAnimationPlayableGraphRuntime(Animator animator, string graphName = "MxFramework.Animation.Unity")
        {
            if (animator == null)
                throw new ArgumentNullException(nameof(animator));

            _graph = PlayableGraph.Create(string.IsNullOrWhiteSpace(graphName) ? "MxFramework.Animation.Unity" : graphName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _rootMixer = AnimationLayerMixerPlayable.Create(_graph, 0);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            output.SetSourcePlayable(_rootMixer);
            _graph.Play();
        }

        public bool IsGraphValid => _graph.IsValid();
        public int LayerCount => _rootMixer.IsValid() ? _rootMixer.GetInputCount() : 0;
        internal AnimationLayerMixerPlayable RootMixer => _rootMixer;

        public void Evaluate(float deltaTime)
        {
            if (_graph.IsValid())
                _graph.Evaluate(deltaTime);
        }

        public void Destroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();

            _graph = default;
            _rootMixer = default;
        }

        public AnimationClipPlayable CreateClipPlayable(AnimationClip clip, float playbackSpeed, float startOffsetSeconds)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetTime(Math.Max(0f, startOffsetSeconds));
            playable.SetSpeed(playbackSpeed);
            return playable;
        }

        public void DestroyClipPlayable(AnimationClipPlayable playable)
        {
            if (playable.IsValid() && _graph.IsValid())
                _graph.DestroyPlayable(playable);
        }

        public MxAnimationLayerMixerHandle AddLayerMixer()
        {
            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(_graph, 0);
            int inputIndex = _rootMixer.GetInputCount();
            _rootMixer.SetInputCount(inputIndex + 1);
            _graph.Connect(mixer, 0, _rootMixer, inputIndex);
            return new MxAnimationLayerMixerHandle(mixer, inputIndex);
        }

        public void SetLayerWeight(MxAnimationLayerMixerHandle layer, float weight)
        {
            if (layer.IsValid && _rootMixer.IsValid())
                _rootMixer.SetInputWeight(layer.RootInputIndex, Clamp01(weight));
        }

        public void SetLayerAdditive(MxAnimationLayerMixerHandle layer, bool additive)
        {
            if (layer.IsValid && _rootMixer.IsValid())
                _rootMixer.SetLayerAdditive((uint)layer.RootInputIndex, additive);
        }

        public void SetLayerMask(MxAnimationLayerMixerHandle layer, AvatarMask mask)
        {
            if (layer.IsValid && _rootMixer.IsValid() && mask != null)
                _rootMixer.SetLayerMaskFromAvatarMask((uint)layer.RootInputIndex, mask);
        }

        public int ConnectClip(MxAnimationLayerMixerHandle layer, AnimationClipPlayable playable, float weight)
        {
            if (!layer.IsValid || !playable.IsValid())
                return -1;

            AnimationMixerPlayable mixer = layer.Mixer;
            int inputIndex = mixer.GetInputCount();
            mixer.SetInputCount(inputIndex + 1);
            _graph.Connect(playable, 0, mixer, inputIndex);
            SetClipWeight(layer, inputIndex, weight);
            return inputIndex;
        }

        public void SetClipWeight(MxAnimationLayerMixerHandle layer, int inputIndex, float weight)
        {
            if (inputIndex < 0 || !layer.IsValid)
                return;

            layer.Mixer.SetInputWeight(inputIndex, Clamp01(weight));
        }

        public void DisconnectClip(MxAnimationLayerMixerHandle layer, int inputIndex)
        {
            if (inputIndex < 0 || !layer.IsValid)
                return;

            layer.Mixer.SetInputWeight(inputIndex, 0f);
            layer.Mixer.DisconnectInput(inputIndex);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}
