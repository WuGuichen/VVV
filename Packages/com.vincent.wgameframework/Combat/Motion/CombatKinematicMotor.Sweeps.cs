using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private bool TrySweepAabb(
            FixVector3 movingMin,
            FixVector3 movingMax,
            FixVector3 delta,
            ref bool hasDeltaLength,
            ref Fix64 deltaLength,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 normal)
        {
            if (StrictlyOverlaps(movingMin, movingMax, obstacleMin, obstacleMax))
            {
                FixVector3 initialNormal = GetInitialOverlapNormal(movingMin, movingMax, obstacleMin, obstacleMax);
                if (delta.Dot(initialNormal) >= Fix64.Zero)
                {
                    return NoSweep(out fraction, out distance, out normal);
                }

                fraction = Fix64.Zero;
                distance = Fix64.Zero;
                normal = initialNormal;
                return true;
            }

            Fix64 entryX;
            Fix64 exitX;
            if (!CalculateSweepAxis(movingMin.X, movingMax.X, obstacleMin.X, obstacleMax.X, delta.X, out entryX, out exitX))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entryY;
            Fix64 exitY;
            if (!CalculateSweepAxis(movingMin.Y, movingMax.Y, obstacleMin.Y, obstacleMax.Y, delta.Y, out entryY, out exitY))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entryZ;
            Fix64 exitZ;
            if (!CalculateSweepAxis(movingMin.Z, movingMax.Z, obstacleMin.Z, obstacleMax.Z, delta.Z, out entryZ, out exitZ))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entry = Fix64.Max(entryX, Fix64.Max(entryY, entryZ));
            Fix64 exit = Fix64.Min(exitX, Fix64.Min(exitY, exitZ));
            if (entry > exit || entry < Fix64.Zero || entry > Fix64.One)
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            fraction = entry;
            distance = ResolveDeltaLength(delta, ref hasDeltaLength, ref deltaLength) * fraction;
            normal = GetSweepNormal(entry, entryX, entryY, entryZ, delta);
            return true;
        }

        private bool CalculateSweepAxis(
            Fix64 movingMin,
            Fix64 movingMax,
            Fix64 obstacleMin,
            Fix64 obstacleMax,
            Fix64 delta,
            out Fix64 entry,
            out Fix64 exit)
        {
            if (delta.IsZero)
            {
                if (movingMax < obstacleMin || movingMin > obstacleMax)
                {
                    entry = Fix64.Zero;
                    exit = Fix64.Zero;
                    return false;
                }

                entry = LargeNegative;
                exit = LargePositive;
                return true;
            }

            if (delta > Fix64.Zero)
            {
                entry = (obstacleMin - movingMax) / delta;
                exit = (obstacleMax - movingMin) / delta;
            }
            else
            {
                entry = (obstacleMax - movingMin) / delta;
                exit = (obstacleMin - movingMax) / delta;
            }

            return true;
        }

        private static bool NoSweep(out Fix64 fraction, out Fix64 distance, out FixVector3 normal)
        {
            fraction = Fix64.Zero;
            distance = Fix64.Zero;
            normal = FixVector3.Zero;
            return false;
        }

        private static FixVector3 GetSweepNormal(Fix64 entry, Fix64 entryX, Fix64 entryY, Fix64 entryZ, FixVector3 delta)
        {
            if (entry == entryX && !delta.X.IsZero)
            {
                return delta.X > Fix64.Zero ? -AxisX : AxisX;
            }

            if (entry == entryY && !delta.Y.IsZero)
            {
                return delta.Y > Fix64.Zero ? -AxisY : AxisY;
            }

            if (entry == entryZ && !delta.Z.IsZero)
            {
                return delta.Z > Fix64.Zero ? -AxisZ : AxisZ;
            }

            return FixVector3.Zero;
        }

        private static FixVector3 GetInitialOverlapNormal(
            FixVector3 movingMin,
            FixVector3 movingMax,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax)
        {
            Fix64 movingCenterX = (movingMin.X + movingMax.X) / Fix64.FromInt(2);
            Fix64 movingCenterY = (movingMin.Y + movingMax.Y) / Fix64.FromInt(2);
            Fix64 movingCenterZ = (movingMin.Z + movingMax.Z) / Fix64.FromInt(2);
            Fix64 obstacleCenterX = (obstacleMin.X + obstacleMax.X) / Fix64.FromInt(2);
            Fix64 obstacleCenterY = (obstacleMin.Y + obstacleMax.Y) / Fix64.FromInt(2);
            Fix64 obstacleCenterZ = (obstacleMin.Z + obstacleMax.Z) / Fix64.FromInt(2);

            Fix64 overlapX = CalculatePenetrationDepth(movingMin.X, movingMax.X, obstacleMin.X, obstacleMax.X);
            Fix64 overlapY = CalculatePenetrationDepth(movingMin.Y, movingMax.Y, obstacleMin.Y, obstacleMax.Y);
            Fix64 overlapZ = CalculatePenetrationDepth(movingMin.Z, movingMax.Z, obstacleMin.Z, obstacleMax.Z);

            if (overlapY <= overlapX && overlapY <= overlapZ)
            {
                return movingCenterY >= obstacleCenterY ? AxisY : -AxisY;
            }

            if (overlapX <= overlapZ)
            {
                return movingCenterX >= obstacleCenterX ? AxisX : -AxisX;
            }

            return movingCenterZ >= obstacleCenterZ ? AxisZ : -AxisZ;
        }

        private static Fix64 CalculatePenetrationDepth(
            Fix64 movingMin,
            Fix64 movingMax,
            Fix64 obstacleMin,
            Fix64 obstacleMax)
        {
            Fix64 distanceToMinFace = movingMax - obstacleMin;
            Fix64 distanceToMaxFace = obstacleMax - movingMin;
            return Fix64.Min(distanceToMinFace, distanceToMaxFace);
        }

        private static bool StrictlyOverlaps(FixVector3 leftMin, FixVector3 leftMax, FixVector3 rightMin, FixVector3 rightMax)
        {
            return leftMin.X < rightMax.X
                && leftMax.X > rightMin.X
                && leftMin.Y < rightMax.Y
                && leftMax.Y > rightMin.Y
                && leftMin.Z < rightMax.Z
                && leftMax.Z > rightMin.Z;
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }
    }
}
