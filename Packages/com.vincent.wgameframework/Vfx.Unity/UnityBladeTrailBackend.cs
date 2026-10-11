using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Vfx.Unity
{
    /// <summary>The currently presented blade, with authenticated action/equipment continuity.</summary>
    public readonly struct UnityBladeTrailPose
    {
        public UnityBladeTrailPose(Vector3 root, Vector3 tip, Vector3 ownerPosition,
            Quaternion ownerRotation, double actionTime, long epoch)
        {
            Root = root; Tip = tip; OwnerPosition = ownerPosition; OwnerRotation = ownerRotation;
            ActionTime = actionTime; Epoch = epoch;
        }
        public Vector3 Root { get; }
        public Vector3 Tip { get; }
        public Vector3 OwnerPosition { get; }
        public Quaternion OwnerRotation { get; }
        public double ActionTime { get; }
        public long Epoch { get; }
    }

    public interface IUnityBladeTrailPoseSource : IDisposable
    {
        /// <summary>NoAlloc. False means no actual matching weapon/action, not a substitute socket.</summary>
        bool TryGetPresentedPose(out UnityBladeTrailPose pose);
        /// <summary>NoAlloc. Evaluate the bound animation at an intermediate time, restoring live pose/state.</summary>
        bool TrySamplePose(double actionTime, Vector3 ownerPosition, Quaternion ownerRotation,
            out Vector3 root, out Vector3 tip);
    }

    /// <summary>Scope-owned cap covering emitting AND retiring ribbons. No global camera state.</summary>
    public sealed class UnityBladeTrailBudget
    {
        private readonly int _limit;
        private int _leased;
        public UnityBladeTrailBudget(int limit = 8) { _limit = Math.Max(1, Math.Min(32, limit)); }
        public int LeasedCount => _leased;
        public bool TryAcquire() { if (_leased >= _limit) return false; _leased++; return true; }
        public void Release() { if (_leased > 0) _leased--; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("MxFramework/Vfx/Blade Trail Backend")]
    public sealed class UnityBladeTrailBackend : MonoBehaviour, IUnityVfxBackendController, IUnityVfxTimedBackend, IDisposable
    {
        [SerializeField, Range(0.08f, 0.15f)] private float _residueSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _exitFadeSeconds;
        [SerializeField, Range(8, 64)] private int _maxSlices = 32;
        [SerializeField, Range(1, 16)] private int _maxSubsamples = 16;
        [SerializeField, Range(0.004f, 0.02f)] private float _sampleStepSeconds = 1f / 200f;
        [SerializeField, Range(0.01f, 1f)] private float _opacity = 0.22f;
        [SerializeField] private Color _tint = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;
        private UnityBladeTrailHistory _history;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector2[] _uv;
        private Color32[] _colors;
        private int[] _indices;
        private IUnityBladeTrailPoseSource _source;
        private UnityBladeTrailBudget _budget;
        private bool _leased;
        private bool _emitting;
        private bool _hasPrevious;
        private UnityBladeTrailPose _previous;
        private double _clock;
        private bool _fading;
        private double _fadeStartedAt;
        private float _defaultResidueSeconds;
        private static readonly int TrailStylePropertyId = Shader.PropertyToID("_TrailStyle");
        private MeshRenderer _styleRenderer;
        private MaterialPropertyBlock _styleProperties;
        public bool IsAvailable => true;
        public bool SuppressParticleSystemFallbackWhenAvailable => true;
        public bool IsPresentationComplete => !_emitting && (_history == null || _history.Count < 2);
        public int SliceCount => _history == null ? 0 : _history.Count;

        private void Awake() { EnsureStorage(); }
        /// <summary>NoAlloc after initialization. 0 restores the material default; 1–4 select thin, ribbon, double or energy.</summary>
        public bool ConfigureStyle(int styleId)
        {
            if (styleId < 0 || styleId > 4) return false;
            EnsureStorage();
            ApplyStyle(styleId);
            return true;
        }

        private void ApplyStyle(int styleId)
        {
            if (_styleRenderer == null || _styleProperties == null) return;
            Material material = _styleRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(TrailStylePropertyId)) return;
            _styleRenderer.GetPropertyBlock(_styleProperties);
            _styleProperties.SetFloat(TrailStylePropertyId,
                styleId == 0 ? material.GetFloat(TrailStylePropertyId) : styleId - 1);
            _styleRenderer.SetPropertyBlock(_styleProperties);
        }

        public void ConfigureResidueMilliseconds(int milliseconds)
        {
            if (milliseconds >= 80 && milliseconds <= 150) _residueSeconds = milliseconds * 0.001f;
        }

        /// <summary>LowFreqAlloc at instance binding only. A failed lease disposes the supplied source.</summary>
        public void BindSource(IUnityBladeTrailPoseSource source, UnityBladeTrailBudget budget)
        {
            StopAndClear();
            _budget = budget;
            _leased = source != null && budget != null && budget.TryAcquire();
            if (_leased) _source = source;
            else source?.Dispose();
        }
        public void ReinitializeAndPlay(VfxInstanceSnapshot instance)
        {
            EnsureStorage();
            _history.Clear(); _clock = 0d; _hasPrevious = false; _fading = false;
            _residueSeconds = _defaultResidueSeconds;
            _emitting = _source != null && _leased;
            if (instance != null && instance.Parameters.Value3 >= 80 && instance.Parameters.Value3 <= 150)
                _residueSeconds = instance.Parameters.Value3 * 0.001f;
            _mesh.Clear(false);
        }
        public void BeginPresentationExit(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { StopAndClear(); return; }
            _emitting = false; _hasPrevious = false;
            // An opt-in style tail retains its current world-space silhouette while fading.
            // Other prefabs retain their original per-sample expiry behavior.
            if (!_fading && _exitFadeSeconds > 0f && _history != null && _history.Count >= 2)
            { _fading = true; _fadeStartedAt = _clock; }
        }
        public void TickPresentation(float deltaTime)
        {
            if (_history == null || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float elapsed = Mathf.Max(0f, deltaTime);
            _clock += elapsed;
            if (!_fading) _history.Expire(_clock, _residueSeconds);
            if (_emitting)
            {
                if (_source == null || !_source.TryGetPresentedPose(out UnityBladeTrailPose current))
                    BeginPresentationExit(VfxStopMode.Dissipate);
                else SamplePresented(current, elapsed);
            }
            if (_fading && _clock - _fadeStartedAt >= _exitFadeSeconds)
            { _history.Clear(); _fading = false; }
            RebuildMesh();
        }
        private void SamplePresented(UnityBladeTrailPose current, float elapsed)
        {
            if (double.IsNaN(current.ActionTime) || double.IsInfinity(current.ActionTime))
            { _history.Clear(); _hasPrevious = false; return; }
            double actionDelta = current.ActionTime - _previous.ActionTime;
            bool discontinuity = !_hasPrevious || current.Epoch != _previous.Epoch || actionDelta < 0d
                || (current.OwnerPosition - _previous.OwnerPosition).sqrMagnitude > _teleportDistance * _teleportDistance
                || actionDelta > _sampleStepSeconds * _maxSubsamples;
            int steps = discontinuity ? 1 : Math.Max(1, (int)Math.Ceiling(actionDelta / _sampleStepSeconds));
            // Missing poses are never replaced by straight endpoint interpolation across a spin.
            if (steps > _maxSubsamples || elapsed > _residueSeconds * 2f) discontinuity = true;
            if (discontinuity) _history.Clear();
            else if (actionDelta > 0d)
            {
                for (int i = 1; i < steps; i++)
                {
                    float fraction = (float)i / steps;
                    if (!_source.TrySamplePose(_previous.ActionTime + actionDelta * fraction,
                        Vector3.Lerp(_previous.OwnerPosition, current.OwnerPosition, fraction),
                        Quaternion.Slerp(_previous.OwnerRotation, current.OwnerRotation, fraction),
                        out Vector3 root, out Vector3 tip))
                    { _history.Clear(); break; }
                    Append(root, tip, _clock - elapsed * (1f - fraction));
                }
            }
            Append(current.Root, current.Tip, _clock);
            _previous = current; _hasPrevious = true;
        }
        private void Append(Vector3 root, Vector3 tip, double time)
        {
            if ((tip - root).sqrMagnitude < 0.000001f) { _history.Clear(); return; }
            _history.Append(new BladeTrailPosition(root.x, root.y, root.z),
                new BladeTrailPosition(tip.x, tip.y, tip.z), time);
        }
        private void EnsureStorage()
        {
            if (_history != null) return;
            int count = Mathf.Clamp(_maxSlices, 8, 64);
            _residueSeconds = Mathf.Clamp(_residueSeconds, 0.08f, 0.15f);
            _exitFadeSeconds = Mathf.Clamp(_exitFadeSeconds, 0f, 1f);
            _defaultResidueSeconds = _residueSeconds;
            _maxSubsamples = Mathf.Clamp(_maxSubsamples, 1, 16);
            _sampleStepSeconds = Mathf.Clamp(_sampleStepSeconds, 0.004f, 0.02f);
            _history = new UnityBladeTrailHistory(count);
            _styleRenderer = GetComponent<MeshRenderer>();
            _styleProperties = new MaterialPropertyBlock();
            _vertices = new Vector3[count * 2]; _uv = new Vector2[count * 2];
            _colors = new Color32[count * 2]; _indices = new int[(count - 1) * 6];
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2, t = i * 6;
                _indices[t] = v; _indices[t + 1] = v + 1; _indices[t + 2] = v + 2;
                _indices[t + 3] = v + 1; _indices[t + 4] = v + 3; _indices[t + 5] = v + 2;
            }
            _mesh = new Mesh { name = "MxBladeTrail.Dynamic", indexFormat = IndexFormat.UInt16 };
            _mesh.MarkDynamic(); GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        private void RebuildMesh()
        {
            int count = _history.Count;
            if (count < 2) { _mesh.Clear(false); return; }
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            double ageClock = _fading ? _fadeStartedAt : _clock;
            float exitAlpha = _fading
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)((_clock - _fadeStartedAt) / _exitFadeSeconds)))
                : 1f;
            for (int i = 0; i < count; i++)
            {
                BladeTrailSlice slice = _history[i]; int v = i * 2;
                _vertices[v] = worldToLocal.MultiplyPoint3x4(new Vector3(slice.Root.X, slice.Root.Y, slice.Root.Z));
                _vertices[v + 1] = worldToLocal.MultiplyPoint3x4(new Vector3(slice.Tip.X, slice.Tip.Y, slice.Tip.Z));
                Color color = _tint; color.a *= _opacity * exitAlpha
                    * UnityBladeTrailHistory.Fade(ageClock, slice.Time, _residueSeconds);
                _colors[v] = color; _colors[v + 1] = color;
                float u = (float)i / (count - 1); _uv[v] = new Vector2(u, 0); _uv[v + 1] = new Vector2(u, 1);
            }
            _mesh.Clear(false);
            _mesh.SetVertices(_vertices, 0, count * 2); _mesh.SetUVs(0, _uv, 0, count * 2);
            _mesh.SetColors(_colors, 0, count * 2); _mesh.SetTriangles(_indices, 0, (count - 1) * 6, 0, true);
        }
        public void StopAndClear()
        {
            _emitting = false; _hasPrevious = false; _fading = false; _history?.Clear();
            ApplyStyle(0);
            if (_mesh != null) _mesh.Clear(false);
            _source?.Dispose(); _source = null;
            if (_leased) _budget?.Release();
            _leased = false; _budget = null;
        }
        /// <summary>Release scope-owned storage explicitly; Edit Mode hosts cannot rely on MonoBehaviour destruction callbacks.</summary>
        public void Dispose()
        {
            StopAndClear();
            if (_mesh != null)
            { if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh); }
            _mesh = null; _history = null;
            _vertices = null; _uv = null; _colors = null; _indices = null;
        }
        private void OnDestroy() => Dispose();
    }
}
