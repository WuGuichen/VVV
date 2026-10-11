using System;
using MxFramework.Vfx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace MxFramework.Vfx.Unity.VfxGraph
{
    public enum UnityVfxGraphPropertyType
    {
        Float,
        Int,
        UInt,
        Bool
    }

    [Serializable]
    public sealed class UnityVfxGraphParameterMapping
    {
        [SerializeField] private int _runtimeSlot;
        [SerializeField] private string _propertyName = string.Empty;
        [SerializeField] private UnityVfxGraphPropertyType _propertyType = UnityVfxGraphPropertyType.Float;
        [SerializeField] private float _runtimeScale = 1f;

        public int RuntimeSlot => _runtimeSlot;
        public string PropertyName => _propertyName;
        public UnityVfxGraphPropertyType PropertyType => _propertyType;
        public float RuntimeScale => _runtimeScale;
    }

    /// <summary>Optional VFX Graph controller for one Published Effect Prefab.</summary>
    /// <remarks>NoAlloc after initialization. Reuses serialized mappings and cached property ids during spawn/stop.</remarks>
    [DisallowMultipleComponent]
    public sealed class UnityVfxGraphController : MonoBehaviour, IUnityVfxBackendController
    {
        [SerializeField] private VisualEffect[] _visualEffects = Array.Empty<VisualEffect>();
        [SerializeField] private UnityVfxGraphParameterMapping[] _parameterMappings = Array.Empty<UnityVfxGraphParameterMapping>();
        [SerializeField] private bool _suppressParticleSystemFallbackWhenAvailable = true;

        private int[] _propertyIds = Array.Empty<int>();
        private bool _initialized;

        public bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                if (!SystemInfo.supportsComputeShaders || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    return false;
                for (int i = 0; i < _visualEffects.Length; i++)
                    if (_visualEffects[i] != null && _visualEffects[i].visualEffectAsset != null)
                        return true;
                return false;
            }
        }

        public bool SuppressParticleSystemFallbackWhenAvailable => _suppressParticleSystemFallbackWhenAvailable;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Reinitializes Graph simulation, maps runtime slots, and starts playback.</summary>
        /// <remarks>NoAlloc after Awake/initialization. Called only for an activated VFX instance.</remarks>
        public void ReinitializeAndPlay(VfxInstanceSnapshot instance)
        {
            EnsureInitialized();
            if (instance == null)
                return;
            for (int i = 0; i < _visualEffects.Length; i++)
            {
                VisualEffect visualEffect = _visualEffects[i];
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    continue;
                visualEffect.Stop();
                visualEffect.Reinit();
                visualEffect.playRate = Mathf.Clamp(instance.PlaybackRatePermille / 1000f, 0.05f, 16f);
                ApplyMappings(visualEffect, instance.Parameters);
                visualEffect.Play();
                if (instance.SourceStartOffsetFrames > 0)
                {
                    float stepDeltaTime = 1f / (60f * visualEffect.playRate);
                    visualEffect.Simulate(stepDeltaTime, (uint)instance.SourceStartOffsetFrames);
                }
            }
        }

        /// <summary>Stops Graph emission and resets simulation for pooling.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void StopAndClear()
        {
            EnsureInitialized();
            for (int i = 0; i < _visualEffects.Length; i++)
            {
                VisualEffect visualEffect = _visualEffects[i];
                if (visualEffect == null)
                    continue;
                visualEffect.Stop();
                visualEffect.Reinit();
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;
            if (_visualEffects == null || _visualEffects.Length == 0)
                _visualEffects = GetComponentsInChildren<VisualEffect>(true);
            _parameterMappings ??= Array.Empty<UnityVfxGraphParameterMapping>();
            _propertyIds = new int[_parameterMappings.Length];
            for (int i = 0; i < _parameterMappings.Length; i++)
                _propertyIds[i] = Shader.PropertyToID(_parameterMappings[i] != null ? _parameterMappings[i].PropertyName : string.Empty);
            _initialized = true;
        }

        private void ApplyMappings(VisualEffect visualEffect, VfxParameterBlock parameters)
        {
            for (int i = 0; i < _parameterMappings.Length; i++)
            {
                UnityVfxGraphParameterMapping mapping = _parameterMappings[i];
                if (mapping == null || mapping.RuntimeSlot < 0 || mapping.RuntimeSlot > 3 || string.IsNullOrWhiteSpace(mapping.PropertyName))
                    continue;
                int rawValue = ReadSlot(parameters, mapping.RuntimeSlot);
                int propertyId = _propertyIds[i];
                switch (mapping.PropertyType)
                {
                    case UnityVfxGraphPropertyType.Float:
                        if (visualEffect.HasFloat(propertyId)) visualEffect.SetFloat(propertyId, rawValue * mapping.RuntimeScale);
                        break;
                    case UnityVfxGraphPropertyType.Int:
                        if (visualEffect.HasInt(propertyId)) visualEffect.SetInt(propertyId, Mathf.RoundToInt(rawValue * mapping.RuntimeScale));
                        break;
                    case UnityVfxGraphPropertyType.UInt:
                        if (visualEffect.HasUInt(propertyId)) visualEffect.SetUInt(propertyId, (uint)Mathf.Max(0, Mathf.RoundToInt(rawValue * mapping.RuntimeScale)));
                        break;
                    case UnityVfxGraphPropertyType.Bool:
                        if (visualEffect.HasBool(propertyId)) visualEffect.SetBool(propertyId, rawValue != 0);
                        break;
                }
            }
        }

        private static int ReadSlot(VfxParameterBlock parameters, int slot)
        {
            return slot switch
            {
                0 => parameters.Value0,
                1 => parameters.Value1,
                2 => parameters.Value2,
                3 => parameters.Value3,
                _ => 0
            };
        }
    }
}
