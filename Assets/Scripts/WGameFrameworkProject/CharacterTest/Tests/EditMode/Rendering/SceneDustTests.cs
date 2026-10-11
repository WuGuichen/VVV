using System.Linq;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Rendering
{
    public sealed class SceneDustTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ElevatedCameraDustVolumeCoversVisibleLayer(bool orthographic)
        {
            var cameraObject = new GameObject("Tactical dust coverage camera");
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>(); camera.enabled = false;
                camera.transform.SetPositionAndRotation(new Vector3(0, 56, -80), Quaternion.Euler(35, 0, 0));
                camera.fieldOfView = 60; camera.aspect = 16f / 9; camera.farClipPlane = 300;
                camera.orthographic = orthographic; camera.orthographicSize = 7;
                var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                var size = new Vector3(36, 5, 36);
                Assert.IsFalse(GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(0, 2.5f, -80), size)));
                Vector3 center = MxSceneDustProvider.CalculateVolumeCenter(camera, 18, 0, 5);
                Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, new Bounds(center, size)));
                Vector3 viewport = camera.WorldToViewportPoint(center);
                Assert.That(viewport.x, Is.EqualTo(.5f).Within(.001f));
                Assert.That(viewport.y, Is.EqualTo(.5f).Within(.001f));
                Assert.That(center.y, Is.EqualTo(2.5f).Within(.001f));
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void DustSessionWorksWithoutGiOrFogAndReleasesCameraBindings()
        {
            var feature = ScriptableObject.CreateInstance<MxRenderingPipelineFeature>();
            var firstObject = new GameObject("Dust camera A");
            var secondObject = new GameObject("Dust camera B");
            MxSceneRenderingSession session = null;
            try
            {
                var first = firstObject.AddComponent<UnityEngine.Camera>(); first.enabled = false;
                var second = secondObject.AddComponent<UnityEngine.Camera>(); second.enabled = false;
                var configuration = new MxSceneRenderingConfiguration
                {
                    DustEnabled = true, PreviewDustInSceneView = false, TargetCamera = first,
                    Feature = feature, DustShader = Shader.Find("Hidden/MxFramework/Rendering/SceneDust")
                };
                session = new MxSceneRenderingSession(() => configuration, SceneManager.GetActiveScene());
                Assert.IsTrue(session.HasDust, session.DustIssue);
                Assert.IsFalse(session.HasLighting);
                Assert.IsFalse(session.HasFog);
                var a = new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game, first, Vector3.zero);
                var b = new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game, second, Vector3.zero);
                Assert.AreEqual("SceneDust", feature.Pipeline.CollectPasses(a).Single().DebugName);
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(b).Count);
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(new MxCameraRenderContextDescriptor(MxCameraRenderKind.Reflection, first, Vector3.zero)).Count);
                configuration.TargetCamera = second; session.Tick();
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(a).Count);
                Assert.AreEqual(1, feature.Pipeline.CollectPasses(b).Count);
                configuration.Dust.ParticleCount = 0; session.Tick();
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(b).Count);
                configuration.Dust.ParticleCount = 16;
                configuration.DustEnabled = false; session.Tick();
                Assert.IsFalse(session.HasDust);
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(b).Count);
                configuration.DustEnabled = true; session.Tick();
                Assert.IsTrue(session.HasDust);
                session.Dispose();
                Assert.AreEqual(0, feature.Pipeline.CollectPasses(b).Count);
            }
            finally
            {
                session?.Dispose();
                Object.DestroyImmediate(firstObject); Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void MediumLightSamplingPreservesIsotropicEnergyAndDirectionalResponse()
        {
            const string source = @"Shader ""Hidden/MxDustMediumTest"" { SubShader { Pass {
                ZTest Always ZWrite Off Cull Off HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex Vert
                #pragma fragment Frag
                #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
                #include ""Packages/com.vincent.wgameframework/Rendering/MxLightField.hlsl""
                float4 _Sample;
                float4 Vert(uint id:SV_VertexID):SV_POSITION { return GetFullScreenTriangleVertexPosition(id); }
                float4 Frag():SV_Target { return float4(MxSampleMediumAmbient(float3(0,0,0),_Sample.xyz,_Sample.w),1); }
                ENDHLSL } } }";
            Shader shader = null; Material material = null; RenderTexture target = null; Texture2D readback = null;
            GraphicsBuffer probes = null, coefficients = null, visibility = null;
            var command = new CommandBuffer();
            var previous = RenderTexture.active;
            try
            {
                shader = ShaderUtil.CreateShaderAsset(source); material = new Material(shader);
                probes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 64);
                probes.SetData(new[] { new MxSceneLightingProbe { PositionValid = new Vector4(0, 0, 0, 1) } });
                coefficients = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 48);
                coefficients.SetData(new[] { new Coefficients { R = new Vector4(0, .1f, 0, .3f), G = new Vector4(0, 0, 0, .4f), B = new Vector4(0, 0, 0, .5f) } });
                visibility = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 96, 8);
                visibility.SetData(Enumerable.Repeat(new Vector2(10, 100), 96).ToArray());
                material.SetBuffer("_MxLightingProbes", probes); material.SetBuffer("_MxLightingCoefficients", coefficients);
                material.SetBuffer("_MxLightingVisibility", visibility);
                material.SetVector("_MxLightingGridOrigin", new Vector4(0, 0, 0, 1));
                material.SetVector("_MxLightingGridSize", new Vector4(1, 1, 1, 0));
                target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); target.Create();
                readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                Color Capture(Vector4 sample)
                {
                    material.SetVector("_Sample", sample);
                    command.Clear(); command.SetRenderTarget(target); command.SetViewport(new Rect(0, 0, 1, 1));
                    command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                    Graphics.ExecuteCommandBuffer(command); RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply(); return readback.GetPixel(0, 0);
                }
                var isotropic = Capture(new Vector4(0, 1, 0, 0));
                Assert.That(isotropic.r, Is.EqualTo(.3f).Within(.0001));
                Assert.That(isotropic.g, Is.EqualTo(.4f).Within(.0001));
                Assert.That(isotropic.b, Is.EqualTo(.5f).Within(.0001));
                Assert.That(Capture(new Vector4(0, 1, 0, .5f)).r, Is.EqualTo(.15f).Within(.0001));
                Assert.That(Capture(new Vector4(0, -1, 0, .5f)).r, Is.EqualTo(.45f).Within(.0001));
            }
            finally
            {
                RenderTexture.active = previous; command.Dispose();
                probes?.Dispose(); coefficients?.Dispose(); visibility?.Dispose();
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
                if (material != null) Object.DestroyImmediate(material);
                if (shader != null) Object.DestroyImmediate(shader);
            }
        }

        private struct Coefficients { public Vector4 R, G, B; }
    }
}
