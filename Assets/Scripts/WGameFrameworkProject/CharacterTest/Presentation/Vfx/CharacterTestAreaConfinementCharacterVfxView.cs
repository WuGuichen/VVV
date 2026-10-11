using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestAreaConfinementCharacterVfxMode
    {
        Active = 0,
        Release = 1,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Area Confinement Character VFX View")]
    public sealed class CharacterTestAreaConfinementCharacterVfxView : MonoBehaviour
    {
        private const int RingSegments = 72;
        private const int ChainCount = 5;
        private const float DefaultRadius = 0.62f;

        [SerializeField][Min(0.1f)] private float _radius = DefaultRadius;
        [SerializeField][Min(0.1f)] private float _height = 1.75f;
        [SerializeField][Min(0f)] private float _rotationSpeedDegrees = 32f;
        [SerializeField] private CharacterTestAreaConfinementCharacterVfxMode _mode = CharacterTestAreaConfinementCharacterVfxMode.Active;
        [SerializeField][Min(0)] private int _fadeInFrames = CharacterTestAreaConfinementIds.VfxFadeInFrames;
        [SerializeField][Min(1)] private int _releaseFrames = CharacterTestAreaConfinementIds.CharacterReleaseFrames;
        [SerializeField] private Color _ringColor = new Color(0.42f, 0.94f, 1f, 0.86f);
        [SerializeField] private Color _chainColor = new Color(1f, 0.82f, 0.34f, 0.72f);
        [SerializeField] private Material _lineMaterialOverride;

        private Material _runtimeLineMaterial;
        private Material _lineMaterial;
        private Transform _root;
        private LineRenderer[] _chains;
        private float _ageSeconds;

        public static GameObject CreateRuntimePrefab(
            CharacterTestAreaConfinementCharacterVfxMode mode = CharacterTestAreaConfinementCharacterVfxMode.Active)
        {
            var prefab = new GameObject("CharacterTest_AreaConfinementCharacter" + mode + "Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestAreaConfinementCharacterVfxView view =
                prefab.AddComponent<CharacterTestAreaConfinementCharacterVfxView>();
            view.ApplyModeDefaults(mode);
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void ApplyModeDefaults(CharacterTestAreaConfinementCharacterVfxMode mode)
        {
            _mode = mode;
            if (_mode == CharacterTestAreaConfinementCharacterVfxMode.Release)
            {
                _fadeInFrames = 0;
                _releaseFrames = CharacterTestAreaConfinementIds.CharacterReleaseFrames;
                _rotationSpeedDegrees = -44f;
                _ringColor = new Color(0.52f, 0.98f, 1f, 0.72f);
                _chainColor = new Color(1f, 0.9f, 0.42f, 0.58f);
                return;
            }

            _fadeInFrames = CharacterTestAreaConfinementIds.VfxFadeInFrames;
            _releaseFrames = CharacterTestAreaConfinementIds.CharacterReleaseFrames;
            _rotationSpeedDegrees = 32f;
            _ringColor = new Color(0.42f, 0.94f, 1f, 0.86f);
            _chainColor = new Color(1f, 0.82f, 0.34f, 0.72f);
        }

        public void RebuildVisuals()
        {
            EnsureVisuals(1f);
        }

        private void OnEnable()
        {
            _ageSeconds = 0f;
            EnsureVisuals(ResolveVisualAlpha(_ageSeconds));
        }

        private void OnDestroy()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);

            _runtimeLineMaterial = null;
            _lineMaterial = null;
        }

        private void Update()
        {
            float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            _ageSeconds += Mathf.Max(0f, deltaTime);
            float time = Application.isPlaying ? _ageSeconds : Time.realtimeSinceStartup;
            float alpha = ResolveVisualAlpha(_ageSeconds);
            float scale = ResolveModeScale(_ageSeconds);
            EnsureVisuals(alpha);
            if (_root != null)
            {
                _root.localScale = new Vector3(scale, scale, scale);
                _root.Rotate(0f, _rotationSpeedDegrees * deltaTime, 0f, Space.Self);
            }

            float pulse = alpha * (0.78f + 0.2f * (Mathf.Sin(time * 5.2f) + 1f) * 0.5f);
            Color chainColor = WithAlpha(_chainColor, _chainColor.a * pulse);
            if (_chains == null)
                return;

            for (int i = 0; i < _chains.Length; i++)
            {
                if (_chains[i] == null)
                    continue;

                _chains[i].startColor = chainColor;
                _chains[i].endColor = WithAlpha(_ringColor, _ringColor.a * 0.55f * pulse);
            }
        }

        private void EnsureVisuals(float alpha)
        {
            EnsureLineMaterial();
            _root = EnsureChild("CharacterRootSeal");
            EnsureRing(_root, "WaistLockRing", _height * 0.46f, _radius, 0.035f, WithAlpha(_ringColor, _ringColor.a * alpha));
            EnsureRing(_root, "ChestLockRing", _height * 0.73f, _radius * 0.72f, 0.028f, WithAlpha(_ringColor, _ringColor.a * 0.62f * alpha));
            EnsureChains(_root, alpha);
        }

        private Transform EnsureChild(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
                return existing;

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            return child.transform;
        }

        private LineRenderer EnsureRing(
            Transform parent,
            string objectName,
            float y,
            float radius,
            float width,
            Color color)
        {
            Transform existing = parent.Find(objectName);
            GameObject view = existing != null ? existing.gameObject : new GameObject(objectName);
            if (existing == null)
                view.transform.SetParent(parent, false);

            view.transform.localPosition = Vector3.zero;
            var line = view.GetComponent<LineRenderer>();
            if (line == null)
                line = view.AddComponent<LineRenderer>();

            ConfigureLine(line, width, color);
            line.loop = true;
            line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float t = (float)i / RingSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, y, Mathf.Sin(t) * radius));
            }

            return line;
        }

        private void EnsureChains(Transform parent, float alpha)
        {
            if (_chains == null || _chains.Length != ChainCount)
                _chains = new LineRenderer[ChainCount];

            for (int i = 0; i < ChainCount; i++)
            {
                string objectName = "BindingChain" + i.ToString("00");
                Transform existing = parent.Find(objectName);
                GameObject view = existing != null ? existing.gameObject : new GameObject(objectName);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                var line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();

                ConfigureLine(line, 0.055f, WithAlpha(_chainColor, _chainColor.a * alpha));
                line.loop = false;
                line.positionCount = 3;

                float angle = (float)i / ChainCount * Mathf.PI * 2f;
                float nextAngle = angle + Mathf.PI / ChainCount;
                line.SetPosition(0, PointOnRing(angle, _height * 0.22f, _radius * 0.82f));
                line.SetPosition(1, PointOnRing(nextAngle, _height * 0.50f, _radius * 0.48f));
                line.SetPosition(2, PointOnRing(angle + Mathf.PI * 0.7f, _height * 0.83f, _radius * 0.66f));
                _chains[i] = line;
            }
        }

        private Vector3 PointOnRing(float angle, float y, float radius)
        {
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private void ConfigureLine(LineRenderer line, float width, Color color)
        {
            line.useWorldSpace = false;
            line.material = _lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
        }

        private void EnsureLineMaterial()
        {
            if (_lineMaterialOverride != null)
            {
                _lineMaterial = _lineMaterialOverride;
                return;
            }

            if (_runtimeLineMaterial == null)
                _runtimeLineMaterial = CreateLineMaterial();

            _lineMaterial = _runtimeLineMaterial;
        }

        private static Material CreateLineMaterial()
        {
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest Area Confinement Character Line Material", Color.white);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private float ResolveVisualAlpha(float ageSeconds)
        {
            if (!Application.isPlaying)
                return 1f;

            float ageFrames = Mathf.Max(0f, ageSeconds * CharacterTestDeterministicUnits.DefaultTickRate);
            if (_mode == CharacterTestAreaConfinementCharacterVfxMode.Release)
                return 1f - Mathf.Clamp01(ageFrames / Mathf.Max(1, _releaseFrames));
            if (_fadeInFrames <= 0)
                return 1f;

            return Mathf.Clamp01(ageFrames / _fadeInFrames);
        }

        private float ResolveModeScale(float ageSeconds)
        {
            if (!Application.isPlaying || _mode != CharacterTestAreaConfinementCharacterVfxMode.Release)
                return 1f;

            float ageFrames = Mathf.Max(0f, ageSeconds * CharacterTestDeterministicUnits.DefaultTickRate);
            float t = Mathf.Clamp01(ageFrames / Mathf.Max(1, _releaseFrames));
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, 1.28f, t);
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }

    public enum CharacterTestControlStatusVfxMode
    {
        Stunned = 0,
        Paralyzed = 1,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Control Status VFX View")]
    public sealed class CharacterTestControlStatusVfxView : MonoBehaviour
    {
        private const int RingSegments = 72;
        private const int SparkCount = 8;
        private const float DefaultHeight = 0f;
        private static readonly string[] SparkNames =
        {
            "Spark00",
            "Spark01",
            "Spark02",
            "Spark03",
            "Spark04",
            "Spark05",
            "Spark06",
            "Spark07",
        };

        [SerializeField] private CharacterTestControlStatusVfxMode _mode = CharacterTestControlStatusVfxMode.Stunned;
        [SerializeField][Min(0.1f)] private float _radius = 0.58f;
        [SerializeField][Min(0f)] private float _height = DefaultHeight;
        [SerializeField][Min(0f)] private float _rotationSpeedDegrees = 96f;
        [SerializeField] private Color _ringColor = new Color(1f, 0.88f, 0.24f, 0.92f);
        [SerializeField] private Color _sparkColor = new Color(0.36f, 0.9f, 1f, 0.94f);
        [SerializeField] private Material _lineMaterialOverride;

        private Material _runtimeLineMaterial;
        private Material _lineMaterial;
        private Transform _ringRoot;
        private Transform _sparkRoot;
        private LineRenderer _outerRing;
        private LineRenderer _innerRing;
        private LineRenderer[] _sparks;
        private float _ageSeconds;

        public static GameObject CreateRuntimePrefab(
            CharacterTestControlStatusVfxMode mode = CharacterTestControlStatusVfxMode.Stunned)
        {
            var prefab = new GameObject("CharacterTest_ControlStatus_" + mode + "_Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestControlStatusVfxView view = prefab.AddComponent<CharacterTestControlStatusVfxView>();
            view.ApplyModeDefaults(mode);
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void ApplyModeDefaults(CharacterTestControlStatusVfxMode mode)
        {
            _mode = mode;
            if (_mode == CharacterTestControlStatusVfxMode.Paralyzed)
            {
                _radius = 0.64f;
                _rotationSpeedDegrees = 128f;
                _ringColor = new Color(0.38f, 0.94f, 1f, 0.94f);
                _sparkColor = new Color(0.78f, 0.98f, 1f, 0.98f);
                return;
            }

            _radius = 0.58f;
            _rotationSpeedDegrees = 96f;
            _ringColor = new Color(1f, 0.88f, 0.24f, 0.92f);
            _sparkColor = new Color(0.36f, 0.9f, 1f, 0.94f);
        }

        public void RebuildVisuals()
        {
            EnsureVisuals(1f, 0f);
        }

        private void OnEnable()
        {
            _ageSeconds = 0f;
            EnsureVisuals(1f, 0f);
        }

        private void OnDestroy()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);

            _runtimeLineMaterial = null;
            _lineMaterial = null;
        }

        private void Update()
        {
            float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            _ageSeconds += Mathf.Max(0f, deltaTime);
            float pulse = 0.82f + 0.18f * (Mathf.Sin(_ageSeconds * 8.4f) + 1f) * 0.5f;
            EnsureVisuals(pulse, _ageSeconds);
            if (_ringRoot != null)
                _ringRoot.Rotate(0f, _rotationSpeedDegrees * deltaTime, 0f, Space.Self);
            if (_sparkRoot != null)
                _sparkRoot.Rotate(0f, -_rotationSpeedDegrees * 1.35f * deltaTime, 0f, Space.Self);
        }

        private void EnsureVisuals(float alphaScale, float time)
        {
            EnsureLineMaterial();
            _ringRoot = EnsureChild("ControlStatusRings");
            _sparkRoot = EnsureChild("ControlStatusSparks");

            Color ring = WithAlpha(_ringColor, _ringColor.a * alphaScale);
            Color halo = WithAlpha(_sparkColor, _sparkColor.a * alphaScale * 0.62f);
            _outerRing = EnsureRing(_ringRoot, "OuterRing", _outerRing, _radius, 0.045f, ring);
            _innerRing = EnsureRing(_ringRoot, "InnerPulse", _innerRing, _radius * 0.72f, 0.028f, halo);
            EnsureSparks(_sparkRoot, alphaScale, time);
        }

        private Transform EnsureChild(string childName)
        {
            if (string.Equals(childName, "ControlStatusRings", StringComparison.Ordinal)
                && _ringRoot != null)
            {
                _ringRoot.localPosition = new Vector3(0f, _height, 0f);
                return _ringRoot;
            }

            if (string.Equals(childName, "ControlStatusSparks", StringComparison.Ordinal)
                && _sparkRoot != null)
            {
                _sparkRoot.localPosition = new Vector3(0f, _height, 0f);
                return _sparkRoot;
            }

            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                existing.localPosition = new Vector3(0f, _height, 0f);
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, _height, 0f);
            return child.transform;
        }

        private LineRenderer EnsureRing(
            Transform parent,
            string objectName,
            LineRenderer existingLine,
            float radius,
            float width,
            Color color)
        {
            LineRenderer line = existingLine;
            if (line == null)
            {
                Transform existing = parent.Find(objectName);
                GameObject view = existing != null ? existing.gameObject : new GameObject(objectName);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                view.transform.localPosition = Vector3.zero;
                line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();
            }

            ConfigureLine(line, width, color);
            line.loop = true;
            line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float t = (float)i / RingSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius));
            }

            return line;
        }

        private void EnsureSparks(Transform parent, float alphaScale, float time)
        {
            if (_sparks == null || _sparks.Length != SparkCount)
                _sparks = new LineRenderer[SparkCount];

            for (int i = 0; i < SparkCount; i++)
            {
                LineRenderer line = _sparks[i];
                if (line == null)
                {
                    string name = SparkNames[i];
                    Transform existing = parent.Find(name);
                    GameObject view = existing != null ? existing.gameObject : new GameObject(name);
                    if (existing == null)
                        view.transform.SetParent(parent, false);

                    line = view.GetComponent<LineRenderer>();
                    if (line == null)
                        line = view.AddComponent<LineRenderer>();
                    _sparks[i] = line;
                }

                float angle = ((float)i / SparkCount * Mathf.PI * 2f) + time * (1.4f + i * 0.07f);
                float flicker = 0.56f + 0.44f * (Mathf.Sin(time * 11.2f + i * 1.73f) + 1f) * 0.5f;
                Color color = WithAlpha(_sparkColor, _sparkColor.a * alphaScale * flicker);
                ConfigureLine(line, i % 2 == 0 ? 0.035f : 0.024f, color);
                line.loop = false;
                line.positionCount = 3;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 center = radial * (_radius * (0.78f + 0.08f * flicker));
                float length = _mode == CharacterTestControlStatusVfxMode.Paralyzed ? 0.34f : 0.22f;
                float rise = _mode == CharacterTestControlStatusVfxMode.Paralyzed ? 0.16f : 0.08f;
                line.SetPosition(0, center - tangent * length * 0.5f);
                line.SetPosition(1, center + Vector3.up * rise);
                line.SetPosition(2, center + tangent * length * 0.5f);
            }
        }

        private void EnsureLineMaterial()
        {
            if (_lineMaterialOverride != null)
            {
                _lineMaterial = _lineMaterialOverride;
                return;
            }

            if (_runtimeLineMaterial == null)
                _runtimeLineMaterial = CreateLineMaterial();

            _lineMaterial = _runtimeLineMaterial;
        }

        private void ConfigureLine(LineRenderer line, float width, Color color)
        {
            line.useWorldSpace = false;
            line.material = _lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
        }

        private static Material CreateLineMaterial()
        {
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest Control Status Line Material", Color.white);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
