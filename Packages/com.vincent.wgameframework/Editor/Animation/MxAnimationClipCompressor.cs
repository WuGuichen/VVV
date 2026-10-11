using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    internal sealed class MxAnimationClipCompressionPlan
    {
        internal readonly List<EditorCurveBinding> Bindings = new List<EditorCurveBinding>();
        internal readonly List<AnimationCurve> Curves = new List<AnimationCurve>();
        internal int OriginalKeys;
        internal int ReducedKeys;
        internal int ProtectedCurves;
        internal int UnsupportedCurves;
    }

    internal static class MxAnimationClipCompressor
    {
        internal const float DefaultTolerance = 0.001f;

        internal static MxAnimationClipCompressionPlan Analyze(AnimationClip clip, float tolerance, bool preserveRootMotion)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            if (!IsFinite(tolerance) || tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            var plan = new MxAnimationClipCompressionPlan();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
                if (source == null)
                    continue;

                plan.OriginalKeys += source.length;
                plan.ReducedKeys += source.length;
                if (preserveRootMotion && IsRootMotion(binding))
                {
                    plan.ProtectedCurves++;
                    continue;
                }
                if (binding.isDiscreteCurve || binding.isPPtrCurve)
                {
                    plan.UnsupportedCurves++;
                    continue;
                }

                AnimationCurve reduced = Reduce(source, tolerance);
                if (reduced == null)
                {
                    if (source.length > 2)
                        plan.UnsupportedCurves++;
                    continue;
                }

                plan.Bindings.Add(binding);
                plan.Curves.Add(reduced);
                plan.ReducedKeys -= source.length - reduced.length;
            }
            return plan;
        }

        private static bool IsRootMotion(EditorCurveBinding binding)
        {
            string property = binding.propertyName;
            return property.StartsWith("RootT.", StringComparison.Ordinal)
                || property.StartsWith("RootQ.", StringComparison.Ordinal)
                || property.StartsWith("MotionT.", StringComparison.Ordinal)
                || property.StartsWith("MotionQ.", StringComparison.Ordinal)
                || (binding.type == typeof(Transform) && string.IsNullOrEmpty(binding.path));
        }

        private static AnimationCurve Reduce(AnimationCurve source, double tolerance)
        {
            if (source.length <= 2)
                return null;

            Keyframe[] keys = source.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                if (key.weightedMode != WeightedMode.None || !IsFinite(key.time) || !IsFinite(key.value)
                    || !IsFinite(key.inTangent) || !IsFinite(key.outTangent)
                    || (i > 0 && key.time <= keys[i - 1].time))
                    return null;
            }

            var retained = new bool[keys.Length];
            retained[0] = retained[keys.Length - 1] = true;
            var pending = new Stack<KeyRange>();
            pending.Push(new KeyRange(0, keys.Length - 1));
            int retainedCount = 2;
            while (pending.Count > 0)
            {
                KeyRange range = pending.Pop();
                if (range.Last - range.First <= 1)
                    continue;

                int split = FindSplit(keys, range.First, range.Last, tolerance);
                if (split < 0)
                    continue;

                retained[split] = true;
                retainedCount++;
                pending.Push(new KeyRange(range.First, split));
                pending.Push(new KeyRange(split, range.Last));
            }

            if (retainedCount == keys.Length)
                return null;

            var reducedKeys = new Keyframe[retainedCount];
            for (int i = 0, target = 0; i < keys.Length; i++)
            {
                if (retained[i])
                    reducedKeys[target++] = keys[i];
            }

            var result = new AnimationCurve(reducedKeys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            // Keep the analyzed endpoint slopes; automatic tangents could change them after key removal.
            for (int i = 0; i < result.length; i++)
            {
                AnimationUtility.SetKeyBroken(result, i, true);
                AnimationUtility.SetKeyLeftTangentMode(result, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(result, i, AnimationUtility.TangentMode.Free);
            }
            return result;
        }

        private static int FindSplit(Keyframe[] keys, int first, int last, double tolerance)
        {
            double duration = (double)keys[last].time - keys[first].time;
            Cubic candidate = Cubic.FromKeys(keys[first], keys[last]);
            double maxError = tolerance;
            int split = -1;
            for (int i = first; i < last; i++)
            {
                double start = ((double)keys[i].time - keys[first].time) / duration;
                double scale = ((double)keys[i + 1].time - keys[i].time) / duration;
                Cubic difference = Cubic.FromKeys(keys[i], keys[i + 1]) - candidate.Remap(start, scale);
                difference.MaximumAbsoluteValue(out double error, out double position);
                if (error <= maxError)
                    continue;

                maxError = error;
                split = Math.Max(first + 1, Math.Min(last - 1, position < 0.5 ? i : i + 1));
            }
            return split;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct KeyRange
        {
            internal readonly int First;
            internal readonly int Last;

            internal KeyRange(int first, int last)
            {
                First = first;
                Last = last;
            }
        }

        // Unweighted Unity curves are cubic Hermite segments. Their difference is also a cubic,
        // so endpoints and derivative roots bound the error between keys, including overshoots.
        private readonly struct Cubic
        {
            private readonly double _a;
            private readonly double _b;
            private readonly double _c;
            private readonly double _d;

            private Cubic(double a, double b, double c, double d)
            {
                _a = a;
                _b = b;
                _c = c;
                _d = d;
            }

            internal static Cubic FromKeys(Keyframe left, Keyframe right)
            {
                double duration = (double)right.time - left.time;
                double outgoing = duration * left.outTangent;
                double incoming = duration * right.inTangent;
                double delta = (double)right.value - left.value;
                return new Cubic(-2 * delta + outgoing + incoming,
                    3 * delta - 2 * outgoing - incoming, outgoing, left.value);
            }

            internal Cubic Remap(double start, double scale)
            {
                return new Cubic(_a * scale * scale * scale,
                    (3 * _a * start + _b) * scale * scale,
                    (3 * _a * start * start + 2 * _b * start + _c) * scale,
                    Evaluate(start));
            }

            public static Cubic operator -(Cubic left, Cubic right)
            {
                return new Cubic(left._a - right._a, left._b - right._b,
                    left._c - right._c, left._d - right._d);
            }

            internal void MaximumAbsoluteValue(out double error, out double position)
            {
                error = Math.Abs(_d);
                position = 0;
                Consider(1, ref error, ref position);
                double a = 3 * _a;
                double b = 2 * _b;
                if (Math.Abs(a) < 1e-15)
                {
                    if (Math.Abs(b) >= 1e-15)
                        Consider(-_c / b, ref error, ref position);
                    return;
                }

                double discriminant = b * b - 4 * a * _c;
                if (discriminant < 0)
                    return;
                // Stable quadratic roots avoid cancellation for almost-linear derivative polynomials.
                double root = Math.Sqrt(discriminant);
                double q = -0.5 * (b + (b >= 0 ? root : -root));
                if (Math.Abs(q) < 1e-30)
                    Consider(-b / (2 * a), ref error, ref position);
                else
                {
                    Consider(q / a, ref error, ref position);
                    Consider(_c / q, ref error, ref position);
                }
            }

            private double Evaluate(double position)
            {
                return ((_a * position + _b) * position + _c) * position + _d;
            }

            private void Consider(double candidate, ref double error, ref double position)
            {
                if (candidate < 0 || candidate > 1)
                    return;
                double value = Math.Abs(Evaluate(candidate));
                if (value > error)
                {
                    error = value;
                    position = candidate;
                }
            }
        }
    }
}
