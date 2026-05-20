using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    internal static class CombatPhysicsObbMath
    {
        public static void ValidateHalfExtents(FixVector3 halfExtents, string parameterName)
        {
            if (halfExtents.X < Fix64.Zero || halfExtents.Y < Fix64.Zero || halfExtents.Z < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(parameterName, "OBB half extents cannot be negative.");
            }
        }

        public static void NormalizeAxes(
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            out FixVector3 normalizedAxisX,
            out FixVector3 normalizedAxisY,
            out FixVector3 normalizedAxisZ)
        {
            if (!axisX.TryNormalize(out normalizedAxisX))
            {
                throw new ArgumentException("OBB axis X cannot be zero.", nameof(axisX));
            }

            if (!axisY.TryNormalize(out normalizedAxisY))
            {
                throw new ArgumentException("OBB axis Y cannot be zero.", nameof(axisY));
            }

            if (!axisZ.TryNormalize(out normalizedAxisZ))
            {
                throw new ArgumentException("OBB axis Z cannot be zero.", nameof(axisZ));
            }
        }

        public static void GetAabb(
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            out FixVector3 min,
            out FixVector3 max)
        {
            FixVector3 extent = new FixVector3(
                (Abs(axisX.X) * halfExtents.X) + (Abs(axisY.X) * halfExtents.Y) + (Abs(axisZ.X) * halfExtents.Z),
                (Abs(axisX.Y) * halfExtents.X) + (Abs(axisY.Y) * halfExtents.Y) + (Abs(axisZ.Y) * halfExtents.Z),
                (Abs(axisX.Z) * halfExtents.X) + (Abs(axisY.Z) * halfExtents.Y) + (Abs(axisZ.Z) * halfExtents.Z));

            min = center - extent;
            max = center + extent;
        }

        public static bool OverlapsAabb(
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            FixVector3 aabbMin,
            FixVector3 aabbMax)
        {
            FixVector3 aabbCenter = new FixVector3(
                (aabbMin.X + aabbMax.X) / Fix64.FromInt(2),
                (aabbMin.Y + aabbMax.Y) / Fix64.FromInt(2),
                (aabbMin.Z + aabbMax.Z) / Fix64.FromInt(2));
            FixVector3 aabbHalfExtents = new FixVector3(
                (aabbMax.X - aabbMin.X) / Fix64.FromInt(2),
                (aabbMax.Y - aabbMin.Y) / Fix64.FromInt(2),
                (aabbMax.Z - aabbMin.Z) / Fix64.FromInt(2));
            FixVector3 delta = center - aabbCenter;

            if (!OverlapsOnAxis(delta, axisX, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                || !OverlapsOnAxis(delta, axisY, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                || !OverlapsOnAxis(delta, axisZ, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                || !OverlapsOnAxis(delta, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                || !OverlapsOnAxis(delta, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero), halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                || !OverlapsOnAxis(delta, new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One), halfExtents, axisX, axisY, axisZ, aabbHalfExtents))
            {
                return false;
            }

            FixVector3 worldX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            FixVector3 worldY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            FixVector3 worldZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            return OverlapsOnCrossAxis(delta, axisX, worldX, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisX, worldY, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisX, worldZ, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisY, worldX, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisY, worldY, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisY, worldZ, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisZ, worldX, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisZ, worldY, halfExtents, axisX, axisY, axisZ, aabbHalfExtents)
                && OverlapsOnCrossAxis(delta, axisZ, worldZ, halfExtents, axisX, axisY, axisZ, aabbHalfExtents);
        }

        private static bool OverlapsOnCrossAxis(
            FixVector3 delta,
            FixVector3 left,
            FixVector3 right,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            FixVector3 aabbHalfExtents)
        {
            FixVector3 axis = Cross(left, right);
            return axis.IsZero || OverlapsOnAxis(delta, axis, halfExtents, axisX, axisY, axisZ, aabbHalfExtents);
        }

        private static bool OverlapsOnAxis(
            FixVector3 delta,
            FixVector3 axis,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            FixVector3 aabbHalfExtents)
        {
            Fix64 distance = Abs(delta.Dot(axis));
            Fix64 obbRadius = (halfExtents.X * Abs(axisX.Dot(axis)))
                + (halfExtents.Y * Abs(axisY.Dot(axis)))
                + (halfExtents.Z * Abs(axisZ.Dot(axis)));
            Fix64 aabbRadius = (aabbHalfExtents.X * Abs(axis.X))
                + (aabbHalfExtents.Y * Abs(axis.Y))
                + (aabbHalfExtents.Z * Abs(axis.Z));

            return distance <= obbRadius + aabbRadius;
        }

        private static FixVector3 Cross(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
        }

        private static Fix64 Abs(Fix64 value)
        {
            return value < Fix64.Zero ? -value : value;
        }
    }
}
