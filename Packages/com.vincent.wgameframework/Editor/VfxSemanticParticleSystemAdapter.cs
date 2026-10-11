using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Applies v3 semantic authoring from the immutable source instance into preview or Candidate output.</summary>
    /// <remarks>Editor-only, AllocByDesign. Preview and bake call this same implementation.</remarks>
    internal static partial class VfxSemanticParticleSystemAdapter
    {
        public static VfxCompiledLayerView CompileAndApply(
            GameObject instance,
            VfxCompositionLayerView layer,
            VfxSourceElementCatalog catalog,
            VfxSourceElementView element,
            VfxSemanticPropertyRegistryView registry,
            bool apply,
            bool allowSuggestedProfiles,
            bool useExternalLayerScheduler,
            out VfxResolvedLayerAuthoringProfile resolved)
        {
            resolved = new VfxSemanticProfileResolver().Resolve(catalog, element, instance, layer, captureMissingSnapshots: true);
            VfxResolvedComponentAuthoringProfile first = resolved.Members.FirstOrDefault();
            VfxComponentProfileView[] compileProfiles = resolved.Members
                .Select(CreateCompileProfile)
                .Where(value => value != null)
                .ToArray();
            var context = new VfxSemanticCompileContext
            {
                Registry = registry,
                ComponentProfile = compileProfiles.FirstOrDefault(),
                ComponentProfiles = compileProfiles,
                ElementProfile = resolved.ElementProfile,
                Events = resolved.Members.SelectMany(value => value.Profile?.Events ?? Enumerable.Empty<VfxTimeEventView>()).GroupBy(value => value.EventId, StringComparer.Ordinal).Select(value => value.First()).ToArray(),
                AllowAutoSuggestedProfile = allowSuggestedProfiles
            };
            VfxCompiledLayerView result = VfxSemanticRetimeCompiler.Compile(layer, context);
            ResolveVisualStart(layer, resolved, result);
            result.Diagnostics.AddRange(resolved.Diagnostics);
            if (apply)
            {
                ApplyTransform(instance, layer);
                for (int i = 0; i < resolved.Members.Count; i++)
                {
                    VfxResolvedComponentAuthoringProfile member = resolved.Members[i];
                    if (!Participates(layer, member.MemberId))
                    {
                        member.ParticleSystem.gameObject.SetActive(false);
                        continue;
                    }
                    ApplyVisualStart(member, result);
                    ApplyMember(member, resolved.ElementProfile, layer, result, useExternalLayerScheduler);
                }
            }
            result.CompileHash = VfxCanonicalJson.Hash(result, "compileHash");
            return result;
        }

        internal static void ConfigureLayerSchedule(
            UnityVfxBakedLayerScheduleEntry schedule,
            GameObject instance,
            VfxCompositionLayerView layer,
            VfxCompiledLayerView compiled)
        {
            bool playbackWindow = string.Equals(
                layer?.Timing?.RetimeMode,
                nameof(VfxRetimeMode.PlaybackWindow),
                StringComparison.Ordinal);
            UnityVfxLayerWindowEndPolicy endPolicy = playbackWindow
                && Enum.TryParse(layer?.Timing?.WindowEndPolicy, false, out UnityVfxLayerWindowEndPolicy parsed)
                    ? parsed
                    : UnityVfxLayerWindowEndPolicy.StopEmitting;
            int dissipateFrames = endPolicy == UnityVfxLayerWindowEndPolicy.DissipateThenClear
                ? Math.Max(0, layer?.Timing?.DissipateFrames ?? 0)
                : 0;
            schedule.Configure(
                instance,
                Enum.Parse<UnityVfxLifecyclePhase>(layer.Phase),
                compiled.StartFrame,
                compiled.ArtisticEndFrame,
                endPolicy,
                dissipateFrames);
        }

        private static void ResolveVisualStart(
            VfxCompositionLayerView layer,
            VfxResolvedLayerAuthoringProfile resolved,
            VfxCompiledLayerView compiled)
        {
            string requestedMode = layer?.VisualStart?.Mode ?? nameof(VfxVisualStartMode.AutoNormalize);
            if (!Enum.TryParse(requestedMode, false, out VfxVisualStartMode mode))
            {
                VfxSemanticRetimeCompiler.Error(
                    compiled,
                    "VFXTIME001",
                    "未知视觉起点模式：" + requestedMode,
                    propertyId: "timing.visualStart.mode",
                    fixAction: "ChooseVisualStartMode");
                mode = VfxVisualStartMode.AutoNormalize;
            }

            float earliestSeconds = float.PositiveInfinity;
            if (resolved?.Members != null)
            {
                for (int i = 0; i < resolved.Members.Count; i++)
                {
                    VfxResolvedComponentAuthoringProfile member = resolved.Members[i];
                    if (member?.ParticleSystem == null || !Participates(layer, member.MemberId)) continue;
                    float memberStart = EarliestEmissionSeconds(member.ParticleSystem);
                    if (!float.IsPositiveInfinity(memberStart))
                        earliestSeconds = Mathf.Min(earliestSeconds, memberStart);
                }
            }

            int sourceFrames = float.IsPositiveInfinity(earliestSeconds)
                ? 0
                : Mathf.Max(0, Mathf.FloorToInt(earliestSeconds * 60f + 0.0001f));
            int resolvedFrames = mode switch
            {
                VfxVisualStartMode.PreserveSource => sourceFrames,
                VfxVisualStartMode.ManualLeadIn => Math.Max(0, layer?.VisualStart?.ManualLeadInFrames ?? 0),
                _ => 0
            };
            compiled.SourceLeadInFrames = sourceFrames;
            compiled.ResolvedLeadInFrames = resolvedFrames;
            compiled.VisualStartMode = mode.ToString();
        }

        private static float EarliestEmissionSeconds(ParticleSystem system)
        {
            if (system == null) return float.PositiveInfinity;
            ParticleSystem.MainModule main = system.main;
            if (main.prewarm) return 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            if (!emission.enabled) return float.PositiveInfinity;

            float emissionOffset = float.PositiveInfinity;
            if (CurveMaximum(emission.rateOverTime) > 0.00001f
                || CurveMaximum(emission.rateOverDistance) > 0.00001f)
                emissionOffset = 0f;
            for (int i = 0; i < emission.burstCount; i++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(i);
                if (CurveMaximum(burst.count) <= 0.00001f) continue;
                emissionOffset = Mathf.Min(emissionOffset, Mathf.Max(0f, burst.time));
            }
            if (float.IsPositiveInfinity(emissionOffset)) return float.PositiveInfinity;
            return Mathf.Max(0f, CurveMinimum(main.startDelay)) + emissionOffset;
        }

        private static void ApplyVisualStart(
            VfxResolvedComponentAuthoringProfile member,
            VfxCompiledLayerView compiled)
        {
            int deltaFrames = compiled.ResolvedLeadInFrames - compiled.SourceLeadInFrames;
            if (deltaFrames == 0 || member?.ParticleSystem == null) return;

            ParticleSystem system = member.ParticleSystem;
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve sourceDelay = main.startDelay;
            float deltaSeconds = deltaFrames / 60f;
            float burstShiftSeconds = 0f;
            if (deltaSeconds >= 0f)
            {
                main.startDelay = Add(sourceDelay, deltaSeconds);
            }
            else
            {
                float requestedReduction = -deltaSeconds;
                float delayReduction = Mathf.Min(requestedReduction, Mathf.Max(0f, CurveMinimum(sourceDelay)));
                if (delayReduction > 0f)
                    main.startDelay = Add(sourceDelay, -delayReduction);
                burstShiftSeconds = Mathf.Max(0f, requestedReduction - delayReduction);
            }

            if (!string.Equals(CurveSummary(sourceDelay), CurveSummary(main.startDelay), StringComparison.Ordinal))
            {
                VfxSemanticRetimeCompiler.AddDiff(
                    compiled,
                    member.MemberId,
                    "timing.visualStart",
                    "ParticleMain",
                    "main.startDelay",
                    CurveSummary(sourceDelay),
                    "Shift",
                    compiled.VisualStartMode + ":" + compiled.ResolvedLeadInFrames + "f",
                    CurveSummary(main.startDelay),
                    nameof(VfxSemanticTimeDomain.LayerFrame));
            }

            if (burstShiftSeconds <= 0f) return;
            ParticleSystem.EmissionModule emission = system.emission;
            for (int i = 0; i < emission.burstCount; i++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(i);
                float sourceTime = burst.time;
                burst.time = Mathf.Max(0f, sourceTime - burstShiftSeconds);
                emission.SetBurst(i, burst);
                if (Mathf.Abs(sourceTime - burst.time) <= 0.00001f) continue;
                VfxSemanticRetimeCompiler.AddDiff(
                    compiled,
                    member.MemberId,
                    "timing.visualStart",
                    "ParticleEmission",
                    "emission.bursts[" + i + "].time",
                    Format(sourceTime),
                    "Shift",
                    compiled.VisualStartMode + ":" + compiled.ResolvedLeadInFrames + "f",
                    Format(burst.time),
                    nameof(VfxSemanticTimeDomain.LayerFrame));
            }
        }

        private static float CurveMinimum(ParticleSystem.MinMaxCurve value)
        {
            switch (value.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return value.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Min(value.constantMin, value.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return SampleCurveMinimum(value.curve, value.curveMultiplier);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Min(
                        SampleCurveMinimum(value.curveMin, value.curveMultiplier),
                        SampleCurveMinimum(value.curveMax, value.curveMultiplier));
                default:
                    return 0f;
            }
        }

        private static float CurveMaximum(ParticleSystem.MinMaxCurve value)
        {
            switch (value.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return value.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(value.constantMin, value.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return SampleCurveMaximum(value.curve, value.curveMultiplier);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Max(
                        SampleCurveMaximum(value.curveMin, value.curveMultiplier),
                        SampleCurveMaximum(value.curveMax, value.curveMultiplier));
                default:
                    return 0f;
            }
        }

        private static float SampleCurveMinimum(AnimationCurve curve, float multiplier)
        {
            if (curve == null || curve.length == 0) return 0f;
            float result = float.PositiveInfinity;
            for (int i = 0; i <= 32; i++)
                result = Mathf.Min(result, curve.Evaluate(i / 32f) * multiplier);
            return float.IsPositiveInfinity(result) ? 0f : result;
        }

        private static float SampleCurveMaximum(AnimationCurve curve, float multiplier)
        {
            if (curve == null || curve.length == 0) return 0f;
            float result = float.NegativeInfinity;
            for (int i = 0; i <= 32; i++)
                result = Mathf.Max(result, curve.Evaluate(i / 32f) * multiplier);
            return float.IsNegativeInfinity(result) ? 0f : result;
        }

        private static VfxComponentProfileView CreateCompileProfile(VfxResolvedComponentAuthoringProfile member)
        {
            if (member?.Profile == null) return null;
            VfxComponentProfileView source = member.Profile;
            var result = new VfxComponentProfileView
            {
                SchemaVersion = source.SchemaVersion,
                ProfileId = source.ProfileId,
                SourceComponentId = source.SourceComponentId,
                SourceSnapshotHash = source.SourceSnapshotHash,
                Status = source.Status,
                Timing = source.Timing,
                Events = source.Events,
                ReadOnlyModules = source.ReadOnlyModules,
                Blockers = source.Blockers,
                ContentHash = source.ContentHash,
                Bindings = new List<VfxSemanticBindingView>(source.Bindings)
            };
            if (member.ShaderProfile?.Bindings != null)
            {
                for (int bindingIndex = 0; bindingIndex < member.ShaderProfile.Bindings.Count; bindingIndex++)
                {
                    VfxShaderPropertyBindingView binding = member.ShaderProfile.Bindings[bindingIndex];
                    result.Bindings.Add(new VfxSemanticBindingView
                    {
                        PropertyId = binding.SemanticPropertyId,
                        Adapter = binding.DynamicPropertyBlock ? "MaterialPropertyBlock" : binding.RequiresDerivedMaterial ? "DerivedMaterial" : "MaterialReadOnly",
                        Target = binding.ShaderPropertyName,
                        Operation = binding.Operation,
                        TimeDomain = binding.TimeDomain,
                        ValueType = binding.PropertyType,
                        ShaderProfileId = member.ShaderProfile.ProfileId,
                        Keyword = binding.Keyword,
                        KeywordRequiredValue = binding.KeywordRequiredValue,
                        ReadOnly = !binding.DynamicPropertyBlock && !binding.RequiresDerivedMaterial
                    });
                }
            }
            if (member.ShaderProfile?.CustomDataBindings != null)
            {
                for (int bindingIndex = 0; bindingIndex < member.ShaderProfile.CustomDataBindings.Count; bindingIndex++)
                {
                    VfxShaderCustomDataBindingView binding = member.ShaderProfile.CustomDataBindings[bindingIndex];
                    if (!binding.Editable || string.IsNullOrWhiteSpace(binding.SemanticPropertyId)) continue;
                    result.Bindings.Add(new VfxSemanticBindingView
                    {
                        PropertyId = binding.SemanticPropertyId,
                        Adapter = "ParticleCustomDataExact",
                        Target = binding.Stream + "." + binding.Channels,
                        Operation = nameof(VfxSemanticOperation.Replace),
                        TimeDomain = binding.TimeDomain,
                        ValueType = "Curve",
                        ShaderProfileId = member.ShaderProfile.ProfileId
                    });
                }
            }
            return result;
        }

        /// <summary>Builds serialized MPB tracks for properties that cannot survive Prefab serialization as preview-only blocks.</summary>
        /// <remarks>Editor baking only, AllocByDesign.</remarks>
        public static void AppendBakedPropertyTracks(
            VfxResolvedLayerAuthoringProfile resolved,
            VfxCompositionLayerView layer,
            VfxCompiledLayerView compiled,
            List<UnityVfxBakedPropertyTrack> tracks)
        {
            if (resolved == null || layer == null || compiled == null || tracks == null) return;
            UnityVfxLifecyclePhase phase = Enum.TryParse(layer.Phase, false, out UnityVfxLifecyclePhase parsed)
                ? parsed
                : UnityVfxLifecyclePhase.Loop;
            for (int memberIndex = 0; memberIndex < resolved.Members.Count; memberIndex++)
            {
                VfxResolvedComponentAuthoringProfile member = resolved.Members[memberIndex];
                if (!Participates(layer, member.MemberId)) continue;
                ParticleSystemRenderer renderer = member.ParticleSystem != null
                    ? member.ParticleSystem.GetComponent<ParticleSystemRenderer>()
                    : null;
                if (renderer == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                foreach (VfxSemanticOverrideView semantic in EffectiveOverridesForMember(layer, member.MemberId))
                {
                    if (!Participates(layer, resolved.ElementProfile, member.MemberId, semantic.PropertyId)) continue;
                    foreach (VfxSemanticBindingView binding in BindingsForProperty(member, semantic.PropertyId))
                    {
                        if (binding.ReadOnly || binding.Adapter != "MaterialPropertyBlock" || string.IsNullOrWhiteSpace(binding.Target)) continue;
                        int propertyId = Shader.PropertyToID(binding.Target);
                        var track = new UnityVfxBakedPropertyTrack();
                        bool colorTarget = string.Equals(binding.ValueType, "Color", StringComparison.Ordinal);
                        if (semantic.Value?.Type == JTokenType.Array || colorTarget)
                        {
                            Color value = block.GetColor(propertyId);
                            var gradient = new Gradient();
                            gradient.SetKeys(
                                new[] { new GradientColorKey(value, 0f), new GradientColorKey(value, 1f) },
                                new[] { new GradientAlphaKey(value.a, 0f), new GradientAlphaKey(value.a, 1f) });
                            track.ConfigureColor(renderer, binding.Target, phase, compiled.StartFrame, compiled.ArtisticEndFrame, gradient);
                        }
                        else
                        {
                            AnimationCurve curve;
                            if (semantic.Value?.Type == JTokenType.Object)
                            {
                                curve = VfxCurveAuthoringJson.Restore(semantic.Value) ?? AnimationCurve.Constant(0f, 1f, block.GetFloat(propertyId));
                                if (semantic.Operation != nameof(VfxSemanticOperation.Replace))
                                {
                                    float sourceValue = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(propertyId)
                                        ? renderer.sharedMaterial.GetFloat(propertyId)
                                        : 0f;
                                    curve = ComposeCurve(AnimationCurve.Constant(0f, 1f, sourceValue), curve, semantic.Operation, 1f);
                                }
                                curve = NormalizeTrackCurve(curve, binding.TimeDomain, compiled);
                            }
                            else
                            {
                                float value = block.GetFloat(propertyId);
                                curve = semantic.PropertyId == "timing.materialLocalTime.scale"
                                    ? AnimationCurve.Linear(0f, 0f, 1f, value * Math.Max(1, compiled.TargetSegments.ArtisticFrames) / 60f)
                                    : AnimationCurve.Constant(0f, 1f, value);
                            }
                            track.ConfigureFloat(renderer, binding.Target, phase, compiled.StartFrame, compiled.ArtisticEndFrame, curve);
                        }
                        tracks.Add(track);
                    }
                }
            }
        }

        private static AnimationCurve NormalizeTrackCurve(AnimationCurve source, string timeDomain, VfxCompiledLayerView compiled)
        {
            AnimationCurve result = VfxCurveAuthoringJson.Clone(source) ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            if (!string.Equals(timeDomain, nameof(VfxSemanticTimeDomain.LayerFrame), StringComparison.Ordinal)) return result;
            Keyframe[] keys = result.keys;
            if (keys.Length == 0 || keys[keys.Length - 1].time <= 1.00001f) return result;
            float frames = Math.Max(1, compiled.TargetSegments.ArtisticFrames);
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time /= frames;
                keys[i].inTangent *= frames;
                keys[i].outTangent *= frames;
            }
            result.keys = keys;
            return result;
        }

        internal static IEnumerable<VfxSemanticOverrideView> EffectiveOverridesForMember(VfxCompositionLayerView layer, string memberId)
        {
            IEnumerable<VfxSemanticOverrideView> result = layer.SemanticOverrides ?? Enumerable.Empty<VfxSemanticOverrideView>();
            VfxMemberSemanticOverrideView member = layer.MemberOverrides?.FirstOrDefault(value => value.MemberId == memberId);
            if (member?.Overrides == null || member.Overrides.Count == 0) return result;
            var memberProperties = new HashSet<string>(
                member.Overrides
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.PropertyId))
                    .Select(value => value.PropertyId),
                StringComparer.Ordinal);
            return result
                .Where(value => value == null || !memberProperties.Contains(value.PropertyId))
                .Concat(member.Overrides);
        }

        private static void ApplyTransform(GameObject instance, VfxCompositionLayerView layer)
        {
            instance.transform.localPosition = Vector(layer.Transform?.Position ?? layer.LocalPosition, Vector3.zero);
            instance.transform.localEulerAngles = Vector(layer.Transform?.RotationEuler ?? layer.LocalEulerAngles, Vector3.zero);
            instance.transform.localScale = Vector(layer.Transform?.Scale ?? layer.LocalScale, Vector3.one);
        }

        private static bool Participates(VfxCompositionLayerView layer, string memberId)
        {
            VfxMemberParticipationView participation = layer.MemberParticipation?.FirstOrDefault(value => value.MemberId == memberId);
            return participation == null
                || (participation.State != nameof(VfxMemberParticipationState.Excluded)
                    && participation.State != nameof(VfxMemberParticipationState.Unsupported)
                    && participation.State != nameof(VfxMemberParticipationState.Unconfirmed));
        }

        private static void ApplyMember(VfxResolvedComponentAuthoringProfile member, VfxElementProfileView elementProfile, VfxCompositionLayerView layer, VfxCompiledLayerView compiled, bool useExternalLayerScheduler)
        {
            ParticleSystem system = member.ParticleSystem;
            ParticleSystem.MainModule main = system.main;
            float sourceBodySeconds = Math.Max(1, compiled.SourceSegments.BodyFrames) / 60f;
            float targetBodySeconds = Math.Max(1, compiled.TargetSegments.BodyFrames) / 60f;
            if (Enum.TryParse(layer.Timing?.RetimeMode, false, out VfxRetimeMode layerMode))
            {
                VfxRetimeMode mode = VfxSemanticRetimeCompiler.ResolveMemberRetimeMode(layer, member.MemberId, layerMode);
                switch (mode)
                {
                    case VfxRetimeMode.PlaybackWindow:
                        break;
                    case VfxRetimeMode.ExtendBody:
                    case VfxRetimeMode.RepeatBody:
                    case VfxRetimeMode.EventRemap:
                        if (member.Profile.Timing.RetimeMechanism == "EmitterWindow")
                        {
                            float source = main.duration;
                            RetimeEmitterEnvelope(system, member.MemberId, compiled, mode);
                            float targetEmitterSeconds = Math.Max(0.05f,
                                (compiled.TargetSegments.EntryFrames + compiled.TargetSegments.BodyFrames + compiled.TargetSegments.ExitFrames) / 60f);
                            main.duration = targetEmitterSeconds;
                            VfxSemanticRetimeCompiler.AddDiff(compiled, member.MemberId, "timing.body.frames", "ParticleMain", "main.duration", Format(source), "Replace", compiled.TargetSegments.BodyFrames + "f", Format(main.duration), nameof(VfxSemanticTimeDomain.LayerFrame));
                        }
                        break;
                    case VfxRetimeMode.ExtendTail:
                    {
                        float ratio = compiled.SourceSegments.ExitFrames <= 0 ? 1f : compiled.TargetSegments.ExitFrames / (float)compiled.SourceSegments.ExitFrames;
                        ParticleSystem.MinMaxCurve source = main.startLifetime;
                        ParticleSystem.MinMaxCurve target = Multiply(source, ratio);
                        main.startLifetime = target;
                        VfxSemanticRetimeCompiler.AddDiff(compiled, member.MemberId, "timing.exit.frames", "ParticleMain", "main.startLifetime.curveMultiplier", Format(source.curveMultiplier), "Multiply", Format(ratio), Format(target.curveMultiplier), nameof(VfxSemanticTimeDomain.ParticleNormalized));
                        RetimeTrails(system, member, compiled, ratio);
                        break;
                    }
                    case VfxRetimeMode.WholeSpeed:
                    {
                        float ratio = compiled.TargetSegments.ArtisticFrames <= 0 ? 1f : compiled.SourceSegments.ArtisticFrames / (float)compiled.TargetSegments.ArtisticFrames;
                        float source = main.simulationSpeed;
                        main.simulationSpeed = source * ratio;
                        VfxSemanticRetimeCompiler.AddDiff(compiled, member.MemberId, "timing.materialLocalTime.scale", "ParticleMain", "main.simulationSpeed", Format(source), "Multiply", Format(ratio), Format(main.simulationSpeed), nameof(VfxSemanticTimeDomain.LayerFrame));
                        break;
                    }
                }
            }
            ParticleSystem.MinMaxCurve delay = main.startDelay;
            float startSeconds = Math.Max(0, layer.StartFrame) / 60f;
            if (startSeconds > 0f && !useExternalLayerScheduler)
            {
                ParticleSystem.MinMaxCurve target = Add(delay, startSeconds);
                main.startDelay = target;
                VfxSemanticRetimeCompiler.AddDiff(compiled, member.MemberId, "timing.preRoll.frames", "ParticleMain", "main.startDelay", Format(delay.constantMax), "Add", layer.StartFrame + "f", Format(target.constantMax), nameof(VfxSemanticTimeDomain.LayerFrame));
            }
            else if (startSeconds > 0f)
            {
                VfxSemanticRetimeCompiler.AddDiff(compiled, member.MemberId, "timing.startFrame", "LayerScheduler", "schedule.startFrame", "0", "Replace", layer.StartFrame + "f", layer.StartFrame.ToString(CultureInfo.InvariantCulture), nameof(VfxSemanticTimeDomain.LayerFrame));
            }
            ApplyOverrides(system, member, elementProfile, layer, compiled, sourceBodySeconds, targetBodySeconds);
        }

        internal static void RetimeTrails(
            ParticleSystem system,
            VfxResolvedComponentAuthoringProfile member,
            VfxCompiledLayerView compiled,
            float ratio)
        {
            ParticleSystem.TrailModule trails = system.trails;
            if (!trails.enabled) return;
            VfxTrailAuthoringContractView contract = member.Profile?.Timing?.TrailContract;
            if (contract == null || !string.Equals(contract.RetimePolicy, "PreserveNormalizedLifetime", StringComparison.Ordinal))
                return;

            ParticleSystem.MinMaxCurve source = trails.lifetime;
            VfxSemanticRetimeCompiler.AddDiff(
                compiled,
                member.MemberId,
                "timing.exit.frames",
                "ParticleTrails",
                "trails.lifetime",
                CurveSummary(source),
                "Preserve",
                "normalized lifetime; main lifetime × " + Format(ratio),
                CurveSummary(source),
                nameof(VfxSemanticTimeDomain.ParticleNormalized));
        }

        private static void RetimeEmitterEnvelope(ParticleSystem system, string memberId, VfxCompiledLayerView compiled, VfxRetimeMode mode)
        {
            if (compiled.TimeMap == null || compiled.TimeMap.Count < 2) return;
            int sourceOffset = Math.Max(0, compiled.SourceSegments.PreRollFrames);
            int targetOffset = Math.Max(0, compiled.TargetSegments.PreRollFrames);
            int sourceFrames = Math.Max(1, compiled.SourceSegments.ArtisticFrames - sourceOffset);
            int targetFrames = Math.Max(1, compiled.TargetSegments.ArtisticFrames - targetOffset);
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.MinMaxCurve sourceRate = emission.rateOverTime;
            ParticleSystem.MinMaxCurve targetRate = RemapNormalizedCurve(sourceRate, compiled.TimeMap, sourceFrames, targetFrames, sourceOffset, targetOffset);
            emission.rateOverTime = targetRate;
            if (sourceRate.mode == ParticleSystemCurveMode.Curve || sourceRate.mode == ParticleSystemCurveMode.TwoCurves)
            {
                VfxSemanticRetimeCompiler.AddDiff(
                    compiled,
                    memberId,
                    "timing.emitterEnvelope",
                    "ParticleEmission",
                    "emission.rateOverTime",
                    CurveSummary(sourceRate),
                    "Remap",
                    sourceFrames + "f→" + targetFrames + "f",
                    CurveSummary(targetRate),
                    nameof(VfxSemanticTimeDomain.EmitterNormalized));
            }

            int burstCount = emission.burstCount;
            if (burstCount <= 0) return;
            var sourceBursts = new ParticleSystem.Burst[burstCount];
            emission.GetBursts(sourceBursts);
            var targetBursts = new List<ParticleSystem.Burst>(burstCount);
            int sourceBodyStart = compiled.SourceSegments.PreRollFrames + compiled.SourceSegments.EntryFrames;
            int sourceCycle = compiled.SourceSegments.CycleFrames;
            int targetCycles = mode == VfxRetimeMode.RepeatBody && sourceCycle > 0
                ? Math.Max(1, compiled.TargetSegments.BodyFrames / sourceCycle)
                : 1;
            for (int i = 0; i < sourceBursts.Length; i++)
            {
                ParticleSystem.Burst burst = sourceBursts[i];
                int sourceFrame = Mathf.RoundToInt(burst.time * 60f) + compiled.SourceSegments.PreRollFrames;
                bool repeatable = targetCycles > 1
                    && sourceFrame >= sourceBodyStart
                    && sourceFrame < sourceBodyStart + sourceCycle;
                int copies = repeatable ? targetCycles : 1;
                for (int copy = 0; copy < copies; copy++)
                {
                    ParticleSystem.Burst mapped = burst;
                    float mappedFrame;
                    if (repeatable)
                    {
                        int local = sourceFrame - sourceBodyStart;
                        mappedFrame = compiled.TargetSegments.PreRollFrames + compiled.TargetSegments.EntryFrames + local + copy * sourceCycle;
                    }
                    else
                    {
                        mappedFrame = VfxSemanticRetimeCompiler.MapPiecewise(sourceFrame, compiled.TimeMap, out _);
                    }
                    mapped.time = Math.Max(0f, (mappedFrame - compiled.TargetSegments.PreRollFrames) / 60f);
                    targetBursts.Add(mapped);
                }
                VfxSemanticRetimeCompiler.AddDiff(
                    compiled,
                    memberId,
                    "timing.burst.events",
                    "ParticleEmission",
                    "emission.bursts[" + i + "].time",
                    Format(burst.time),
                    repeatable ? "Repeat" : "Remap",
                    sourceFrame + "f",
                    string.Join(",", targetBursts.Skip(targetBursts.Count - copies).Select(value => Format(value.time))),
                    nameof(VfxSemanticTimeDomain.LayerFrame));
            }
            emission.SetBursts(targetBursts.ToArray(), targetBursts.Count);
        }

        private static ParticleSystem.MinMaxCurve RemapNormalizedCurve(
            ParticleSystem.MinMaxCurve source,
            IReadOnlyList<VfxRetimeMapPointView> map,
            int sourceFrames,
            int targetFrames,
            int sourceOffsetFrames,
            int targetOffsetFrames)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Curve:
                    source.curve = VfxSemanticRetimeCompiler.RemapNormalizedCurve(source.curve, map, sourceFrames, targetFrames, sourceOffsetFrames, targetOffsetFrames);
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    source.curveMin = VfxSemanticRetimeCompiler.RemapNormalizedCurve(source.curveMin, map, sourceFrames, targetFrames, sourceOffsetFrames, targetOffsetFrames);
                    source.curveMax = VfxSemanticRetimeCompiler.RemapNormalizedCurve(source.curveMax, map, sourceFrames, targetFrames, sourceOffsetFrames, targetOffsetFrames);
                    break;
            }
            return source;
        }
    }
}
