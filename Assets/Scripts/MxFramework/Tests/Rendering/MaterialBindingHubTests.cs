using System;
using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class MaterialBindingHubTests
    {
        private const string TestFloatName = "_MxMaterialBindingTestFloat";
        private const string TestColorName = "_MxMaterialBindingTestColor";
        private const string TestVectorName = "_MxMaterialBindingTestVector";

        [Test]
        public void PublicSurface_ExposesDocumentedHubAndWriterContracts()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(IMaterialBindingHub.Bind),
                    nameof(IMaterialBindingHub.CaptureDiagnostics),
                    nameof(IMaterialBindingHub.Release),
                    nameof(IMaterialBindingHub.Release)
                },
                typeof(IMaterialBindingHub).GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(IMaterialBindingWriter.Pulse),
                    nameof(IMaterialBindingWriter.SetColor),
                    nameof(IMaterialBindingWriter.SetFloat),
                    nameof(IMaterialBindingWriter.SetTexture),
                    nameof(IMaterialBindingWriter.SetVector)
                },
                typeof(IMaterialBindingWriter).GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }

        [Test]
        public void Flush_MergesChannelsAndAppliesOncePerTarget()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                var registry = new MxRenderSubjectRegistry();
                MxRenderSubjectId subjectA = registry.Register(MxRenderSubjectRole.Primary);
                MxRenderSubjectId subjectB = registry.Register(MxRenderSubjectRole.Tracked);
                var hub = new MaterialBindingHub(registry);
                int floatId = Shader.PropertyToID(TestFloatName);
                int colorId = Shader.PropertyToID(TestColorName);

                MaterialBinding hitFlash = hub.Bind(subjectA, MxMaterialChannel.HitFlash, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                MaterialBinding debugOverlay = hub.Bind(subjectB, MxMaterialChannel.DebugOverlay, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));

                hub.SetFloat(hitFlash, floatId, 0.25f);
                hub.SetColor(debugOverlay, colorId, Color.green);
                hub.Flush();

                MaterialPropertyBlock block = fixture.ReadPropertyBlock(0);
                Assert.AreEqual(0.25f, block.GetFloat(floatId));
                Assert.AreEqual(Color.green, block.GetColor(colorId));

                MaterialBindingDiagnosticsSnapshot snapshot = hub.CaptureDiagnostics();
                Assert.AreEqual(2, snapshot.BindingCount);
                Assert.AreEqual(1, snapshot.TargetCount);
                Assert.AreEqual(1, snapshot.LastAppliedTargetCount);
                Assert.AreEqual(2, snapshot.LastMergedPropertyCount);
                Assert.AreEqual(1, snapshot.CountFor(MxMaterialChannel.HitFlash));
                Assert.AreEqual(1, snapshot.CountFor(MxMaterialChannel.DebugOverlay));
            }
        }

        [Test]
        public void Flush_AppliesRendererScopeToEveryMaterialIndex()
        {
            using (RendererFixture fixture = RendererFixture.Create(materialCount: 2))
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                int floatId = Shader.PropertyToID(TestFloatName);

                MaterialBinding binding = hub.Bind(subject, MxMaterialChannel.StatusTint, MaterialBindingScope.ForRenderer(fixture.Renderer));
                hub.SetFloat(binding, floatId, 0.5f);
                hub.Flush();

                Assert.AreEqual(0.5f, fixture.ReadPropertyBlock(0).GetFloat(floatId));
                Assert.AreEqual(0.5f, fixture.ReadPropertyBlock(1).GetFloat(floatId));
                Assert.AreEqual(2, hub.CaptureDiagnostics().LastAppliedTargetCount);
            }
        }

        [Test]
        public void Binding_DuplicateSubjectChannelReplacesOldBindingAndWarns()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                int floatId = Shader.PropertyToID(TestFloatName);
                int colorId = Shader.PropertyToID(TestColorName);

                MaterialBinding first = hub.Bind(subject, MxMaterialChannel.HitFlash, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                hub.SetFloat(first, floatId, 1f);
                MaterialBinding second = hub.Bind(subject, MxMaterialChannel.HitFlash, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                hub.SetColor(second, colorId, Color.red);
                hub.SetFloat(first, floatId, 0.25f);
                hub.Flush();

                MaterialPropertyBlock block = fixture.ReadPropertyBlock(0);
                Assert.AreEqual(0f, block.GetFloat(floatId));
                Assert.AreEqual(Color.red, block.GetColor(colorId));

                MaterialBindingDiagnosticsSnapshot snapshot = hub.CaptureDiagnostics();
                Assert.AreEqual(1, snapshot.BindingCount);
                Assert.AreEqual(1, snapshot.CountFor(MxMaterialChannel.HitFlash));
                Assert.AreEqual(1, snapshot.DuplicateWarnings.Count);
                Assert.AreEqual(first.Id, snapshot.DuplicateWarnings[0].ReplacedBindingId);
            }
        }

        [Test]
        public void SubjectRelease_ClearsBindingsAndRendererPropertyBlocks()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                var registry = new MxRenderSubjectRegistry();
                MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
                var hub = new MaterialBindingHub(registry);
                int floatId = Shader.PropertyToID(TestFloatName);

                MaterialBinding binding = hub.Bind(subject, MxMaterialChannel.WetnessOverride, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                hub.SetFloat(binding, floatId, 0.75f);
                hub.Flush();
                Assert.AreEqual(0.75f, fixture.ReadPropertyBlock(0).GetFloat(floatId));

                Assert.IsTrue(registry.Release(subject));
                hub.Flush();

                Assert.AreEqual(0f, fixture.ReadPropertyBlock(0).GetFloat(floatId));
                MaterialBindingDiagnosticsSnapshot snapshot = hub.CaptureDiagnostics();
                Assert.AreEqual(0, snapshot.BindingCount);
                Assert.AreEqual(0, snapshot.TargetCount);
                Assert.AreEqual(1, snapshot.LastAppliedTargetCount);
            }
        }

        [Test]
        public void SubjectHierarchyScope_MergesAllRendererTargets()
        {
            using (RendererFixture first = RendererFixture.Create())
            using (RendererFixture second = RendererFixture.Create())
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                int vectorId = Shader.PropertyToID(TestVectorName);
                Vector4 value = new Vector4(1f, 2f, 3f, 4f);

                MaterialBinding binding = hub.Bind(
                    subject,
                    MxMaterialChannel.OutlineState,
                    MaterialBindingScope.ForSubjectHierarchy(new[] { first.Renderer, null, second.Renderer }));
                hub.SetVector(binding, vectorId, value);
                hub.Flush();

                Assert.AreEqual(value, first.ReadPropertyBlock(0).GetVector(vectorId));
                Assert.AreEqual(value, second.ReadPropertyBlock(0).GetVector(vectorId));
                Assert.AreEqual(2, hub.CaptureDiagnostics().LastAppliedTargetCount);
            }
        }

        [Test]
        public void CurveDescriptor_PulseUsesStablePointContract()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                int floatId = Shader.PropertyToID(TestFloatName);

                MaterialBinding binding = hub.Bind(subject, MxMaterialChannel.DissolveProgress, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                hub.Pulse(binding, floatId, new MaterialBindingCurveDescriptor(new[]
                {
                    new MaterialBindingCurvePoint(0f, 0.1f),
                    new MaterialBindingCurvePoint(1f, 0.9f)
                }), 0.2f);
                hub.Flush();

                Assert.AreEqual(0.9f, fixture.ReadPropertyBlock(0).GetFloat(floatId));
            }
        }

        [Test]
        public void DiagnosticsDebugSource_ExposesMaterialBindingsSection()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                hub.Bind(subject, MxMaterialChannel.BridgeCustom, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                var source = new MaterialBindingHubDebugSource(hub);

                FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

                Assert.AreEqual("Rendering", snapshot.SourceName);
                Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
                Assert.AreEqual(RenderingDebugSectionNames.MaterialBindings, snapshot.Sections[0].Title);
                StringAssert.Contains("bindings: 1", snapshot.Sections[0].Body);
                StringAssert.Contains("BridgeCustom: 1", snapshot.Sections[0].Body);
            }
        }

        [Test]
        public void Flush_ReusesMaterialPropertyBlocksFromPool()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                MxRenderSubjectId subject = new MxRenderSubjectId(1);
                var hub = new MaterialBindingHub();
                int floatId = Shader.PropertyToID(TestFloatName);
                MaterialBinding binding = hub.Bind(subject, MxMaterialChannel.DebugOverlay, MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));

                hub.SetFloat(binding, floatId, 1f);
                hub.Flush();
                hub.SetFloat(binding, floatId, 2f);
                hub.Flush();

                MaterialBindingDiagnosticsSnapshot snapshot = hub.CaptureDiagnostics();
                Assert.AreEqual(2, snapshot.TotalPropertyBlockGets);
                Assert.AreEqual(1, snapshot.PropertyBlockPoolHits);
                Assert.AreEqual(0.5f, snapshot.PropertyBlockPoolHitRate);
            }
        }

        private sealed class RendererFixture : IDisposable
        {
            private RendererFixture(GameObject gameObject, Mesh mesh, MeshRenderer renderer, Material[] materials)
            {
                GameObject = gameObject;
                Mesh = mesh;
                Renderer = renderer;
                Materials = materials;
            }

            public GameObject GameObject { get; }
            public Mesh Mesh { get; }
            public MeshRenderer Renderer { get; }
            public Material[] Materials { get; }

            public static RendererFixture Create(int materialCount = 1)
            {
                var gameObject = new GameObject("MaterialBindingHubTests");
                var mesh = new Mesh();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
                var materials = new Material[Math.Max(1, materialCount)];
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = new Material(shader);

                renderer.sharedMaterials = materials;
                return new RendererFixture(gameObject, mesh, renderer, materials);
            }

            public MaterialPropertyBlock ReadPropertyBlock(int materialIndex)
            {
                var block = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(block, materialIndex);
                return block;
            }

            public void Dispose()
            {
                for (int i = 0; i < Materials.Length; i++)
                {
                    if (Materials[i] != null)
                        UnityEngine.Object.DestroyImmediate(Materials[i]);
                }

                if (GameObject != null)
                    UnityEngine.Object.DestroyImmediate(GameObject);

                if (Mesh != null)
                    UnityEngine.Object.DestroyImmediate(Mesh);
            }
        }
    }
}
