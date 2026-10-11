using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests
{
    public sealed partial class VfxManualCompositionTests
    {
        [Test]
        public void DerivedMaterialPreview_ReplacesTextureWithoutMutatingSourceMaterial()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            string textureGuid = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Piloto Studio" })
                .FirstOrDefault(value => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(value)) != null);
            Assert.That(shader, Is.Not.Null);
            Assert.That(textureGuid, Is.Not.Empty);
            var root = new GameObject("Derived material fixture");
            Material sourceMaterial = new Material(shader);
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = sourceMaterial;
                var member = new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = "source.member.material",
                    ParticleSystem = system,
                    Profile = new VfxComponentProfileView(),
                    ShaderProfile = new VfxShaderProfileView()
                };
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "render.texture.main",
                    ShaderPropertyName = "_MainTex",
                    PropertyType = "Texture",
                    RequiresDerivedMaterial = true
                });
                var resolved = new VfxResolvedLayerAuthoringProfile();
                resolved.Members.Add(member);
                var layer = new VfxCompositionLayerView { LayerId = "layer.material" };
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "render.texture.main",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = new Newtonsoft.Json.Linq.JValue(textureGuid)
                });
                var compiled = new VfxCompiledLayerView { SourceSegments = new VfxTimeSegmentsView(), TargetSegments = new VfxTimeSegmentsView() };
                Texture original = sourceMaterial.GetTexture("_MainTex");
                using (var session = new VfxSemanticMaterialDerivationSession(null))
                {
                    session.Apply(resolved, layer, compiled);
                    Assert.That(renderer.sharedMaterial, Is.Not.SameAs(sourceMaterial));
                    Assert.That(renderer.sharedMaterial.GetTexture("_MainTex"), Is.Not.Null);
                    Assert.That(sourceMaterial.GetTexture("_MainTex"), Is.SameAs(original));
                    Assert.That(compiled.Diagnostics.Any(value => value.Severity == "Error"), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void DerivedMaterialPreview_MergesMultipleTextureOverridesIntoOneClone()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Piloto Studio" })
                .Where(value => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(value)) != null)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();
            Assert.That(shader, Is.Not.Null);
            Assert.That(textureGuids, Has.Length.EqualTo(2));
            var root = new GameObject("Derived material merge fixture");
            Material sourceMaterial = new Material(shader);
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = sourceMaterial;
                var member = new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = "source.member.material.merge",
                    ParticleSystem = system,
                    Profile = new VfxComponentProfileView(),
                    ShaderProfile = new VfxShaderProfileView()
                };
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "render.texture.main",
                    ShaderPropertyName = "_MainTex",
                    PropertyType = "Texture",
                    RequiresDerivedMaterial = true
                });
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "render.texture.noise",
                    ShaderPropertyName = "_DetailNoise",
                    PropertyType = "Texture",
                    RequiresDerivedMaterial = true
                });
                var resolved = new VfxResolvedLayerAuthoringProfile();
                resolved.Members.Add(member);
                var layer = new VfxCompositionLayerView { LayerId = "layer.material.merge" };
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "render.texture.main",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = new Newtonsoft.Json.Linq.JValue(textureGuids[0])
                });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "render.texture.noise",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = new Newtonsoft.Json.Linq.JValue(textureGuids[1])
                });
                var compiled = new VfxCompiledLayerView { SourceSegments = new VfxTimeSegmentsView(), TargetSegments = new VfxTimeSegmentsView() };
                Texture originalMain = sourceMaterial.GetTexture("_MainTex");
                Texture originalNoise = sourceMaterial.GetTexture("_DetailNoise");

                using (var session = new VfxSemanticMaterialDerivationSession(null))
                {
                    session.Apply(resolved, layer, compiled);
                    Material result = renderer.sharedMaterial;
                    Assert.That(result, Is.Not.SameAs(sourceMaterial));
                    Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(result.GetTexture("_MainTex"))), Is.EqualTo(textureGuids[0]));
                    Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(result.GetTexture("_DetailNoise"))), Is.EqualTo(textureGuids[1]));
                    Assert.That(sourceMaterial.GetTexture("_MainTex"), Is.SameAs(originalMain));
                    Assert.That(sourceMaterial.GetTexture("_DetailNoise"), Is.SameAs(originalNoise));
                    Assert.That(compiled.Diagnostics.Any(value => value.Severity == "Error"), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void DerivedMaterialPreview_AppliesRenderQueueAndKeywordWithoutMutatingSource()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            string textureGuid = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Piloto Studio" })
                .First(value => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(value)) != null);
            Assert.That(shader, Is.Not.Null);
            var root = new GameObject("Derived material state fixture");
            Material sourceMaterial = new Material(shader) { renderQueue = 3000 };
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = sourceMaterial;
                var member = new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = "source.member.material.state",
                    ParticleSystem = system,
                    Profile = new VfxComponentProfileView(),
                    ShaderProfile = new VfxShaderProfileView()
                };
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "render.state.renderQueue",
                    ShaderPropertyName = "@RenderQueue",
                    PropertyType = "Int",
                    RequiresDerivedMaterial = true
                });
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "render.texture.mask",
                    ShaderPropertyName = "_AlphaOverride",
                    PropertyType = "Texture",
                    RequiresDerivedMaterial = true,
                    Keyword = "_USEALPHAOVERRIDE"
                });
                var resolved = new VfxResolvedLayerAuthoringProfile();
                resolved.Members.Add(member);
                var layer = new VfxCompositionLayerView { LayerId = "layer.material.state" };
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.state.renderQueue", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue(3150) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.texture.mask", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue(textureGuid) });
                var compiled = new VfxCompiledLayerView { SourceSegments = new VfxTimeSegmentsView(), TargetSegments = new VfxTimeSegmentsView() };
                using (var session = new VfxSemanticMaterialDerivationSession(null))
                {
                    session.Apply(resolved, layer, compiled);
                    Assert.That(renderer.sharedMaterial, Is.Not.SameAs(sourceMaterial));
                    Assert.That(renderer.sharedMaterial.renderQueue, Is.EqualTo(3150));
                    Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_USEALPHAOVERRIDE"), Is.True);
                    Assert.That(sourceMaterial.renderQueue, Is.EqualTo(3000));
                    Assert.That(sourceMaterial.IsKeywordEnabled("_USEALPHAOVERRIDE"), Is.False);
                    Assert.That(compiled.Diagnostics.Any(value => value.Severity == "Error"), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void BakedMaterialCurveTrack_PreservesCurveAndNormalizesLayerFrames()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            var root = new GameObject("Baked material curve fixture");
            Material material = new Material(shader);
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                var member = new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = "source.member.curve",
                    ParticleSystem = system,
                    Profile = new VfxComponentProfileView(),
                    ShaderProfile = new VfxShaderProfileView()
                };
                member.ShaderProfile.Bindings.Add(new VfxShaderPropertyBindingView
                {
                    SemanticPropertyId = "appearance.erosion",
                    ShaderPropertyName = "_Alpha_Clip_Threshold",
                    PropertyType = "Float",
                    DynamicPropertyBlock = true,
                    TimeDomain = nameof(VfxSemanticTimeDomain.LayerFrame)
                });
                var resolved = new VfxResolvedLayerAuthoringProfile();
                resolved.Members.Add(member);
                var layer = new VfxCompositionLayerView { LayerId = "layer.curve", Phase = "Loop" };
                var curve = new AnimationCurve(
                    new Keyframe(0f, 0.1f, 0f, 0.02f),
                    new Keyframe(120f, 0.9f, 0.03f, 0f));
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "appearance.erosion",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = VfxCurveAuthoringJson.Capture(curve)
                });
                var compiled = new VfxCompiledLayerView
                {
                    StartFrame = 0,
                    SourceSegments = new VfxTimeSegmentsView { BodyFrames = 120 },
                    TargetSegments = new VfxTimeSegmentsView { BodyFrames = 120 }
                };
                var tracks = new List<UnityVfxBakedPropertyTrack>();

                VfxSemanticParticleSystemAdapter.AppendBakedPropertyTracks(resolved, layer, compiled, tracks);

                Assert.That(tracks, Has.Count.EqualTo(1));
                Assert.That(tracks[0].FloatCurve.length, Is.EqualTo(2));
                Assert.That(tracks[0].FloatCurve.keys[0].time, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(tracks[0].FloatCurve.keys[1].time, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(tracks[0].FloatCurve.Evaluate(0f), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(tracks[0].FloatCurve.Evaluate(1f), Is.EqualTo(0.9f).Within(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static GameObject CreateRetimeParticleFixture(string name)
        {
            var instance = new GameObject(name);
            ParticleSystem system = instance.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 100f / 60f;
            main.loop = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 10f),
                new Keyframe(0.9f, 10f),
                new Keyframe(1f, 0f)));
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(5f / 60f, 1),
                new ParticleSystem.Burst(95f / 60f, 1)
            });
            return instance;
        }

        private static VfxCompositionLayerView SemanticFixtureLayer(VfxRetimeMode mode, int targetFrames)
        {
            return new VfxCompositionLayerView
            {
                LayerId = "layer.fixture",
                ElementId = "element.fixture",
                StartFrame = 0,
                Timing = new VfxLayerTimingView
                {
                    RetimeMode = mode.ToString(),
                    SourceSegments = new VfxTimeSegmentsView
                    {
                        EntryFrames = 10,
                        BodyFrames = 80,
                        ExitFrames = 10,
                        MinimumBodyFrames = 20,
                        CycleFrames = 20
                    },
                    TargetArtisticFrames = targetFrames
                }
            };
        }

        private static VfxSemanticCompileContext SemanticFixtureContext()
        {
            var profile = new VfxComponentProfileView
            {
                ProfileId = "vfx.component.fixture",
                SourceComponentId = "source.fixture",
                Status = nameof(VfxProfileReviewState.Confirmed),
                Timing = new VfxProfileTimingView
                {
                    EntryFrames = 10,
                    BodyFrames = 80,
                    ExitFrames = 10,
                    MinimumBodyFrames = 20,
                    CycleFrames = 20,
                    RetimeMechanism = "EmitterWindow",
                    TailPolicy = "EndLocked"
                }
            };
            profile.Events.Add(new VfxTimeEventView { EventId = "entry", Frame = 5, Anchor = nameof(VfxTimeEventAnchor.StartLocked) });
            profile.Events.Add(new VfxTimeEventView { EventId = "exit", Frame = 95, Anchor = nameof(VfxTimeEventAnchor.EndLocked) });
            return new VfxSemanticCompileContext
            {
                Registry = new VfxSemanticPropertyRegistryView(),
                ComponentProfile = profile,
                Events = profile.Events,
                AllowAutoSuggestedProfile = true
            };
        }
    }
}

