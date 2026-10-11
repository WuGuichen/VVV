using System;
using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.CharacterRuntimeSpawn.Unity;
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

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Flush_PreservesPaletteBaselineThroughEffectUpdatesAndRelease(int repeat)
        {
            using (RendererFixture fixture = RendererFixture.Create(shaderName: "WGame/CharacterPaletteLegacy"))
            {
                var atlas = new Texture2D(256, 256, TextureFormat.RGB24, false);
                var face = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                try
                {
                    fixture.GameObject.SetActive(false);
                    Material material = fixture.Materials[0];
                    material.EnableKeyword("_WG_PALETTE_ON");
                    material.EnableKeyword("_WG_FACE_DETAIL_ON");
                    material.SetFloat("_WgFaceDetailIntensity", 0.2f);
                    var appearance = fixture.GameObject.AddComponent<CharacterPaletteBinding>();
                    appearance.Configure(atlas, 256, 256, fixture.GameObject.transform, true, face, 0.75f);
                    Assert.AreEqual(1, appearance.BoundRendererSlotCount);

                    var hub = new MaterialBindingHub();
                    var subject = new MxRenderSubjectId(901);
                    var scope = MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0);
                    int hitId = Shader.PropertyToID("_WgHitFlashAmount");
                    int tintId = Shader.PropertyToID("_WgStatusTintAmount");
                    MaterialBinding hit = default;
                    MaterialBinding tint = default;
                    for (int stage = 0; stage < 5; stage++)
                    {
                        if (stage == 1)
                        {
                            hit = hub.Bind(subject, MxMaterialChannel.HitFlash, scope);
                            tint = hub.Bind(subject, MxMaterialChannel.StatusTint, scope);
                            hub.SetFloat(hit, hitId, 0.65f);
                            hub.SetFloat(tint, tintId, 0.3f);
                            hub.Flush();
                        }
                        if (stage == 2) { hub.SetFloat(hit, hitId, 0.25f); hub.Flush(); }
                        if (stage == 3) { Assert.IsTrue(hub.Release(hit)); hub.Flush(); }
                        if (stage == 4) { Assert.IsTrue(hub.Release(tint)); hub.Flush(); }
                        MaterialPropertyBlock block = fixture.ReadPropertyBlock(0);
                        Assert.AreEqual(1f, block.GetFloat("_WgPaletteEnabled"), $"repeat={repeat} stage={stage}");
                        Assert.AreSame(atlas, block.GetTexture("_WgColorPaletteTex"));
                        Assert.AreEqual(new Vector4(256f, 256f, 1f / 256f, 1f / 256f), block.GetVector("_WgColorPaletteParams"));
                        Assert.AreEqual(1f, block.GetFloat("_WgFaceDetailEnabled"));
                        Assert.AreSame(face, block.GetTexture("_WgFaceDetailTex"));
                        Assert.AreEqual(0.75f, block.GetFloat("_WgFaceDetailIntensity"));
                        Assert.AreEqual(stage == 1 ? 0.65f : stage == 2 ? 0.25f : 0f, block.GetFloat(hitId));
                        Assert.AreEqual(stage >= 1 && stage <= 3 ? 0.3f : 0f, block.GetFloat(tintId));
                        Assert.AreSame(material, fixture.Renderer.sharedMaterial);
                        Assert.AreEqual(0.2f, material.GetFloat("_WgFaceDetailIntensity"));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(atlas);
                    UnityEngine.Object.DestroyImmediate(face);
                }
            }
        }

        [Test]
        public void Release_RestoresOverriddenPropertyAndRecapturesNextOwnershipBaseline()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                int id = Shader.PropertyToID(TestFloatName);
                var baseline = new MaterialPropertyBlock();
                baseline.SetFloat(id, 0.4f);
                fixture.Renderer.SetPropertyBlock(baseline, 0);
                var hub = new MaterialBindingHub();
                var subject = new MxRenderSubjectId(1);
                var scope = MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0);
                MaterialBinding old = hub.Bind(subject, MxMaterialChannel.HitFlash, scope);
                hub.SetFloat(old, id, 0.8f);
                hub.Flush();
                MaterialBinding replacement = hub.Bind(subject, MxMaterialChannel.HitFlash, scope);
                hub.SetFloat(replacement, id, 0.9f);
                hub.Flush();
                Assert.IsFalse(hub.Release(old));
                hub.Release(replacement);
                hub.Flush();
                Assert.AreEqual(0.4f, fixture.ReadPropertyBlock(0).GetFloat(id));

                baseline.SetFloat(id, 0.6f);
                fixture.Renderer.SetPropertyBlock(baseline, 0);
                MaterialBinding next = hub.Bind(subject, MxMaterialChannel.HitFlash, scope);
                hub.SetFloat(next, id, 1f);
                hub.Flush();
                hub.Release(next);
                hub.Flush();
                Assert.AreEqual(0.6f, fixture.ReadPropertyBlock(0).GetFloat(id));
            }
        }

        [Test]
        public void Flush_EmptySlotInheritsRendererAppearanceAndReleaseRestoresEmptySlot()
        {
            using (RendererFixture fixture = RendererFixture.Create(materialCount: 2))
            {
                int appearanceId = Shader.PropertyToID(TestColorName);
                int effectId = Shader.PropertyToID(TestFloatName);
                var rendererBlock = new MaterialPropertyBlock();
                rendererBlock.SetColor(appearanceId, Color.green);
                fixture.Renderer.SetPropertyBlock(rendererBlock);
                var hub = new MaterialBindingHub();
                var binding = hub.Bind(new MxRenderSubjectId(1), MxMaterialChannel.HitFlash,
                    MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                hub.SetFloat(binding, effectId, 0.5f);
                hub.Flush();
                Assert.AreEqual(Color.green, fixture.ReadPropertyBlock(0).GetColor(appearanceId));
                Assert.IsTrue(fixture.ReadPropertyBlock(1).isEmpty);
                hub.Release(binding);
                hub.Flush();
                Assert.IsTrue(fixture.ReadPropertyBlock(0).isEmpty);
                var restored = new MaterialPropertyBlock();
                fixture.Renderer.GetPropertyBlock(restored);
                Assert.AreEqual(Color.green, restored.GetColor(appearanceId));
                Assert.AreEqual(0f, restored.GetFloat(effectId));
            }
        }

        [Test]
        public void Release_DestroyedRendererReturnsBaselineAndRemovesTarget()
        {
            using (RendererFixture fixture = RendererFixture.Create())
            {
                var hub = new MaterialBindingHub();
                MaterialBinding binding = hub.Bind(new MxRenderSubjectId(1), MxMaterialChannel.HitFlash,
                    MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                UnityEngine.Object.DestroyImmediate(fixture.Renderer);
                Assert.IsTrue(hub.Release(binding));
                hub.Flush();
                var diagnostics = hub.CaptureDiagnostics();
                Assert.AreEqual(0, diagnostics.BindingCount);
                Assert.AreEqual(0, diagnostics.TargetCount);
                Assert.AreEqual(0, diagnostics.LastAppliedTargetCount);
                Assert.AreEqual(1, diagnostics.PooledPropertyBlockCount);
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

            public static RendererFixture Create(int materialCount = 1, string shaderName = null)
            {
                var gameObject = new GameObject("MaterialBindingHubTests");
                var mesh = new Mesh();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
                var materials = new Material[Math.Max(1, materialCount)];
                Shader shader = shaderName != null ? Shader.Find(shaderName) :
                    Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                Assert.IsNotNull(shader);
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
