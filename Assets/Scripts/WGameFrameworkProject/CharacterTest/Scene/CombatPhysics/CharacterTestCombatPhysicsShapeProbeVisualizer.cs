using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Combat Physics Shape Probe Visualizer")]
    public sealed class CharacterTestCombatPhysicsShapeProbeVisualizer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private bool _enabled = true;
        [SerializeField] private CharacterTestCombatPhysicsSceneBridge _bridge;
        [SerializeField] private Transform _scanner;
        [SerializeField] private CharacterTestCombatPhysicsSphereMarker _sphereTarget;
        [SerializeField] private CharacterTestCombatPhysicsCapsuleMarker _capsuleTarget;
        [SerializeField] private Transform _sphereTargetTransform;
        [SerializeField] private Transform _capsuleTargetTransform;
        [SerializeField] private TextMesh _statusLabel;
        [SerializeField] private Vector3 _queryCenter = new Vector3(8.5f, 1.1f, 2.5f);
        [SerializeField] private Vector3 _queryHalfExtents = new Vector3(1.1f, 2f, 1.15f);
        [SerializeField] private float _sweepHalfDistance = 1.4f;
        [SerializeField] private float _sweepPeriodSeconds = 3f;
        [SerializeField] private float _runtimeRefreshInterval = 0.5f;
        [SerializeField] private Color _sphereIdleColor = new Color(0.25f, 0.7f, 0.45f, 1f);
        [SerializeField] private Color _sphereHitColor = new Color(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color _capsuleIdleColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        [SerializeField] private Color _capsuleHitColor = new Color(1f, 0.45f, 0.12f, 1f);
        [SerializeField] private Color _scannerIdleColor = new Color(0.15f, 0.55f, 1f, 0.85f);
        [SerializeField] private Color _scannerHitColor = new Color(0.1f, 1f, 0.9f, 1f);

        private readonly List<CombatQueryResult> _hits = new List<CombatQueryResult>();
        private CharacterTestCharacterPhysicsRuntime _runtime;
        private MaterialPropertyBlock _propertyBlock;
        private float _nextRuntimeRefreshTime;
        private int _queryId;
        private bool _hasSphereTargetIds;
        private bool _hasCapsuleTargetIds;
        private int _sphereBodyId;
        private int _sphereColliderId;
        private int _sphereLayer;
        private int _capsuleBodyId;
        private int _capsuleColliderId;
        private int _capsuleLayer;

        public bool IsEnabled => _enabled;
        public Vector3 QueryCenter => _queryCenter;
        public Vector3 QueryHalfExtents => _queryHalfExtents;
        public bool LastSphereHit { get; private set; }
        public bool LastCapsuleHit { get; private set; }
        public int LastHitCount { get; private set; }
        public Vector3 LastQueryCenter { get; private set; }
        public string LastSummary { get; private set; } = "not sampled";

        private void OnEnable()
        {
            AutoBind();
            RefreshRuntime(force: true, currentTime: 0f, scheduleNextRefresh: false);
            SampleAtWorldCenter(_queryCenter);
        }

        private void OnValidate()
        {
            _queryHalfExtents = MaxVector3(_queryHalfExtents, new Vector3(0.05f, 0.05f, 0.05f));
            _sweepHalfDistance = Mathf.Max(0f, _sweepHalfDistance);
            _sweepPeriodSeconds = Mathf.Max(0.1f, _sweepPeriodSeconds);
            _runtimeRefreshInterval = Mathf.Max(0.05f, _runtimeRefreshInterval);
            AutoBind();
        }

        private void Update()
        {
            if (!_enabled)
                return;

            AutoBind();
            float time = Application.isPlaying ? Time.time : 0f;
            RefreshRuntime(force: false, currentTime: time, scheduleNextRefresh: true);
            SampleAtWorldCenter(GetSweepCenter(time), time, scheduleNextRefresh: true);
        }

        public int SampleAtWorldCenter(Vector3 worldCenter)
        {
            return SampleAtWorldCenter(worldCenter, currentTime: 0f, scheduleNextRefresh: false);
        }

        private int SampleAtWorldCenter(Vector3 worldCenter, float currentTime, bool scheduleNextRefresh)
        {
            LastQueryCenter = worldCenter;
            LastSphereHit = false;
            LastCapsuleHit = false;
            LastHitCount = 0;

            if (!_enabled || _bridge == null)
            {
                ApplyVisuals();
                return 0;
            }

            if (_runtime == null)
                RefreshRuntime(force: true, currentTime, scheduleNextRefresh);

            if (_runtime == null || _runtime.World == null)
            {
                ApplyVisuals();
                return 0;
            }

            CombatPhysicsLayerMask mask = BuildLayerMask();
            var query = new CombatAabbQuery(
                Header(mask),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(worldCenter - _queryHalfExtents),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(worldCenter + _queryHalfExtents));

            _hits.Clear();
            LastHitCount = _runtime.World.Query(CombatPhysicsQuery.From(query), _hits);
            for (int i = 0; i < _hits.Count; i++)
            {
                CombatQueryResult hit = _hits[i];
                if (_hasSphereTargetIds
                    && hit.TargetBodyId.Value == _sphereBodyId
                    && hit.TargetColliderId.Value == _sphereColliderId
                    && hit.TargetShapeKind == CombatPhysicsShapeKind.Sphere)
                {
                    LastSphereHit = true;
                }

                if (_hasCapsuleTargetIds
                    && hit.TargetBodyId.Value == _capsuleBodyId
                    && hit.TargetColliderId.Value == _capsuleColliderId
                    && hit.TargetShapeKind == CombatPhysicsShapeKind.Capsule)
                {
                    LastCapsuleHit = true;
                }
            }

            ApplyVisuals();
            return LastHitCount;
        }

        private void RefreshRuntime(bool force, float currentTime, bool scheduleNextRefresh)
        {
            if (_bridge == null)
                return;

            if (!force && Application.isPlaying && currentTime < _nextRuntimeRefreshTime)
                return;

            _runtime = _bridge.BuildRuntime();
            if (Application.isPlaying && scheduleNextRefresh)
                _nextRuntimeRefreshTime = currentTime + _runtimeRefreshInterval;
        }

        private CombatPhysicsLayerMask BuildLayerMask()
        {
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.None;
            bool hasLayer = false;
            if (_hasSphereTargetIds)
            {
                mask = CombatPhysicsLayerMask.FromLayer(Mathf.Clamp(_sphereLayer, 0, 31));
                hasLayer = true;
            }

            if (_hasCapsuleTargetIds)
            {
                int layer = Mathf.Clamp(_capsuleLayer, 0, 31);
                mask = hasLayer ? mask.AddLayer(layer) : CombatPhysicsLayerMask.FromLayer(layer);
                hasLayer = true;
            }

            return hasLayer ? mask : CombatPhysicsLayerMask.All;
        }

        private CombatQueryHeader Header(CombatPhysicsLayerMask mask)
        {
            _queryId++;
            return new CombatQueryHeader(
                _queryId,
                CombatQueryKind.Aabb,
                new CombatEntityId(910040),
                traceId: 40,
                actionId: 40,
                sourceOrder: 40,
                mask);
        }

        private Vector3 GetSweepCenter(float time)
        {
            if (!Application.isPlaying || _sweepHalfDistance <= 0f)
                return _queryCenter;

            float phase = Mathf.Sin((time / _sweepPeriodSeconds) * Mathf.PI * 2f);
            return _queryCenter + Vector3.right * (phase * _sweepHalfDistance);
        }

        private void ApplyVisuals()
        {
            ApplyRendererColor(
                GetTargetRenderer(_sphereTarget, _sphereTargetTransform),
                LastSphereHit ? _sphereHitColor : _sphereIdleColor);
            ApplyRendererColor(
                GetTargetRenderer(_capsuleTarget, _capsuleTargetTransform),
                LastCapsuleHit ? _capsuleHitColor : _capsuleIdleColor);
            ApplyRendererColor(
                _scanner == null ? null : _scanner.GetComponent<Renderer>(),
                (LastSphereHit || LastCapsuleHit) ? _scannerHitColor : _scannerIdleColor);

            if (_scanner != null)
            {
                Vector3 scannerPosition = _scanner.position;
                scannerPosition.x = LastQueryCenter.x;
                scannerPosition.z = LastQueryCenter.z;
                _scanner.position = scannerPosition;
            }

            LastSummary = CreateSummary();
            if (_statusLabel != null)
                _statusLabel.text = LastSummary;
        }

        private string CreateSummary()
        {
            if (LastSphereHit && LastCapsuleHit)
                return "Query Hit: Sphere + Capsule";

            if (LastSphereHit)
                return "Query Hit: Sphere";

            if (LastCapsuleHit)
                return "Query Hit: Capsule";

            return "Query Hit: none";
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void AutoBind()
        {
            if (_bridge == null)
                _bridge = GetComponentInParent<CharacterTestCombatPhysicsSceneBridge>();

            if (_bridge == null)
            {
                CharacterTestCombatPhysicsSceneBridge[] bridges =
                    FindObjectsByType<CharacterTestCombatPhysicsSceneBridge>(UnityEngine.FindObjectsInactive.Exclude);
                if (bridges.Length > 0)
                    _bridge = bridges[0];
            }

            if (_sphereTarget == null)
                _sphereTarget = FindChildMarker<CharacterTestCombatPhysicsSphereMarker>("PhysicsProbe_SphereTarget");

            if (_capsuleTarget == null)
                _capsuleTarget = FindChildMarker<CharacterTestCombatPhysicsCapsuleMarker>("PhysicsProbe_CapsuleTarget");

            if (_sphereTargetTransform == null)
                _sphereTargetTransform = FindChildTransform("PhysicsProbe_SphereTarget");

            if (_capsuleTargetTransform == null)
                _capsuleTargetTransform = FindChildTransform("PhysicsProbe_CapsuleTarget");

            RefreshTargetIds();

            if (_scanner == null)
            {
                Transform child = transform.Find("PhysicsProbe_QueryScanner");
                if (child != null)
                    _scanner = child;
            }

            if (_statusLabel == null)
            {
                Transform child = transform.Find("CharacterTest_Label_ColliderShapes");
                if (child != null)
                    _statusLabel = child.GetComponent<TextMesh>();
            }
        }

        private T FindChildMarker<T>(string markerName)
            where T : Component
        {
            T[] markers = GetComponentsInChildren<T>(includeInactive: true);
            for (int i = 0; i < markers.Length; i++)
            {
                T marker = markers[i];
                if (marker != null && marker.name == markerName)
                    return marker;
            }

            return null;
        }

        private Transform FindChildTransform(string targetName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target != null && target.name == targetName)
                    return target;
            }

            return null;
        }

        private void RefreshTargetIds()
        {
            _hasSphereTargetIds = false;
            _hasCapsuleTargetIds = false;

            if (_sphereTarget != null)
            {
                _sphereTargetTransform = _sphereTarget.transform;
                _sphereBodyId = _sphereTarget.BodyId;
                _sphereColliderId = _sphereTarget.ColliderId;
                _sphereLayer = _sphereTarget.CombatLayer;
                _hasSphereTargetIds = true;
            }

            if (_capsuleTarget != null)
            {
                _capsuleTargetTransform = _capsuleTarget.transform;
                _capsuleBodyId = _capsuleTarget.BodyId;
                _capsuleColliderId = _capsuleTarget.ColliderId;
                _capsuleLayer = _capsuleTarget.CombatLayer;
                _hasCapsuleTargetIds = true;
            }

            if (_hasSphereTargetIds && _hasCapsuleTargetIds)
                return;

            CharacterTestWorldObjectSceneSource source = CharacterTestWorldObjectSceneSource.FindFor(transform);
            if (source == null || source.StaticColliderInstanceCount <= 0)
                return;

            CharacterTestWorldObjectStaticColliderSpec[] specs = source.CollectStaticColliderSpecs(out _);
            for (int i = 0; i < specs.Length; i++)
            {
                CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                if (spec.Transform == null)
                    continue;

                if (!_hasSphereTargetIds
                    && spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere
                    && spec.Transform.name == "PhysicsProbe_SphereTarget")
                {
                    _sphereTargetTransform = spec.Transform;
                    _sphereBodyId = spec.BodyId;
                    _sphereColliderId = spec.ColliderId;
                    _sphereLayer = spec.Layer;
                    _hasSphereTargetIds = _sphereBodyId > 0 && _sphereColliderId > 0;
                }

                if (!_hasCapsuleTargetIds
                    && spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule
                    && spec.Transform.name == "PhysicsProbe_CapsuleTarget")
                {
                    _capsuleTargetTransform = spec.Transform;
                    _capsuleBodyId = spec.BodyId;
                    _capsuleColliderId = spec.ColliderId;
                    _capsuleLayer = spec.Layer;
                    _hasCapsuleTargetIds = _capsuleBodyId > 0 && _capsuleColliderId > 0;
                }
            }
        }

        private static Renderer GetTargetRenderer(Component marker, Transform fallback)
        {
            if (marker != null)
                return marker.GetComponent<Renderer>();

            return fallback == null ? null : fallback.GetComponent<Renderer>();
        }

        private void OnDrawGizmos()
        {
            if (!_enabled)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = (LastSphereHit || LastCapsuleHit) ? _scannerHitColor : _scannerIdleColor;
            Gizmos.DrawWireCube(LastQueryCenter == Vector3.zero ? _queryCenter : LastQueryCenter, _queryHalfExtents * 2f);
            Gizmos.color = previous;
        }

        private static Vector3 MaxVector3(Vector3 value, Vector3 min)
        {
            return new Vector3(
                Mathf.Max(value.x, min.x),
                Mathf.Max(value.y, min.y),
                Mathf.Max(value.z, min.z));
        }
    }
}
