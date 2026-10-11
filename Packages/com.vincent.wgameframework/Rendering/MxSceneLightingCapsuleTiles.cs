using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Rendering
{
    /// <summary>Conservative screen bounds for finite, smoothly faded capsule influence.</summary>
    internal static class MxSceneLightingCapsuleTiles
    {
        internal const int TileSize = 16;
        internal const float MaximumOcclusionError = 0.01f;

        internal static Vector4 Range(float radius, float strength, int count)
        {
            // Each omitted term is <= error/count. The product's total change is <= error.
            float innerSquared = radius * radius * Mathf.Clamp01(strength) * Mathf.Max(1, count) / MaximumOcclusionError;
            float outerSquared = innerSquared * 1.5625f; // Fade from R to 1.25 R.
            return new Vector4(innerSquared, 1f / Mathf.Max(outerSquared - innerSquared, 1e-10f), outerSquared, 0);
        }

        internal static Vector4 ScreenRect(Vector3 start, Vector3 end, float radius,
            Matrix4x4 view, Matrix4x4 gpuProjection, float nearPlane, int width, int height, bool uvStartsAtTop)
        {
            if (radius <= 0) return Vector4.zero;
            Vector3 min = Vector3.Min(start, end) - Vector3.one * radius;
            Vector3 max = Vector3.Max(start, end) + Vector3.one * radius;
            float minDepth = float.PositiveInfinity, maxDepth = float.NegativeInfinity;
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            bool crossesEye = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 eye = view.MultiplyPoint3x4(world);
                minDepth = Mathf.Min(minDepth, -eye.z); maxDepth = Mathf.Max(maxDepth, -eye.z);
                Vector4 clip = gpuProjection * new Vector4(eye.x, eye.y, eye.z, 1);
                if (clip.w <= 1e-6f) { crossesEye = true; continue; }
                float x = (clip.x / clip.w * 0.5f + 0.5f) * width;
                float y = (clip.y / clip.w * (uvStartsAtTop ? -0.5f : 0.5f) + 0.5f) * height;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            if (maxDepth < nearPlane) return Vector4.zero;
            // Clipping a projected box at the near plane is unnecessary: full coverage is conservative.
            if (minDepth <= nearPlane || crossesEye) return new Vector4(0, 0, width, height);
            return new Vector4(Mathf.Clamp(Mathf.Floor(minX) - 1, 0, width),
                Mathf.Clamp(Mathf.Floor(minY) - 1, 0, height),
                Mathf.Clamp(Mathf.Ceil(maxX) + 1, 0, width),
                Mathf.Clamp(Mathf.Ceil(maxY) + 1, 0, height));
        }
    }
}
