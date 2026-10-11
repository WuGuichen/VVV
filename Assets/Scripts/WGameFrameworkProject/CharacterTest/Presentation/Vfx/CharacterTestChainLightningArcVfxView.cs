using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Chain Lightning Arc VFX View")]
    public sealed class CharacterTestChainLightningArcVfxView : MonoBehaviour, IUnityVfxInstanceReceiver
    {
        private const float MillimetersToMeters = 0.001f;
        private const float MinimumLengthMeters = 0.1f;
        private const int PointCount = 7;

        [SerializeField][Min(MinimumLengthMeters)] private float _lengthMeters = 1f;
        [SerializeField][Min(0.001f)] private float _coreWidth = 0.055f;
        [SerializeField][Min(0.001f)] private float _haloWidth = 0.22f;
        [SerializeField][Min(0f)] private float _jitterMeters = 0.18f;
        [SerializeField] private Color _coreColor = new Color(0.95f, 1f, 1f, 1f);
        [SerializeField] private Color _haloColor = new Color(0.28f, 0.65f, 1f, 0.45f);
        [SerializeField] private Material _lineMaterialOverride;

        private readonly Vector3[] _points = new Vector3[PointCount];
        private LineRenderer _core;
        private LineRenderer _halo;
        private Material _runtimeLineMaterial;
        private Material _lineMaterial;
        private int _segmentSeed;

        public static GameObject CreateRuntimePrefab()
        {
            var prefab = new GameObject("CharacterTest_ChainLightningArcVfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestChainLightningArcVfxView view = prefab.AddComponent<CharacterTestChainLightningArcVfxView>();
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public float LengthMeters => _lengthMeters;

        public void OnUnityVfxInstanceSpawned(VfxInstanceSnapshot instance)
        {
            if (instance != null)
            {
                if (instance.Parameters.Value0 > 0)
                    _lengthMeters = Mathf.Max(MinimumLengthMeters, instance.Parameters.Value0 * MillimetersToMeters);
                _segmentSeed = instance.Parameters.Value2;
            }

            RebuildVisuals();
        }

        public void OnUnityVfxInstanceReleased(VfxInstanceId instanceId)
        {
        }

        public void RebuildVisuals()
        {
            EnsureVisuals();
        }

        private void OnEnable()
        {
            EnsureVisuals();
        }

        private void OnDisable()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);

            _runtimeLineMaterial = null;
            _lineMaterial = null;
        }

        private void Update()
        {
            EnsureVisuals();
        }

        private void EnsureVisuals()
        {
            _lengthMeters = Mathf.Max(MinimumLengthMeters, _lengthMeters);
            Material material = ResolveLineMaterial();
            _core = EnsureLine("LightningCore", _core, _coreWidth, _coreColor, material);
            _halo = EnsureLine("LightningHalo", _halo, _haloWidth, _haloColor, material);
            BuildPoints();
            ApplyPath(_core);
            ApplyPath(_halo);
        }

        private LineRenderer EnsureLine(
            string name,
            LineRenderer existing,
            float width,
            Color color,
            Material material)
        {
            if (existing == null)
            {
                Transform child = transform.Find(name);
                GameObject go = child != null ? child.gameObject : new GameObject(name);
                go.transform.SetParent(transform, false);
                existing = go.GetComponent<LineRenderer>();
                if (existing == null)
                    existing = go.AddComponent<LineRenderer>();
            }

            ConfigureLine(existing, width, color, material);
            return existing;
        }

        private void BuildPoints()
        {
            for (int i = 0; i < PointCount; i++)
            {
                float t = i / (float)(PointCount - 1);
                float z = _lengthMeters * t;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float x = (PseudoNoise(i, _segmentSeed) - 0.5f) * _jitterMeters * envelope;
                float y = (PseudoNoise(i + 11, _segmentSeed) - 0.5f) * _jitterMeters * 0.55f * envelope;
                _points[i] = new Vector3(x, y, z);
            }

            _points[0] = Vector3.zero;
            _points[PointCount - 1] = new Vector3(0f, 0f, _lengthMeters);
        }

        private void ApplyPath(LineRenderer line)
        {
            line.positionCount = PointCount;
            for (int i = 0; i < PointCount; i++)
                line.SetPosition(i, _points[i]);
        }

        private static void ConfigureLine(LineRenderer line, float width, Color color, Material material)
        {
            line.useWorldSpace = false;
            line.loop = false;
            line.enabled = true;
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width * 0.35f;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.2f);
            line.numCapVertices = 4;
            line.numCornerVertices = 1;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            if (_lineMaterialOverride != null)
            {
                _lineMaterial = _lineMaterialOverride;
                return _lineMaterial;
            }

            _runtimeLineMaterial = CharacterTestUnlitMaterials.CreateVfx("CharacterTest_ChainLightning_RuntimeLine", Color.white);
            _runtimeLineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial = _runtimeLineMaterial;
            return _lineMaterial;
        }

        private static float PseudoNoise(int index, int seed)
        {
            unchecked
            {
                uint value = ((uint)index * 747796405u) ^ ((uint)seed * 2891336453u);
                value = (value ^ (value >> 16)) * 2246822519u;
                value = (value ^ (value >> 13)) * 3266489917u;
                value ^= value >> 16;
                return (value & 0xFFFF) / 65535f;
            }
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
