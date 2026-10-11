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
        private static ParticleSystem.MinMaxCurve ApplyCurveOperation(ParticleSystem.MinMaxCurve source, string operation, float value)
        {
            if (operation == nameof(VfxSemanticOperation.Replace))
            {
                source.curveMultiplier = value;
                source.constantMin = value;
                source.constantMax = value;
                return source;
            }
            return operation == nameof(VfxSemanticOperation.Add) ? Add(source, value) : Multiply(source, value);
        }

        private static ParticleSystem.MinMaxCurve ApplyCurveOperation(
            ParticleSystem.MinMaxCurve source,
            VfxSemanticOverrideView semantic,
            float weight,
            VfxCompiledLayerView compiled,
            string memberId)
        {
            if (VfxMinMaxCurveAuthoringJson.IsMinMaxCurve(semantic.Value))
            {
                if (semantic.Operation != nameof(VfxSemanticOperation.Replace))
                {
                    VfxSemanticRetimeCompiler.Error(
                        compiled,
                        "VFXCURVE003",
                        "完整 MinMaxCurve 模式只能使用显式 Replace；Multiply/Add/Remap 请使用非破坏曲线层。",
                        memberId,
                        semantic.PropertyId,
                        "UseReplaceForExactMinMaxCurve");
                    return source;
                }
                return VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source);
            }
            AnimationCurve modifier = VfxCurveAuthoringJson.Restore(semantic.Value);
            if (modifier == null) return source;
            if (semantic.Operation == nameof(VfxSemanticOperation.Replace))
                return new ParticleSystem.MinMaxCurve(1f, VfxCurveAuthoringJson.Clone(modifier));

            AnimationCurve sourceMin = CurveForMode(source, minimum: true);
            AnimationCurve sourceMax = CurveForMode(source, minimum: false);
            AnimationCurve targetMin = ComposeCurve(sourceMin, modifier, semantic.Operation, weight);
            AnimationCurve targetMax = ComposeCurve(sourceMax, modifier, semantic.Operation, weight);
            VfxSemanticRetimeCompiler.Warning(
                compiled,
                "VFXCURVE002",
                semantic.PropertyId + " 的 " + semantic.Operation + " 需要把两条 Hermite 曲线编译为确定性采样曲线；Replace 可完整保留原始 Key/切线。",
                memberId,
                semantic.PropertyId,
                "UseReplaceForExactKeys");
            return source.mode == ParticleSystemCurveMode.TwoConstants || source.mode == ParticleSystemCurveMode.TwoCurves
                ? new ParticleSystem.MinMaxCurve(1f, targetMin, targetMax)
                : new ParticleSystem.MinMaxCurve(1f, targetMax);
        }

        private static AnimationCurve CurveForMode(ParticleSystem.MinMaxCurve source, bool minimum)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Curve:
                    return ScaledCurve(source.curve, source.curveMultiplier);
                case ParticleSystemCurveMode.TwoCurves:
                    return ScaledCurve(minimum ? source.curveMin : source.curveMax, source.curveMultiplier);
                case ParticleSystemCurveMode.TwoConstants:
                    return AnimationCurve.Constant(0f, 1f, minimum ? source.constantMin : source.constantMax);
                default:
                    return AnimationCurve.Constant(0f, 1f, source.constantMax);
            }
        }

        private static AnimationCurve ScaledCurve(AnimationCurve source, float multiplier)
        {
            AnimationCurve result = VfxCurveAuthoringJson.Clone(source) ?? AnimationCurve.Constant(0f, 1f, 0f);
            Keyframe[] keys = result.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= multiplier;
                keys[i].inTangent *= multiplier;
                keys[i].outTangent *= multiplier;
            }
            result.keys = keys;
            return result;
        }

        private static AnimationCurve ComposeCurve(AnimationCurve source, AnimationCurve modifier, string operation, float weight)
        {
            const int SampleCount = 65;
            var keys = new Keyframe[SampleCount];
            float clampedWeight = Mathf.Clamp01(weight);
            for (int i = 0; i < SampleCount; i++)
            {
                float time = i / (SampleCount - 1f);
                float sourceValue = source.Evaluate(time);
                float modifierValue = modifier.Evaluate(time);
                float final;
                if (operation == nameof(VfxSemanticOperation.Add)) final = sourceValue + modifierValue * clampedWeight;
                else if (operation == nameof(VfxSemanticOperation.Remap)) final = source.Evaluate(Mathf.Clamp01(Mathf.Lerp(time, modifierValue, clampedWeight)));
                else final = sourceValue * Mathf.Lerp(1f, modifierValue, clampedWeight);
                keys[i] = new Keyframe(time, final);
            }
            return new AnimationCurve(keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }

        private static ParticleSystem.MinMaxGradient TransformOpacityEnvelope(
            ParticleSystem.MinMaxGradient source,
            AnimationCurve envelope,
            string operation,
            float weight,
            VfxCompiledLayerView compiled,
            string memberId,
            string propertyId)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(BuildOpacityGradient(null, source.color, envelope, operation, weight, compiled, memberId, propertyId));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        BuildOpacityGradient(null, source.colorMin, envelope, operation, weight, compiled, memberId, propertyId),
                        BuildOpacityGradient(null, source.colorMax, envelope, operation, weight, compiled, memberId, propertyId));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        BuildOpacityGradient(source.gradientMin, Color.white, envelope, operation, weight, compiled, memberId, propertyId),
                        BuildOpacityGradient(source.gradientMax, Color.white, envelope, operation, weight, compiled, memberId, propertyId));
                default:
                    return new ParticleSystem.MinMaxGradient(BuildOpacityGradient(source.gradient, Color.white, envelope, operation, weight, compiled, memberId, propertyId));
            }
        }

        private static Gradient BuildOpacityGradient(
            Gradient source,
            Color fallback,
            AnimationCurve envelope,
            string operation,
            float weight,
            VfxCompiledLayerView compiled,
            string memberId,
            string propertyId)
        {
            float[] times = (source?.alphaKeys.Select(value => value.time) ?? Enumerable.Empty<float>())
                .Concat(envelope.keys.Select(value => value.time))
                .Concat(new[] { 0f, 1f })
                .Select(Mathf.Clamp01)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (times.Length > 8)
            {
                times = Enumerable.Range(0, 8).Select(index => index / 7f).ToArray();
                VfxSemanticRetimeCompiler.Warning(compiled, "VFXCURVE001", propertyId + " 超过 Gradient 的 8 Key 表达上限，已使用确定性 8 点采样。", memberId, propertyId, "UseShaderOpacityProfile");
            }
            GradientColorKey[] colors = source?.colorKeys;
            if (colors == null || colors.Length == 0)
                colors = new[] { new GradientColorKey(fallback, 0f), new GradientColorKey(fallback, 1f) };
            var alphas = new GradientAlphaKey[times.Length];
            float clampedWeight = Mathf.Clamp01(weight);
            for (int i = 0; i < times.Length; i++)
            {
                float time = times[i];
                float sourceAlpha = source != null ? source.Evaluate(time).a : fallback.a;
                float modifier = envelope.Evaluate(time);
                float target = operation == nameof(VfxSemanticOperation.Replace)
                    ? Mathf.Lerp(sourceAlpha, modifier, clampedWeight)
                    : operation == nameof(VfxSemanticOperation.Add)
                        ? sourceAlpha + modifier * clampedWeight
                        : operation == nameof(VfxSemanticOperation.Remap)
                            ? (source != null ? source.Evaluate(Mathf.Clamp01(Mathf.Lerp(time, modifier, clampedWeight))).a : fallback.a)
                            : sourceAlpha * Mathf.Lerp(1f, modifier, clampedWeight);
                alphas[i] = new GradientAlphaKey(Mathf.Clamp01(target), time);
            }
            var result = new Gradient { mode = source?.mode ?? GradientMode.Blend };
            result.SetKeys(colors, alphas);
            return result;
        }

        private static ParticleSystem.MinMaxCurve Multiply(ParticleSystem.MinMaxCurve source, float factor)
        {
            source.curveMultiplier *= factor;
            return source;
        }

        private static ParticleSystem.MinMaxCurve Add(ParticleSystem.MinMaxCurve source, float value)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    source.constant = source.constant + value;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    source.constantMin += value;
                    source.constantMax += value;
                    break;
                case ParticleSystemCurveMode.Curve:
                    source.curve = OffsetCurve(source.curve, value / Mathf.Max(0.00001f, source.curveMultiplier));
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    source.curveMin = OffsetCurve(source.curveMin, value / Mathf.Max(0.00001f, source.curveMultiplier));
                    source.curveMax = OffsetCurve(source.curveMax, value / Mathf.Max(0.00001f, source.curveMultiplier));
                    break;
            }
            return source;
        }

        private static AnimationCurve OffsetCurve(AnimationCurve source, float value)
        {
            if (source == null) return null;
            Keyframe[] keys = source.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value += value;
            return new AnimationCurve(keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }

        private static ParticleSystem.MinMaxGradient TransformGradient(ParticleSystem.MinMaxGradient source, Func<Color, Color> transform)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color: source.color = transform(source.color); break;
                case ParticleSystemGradientMode.TwoColors: source.colorMin = transform(source.colorMin); source.colorMax = transform(source.colorMax); break;
                case ParticleSystemGradientMode.Gradient: source.gradient = Transform(source.gradient, transform); break;
                case ParticleSystemGradientMode.TwoGradients: source.gradientMin = Transform(source.gradientMin, transform); source.gradientMax = Transform(source.gradientMax, transform); break;
                case ParticleSystemGradientMode.RandomColor: source.gradient = Transform(source.gradient, transform); break;
            }
            return source;
        }

        private static Gradient Transform(Gradient source, Func<Color, Color> transform)
        {
            if (source == null) return null;
            GradientColorKey[] colors = source.colorKeys;
            GradientAlphaKey[] alphas = source.alphaKeys;
            for (int i = 0; i < colors.Length; i++) colors[i].color = transform(colors[i].color);
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha = transform(new Color(1f, 1f, 1f, alphas[i].alpha)).a;
            var target = new Gradient { mode = source.mode };
            target.SetKeys(colors, alphas);
            return target;
        }

        private static float Apply(float source, string operation, float value)
        {
            if (operation == nameof(VfxSemanticOperation.Add)) return source + value;
            if (operation == nameof(VfxSemanticOperation.Replace)) return value;
            return source * value;
        }

        private static float Response(float value, float weight, float neutral)
        {
            return Mathf.Lerp(neutral, value, Mathf.Clamp01(weight));
        }

        private static Color ColorValue(JToken value, Color fallback)
        {
            if (value is not JArray array || array.Count < 3) return fallback;
            float r = array[0].Value<float>();
            float g = array[1].Value<float>();
            float b = array[2].Value<float>();
            float a = array.Count > 3 ? array[3].Value<float>() : 1f;
            float exposure = array.Count > 4 ? Mathf.Pow(2f, array[4].Value<float>()) : 1f;
            return new Color(r * exposure, g * exposure, b * exposure, a);
        }

        private static Vector3 VectorValue(JToken value, Vector3 fallback)
        {
            if (value is not JArray array || array.Count < 3) return fallback;
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
        }

        private static Vector3 Vector(IReadOnlyList<float> value, Vector3 fallback)
        {
            return value != null && value.Count == 3 ? new Vector3(value[0], value[1], value[2]) : fallback;
        }

        private static string CurveSummary(ParticleSystem.MinMaxCurve value) => value.mode + " × " + Format(value.curveMultiplier) + " [" + Format(value.constantMin) + ", " + Format(value.constantMax) + "]";
        private static string GradientSummary(ParticleSystem.MinMaxGradient value) => value.mode.ToString();
        private static string Format(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static void Diff(VfxCompiledLayerView compiled, string memberId, VfxSemanticOverrideView semantic, string adapter, string path, string source, string final, VfxSemanticTimeDomain domain)
        {
            VfxSemanticRetimeCompiler.AddDiff(compiled, memberId, semantic.PropertyId, adapter, path, source, semantic.Operation, semantic.Value?.ToString(Formatting.None) ?? string.Empty, final, domain.ToString());
        }
    }
}

