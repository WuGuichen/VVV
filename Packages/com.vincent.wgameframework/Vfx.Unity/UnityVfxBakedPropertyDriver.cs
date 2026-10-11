using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    public enum UnityVfxBakedPropertyValueKind
    {
        Float = 0,
        Color = 1
    }

    [Serializable]
    public sealed class UnityVfxBakedPropertyTrack
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _propertyName = string.Empty;
        [SerializeField] private UnityVfxBakedPropertyValueKind _valueKind;
        [SerializeField] private UnityVfxLifecyclePhase _phase;
        [SerializeField] private int _startFrame;
        [SerializeField] private int _endFrame = 1;
        [SerializeField] private AnimationCurve _floatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private Gradient _colorGradient = new Gradient();

        public Renderer Renderer => _renderer;
        public string PropertyName => _propertyName;
        public UnityVfxBakedPropertyValueKind ValueKind => _valueKind;
        public UnityVfxLifecyclePhase Phase => _phase;
        public int StartFrame => _startFrame;
        public int EndFrame => _endFrame;
        public AnimationCurve FloatCurve => _floatCurve;
        public Gradient ColorGradient => _colorGradient;

        /// <summary>Sets one immutable MaterialPropertyBlock track.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void ConfigureFloat(Renderer renderer, string propertyName, UnityVfxLifecyclePhase phase, int startFrame, int endFrame, AnimationCurve curve)
        {
            _renderer = renderer;
            _propertyName = propertyName ?? string.Empty;
            _valueKind = UnityVfxBakedPropertyValueKind.Float;
            _phase = phase;
            _startFrame = Mathf.Max(0, startFrame);
            _endFrame = Mathf.Max(_startFrame + 1, endFrame);
            _floatCurve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>Sets one immutable MaterialPropertyBlock color track.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void ConfigureColor(Renderer renderer, string propertyName, UnityVfxLifecyclePhase phase, int startFrame, int endFrame, Gradient gradient)
        {
            _renderer = renderer;
            _propertyName = propertyName ?? string.Empty;
            _valueKind = UnityVfxBakedPropertyValueKind.Color;
            _phase = phase;
            _startFrame = Mathf.Max(0, startFrame);
            _endFrame = Mathf.Max(_startFrame + 1, endFrame);
            _colorGradient = gradient ?? new Gradient();
        }
    }

    /// <summary>Drives baked Shader semantic tracks using cached MaterialPropertyBlocks.</summary>
    /// <remarks>Configure/cache are AllocByDesign. EnterPhase and AdvancePhase are NoAlloc after initialization.</remarks>
    [DisallowMultipleComponent]
    public sealed class UnityVfxBakedPropertyDriver : MonoBehaviour
    {
        [SerializeField] private UnityVfxBakedPropertyTrack[] _tracks = Array.Empty<UnityVfxBakedPropertyTrack>();

        private int[] _propertyIds = Array.Empty<int>();
        private MaterialPropertyBlock[] _blocks = Array.Empty<MaterialPropertyBlock>();
        private bool _initialized;
        private UnityVfxLifecyclePhase _phase;

        public int TrackCount => _tracks?.Length ?? 0;

        /// <summary>Assigns immutable property tracks during Candidate baking.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void Configure(UnityVfxBakedPropertyTrack[] tracks)
        {
            _tracks = tracks ?? Array.Empty<UnityVfxBakedPropertyTrack>();
            Cache();
        }

        /// <summary>Starts property evaluation for a phase.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void EnterPhase(UnityVfxLifecyclePhase phase)
        {
            EnsureInitialized();
            _phase = phase;
            AdvancePhase(0f);
        }

        /// <summary>Evaluates all tracks at the shared floating-point phase frame.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void AdvancePhase(float phaseFrame)
        {
            if (!_initialized || _phase == UnityVfxLifecyclePhase.None) return;
            for (int i = 0; i < _tracks.Length; i++)
            {
                UnityVfxBakedPropertyTrack track = _tracks[i];
                Renderer renderer = track?.Renderer;
                if (renderer == null || track.Phase != _phase) continue;
                float normalized = Mathf.InverseLerp(track.StartFrame, track.EndFrame, phaseFrame);
                MaterialPropertyBlock block = _blocks[i];
                renderer.GetPropertyBlock(block);
                if (track.ValueKind == UnityVfxBakedPropertyValueKind.Color)
                    block.SetColor(_propertyIds[i], track.ColorGradient.Evaluate(normalized));
                else
                    block.SetFloat(_propertyIds[i], track.FloatCurve.Evaluate(normalized));
                renderer.SetPropertyBlock(block);
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (!_initialized) Cache();
        }

        private void Cache()
        {
            _tracks ??= Array.Empty<UnityVfxBakedPropertyTrack>();
            _propertyIds = new int[_tracks.Length];
            _blocks = new MaterialPropertyBlock[_tracks.Length];
            for (int i = 0; i < _tracks.Length; i++)
            {
                _propertyIds[i] = Shader.PropertyToID(_tracks[i]?.PropertyName ?? string.Empty);
                _blocks[i] = new MaterialPropertyBlock();
            }
            _initialized = true;
        }
    }
}
