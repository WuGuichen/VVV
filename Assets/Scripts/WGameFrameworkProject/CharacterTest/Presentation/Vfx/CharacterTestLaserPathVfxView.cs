using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Laser Path VFX View")]
    public sealed class CharacterTestLaserPathVfxView : MonoBehaviour, IUnityVfxInstanceReceiver
    {
        private const float MillimetersToMeters = 0.001f;
        private const float MinimumLengthMeters = 0.1f;

        [SerializeField][Min(MinimumLengthMeters)] private float _lengthMeters = 1f;
        [SerializeField][Min(0.001f)] private float _coreWidth = 0.035f;
        [SerializeField][Min(0.001f)] private float _haloWidth = 0.16f;
        [SerializeField][Min(0.001f)] private float _endpointRadius = 0.22f;
        [SerializeField] private Color _coreColor = new Color(0.93f, 1f, 1f, 0.98f);
        [SerializeField] private Color _haloColor = new Color(0.24f, 0.85f, 1f, 0.36f);
        [SerializeField] private Color _endpointColor = new Color(0.84f, 1f, 1f, 0.72f);
        [SerializeField] private Material _lineMaterialOverride;

        private LineRenderer _core;
        private LineRenderer _halo;
        private Transform _endpointRoot;
        private LineRenderer _endpointHorizontal;
        private LineRenderer _endpointVertical;
        private Material _runtimeLineMaterial;
        private Material _lineMaterial;

        public static GameObject CreateRuntimePrefab()
        {
            var prefab = new GameObject("CharacterTest_LaserLancePathVfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestLaserPathVfxView view = prefab.AddComponent<CharacterTestLaserPathVfxView>();
            view.RebuildVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public float LengthMeters => _lengthMeters;

        public void OnUnityVfxInstanceSpawned(VfxInstanceSnapshot instance)
        {
            if (instance != null && instance.Parameters.Value0 > 0)
                _lengthMeters = Mathf.Max(MinimumLengthMeters, instance.Parameters.Value0 * MillimetersToMeters);

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

        private void OnDestroy()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);

            _runtimeLineMaterial = null;
            _lineMaterial = null;
        }

private void EnsureVisuals()
        {
            _lengthMeters = Mathf.Max(MinimumLengthMeters, _lengthMeters);
            Material material = ResolveLineMaterial();
            _core = EnsureLine("LaserCore", _core, _coreWidth, _coreColor, material);
            _halo = EnsureLine("LaserHalo", _halo, _haloWidth, _haloColor, material);
            ApplyPath(_core, _lengthMeters);
            ApplyPath(_halo, _lengthMeters);

            if (_endpointRoot == null)
            {
                _endpointRoot = new GameObject("LaserEndpointFlash").transform;
                _endpointRoot.SetParent(transform, false);
            }

            _endpointRoot.localPosition = new Vector3(0f, 0f, _lengthMeters);
            _endpointHorizontal = EnsureEndpointLine("EndpointHorizontal", _endpointHorizontal, material);
            _endpointVertical = EnsureEndpointLine("EndpointVertical", _endpointVertical, material);
            ApplyEndpoint(_endpointHorizontal, horizontal: true);
            ApplyEndpoint(_endpointVertical, horizontal: false);
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

        private LineRenderer EnsureEndpointLine(string name, LineRenderer existing, Material material)
        {
            if (existing == null)
            {
                Transform child = _endpointRoot.Find(name);
                GameObject go = child != null ? child.gameObject : new GameObject(name);
                go.transform.SetParent(_endpointRoot, false);
                existing = go.GetComponent<LineRenderer>();
                if (existing == null)
                    existing = go.AddComponent<LineRenderer>();
            }

            ConfigureLine(existing, _haloWidth * 0.35f, _endpointColor, material);
            return existing;
        }

        private static void ApplyPath(LineRenderer line, float lengthMeters)
        {
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, new Vector3(0f, 0f, lengthMeters));
        }

        private void ApplyEndpoint(LineRenderer line, bool horizontal)
        {
            line.positionCount = 2;
            line.SetPosition(0, horizontal
                ? new Vector3(-_endpointRadius, 0f, 0f)
                : new Vector3(0f, -_endpointRadius, 0f));
            line.SetPosition(1, horizontal
                ? new Vector3(_endpointRadius, 0f, 0f)
                : new Vector3(0f, _endpointRadius, 0f));
        }

        private static void ConfigureLine(LineRenderer line, float width, Color color, Material material)
        {
            line.useWorldSpace = false;
            line.loop = false;
            line.enabled = true;
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
            line.numCapVertices = 6;
            line.numCornerVertices = 2;
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

            _runtimeLineMaterial = CharacterTestUnlitMaterials.CreateVfx("CharacterTest_LaserPath_RuntimeLine", Color.white);
            _runtimeLineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial = _runtimeLineMaterial;
            return _lineMaterial;
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
