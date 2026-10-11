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
    internal static partial class VfxSemanticParticleSystemAdapter
    {
        private static void ApplyOverrides(ParticleSystem system, VfxResolvedComponentAuthoringProfile member, VfxElementProfileView elementProfile, VfxCompositionLayerView layer, VfxCompiledLayerView compiled, float sourceBodySeconds, float targetBodySeconds)
        {
            List<VfxSemanticOverrideView> all = EffectiveOverridesForMember(layer, member.MemberId).ToList();
            ApplyEmissionWindow(system, member.MemberId, layer, elementProfile, all, compiled);
            foreach (VfxSemanticOverrideView semantic in all)
            {
                if (!Participates(layer, elementProfile, member.MemberId, semantic.PropertyId)) continue;
                if (semantic.PropertyId == "emission.window.startFrame" || semantic.PropertyId == "emission.window.endFrame") continue;
                List<VfxSemanticBindingView> propertyBindings = BindingsForProperty(member, semantic.PropertyId).Where(value => !value.ReadOnly).ToList();
                VfxSemanticBindingView componentBinding = propertyBindings.FirstOrDefault(value => value.Adapter != "MaterialPropertyBlock" && value.Adapter != "DerivedMaterial");
                List<VfxSemanticBindingView> materialBindings = propertyBindings
                    .Where(value => value.Adapter == "MaterialPropertyBlock" || value.Adapter == "DerivedMaterial")
                    .ToList();
                if (componentBinding == null && materialBindings.Count == 0) continue;
                float weight = componentBinding == null || Math.Abs(componentBinding.ResponseWeight) < 0.00001f ? 1f : componentBinding.ResponseWeight;
                if (componentBinding != null)
                {
                    if (componentBinding.Adapter == "ParticleCustomDataExact")
                    {
                        ApplyCustomDataExact(system, member.MemberId, semantic, componentBinding, compiled);
                    }
                    else
                    switch (semantic.PropertyId)
                    {
                        case "appearance.opacity": ApplyOpacity(system, member.MemberId, semantic, weight, compiled); break;
                        case "appearance.opacityEnvelope": ApplyOpacityEnvelope(system, member.MemberId, semantic, weight, compiled); break;
                        case "appearance.colorOverLifetime.exact": ApplyExactGradient(system, member.MemberId, semantic, compiled); break;
                        case "appearance.palette.primary": ApplyTint(system, member.MemberId, semantic, weight, compiled); break;
                        case "timing.startDelay.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startDelay", compiled); break;
                        case "timing.startLifetime.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startLifetime", compiled); break;
                        case "emission.density": ApplyEmissionDensity(system, member.MemberId, semantic, weight, compiled, includeBursts: true); break;
                        case "emission.rate.time": ApplyRate(system, member.MemberId, semantic, weight, compiled, distance: false); break;
                        case "emission.rate.distance": ApplyRate(system, member.MemberId, semantic, weight, compiled, distance: true); break;
                        case "emission.burst.scale": ApplyBurstScale(system, member.MemberId, semantic, weight, compiled); break;
                        case "emission.capacity.maxParticles": ApplyCapacity(system, member.MemberId, semantic, compiled); break;
                        case "motion.speed": ApplyStartSpeed(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.startSpeed.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startSpeed", compiled); break;
                        case "motion.startRotation.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startRotation", compiled); break;
                        case "motion.startRotation.x.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startRotationX", compiled); break;
                        case "motion.startRotation.y.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startRotationY", compiled); break;
                        case "motion.startRotation.z.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startRotationZ", compiled); break;
                        case "motion.travelRange": ApplyTravelRange(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.gravity.world": ApplyGravity(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.gravity.local": ApplyForce(system, member.MemberId, semantic, weight, compiled, ParticleSystemSimulationSpace.Local); break;
                        case "motion.force.vector": ApplyForce(system, member.MemberId, semantic, weight, compiled, ParticleSystemSimulationSpace.World); break;
                        case "motion.force.x.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "forceOverLifetime.x", compiled); break;
                        case "motion.force.y.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "forceOverLifetime.y", compiled); break;
                        case "motion.force.z.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "forceOverLifetime.z", compiled); break;
                        case "motion.drag": ApplyDrag(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.rotation.speed": ApplyRotationSpeed(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.rotation.x.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "rotationOverLifetime.x", compiled); break;
                        case "motion.rotation.y.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "rotationOverLifetime.y", compiled); break;
                        case "motion.rotation.z.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "rotationOverLifetime.z", compiled); break;
                        case "motion.noise.strength": ApplyNoiseStrength(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.noise.envelope": ApplyNoiseEnvelope(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.noise.frequency": ApplyNoiseFrequency(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.noise.scroll.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "noise.scrollSpeed", compiled); break;
                        case "shape.composition.scale": ApplyCompositionScale(system, member.MemberId, semantic, weight, compiled); break;
                        case "shape.particle.size": ApplyParticleSize(system, member.MemberId, semantic, weight, compiled); break;
                        case "shape.startSize.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startSize", compiled); break;
                        case "shape.startSize.x.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startSizeX", compiled); break;
                        case "shape.startSize.y.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startSizeY", compiled); break;
                        case "shape.startSize.z.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "main.startSizeZ", compiled); break;
                        case "shape.particle.sizeEnvelope": ApplySizeEnvelope(system, member.MemberId, semantic, weight, compiled); break;
                        case "motion.velocity.envelope": ApplyVelocityEnvelope(system, member.MemberId, semantic, weight, compiled); break;
                        case "shape.emitter.scale": ApplyEmitterScale(system, member.MemberId, semantic, weight, compiled); break;
                        case "shape.radius": ApplyShapeFloat(system, member.MemberId, semantic, weight, compiled, "radius"); break;
                        case "shape.angle": ApplyShapeFloat(system, member.MemberId, semantic, weight, compiled, "angle"); break;
                        case "shape.arc": ApplyShapeFloat(system, member.MemberId, semantic, weight, compiled, "arc"); break;
                        case "shape.direction.spread": ApplyDirectionSpread(system, member.MemberId, semantic, weight, compiled); break;
                        case "render.renderer.mode": ApplyRendererEnum(system, member.MemberId, semantic, compiled, "renderMode"); break;
                        case "render.renderer.alignment": ApplyRendererEnum(system, member.MemberId, semantic, compiled, "alignment"); break;
                        case "render.renderer.sortMode": ApplyRendererEnum(system, member.MemberId, semantic, compiled, "sortMode"); break;
                        case "render.renderer.pivot": ApplyRendererPivot(system, member.MemberId, semantic, compiled); break;
                        case "render.renderer.minParticleSize": ApplyRendererSizeLimit(system, member.MemberId, semantic, compiled, minimum: true); break;
                        case "render.renderer.maxParticleSize": ApplyRendererSizeLimit(system, member.MemberId, semantic, compiled, minimum: false); break;
                        case "render.renderer.material": ApplyRendererAsset(system, member.MemberId, semantic, compiled, material: true); break;
                        case "render.renderer.mesh": ApplyRendererAsset(system, member.MemberId, semantic, compiled, material: false); break;
                        case "render.sortingOrder.offset": ApplySorting(system, member.MemberId, semantic, compiled); break;
                        case "render.flipbook.speed": ApplyFlipbook(system, member.MemberId, semantic, weight, compiled); break;
                        case "render.flipbook.frame.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "textureSheet.frameOverTime", compiled); break;
                        case "render.flipbook.startFrame.exact": ApplyExactMinMaxCurve(system, member.MemberId, semantic, "textureSheet.startFrame", compiled); break;
                        case "render.flipbook.cycles": ApplyFlipbookCycles(system, member.MemberId, semantic, compiled); break;
                    }
                }
                for (int i = 0; i < materialBindings.Count; i++)
                {
                    VfxSemanticBindingView materialBinding = materialBindings[i];
                    if (materialBinding.Adapter == "MaterialPropertyBlock")
                        ApplyMaterialProperty(system, member.MemberId, semantic, materialBinding, compiled, sourceBodySeconds, targetBodySeconds);
                    else if (materialBinding.Adapter == "DerivedMaterial")
                        VfxSemanticRetimeCompiler.Warning(compiled, "VFXMAT002", semantic.PropertyId + " 需要派生 Material；预览保持源材质，烘焙前必须生成受追踪派生资产。", member.MemberId, semantic.PropertyId, "CreateDerivedMaterial");
                }
            }
        }

        internal static IEnumerable<VfxSemanticBindingView> BindingsForProperty(VfxResolvedComponentAuthoringProfile member, string propertyId)
        {
            if (member?.Profile?.Bindings != null)
            {
                for (int i = 0; i < member.Profile.Bindings.Count; i++)
                    if (member.Profile.Bindings[i].PropertyId == propertyId) yield return member.Profile.Bindings[i];
            }
            if (member?.ShaderProfile?.Bindings == null) yield break;
            for (int i = 0; i < member.ShaderProfile.Bindings.Count; i++)
            {
                VfxShaderPropertyBindingView source = member.ShaderProfile.Bindings[i];
                if (source.SemanticPropertyId != propertyId) continue;
                yield return new VfxSemanticBindingView
                {
                    PropertyId = source.SemanticPropertyId,
                    Adapter = source.DynamicPropertyBlock ? "MaterialPropertyBlock" : source.RequiresDerivedMaterial ? "DerivedMaterial" : "MaterialReadOnly",
                    Target = source.ShaderPropertyName,
                    Operation = source.Operation,
                    TimeDomain = source.TimeDomain,
                    ValueType = source.PropertyType,
                    ShaderProfileId = member.ShaderProfile.ProfileId,
                    Keyword = source.Keyword,
                    KeywordRequiredValue = source.KeywordRequiredValue,
                    ReadOnly = !source.DynamicPropertyBlock && !source.RequiresDerivedMaterial
                };
            }
            for (int i = 0; i < member.ShaderProfile.CustomDataBindings.Count; i++)
            {
                VfxShaderCustomDataBindingView source = member.ShaderProfile.CustomDataBindings[i];
                if (!source.Editable || source.SemanticPropertyId != propertyId) continue;
                yield return new VfxSemanticBindingView
                {
                    PropertyId = source.SemanticPropertyId,
                    Adapter = "ParticleCustomDataExact",
                    Target = source.Stream + "." + source.Channels,
                    Operation = nameof(VfxSemanticOperation.Replace),
                    TimeDomain = source.TimeDomain,
                    ValueType = "Curve",
                    ShaderProfileId = member.ShaderProfile.ProfileId
                };
            }
        }

        private static bool Participates(VfxCompositionLayerView layer, VfxElementProfileView elementProfile, string memberId, string propertyId)
        {
            VfxMemberParticipationView draft = layer.MemberParticipation?.FirstOrDefault(value => value.MemberId == memberId);
            string state = null;
            if (draft?.PropertyStates != null) draft.PropertyStates.TryGetValue(propertyId, out state);
            if (string.IsNullOrWhiteSpace(state))
            {
                VfxElementProfileMemberView profile = elementProfile?.Members?.FirstOrDefault(value => value.MemberId == memberId);
                if (profile?.Participation != null) profile.Participation.TryGetValue(propertyId, out state);
            }
            if (string.IsNullOrWhiteSpace(state)) state = draft?.State;
            return state != nameof(VfxMemberParticipationState.Excluded)
                && state != nameof(VfxMemberParticipationState.Unsupported)
                && state != nameof(VfxMemberParticipationState.Unconfirmed);
        }

        private static void ApplyOpacity(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxGradient source = main.startColor;
            float value = Response(semantic.FloatValue(1f), weight, neutral: 1f);
            ParticleSystem.MinMaxGradient target = TransformGradient(source, color => new Color(color.r, color.g, color.b, Apply(color.a, semantic.Operation, value)));
            main.startColor = target;
            Diff(compiled, memberId, semantic, "ParticleStartColor", "main.startColor.alpha", GradientSummary(source), GradientSummary(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyOpacityEnvelope(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            AnimationCurve envelope = VfxCurveAuthoringJson.Restore(semantic.Value);
            if (envelope == null || envelope.length == 0) return;
            ParticleSystem.ColorOverLifetimeModule module = system.colorOverLifetime;
            ParticleSystem.MinMaxGradient source = module.enabled
                ? module.color
                : new ParticleSystem.MinMaxGradient(Color.white);
            ParticleSystem.MinMaxGradient target = TransformOpacityEnvelope(source, envelope, semantic.Operation, weight, compiled, memberId, semantic.PropertyId);
            module.enabled = true;
            module.color = target;
            Diff(compiled, memberId, semantic, "ParticleColorOverLifetime", "colorOverLifetime.color.alpha", GradientSummary(source), GradientSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyExactGradient(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled)
        {
            if (!VfxMinMaxGradientAuthoringJson.IsMinMaxGradient(semantic.Value))
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXGRD001", "精确 Gradient 覆盖缺少完整 MinMaxGradient 数据。", memberId, semantic.PropertyId, "OpenGradientEditor");
                return;
            }
            ParticleSystem.ColorOverLifetimeModule module = system.colorOverLifetime;
            ParticleSystem.MinMaxGradient source = module.enabled ? module.color : new ParticleSystem.MinMaxGradient(Color.white);
            ParticleSystem.MinMaxGradient target = VfxMinMaxGradientAuthoringJson.Restore(semantic.Value, source);
            module.enabled = true;
            module.color = target;
            Diff(compiled, memberId, semantic, "ParticleColorOverLifetimeExact", "colorOverLifetime.color", GradientSummary(source), GradientSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyEmissionWindow(
            ParticleSystem system,
            string memberId,
            VfxCompositionLayerView layer,
            VfxElementProfileView elementProfile,
            IReadOnlyList<VfxSemanticOverrideView> overrides,
            VfxCompiledLayerView compiled)
        {
            VfxSemanticOverrideView start = overrides.LastOrDefault(value => value.PropertyId == "emission.window.startFrame");
            VfxSemanticOverrideView end = overrides.LastOrDefault(value => value.PropertyId == "emission.window.endFrame");
            if (start != null && !Participates(layer, elementProfile, memberId, start.PropertyId)) start = null;
            if (end != null && !Participates(layer, elementProfile, memberId, end.PropertyId)) end = null;
            if (start == null && end == null) return;

            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve sourceDelay = main.startDelay;
            float sourceDuration = main.duration;
            int startFrame = start != null
                ? Math.Max(0, start.IntValue())
                : Math.Max(0, Mathf.RoundToInt(sourceDelay.constantMax * 60f));
            int endFrame = end != null
                ? Math.Max(startFrame + 1, end.IntValue())
                : Math.Max(startFrame + 1, startFrame + Mathf.RoundToInt(sourceDuration * 60f));
            if (start != null)
            {
                main.startDelay = new ParticleSystem.MinMaxCurve(startFrame / 60f);
                Diff(compiled, memberId, start, "ParticleEmissionWindow", "main.startDelay", CurveSummary(sourceDelay), CurveSummary(main.startDelay), VfxSemanticTimeDomain.LayerFrame);
            }
            if (end != null)
            {
                main.duration = Math.Max(1, endFrame - startFrame) / 60f;
                Diff(compiled, memberId, end, "ParticleEmissionWindow", "main.duration", Format(sourceDuration), Format(main.duration), VfxSemanticTimeDomain.LayerFrame);
            }
        }

        private static void ApplyExactMinMaxCurve(
            ParticleSystem system,
            string memberId,
            VfxSemanticOverrideView semantic,
            string targetPath,
            VfxCompiledLayerView compiled)
        {
            if (!VfxMinMaxCurveAuthoringJson.IsMinMaxCurve(semantic.Value))
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXCURVE004", "精确字段覆盖缺少完整 MinMaxCurve 数据。", memberId, semantic.PropertyId, "OpenExactMinMaxEditor");
                return;
            }

            ParticleSystem.MinMaxCurve source;
            ParticleSystem.MinMaxCurve target;
            switch (targetPath)
            {
                case "main.startDelay":
                {
                    ParticleSystem.MainModule module = system.main;
                    source = module.startDelay;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startDelay = target;
                    break;
                }
                case "main.startLifetime":
                {
                    ParticleSystem.MainModule module = system.main;
                    source = module.startLifetime;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startLifetime = target;
                    break;
                }
                case "main.startSpeed":
                {
                    ParticleSystem.MainModule module = system.main;
                    source = module.startSpeed;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startSpeed = target;
                    break;
                }
                case "main.startSize":
                {
                    ParticleSystem.MainModule module = system.main;
                    if (module.startSize3D)
                    {
                        VfxSemanticRetimeCompiler.Error(compiled, "VFXCURVE006", "该成员使用 3D Start Size；请分别编辑 X/Y/Z，不能用单轴值扁平化。", memberId, semantic.PropertyId, "SelectStartSizeAxis");
                        return;
                    }
                    source = module.startSize;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startSize = target;
                    break;
                }
                case "main.startSizeX":
                case "main.startSizeY":
                case "main.startSizeZ":
                {
                    ParticleSystem.MainModule module = system.main;
                    source = targetPath.EndsWith("X", StringComparison.Ordinal) ? module.startSizeX : targetPath.EndsWith("Y", StringComparison.Ordinal) ? module.startSizeY : module.startSizeZ;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startSize3D = true;
                    if (targetPath.EndsWith("X", StringComparison.Ordinal)) module.startSizeX = target;
                    else if (targetPath.EndsWith("Y", StringComparison.Ordinal)) module.startSizeY = target;
                    else module.startSizeZ = target;
                    break;
                }
                case "main.startRotation":
                {
                    ParticleSystem.MainModule module = system.main;
                    if (module.startRotation3D)
                    {
                        VfxSemanticRetimeCompiler.Error(compiled, "VFXCURVE007", "该成员使用 3D Start Rotation；请分别编辑 X/Y/Z，不能用单轴值扁平化。", memberId, semantic.PropertyId, "SelectStartRotationAxis");
                        return;
                    }
                    source = module.startRotation;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startRotation = target;
                    break;
                }
                case "main.startRotationX":
                case "main.startRotationY":
                case "main.startRotationZ":
                {
                    ParticleSystem.MainModule module = system.main;
                    source = targetPath.EndsWith("X", StringComparison.Ordinal) ? module.startRotationX : targetPath.EndsWith("Y", StringComparison.Ordinal) ? module.startRotationY : module.startRotationZ;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.startRotation3D = true;
                    if (targetPath.EndsWith("X", StringComparison.Ordinal)) module.startRotationX = target;
                    else if (targetPath.EndsWith("Y", StringComparison.Ordinal)) module.startRotationY = target;
                    else module.startRotationZ = target;
                    break;
                }
                case "forceOverLifetime.x":
                case "forceOverLifetime.y":
                case "forceOverLifetime.z":
                {
                    ParticleSystem.ForceOverLifetimeModule module = system.forceOverLifetime;
                    source = targetPath.EndsWith(".x", StringComparison.Ordinal) ? module.x : targetPath.EndsWith(".y", StringComparison.Ordinal) ? module.y : module.z;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.enabled = true;
                    if (targetPath.EndsWith(".x", StringComparison.Ordinal)) module.x = target;
                    else if (targetPath.EndsWith(".y", StringComparison.Ordinal)) module.y = target;
                    else module.z = target;
                    break;
                }
                case "rotationOverLifetime.x":
                case "rotationOverLifetime.y":
                case "rotationOverLifetime.z":
                {
                    ParticleSystem.RotationOverLifetimeModule module = system.rotationOverLifetime;
                    source = targetPath.EndsWith(".x", StringComparison.Ordinal) ? module.x : targetPath.EndsWith(".y", StringComparison.Ordinal) ? module.y : module.z;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.enabled = true;
                    if (!targetPath.EndsWith(".z", StringComparison.Ordinal)) module.separateAxes = true;
                    if (targetPath.EndsWith(".x", StringComparison.Ordinal)) module.x = target;
                    else if (targetPath.EndsWith(".y", StringComparison.Ordinal)) module.y = target;
                    else module.z = target;
                    break;
                }
                case "noise.scrollSpeed":
                {
                    ParticleSystem.NoiseModule module = system.noise;
                    source = module.scrollSpeed;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.enabled = true;
                    module.scrollSpeed = target;
                    break;
                }
                case "textureSheet.frameOverTime":
                case "textureSheet.startFrame":
                {
                    ParticleSystem.TextureSheetAnimationModule module = system.textureSheetAnimation;
                    source = targetPath.EndsWith("frameOverTime", StringComparison.Ordinal) ? module.frameOverTime : module.startFrame;
                    target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
                    module.enabled = true;
                    if (targetPath.EndsWith("frameOverTime", StringComparison.Ordinal)) module.frameOverTime = target;
                    else module.startFrame = target;
                    break;
                }
                default:
                    VfxSemanticRetimeCompiler.Error(compiled, "VFXCURVE005", "未知的精确 MinMaxCurve 目标：" + targetPath, memberId, semantic.PropertyId, "EditComponentProfile");
                    return;
            }
            Diff(compiled, memberId, semantic, "ParticleExactMinMaxCurve", targetPath, CurveSummary(source), CurveSummary(target), ParseDomain(semantic.PropertyId));
        }
    }
}

