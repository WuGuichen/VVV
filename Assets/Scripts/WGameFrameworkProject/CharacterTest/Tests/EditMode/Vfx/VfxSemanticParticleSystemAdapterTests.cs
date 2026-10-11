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
        public void ParticleSystemAdapter_VisualStartNormalizesCommonDelayAndPreservesRelativeTiming()
        {
            string token = Guid.NewGuid().ToString("N");
            var firstSource = new VfxSourceComponentView
            {
                ComponentId = "source.component.visual-start.first." + token,
                DisplayName = "Visual start first",
                SourceContentHash = "sha256:" + new string('a', 64),
                DurationSeconds = 1f
            };
            var secondSource = new VfxSourceComponentView
            {
                ComponentId = "source.component.visual-start.second." + token,
                DisplayName = "Visual start second",
                SourceContentHash = "sha256:" + new string('b', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.visual-start." + token,
                DisplayName = "Visual start fixture"
            };
            element.MemberComponentIds.Add(firstSource.ComponentId);
            element.MemberComponentIds.Add(secondSource.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(firstSource.ComponentId, firstSource);
            catalog.Components.Add(secondSource.ComponentId, secondSource);
            catalog.Elements.Add(element);

            GameObject CreateFixture(string name)
            {
                var root = new GameObject(name);
                ParticleSystem first = new GameObject("First").AddComponent<ParticleSystem>();
                first.transform.SetParent(root.transform, false);
                ParticleSystem.MainModule firstMain = first.main;
                firstMain.startDelay = 30f / 60f;
                ParticleSystem.EmissionModule firstEmission = first.emission;
                firstEmission.rateOverTime = 10f;

                ParticleSystem second = new GameObject("Second").AddComponent<ParticleSystem>();
                second.transform.SetParent(root.transform, false);
                ParticleSystem.MainModule secondMain = second.main;
                secondMain.startDelay = 45f / 60f;
                ParticleSystem.EmissionModule secondEmission = second.emission;
                secondEmission.rateOverTime = 10f;
                return root;
            }

            GameObject normalized = CreateFixture("Visual start normalized");
            GameObject manual = CreateFixture("Visual start manual");
            GameObject preserved = CreateFixture("Visual start preserved");
            try
            {
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 60);
                layer.ElementId = element.ElementId;
                VfxCompiledLayerView normalizedResult = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    normalized, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);
                ParticleSystem[] normalizedSystems = normalized.GetComponentsInChildren<ParticleSystem>(true);

                Assert.That(normalizedResult.SourceLeadInFrames, Is.EqualTo(30));
                Assert.That(normalizedResult.ResolvedLeadInFrames, Is.EqualTo(0));
                Assert.That(normalizedSystems[0].main.startDelay.constant, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(normalizedSystems[1].main.startDelay.constant, Is.EqualTo(15f / 60f).Within(0.00001f));

                layer.VisualStart = new VfxLayerVisualStartView
                {
                    Mode = nameof(VfxVisualStartMode.ManualLeadIn),
                    ManualLeadInFrames = 12
                };
                VfxCompiledLayerView manualResult = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    manual, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);
                ParticleSystem[] manualSystems = manual.GetComponentsInChildren<ParticleSystem>(true);

                Assert.That(manualResult.SourceLeadInFrames, Is.EqualTo(30));
                Assert.That(manualResult.ResolvedLeadInFrames, Is.EqualTo(12));
                Assert.That(manualSystems[0].main.startDelay.constant, Is.EqualTo(12f / 60f).Within(0.00001f));
                Assert.That(manualSystems[1].main.startDelay.constant, Is.EqualTo(27f / 60f).Within(0.00001f));
                Assert.That(manualResult.FieldDiffs.Any(value => value.PropertyId == "timing.visualStart"), Is.True);

                layer.VisualStart.Mode = nameof(VfxVisualStartMode.PreserveSource);
                VfxCompiledLayerView preservedResult = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    preserved, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);
                ParticleSystem[] preservedSystems = preserved.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(preservedResult.ResolvedLeadInFrames, Is.EqualTo(30));
                Assert.That(preservedSystems[0].main.startDelay.constant, Is.EqualTo(30f / 60f).Within(0.00001f));
                Assert.That(preservedSystems[1].main.startDelay.constant, Is.EqualTo(45f / 60f).Within(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalized);
                UnityEngine.Object.DestroyImmediate(manual);
                UnityEngine.Object.DestroyImmediate(preserved);
            }
        }

        [Test]
        public void ParticleSystemAdapter_RecompilesFromFreshSourceWithoutCumulativeRetimeError()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.recompile." + token,
                DisplayName = "Recompile fixture",
                SourceContentHash = "sha256:" + new string('2', 64),
                DurationSeconds = 100f / 60f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.recompile." + token, DisplayName = "Recompile fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);

            GameObject extended = CreateRetimeParticleFixture("Extended source");
            GameObject shortenedA = CreateRetimeParticleFixture("Shortened source A");
            GameObject shortenedB = CreateRetimeParticleFixture("Shortened source B");
            try
            {
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 200);
                layer.ElementId = element.ElementId;
                VfxSemanticParticleSystemAdapter.CompileAndApply(extended, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _), true, true, false, out _);

                layer.Timing.TargetArtisticFrames = 120;
                VfxCompiledLayerView first = VfxSemanticParticleSystemAdapter.CompileAndApply(shortenedA, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _), true, true, false, out _);
                VfxCompiledLayerView second = VfxSemanticParticleSystemAdapter.CompileAndApply(shortenedB, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _), true, true, false, out _);

                Assert.That(first.Valid, Is.True);
                Assert.That(second.Valid, Is.True);
                Assert.That(first.CompileHash, Is.EqualTo(second.CompileHash));
                Assert.That(shortenedA.GetComponent<ParticleSystem>().main.duration, Is.EqualTo(2f).Within(0.00001f));
                Assert.That(shortenedB.GetComponent<ParticleSystem>().main.duration, Is.EqualTo(2f).Within(0.00001f));
                Assert.That(shortenedA.GetComponent<ParticleSystem>().emission.rateOverTime.curve.keys.Select(value => value.time),
                    Is.EqualTo(shortenedB.GetComponent<ParticleSystem>().emission.rateOverTime.curve.keys.Select(value => value.time)).Within(0.00001f));
                Assert.That(extended.GetComponent<ParticleSystem>().main.duration, Is.EqualTo(200f / 60f).Within(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(extended);
                UnityEngine.Object.DestroyImmediate(shortenedA);
                UnityEngine.Object.DestroyImmediate(shortenedB);
            }
        }

        [Test]
        public void ParticleSystemAdapter_PlaybackWindowPreservesSourceParticleTiming()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.window." + token,
                DisplayName = "Playback window fixture",
                SourceContentHash = "sha256:" + new string('3', 64),
                DurationSeconds = 100f / 60f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.window." + token, DisplayName = "Playback window fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);

            GameObject instance = CreateRetimeParticleFixture("Playback window source");
            try
            {
                ParticleSystem system = instance.GetComponent<ParticleSystem>();
                float sourceDuration = system.main.duration;
                float[] sourceCurveTimes = system.emission.rateOverTime.curve.keys.Select(value => value.time).ToArray();
                var sourceBursts = new ParticleSystem.Burst[system.emission.burstCount];
                system.emission.GetBursts(sourceBursts);
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.PlaybackWindow, 40);
                layer.ElementId = element.ElementId;

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, true, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
                Assert.That(compiled.PlaybackWindowFrames, Is.EqualTo(40));
                Assert.That(system.main.duration, Is.EqualTo(sourceDuration).Within(0.00001f));
                Assert.That(system.emission.rateOverTime.curve.keys.Select(value => value.time), Is.EqualTo(sourceCurveTimes).Within(0.00001f));
                var targetBursts = new ParticleSystem.Burst[system.emission.burstCount];
                system.emission.GetBursts(targetBursts);
                Assert.That(targetBursts.Select(value => value.time), Is.EqualTo(sourceBursts.Select(value => value.time)).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Any(value => value.PropertyId.StartsWith("timing.", StringComparison.Ordinal)), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ParticleSystemAdapter_OpacityEnvelopeCreatesDeterministicColorOverLifetimeGradient()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.opacity." + token,
                DisplayName = "Opacity fixture",
                SourceContentHash = "sha256:" + new string('4', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.opacity." + token, DisplayName = "Opacity fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            GameObject instance = new GameObject("Opacity semantic fixture");
            try
            {
                ParticleSystem system = instance.AddComponent<ParticleSystem>();
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
                layer.ElementId = element.ElementId;
                var envelope = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(0.8f, 1f),
                    new Keyframe(1f, 0f));
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "appearance.opacityEnvelope",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = VfxCurveAuthoringJson.Capture(envelope)
                });

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                Assert.That(system.colorOverLifetime.enabled, Is.True);
                Gradient gradient = system.colorOverLifetime.color.gradient;
                Assert.That(gradient.Evaluate(0f).a, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(gradient.Evaluate(0.2f).a, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(gradient.Evaluate(0.8f).a, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(gradient.Evaluate(1f).a, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Any(value => value.UnityPath == "colorOverLifetime.color.alpha"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ParticleSystemAdapter_ExactGradientPreservesTwoGradientMode()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.gradient." + token,
                DisplayName = "Gradient fixture",
                SourceContentHash = "sha256:" + new string('5', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.gradient." + token, DisplayName = "Gradient fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            GameObject instance = new GameObject("Exact gradient fixture");
            try
            {
                ParticleSystem system = instance.AddComponent<ParticleSystem>();
                var minimum = new Gradient();
                minimum.SetKeys(
                    new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 1f) },
                    new[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(0.7f, 1f) });
                var maximum = new Gradient();
                maximum.SetKeys(
                    new[] { new GradientColorKey(Color.blue, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(1f, 1f) });
                var exact = new ParticleSystem.MinMaxGradient(minimum, maximum);
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
                layer.ElementId = element.ElementId;
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "appearance.colorOverLifetime.exact",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = VfxMinMaxGradientAuthoringJson.Capture(exact)
                });

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                ParticleSystem.MinMaxGradient result = system.colorOverLifetime.color;
                Assert.That(result.mode, Is.EqualTo(ParticleSystemGradientMode.TwoGradients));
                Assert.That(result.gradientMin.Evaluate(0f).a, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(result.gradientMax.Evaluate(1f).r, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(result.gradientMax.Evaluate(1f).g, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(result.gradientMax.Evaluate(1f).b, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(result.gradientMax.Evaluate(1f).a, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Any(value => value.UnityPath == "colorOverLifetime.color"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ParticleSystemAdapter_ExactAxisCurveAndEmissionWindowPreserveOtherAxes()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.exact-axis." + token,
                DisplayName = "Exact axis fixture",
                SourceContentHash = "sha256:" + new string('6', 64),
                DurationSeconds = 2f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.exact-axis." + token, DisplayName = "Exact axis fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            GameObject instance = new GameObject("Exact axis fixture");
            try
            {
                ParticleSystem system = instance.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = system.main;
                main.duration = 2f;
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(1f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(2f);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(3f);
                var minimum = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
                var maximum = AnimationCurve.Linear(0f, 2f, 1f, 4f);
                var exactY = new ParticleSystem.MinMaxCurve(1.5f, minimum, maximum);
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 120);
                layer.ElementId = element.ElementId;
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "shape.startSize.y.exact",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = VfxMinMaxCurveAuthoringJson.Capture(exactY)
                });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "emission.window.startFrame",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = new Newtonsoft.Json.Linq.JValue(12)
                });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "emission.window.endFrame",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = new Newtonsoft.Json.Linq.JValue(102)
                });

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                Assert.That(system.main.startSize3D, Is.True);
                Assert.That(system.main.startSizeX.constant, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(system.main.startSizeZ.constant, Is.EqualTo(3f).Within(0.00001f));
                Assert.That(system.main.startSizeY.mode, Is.EqualTo(ParticleSystemCurveMode.TwoCurves));
                Assert.That(system.main.startSizeY.curveMultiplier, Is.EqualTo(1.5f).Within(0.00001f));
                Assert.That(system.main.startDelay.constant, Is.EqualTo(12f / 60f).Within(0.00001f));
                Assert.That(system.main.duration, Is.EqualTo(90f / 60f).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Any(value => value.UnityPath == "main.startSizeY"), Is.True);
                Assert.That(compiled.FieldDiffs.Any(value => value.UnityPath == "main.duration"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ParticleSystemRendererAdapter_AppliesTypedModePivotAndScreenLimits()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.renderer." + token,
                DisplayName = "Renderer fixture",
                SourceContentHash = "sha256:" + new string('8', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.renderer." + token, DisplayName = "Renderer fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            var root = new GameObject("Renderer adapter fixture");
            try
            {
                root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortMode = ParticleSystemSortMode.None;
                renderer.pivot = Vector3.zero;
                renderer.minParticleSize = 0.01f;
                renderer.maxParticleSize = 0.5f;
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 60);
                layer.ElementId = element.ElementId;
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.mode", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue((int)ParticleSystemRenderMode.Mesh) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.alignment", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue((int)ParticleSystemRenderSpace.World) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.sortMode", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue((int)ParticleSystemSortMode.YoungestInFront) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.pivot", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JArray(0.1f, 0.2f, 0.3f) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.minParticleSize", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue(0.05f) });
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView { PropertyId = "render.renderer.maxParticleSize", Operation = nameof(VfxSemanticOperation.Replace), Value = new Newtonsoft.Json.Linq.JValue(0.8f) });

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    root, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
                Assert.That(renderer.alignment, Is.EqualTo(ParticleSystemRenderSpace.World));
                Assert.That(renderer.sortMode, Is.EqualTo(ParticleSystemSortMode.YoungestInFront));
                Assert.That(renderer.pivot, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                Assert.That(renderer.minParticleSize, Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(renderer.maxParticleSize, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Count(value => value.Adapter == "ParticleRenderer"), Is.GreaterThanOrEqualTo(6));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SourceSnapshotV2_CapturesStructuredModulesCustomDataAndRendererStreams()
        {
            var root = new GameObject("Snapshot v2 fixture");
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.radius = 2.5f;
                shape.angle = 33f;
                shape.arc = 210f;
                shape.randomDirectionAmount = 0.4f;
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));
                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.strength = 1.75f;
                noise.frequency = 0.65f;
                ParticleSystem.TrailModule trails = system.trails;
                trails.enabled = true;
                trails.ratio = 0.65f;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.75f);
                trails.minVertexDistance = 0.15f;
                trails.dieWithParticles = false;
                ParticleSystem.CustomDataModule custom = system.customData;
                custom.enabled = true;
                custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
                custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0.75f));
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.Custom1XYZW
                });

                VfxParticleSystemSnapshotView snapshot = VfxParticleSystemSnapshotCapture.Capture(
                    new VfxSourceComponentView { ComponentId = "source.snapshot.v2", SourceContentHash = "sha256:" + new string('6', 64) },
                    system);

                Assert.That(snapshot.ShapeRadius, Is.EqualTo(2.5f).Within(0.00001f));
                Assert.That(snapshot.ShapeAngle, Is.EqualTo(33f).Within(0.00001f));
                Assert.That(snapshot.ShapeArc, Is.EqualTo(210f).Within(0.00001f));
                Assert.That(snapshot.VelocitySpeedModifier.CurveMax.Keys.Count, Is.EqualTo(2));
                Assert.That(snapshot.NoiseFrequency, Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(snapshot.Trails.Enabled, Is.True);
                Assert.That(snapshot.Trails.Ratio, Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(snapshot.Trails.Lifetime.ConstantMax, Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(snapshot.Trails.MinimumVertexDistance, Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(snapshot.Trails.DieWithParticles, Is.False);
                Assert.That(snapshot.Trails.ContentHash, Does.StartWith("sha256:"));
                Assert.That(snapshot.CustomData.Any(value => value.Stream == (int)ParticleSystemCustomData.Custom1 && value.Mode == (int)ParticleSystemCustomDataMode.Vector), Is.True);
                Assert.That(snapshot.VertexStreams, Does.Contain((int)ParticleSystemVertexStream.Custom1XYZW));
                Assert.That(snapshot.SnapshotHash, Does.StartWith("sha256:"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CustomDataAdapter_RequiresConfirmedVectorContractAndPreservesMinMaxCurve()
        {
            var root = new GameObject("Custom Data adapter fixture");
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystem.CustomDataModule custom = system.customData;
                custom.enabled = true;
                custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
                custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0.25f));
                var exact = new ParticleSystem.MinMaxCurve(
                    2f,
                    AnimationCurve.Linear(0f, 0.2f, 1f, 0.8f),
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f));
                var semantic = new VfxSemanticOverrideView
                {
                    PropertyId = "render.customData.custom1.x.exact",
                    Operation = nameof(VfxSemanticOperation.Replace),
                    Value = VfxMinMaxCurveAuthoringJson.Capture(exact)
                };
                var binding = new VfxSemanticBindingView
                {
                    PropertyId = semantic.PropertyId,
                    Adapter = "ParticleCustomDataExact",
                    Target = "Custom1.X",
                    Operation = nameof(VfxSemanticOperation.Replace)
                };
                var compiled = new VfxCompiledLayerView();

                VfxSemanticParticleSystemAdapter.ApplyCustomDataExact(system, "member.custom", semantic, binding, compiled);

                ParticleSystem.MinMaxCurve result = system.customData.GetVector(ParticleSystemCustomData.Custom1, 0);
                Assert.That(result.mode, Is.EqualTo(ParticleSystemCurveMode.TwoCurves));
                Assert.That(result.curveMultiplier, Is.EqualTo(2f).Within(0.00001f));
                Assert.That(result.curveMin.Evaluate(0f), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(result.curveMax.Evaluate(1f), Is.EqualTo(1f).Within(0.00001f));
                Assert.That(compiled.FieldDiffs.Single().UnityPath, Is.EqualTo("customData.Custom1.X"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UberFxShaderProfile_UsesVerifiedPaletteTextureAndCustomDataContracts()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            Assert.That(shader, Is.Not.Null, "Piloto Studio/UberFXSG must remain available for the curated source library.");
            var root = new GameObject("UberFX profile fixture");
            Material material = new Material(shader);
            try
            {
                root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.Custom1XYZW
                });

                VfxShaderProfileView profile = VfxShaderProfileSuggestion.Create(renderer);

                Assert.That(profile.ProfileId, Is.EqualTo("vfx.shader.piloto.uberfxsg"));
                Assert.That(profile.ShaderGuid, Is.EqualTo("7f11fe28133f1a74594785107b98f2fe"));
                Assert.That(profile.Bindings.Any(value => value.SemanticPropertyId == "appearance.palette.primary" && value.ShaderPropertyName == "_WhiteColor" && value.DynamicPropertyBlock), Is.True);
                Assert.That(profile.Bindings.Any(value => value.SemanticPropertyId == "render.texture.main" && value.ShaderPropertyName == "_MainTex" && value.RequiresDerivedMaterial), Is.True);
                Assert.That(profile.RequiredVertexStreams, Is.EqualTo(new[] { "Position", "Normal", "Color", "UV" }));
                Assert.That(profile.CustomDataBindings.Single().Editable, Is.False);
                Assert.That(profile.ContentHash, Does.StartWith("sha256:"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void UberFxShaderProfile_AllowsRendererWithoutOptionalCustomDataStream()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            Assert.That(shader, Is.Not.Null);
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.uber-no-custom." + token,
                DisplayName = "Uber without custom stream",
                SourceContentHash = "sha256:" + new string('6', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.uber-no-custom." + token,
                DisplayName = "Uber without custom stream"
            };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            var root = new GameObject("Uber without custom stream");
            Material material = new Material(shader);
            try
            {
                root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                var sourceStreams = new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV
                };
                renderer.SetActiveVertexStreams(sourceStreams);
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 60);
                layer.ElementId = element.ElementId;

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    root,
                    layer,
                    catalog,
                    element,
                    new VfxSemanticPropertyRegistryService().Load(out _),
                    true,
                    true,
                    false,
                    out _);

                Assert.That(compiled.Diagnostics.Any(value =>
                    value.Severity == "Error"
                    && (value.Code == "VFXSHD001" || value.Code == "VFXSHD002")), Is.False);
                var activeStreams = new List<ParticleSystemVertexStream>();
                renderer.GetActiveVertexStreams(activeStreams);
                Assert.That(activeStreams, Is.EqualTo(sourceStreams));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void UberFxEmissionExposure_UsesColorPropertyBlocksWithoutMutatingMaterial()
        {
            Shader shader = Shader.Find("Piloto Studio/UberFXSG");
            Assert.That(shader, Is.Not.Null);
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.uber-ev." + token,
                DisplayName = "Uber EV fixture",
                SourceContentHash = "sha256:" + new string('7', 64),
                DurationSeconds = 1f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.uber-ev." + token, DisplayName = "Uber EV fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);
            var root = new GameObject("Uber EV fixture");
            Material material = new Material(shader);
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.Custom1XYZW
                });
                Color sourceWhite = new Color(0.5f, 0.25f, 0.125f, 0.8f);
                Color sourceMid = new Color(0.2f, 0.3f, 0.4f, 0.7f);
                Color sourceLast = new Color(0.1f, 0.15f, 0.2f, 0.6f);
                material.SetColor("_WhiteColor", sourceWhite);
                material.SetColor("_MidColor", sourceMid);
                material.SetColor("_LastColor", sourceLast);
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 60);
                layer.ElementId = element.ElementId;
                layer.SemanticOverrides.Add(new VfxSemanticOverrideView
                {
                    PropertyId = "appearance.emission.exposureEv",
                    Operation = nameof(VfxSemanticOperation.Add),
                    Value = new Newtonsoft.Json.Linq.JValue(1f)
                });

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    root, layer, catalog, element, new VfxSemanticPropertyRegistryService().Load(out _),
                    true, true, false, out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_WhiteColor").r, Is.EqualTo(sourceWhite.r * 2f).Within(0.00001f));
                Assert.That(block.GetColor("_MidColor").g, Is.EqualTo(sourceMid.g * 2f).Within(0.00001f));
                Assert.That(block.GetColor("_LastColor").b, Is.EqualTo(sourceLast.b * 2f).Within(0.00001f));
                Assert.That(block.GetColor("_WhiteColor").a, Is.EqualTo(sourceWhite.a).Within(0.00001f));
                Assert.That(material.GetColor("_WhiteColor"), Is.EqualTo(sourceWhite));
                Assert.That(compiled.FieldDiffs.Count(value => value.PropertyId == "appearance.emission.exposureEv"), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
