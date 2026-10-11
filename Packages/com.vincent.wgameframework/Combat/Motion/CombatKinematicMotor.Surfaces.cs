using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private const int MaxRawCorrectionSearchSteps = 64;

        internal CombatMotionCollisionFlags ClassifyCollision(FixVector3 normal)
        {
            if (normal.Y > Fix64.Zero && normal.Y >= Config.GroundMinNormalY)
            {
                return CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.BlockedY;
            }

            if (normal.Y > Fix64.Zero)
            {
                CombatMotionCollisionFlags steep = CombatMotionCollisionFlags.SteepSlope
                    | CombatMotionCollisionFlags.Wall
                    | CombatMotionCollisionFlags.BlockedY;
                if (!normal.X.IsZero)
                    steep |= CombatMotionCollisionFlags.BlockedX;
                if (!normal.Z.IsZero)
                    steep |= CombatMotionCollisionFlags.BlockedZ;
                return steep;
            }

            if (normal.Y < Fix64.Zero && normal.Y <= -Config.CeilingMinNormalY)
            {
                return CombatMotionCollisionFlags.Ceiling | CombatMotionCollisionFlags.BlockedY;
            }

            CombatMotionCollisionFlags wall = CombatMotionCollisionFlags.Wall;
            if (!normal.X.IsZero)
                wall |= CombatMotionCollisionFlags.BlockedX;
            if (!normal.Y.IsZero)
                wall |= CombatMotionCollisionFlags.BlockedY;
            if (!normal.Z.IsZero)
                wall |= CombatMotionCollisionFlags.BlockedZ;
            return wall;
        }

        private static FixVector3 ResolveRemainingDelta(
            FixVector3 value,
            FixVector3 normal,
            FixVector3 nonEnteringDirection,
            CombatMotionCollisionFlags flags,
            Fix64 wallSlideMinimumHorizontalDelta)
        {
            if (nonEnteringDirection.IsZero)
                nonEnteringDirection = normal;
            if ((flags & CombatMotionCollisionFlags.Grounded) != 0)
                return ProjectWalkableRemaining(value, normal, nonEnteringDirection);
            if ((flags & CombatMotionCollisionFlags.SteepSlope) != 0)
                return ProjectSteepRemaining(value, normal, nonEnteringDirection);
            if ((flags & CombatMotionCollisionFlags.Wall) != 0)
            {
                return ProjectWallRemaining(
                    value,
                    normal,
                    nonEnteringDirection,
                    wallSlideMinimumHorizontalDelta);
            }
            return ProjectOnPlane(value, normal);
        }

        internal static FixVector3 ProjectWallRemaining(
            FixVector3 value,
            FixVector3 normal,
            FixVector3 nonEnteringDirection,
            Fix64 minimumHorizontalDelta)
        {
            try
            {
                FixVector3 projected = ProjectOnPlane(value, normal);
                FixVector3 corrected = CorrectRawNonEnteringDual(
                    projected,
                    normal,
                    normal,
                    nonEnteringDirection,
                    requireNonPositiveY: false);
                if (minimumHorizontalDelta <= Fix64.Zero)
                    return corrected;

                FixVector3 horizontal = new FixVector3(
                    corrected.X,
                    Fix64.Zero,
                    corrected.Z);
                Fix64 horizontalLengthSquared = horizontal.LengthSquared();
                if (horizontalLengthSquared.IsZero)
                    return corrected;

                Fix64 horizontalLength = horizontalLengthSquared.Sqrt();
                if (horizontalLength >= minimumHorizontalDelta)
                    return corrected;

                Fix64 scale = minimumHorizontalDelta / horizontalLength;
                FixVector3 scaled = new FixVector3(
                    corrected.X * scale,
                    corrected.Y,
                    corrected.Z * scale);
                return CorrectRawNonEnteringDual(
                    scaled,
                    normal,
                    normal,
                    nonEnteringDirection,
                    requireNonPositiveY: false);
            }
            catch (OverflowException)
            {
                return FixVector3.Zero;
            }
        }

        internal static FixVector3 ProjectWalkableRemaining(FixVector3 value, FixVector3 normal)
        {
            return ProjectWalkableRemaining(value, normal, normal);
        }

        internal static FixVector3 ProjectWalkableRemaining(
            FixVector3 value,
            FixVector3 normal,
            FixVector3 nonEnteringDirection)
        {
            try
            {
                FixVector3 projected = ProjectOnPlane(value, normal);
                return CorrectRawNonEnteringDual(
                    projected,
                    normal,
                    normal,
                    nonEnteringDirection,
                    requireNonPositiveY: false);
            }
            catch (OverflowException)
            {
                return FixVector3.Zero;
            }
        }

        internal static FixVector3 ProjectSteepRemaining(FixVector3 value, FixVector3 normal)
        {
            return ProjectSteepRemaining(value, normal, normal);
        }

        internal static FixVector3 ProjectSteepRemaining(
            FixVector3 value,
            FixVector3 normal,
            FixVector3 nonEnteringDirection)
        {
            try
            {
                FixVector3 projected = ProjectOnPlane(value, normal);
                if (projected.Y <= Fix64.Zero)
                {
                    return CorrectRawNonEnteringDual(
                        projected,
                        normal,
                        normal,
                        nonEnteringDirection,
                        requireNonPositiveY: true);
                }

                FixVector3 horizontalNormal = new FixVector3(normal.X, Fix64.Zero, normal.Z);
                if (!TryRawDotNumerator(horizontalNormal, horizontalNormal, out long horizontalLengthRaw2)
                    || horizontalLengthRaw2 <= 0L)
                {
                    return FixVector3.Zero;
                }

                FixVector3 constrained = new FixVector3(projected.X, Fix64.Zero, projected.Z);
                if (constrained.Dot(horizontalNormal) < Fix64.Zero)
                {
                    Fix64 ratio = constrained.Dot(horizontalNormal) / horizontalNormal.Dot(horizontalNormal);
                    constrained -= horizontalNormal * ratio;
                }

                return CorrectRawNonEnteringDual(
                    constrained,
                    horizontalNormal,
                    normal,
                    nonEnteringDirection,
                    requireNonPositiveY: true);
            }
            catch (OverflowException)
            {
                return FixVector3.Zero;
            }
        }

        internal static bool TryRawDotNumerator(FixVector3 left, FixVector3 right, out long numeratorRaw2)
        {
            numeratorRaw2 = 0L;
            try
            {
                numeratorRaw2 = checked(
                    checked(left.X.RawValue * right.X.RawValue)
                    + checked(left.Y.RawValue * right.Y.RawValue));
                numeratorRaw2 = checked(numeratorRaw2 + checked(left.Z.RawValue * right.Z.RawValue));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static FixVector3 CorrectRawNonEntering(
            FixVector3 candidate,
            FixVector3 correctionDirection,
            FixVector3 surfaceNormal,
            bool requireNonPositiveY)
        {
            return CorrectRawNonEnteringCore(
                candidate,
                correctionDirection,
                surfaceNormal,
                requireNonPositiveY,
                out _);
        }

        private static FixVector3 CorrectRawNonEnteringDual(
            FixVector3 candidate,
            FixVector3 correctionDirection,
            FixVector3 classificationNormal,
            FixVector3 nonEnteringDirection,
            bool requireNonPositiveY)
        {
            FixVector3 corrected = CorrectRawNonEntering(
                candidate,
                correctionDirection,
                classificationNormal,
                requireNonPositiveY);
            corrected = CorrectRawNonEntering(
                corrected,
                correctionDirection,
                nonEnteringDirection,
                requireNonPositiveY);
            if (!TryRawDotNumerator(corrected, classificationNormal, out long classificationDotRaw2)
                || classificationDotRaw2 < 0L
                || !TryRawDotNumerator(corrected, nonEnteringDirection, out long nonEnteringDotRaw2)
                || nonEnteringDotRaw2 < 0L
                || (requireNonPositiveY && corrected.Y > Fix64.Zero))
            {
                return FixVector3.Zero;
            }

            return corrected;
        }

        internal static FixVector3 CorrectRawNonEnteringForTests(
            FixVector3 candidate,
            FixVector3 correctionDirection,
            FixVector3 surfaceNormal,
            bool requireNonPositiveY,
            out long correctionRaw)
        {
            return CorrectRawNonEnteringCore(
                candidate,
                correctionDirection,
                surfaceNormal,
                requireNonPositiveY,
                out correctionRaw);
        }

        private static FixVector3 CorrectRawNonEnteringCore(
            FixVector3 candidate,
            FixVector3 correctionDirection,
            FixVector3 surfaceNormal,
            bool requireNonPositiveY,
            out long selectedCorrectionRaw)
        {
            selectedCorrectionRaw = 0L;
            if (!TryRawDotNumerator(candidate, surfaceNormal, out long candidateDotRaw2))
                return FixVector3.Zero;
            if (candidateDotRaw2 >= 0L)
                return !requireNonPositiveY || candidate.Y <= Fix64.Zero ? candidate : FixVector3.Zero;
            if (!TryRawDotNumerator(correctionDirection, surfaceNormal, out long correctionDotRaw2)
                || correctionDotRaw2 <= 0L
                || candidateDotRaw2 == long.MinValue)
            {
                return FixVector3.Zero;
            }

            long correctionRaw;
            try
            {
                correctionRaw = MulDivCeilNonNegative(
                    -candidateDotRaw2,
                    Fix64.Scale,
                    correctionDotRaw2);
            }
            catch (OverflowException)
            {
                return FixVector3.Zero;
            }

            if (correctionRaw <= 0L)
                correctionRaw = 1L;
            if (!TryBuildCorrected(candidate, correctionDirection, correctionRaw, surfaceNormal, out FixVector3 corrected))
            {
                long high = correctionRaw;
                bool found = false;
                for (int i = 0; i < MaxRawCorrectionSearchSteps && high <= long.MaxValue / 2L; i++)
                {
                    high *= 2L;
                    if (TryBuildCorrected(candidate, correctionDirection, high, surfaceNormal, out corrected))
                    {
                        correctionRaw = high;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return FixVector3.Zero;
            }

            long low = 0L;
            long highBound = correctionRaw;
            for (int i = 0; i < MaxRawCorrectionSearchSteps && low < highBound; i++)
            {
                long middle = low + ((highBound - low) / 2L);
                if (TryBuildCorrected(candidate, correctionDirection, middle, surfaceNormal, out _))
                    highBound = middle;
                else
                    low = middle + 1L;
            }

            if (!TryBuildCorrected(candidate, correctionDirection, low, surfaceNormal, out corrected))
                return FixVector3.Zero;
            if (requireNonPositiveY && corrected.Y > Fix64.Zero)
                return FixVector3.Zero;
            selectedCorrectionRaw = low;
            return corrected;
        }

        private static bool TryBuildCorrected(
            FixVector3 candidate,
            FixVector3 correctionDirection,
            long correctionRaw,
            FixVector3 surfaceNormal,
            out FixVector3 corrected)
        {
            corrected = FixVector3.Zero;
            try
            {
                Fix64 correction = Fix64.FromRaw(correctionRaw);
                corrected = candidate + correctionDirection * correction;
                return TryRawDotNumerator(corrected, surfaceNormal, out long correctedDotRaw2)
                    && correctedDotRaw2 >= 0L;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static long MulDivCeilNonNegative(long value, long multiplier, long divisor)
        {
            if (value < 0L || multiplier < 0L || divisor <= 0L)
                throw new OverflowException();

            long quotient = value / divisor;
            long remainder = value % divisor;
            long result = checked(quotient * multiplier);
            long fractionalQuotient = 0L;
            long fractionalRemainder = 0L;
            long bit = HighestPowerOfTwoAtMost(multiplier);
            while (bit != 0L)
            {
                DoubleDivRem(fractionalRemainder, divisor, out long doubledCarry, out fractionalRemainder);
                fractionalQuotient = checked(fractionalQuotient * 2L + doubledCarry);
                if ((multiplier & bit) != 0L)
                {
                    AddDivRem(fractionalRemainder, remainder, divisor, out long addCarry, out fractionalRemainder);
                    fractionalQuotient = checked(fractionalQuotient + addCarry);
                }

                bit >>= 1;
            }

            result = checked(result + fractionalQuotient);
            return fractionalRemainder == 0L ? result : checked(result + 1L);
        }

        private static long HighestPowerOfTwoAtMost(long value)
        {
            if (value <= 0L)
                return 0L;
            long bit = 1L;
            while (bit <= value / 2L)
                bit <<= 1;
            return bit;
        }

        private static void DoubleDivRem(long value, long divisor, out long carry, out long remainder)
        {
            if (value >= divisor - value)
            {
                carry = 1L;
                remainder = value - (divisor - value);
                return;
            }

            carry = 0L;
            remainder = value + value;
        }

        private static void AddDivRem(long left, long right, long divisor, out long carry, out long remainder)
        {
            if (left >= divisor - right)
            {
                carry = 1L;
                remainder = left - (divisor - right);
                return;
            }

            carry = 0L;
            remainder = left + right;
        }
    }
}
