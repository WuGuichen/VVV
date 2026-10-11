using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestAreaConfinementVfxMode
    {
        Zone = 0,
        Telegraph = 1,
        TelegraphCancel = 2,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Area Confinement VFX View")]
    public sealed class CharacterTestAreaConfinementVfxView : MonoBehaviour
    {
        private const int CircleSegments = 128;
        private const int RuneTickCount = 24;
        private const int PillarCount = 6;
        private const float DefaultRadius = 2.5f;

        [SerializeField][Min(0.1f)] private float _radius = DefaultRadius;
        [SerializeField][Min(0.01f)] private float _heightOffset = 0.06f;
        [SerializeField][Min(0.1f)] private float _pillarHeight = 2.35f;
        [SerializeField][Min(0f)] private float _rotationSpeedDegrees = 18f;
        [SerializeField] private CharacterTestAreaConfinementVfxMode _mode = CharacterTestAreaConfinementVfxMode.Zone;
        [SerializeField][Min(1)] private int _lifetimeFrames = (int)CharacterTestAreaConfinementIds.ZoneDurationFrames;
        [SerializeField][Min(0)] private int _fadeInFrames = CharacterTestAreaConfinementIds.VfxFadeInFrames;
        [SerializeField][Min(0)] private int _fadeOutFrames = CharacterTestAreaConfinementIds.VfxFadeOutFrames;
        [SerializeField] private Color _coreColor = new Color(0.34f, 0.9f, 1f, 0.92f);
        [SerializeField] private Color _runeColor = new Color(1f, 0.86f, 0.36f, 0.86f);
        [SerializeField] private Color _pillarColor = new Color(0.62f, 0.96f, 1f, 0.68f);
        [SerializeField] private Material _lineMaterialOverride;

        private Material _runtimeLineMaterial;
        private Material _lineMaterial;
        private Transform _outerRoot;
        private Transform _innerRoot;
        private Transform _runeRoot;
        private Transform _pillarRoot;
        private LineRenderer[] _pillars;
        private float _ageSeconds;

        public static GameObject CreateRuntimePrefab(
            CharacterTestAreaConfinementVfxMode mode = CharacterTestAreaConfinementVfxMode.Zone)
        {
            var prefab = new GameObject("CharacterTest_AreaConfinement" + mode + "Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestAreaConfinementVfxView view = prefab.AddComponent<CharacterTestAreaConfinementVfxView>();
            view.ApplyModeDefaults(mode);
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void ApplyModeDefaults(CharacterTestAreaConfinementVfxMode mode)
        {
            _mode = mode;
            switch (_mode)
            {
                case CharacterTestAreaConfinementVfxMode.Telegraph:
                    _lifetimeFrames = CharacterTestAreaConfinementIds.TelegraphFrames;
                    _fadeInFrames = CharacterTestAreaConfinementIds.TelegraphFrames;
                    _fadeOutFrames = 0;
                    _pillarHeight = 0.85f;
                    _rotationSpeedDegrees = 28f;
                    _coreColor = new Color(0.38f, 0.98f, 1f, 0.72f);
                    _runeColor = new Color(1f, 0.9f, 0.42f, 0.72f);
                    _pillarColor = new Color(0.62f, 0.96f, 1f, 0.32f);
                    break;
                case CharacterTestAreaConfinementVfxMode.TelegraphCancel:
                    _lifetimeFrames = CharacterTestAreaConfinementIds.TelegraphCancelFrames;
                    _fadeInFrames = 0;
                    _fadeOutFrames = CharacterTestAreaConfinementIds.TelegraphCancelFrames;
                    _pillarHeight = 0.45f;
                    _rotationSpeedDegrees = -42f;
                    _coreColor = new Color(0.3f, 0.8f, 1f, 0.62f);
                    _runeColor = new Color(1f, 0.72f, 0.28f, 0.56f);
                    _pillarColor = new Color(0.42f, 0.92f, 1f, 0.22f);
                    break;
                default:
                    _lifetimeFrames = (int)CharacterTestAreaConfinementIds.ZoneDurationFrames;
                    _fadeInFrames = CharacterTestAreaConfinementIds.VfxFadeInFrames;
                    _fadeOutFrames = CharacterTestAreaConfinementIds.VfxFadeOutFrames;
                    _pillarHeight = 2.35f;
                    _rotationSpeedDegrees = 18f;
                    _coreColor = new Color(0.34f, 0.9f, 1f, 0.92f);
                    _runeColor = new Color(1f, 0.86f, 0.36f, 0.86f);
                    _pillarColor = new Color(0.62f, 0.96f, 1f, 0.68f);
                    break;
            }
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
            float modeScale = ResolveModeScale(_ageSeconds);
            EnsureVisuals(alpha);
            if (_outerRoot != null)
            {
                _outerRoot.localScale = new Vector3(modeScale, 1f, modeScale);
                _outerRoot.Rotate(0f, _rotationSpeedDegrees * deltaTime, 0f, Space.Self);
            }
            if (_runeRoot != null)
            {
                _runeRoot.localScale = new Vector3(modeScale, 1f, modeScale);
                _runeRoot.Rotate(0f, -_rotationSpeedDegrees * 1.35f * deltaTime, 0f, Space.Self);
            }
            if (_innerRoot != null)
            {
                float pulse = 1f + Mathf.Sin(time * 3.8f) * 0.035f;
                _innerRoot.localScale = new Vector3(pulse * modeScale, 1f, pulse * modeScale);
            }
            if (_pillarRoot != null)
                _pillarRoot.localScale = new Vector3(modeScale, 1f, modeScale);

            if (_pillars == null)
                return;

            float pillarAlpha = _pillarColor.a * alpha * (0.72f + 0.22f * (Mathf.Sin(time * 4.4f) + 1f) * 0.5f);
            Color pillarColor = WithAlpha(_pillarColor, pillarAlpha);
            for (int i = 0; i < _pillars.Length; i++)
            {
                LineRenderer pillar = _pillars[i];
                if (pillar == null)
                    continue;

                pillar.startColor = pillarColor;
                pillar.endColor = WithAlpha(_coreColor, pillarAlpha * 0.42f);
            }
        }

        private void EnsureVisuals(float alpha)
        {
            EnsureLineMaterial();
            _outerRoot = EnsureChild("OuterLockRings", _heightOffset);
            _innerRoot = EnsureChild("PulseSeal", _heightOffset + 0.01f);
            _runeRoot = EnsureChild("RuneTicks", _heightOffset + 0.02f);
            _pillarRoot = EnsureChild("BindingPillars", _heightOffset);

            EnsureRing(_outerRoot, "OuterBoundary", _radius, 0.07f, WithAlpha(_coreColor, _coreColor.a * alpha));
            EnsureRing(_outerRoot, "RuneBoundary", _radius * 0.88f, 0.032f, WithAlpha(_runeColor, _runeColor.a * 0.72f * alpha));
            EnsureRing(_innerRoot, "InnerSeal", _radius * 0.48f, 0.046f, WithAlpha(_coreColor, _coreColor.a * 0.58f * alpha));
            EnsureRuneTicks(_runeRoot, alpha);
            EnsurePillars(_pillarRoot, alpha);
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

        private Transform EnsureChild(string childName, float heightOffset)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                existing.localPosition = new Vector3(0f, heightOffset, 0f);
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            return child.transform;
        }

        private LineRenderer EnsureRing(
            Transform parent,
            string objectName,
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
            line.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float t = (float)i / CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius));
            }

            return line;
        }

        private void EnsureRuneTicks(Transform parent, float alpha)
        {
            float inner = _radius * 0.66f;
            float outer = _radius * 0.82f;
            for (int i = 0; i < RuneTickCount; i++)
            {
                string name = "RuneTick" + i.ToString("00");
                Transform existing = parent.Find(name);
                GameObject view = existing != null ? existing.gameObject : new GameObject(name);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                var line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();

                ConfigureLine(line, i % 3 == 0 ? 0.055f : 0.035f, WithAlpha(_runeColor, _runeColor.a * alpha));
                line.loop = false;
                line.positionCount = 2;
                float t = (float)i / RuneTickCount * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                line.SetPosition(0, direction * inner);
                line.SetPosition(1, direction * outer);
            }
        }

        private void EnsurePillars(Transform parent, float alpha)
        {
            if (_pillars == null || _pillars.Length != PillarCount)
                _pillars = new LineRenderer[PillarCount];

            float pillarRadius = _radius * 0.92f;
            for (int i = 0; i < PillarCount; i++)
            {
                string name = "BindingPillar" + i.ToString("00");
                Transform existing = parent.Find(name);
                GameObject view = existing != null ? existing.gameObject : new GameObject(name);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                var line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();

                ConfigureLine(line, 0.12f, WithAlpha(_pillarColor, _pillarColor.a * alpha));
                line.loop = false;
                line.positionCount = 2;
                float t = (float)i / PillarCount * Mathf.PI * 2f;
                Vector3 basePoint = new Vector3(Mathf.Cos(t) * pillarRadius, 0f, Mathf.Sin(t) * pillarRadius);
                line.SetPosition(0, basePoint);
                line.SetPosition(1, basePoint + Vector3.up * _pillarHeight);
                _pillars[i] = line;
            }
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
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest Area Confinement Line Material", Color.white);
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
            if (_mode == CharacterTestAreaConfinementVfxMode.TelegraphCancel)
                return 1f - Mathf.Clamp01(ageFrames / Mathf.Max(1, _lifetimeFrames));

            float alpha = 1f;
            if (_fadeInFrames > 0)
                alpha = Mathf.Min(alpha, Mathf.Clamp01(ageFrames / _fadeInFrames));
            if (_fadeOutFrames > 0 && _lifetimeFrames > 0)
            {
                float remaining = _lifetimeFrames - ageFrames;
                alpha = Mathf.Min(alpha, Mathf.Clamp01(remaining / _fadeOutFrames));
            }

            return alpha;
        }

        private float ResolveModeScale(float ageSeconds)
        {
            if (!Application.isPlaying)
                return 1f;

            float ageFrames = Mathf.Max(0f, ageSeconds * CharacterTestDeterministicUnits.DefaultTickRate);
            float t = Mathf.Clamp01(ageFrames / Mathf.Max(1, _lifetimeFrames));
            t = t * t * (3f - 2f * t);
            if (_mode == CharacterTestAreaConfinementVfxMode.Telegraph)
                return Mathf.Lerp(0.72f, 1f, t);
            if (_mode == CharacterTestAreaConfinementVfxMode.TelegraphCancel)
                return Mathf.Lerp(1f, 0.28f, t);

            return 1f;
        }

        private static void DestroyUnityObject(Object value)
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
