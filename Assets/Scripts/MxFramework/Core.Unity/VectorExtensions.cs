// Source: WGame/Client/Assets/Scripts/Common/Vector3Extension.cs
// Migrated: 2026-05-05 — Batch 1 → Phase 1: moved to Core.Unity
// Fixed: ScaledToLength now normalizes before scaling

using UnityEngine;

namespace MxFramework.Core.Unity
{
    public static class VectorExtensions
    {
        private static readonly float TwoPI = Mathf.PI * 2f;

        /// <summary>Angle between two vectors (0 ~ PI)</summary>
        public static float AngleTo(this Vector3 v, Vector3 other)
        {
            if (v.sqrMagnitude <= Mathf.Epsilon || other.sqrMagnitude <= Mathf.Epsilon)
                return 0f;

            float d = Vector3.Dot(v.normalized, other.normalized);
            d = Mathf.Clamp(d, -1f, 1f);
            return Mathf.Acos(d);
        }

        public static float AngleTo(this Vector2 v, Vector2 other)
        {
            if (v.sqrMagnitude <= Mathf.Epsilon || other.sqrMagnitude <= Mathf.Epsilon)
                return 0f;

            float d = Vector2.Dot(v.normalized, other.normalized);
            d = Mathf.Clamp(d, -1f, 1f);
            return Mathf.Acos(d);
        }

        /// <summary>Clockwise angle (0 ~ 2*PI)</summary>
        public static float AngleTo360(this Vector3 v, Vector3 other, Vector3 up)
        {
            float angle = v.AngleTo(other);
            if (angle <= Mathf.Epsilon)
                return 0f;

            return v.IsClockwiseTo(other, up) ? angle : TwoPI - angle;
        }

        /// <summary>True if v is clockwise from other (around up axis)</summary>
        public static bool IsClockwiseTo(this Vector3 v, Vector3 other, Vector3 up)
        {
            var normal = Vector3.Cross(v, other);
            return Vector3.Dot(normal, up) > 0;
        }

        /// <summary>Normalize vector to length 1 then scale to target length</summary>
        public static Vector3 ScaledToLength(this Vector3 v, float length)
        {
            return v.normalized * length;
        }
    }
}
