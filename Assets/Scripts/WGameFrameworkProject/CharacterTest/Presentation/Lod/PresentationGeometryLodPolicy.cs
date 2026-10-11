using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Shared screen-size decisions; thresholds describe visual error, not hardware cost.</summary>
    internal static class PresentationGeometryLodPolicy
    {
        internal const float DefaultHysteresis = 0.15f;
        internal static int Select(float height, float high, float medium, int previous, float hysteresis)
        {
            if (!float.IsFinite(height) || height < 0 || !(high > medium) || !(medium > 0)) return 0;
            hysteresis = Math.Min(0.49f, Math.Max(0, hysteresis));
            if (previous < 0 || previous > 2) return height >= high ? 0 : height >= medium ? 1 : 2;
            if (previous == 0) return height >= high * (1 - hysteresis) ? 0 : height >= medium * (1 - hysteresis) ? 1 : 2;
            if (previous == 1) return height >= high * (1 + hysteresis) ? 0 : height >= medium * (1 - hysteresis) ? 1 : 2;
            return height >= high * (1 + hysteresis) ? 0 : height >= medium * (1 + hysteresis) ? 1 : 2;
        }

        /// <summary>Conservative projected sphere diameter relative to viewport height, including near-plane crossings.</summary>
        internal static float ScreenHeight(Matrix4x4 view, Matrix4x4 projection, Vector3 center, float radius, bool orthographic)
        {
            if (!(radius > 0) || !float.IsFinite(radius)) return float.PositiveInfinity;
            float scale = Mathf.Abs(projection.m11);
            if (!(scale > 0) || !float.IsFinite(scale)) return float.PositiveInfinity;
            if (orthographic) return radius * scale;
            float depth = -view.MultiplyPoint3x4(center).z;
            if (!float.IsFinite(depth) || depth <= radius) return float.PositiveInfinity;
            return radius * scale / (depth - radius);
        }
    }
}
