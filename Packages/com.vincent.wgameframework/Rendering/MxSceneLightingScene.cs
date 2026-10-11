using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>
    /// Serialized legacy data retained only so migration Undo and older scenes keep their references.
    /// Rendering is owned by the GameManager's MxSceneRenderingSession.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MxSceneLightingScene : MonoBehaviour
    {
        public Transform StaticRoot;
        public Light Sun;
        public Camera TargetCamera;
        public MxRenderingPipelineFeature Feature;
        public MxSceneLightingData Data;
        public MxSceneLightingLook Look;
        public MxSceneLightingSourceLayout SourceLayout = new MxSceneLightingSourceLayout();
        public ComputeShader Compute;
        public Shader CaptureShader;
        public Shader ResolveShader;
        public bool PreviewInSceneView = true;
        public bool RequireMotionVectors;
    }
}
