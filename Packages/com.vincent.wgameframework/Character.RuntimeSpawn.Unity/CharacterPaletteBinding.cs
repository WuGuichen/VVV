using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    /// <summary>Binds the project-wide RGB Color Palette to palette-compatible renderers.</summary>
    /// <remarks>Color is authored in UV1 and Surface Profile is authored in PaletteData.A.</remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterPaletteBinding : MonoBehaviour
    {
        public const int DefaultAtlasWidth = 256;
        public const int DefaultAtlasHeight = 256;

        private static readonly int PaletteEnabledId = Shader.PropertyToID("_WgPaletteEnabled");
        private static readonly int PaletteTextureId = Shader.PropertyToID("_WgColorPaletteTex");
        private static readonly int PaletteParamsId = Shader.PropertyToID("_WgColorPaletteParams");
        private static readonly int FaceDetailEnabledId = Shader.PropertyToID("_WgFaceDetailEnabled");
        private static readonly int FaceDetailTextureId = Shader.PropertyToID("_WgFaceDetailTex");
        private static readonly int FaceDetailIntensityId = Shader.PropertyToID("_WgFaceDetailIntensity");

        [SerializeField] private Texture2D _colorPaletteAtlas;
        [SerializeField, Min(1)] private int _atlasWidth = DefaultAtlasWidth;
        [SerializeField, Min(1)] private int _atlasHeight = DefaultAtlasHeight;
        [SerializeField] private Transform _targetRoot;
        [SerializeField] private bool _faceDetailEnabled;
        [SerializeField] private Texture2D _faceDetailTexture;
        [SerializeField, Range(0f, 1f)] private float _faceDetailIntensity = 1f;

        private readonly List<Material> _materialScratch = new List<Material>(4);
        private PaletteTarget[] _targets = Array.Empty<PaletteTarget>();
        private bool _initialized;
        private bool _missingAtlasWarningIssued;
        private bool _missingFaceDetailTextureWarningIssued;

        public Texture2D ColorPaletteAtlas => _colorPaletteAtlas;
        public int AtlasWidth => Math.Max(1, _atlasWidth);
        public int AtlasHeight => Math.Max(1, _atlasHeight);
        public int BoundRendererSlotCount => _targets.Length;
        public bool FaceDetailEnabled => _faceDetailEnabled;
        public Texture2D FaceDetailTexture => _faceDetailTexture;
        public float FaceDetailIntensity => Mathf.Clamp01(_faceDetailIntensity);

        /// <summary>Configures the shared atlas and optional face-detail token bindings before prefab save.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public void Configure(
            Texture2D colorPaletteAtlas,
            int atlasWidth = DefaultAtlasWidth,
            int atlasHeight = DefaultAtlasHeight,
            Transform targetRoot = null,
            bool faceDetailEnabled = false,
            Texture2D faceDetailTexture = null,
            float faceDetailIntensity = 1f)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Character Surface Palette binding is immutable at runtime.");

            _colorPaletteAtlas = colorPaletteAtlas;
            _atlasWidth = Math.Max(1, atlasWidth);
            _atlasHeight = Math.Max(1, atlasHeight);
            _targetRoot = targetRoot;
            _faceDetailEnabled = faceDetailEnabled;
            _faceDetailTexture = faceDetailTexture;
            _faceDetailIntensity = Mathf.Clamp01(faceDetailIntensity);
            _missingFaceDetailTextureWarningIssued = false;
            _missingAtlasWarningIssued = false;
            _initialized = false;
            EnsureInitialized();
            ApplyPalette();
        }

        private void Awake()
        {
            EnsureInitialized();
            ApplyPalette();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ApplyPalette();
        }

        private void OnTransformChildrenChanged()
        {
            if (!isActiveAndEnabled)
                return;
            RebuildTargets();
            ApplyPalette();
        }

        /// <summary>Rebuilds renderer slots after an explicit equipment composition change.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public void RefreshTargets()
        {
            EnsureInitialized();
            RebuildTargets();
            ApplyPalette();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;
            _atlasWidth = Math.Max(1, _atlasWidth);
            _atlasHeight = Math.Max(1, _atlasHeight);
            RebuildTargets();
            _initialized = true;
        }

        private void RebuildTargets()
        {
            Transform root = _targetRoot != null ? _targetRoot : transform;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            var targets = new List<PaletteTarget>(renderers.Length);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;
                _materialScratch.Clear();
                renderer.GetSharedMaterials(_materialScratch);
                for (int materialIndex = 0; materialIndex < _materialScratch.Count; materialIndex++)
                {
                    Material material = _materialScratch[materialIndex];
                    if (!IsPaletteCompatible(material))
                        continue;
                    if (_colorPaletteAtlas == null)
                        _colorPaletteAtlas = material.GetTexture(PaletteTextureId) as Texture2D;
                    bool supportsFaceDetail = material.HasProperty(FaceDetailEnabledId)
                        && material.HasProperty(FaceDetailTextureId)
                        && material.HasProperty(FaceDetailIntensityId);
                    targets.Add(new PaletteTarget(renderer, materialIndex, supportsFaceDetail));
                }
            }
            _targets = targets.ToArray();
        }

        private void ApplyPalette()
        {
            if (_targets.Length == 0)
                return;
            Texture2D atlas = ResolveAtlasFromTargets();
            bool hasAtlas = atlas != null;
            bool hasFaceDetailTexture = _faceDetailEnabled && _faceDetailTexture != null;
            if (!hasAtlas && !_missingAtlasWarningIssued)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterPaletteBinding)} on '{name}' has no shared RGB Color Palette atlas; palette rendering is disabled.",
                    this);
                _missingAtlasWarningIssued = true;
            }
            if (_faceDetailEnabled && _faceDetailTexture == null && !_missingFaceDetailTextureWarningIssued)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterPaletteBinding)} on '{name}' has face details enabled but no character-owned face-detail texture; face details are disabled.",
                    this);
                _missingFaceDetailTextureWarningIssued = true;
            }

            float width = AtlasWidth;
            float height = AtlasHeight;
            var paletteParams = new Vector4(width, height, 1f / width, 1f / height);
            for (int index = 0; index < _targets.Length; index++)
            {
                PaletteTarget target = _targets[index];
                target.Renderer.GetPropertyBlock(target.PropertyBlock, target.MaterialIndex);
                target.PropertyBlock.SetFloat(PaletteEnabledId, hasAtlas ? 1f : 0f);
                target.PropertyBlock.SetVector(PaletteParamsId, paletteParams);
                if (hasAtlas)
                    target.PropertyBlock.SetTexture(PaletteTextureId, atlas);
                if (target.SupportsFaceDetail)
                {
                    target.PropertyBlock.SetFloat(FaceDetailEnabledId, hasFaceDetailTexture ? 1f : 0f);
                    target.PropertyBlock.SetFloat(FaceDetailIntensityId, FaceDetailIntensity);
                    if (hasFaceDetailTexture)
                        target.PropertyBlock.SetTexture(FaceDetailTextureId, _faceDetailTexture);
                }
                target.Renderer.SetPropertyBlock(target.PropertyBlock, target.MaterialIndex);
            }
        }

        private Texture2D ResolveAtlasFromTargets()
        {
            if (_colorPaletteAtlas != null)
                return _colorPaletteAtlas;
            for (int index = 0; index < _targets.Length; index++)
            {
                PaletteTarget target = _targets[index];
                _materialScratch.Clear();
                target.Renderer.GetSharedMaterials(_materialScratch);
                if (target.MaterialIndex < 0 || target.MaterialIndex >= _materialScratch.Count)
                    continue;
                Material material = _materialScratch[target.MaterialIndex];
                Texture2D atlas = material != null ? material.GetTexture(PaletteTextureId) as Texture2D : null;
                if (atlas != null)
                {
                    _colorPaletteAtlas = atlas;
                    return atlas;
                }
            }
            return null;
        }

        private static bool IsPaletteCompatible(Material material)
        {
            return material != null
                && !(material.HasProperty("_WgBakedSurfaceEnabled") && material.GetFloat("_WgBakedSurfaceEnabled") > 0.5f)
                && material.HasProperty(PaletteEnabledId)
                && material.HasProperty(PaletteTextureId)
                && material.HasProperty(PaletteParamsId);
        }

        private sealed class PaletteTarget
        {
            public PaletteTarget(Renderer renderer, int materialIndex, bool supportsFaceDetail)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                SupportsFaceDetail = supportsFaceDetail;
                PropertyBlock = new MaterialPropertyBlock();
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
            public bool SupportsFaceDetail { get; }
            public MaterialPropertyBlock PropertyBlock { get; }
        }
    }
}
