using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;

namespace MxFramework.Demo.Rendering
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Volumetric Fog Scene Bootstrap")]
    public sealed class MxVolumetricFogSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private MxVolumetricFogQuality _quality = MxVolumetricFogQuality.Low;
        [SerializeField] private Color _color = new Color(0.18f, 0.36f, 0.38f, 1f);
        [SerializeField] private float _density = 0.72f;
        [SerializeField] private float _baseHeight = -0.45f;
        [SerializeField] private float _heightFalloff = 0.65f;
        [SerializeField] private float _maxDistance = 32f;
        [SerializeField] private float _anisotropy = 0.12f;
        [SerializeField] private float _scatteringIntensity = 1.1f;
        [SerializeField] private float _noiseStrength = 0.1f;
        [SerializeField] private float _noiseScale = 40f;
        [SerializeField] private float _focusClearRadius = 3.5f;
        [SerializeField] private float _focusClearSoftness = 5f;
        [SerializeField] private bool _previewInSceneView;

        private readonly List<IMxRenderPipeline> _registeredPipelines = new List<IMxRenderPipeline>();
        private MxVolumetricFogPassProvider _provider;

        private void OnEnable()
        {
            Register();
        }

        private void Start()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void OnValidate()
        {
            ApplySettings();

            if (isActiveAndEnabled)
                RegisterAvailablePipelines();
        }

        private void Register()
        {
            if (_provider == null)
                _provider = new MxVolumetricFogPassProvider(CreateSettings());
            else
                ApplySettings();

            RegisterAvailablePipelines();
        }

        private void RegisterAvailablePipelines()
        {
            if (_provider == null)
                return;

            MxRenderingPipelineFeature[] features = UnityEngine.Resources.FindObjectsOfTypeAll<MxRenderingPipelineFeature>();
            for (int i = 0; i < features.Length; i++)
            {
                MxRenderingPipelineFeature feature = features[i];
                if (feature == null)
                    continue;

                IMxRenderPipeline pipeline = feature.Pipeline;
                if (pipeline == null || _registeredPipelines.Contains(pipeline))
                    continue;

                pipeline.RegisterProvider(_provider);
                _registeredPipelines.Add(pipeline);
            }
        }

        private void ApplySettings()
        {
            _provider?.SetSettings(CreateSettings());
        }

        private void Unregister()
        {
            if (_provider == null)
                return;

            for (int i = 0; i < _registeredPipelines.Count; i++)
                _registeredPipelines[i]?.UnregisterProvider(_provider.DebugName);

            _registeredPipelines.Clear();
            _provider.Dispose();
            _provider = null;
        }

        private MxVolumetricFogSettings CreateSettings()
        {
            bool enabled = _quality != MxVolumetricFogQuality.Disabled;
            return new MxVolumetricFogSettings(
                enabled,
                _quality,
                _color,
                _density,
                _baseHeight,
                _heightFalloff,
                _maxDistance,
                _anisotropy,
                _scatteringIntensity,
                _noiseStrength,
                _noiseScale,
                6,
                4,
                0f,
                _focusClearRadius,
                _focusClearSoftness,
                _previewInSceneView);
        }
    }
}
