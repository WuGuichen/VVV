using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Serialized legacy data for migration Undo; it never registers a render provider.</summary>
    [AddComponentMenu("")]
    public sealed class MxHeightFogScene : MonoBehaviour
    {
        public MxRenderingPipelineFeature Feature;
        public Camera TargetCamera;
        public Shader CompositeShader;
        public bool PreviewInSceneView = true;
        public Color FogColor = new Color(0.60f, 0.66f, 0.72f, 1);
        public float Density = 0.18f;
        public float BaseHeight;
        public float HeightFalloff = 0.12f;
        public float MaxDistance = 180;
        public float ScatteringIntensity = 1;
        public float FocusClearRadius = 3.5f;
        public float FocusClearSoftness = 8;
    }
}
