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
        public void SemanticRetime_PlaybackWindowKeepsSourceTimingAndOnlyChangesLayerEndFrame()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.PlaybackWindow, 40);

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            Assert.That(compiled.TargetSegments.ArtisticFrames, Is.EqualTo(compiled.SourceSegments.ArtisticFrames));
            Assert.That(compiled.PlaybackWindowFrames, Is.EqualTo(40));
            Assert.That(compiled.ArtisticEndFrame, Is.EqualTo(layer.StartFrame + 40));
            Assert.That(compiled.FieldDiffs, Is.Empty);
        }

        [Test]
        public void SemanticRetime_ExtendBodyPreservesAbsoluteEntryExitAndBuildsStableMap()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 200);
            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            Assert.That(compiled.TargetSegments.EntryFrames, Is.EqualTo(10));
            Assert.That(compiled.TargetSegments.ExitFrames, Is.EqualTo(10));
            Assert.That(compiled.TargetSegments.BodyFrames, Is.EqualTo(180));
            Assert.That(compiled.TimeMap.Select(value => value.SourceFrame), Is.EqualTo(new[] { 0, 10, 90, 100 }));
            Assert.That(compiled.TimeMap.Select(value => value.TargetFrame), Is.EqualTo(new[] { 0, 10, 190, 200 }));
            Assert.That(compiled.Events.Single(value => value.EventId == "entry").TargetFrame, Is.EqualTo(5));
            Assert.That(compiled.Events.Single(value => value.EventId == "exit").TargetFrame, Is.EqualTo(195));
        }

        [Test]
        public void SemanticRetime_NormalizedEmitterCurveKeepsEntryAndExitKeyTimes()
        {
            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(
                SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 200),
                SemanticFixtureContext());
            var source = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.9f, 1f),
                new Keyframe(1f, 0f));

            AnimationCurve target = VfxSemanticRetimeCompiler.RemapNormalizedCurve(source, compiled.TimeMap, 100, 200);

            Assert.That(target.keys[0].time, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(target.keys[1].time, Is.EqualTo(0.05f).Within(0.00001f));
            Assert.That(target.keys[2].time, Is.EqualTo(0.95f).Within(0.00001f));
            Assert.That(target.keys[3].time, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(target.keys.Select(value => value.value), Is.EqualTo(source.keys.Select(value => value.value)).Within(0.00001f));
        }

        [Test]
        public void SemanticCurveJson_RoundTripsKeysTangentsWeightsAndWrapModes()
        {
            var source = new AnimationCurve(
                new Keyframe(0f, 0f, 0.25f, 0.5f, 0.12f, 0.23f) { weightedMode = WeightedMode.Both },
                new Keyframe(0.35f, 1.4f, -0.75f, 0.2f, 0.31f, 0.42f) { weightedMode = WeightedMode.In },
                new Keyframe(1f, 0.1f, -0.4f, 0f, 0.18f, 0.33f) { weightedMode = WeightedMode.Out })
            {
                preWrapMode = WrapMode.PingPong,
                postWrapMode = WrapMode.Loop
            };

            AnimationCurve restored = VfxCurveAuthoringJson.Restore(VfxCurveAuthoringJson.Capture(source));

            Assert.That(restored.preWrapMode, Is.EqualTo(source.preWrapMode));
            Assert.That(restored.postWrapMode, Is.EqualTo(source.postWrapMode));
            Assert.That(restored.keys.Length, Is.EqualTo(source.keys.Length));
            for (int i = 0; i < source.keys.Length; i++)
            {
                Assert.That(restored.keys[i].time, Is.EqualTo(source.keys[i].time).Within(0.000001f));
                Assert.That(restored.keys[i].value, Is.EqualTo(source.keys[i].value).Within(0.000001f));
                Assert.That(restored.keys[i].inTangent, Is.EqualTo(source.keys[i].inTangent).Within(0.000001f));
                Assert.That(restored.keys[i].outTangent, Is.EqualTo(source.keys[i].outTangent).Within(0.000001f));
                Assert.That(restored.keys[i].inWeight, Is.EqualTo(source.keys[i].inWeight).Within(0.000001f));
                Assert.That(restored.keys[i].outWeight, Is.EqualTo(source.keys[i].outWeight).Within(0.000001f));
                Assert.That(restored.keys[i].weightedMode, Is.EqualTo(source.keys[i].weightedMode));
            }
        }

        [Test]
        public void SemanticCurveJson_RoundTripsUnitySteppedInfinityTangentsCanonically()
        {
            var source = new AnimationCurve(
                new Keyframe(0f, 0f, float.NegativeInfinity, float.PositiveInfinity),
                new Keyframe(1f, 1f, float.PositiveInfinity, float.NegativeInfinity));

            JToken captured = VfxCurveAuthoringJson.Capture(source);
            string canonical = VfxCanonicalJson.Serialize(captured);
            AnimationCurve restored = VfxCurveAuthoringJson.Restore(captured);

            Assert.That(captured["keys"]?[0]?["inTangent"]?.Value<float>(), Is.Zero);
            Assert.That(captured["keys"]?[0]?["inTangentSpecial"]?.Value<string>(), Is.EqualTo("NegativeInfinity"));
            Assert.That(captured["keys"]?[0]?["outTangentSpecial"]?.Value<string>(), Is.EqualTo("PositiveInfinity"));
            Assert.That(canonical, Does.Contain("NegativeInfinity"));
            Assert.That(VfxCanonicalJson.Hash(captured), Does.StartWith("sha256:"));
            Assert.That(float.IsNegativeInfinity(restored.keys[0].inTangent), Is.True);
            Assert.That(float.IsPositiveInfinity(restored.keys[0].outTangent), Is.True);
            Assert.That(float.IsPositiveInfinity(restored.keys[1].inTangent), Is.True);
            Assert.That(float.IsNegativeInfinity(restored.keys[1].outTangent), Is.True);
            VfxCurveKeySnapshotView firstKey = captured["keys"]?[0]?.ToObject<VfxCurveKeySnapshotView>();
            Assert.That(float.IsNegativeInfinity(firstKey.ScaleInTangent(0f)), Is.True);
            Assert.That(float.IsPositiveInfinity(firstKey.ScaleOutTangent(0f)), Is.True);
            Assert.That(float.IsNaN(firstKey.ScaleInTangent(0f)), Is.False);
            Assert.That(float.IsNaN(firstKey.ScaleOutTangent(0f)), Is.False);
        }

        [Test]
        public void CurveSnapshotCapture_RejectsNaNTangentWithLocatedVfxDiagnostic()
        {
            var key = new Keyframe(0f, 1f, float.NaN, 0f);

            JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() =>
                VfxCurveKeySnapshotView.Capture(key, "fixture.keys[0]"));

            Assert.That(exception.Message, Does.Contain("VFXCURVE001"));
            Assert.That(exception.Message, Does.Contain("fixture.keys[0].inTangent"));
        }

        [Test]
        public void SemanticMinMaxCurveJson_PreservesTwoCurvesWithoutFlattening()
        {
            var minimum = new AnimationCurve(
                new Keyframe(0f, 1f, 0.2f, 0.3f, 0.15f, 0.25f),
                new Keyframe(1f, 2f, 0.4f, 0.5f, 0.2f, 0.3f));
            var maximum = new AnimationCurve(
                new Keyframe(0f, 3f, 0.6f, 0.7f, 0.1f, 0.2f),
                new Keyframe(1f, 5f, 0.8f, 0.9f, 0.3f, 0.4f));
            var source = new ParticleSystem.MinMaxCurve(2.5f, minimum, maximum);

            Newtonsoft.Json.Linq.JToken token = VfxMinMaxCurveAuthoringJson.Capture(source);
            ParticleSystem.MinMaxCurve restored = VfxMinMaxCurveAuthoringJson.Restore(token, new ParticleSystem.MinMaxCurve(0f));

            Assert.That(restored.mode, Is.EqualTo(ParticleSystemCurveMode.TwoCurves));
            Assert.That(restored.curveMultiplier, Is.EqualTo(2.5f).Within(0.00001f));
            Assert.That(restored.curveMin.keys.Select(value => value.value), Is.EqualTo(minimum.keys.Select(value => value.value)).Within(0.00001f));
            Assert.That(restored.curveMax.keys.Select(value => value.value), Is.EqualTo(maximum.keys.Select(value => value.value)).Within(0.00001f));
            Assert.That(restored.curveMin.keys[0].outTangent, Is.EqualTo(minimum.keys[0].outTangent).Within(0.00001f));
            Assert.That(restored.curveMax.keys[1].inWeight, Is.EqualTo(maximum.keys[1].inWeight).Within(0.00001f));
        }

        [Test]
        public void SemanticMinMaxGradientJson_PreservesTwoGradientsAndAlphaKeys()
        {
            var minimum = new Gradient { mode = GradientMode.Blend };
            minimum.SetKeys(
                new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 1f) },
                new[] { new GradientAlphaKey(0.15f, 0f), new GradientAlphaKey(0.8f, 1f) });
            var maximum = new Gradient { mode = GradientMode.Fixed };
            maximum.SetKeys(
                new[] { new GradientColorKey(Color.blue, 0.25f), new GradientColorKey(Color.white, 0.75f) },
                new[] { new GradientAlphaKey(0.4f, 0.2f), new GradientAlphaKey(1f, 0.9f) });
            var source = new ParticleSystem.MinMaxGradient(minimum, maximum);

            Newtonsoft.Json.Linq.JToken token = VfxMinMaxGradientAuthoringJson.Capture(source);
            ParticleSystem.MinMaxGradient restored = VfxMinMaxGradientAuthoringJson.Restore(token, new ParticleSystem.MinMaxGradient(Color.black));

            Assert.That(restored.mode, Is.EqualTo(ParticleSystemGradientMode.TwoGradients));
            Assert.That(restored.gradientMin.mode, Is.EqualTo(GradientMode.Blend));
            Assert.That(restored.gradientMax.mode, Is.EqualTo(GradientMode.Fixed));
            Assert.That(restored.gradientMin.alphaKeys.Select(value => value.alpha), Is.EqualTo(minimum.alphaKeys.Select(value => value.alpha)).Within(0.00001f));
            Assert.That(restored.gradientMax.colorKeys.Select(value => value.time), Is.EqualTo(maximum.colorKeys.Select(value => value.time)).Within(0.00001f));
            Assert.That(restored.gradientMax.Evaluate(0.75f).b, Is.EqualTo(maximum.Evaluate(0.75f).b).Within(0.00001f));
        }

        [Test]
        public void SemanticRetime_RepeatBodySnapsToWholeConfirmedCycle()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.RepeatBody, 173);
            layer.Timing.SourceSegments.CycleFrames = 20;
            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.True);
            Assert.That(compiled.TargetSegments.BodyFrames % 20, Is.Zero);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXRET033"), Is.True);
        }

        [Test]
        public void SemanticRetime_FixedRejectsInternalLengthChange()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.Fixed, 120);
            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.False);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXRET060"), Is.True);
            Assert.That(
                VfxSemanticLayerInspectorController.ResolveTargetFrameFieldValue(layer, compiled.TargetSegments),
                Is.EqualTo(120),
                "The inspector must keep the requested value visible so the user can correct an invalid Fixed contract.");
            Assert.That(
                VfxSemanticLayerInspectorController.ResolveTargetFrameError(layer, compiled),
                Does.Contain("VFXRET060").And.Contain("源长度 100 帧").And.Contain("当前为 120 帧"));
        }

        [Test]
        public void SemanticRetime_AuthoredMinimumBodyOverridesImportedSuggestion()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 40);
            layer.Timing.SourceSegments.MinimumBodyFrames = 100;
            layer.Timing.MinimumBodyFrames = 20;

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            Assert.That(compiled.SourceSegments.MinimumBodyFrames, Is.EqualTo(20));
            Assert.That(compiled.TargetSegments.BodyFrames, Is.EqualTo(20));
        }

        [Test]
        public void SemanticRetime_TargetLengthIgnoresLegacyDerivedSegmentOverrides()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 60);
            layer.SemanticOverrides.Add(new VfxSemanticOverrideView
            {
                PropertyId = "timing.body.frames",
                Operation = nameof(VfxSemanticOperation.Replace),
                Value = new JValue(161)
            });

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, SemanticFixtureContext());

            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            Assert.That(compiled.TargetSegments.ArtisticFrames, Is.EqualTo(60));
            Assert.That(compiled.TargetSegments.BodyFrames, Is.EqualTo(40));
        }

        [Test]
        public void SemanticMemberOverride_ValidatesMemberRegistryAndBindingBeforeAdapterApplication()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView
            {
                MemberId = "source.fixture",
                Overrides =
                {
                    new VfxSemanticOverrideView
                    {
                        PropertyId = "unregistered.member.property",
                        Operation = nameof(VfxSemanticOperation.Replace),
                        Value = new JValue(1f)
                    }
                }
            });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Valid, Is.False);
            Assert.That(compiled.Diagnostics.Any(value =>
                value.Code == "VFXSEM010"
                && value.MemberId == "source.fixture"
                && value.PropertyId == "unregistered.member.property"), Is.True);
        }

        [Test]
        public void SemanticMemberOverride_ExplicitOverrideStateRequiresIndependentValue()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.MemberParticipation.Add(new VfxMemberParticipationView
            {
                MemberId = "source.fixture",
                PropertyStates =
                {
                    ["appearance.opacity"] = nameof(VfxMemberParticipationState.Override)
                }
            });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Valid, Is.False);
            Assert.That(compiled.Diagnostics.Any(value =>
                value.Code == "VFXSEM023"
                && value.MemberId == "source.fixture"
                && value.PropertyId == "appearance.opacity"), Is.True);
        }

        [Test]
        public void SemanticMemberParticipation_RejectsUnknownDuplicateAndInvalidStates()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.MemberParticipation.Add(new VfxMemberParticipationView
            {
                MemberId = "source.fixture",
                State = "Maybe",
                PropertyStates =
                {
                    ["missing.property"] = "Sometimes"
                }
            });
            layer.MemberParticipation.Add(new VfxMemberParticipationView { MemberId = "source.fixture" });
            layer.MemberParticipation.Add(new VfxMemberParticipationView { MemberId = "missing.member" });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Valid, Is.False);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM025"), Is.True);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM026"), Is.True);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM027"), Is.True);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM028"), Is.True);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM029"), Is.True);
        }

        [Test]
        public void SemanticMemberOverride_RejectsUnknownAndDuplicateMemberScopes()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView { MemberId = "missing.member" });
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView { MemberId = "source.fixture" });
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView { MemberId = "source.fixture" });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM019" && value.MemberId == "missing.member"), Is.True);
            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM018" && value.MemberId == "source.fixture"), Is.True);
        }

        [Test]
        public void SemanticMemberOverride_RejectsUnsupportedOrUnconfirmedParticipation()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView
            {
                MemberId = "source.fixture",
                Overrides =
                {
                    new VfxSemanticOverrideView
                    {
                        PropertyId = "appearance.opacity",
                        Operation = nameof(VfxSemanticOperation.Multiply),
                        Value = new JValue(0.5f)
                    }
                }
            });
            layer.MemberParticipation.Add(new VfxMemberParticipationView
            {
                MemberId = "source.fixture",
                PropertyStates =
                {
                    ["appearance.opacity"] = nameof(VfxMemberParticipationState.Unconfirmed)
                }
            });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.Registry.Properties.Add(new VfxSemanticPropertyDefinitionView
            {
                PropertyId = "appearance.opacity",
                ValueType = nameof(VfxSemanticValueType.Float),
                Operations = { nameof(VfxSemanticOperation.Multiply) }
            });
            context.ComponentProfile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "appearance.opacity",
                Adapter = "ParticleMain",
                Target = "main.startColor",
                Operation = nameof(VfxSemanticOperation.Multiply),
                PrimaryOpacity = true
            });
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Valid, Is.False);
            Assert.That(compiled.Diagnostics.Any(value =>
                value.Code == "VFXSEM022"
                && value.MemberId == "source.fixture"
                && value.PropertyId == "appearance.opacity"), Is.True);
        }

        [Test]
        public void SemanticMemberOverride_ReplacingSamePrimaryOpacityDoesNotCreateFalseDuplicate()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            layer.SemanticOverrides.Add(new VfxSemanticOverrideView
            {
                PropertyId = "appearance.opacity",
                Operation = nameof(VfxSemanticOperation.Multiply),
                Value = new JValue(0.8f)
            });
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView
            {
                MemberId = "source.fixture",
                Overrides =
                {
                    new VfxSemanticOverrideView
                    {
                        PropertyId = "appearance.opacity",
                        Operation = nameof(VfxSemanticOperation.Multiply),
                        Value = new JValue(0.5f)
                    }
                }
            });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.Registry.Properties.Add(new VfxSemanticPropertyDefinitionView
            {
                PropertyId = "appearance.opacity",
                ValueType = nameof(VfxSemanticValueType.Float),
                Operations = { nameof(VfxSemanticOperation.Multiply) }
            });
            context.ComponentProfile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "appearance.opacity",
                Adapter = "ParticleMain",
                Target = "main.startColor",
                Operation = nameof(VfxSemanticOperation.Multiply),
                PrimaryOpacity = true
            });
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Diagnostics.Any(value => value.Code == "VFXSEM016"), Is.False);
            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            List<VfxSemanticOverrideView> effective = VfxSemanticParticleSystemAdapter
                .EffectiveOverridesForMember(layer, "source.fixture")
                .Where(value => value.PropertyId == "appearance.opacity")
                .ToList();
            Assert.That(effective, Has.Count.EqualTo(1));
            Assert.That(effective[0].FloatValue(0f), Is.EqualTo(0.5f));
        }

        [Test]
        public void SemanticMemberRetime_OverridesLayerModeAndRejectsUnknownValue()
        {
            VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 140);
            layer.MemberOverrides.Add(new VfxMemberSemanticOverrideView
            {
                MemberId = "source.fixture",
                RetimeMode = nameof(VfxRetimeMode.ExtendTail)
            });
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };

            VfxCompiledLayerView compiled = VfxSemanticRetimeCompiler.Compile(layer, context);

            Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
            Assert.That(
                VfxSemanticRetimeCompiler.ResolveMemberRetimeMode(layer, "source.fixture", VfxRetimeMode.ExtendBody),
                Is.EqualTo(VfxRetimeMode.ExtendTail));

            layer.MemberOverrides[0].RetimeMode = "StretchEverything";
            VfxCompiledLayerView invalid = VfxSemanticRetimeCompiler.Compile(layer, context);
            Assert.That(invalid.Valid, Is.False);
            Assert.That(invalid.Diagnostics.Any(value =>
                value.Code == "VFXRET061" && value.MemberId == "source.fixture"), Is.True);

            layer.MemberOverrides[0].RetimeMode = nameof(VfxRetimeMode.ExtendTail);
            layer.MemberParticipation.Add(new VfxMemberParticipationView
            {
                MemberId = "source.fixture",
                State = nameof(VfxMemberParticipationState.Unconfirmed)
            });
            VfxCompiledLayerView unconfirmed = VfxSemanticRetimeCompiler.Compile(layer, context);
            Assert.That(unconfirmed.Valid, Is.False);
            Assert.That(unconfirmed.Diagnostics.Any(value =>
                value.Code == "VFXRET062" && value.MemberId == "source.fixture"), Is.True);
        }

        [Test]
        public void CoupledParticleModules_AllowSourcePreviewAndBlockUnsafeSemanticChanges()
        {
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };
            context.ComponentProfile.ReadOnlyModules.Add("SubEmitters");
            VfxCompositionLayerView unchanged = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);

            VfxCompiledLayerView sourcePreview = VfxSemanticRetimeCompiler.Compile(unchanged, context);

            Assert.That(sourcePreview.Valid, Is.True, string.Join("\n", sourcePreview.Diagnostics.Select(value => value.Message)));

            VfxCompositionLayerView modified = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 140);
            VfxCompiledLayerView edited = VfxSemanticRetimeCompiler.Compile(modified, context);

            Assert.That(edited.Valid, Is.False);
            Assert.That(edited.Diagnostics.Any(value => value.Code == "VFXMOD001"), Is.True);

            modified.MemberOverrides.Add(new VfxMemberSemanticOverrideView
            {
                MemberId = "source.fixture",
                RetimeMode = nameof(VfxRetimeMode.Fixed)
            });
            VfxCompiledLayerView fixedMember = VfxSemanticRetimeCompiler.Compile(modified, context);
            Assert.That(fixedMember.Valid, Is.True, string.Join("\n", fixedMember.Diagnostics.Select(value => value.Message)));
            Assert.That(fixedMember.Diagnostics.Any(value => value.Code == "VFXMOD001"), Is.False);
        }

        [Test]
        public void TrailContract_AllowsConfirmedRetimeButStillBlocksMotionEdits()
        {
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };
            context.ComponentProfile.Timing.TrailContract = new VfxTrailAuthoringContractView
            {
                RetimePolicy = "PreserveNormalizedLifetime",
                PreserveWidth = true,
                PreserveColor = true,
                SourceTrailHash = "sha256:" + new string('7', 64)
            };

            VfxCompositionLayerView retimed = SemanticFixtureLayer(VfxRetimeMode.ExtendTail, 140);
            VfxCompiledLayerView safe = VfxSemanticRetimeCompiler.Compile(retimed, context);

            Assert.That(safe.Valid, Is.True, string.Join("\n", safe.Diagnostics.Select(value => value.Message)));
            Assert.That(safe.Diagnostics.Any(value => value.Code == "VFXMOD002"), Is.False);

            context.Registry.Properties.Add(new VfxSemanticPropertyDefinitionView
            {
                PropertyId = "motion.speed",
                ValueType = nameof(VfxSemanticValueType.Float),
                Operations = { nameof(VfxSemanticOperation.Multiply) }
            });
            context.ComponentProfile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "motion.speed",
                Adapter = "ParticleMain",
                Target = "main.startSpeed",
                Operation = nameof(VfxSemanticOperation.Multiply)
            });
            retimed.SemanticOverrides.Add(new VfxSemanticOverrideView
            {
                PropertyId = "motion.speed",
                Operation = nameof(VfxSemanticOperation.Multiply),
                Value = new JValue(1.25f)
            });

            VfxCompiledLayerView unsafeMotion = VfxSemanticRetimeCompiler.Compile(retimed, context);

            Assert.That(unsafeMotion.Valid, Is.False);
            Assert.That(unsafeMotion.Diagnostics.Any(value => value.Code == "VFXMOD002"), Is.True);
        }

        [Test]
        public void TrailRetime_PreservesNormalizedLifetimeWidthAndColorContract()
        {
            var root = new GameObject("Trail retime fixture");
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystem.TrailModule trails = system.trails;
                trails.enabled = true;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.75f);
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 1f, 1f, 0f));
                trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(Color.red, Color.yellow);
                var member = new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = "source.trail",
                    ParticleSystem = system,
                    Profile = new VfxComponentProfileView
                    {
                        Timing = new VfxProfileTimingView
                        {
                            TrailContract = new VfxTrailAuthoringContractView
                            {
                                RetimePolicy = "PreserveNormalizedLifetime",
                                PreserveWidth = true,
                                PreserveColor = true
                            }
                        }
                    }
                };
                ParticleSystem.MinMaxCurve sourceWidth = trails.widthOverTrail;
                ParticleSystem.MinMaxGradient sourceColor = trails.colorOverLifetime;
                var compiled = new VfxCompiledLayerView();

                VfxSemanticParticleSystemAdapter.RetimeTrails(system, member, compiled, 1.5f);

                Assert.That(system.trails.lifetime.constantMax, Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(system.trails.widthOverTrail.curveMultiplier, Is.EqualTo(sourceWidth.curveMultiplier).Within(0.00001f));
                Assert.That(system.trails.colorOverLifetime.colorMin, Is.EqualTo(sourceColor.colorMin));
                Assert.That(system.trails.colorOverLifetime.colorMax, Is.EqualTo(sourceColor.colorMax));
                Assert.That(compiled.FieldDiffs.Any(value =>
                    value.Adapter == "ParticleTrails" && value.UnityPath == "trails.lifetime"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LimitVelocityDrag_IsExplicitlyExperimentalWhileOtherMotionChangesAreBlocked()
        {
            VfxSemanticCompileContext context = SemanticFixtureContext();
            context.ComponentProfiles = new[] { context.ComponentProfile };
            context.ComponentProfile.ReadOnlyModules.Add("LimitVelocityOverLifetime");
            context.ComponentProfile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "motion.drag",
                Adapter = "ParticleLimitVelocity",
                Target = "limitVelocityOverLifetime.drag",
                Operation = nameof(VfxSemanticOperation.Add),
                TimeDomain = nameof(VfxSemanticTimeDomain.ParticleNormalized)
            });
            context.Registry.Properties.Add(new VfxSemanticPropertyDefinitionView
            {
                PropertyId = "motion.drag",
                ValueType = nameof(VfxSemanticValueType.Float),
                Operations = { nameof(VfxSemanticOperation.Add), nameof(VfxSemanticOperation.Replace) }
            });
            context.Registry.Properties.Add(new VfxSemanticPropertyDefinitionView
            {
                PropertyId = "motion.speed",
                ValueType = nameof(VfxSemanticValueType.Float),
                Operations = { nameof(VfxSemanticOperation.Multiply), nameof(VfxSemanticOperation.Replace) }
            });
            context.ComponentProfile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "motion.speed",
                Adapter = "ParticleMain",
                Target = "main.startSpeed",
                Operation = nameof(VfxSemanticOperation.Multiply),
                TimeDomain = nameof(VfxSemanticTimeDomain.Static)
            });

            VfxCompositionLayerView dragLayer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            dragLayer.SemanticOverrides.Add(new VfxSemanticOverrideView
            {
                PropertyId = "motion.drag",
                Operation = nameof(VfxSemanticOperation.Add),
                Value = new JValue(0.2f)
            });
            VfxCompiledLayerView drag = VfxSemanticRetimeCompiler.Compile(dragLayer, context);

            Assert.That(drag.Valid, Is.True, string.Join("\n", drag.Diagnostics.Select(value => value.Message)));
            Assert.That(drag.Experimental, Is.True);
            Assert.That(drag.Diagnostics.Any(value => value.Code == "VFXMOD005"), Is.True);

            VfxCompositionLayerView speedLayer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 100);
            speedLayer.SemanticOverrides.Add(new VfxSemanticOverrideView
            {
                PropertyId = "motion.speed",
                Operation = nameof(VfxSemanticOperation.Multiply),
                Value = new JValue(1.2f)
            });
            VfxCompiledLayerView speed = VfxSemanticRetimeCompiler.Compile(speedLayer, context);

            Assert.That(speed.Valid, Is.False);
            Assert.That(speed.Diagnostics.Any(value => value.Code == "VFXMOD004"), Is.True);
        }

        [Test]
        public void SemanticRetimeFixtureCatalog_CoversSevenRepresentativeContractsAndCompilesDeterministically()
        {
            VfxSemanticRetimeFixtureCatalogView catalog = new VfxSemanticRetimeFixtureCatalogService().Load(out string message);
            Assert.That(catalog, Is.Not.Null, message);
            Assert.That(catalog.SchemaVersion, Is.EqualTo("mx.vfx.semantic-retime-fixtures.v1"));
            Assert.That(catalog.Fixtures, Has.Count.EqualTo(7));
            Assert.That(catalog.Fixtures.Select(value => value.FixtureId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(7));
            Assert.That(catalog.Fixtures.SelectMany(value => value.RequiredAdapters), Does.Contain("UberFXSG"));
            Assert.That(catalog.Fixtures.SelectMany(value => value.Invariants), Does.Contain("PreviewBakeEquivalent"));
            VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out message);
            Assert.That(registry, Is.Not.Null, message);
            var propertyIds = new HashSet<string>(registry.Properties.Select(value => value.PropertyId), StringComparer.Ordinal);
            Assert.That(catalog.Fixtures.SelectMany(value => value.RequiredPropertyIds).All(propertyIds.Contains), Is.True);

            foreach (VfxSemanticRetimeFixtureView fixture in catalog.Fixtures)
            {
                Assert.That(Enum.TryParse(fixture.RetimeMode, false, out VfxRetimeMode mode), Is.True, fixture.FixtureId);
                VfxTimeSegmentsView source = fixture.SourceSegments.Clone();
                source.CycleFrames = fixture.CycleFrames;
                var layer = new VfxCompositionLayerView
                {
                    LayerId = fixture.FixtureId,
                    ElementId = "element.fixture",
                    Timing = new VfxLayerTimingView
                    {
                        RetimeMode = mode.ToString(),
                        SourceSegments = source,
                        TargetArtisticFrames = fixture.TargetArtisticFrames,
                        TailPolicy = "EndLocked"
                    }
                };
                if (mode == VfxRetimeMode.EventRemap)
                {
                    layer.Timing.EventMap.Add(new VfxRetimeMapPointView { SourceFrame = 0, TargetFrame = 0 });
                    layer.Timing.EventMap.Add(new VfxRetimeMapPointView { SourceFrame = source.ArtisticFrames / 2, TargetFrame = fixture.TargetArtisticFrames / 2 });
                    layer.Timing.EventMap.Add(new VfxRetimeMapPointView { SourceFrame = source.ArtisticFrames, TargetFrame = fixture.TargetArtisticFrames });
                }
                var profile = new VfxComponentProfileView
                {
                    ProfileId = "profile." + fixture.FixtureId,
                    SourceComponentId = "source." + fixture.FixtureId,
                    Status = nameof(VfxProfileReviewState.Confirmed),
                    Timing = new VfxProfileTimingView
                    {
                        PreRollFrames = source.PreRollFrames,
                        EntryFrames = source.EntryFrames,
                        BodyFrames = source.BodyFrames,
                        ExitFrames = source.ExitFrames,
                        CleanupFrames = source.CleanupFrames,
                        MinimumBodyFrames = source.MinimumBodyFrames,
                        CycleFrames = source.CycleFrames,
                        RetimeMechanism = "EmitterWindow",
                        TailPolicy = "EndLocked"
                    }
                };
                var context = new VfxSemanticCompileContext
                {
                    Registry = new VfxSemanticPropertyRegistryView(),
                    ComponentProfile = profile,
                    ComponentProfiles = new[] { profile },
                    AllowAutoSuggestedProfile = false
                };
                VfxCompiledLayerView first = VfxSemanticRetimeCompiler.Compile(layer, context);
                VfxCompiledLayerView second = VfxSemanticRetimeCompiler.Compile(layer, context);
                Assert.That(first.Valid, Is.True, fixture.FixtureId + ": " + string.Join("; ", first.Diagnostics.Select(value => value.Code + " " + value.Message)));
                Assert.That(first.TargetSegments.ArtisticFrames, Is.EqualTo(fixture.TargetArtisticFrames), fixture.FixtureId);
                Assert.That(first.CompileHash, Is.EqualTo(second.CompileHash), fixture.FixtureId);
            }
        }

        [Test]
        public void ParticleSystemAdapter_ExtendBodyRemapsEmitterCurveAndBurstFramesWithoutScalingEntryExit()
        {
            string token = Guid.NewGuid().ToString("N");
            var source = new VfxSourceComponentView
            {
                ComponentId = "source.component.fixture." + token,
                DisplayName = "Fixture",
                SourceContentHash = "sha256:" + new string('1', 64),
                DurationSeconds = 100f / 60f
            };
            var element = new VfxSourceElementView { ElementId = "vfx.element.fixture." + token, DisplayName = "Fixture" };
            element.MemberComponentIds.Add(source.ComponentId);
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:curation" }
            };
            catalog.Components.Add(source.ComponentId, source);
            catalog.Elements.Add(element);

            GameObject instance = new GameObject("Semantic Particle Fixture");
            try
            {
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
                VfxCompositionLayerView layer = SemanticFixtureLayer(VfxRetimeMode.ExtendBody, 200);
                layer.ElementId = element.ElementId;

                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance,
                    layer,
                    catalog,
                    element,
                    new VfxSemanticPropertyRegistryService().Load(out _),
                    apply: true,
                    allowSuggestedProfiles: true,
                    useExternalLayerScheduler: false,
                    out _);

                Assert.That(compiled.Valid, Is.True, string.Join("\n", compiled.Diagnostics.Select(value => value.Message)));
                Assert.That(system.main.duration, Is.EqualTo(200f / 60f).Within(0.00001f));
                Keyframe[] keys = system.emission.rateOverTime.curve.keys;
                Assert.That(keys[1].time, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(keys[2].time, Is.EqualTo(0.95f).Within(0.0001f));
                var bursts = new ParticleSystem.Burst[system.emission.burstCount];
                system.emission.GetBursts(bursts);
                Assert.That(bursts[0].time, Is.EqualTo(5f / 60f).Within(0.0001f));
                Assert.That(bursts[1].time, Is.EqualTo(195f / 60f).Within(0.0001f));
                Assert.That(compiled.FieldDiffs.Any(value => value.UnityPath == "emission.rateOverTime"), Is.True);
                Assert.That(compiled.FieldDiffs.Count(value => value.UnityPath.Contains("emission.bursts")), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
