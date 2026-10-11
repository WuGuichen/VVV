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
        private static void ApplyFlipbookCycles(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled)
        {
            ParticleSystem.TextureSheetAnimationModule module = system.textureSheetAnimation;
            int source = module.cycleCount;
            int target = Math.Max(1, semantic.IntValue(source));
            module.enabled = true;
            module.cycleCount = target;
            Diff(compiled, memberId, semantic, "ParticleTextureSheet", "textureSheet.cycleCount", source.ToString(CultureInfo.InvariantCulture), target.ToString(CultureInfo.InvariantCulture), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static VfxSemanticTimeDomain ParseDomain(string propertyId)
        {
            if (propertyId == "timing.startDelay.exact") return VfxSemanticTimeDomain.LayerFrame;
            return VfxSemanticTimeDomain.ParticleNormalized;
        }

        private static void ApplyTint(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxGradient source = main.startColor;
            Color tint = ColorValue(semantic.Value, Color.white);
            tint = Color.Lerp(Color.white, tint, Mathf.Clamp01(weight));
            ParticleSystem.MinMaxGradient target = TransformGradient(source, color => new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a * tint.a));
            main.startColor = target;
            Diff(compiled, memberId, semantic, "ParticleStartColor", "main.startColor", GradientSummary(source), GradientSummary(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyEmissionDensity(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled, bool includeBursts)
        {
            ApplyRate(system, memberId, semantic, weight, compiled, distance: false);
            if (includeBursts) ApplyBurstScale(system, memberId, semantic, weight, compiled);
        }

        private static void ApplyRate(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled, bool distance)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.MinMaxCurve source = distance ? emission.rateOverDistance : emission.rateOverTime;
            ParticleSystem.MinMaxCurve target;
            if (semantic.Value?.Type == JTokenType.Object)
                target = ApplyCurveOperation(source, semantic, weight, compiled, memberId);
            else
            {
                float factor = Response(semantic.FloatValue(1f), weight, 1f);
                target = ApplyCurveOperation(source, semantic.Operation, factor);
            }
            if (distance) emission.rateOverDistance = target; else emission.rateOverTime = target;
            Diff(compiled, memberId, semantic, "ParticleEmission", distance ? "emission.rateOverDistance" : "emission.rateOverTime", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.EmitterNormalized);
        }

        private static void ApplyBurstScale(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            int count = emission.burstCount;
            if (count <= 0) return;
            var bursts = new ParticleSystem.Burst[count];
            emission.GetBursts(bursts);
            float factor = Response(semantic.FloatValue(1f), weight, 1f);
            for (int i = 0; i < bursts.Length; i++)
            {
                ParticleSystem.Burst burst = bursts[i];
                ParticleSystem.MinMaxCurve source = burst.count;
                burst.count = ApplyCurveOperation(source, semantic.Operation, factor);
                bursts[i] = burst;
                VfxSemanticRetimeCompiler.AddDiff(compiled, memberId, semantic.PropertyId, "ParticleEmission", "emission.bursts[" + i + "].count", CurveSummary(source), semantic.Operation, Format(factor), CurveSummary(burst.count), nameof(VfxSemanticTimeDomain.LayerFrame));
            }
            emission.SetBursts(bursts, bursts.Length);
        }

        private static void ApplyCapacity(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            int source = main.maxParticles;
            int target = Math.Max(1, semantic.IntValue(source));
            main.maxParticles = target;
            Diff(compiled, memberId, semantic, "ParticleMain", "main.maxParticles", source.ToString(CultureInfo.InvariantCulture), target.ToString(CultureInfo.InvariantCulture), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyStartSpeed(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve source = main.startSpeed;
            float value = Response(semantic.FloatValue(1f), weight, 1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic.Operation, value);
            main.startSpeed = target;
            Diff(compiled, memberId, semantic, "ParticleMain", "main.startSpeed", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyTravelRange(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve sourceSpeed = main.startSpeed;
            float lifetime = Math.Max(0.0001f, main.startLifetime.constantMax);
            float sourceRange = Math.Max(0.0001f, sourceSpeed.constantMax * lifetime);
            float requested = Math.Max(0f, semantic.FloatValue(sourceRange));
            float factor = semantic.Operation == nameof(VfxSemanticOperation.Multiply)
                ? Response(requested, weight, 1f)
                : Mathf.Lerp(1f, requested / sourceRange, Mathf.Clamp01(weight));
            ParticleSystem.MinMaxCurve targetSpeed = Multiply(sourceSpeed, factor);
            main.startSpeed = targetSpeed;
            Diff(compiled, memberId, semantic, "ParticleMain", "main.startSpeed (travelRange)", Format(sourceRange) + " m · " + CurveSummary(sourceSpeed), Format(targetSpeed.constantMax * lifetime) + " m · " + CurveSummary(targetSpeed), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyGravity(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve source = main.gravityModifier;
            float value = Response(semantic.FloatValue(1f), weight, semantic.Operation == nameof(VfxSemanticOperation.Add) ? 0f : 1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic.Operation, value);
            main.gravityModifier = target;
            Diff(compiled, memberId, semantic, "ParticleMain", "main.gravityModifier", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyForce(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled, ParticleSystemSimulationSpace space)
        {
            ParticleSystem.ForceOverLifetimeModule module = system.forceOverLifetime;
            Vector3 value = VectorValue(semantic.Value, Vector3.zero) * Mathf.Clamp01(weight);
            if (module.enabled && module.space != space)
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXMOT001", "同一 ParticleSystem 的 Force over Lifetime 不能同时表达局部与世界空间受力。", memberId, semantic.PropertyId, "UseSeparateMemberOrSingleForceSpace");
                return;
            }
            ParticleSystem.MinMaxCurve sourceX = module.enabled ? module.x : new ParticleSystem.MinMaxCurve(0f);
            ParticleSystem.MinMaxCurve sourceY = module.enabled ? module.y : new ParticleSystem.MinMaxCurve(0f);
            ParticleSystem.MinMaxCurve sourceZ = module.enabled ? module.z : new ParticleSystem.MinMaxCurve(0f);
            module.enabled = true;
            module.space = space;
            module.x = ApplyCurveOperation(sourceX, semantic.Operation, value.x);
            module.y = ApplyCurveOperation(sourceY, semantic.Operation, value.y);
            module.z = ApplyCurveOperation(sourceZ, semantic.Operation, value.z);
            Diff(compiled, memberId, semantic, "ParticleForce", "forceOverLifetime.xyz." + space, CurveSummary(sourceX) + "," + CurveSummary(sourceY) + "," + CurveSummary(sourceZ), value.ToString("F4") + " → " + CurveSummary(module.x) + "," + CurveSummary(module.y) + "," + CurveSummary(module.z), VfxSemanticTimeDomain.EmitterNormalized);
        }

        private static void ApplyDrag(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule module = system.limitVelocityOverLifetime;
            ParticleSystem.MinMaxCurve source = module.enabled ? module.drag : new ParticleSystem.MinMaxCurve(0f);
            float neutral = semantic.Operation == nameof(VfxSemanticOperation.Add) ? 0f : 1f;
            float value = Response(semantic.FloatValue(neutral), weight, neutral);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic.Operation, value);
            module.enabled = true;
            module.drag = target;
            Diff(compiled, memberId, semantic, "ParticleLimitVelocity", "limitVelocityOverLifetime.drag", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyRotationSpeed(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.RotationOverLifetimeModule module = system.rotationOverLifetime;
            Vector3 degrees = VectorValue(semantic.Value, Vector3.zero) * Mathf.Clamp01(weight);
            Vector3 radians = degrees * Mathf.Deg2Rad;
            ParticleSystem.MinMaxCurve sourceX = module.enabled ? module.x : new ParticleSystem.MinMaxCurve(0f);
            ParticleSystem.MinMaxCurve sourceY = module.enabled ? module.y : new ParticleSystem.MinMaxCurve(0f);
            ParticleSystem.MinMaxCurve sourceZ = module.enabled ? module.z : new ParticleSystem.MinMaxCurve(0f);
            module.enabled = true;
            module.separateAxes = true;
            module.x = ApplyCurveOperation(sourceX, semantic.Operation, radians.x);
            module.y = ApplyCurveOperation(sourceY, semantic.Operation, radians.y);
            module.z = ApplyCurveOperation(sourceZ, semantic.Operation, radians.z);
            Diff(compiled, memberId, semantic, "ParticleRotationOverLifetime", "rotationOverLifetime.xyz", "rad " + CurveSummary(sourceX) + "," + CurveSummary(sourceY) + "," + CurveSummary(sourceZ), degrees.ToString("F3") + " deg/s → rad " + CurveSummary(module.x) + "," + CurveSummary(module.y) + "," + CurveSummary(module.z), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyNoiseStrength(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            if (!noise.enabled) return;
            ParticleSystem.MinMaxCurve source = noise.strength;
            float value = Response(semantic.FloatValue(1f), weight, 1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic.Operation, value);
            noise.strength = target;
            Diff(compiled, memberId, semantic, "ParticleNoise", "noise.strength", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyNoiseEnvelope(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            if (!noise.enabled) return;
            ParticleSystem.MinMaxCurve source = noise.strength;
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic, weight, compiled, memberId);
            noise.strength = target;
            Diff(compiled, memberId, semantic, "ParticleNoise", "noise.strength", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplySizeEnvelope(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.SizeOverLifetimeModule module = system.sizeOverLifetime;
            ParticleSystem.MinMaxCurve source = module.enabled ? module.size : new ParticleSystem.MinMaxCurve(1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic, weight, compiled, memberId);
            module.enabled = true;
            module.separateAxes = false;
            module.size = target;
            Diff(compiled, memberId, semantic, "ParticleSizeOverLifetime", "sizeOverLifetime.size", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyVelocityEnvelope(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.VelocityOverLifetimeModule module = system.velocityOverLifetime;
            ParticleSystem.MinMaxCurve source = module.enabled ? module.speedModifier : new ParticleSystem.MinMaxCurve(1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic, weight, compiled, memberId);
            module.enabled = true;
            module.speedModifier = target;
            Diff(compiled, memberId, semantic, "ParticleVelocityOverLifetime", "velocityOverLifetime.speedModifier", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyNoiseFrequency(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            if (!noise.enabled) return;
            float source = noise.frequency;
            float value = Response(semantic.FloatValue(1f), weight, 1f);
            noise.frequency = Mathf.Max(0f, Apply(source, semantic.Operation, value));
            Diff(compiled, memberId, semantic, "ParticleNoise", "noise.frequency", Format(source), Format(noise.frequency), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyCompositionScale(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            Vector3 source = system.transform.localScale;
            float factor = Response(semantic.FloatValue(1f), weight, 1f);
            Vector3 target = semantic.Operation == nameof(VfxSemanticOperation.Replace) ? Vector3.one * factor : source * factor;
            system.transform.localScale = target;
            Diff(compiled, memberId, semantic, "Transform", "transform.localScale", source.ToString("F4"), target.ToString("F4"), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyParticleSize(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxCurve source = main.startSize;
            float value = Response(semantic.FloatValue(1f), weight, 1f);
            ParticleSystem.MinMaxCurve target = ApplyCurveOperation(source, semantic.Operation, value);
            main.startSize = target;
            Diff(compiled, memberId, semantic, "ParticleMain", "main.startSize", CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyEmitterScale(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            if (!shape.enabled) return;
            Vector3 source = shape.scale;
            float factor = Response(semantic.FloatValue(1f), weight, 1f);
            Vector3 target = semantic.Operation == nameof(VfxSemanticOperation.Replace) ? Vector3.one * factor : source * factor;
            shape.scale = target;
            Diff(compiled, memberId, semantic, "ParticleShape", "shape.scale", source.ToString("F4"), target.ToString("F4"), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyShapeFloat(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled, string field)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            if (!shape.enabled) return;
            float value = Response(semantic.FloatValue(field == "radius" ? 1f : 0f), weight, field == "radius" ? 1f : 0f);
            float source;
            float target;
            switch (field)
            {
                case "radius": source = shape.radius; target = Mathf.Max(0f, Apply(source, semantic.Operation, value)); shape.radius = target; break;
                case "angle": source = shape.angle; target = Mathf.Clamp(Apply(source, semantic.Operation, value), 0f, 90f); shape.angle = target; break;
                default: source = shape.arc; target = Mathf.Clamp(Apply(source, semantic.Operation, value), 0f, 360f); shape.arc = target; break;
            }
            Diff(compiled, memberId, semantic, "ParticleShape", "shape." + field, Format(source), Format(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyDirectionSpread(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            if (!shape.enabled) return;
            float source = shape.randomDirectionAmount;
            float neutral = semantic.Operation == nameof(VfxSemanticOperation.Add) ? 0f : 1f;
            float value = Response(semantic.FloatValue(neutral), weight, neutral);
            float target = Mathf.Clamp01(Apply(source, semantic.Operation, value));
            shape.randomDirectionAmount = target;
            Diff(compiled, memberId, semantic, "ParticleShape", "shape.randomDirectionAmount", Format(source), Format(target), VfxSemanticTimeDomain.Static);
        }

        private static void ApplySorting(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            int source = renderer.sortingOrder;
            int value = semantic.IntValue(0);
            int target = semantic.Operation == nameof(VfxSemanticOperation.Replace) ? value : source + value;
            renderer.sortingOrder = target;
            Diff(compiled, memberId, semantic, "ParticleRenderer", "renderer.sortingOrder", source.ToString(CultureInfo.InvariantCulture), target.ToString(CultureInfo.InvariantCulture), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyRendererEnum(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled, string field)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            int requested = semantic.IntValue(0);
            string source;
            string target;
            switch (field)
            {
                case "renderMode":
                    source = renderer.renderMode.ToString();
                    if (!Enum.IsDefined(typeof(ParticleSystemRenderMode), requested)) return;
                    renderer.renderMode = (ParticleSystemRenderMode)requested;
                    target = renderer.renderMode.ToString();
                    break;
                case "alignment":
                    source = renderer.alignment.ToString();
                    if (!Enum.IsDefined(typeof(ParticleSystemRenderSpace), requested)) return;
                    renderer.alignment = (ParticleSystemRenderSpace)requested;
                    target = renderer.alignment.ToString();
                    break;
                default:
                    source = renderer.sortMode.ToString();
                    if (!Enum.IsDefined(typeof(ParticleSystemSortMode), requested)) return;
                    renderer.sortMode = (ParticleSystemSortMode)requested;
                    target = renderer.sortMode.ToString();
                    break;
            }
            Diff(compiled, memberId, semantic, "ParticleRenderer", "renderer." + field, source, target, VfxSemanticTimeDomain.Static);
        }

        private static void ApplyRendererPivot(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            Vector3 source = renderer.pivot;
            Vector3 target = VectorValue(semantic.Value, source);
            renderer.pivot = target;
            Diff(compiled, memberId, semantic, "ParticleRenderer", "renderer.pivot", source.ToString("F4"), target.ToString("F4"), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyRendererSizeLimit(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled, bool minimum)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            float source = minimum ? renderer.minParticleSize : renderer.maxParticleSize;
            float target = Mathf.Clamp01(semantic.FloatValue(source));
            if (minimum) renderer.minParticleSize = Math.Min(target, renderer.maxParticleSize);
            else renderer.maxParticleSize = Math.Max(target, renderer.minParticleSize);
            float final = minimum ? renderer.minParticleSize : renderer.maxParticleSize;
            Diff(compiled, memberId, semantic, "ParticleRenderer", minimum ? "renderer.minParticleSize" : "renderer.maxParticleSize", Format(source), Format(final), VfxSemanticTimeDomain.Static);
        }

        private static void ApplyRendererAsset(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxCompiledLayerView compiled, bool material)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || semantic.Value?.Type != JTokenType.String) return;
            string guid = semantic.Value.Value<string>();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXREN001", "Renderer 资产 GUID 不存在：" + guid, memberId, semantic.PropertyId, "SelectValidProjectAsset");
                return;
            }
            if (material)
            {
                Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (target == null)
                {
                    VfxSemanticRetimeCompiler.Error(compiled, "VFXREN002", "选择的资产不是 Material：" + path, memberId, semantic.PropertyId, "SelectMaterial");
                    return;
                }
                Material source = renderer.sharedMaterial;
                renderer.sharedMaterial = target;
                Diff(compiled, memberId, semantic, "ParticleRendererAsset", "renderer.sharedMaterial", AssetGuid(source), guid, VfxSemanticTimeDomain.Static);
            }
            else
            {
                Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (target == null)
                {
                    VfxSemanticRetimeCompiler.Error(compiled, "VFXREN003", "选择的资产不是 Mesh：" + path, memberId, semantic.PropertyId, "SelectMesh");
                    return;
                }
                Mesh source = renderer.mesh;
                renderer.mesh = target;
                Diff(compiled, memberId, semantic, "ParticleRendererAsset", "renderer.mesh", AssetGuid(source), guid, VfxSemanticTimeDomain.Static);
            }
        }

        internal static void ApplyCustomDataExact(
            ParticleSystem system,
            string memberId,
            VfxSemanticOverrideView semantic,
            VfxSemanticBindingView binding,
            VfxCompiledLayerView compiled)
        {
            string[] parts = (binding.Target ?? string.Empty).Split('.');
            if (parts.Length != 2 || parts[1].Length != 1)
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXCUS001", "Custom Data Profile target 必须为 Custom1.X 形式。", memberId, semantic.PropertyId, "EditShaderProfile");
                return;
            }
            ParticleSystemCustomData stream = string.Equals(parts[0], "Custom2", StringComparison.Ordinal)
                ? ParticleSystemCustomData.Custom2
                : ParticleSystemCustomData.Custom1;
            int channel = "XYZW".IndexOf(char.ToUpperInvariant(parts[1][0]));
            if (channel < 0)
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXCUS002", "Custom Data channel 只能是 X/Y/Z/W。", memberId, semantic.PropertyId, "EditShaderProfile");
                return;
            }
            ParticleSystem.CustomDataModule module = system.customData;
            if (!module.enabled || module.GetMode(stream) != ParticleSystemCustomDataMode.Vector)
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXCUS003", binding.Target + " 不是已确认的 Vector Custom Data。", memberId, semantic.PropertyId, "ConfirmCustomDataProfile");
                return;
            }
            ParticleSystem.MinMaxCurve source = module.GetVector(stream, channel);
            ParticleSystem.MinMaxCurve target = VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
            module.SetVector(stream, channel, target);
            Diff(compiled, memberId, semantic, "ParticleCustomDataExact", "customData." + binding.Target, CurveSummary(source), CurveSummary(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static string AssetGuid(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }

        private static void ApplyFlipbook(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, float weight, VfxCompiledLayerView compiled)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            if (!sheet.enabled) return;
            int source = sheet.cycleCount;
            float value = Response(semantic.FloatValue(1f), weight, 1f);
            int target = Mathf.Max(1, Mathf.RoundToInt(Apply(source, semantic.Operation, value)));
            sheet.cycleCount = target;
            Diff(compiled, memberId, semantic, "ParticleTextureSheet", "textureSheet.cycleCount", Format(source), Format(target), VfxSemanticTimeDomain.ParticleNormalized);
        }

        private static void ApplyMaterialProperty(ParticleSystem system, string memberId, VfxSemanticOverrideView semantic, VfxSemanticBindingView binding, VfxCompiledLayerView compiled, float sourceBodySeconds, float targetBodySeconds)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || string.IsNullOrWhiteSpace(binding.Target)) return;
            Material material = renderer.sharedMaterial;
            if (material == null) return;
            int id = Shader.PropertyToID(binding.Target);
            if (!material.HasProperty(id))
            {
                VfxSemanticRetimeCompiler.Warning(compiled, "VFXMAT001", "Shader 不包含 Profile 指定属性：" + binding.Target, memberId, semantic.PropertyId, "EditShaderProfile");
                return;
            }
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            bool colorTarget = string.Equals(binding.ValueType, "Color", StringComparison.Ordinal);
            if (semantic.Value.Type == JTokenType.Object && !colorTarget)
            {
                float source = material.GetFloat(id);
                AnimationCurve curve = VfxCurveAuthoringJson.Restore(semantic.Value);
                if (curve == null) return;
                float value = curve.Evaluate(0f);
                float target = semantic.Operation == nameof(VfxSemanticOperation.Replace)
                    ? value
                    : Apply(source, semantic.Operation, value);
                block.SetFloat(id, target);
                renderer.SetPropertyBlock(block);
                Diff(compiled, memberId, semantic, "MaterialPropertyBlock", binding.Target, Format(source), "curve[" + curve.length + "] @0=" + Format(target), Enum.TryParse(binding.TimeDomain, out VfxSemanticTimeDomain curveDomain) ? curveDomain : VfxSemanticTimeDomain.LayerFrame);
            }
            else if (semantic.Value.Type == JTokenType.Array)
            {
                Color source = material.GetColor(id);
                Color value = ColorValue(semantic.Value, Color.white);
                Color target = semantic.Operation == nameof(VfxSemanticOperation.Replace) ? value : new Color(source.r * value.r, source.g * value.g, source.b * value.b, source.a * value.a);
                block.SetColor(id, target);
                renderer.SetPropertyBlock(block);
                Diff(compiled, memberId, semantic, "MaterialPropertyBlock", binding.Target, source.ToString(), target.ToString(), Enum.TryParse(binding.TimeDomain, out VfxSemanticTimeDomain domain) ? domain : VfxSemanticTimeDomain.Static);
            }
            else if (colorTarget)
            {
                Color source = material.GetColor(id);
                float value = semantic.FloatValue(semantic.Operation == nameof(VfxSemanticOperation.Add) ? 0f : 1f);
                if (semantic.PropertyId == "appearance.emission.exposureEv") value = Mathf.Pow(2f, value);
                Color target = semantic.Operation == nameof(VfxSemanticOperation.Replace)
                    ? new Color(source.r * value, source.g * value, source.b * value, source.a)
                    : new Color(source.r * value, source.g * value, source.b * value, source.a);
                block.SetColor(id, target);
                renderer.SetPropertyBlock(block);
                Diff(compiled, memberId, semantic, "MaterialPropertyBlock", binding.Target, source.ToString(), target.ToString(), Enum.TryParse(binding.TimeDomain, out VfxSemanticTimeDomain colorDomain) ? colorDomain : VfxSemanticTimeDomain.Static);
            }
            else
            {
                float source = material.GetFloat(id);
                float value = semantic.FloatValue(semantic.Operation == nameof(VfxSemanticOperation.Add) ? 0f : 1f);
                if (semantic.PropertyId == "appearance.emission.exposureEv") value = Mathf.Pow(2f, value);
                if (semantic.PropertyId == "timing.materialLocalTime.scale" && targetBodySeconds > 0f) value *= sourceBodySeconds / targetBodySeconds;
                float target = Apply(source, semantic.Operation, value);
                block.SetFloat(id, target);
                renderer.SetPropertyBlock(block);
                Diff(compiled, memberId, semantic, "MaterialPropertyBlock", binding.Target, Format(source), Format(target), Enum.TryParse(binding.TimeDomain, out VfxSemanticTimeDomain domain) ? domain : VfxSemanticTimeDomain.Static);
            }
        }
    }
}

