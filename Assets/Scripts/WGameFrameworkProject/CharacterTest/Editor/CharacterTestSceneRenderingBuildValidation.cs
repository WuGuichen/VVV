using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Checks scene-owned rendering data without introducing it into level gameplay data.</summary>
    internal sealed class CharacterTestSceneRenderingBuildValidation : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            ValidateScene(scene);
        }

        internal static void ValidateScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var owner in root.GetComponentsInChildren<GameManager>(true))
                {
                    var c = owner.SceneRendering;
                    if (c == null) continue;
                    if (c.IndirectLightingEnabled)
                    {
                        if (c.Feature == null || c.TargetCamera == null || c.StaticRoot == null ||
                            c.Sun == null || c.Look == null || c.Data == null || c.Compute == null ||
                            c.CaptureShader == null || c.ResolveShader == null || c.SourceLayout == null ||
                            !MxSceneLightingBuilder.CacheMatchesSource(c, out _))
                            throw new BuildFailedException("Scene light transport is missing or stale: " + scene.path);
                        c.Data.ReadPayload().Validate();
                    }
                    if (c.DustEnabled && (c.Feature == null || c.TargetCamera == null || c.Dust == null ||
                        c.DustShader == null || !c.DustShader.isSupported))
                        throw new BuildFailedException("Scene dust bindings are incomplete: " + scene.path);
                    if (c.HeightFogEnabled && (c.Feature == null || c.TargetCamera == null ||
                        c.FogCompositeShader == null || !c.FogCompositeShader.isSupported))
                        throw new BuildFailedException("Scene height fog bindings are incomplete: " + scene.path);
                }
        }
    }
}
