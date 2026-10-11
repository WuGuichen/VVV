using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    internal enum CombatObbCastStatus
    {
        Miss = 0,
        Hit = 1,
        ZeroDelta = 2,
        InitialTouching = 3,
        InitialOverlap = 4,
        IterationLimitConservativeHit = 5,
        PrecisionLimitConservativeHit = 6,
        InvalidInput = 7,
    }

    internal static class CombatMotionObbCast
    {
        internal const int MaxCastIterations = 40;
        internal const long ContactThresholdRaw = 16L;

        private const long MaxWorldCoordinateAbsRaw = 2000000000L;
        private const long MaxLocalCoordinateAbsRaw = 500000000L;
        private const long MaxDeltaComponentAbsRaw = 1100000000L;
        private const long MaxRadiusOrExtentRaw = 500000000L;
        private const long MaxWitnessComponentAbsRaw = 1000000000L;
        private const long BasisToleranceRaw = 64L;

        internal static bool TryCast(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out CombatObbCastStatus status)
        {
            return TryCast(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                out fraction,
                out distance,
                out worldNormal,
                out _,
                out status);
        }

        internal static bool TryCast(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out FixVector3 nonEnteringDirection,
            out CombatObbCastStatus status)
        {
            bool separatedHit = TryCastSeparated(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                out fraction,
                out distance,
                out worldNormal,
                out FixVector3 separatedNonEnteringDirection,
                out status);
            if (status != CombatObbCastStatus.InitialTouching
                && status != CombatObbCastStatus.InitialOverlap)
            {
                nonEnteringDirection = separatedNonEnteringDirection;
                return separatedHit;
            }

            if (!TryPrepare(capsule, startPosition, desiredDelta, targetObb, out FixVector3 pointA, out FixVector3 pointB)
                || !TryClosestWitness(pointA, pointB, targetObb, out FixVector3 segmentWitness, out FixVector3 boxWitness))
            {
                nonEnteringDirection = FixVector3.Zero;
                return Invalid(out fraction, out distance, out worldNormal, out status);
            }

            FixVector3 witness = segmentWitness - boxWitness;
            long distanceSquaredRaw2 = SquareLengthRaw2Checked(witness);
            long radiusRaw = capsule.Radius.RawValue;
            long touchingUpperRaw = checked(radiusRaw + ContactThresholdRaw);
            long touchingLowerRaw = radiusRaw > ContactThresholdRaw
                ? radiusRaw - ContactThresholdRaw
                : 0L;
            long touchingUpperSquaredRaw2 = checked(touchingUpperRaw * touchingUpperRaw);
            long touchingLowerSquaredRaw2 = checked(touchingLowerRaw * touchingLowerRaw);

            if (distanceSquaredRaw2 > touchingUpperSquaredRaw2)
            {
                nonEnteringDirection = FixVector3.Zero;
                return Invalid(out fraction, out distance, out worldNormal, out status);
            }

            bool overlaps = distanceSquaredRaw2 < touchingLowerSquaredRaw2;
            status = overlaps ? CombatObbCastStatus.InitialOverlap : CombatObbCastStatus.InitialTouching;
            long directionDotRaw2;
            if (!witness.IsZero)
            {
                directionDotRaw2 = DotRaw2Checked(desiredDelta, witness);
                if (!TryNormalizeRawWitness(witness, distanceSquaredRaw2, out worldNormal))
                {
                    nonEnteringDirection = FixVector3.Zero;
                    return Invalid(out fraction, out distance, out worldNormal, out status);
                }
                nonEnteringDirection = witness;
            }
            else if (!TryGetIntersectingNormal(pointA, pointB, capsule.Radius, targetObb, out worldNormal))
            {
                nonEnteringDirection = FixVector3.Zero;
                return Invalid(out fraction, out distance, out worldNormal, out status);
            }
            else
            {
                directionDotRaw2 = DotRaw2Checked(desiredDelta, worldNormal);
                nonEnteringDirection = worldNormal;
            }

            fraction = Fix64.Zero;
            distance = Fix64.Zero;
            return directionDotRaw2 < 0;
        }

        internal static Fix64 CalculateSafeFraction(
            Fix64 contactFraction,
            Fix64 contactDistance,
            FixVector3 desiredDelta,
            in CombatMotionCapsuleProxy capsule)
        {
            if (contactDistance <= capsule.SkinWidth)
                return Fix64.Zero;

            Fix64 totalDistance = desiredDelta.LengthSquared().Sqrt();
            if (totalDistance.IsZero)
                return Fix64.Zero;

            return Fix64.Clamp(
                (contactDistance - capsule.SkinWidth) / totalDistance,
                Fix64.Zero,
                contactFraction);
        }

        internal static bool TryCastSeparated(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out CombatObbCastStatus status)
        {
            return TryCastSeparated(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                out fraction,
                out distance,
                out worldNormal,
                out _,
                out status);
        }

        internal static bool TryCastSeparated(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out FixVector3 nonEnteringDirection,
            out CombatObbCastStatus status)
        {
            return TryCastSeparatedCore(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                MaxCastIterations,
                out fraction,
                out distance,
                out worldNormal,
                out nonEnteringDirection,
                out status);
        }

        internal static bool TryCastSeparatedForTesting(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            int maxIterations,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out CombatObbCastStatus status)
        {
            return TryCastSeparatedForTesting(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                maxIterations,
                out fraction,
                out distance,
                out worldNormal,
                out _,
                out status);
        }

        internal static bool TryCastSeparatedForTesting(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            int maxIterations,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out FixVector3 nonEnteringDirection,
            out CombatObbCastStatus status)
        {
            return TryCastSeparatedCore(
                capsule,
                startPosition,
                desiredDelta,
                targetObb,
                maxIterations,
                out fraction,
                out distance,
                out worldNormal,
                out nonEnteringDirection,
                out status);
        }

        internal static bool TryInspectSeparatedStartForTesting(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            out FixVector3 witness,
            out long distanceSquaredRaw2,
            out long lengthFloor,
            out long lengthCeil,
            out long supportGapLowerRaw2,
            out long thresholdDirectionUpperRaw2,
            out long currentExpandedGapLowerRaw2,
            out long deltaDotWitnessRaw2,
            out FixVector3 normalizedDirection,
            out long normalizedLengthCeil,
            out long normalizedSupportGapLowerRaw2,
            out long normalizedThresholdDirectionUpperRaw2,
            out long normalizedCurrentExpandedGapLowerRaw2,
            out long deltaDotNormalizedRaw2)
        {
            witness = FixVector3.Zero;
            distanceSquaredRaw2 = 0L;
            lengthFloor = 0L;
            lengthCeil = 0L;
            supportGapLowerRaw2 = 0L;
            thresholdDirectionUpperRaw2 = 0L;
            currentExpandedGapLowerRaw2 = 0L;
            deltaDotWitnessRaw2 = 0L;
            normalizedDirection = FixVector3.Zero;
            normalizedLengthCeil = 0L;
            normalizedSupportGapLowerRaw2 = 0L;
            normalizedThresholdDirectionUpperRaw2 = 0L;
            normalizedCurrentExpandedGapLowerRaw2 = 0L;
            deltaDotNormalizedRaw2 = 0L;
            if (!TryPrepare(
                    capsule,
                    startPosition,
                    desiredDelta,
                    targetObb,
                    out FixVector3 startA,
                    out FixVector3 startB)
                || !TryClosestWitness(
                    startA,
                    startB,
                    targetObb,
                    out FixVector3 segmentWitness,
                    out FixVector3 boxWitness))
            {
                return false;
            }

            witness = segmentWitness - boxWitness;
            distanceSquaredRaw2 = SquareLengthRaw2Checked(witness);
            lengthFloor = SqrtFloorRaw2(distanceSquaredRaw2);
            lengthCeil = SqrtCeilRaw2(distanceSquaredRaw2);
            if (lengthFloor <= 0L
                || !TrySupportGapLowerRaw2(
                    startA,
                    startB,
                    targetObb,
                    witness,
                    out supportGapLowerRaw2))
            {
                return false;
            }

            long thresholdRadiusRaw = checked(capsule.Radius.RawValue + ContactThresholdRaw);
            thresholdDirectionUpperRaw2 = checked(thresholdRadiusRaw * lengthCeil);
            currentExpandedGapLowerRaw2 = checked(
                supportGapLowerRaw2 - thresholdDirectionUpperRaw2);
            deltaDotWitnessRaw2 = DotRaw2Checked(desiredDelta, witness);
            if (!TryNormalizeRawWitness(witness, distanceSquaredRaw2, out normalizedDirection))
                return false;
            long normalizedDistanceSquaredRaw2 = SquareLengthRaw2Checked(normalizedDirection);
            normalizedLengthCeil = SqrtCeilRaw2(normalizedDistanceSquaredRaw2);
            if (normalizedLengthCeil <= 0L
                || !TrySupportGapLowerRaw2(
                    startA,
                    startB,
                    targetObb,
                    normalizedDirection,
                    out normalizedSupportGapLowerRaw2))
            {
                return false;
            }

            normalizedThresholdDirectionUpperRaw2 = checked(
                thresholdRadiusRaw * normalizedLengthCeil);
            normalizedCurrentExpandedGapLowerRaw2 = checked(
                normalizedSupportGapLowerRaw2
                - normalizedThresholdDirectionUpperRaw2);
            deltaDotNormalizedRaw2 = DotRaw2Checked(desiredDelta, normalizedDirection);
            return true;
        }

        internal static bool TryInspectExpandedDirectionForTesting(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            FixVector3 direction,
            out long directionLengthCeil,
            out long supportGapLowerRaw2,
            out long thresholdDirectionUpperRaw2,
            out long expandedGapLowerRaw2,
            out long deltaDotDirectionRaw2)
        {
            directionLengthCeil = 0L;
            supportGapLowerRaw2 = 0L;
            thresholdDirectionUpperRaw2 = 0L;
            expandedGapLowerRaw2 = 0L;
            deltaDotDirectionRaw2 = 0L;
            if (direction.IsZero
                || !TryPrepare(
                    capsule,
                    startPosition,
                    desiredDelta,
                    targetObb,
                    out FixVector3 startA,
                    out FixVector3 startB))
            {
                return false;
            }

            long directionLengthSquaredRaw2 = SquareLengthRaw2Checked(direction);
            directionLengthCeil = SqrtCeilRaw2(directionLengthSquaredRaw2);
            if (directionLengthCeil <= 0L
                || !TrySupportGapLowerRaw2(
                    startA,
                    startB,
                    targetObb,
                    direction,
                    out supportGapLowerRaw2))
            {
                return false;
            }

            long thresholdRadiusRaw = checked(capsule.Radius.RawValue + ContactThresholdRaw);
            thresholdDirectionUpperRaw2 = checked(
                thresholdRadiusRaw * directionLengthCeil);
            expandedGapLowerRaw2 = checked(
                supportGapLowerRaw2 - thresholdDirectionUpperRaw2);
            deltaDotDirectionRaw2 = DotRaw2Checked(desiredDelta, direction);
            return true;
        }

        private static bool TryCastSeparatedCore(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape targetObb,
            int maxIterations,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out FixVector3 nonEnteringDirection,
            out CombatObbCastStatus status)
        {
            Reset(out fraction, out distance, out worldNormal);
            nonEnteringDirection = FixVector3.Zero;
            status = CombatObbCastStatus.InvalidInput;
            if (maxIterations <= 0 || maxIterations > MaxCastIterations)
                return false;

            if (!TryPrepare(capsule, startPosition, desiredDelta, targetObb, out FixVector3 startA, out FixVector3 startB))
                return false;

            Fix64 currentFraction = Fix64.Zero;
            FixVector3 lastNormal = FixVector3.Zero;
            FixVector3 lastWitness = FixVector3.Zero;
            long radiusRaw = capsule.Radius.RawValue;
            long thresholdRadiusRaw = radiusRaw + ContactThresholdRaw;
            long radiusSquaredRaw2 = checked(radiusRaw * radiusRaw);
            long thresholdRadiusSquaredRaw2 = checked(thresholdRadiusRaw * thresholdRadiusRaw);

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                FixVector3 movement = desiredDelta * currentFraction;
                FixVector3 worldA = startA + movement;
                FixVector3 worldB = startB + movement;
                if (!WithinWorld(worldA) || !WithinWorld(worldB)
                    || !TryClosestWitness(worldA, worldB, targetObb, out FixVector3 segmentWitnessWorld, out FixVector3 boxWitnessWorld))
                {
                    return Invalid(out fraction, out distance, out worldNormal, out status);
                }

                FixVector3 witness = segmentWitnessWorld - boxWitnessWorld;
                if (!WithinWitness(witness))
                    return Invalid(out fraction, out distance, out worldNormal, out status);

                long distanceSquaredRaw2 = SquareLengthRaw2Checked(witness);
                if (iteration == 0 && distanceSquaredRaw2 <= thresholdRadiusSquaredRaw2)
                {
                    status = distanceSquaredRaw2 < radiusSquaredRaw2
                        ? CombatObbCastStatus.InitialOverlap
                        : CombatObbCastStatus.InitialTouching;
                    return false;
                }

                if (!TryNormalizeRawWitness(witness, distanceSquaredRaw2, out lastNormal))
                    return Invalid(out fraction, out distance, out worldNormal, out status);
                lastWitness = witness;

                if (iteration == 0 && desiredDelta.IsZero)
                {
                    status = CombatObbCastStatus.ZeroDelta;
                    return false;
                }

                if (distanceSquaredRaw2 <= thresholdRadiusSquaredRaw2)
                    return Hit(
                        currentFraction,
                        desiredDelta,
                        lastNormal,
                        witness,
                        CombatObbCastStatus.Hit,
                        out fraction,
                        out distance,
                        out worldNormal,
                        out nonEnteringDirection,
                        out status);

                if (TryProveExpandedNonClosingSeparation(
                        worldA,
                        worldB,
                        targetObb,
                        witness,
                        thresholdRadiusRaw,
                        desiredDelta)
                    || TryProveExpandedNonClosingSeparation(
                        worldA,
                        worldB,
                        targetObb,
                        lastNormal,
                        thresholdRadiusRaw,
                        desiredDelta)
                    || TryProveExpandedNonClosingObbAxisSeparation(
                        worldA,
                        worldB,
                        targetObb,
                        thresholdRadiusRaw,
                        desiredDelta))
                {
                    status = CombatObbCastStatus.Miss;
                    nonEnteringDirection = FixVector3.Zero;
                    return false;
                }

                long lengthFloor = SqrtFloorRaw2(distanceSquaredRaw2);
                long lengthCeil = SqrtCeilRaw2(distanceSquaredRaw2);
                if (!TrySupportGapLowerRaw2(worldA, worldB, targetObb, witness, out long supportGapLowerRaw2)
                    || supportGapLowerRaw2 <= 0)
                {
                    return Hit(
                        currentFraction,
                        desiredDelta,
                        lastNormal,
                        witness,
                        CombatObbCastStatus.PrecisionLimitConservativeHit,
                        out fraction,
                        out distance,
                        out worldNormal,
                        out nonEnteringDirection,
                        out status);
                }

                long deltaDotWitnessRaw2 = DotRaw2Checked(desiredDelta, witness);
                long thresholdDirectionUpperRaw2 = checked(thresholdRadiusRaw * lengthCeil);
                long currentExpandedGapLowerRaw2 = checked(
                    supportGapLowerRaw2 - thresholdDirectionUpperRaw2);
                long supportGapMetricLower = supportGapLowerRaw2 / lengthCeil;
                long advanceGapLower = supportGapMetricLower - radiusRaw - ContactThresholdRaw;
                if (advanceGapLower <= 0)
                    return Hit(
                        currentFraction,
                        desiredDelta,
                        lastNormal,
                        witness,
                        CombatObbCastStatus.PrecisionLimitConservativeHit,
                        out fraction,
                        out distance,
                        out worldNormal,
                        out nonEnteringDirection,
                        out status);

                long remainingRaw = Fix64.Scale - currentFraction.RawValue;
                long endSupportLowerRaw2 = checked(
                    currentExpandedGapLowerRaw2
                    + MulDivFloorSigned(deltaDotWitnessRaw2, remainingRaw, Fix64.Scale));
                if (endSupportLowerRaw2 > 0)
                {
                    status = CombatObbCastStatus.Miss;
                    return false;
                }

                long closingRaw2 = checked(-deltaDotWitnessRaw2);
                if (closingRaw2 <= 0 || lengthFloor <= 0)
                    return Hit(
                        currentFraction,
                        desiredDelta,
                        lastNormal,
                        witness,
                        CombatObbCastStatus.PrecisionLimitConservativeHit,
                        out fraction,
                        out distance,
                        out worldNormal,
                        out nonEnteringDirection,
                        out status);

                long closingUpperRaw = DivideCeilPositive(closingRaw2, lengthFloor);
                long advanceRaw = MulScaleDivFloorPositive(advanceGapLower, closingUpperRaw);
                if (advanceRaw <= 0)
                    return Hit(
                        currentFraction,
                        desiredDelta,
                        lastNormal,
                        witness,
                        CombatObbCastStatus.PrecisionLimitConservativeHit,
                        out fraction,
                        out distance,
                        out worldNormal,
                        out nonEnteringDirection,
                        out status);

                if (advanceRaw > remainingRaw)
                    return Invalid(out fraction, out distance, out worldNormal, out status);

                currentFraction = Fix64.FromRaw(checked(currentFraction.RawValue + advanceRaw));
            }

            if (lastNormal.IsZero)
                return Invalid(out fraction, out distance, out worldNormal, out status);

            return Hit(
                currentFraction,
                desiredDelta,
                lastNormal,
                lastWitness,
                CombatObbCastStatus.IterationLimitConservativeHit,
                out fraction,
                out distance,
                out worldNormal,
                out nonEnteringDirection,
                out status);
        }

        private static bool TryProveExpandedNonClosingSeparation(
            FixVector3 segmentA,
            FixVector3 segmentB,
            in CombatPhysicsShape obb,
            FixVector3 direction,
            long thresholdRadiusRaw,
            FixVector3 delta)
        {
            if (direction.IsZero || thresholdRadiusRaw < 0L)
                return false;

            try
            {
                long directionLengthSquaredRaw2 = SquareLengthRaw2Checked(direction);
                long directionLengthCeil = SqrtCeilRaw2(directionLengthSquaredRaw2);
                if (directionLengthCeil <= 0L
                    || !TrySupportGapLowerRaw2(
                        segmentA,
                        segmentB,
                        obb,
                        direction,
                        out long supportGapLowerRaw2))
                {
                    return false;
                }

                long thresholdDirectionUpperRaw2 = checked(
                    thresholdRadiusRaw * directionLengthCeil);
                long expandedGapLowerRaw2 = checked(
                    supportGapLowerRaw2 - thresholdDirectionUpperRaw2);
                return expandedGapLowerRaw2 > 0L
                    && DotRaw2Checked(delta, direction) >= 0L;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        private static bool TryProveExpandedNonClosingObbAxisSeparation(
            FixVector3 segmentA,
            FixVector3 segmentB,
            in CombatPhysicsShape obb,
            long thresholdRadiusRaw,
            FixVector3 delta)
        {
            return TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, obb.AxisX, thresholdRadiusRaw, delta)
                   || TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, -obb.AxisX, thresholdRadiusRaw, delta)
                   || TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, obb.AxisY, thresholdRadiusRaw, delta)
                   || TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, -obb.AxisY, thresholdRadiusRaw, delta)
                   || TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, obb.AxisZ, thresholdRadiusRaw, delta)
                   || TryProveExpandedNonClosingSeparation(
                       segmentA, segmentB, obb, -obb.AxisZ, thresholdRadiusRaw, delta);
        }

        internal static long DotRaw2Checked(FixVector3 left, FixVector3 right)
        {
            return checked(
                checked(left.X.RawValue * right.X.RawValue)
                + checked(left.Y.RawValue * right.Y.RawValue)
                + checked(left.Z.RawValue * right.Z.RawValue));
        }

        internal static long SquareLengthRaw2Checked(FixVector3 value)
        {
            return DotRaw2Checked(value, value);
        }

        internal static long SqrtFloorRaw2(long nonNegativeRaw2)
        {
            if (nonNegativeRaw2 < 0)
                return -1;

            ulong value = (ulong)nonNegativeRaw2;
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value)
                bit >>= 2;

            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return checked((long)result);
        }

        internal static long SqrtCeilRaw2(long nonNegativeRaw2)
        {
            long floor = SqrtFloorRaw2(nonNegativeRaw2);
            if (floor < 0)
                return -1;
            return checked(floor * floor) == nonNegativeRaw2 ? floor : checked(floor + 1);
        }

        internal static long DivideCeilPositive(long numerator, long denominator)
        {
            if (numerator < 0 || denominator <= 0)
                return -1;
            long quotient = numerator / denominator;
            return numerator % denominator == 0 ? quotient : checked(quotient + 1);
        }

        internal static long MulDivFloorSigned(long value, long multiplierNonNegative, long divisorPositive)
        {
            if (multiplierNonNegative < 0 || divisorPositive <= 0)
                return long.MinValue;

            long quotient = value / divisorPositive;
            long remainder = value % divisorPositive;
            long result = checked(quotient * multiplierNonNegative);
            long remainderProduct = checked(remainder * multiplierNonNegative);
            long remainderQuotient = remainderProduct / divisorPositive;
            long remainderRemainder = remainderProduct % divisorPositive;
            result = checked(result + remainderQuotient);
            if (remainderRemainder < 0)
                result = checked(result - 1);
            return result;
        }

        internal static long MulScaleDivFloorPositive(long value, long positiveDivisor)
        {
            if (value < 0 || positiveDivisor <= 0)
                return -1;
            return checked(value * Fix64.Scale) / positiveDivisor;
        }

        private static bool TryNormalizeRawWitness(FixVector3 witness, long distanceSquaredRaw2, out FixVector3 normal)
        {
            long lengthFloor = SqrtFloorRaw2(distanceSquaredRaw2);
            if (lengthFloor <= 0)
            {
                normal = FixVector3.Zero;
                return false;
            }

            normal = new FixVector3(
                Fix64.FromRaw(checked(witness.X.RawValue * Fix64.Scale) / lengthFloor),
                Fix64.FromRaw(checked(witness.Y.RawValue * Fix64.Scale) / lengthFloor),
                Fix64.FromRaw(checked(witness.Z.RawValue * Fix64.Scale) / lengthFloor));
            return !normal.IsZero;
        }

        private static bool TrySupportGapLowerRaw2(
            FixVector3 segmentA,
            FixVector3 segmentB,
            in CombatPhysicsShape obb,
            FixVector3 witnessDirection,
            out long supportGapLowerRaw2)
        {
            FixVector3 relativeA = segmentA - obb.Center;
            FixVector3 relativeB = segmentB - obb.Center;
            if (!WithinWitness(relativeA) || !WithinWitness(relativeB))
            {
                supportGapLowerRaw2 = 0;
                return false;
            }

            long segmentSupportLowerRaw2 = System.Math.Min(
                DotRaw2Checked(relativeA, witnessDirection),
                DotRaw2Checked(relativeB, witnessDirection));
            long boxSupportUpperRaw2 = 0;
            boxSupportUpperRaw2 = checked(boxSupportUpperRaw2 + AxisSupportUpperRaw2(obb.AxisX, obb.HalfExtents.X.RawValue, witnessDirection));
            boxSupportUpperRaw2 = checked(boxSupportUpperRaw2 + AxisSupportUpperRaw2(obb.AxisY, obb.HalfExtents.Y.RawValue, witnessDirection));
            boxSupportUpperRaw2 = checked(boxSupportUpperRaw2 + AxisSupportUpperRaw2(obb.AxisZ, obb.HalfExtents.Z.RawValue, witnessDirection));
            supportGapLowerRaw2 = checked(segmentSupportLowerRaw2 - boxSupportUpperRaw2);
            return true;
        }

        private static long AxisSupportUpperRaw2(FixVector3 axis, long extentRaw, FixVector3 direction)
        {
            long axisDotRaw2 = DotRaw2Checked(axis, direction);
            long absoluteAxisDotRaw2 = axisDotRaw2 < 0 ? checked(-axisDotRaw2) : axisDotRaw2;
            if (absoluteAxisDotRaw2 == 0)
                return 0;

            long floor = MulDivFloorNonNegative(
                absoluteAxisDotRaw2,
                extentRaw,
                Fix64.Scale,
                out long remainder);
            return remainder == 0 ? floor : checked(floor + 1);
        }

        private static bool TryPrepare(
            in CombatMotionCapsuleProxy capsule,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            in CombatPhysicsShape obb,
            out FixVector3 startA,
            out FixVector3 startB)
        {
            startA = FixVector3.Zero;
            startB = FixVector3.Zero;
            if (obb.Kind != CombatPhysicsShapeKind.Obb
                || capsule.Radius <= Fix64.Zero
                || capsule.Radius.RawValue > MaxRadiusOrExtentRaw
                || capsule.Height <= Fix64.Zero
                || capsule.Height.RawValue > checked(MaxRadiusOrExtentRaw * 2)
                || obb.HalfExtents.X <= Fix64.Zero
                || obb.HalfExtents.Y <= Fix64.Zero
                || obb.HalfExtents.Z <= Fix64.Zero
                || obb.HalfExtents.X.RawValue > MaxRadiusOrExtentRaw
                || obb.HalfExtents.Y.RawValue > MaxRadiusOrExtentRaw
                || obb.HalfExtents.Z.RawValue > MaxRadiusOrExtentRaw
                || !WithinWorld(startPosition)
                || !WithinWorld(capsule.Center)
                || !WithinWorld(obb.Center)
                || !WithinDelta(desiredDelta)
                || !WithinAxis(obb.AxisX)
                || !WithinAxis(obb.AxisY)
                || !WithinAxis(obb.AxisZ)
                || !IsValidBasis(obb.AxisX, obb.AxisY, obb.AxisZ))
            {
                return false;
            }

            capsule.GetSegment(startPosition, out startA, out startB);
            FixVector3 endA = startA + desiredDelta;
            FixVector3 endB = startB + desiredDelta;
            return WithinWorld(startA) && WithinWorld(startB) && WithinWorld(endA) && WithinWorld(endB);
        }

        private static bool IsValidBasis(FixVector3 axisX, FixVector3 axisY, FixVector3 axisZ)
        {
            if (!IsUnit(axisX) || !IsUnit(axisY) || !IsUnit(axisZ)
                || AbsRaw(axisX.Dot(axisY).RawValue) > BasisToleranceRaw
                || AbsRaw(axisX.Dot(axisZ).RawValue) > BasisToleranceRaw
                || AbsRaw(axisY.Dot(axisZ).RawValue) > BasisToleranceRaw)
            {
                return false;
            }

            FixVector3 cross = new FixVector3(
                axisX.Y * axisY.Z - axisX.Z * axisY.Y,
                axisX.Z * axisY.X - axisX.X * axisY.Z,
                axisX.X * axisY.Y - axisX.Y * axisY.X);
            return AbsRaw(cross.Dot(axisZ).RawValue - Fix64.Scale) <= BasisToleranceRaw;
        }

        private static bool IsUnit(FixVector3 value)
        {
            if (!value.TryNormalize(out FixVector3 ignored))
                return false;
            long length = SqrtFloorRaw2(SquareLengthRaw2Checked(value));
            return AbsRaw(length - Fix64.Scale) <= BasisToleranceRaw;
        }

        private static bool TryClosestWitness(
            FixVector3 worldA,
            FixVector3 worldB,
            in CombatPhysicsShape obb,
            out FixVector3 segmentWitnessWorld,
            out FixVector3 boxWitnessWorld)
        {
            FixVector3 localA = ToLocal(worldA, obb);
            FixVector3 localB = ToLocal(worldB, obb);
            FixVector3 min = -obb.HalfExtents;
            FixVector3 max = obb.HalfExtents;
            if (!WithinLocal(localA) || !WithinLocal(localB) || !WithinLocal(min) || !WithinLocal(max))
            {
                segmentWitnessWorld = FixVector3.Zero;
                boxWitnessWorld = FixVector3.Zero;
                return false;
            }

            ClosestSegmentAabb(localA, localB, min, max, out FixVector3 localSegmentWitness, out FixVector3 localBoxWitness);
            segmentWitnessWorld = FromLocal(localSegmentWitness, obb);
            boxWitnessWorld = FromLocal(localBoxWitness, obb);
            return WithinWorld(segmentWitnessWorld) && WithinWorld(boxWitnessWorld);
        }

        private static bool TryGetIntersectingNormal(
            FixVector3 worldA,
            FixVector3 worldB,
            Fix64 radius,
            in CombatPhysicsShape obb,
            out FixVector3 normal)
        {
            FixVector3 localA = ToLocal(worldA, obb);
            FixVector3 localB = ToLocal(worldB, obb);
            if (!WithinLocal(localA) || !WithinLocal(localB))
            {
                normal = FixVector3.Zero;
                return false;
            }

            FixVector3 axisXNormal = SelectAxisExitNormal(
                localA.X, localB.X, radius, obb.HalfExtents.X, obb.AxisX, out long depthX);
            FixVector3 axisYNormal = SelectAxisExitNormal(
                localA.Y, localB.Y, radius, obb.HalfExtents.Y, obb.AxisY, out long depthY);
            FixVector3 axisZNormal = SelectAxisExitNormal(
                localA.Z, localB.Z, radius, obb.HalfExtents.Z, obb.AxisZ, out long depthZ);

            normal = axisYNormal;
            long bestDepth = depthY;
            if (depthX < bestDepth || (depthX == bestDepth && IsCanonicalNormalPreferred(axisXNormal, normal)))
            {
                normal = axisXNormal;
                bestDepth = depthX;
            }

            if (depthZ < bestDepth || (depthZ == bestDepth && IsCanonicalNormalPreferred(axisZNormal, normal)))
                normal = axisZNormal;

            return !normal.IsZero;
        }

        private static FixVector3 SelectAxisExitNormal(
            Fix64 endpointA,
            Fix64 endpointB,
            Fix64 radius,
            Fix64 halfExtent,
            FixVector3 axis,
            out long depthRaw)
        {
            Fix64 capsuleMin = Fix64.Min(endpointA, endpointB) - radius;
            Fix64 capsuleMax = Fix64.Max(endpointA, endpointB) + radius;
            long negativeDepthRaw = checked(capsuleMax.RawValue + halfExtent.RawValue);
            long positiveDepthRaw = checked(halfExtent.RawValue - capsuleMin.RawValue);
            if (negativeDepthRaw < positiveDepthRaw)
            {
                depthRaw = negativeDepthRaw;
                return -axis;
            }

            if (positiveDepthRaw < negativeDepthRaw)
            {
                depthRaw = positiveDepthRaw;
                return axis;
            }

            depthRaw = negativeDepthRaw;
            return CanonicalizeNormal(axis);
        }

        private static bool IsCanonicalNormalPreferred(FixVector3 candidate, FixVector3 current)
        {
            FixVector3 left = CanonicalizeNormal(candidate);
            FixVector3 right = CanonicalizeNormal(current);
            long leftY = AbsRaw(left.Y.RawValue);
            long rightY = AbsRaw(right.Y.RawValue);
            if (leftY != rightY)
                return leftY > rightY;

            long leftX = AbsRaw(left.X.RawValue);
            long rightX = AbsRaw(right.X.RawValue);
            if (leftX != rightX)
                return leftX > rightX;

            long leftZ = AbsRaw(left.Z.RawValue);
            long rightZ = AbsRaw(right.Z.RawValue);
            if (leftZ != rightZ)
                return leftZ > rightZ;

            if (left.Y.RawValue != right.Y.RawValue)
                return left.Y.RawValue > right.Y.RawValue;
            if (left.X.RawValue != right.X.RawValue)
                return left.X.RawValue > right.X.RawValue;
            return left.Z.RawValue > right.Z.RawValue;
        }

        private static FixVector3 CanonicalizeNormal(FixVector3 value)
        {
            long decidingComponent = value.Y.RawValue != 0
                ? value.Y.RawValue
                : value.X.RawValue != 0
                    ? value.X.RawValue
                    : value.Z.RawValue;
            return decidingComponent < 0 ? -value : value;
        }

        private static void ClosestSegmentAabb(
            FixVector3 pointA,
            FixVector3 pointB,
            FixVector3 min,
            FixVector3 max,
            out FixVector3 segmentWitness,
            out FixVector3 boxWitness)
        {
            segmentWitness = FixVector3.Zero;
            boxWitness = FixVector3.Zero;
            long bestDistanceRaw2 = long.MaxValue;

            // Frozen feature order: faces Y-/Y+, X-/X+, Z-/Z+.
            CheckFace(pointA, pointB, min, max, 1, min.Y, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckFace(pointA, pointB, min, max, 1, max.Y, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckFace(pointA, pointB, min, max, 0, min.X, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckFace(pointA, pointB, min, max, 0, max.X, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckFace(pointA, pointB, min, max, 2, min.Z, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckFace(pointA, pointB, min, max, 2, max.Z, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);

            // X-parallel, Y-parallel, Z-parallel edges; signs are lexicographic.
            CheckEdge(pointA, pointB, V(min.X, min.Y, min.Z), V(max.X, min.Y, min.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, min.Y, max.Z), V(max.X, min.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, max.Y, min.Z), V(max.X, max.Y, min.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, max.Y, max.Z), V(max.X, max.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, min.Y, min.Z), V(min.X, max.Y, min.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, min.Y, max.Z), V(min.X, max.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(max.X, min.Y, min.Z), V(max.X, max.Y, min.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(max.X, min.Y, max.Z), V(max.X, max.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, min.Y, min.Z), V(min.X, min.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(min.X, max.Y, min.Z), V(min.X, max.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(max.X, min.Y, min.Z), V(max.X, min.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEdge(pointA, pointB, V(max.X, max.Y, min.Z), V(max.X, max.Y, max.Z), ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);

            CheckEndpoint(pointA, min, max, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
            CheckEndpoint(pointB, min, max, ref bestDistanceRaw2, ref segmentWitness, ref boxWitness);
        }

        private static void CheckFace(
            FixVector3 a,
            FixVector3 b,
            FixVector3 min,
            FixVector3 max,
            int axis,
            Fix64 face,
            ref long bestDistanceRaw2,
            ref FixVector3 bestSegment,
            ref FixVector3 bestBox)
        {
            FixVector3 direction = b - a;
            Fix64 lower = Fix64.Zero;
            Fix64 upper = Fix64.One;
            int tangentA = axis == 0 ? 1 : 0;
            int tangentB = axis == 2 ? 1 : 2;
            if (axis == 1)
                tangentB = 2;
            if (!ClipParameter(Component(a, tangentA), Component(direction, tangentA), Component(min, tangentA), Component(max, tangentA), ref lower, ref upper)
                || !ClipParameter(Component(a, tangentB), Component(direction, tangentB), Component(min, tangentB), Component(max, tangentB), ref lower, ref upper))
            {
                return;
            }

            Fix64 axisDirection = Component(direction, axis);
            Fix64 t = axisDirection.IsZero
                ? lower
                : Fix64.Clamp((face - Component(a, axis)) / axisDirection, lower, upper);
            FixVector3 segment = a + direction * t;
            FixVector3 box = SetComponent(segment, axis, face);
            CheckCandidate(segment, box, ref bestDistanceRaw2, ref bestSegment, ref bestBox);
        }

        private static bool ClipParameter(Fix64 origin, Fix64 direction, Fix64 min, Fix64 max, ref Fix64 lower, ref Fix64 upper)
        {
            if (direction.IsZero)
                return origin >= min && origin <= max;
            Fix64 first = (min - origin) / direction;
            Fix64 second = (max - origin) / direction;
            if (first > second)
            {
                Fix64 swap = first;
                first = second;
                second = swap;
            }
            lower = Fix64.Max(lower, first);
            upper = Fix64.Min(upper, second);
            return lower <= upper;
        }

        private static void CheckEdge(
            FixVector3 segmentA,
            FixVector3 segmentB,
            FixVector3 edgeA,
            FixVector3 edgeB,
            ref long bestDistanceRaw2,
            ref FixVector3 bestSegment,
            ref FixVector3 bestBox)
        {
            FixVector3 direction = segmentB - segmentA;
            int edgeAxis = edgeA.X != edgeB.X ? 0 : edgeA.Y != edgeB.Y ? 1 : 2;
            Fix64 lower = Fix64.Zero;
            Fix64 upper = Fix64.One;
            if (ClipParameter(
                Component(segmentA, edgeAxis),
                Component(direction, edgeAxis),
                Component(edgeA, edgeAxis),
                Component(edgeB, edgeAxis),
                ref lower,
                ref upper))
            {
                int firstPerpendicular = edgeAxis == 0 ? 1 : 0;
                int secondPerpendicular = edgeAxis == 2 ? 1 : 2;
                if (edgeAxis == 1)
                    secondPerpendicular = 2;

                long firstDirectionRaw = Component(direction, firstPerpendicular).RawValue;
                long secondDirectionRaw = Component(direction, secondPerpendicular).RawValue;
                long denominatorRaw2 = checked(
                    checked(firstDirectionRaw * firstDirectionRaw)
                    + checked(secondDirectionRaw * secondDirectionRaw));
                long tRaw = lower.RawValue;
                if (denominatorRaw2 > 0)
                {
                    long firstRelativeRaw = checked(
                        Component(segmentA, firstPerpendicular).RawValue
                        - Component(edgeA, firstPerpendicular).RawValue);
                    long secondRelativeRaw = checked(
                        Component(segmentA, secondPerpendicular).RawValue
                        - Component(edgeA, secondPerpendicular).RawValue);
                    long derivativeHalfRaw2 = checked(
                        checked(firstRelativeRaw * firstDirectionRaw)
                        + checked(secondRelativeRaw * secondDirectionRaw));
                    if (derivativeHalfRaw2 < 0)
                    {
                        long projectedRaw = MulDivFloorNonNegative(
                            checked(-derivativeHalfRaw2),
                            Fix64.Scale,
                            denominatorRaw2);
                        tRaw = ClampRaw(projectedRaw, lower.RawValue, upper.RawValue);
                    }
                }

                Fix64 t = Fix64.FromRaw(tRaw);
                FixVector3 segment = segmentA + direction * t;
                Fix64 edgeCoordinate = Fix64.Clamp(
                    Component(segment, edgeAxis),
                    Component(edgeA, edgeAxis),
                    Component(edgeB, edgeAxis));
                FixVector3 box = SetComponent(edgeA, edgeAxis, edgeCoordinate);
                CheckCandidate(segment, box, ref bestDistanceRaw2, ref bestSegment, ref bestBox);
            }

            // Edge endpoints are vertices. Their closest points may lie in the segment interior.
            CheckPointSegment(edgeA, segmentA, segmentB, ref bestDistanceRaw2, ref bestSegment, ref bestBox);
            CheckPointSegment(edgeB, segmentA, segmentB, ref bestDistanceRaw2, ref bestSegment, ref bestBox);
        }

        private static void CheckPointSegment(
            FixVector3 point,
            FixVector3 segmentA,
            FixVector3 segmentB,
            ref long bestDistanceRaw2,
            ref FixVector3 bestSegment,
            ref FixVector3 bestBox)
        {
            FixVector3 direction = segmentB - segmentA;
            long denominatorRaw2 = SquareLengthRaw2Checked(direction);
            long tRaw = 0;
            if (denominatorRaw2 > 0)
            {
                FixVector3 relative = point - segmentA;
                long numeratorRaw2 = DotRaw2Checked(relative, direction);
                if (numeratorRaw2 > 0)
                {
                    tRaw = ClampRaw(
                        MulDivFloorNonNegative(numeratorRaw2, Fix64.Scale, denominatorRaw2),
                        0,
                        Fix64.Scale);
                }
            }

            FixVector3 segment = segmentA + direction * Fix64.FromRaw(tRaw);
            CheckCandidate(segment, point, ref bestDistanceRaw2, ref bestSegment, ref bestBox);
        }

        // Exact floor(value * multiplier / divisor) without forming the potentially overflowing product.
        private static long MulDivFloorNonNegative(long value, long multiplier, long divisor)
        {
            return MulDivFloorNonNegative(value, multiplier, divisor, out _);
        }

        private static long MulDivFloorNonNegative(
            long value,
            long multiplier,
            long divisor,
            out long productRemainder)
        {
            if (value < 0 || multiplier < 0 || divisor <= 0)
            {
                productRemainder = 0;
                return -1;
            }

            long quotient = value / divisor;
            long remainder = value % divisor;
            long result = checked(quotient * multiplier);
            long fractionalQuotient = 0;
            long fractionalRemainder = 0;
            long bit = HighestPowerOfTwoAtMost(multiplier);
            while (bit != 0)
            {
                DoubleDivRem(fractionalRemainder, divisor, out long doubledCarry, out fractionalRemainder);
                fractionalQuotient = checked(fractionalQuotient * 2 + doubledCarry);
                if ((multiplier & bit) != 0)
                {
                    AddDivRem(fractionalRemainder, remainder, divisor, out long addCarry, out fractionalRemainder);
                    fractionalQuotient = checked(fractionalQuotient + addCarry);
                }

                bit >>= 1;
            }

            productRemainder = fractionalRemainder;
            return checked(result + fractionalQuotient);
        }

        private static long HighestPowerOfTwoAtMost(long value)
        {
            long bit = 1;
            while (bit <= value / 2)
                bit <<= 1;
            return bit;
        }

        private static void DoubleDivRem(long value, long divisor, out long carry, out long remainder)
        {
            if (value >= divisor - value)
            {
                carry = 1;
                remainder = value - (divisor - value);
            }
            else
            {
                carry = 0;
                remainder = value + value;
            }
        }

        private static void AddDivRem(long left, long right, long divisor, out long carry, out long remainder)
        {
            if (left >= divisor - right)
            {
                carry = 1;
                remainder = left - (divisor - right);
            }
            else
            {
                carry = 0;
                remainder = left + right;
            }
        }

        private static long ClampRaw(long value, long min, long max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static void CheckEndpoint(
            FixVector3 point,
            FixVector3 min,
            FixVector3 max,
            ref long bestDistanceRaw2,
            ref FixVector3 bestSegment,
            ref FixVector3 bestBox)
        {
            CheckCandidate(point, ClampPoint(point, min, max), ref bestDistanceRaw2, ref bestSegment, ref bestBox);
        }

        private static void CheckCandidate(
            FixVector3 segment,
            FixVector3 box,
            ref long bestDistanceRaw2,
            ref FixVector3 bestSegment,
            ref FixVector3 bestBox)
        {
            long distanceRaw2 = SquareLengthRaw2Checked(segment - box);
            if (distanceRaw2 < bestDistanceRaw2)
            {
                bestDistanceRaw2 = distanceRaw2;
                bestSegment = segment;
                bestBox = box;
            }
        }

        private static FixVector3 ToLocal(FixVector3 point, in CombatPhysicsShape obb)
        {
            FixVector3 relative = point - obb.Center;
            return new FixVector3(relative.Dot(obb.AxisX), relative.Dot(obb.AxisY), relative.Dot(obb.AxisZ));
        }

        private static FixVector3 FromLocal(FixVector3 point, in CombatPhysicsShape obb)
        {
            return obb.Center + obb.AxisX * point.X + obb.AxisY * point.Y + obb.AxisZ * point.Z;
        }

        private static FixVector3 ClampPoint(FixVector3 point, FixVector3 min, FixVector3 max)
        {
            return new FixVector3(
                Fix64.Clamp(point.X, min.X, max.X),
                Fix64.Clamp(point.Y, min.Y, max.Y),
                Fix64.Clamp(point.Z, min.Z, max.Z));
        }

        private static FixVector3 V(Fix64 x, Fix64 y, Fix64 z) => new FixVector3(x, y, z);

        private static Fix64 Component(FixVector3 value, int axis)
        {
            return axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
        }

        private static FixVector3 SetComponent(FixVector3 value, int axis, Fix64 component)
        {
            return axis == 0
                ? new FixVector3(component, value.Y, value.Z)
                : axis == 1
                    ? new FixVector3(value.X, component, value.Z)
                    : new FixVector3(value.X, value.Y, component);
        }

        private static bool Hit(
            Fix64 hitFraction,
            FixVector3 delta,
            FixVector3 normal,
            FixVector3 hitNonEnteringDirection,
            CombatObbCastStatus hitStatus,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 worldNormal,
            out FixVector3 nonEnteringDirection,
            out CombatObbCastStatus status)
        {
            fraction = hitFraction;
            distance = delta.LengthSquared().Sqrt() * hitFraction;
            worldNormal = normal;
            nonEnteringDirection = hitNonEnteringDirection;
            status = hitStatus;
            return true;
        }

        private static bool Invalid(out Fix64 fraction, out Fix64 distance, out FixVector3 normal, out CombatObbCastStatus status)
        {
            Reset(out fraction, out distance, out normal);
            status = CombatObbCastStatus.InvalidInput;
            return false;
        }

        private static void Reset(out Fix64 fraction, out Fix64 distance, out FixVector3 normal)
        {
            fraction = Fix64.Zero;
            distance = Fix64.Zero;
            normal = FixVector3.Zero;
        }

        private static bool WithinWorld(FixVector3 value) => Within(value, MaxWorldCoordinateAbsRaw);
        private static bool WithinLocal(FixVector3 value) => Within(value, MaxLocalCoordinateAbsRaw);
        private static bool WithinDelta(FixVector3 value) => Within(value, MaxDeltaComponentAbsRaw);
        private static bool WithinWitness(FixVector3 value) => Within(value, MaxWitnessComponentAbsRaw);
        private static bool WithinAxis(FixVector3 value) => Within(value, Fix64.Scale + BasisToleranceRaw);

        private static bool Within(FixVector3 value, long maximum)
        {
            return Within(value.X.RawValue, maximum)
                && Within(value.Y.RawValue, maximum)
                && Within(value.Z.RawValue, maximum);
        }

        private static bool Within(long value, long maximum)
        {
            return value != long.MinValue && AbsRaw(value) <= maximum;
        }

        private static long AbsRaw(long value) => value < 0 ? -value : value;
    }
}
