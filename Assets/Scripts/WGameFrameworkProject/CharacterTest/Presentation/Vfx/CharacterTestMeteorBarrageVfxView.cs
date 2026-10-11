using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Selects one runtime-created visual variant for the CharacterTest Meteor Barrage effect.
    /// </summary>
    public enum CharacterTestMeteorBarrageVfxMode
    {
        Flight = 0,
        Charge = 1,
        Telegraph = 2,
        Impact = 3,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Meteor Barrage VFX View")]
    /// <summary>
    /// Runtime-built Meteor Barrage VFX view. AllocByDesign during initialization; Update is NoAlloc.
    /// </summary>
    public sealed class CharacterTestMeteorBarrageVfxView : MonoBehaviour
    {
        private const int RingSegments = 96;
        private const int CrackCount = 6;
        private const int TrailLineCount = 3;
        private const int TelegraphRayCount = 0;
        private const int ImpactRayCount = 0;
        private const float DefaultAreaRadius = 2f;
        private const string PropShaderName = "WGame/PropStylizedLit";

        private static readonly string[] CrackNames =
        {
            "LavaCrack00",
            "LavaCrack01",
            "LavaCrack02",
            "LavaCrack03",
            "LavaCrack04",
            "LavaCrack05",
        };

        private static readonly string[] TrailNames =
        {
            "FireTrail00",
            "FireTrail01",
            "FireTrail02",
        };

        private static readonly ParticleSystem.Burst[] EmptyBursts = new ParticleSystem.Burst[0];

        private readonly ParticleSystem.Burst[] _singleBurst = new ParticleSystem.Burst[1];
        [SerializeField] private CharacterTestMeteorBarrageVfxMode _mode =
            CharacterTestMeteorBarrageVfxMode.Flight;
        [SerializeField][Min(0.1f)] private float _radius = DefaultAreaRadius;
        [SerializeField][Min(0.01f)] private float _rockRadius = 0.52f;
        [SerializeField][Min(0.1f)] private float _trailLength = 2.35f;
        [SerializeField][Min(1)] private int _lifetimeFrames = CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames;
        [SerializeField] private Color _rockColor = new Color(0.06f, 0.052f, 0.045f, 1f);
        [SerializeField] private Color _lavaColor = new Color(1f, 0.25f, 0.035f, 0.86f);
        [SerializeField] private Color _fireColor = new Color(1f, 0.52f, 0.08f, 0.72f);
        [SerializeField] private Color _smokeColor = new Color(0.18f, 0.15f, 0.12f, 0.48f);
        [SerializeField] private Material _lineMaterialOverride = null;
        [SerializeField] private Material _rockMaterialOverride = null;
        [SerializeField] private Material _particleMaterialOverride = null;

        private readonly LineRenderer[] _cracks = new LineRenderer[CrackCount];
        private readonly LineRenderer[] _trails = new LineRenderer[TrailLineCount];
        private readonly LineRenderer[] _telegraphRays = new LineRenderer[TelegraphRayCount];
        private readonly LineRenderer[] _impactRays = new LineRenderer[ImpactRayCount];

        private Material _runtimeLineMaterial;
        private Material _runtimeRockMaterial;
        private Material _runtimeParticleMaterial;
        private Material _lineMaterial;
        private Material _rockMaterial;
        private Material _particleMaterial;
        private Transform _rockRoot;
        private Transform _trailRoot;
        private Transform _telegraphRoot;
        private Transform _impactRoot;
        private Transform _chargeRoot;
        private LineRenderer _telegraphOuterRing;
        private LineRenderer _telegraphInnerRing;
        private LineRenderer _telegraphHeatRing;
        private LineRenderer _impactShockRing;
        private LineRenderer _impactSmokeRing;
        private LineRenderer _chargeRing;
        private ParticleSystem _fireParticles;
        private ParticleSystem _emberParticles;
        private ParticleSystem _smokeParticles;
        private ParticleSystem _impactDebrisParticles;
        private MeshRenderer _rockRenderer;
        private float _ageSeconds;
        private bool _visualsReady;

        /// <summary>
        /// Creates an inactive runtime prefab for the memory resource provider. AllocByDesign during initialization.
        /// </summary>
        public static GameObject CreateRuntimePrefab(
            CharacterTestMeteorBarrageVfxMode mode = CharacterTestMeteorBarrageVfxMode.Flight)
        {
            var prefab = new GameObject("CharacterTest_MeteorBarrage_" + mode + "_Vfx");
            prefab.hideFlags = HideFlags.HideAndDontSave;
            prefab.SetActive(false);
            CharacterTestMeteorBarrageVfxView view = prefab.AddComponent<CharacterTestMeteorBarrageVfxView>();
            view.ApplyModeDefaults(mode);
            view.RebuildVisuals();
            return prefab;
        }

        /// <summary>
        /// Gets the configured visual variant. NoAlloc.
        /// </summary>
        public CharacterTestMeteorBarrageVfxMode Mode => _mode;

        /// <summary>
        /// Applies default sizing and lifetime for the selected variant. NoAlloc.
        /// </summary>
        public void ApplyModeDefaults(CharacterTestMeteorBarrageVfxMode mode)
        {
            _mode = mode;
            _radius = DefaultAreaRadius;
            _rockRadius = 0.52f;
            _trailLength = 2.35f;
            switch (_mode)
            {
                case CharacterTestMeteorBarrageVfxMode.Charge:
                    _lifetimeFrames = CharacterTestVfxIds.MeteorBarrageChargeLifetimeFrames;
                    _rockRadius = 0.26f;
                    _trailLength = 0.68f;
                    break;
                case CharacterTestMeteorBarrageVfxMode.Telegraph:
                    _lifetimeFrames = CharacterTestVfxIds.MeteorBarrageTelegraphLifetimeFrames;
                    break;
                case CharacterTestMeteorBarrageVfxMode.Impact:
                    _lifetimeFrames = CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames;
                    _rockRadius = 0.18f;
                    _trailLength = 0.35f;
                    break;
                default:
                    _lifetimeFrames = 90;
                    break;
            }
        }

        /// <summary>
        /// Recreates missing child renderers and materials for the current variant. LowFreqAlloc.
        /// </summary>
        public void RebuildVisuals()
        {
            EnsureVisuals();
            ApplyVisualState(0f, 0f);
        }

        private void OnEnable()
        {
            _ageSeconds = 0f;
            EnsureVisuals();
            ApplyVisualState(0f, 0f);
            PlayModeParticles();
        }

        private void OnDisable()
        {
            _visualsReady = false;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeMaterials();
        }

        private void ReleaseRuntimeMaterials()
        {
            if (_runtimeLineMaterial != null)
                DestroyUnityObject(_runtimeLineMaterial);
            if (_runtimeRockMaterial != null)
                DestroyUnityObject(_runtimeRockMaterial);
            if (_runtimeParticleMaterial != null)
                DestroyUnityObject(_runtimeParticleMaterial);

            _runtimeLineMaterial = null;
            _runtimeRockMaterial = null;
            _runtimeParticleMaterial = null;
            _lineMaterial = null;
            _rockMaterial = null;
            _particleMaterial = null;
            _visualsReady = false;
        }

        private void Update()
        {
            if (!_visualsReady)
                EnsureVisuals();

            float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            _ageSeconds += Mathf.Max(0f, deltaTime);
            ApplyVisualState(_ageSeconds, deltaTime);
        }

        private void EnsureVisuals()
        {
            EnsureMaterials();
            switch (_mode)
            {
                case CharacterTestMeteorBarrageVfxMode.Charge:
                    EnsureChargeVisuals();
                    break;
                case CharacterTestMeteorBarrageVfxMode.Telegraph:
                    EnsureTelegraphVisuals();
                    break;
                case CharacterTestMeteorBarrageVfxMode.Impact:
                    EnsureImpactVisuals();
                    break;
                default:
                    EnsureFlightVisuals();
                    break;
            }

            _visualsReady = true;
        }

        private void EnsureFlightVisuals()
        {
            _rockRoot = EnsureChild(_rockRoot, "MoltenRock", Vector3.zero);
            _trailRoot = EnsureChild(_trailRoot, "FallingTrail", Vector3.zero);
            _rockRenderer = EnsureRock(_rockRoot, _rockRenderer);
            EnsureCracks(_rockRoot);
            EnsureTrailLines(_trailRoot);
            _fireParticles = EnsureParticleSystem(
                _trailRoot,
                _fireParticles,
                "FireWake",
                new Vector3(0f, 0f, -0.35f),
                _fireColor,
                0.12f,
                1.05f,
                26,
                0.42f);
            _emberParticles = EnsureParticleSystem(
                _trailRoot,
                _emberParticles,
                "FallingEmbers",
                new Vector3(0f, 0f, -0.25f),
                _lavaColor,
                0.045f,
                0.9f,
                16,
                0.34f);
        }

        private void EnsureChargeVisuals()
        {
            _chargeRoot = EnsureChild(_chargeRoot, "ChargeGlow", Vector3.zero);
            _chargeRing = EnsureRing(
                _chargeRoot,
                "ChargeRing",
                _chargeRing,
                0.62f,
                0.05f,
                _fireColor,
                y: 0.06f);
            _emberParticles = EnsureParticleSystem(
                _chargeRoot,
                _emberParticles,
                "GatheringEmbers",
                Vector3.zero,
                _lavaColor,
                0.06f,
                0.7f,
                36,
                0.32f);
        }

        private void EnsureTelegraphVisuals()
        {
            _telegraphRoot = EnsureChild(_telegraphRoot, "MeteorTelegraph", Vector3.zero);
            RemoveIndexedChildrenAtOrAbove(_telegraphRoot, "WarningRay", TelegraphRayCount);
            _telegraphOuterRing = EnsureRing(
                _telegraphRoot,
                "OuterWarningRing",
                _telegraphOuterRing,
                _radius,
                0.045f,
                _lavaColor,
                y: 0.035f);
            _telegraphInnerRing = EnsureRing(
                _telegraphRoot,
                "InnerHeatRing",
                _telegraphInnerRing,
                _radius * 0.68f,
                0.024f,
                _fireColor,
                y: 0.042f);
            _telegraphHeatRing = EnsureRing(
                _telegraphRoot,
                "HeatPulseRing",
                _telegraphHeatRing,
                _radius * 0.34f,
                0.02f,
                WithAlpha(_fireColor, 0.34f),
                y: 0.048f);
            EnsureTelegraphRays(_telegraphRoot);
            _emberParticles = EnsureParticleSystem(
                _telegraphRoot,
                _emberParticles,
                "GroundEmbers",
                Vector3.up * 0.08f,
                _lavaColor,
                0.026f,
                0.42f,
                10,
                0.16f);
        }

        private void EnsureImpactVisuals()
        {
            _impactRoot = EnsureChild(_impactRoot, "MeteorImpact", Vector3.zero);
            RemoveIndexedChildrenAtOrAbove(_impactRoot, "ImpactRay", ImpactRayCount);
            _impactShockRing = EnsureRing(
                _impactRoot,
                "ShockRing",
                _impactShockRing,
                _radius * 0.3f,
                0.07f,
                _fireColor,
                y: 0.08f);
            _impactSmokeRing = EnsureRing(
                _impactRoot,
                "SmokeRing",
                _impactSmokeRing,
                _radius * 0.44f,
                0.12f,
                _smokeColor,
                y: 0.12f);
            EnsureImpactRays(_impactRoot);
            _fireParticles = EnsureParticleSystem(
                _impactRoot,
                _fireParticles,
                "ImpactFlame",
                Vector3.up * 0.16f,
                _fireColor,
                0.18f,
                0.58f,
                48,
                0.32f);
            _smokeParticles = EnsureParticleSystem(
                _impactRoot,
                _smokeParticles,
                "ImpactSmoke",
                Vector3.up * 0.1f,
                _smokeColor,
                0.36f,
                1.05f,
                34,
                0.52f);
            _impactDebrisParticles = EnsureParticleSystem(
                _impactRoot,
                _impactDebrisParticles,
                "MoltenDebris",
                Vector3.up * 0.18f,
                _lavaColor,
                0.055f,
                0.75f,
                24,
                0.38f);
        }

        private void ApplyVisualState(float ageSeconds, float deltaSeconds)
        {
            float time = ageSeconds;
            float ageFrames = Mathf.Max(0f, ageSeconds * CharacterTestDeterministicUnits.DefaultTickRate);
            float normalizedAge = Mathf.Clamp01(ageFrames / Mathf.Max(1, _lifetimeFrames));
            switch (_mode)
            {
                case CharacterTestMeteorBarrageVfxMode.Charge:
                    ApplyChargeState(time, normalizedAge, deltaSeconds);
                    break;
                case CharacterTestMeteorBarrageVfxMode.Telegraph:
                    ApplyTelegraphState(time, normalizedAge);
                    break;
                case CharacterTestMeteorBarrageVfxMode.Impact:
                    ApplyImpactState(time, normalizedAge);
                    break;
                default:
                    ApplyFlightState(time, deltaSeconds);
                    break;
            }
        }

        private void ApplyFlightState(float time, float deltaSeconds)
        {
            float pulse = 1f + Mathf.Sin(time * 14f) * 0.045f;
            if (_rockRoot != null)
            {
                _rockRoot.localScale = new Vector3(1.05f * pulse, 0.82f * pulse, 1.28f * pulse);
                _rockRoot.Rotate(0f, 110f * deltaSeconds, 38f * deltaSeconds, Space.Self);
            }

            for (int i = 0; i < _trails.Length; i++)
            {
                LineRenderer trail = _trails[i];
                if (trail == null)
                    continue;

                float wave = Mathf.Sin(time * 16f + i * 1.73f) * 0.11f;
                trail.startColor = WithAlpha(_fireColor, 0.58f);
                trail.endColor = WithAlpha(_smokeColor, 0.02f);
                trail.SetPosition(1, ResolveTrailMiddle(i, wave));
                trail.SetPosition(2, ResolveTrailEnd(i, wave));
            }
        }

        private void ApplyChargeState(float time, float normalizedAge, float deltaSeconds)
        {
            float scale = Mathf.Lerp(0.64f, 1.08f, Smooth01(normalizedAge));
            float pulse = 1f + Mathf.Sin(time * 12f) * 0.08f;
            if (_chargeRoot != null)
            {
                _chargeRoot.localScale = Vector3.one * scale * pulse;
                _chargeRoot.Rotate(0f, 128f * deltaSeconds, 0f, Space.Self);
            }

            if (_chargeRing != null)
            {
                _chargeRing.startColor = WithAlpha(_fireColor, 0.55f + normalizedAge * 0.38f);
                _chargeRing.endColor = WithAlpha(_lavaColor, 0.28f + normalizedAge * 0.5f);
            }
        }

        private void ApplyTelegraphState(float time, float normalizedAge)
        {
            float warning = Smooth01(normalizedAge);
            float pulse = 1f + Mathf.Sin(time * 9.5f) * 0.035f;
            if (_telegraphRoot != null)
                _telegraphRoot.localScale = new Vector3(pulse, 1f, pulse);

            ApplyRingColor(_telegraphOuterRing, WithAlpha(_lavaColor, Mathf.Lerp(0.18f, 0.48f, warning)));
            ApplyRingColor(_telegraphInnerRing, WithAlpha(_fireColor, Mathf.Lerp(0.1f, 0.34f, warning)));
            if (_telegraphHeatRing != null)
            {
                float ringRadius = Mathf.Lerp(_radius * 0.22f, _radius * 0.92f, warning);
                SetRingPositions(_telegraphHeatRing, ringRadius, y: 0.048f);
                ApplyRingColor(_telegraphHeatRing, WithAlpha(_fireColor, 0.08f + 0.2f * (1f - warning)));
            }

            for (int i = 0; i < _telegraphRays.Length; i++)
            {
                LineRenderer ray = _telegraphRays[i];
                if (ray == null)
                    continue;

                float alpha = (0.22f + warning * 0.54f) * (0.74f + Mathf.Sin(time * 8f + i) * 0.18f);
                Color color = WithAlpha(_lavaColor, alpha);
                ray.startColor = color;
                ray.endColor = WithAlpha(_fireColor, alpha * 0.2f);
            }
        }

        private void ApplyImpactState(float time, float normalizedAge)
        {
            float expand = Smooth01(normalizedAge);
            float fade = 1f - expand;
            if (_impactShockRing != null)
            {
                SetRingPositions(_impactShockRing, Mathf.Lerp(_radius * 0.24f, _radius * 0.98f, expand), y: 0.08f);
                ApplyRingColor(_impactShockRing, WithAlpha(_fireColor, fade * 0.55f));
            }

            if (_impactSmokeRing != null)
            {
                SetRingPositions(_impactSmokeRing, Mathf.Lerp(_radius * 0.34f, _radius * 1.08f, expand), y: 0.12f);
                ApplyRingColor(_impactSmokeRing, WithAlpha(_smokeColor, fade * 0.5f));
            }

            for (int i = 0; i < _impactRays.Length; i++)
            {
                LineRenderer ray = _impactRays[i];
                if (ray == null)
                    continue;

                float alpha = fade * (0.18f + Mathf.Sin(time * 10f + i * 0.4f) * 0.04f);
                ray.startColor = WithAlpha(_lavaColor, alpha);
                ray.endColor = WithAlpha(_smokeColor, alpha * 0.04f);
                float length = Mathf.Lerp(_radius * 0.28f, _radius * 0.86f, expand);
                SetRayPositions(ray, i, ImpactRayCount, _radius * 0.1f, length, y: 0.1f);
            }
        }

        private void EnsureMaterials()
        {
            if (_lineMaterial == null)
                _lineMaterial = _lineMaterialOverride != null ? _lineMaterialOverride : CreateRuntimeLineMaterial();
            if (_rockMaterial == null)
                _rockMaterial = _rockMaterialOverride != null ? _rockMaterialOverride : CreateRuntimeRockMaterial();
            if (_particleMaterial == null)
            {
                _particleMaterial = _particleMaterialOverride != null
                    ? _particleMaterialOverride
                    : CreateRuntimeParticleMaterial();
            }
        }

        private Material CreateRuntimeLineMaterial()
        {
            Shader shader = CharacterTestUnlitMaterials.RequireVfxShader();
            _runtimeLineMaterial = new Material(shader)
            {
                name = "CharacterTest_MeteorBarrage_RuntimeLine",
                hideFlags = HideFlags.HideAndDontSave
            };
            ConfigureTransparentMaterial(_runtimeLineMaterial);
            return _runtimeLineMaterial;
        }

        private Material CreateRuntimeRockMaterial()
        {
            Shader shader = FindRequiredSupportedShader(PropShaderName);
            _runtimeRockMaterial = new Material(shader)
            {
                name = "CharacterTest_MeteorBarrage_RuntimeRock",
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(_runtimeRockMaterial, "_Color", _rockColor);
            SetMaterialFloat(_runtimeRockMaterial, "_WgAlbedoStrength", 1f);
            SetMaterialFloat(_runtimeRockMaterial, "_WgBronzeTrimAmount", 0f);
            SetMaterialFloat(_runtimeRockMaterial, "_WgInteractableGlowAmount", 0f);
            SetMaterialFloat(_runtimeRockMaterial, "_WgFacetStrength", 0.2f);
            SetMaterialFloat(_runtimeRockMaterial, "_WgFogBlendStrength", 0.25f);
            return _runtimeRockMaterial;
        }

        private Material CreateRuntimeParticleMaterial()
        {
            Shader shader = CharacterTestUnlitMaterials.RequireVfxShader();
            _runtimeParticleMaterial = new Material(shader)
            {
                name = "CharacterTest_MeteorBarrage_RuntimeParticles",
                hideFlags = HideFlags.HideAndDontSave
            };
            ConfigureTransparentMaterial(_runtimeParticleMaterial);
            return _runtimeParticleMaterial;
        }

        private Transform EnsureChild(Transform existing, string childName, Vector3 localPosition)
        {
            if (existing != null)
            {
                existing.localPosition = localPosition;
                return existing;
            }

            Transform child = transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                child = go.transform;
                child.SetParent(transform, false);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            return child;
        }

        private MeshRenderer EnsureRock(Transform parent, MeshRenderer existing)
        {
            if (existing == null)
            {
                Transform child = parent.Find("RockCore");
                GameObject go = child != null ? child.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "RockCore";
                go.transform.SetParent(parent, false);
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                    DestroyUnityObject(collider);

                existing = go.GetComponent<MeshRenderer>();
            }

            existing.sharedMaterial = _rockMaterial;
            existing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            existing.receiveShadows = false;
            existing.transform.localPosition = Vector3.zero;
            existing.transform.localScale = Vector3.one * _rockRadius;
            return existing;
        }

        private void EnsureCracks(Transform parent)
        {
            for (int i = 0; i < CrackCount; i++)
            {
                _cracks[i] = EnsureLine(parent, CrackNames[i], _cracks[i], 0.035f, _lavaColor);
                _cracks[i].positionCount = 3;
                float angle = i / (float)CrackCount * Mathf.PI * 2f;
                float offset = (i % 2 == 0 ? 0.16f : -0.12f) * _rockRadius;
                Vector3 start = new Vector3(Mathf.Cos(angle) * _rockRadius * 0.42f, offset, Mathf.Sin(angle) * _rockRadius * 0.42f);
                Vector3 middle = new Vector3(Mathf.Cos(angle + 0.25f) * _rockRadius * 0.66f, -offset * 0.4f, Mathf.Sin(angle + 0.25f) * _rockRadius * 0.66f);
                Vector3 end = new Vector3(Mathf.Cos(angle + 0.48f) * _rockRadius * 0.84f, offset * 0.2f, Mathf.Sin(angle + 0.48f) * _rockRadius * 0.84f);
                _cracks[i].SetPosition(0, start);
                _cracks[i].SetPosition(1, middle);
                _cracks[i].SetPosition(2, end);
            }
        }

        private void EnsureTrailLines(Transform parent)
        {
            RemoveIndexedChildrenAtOrAbove(parent, "FireTrail", TrailLineCount);
            for (int i = 0; i < TrailLineCount; i++)
            {
                _trails[i] = EnsureLine(parent, TrailNames[i], _trails[i], 0.06f + i * 0.012f, _fireColor);
                _trails[i].positionCount = 3;
                _trails[i].SetPosition(0, ResolveTrailStart(i));
                _trails[i].SetPosition(1, ResolveTrailMiddle(i, 0f));
                _trails[i].SetPosition(2, ResolveTrailEnd(i, 0f));
            }
        }

        private void EnsureTelegraphRays(Transform parent)
        {
            for (int i = 0; i < TelegraphRayCount; i++)
            {
                _telegraphRays[i] = EnsureLine(parent, "WarningRay" + i.ToString("00"), _telegraphRays[i], 0.035f, _lavaColor);
                _telegraphRays[i].positionCount = 2;
                SetRayPositions(_telegraphRays[i], i, TelegraphRayCount, _radius * 0.18f, _radius * 0.92f, y: 0.075f);
            }
        }

        private void EnsureImpactRays(Transform parent)
        {
            for (int i = 0; i < ImpactRayCount; i++)
            {
                _impactRays[i] = EnsureLine(parent, "ImpactRay" + i.ToString("00"), _impactRays[i], 0.035f, _lavaColor);
                _impactRays[i].positionCount = 2;
                SetRayPositions(_impactRays[i], i, ImpactRayCount, _radius * 0.1f, _radius * 0.62f, y: 0.1f);
            }
        }

        private LineRenderer EnsureLine(
            Transform parent,
            string objectName,
            LineRenderer existing,
            float width,
            Color color)
        {
            if (existing == null)
            {
                Transform child = parent.Find(objectName);
                GameObject go = child != null ? child.gameObject : new GameObject(objectName);
                go.transform.SetParent(parent, false);
                existing = go.GetComponent<LineRenderer>();
                if (existing == null)
                    existing = go.AddComponent<LineRenderer>();
            }

            ConfigureLine(existing, width, color);
            return existing;
        }

        private LineRenderer EnsureRing(
            Transform parent,
            string objectName,
            LineRenderer existing,
            float radius,
            float width,
            Color color,
            float y)
        {
            existing = EnsureLine(parent, objectName, existing, width, color);
            existing.loop = true;
            existing.positionCount = RingSegments;
            SetRingPositions(existing, radius, y);
            return existing;
        }

        private static void RemoveIndexedChildrenAtOrAbove(Transform parent, string prefix, int firstRemovedIndex)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (!child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    continue;

                if (TryReadSuffixIndex(child.name, prefix.Length, out int index) && index >= firstRemovedIndex)
                {
                    child.gameObject.SetActive(false);
                    DestroyUnityObject(child.gameObject);
                }
            }
        }

        private static bool TryReadSuffixIndex(string value, int startIndex, out int index)
        {
            index = 0;
            if (value == null || startIndex >= value.Length)
                return false;

            for (int i = startIndex; i < value.Length; i++)
            {
                char character = value[i];
                if (character < '0' || character > '9')
                    return false;

                index = index * 10 + character - '0';
            }

            return true;
        }

        private void ConfigureLine(LineRenderer line, float width, Color color)
        {
            line.useWorldSpace = false;
            line.sharedMaterial = _lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private ParticleSystem EnsureParticleSystem(
            Transform parent,
            ParticleSystem existing,
            string objectName,
            Vector3 localPosition,
            Color color,
            float size,
            float lifetime,
            int rate,
            float speed)
        {
            if (existing == null)
            {
                Transform child = parent.Find(objectName);
                GameObject go = child != null ? child.gameObject : new GameObject(objectName);
                go.transform.SetParent(parent, false);
                existing = go.GetComponent<ParticleSystem>();
                if (existing == null)
                    existing = go.AddComponent<ParticleSystem>();
            }

            existing.transform.localPosition = localPosition;
            existing.transform.localRotation = Quaternion.identity;
            ConfigureParticleSystem(existing, color, size, lifetime, rate, speed);
            return existing;
        }

        private void ConfigureParticleSystem(
            ParticleSystem system,
            Color color,
            float size,
            float lifetime,
            int rate,
            float speed)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = _mode != CharacterTestMeteorBarrageVfxMode.Impact;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.SetBursts(EmptyBursts);
            if (_mode == CharacterTestMeteorBarrageVfxMode.Impact)
            {
                emission.rateOverTime = 0;
                _singleBurst[0] = new ParticleSystem.Burst(0f, (short)Mathf.Max(1, rate));
                emission.SetBursts(_singleBurst);
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = _mode == CharacterTestMeteorBarrageVfxMode.Impact ? 42f : 18f;
            shape.radius = _mode == CharacterTestMeteorBarrageVfxMode.Telegraph ? _radius * 0.35f : 0.2f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(new Color(color.r, color.g * 0.45f, color.b * 0.2f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 0;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void PlayModeParticles()
        {
            PlayParticleSystem(_fireParticles);
            PlayParticleSystem(_emberParticles);
            PlayParticleSystem(_smokeParticles);
            PlayParticleSystem(_impactDebrisParticles);
        }

        private static void PlayParticleSystem(ParticleSystem system)
        {
            if (system != null)
                system.Play(true);
        }

        private Vector3 ResolveTrailStart(int index)
        {
            float angle = index / (float)TrailLineCount * Mathf.PI * 2f;
            float radius = _rockRadius * (0.3f + index * 0.035f);
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -_rockRadius * 0.35f);
        }

        private Vector3 ResolveTrailMiddle(int index, float wave)
        {
            float angle = index / (float)TrailLineCount * Mathf.PI * 2f;
            float radius = _rockRadius * (0.55f + index * 0.04f);
            return new Vector3(
                Mathf.Cos(angle) * radius + wave * 0.25f,
                Mathf.Sin(angle) * radius * 0.55f,
                -_trailLength * 0.46f);
        }

        private Vector3 ResolveTrailEnd(int index, float wave)
        {
            float angle = index / (float)TrailLineCount * Mathf.PI * 2f;
            float radius = _rockRadius * (0.7f + index * 0.05f);
            return new Vector3(
                Mathf.Cos(angle) * radius + wave,
                Mathf.Sin(angle) * radius * 0.72f,
                -_trailLength);
        }

        private static void SetRingPositions(LineRenderer ring, float radius, float y)
        {
            if (ring == null)
                return;

            ring.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float t = i / (float)RingSegments * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, y, Mathf.Sin(t) * radius));
            }
        }

        private static void SetRayPositions(
            LineRenderer ray,
            int index,
            int count,
            float inner,
            float outer,
            float y)
        {
            float angle = index / (float)count * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            ray.SetPosition(0, direction * inner + Vector3.up * y);
            ray.SetPosition(1, direction * outer + Vector3.up * y);
        }

        private static void ApplyRingColor(LineRenderer line, Color color)
        {
            if (line == null)
                return;

            line.startColor = color;
            line.endColor = color;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            CharacterTestUnlitMaterials.ConfigureVfx(material, Color.white);
        }

        private static Shader FindRequiredSupportedShader(string primaryName, string fallbackName)
        {
            Shader shader = Shader.Find(primaryName);
            if (shader != null && shader.isSupported)
                return shader;

            shader = Shader.Find(fallbackName);
            if (shader != null && shader.isSupported)
                return shader;

            throw new UnityException("CharacterTest Meteor Barrage VFX requires a supported URP shader.");
        }

        private static Shader FindRequiredSupportedShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null && shader.isSupported)
                return shader;

            throw new UnityException("CharacterTest Meteor Barrage VFX requires supported shader " + shaderName + ".");
        }

        private static void SetMaterialColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, value);
        }

        private static void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
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
