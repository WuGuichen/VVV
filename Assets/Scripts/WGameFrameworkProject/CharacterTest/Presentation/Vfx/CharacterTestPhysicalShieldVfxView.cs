using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Physical Shield VFX View")]
    public sealed class CharacterTestPhysicalShieldVfxView : MonoBehaviour
    {
        private const int ArcSegments = 40;
        private const int RibCount = 7;
        private const float DefaultWidth = 2.25f;
        private const float DefaultHeight = 2.15f;
        private const float DefaultBowDepth = 0.32f;

        [SerializeField][Min(0.25f)] private float _width = DefaultWidth;
        [SerializeField][Min(0.25f)] private float _height = DefaultHeight;
        [SerializeField][Min(0f)] private float _bowDepth = DefaultBowDepth;
        [SerializeField][Min(0f)] private float _pulseSpeed = 3.8f;
        [SerializeField] private Color _edgeColor = new Color(0.28f, 0.92f, 1f, 0.92f);
        [SerializeField] private Color _ribColor = new Color(0.85f, 1f, 1f, 0.52f);
        [SerializeField] private Material _lineMaterialOverride;

        private Material _lineMaterial;
        private Transform _arcRoot;
        private Transform _ribRoot;
        private LineRenderer _outerArc;
        private LineRenderer _innerArc;

        public static GameObject CreateRuntimePrefab()
        {
            var prefab = new GameObject("CharacterTest_PhysicalShieldVfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            CharacterTestPhysicalShieldVfxView view = prefab.AddComponent<CharacterTestPhysicalShieldVfxView>();
            view.EnsureVisuals();
            prefab.SetActive(false);
            return prefab;
        }

        public void RebuildVisuals()
        {
            ClearChildren();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            EnsureVisuals();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null && _lineMaterial != _lineMaterialOverride)
                DestroyUnityObject(_lineMaterial);

            _lineMaterial = null;
        }

        private void Update()
        {

            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * _pulseSpeed);
            float scale = 1f + pulse * 0.035f;
            if (_arcRoot != null)
                _arcRoot.localScale = new Vector3(scale, scale, 1f);

            if (_outerArc != null)
                SetLineColor(_outerArc, WithAlpha(_edgeColor, _edgeColor.a * (0.72f + pulse * 0.28f)));
            if (_innerArc != null)
                SetLineColor(_innerArc, WithAlpha(_ribColor, _ribColor.a * (0.68f + pulse * 0.22f)));
        }

        private void EnsureVisuals()
        {
            if (_lineMaterialOverride != null)
            {
                if (_lineMaterial != null && _lineMaterial != _lineMaterialOverride)
                    DestroyUnityObject(_lineMaterial);

                _lineMaterial = _lineMaterialOverride;
            }
            else if (_lineMaterial == null)
            {
                _lineMaterial = CreateLineMaterial();
            }

            _arcRoot = EnsureChild("ShieldArcs");
            _ribRoot = EnsureChild("ShieldRibs");
            _outerArc = EnsureHorizontalArc(_arcRoot, "OuterArc", 0f, _width, _height, 0.055f, _edgeColor);
            _innerArc = EnsureHorizontalArc(_arcRoot, "InnerArc", 0f, _width * 0.72f, _height * 0.66f, 0.035f, _ribColor);
            EnsureHorizontalArc(_arcRoot, "TopBand", _height * 0.34f, _width * 0.88f, _height * 0.16f, 0.03f, _ribColor);
            EnsureHorizontalArc(_arcRoot, "BottomBand", -_height * 0.34f, _width * 0.88f, _height * 0.16f, 0.03f, _ribColor);
            EnsureRibs(_ribRoot);
        }

        private Transform EnsureChild(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.transform;
        }

        private LineRenderer EnsureHorizontalArc(
            Transform parent,
            string objectName,
            float yOffset,
            float width,
            float height,
            float lineWidth,
            Color color)
        {
            Transform existing = parent.Find(objectName);
            GameObject view = existing != null ? existing.gameObject : new GameObject(objectName);
            if (existing == null)
                view.transform.SetParent(parent, false);

            var line = view.GetComponent<LineRenderer>();
            if (line == null)
                line = view.AddComponent<LineRenderer>();

            ConfigureLine(line, lineWidth, color);
            line.loop = false;
            line.positionCount = ArcSegments;
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            for (int i = 0; i < ArcSegments; i++)
            {
                float t = (float)i / (ArcSegments - 1);
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                float normalized = halfWidth <= 0f ? 0f : x / halfWidth;
                float y = yOffset + Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized)) * halfHeight;
                if (objectName == "BottomBand")
                    y = yOffset - Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized)) * halfHeight;
                float z = _bowDepth * (1f - Mathf.Abs(normalized));
                line.SetPosition(i, new Vector3(x, y, z));
            }

            return line;
        }

        private void EnsureRibs(Transform parent)
        {
            float halfWidth = _width * 0.5f;
            float halfHeight = _height * 0.5f;
            for (int i = 0; i < RibCount; i++)
            {
                string name = "Rib" + i.ToString("00");
                Transform existing = parent.Find(name);
                GameObject view = existing != null ? existing.gameObject : new GameObject(name);
                if (existing == null)
                    view.transform.SetParent(parent, false);

                var line = view.GetComponent<LineRenderer>();
                if (line == null)
                    line = view.AddComponent<LineRenderer>();

                ConfigureLine(line, 0.025f, WithAlpha(_ribColor, _ribColor.a * 0.72f));
                line.loop = false;
                line.positionCount = 2;
                float t = RibCount <= 1 ? 0.5f : (float)i / (RibCount - 1);
                float x = Mathf.Lerp(-halfWidth * 0.8f, halfWidth * 0.8f, t);
                float normalized = halfWidth <= 0f ? 0f : x / halfWidth;
                float topY = Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized)) * halfHeight;
                float bottomY = -topY * 0.82f;
                float z = _bowDepth * (1f - Mathf.Abs(normalized));
                line.SetPosition(0, new Vector3(x, bottomY, z));
                line.SetPosition(1, new Vector3(x, topY, z));
            }
        }

        private void ConfigureLine(LineRenderer line, float width, Color color)
        {
            line.useWorldSpace = false;
            line.material = _lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            SetLineColor(line, color);
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyUnityObject(transform.GetChild(i).gameObject);
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private static Material CreateLineMaterial()
        {
            var material = CharacterTestUnlitMaterials.CreateVfx("CharacterTest Physical Shield Line Material", Color.white);
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
