using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Generic scene configuration host for presentation-only scenes without a gameplay composition root.</summary>
    [DisallowMultipleComponent]
    public sealed class MxSceneRenderingSceneRoot : MonoBehaviour
    {
        [SerializeField] private MxSceneRenderingConfiguration _configuration = new MxSceneRenderingConfiguration();
        private MxSceneRenderingSession _session;
        public MxSceneRenderingConfiguration Configuration => _configuration;
        private void OnEnable()
        {
            if (Application.isPlaying && _session == null)
                _session = new MxSceneRenderingSession(() => _configuration, gameObject.scene);
        }
        private void Update() { _session?.Tick(); }
        private void OnDisable() { _session?.Dispose(); _session = null; }
        private void OnDestroy() { _session?.Dispose(); _session = null; }
    }
}
