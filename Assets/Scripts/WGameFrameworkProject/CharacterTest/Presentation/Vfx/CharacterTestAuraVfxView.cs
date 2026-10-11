using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Guard Aura VFX View")]
    public sealed class CharacterTestAuraVfxView : MonoBehaviour
    {
        private const int CircleSegments = 128;
        private const int TickCount = 16;
        private const float DefaultRadius = 4f;

        [SerializeField][Min(0.1f)] private float _radius = DefaultRadius;
        [SerializeField][Min(0.01f)] private float _heightOffset = 0.08f;
        [SerializeField][Min(0f)] private float _rotationSpeedDegrees = 28f;
        [SerializeField] private Color _coreColor = new Color(0.35f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color _pulseColor = new Color(0.8f, 1f, 0.45f, 0.7f);

        private Material _lineMaterial;
        private Transform _coreRoot;
        private Transform _pulseRoot;
        private Transform _tickRoot;
        private LineRenderer _pulseRing;
        private bool _visualsReady;

        public static GameObject CreateRuntimePrefab()
        {
            var prefab = new GameObject("CharacterTest_GuardAuraVfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestAuraVfxView view = prefab.AddComponent<CharacterTestAuraVfxView>();
            view.EnsureVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        private void OnEnable()
        {
            EnsureVisuals();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                DestroyUnityObject(_lineMaterial);

            _lineMaterial = null;
            _visualsReady = false;
        }

        private void Update()
        {
            float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            if (_coreRoot != null)
                _coreRoot.Rotate(0f, _rotationSpeedDegrees * deltaTime, 0f, Space.Self);
            if (_tickRoot != null)
                _tickRoot.Rotate(0f, -_rotationSpeedDegrees * 1.45f * deltaTime, 0f, Space.Self);
            if (_pulseRoot != null)
            {
                float pulse = 1f + Mathf.Sin(time * 3.2f) * 0.045f;
                _pulseRoot.localScale = new Vector3(pulse, 1f, pulse);
            }

            if (_pulseRing != null)
            {
                Color pulseColor = _pulseColor;
                pulseColor.a = _pulseColor.a * (0.62f + 0.28f * (Mathf.Sin(time * 4.1f) + 1f));
                _pulseRing.startColor = pulseColor;
                _pulseRing.endColor = pulseColor;
            }
        }

        private void EnsureVisuals()
        {
            if (_visualsReady && _lineMaterial != null && _coreRoot != null
                && _pulseRoot != null && _tickRoot != null && _pulseRing != null)
                return;
            if (_lineMaterial == null)
                _lineMaterial = CreateLineMaterial();

            _coreRoot = EnsureChild("CoreRings");
            _pulseRoot = EnsureChild("PulseRing");
            _tickRoot = EnsureChild("RotatingTicks");

            EnsureRing(_coreRoot, "OuterCore", _radius, 0.055f, _coreColor);
            EnsureRing(_coreRoot, "InnerCore", _radius * 0.48f, 0.032f, WithAlpha(_pulseColor, 0.62f));
            _pulseRing = EnsureRing(_pulseRoot, "Pulse", _radius, 0.16f, _pulseColor);
            EnsureTicks(_tickRoot);
            _visualsReady = true;
        }

        private Transform EnsureChild(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                existing.localPosition = new Vector3(0f, _heightOffset, 0f);
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, _heightOffset, 0f);
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

        private void EnsureTicks(Transform parent)
        {
            float inner = _radius * 0.86f;
            float outer = _radius * 1.08f;
            for (int i = 0; i < TickCount; i++)
            {
                string name = "Tick" + i.ToString("00");
                Transform existing = parent.Find(name);
                GameObject view = existing != null ? existing.gameObject : new GameObject(name);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                var line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();

                ConfigureLine(line, 0.045f, WithAlpha(_coreColor, 0.82f));
                line.loop = false;
                line.positionCount = 2;
                float t = (float)i / TickCount * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                line.SetPosition(0, direction * inner);
                line.SetPosition(1, direction * outer);
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
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest Guard Aura Line Material", Color.white);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
