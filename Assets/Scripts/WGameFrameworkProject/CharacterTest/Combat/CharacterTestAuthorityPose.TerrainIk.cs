using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestAuthorityPoseBaseSample
    {
        internal CharacterTestAuthorityPoseBaseSample(
            int ordinal,
            int channelId,
            FixVector3 position,
            CharacterTestResolvedAuthorityPoseBasis basis)
        {
            Ordinal = ordinal;
            ChannelId = channelId;
            Position = position;
            Basis = basis;
        }

        internal int Ordinal { get; }
        internal int ChannelId { get; }
        internal FixVector3 Position { get; }
        internal CharacterTestResolvedAuthorityPoseBasis Basis { get; }
    }

    /// <summary>Fixed-point math used only by the CharacterTest terrain IK producer.</summary>
    internal static class CharacterTestAuthorityPoseTerrainMath
    {
        internal static FixVector3 TransformByBasis(
            in CharacterTestResolvedAuthorityPoseBasis basis,
            in FixVector3 local)
        {
            return (basis.Right * local.X)
                + (basis.Up * local.Y)
                + (basis.Forward * local.Z);
        }

        internal static FixVector3 ModelPointToWorld(
            in FixVector3 modelPoint,
            in CharacterTestCharacterTransformComponent root)
        {
            return RootPosition(root) + RotateYaw(modelPoint, root.YawMilliDegrees);
        }

        internal static FixVector3 ModelDirectionToWorld(
            in FixVector3 modelDirection,
            int rootYawMilliDegrees)
        {
            return RotateYaw(modelDirection, rootYawMilliDegrees);
        }

        internal static FixVector3 WorldPointToModel(
            in FixVector3 worldPoint,
            in CharacterTestCharacterTransformComponent root)
        {
            return RotateYaw(worldPoint - RootPosition(root), -root.YawMilliDegrees);
        }

        internal static FixVector3 WorldDirectionToModel(
            in FixVector3 worldDirection,
            int rootYawMilliDegrees)
        {
            return RotateYaw(worldDirection, -rootYawMilliDegrees);
        }

        internal static bool TryCreateChainBasis(
            in FixVector3 from,
            in FixVector3 to,
            in FixVector3 preferredUp,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            basis = default;
            if (!(to - from).TryNormalize(out FixVector3 forward))
                return false;

            FixVector3 planarUp = preferredUp - (forward * preferredUp.Dot(forward));
            if (!planarUp.TryNormalize(out FixVector3 up))
                return false;
            FixVector3 right = Cross(up, forward);
            if (!right.TryNormalize(out right))
                return false;
            up = Cross(forward, right);
            if (!up.TryNormalize(out up))
                return false;
            basis = new CharacterTestResolvedAuthorityPoseBasis(right, up, forward);
            return true;
        }

        internal static bool TryBlendBasis(
            in CharacterTestResolvedAuthorityPoseBasis baseBasis,
            in CharacterTestResolvedAuthorityPoseBasis overlayBasis,
            Fix64 weight,
            out CharacterTestResolvedAuthorityPoseBasis result)
        {
            Fix64 clampedWeight = Fix64.Clamp(weight, Fix64.Zero, Fix64.One);
            Fix64 inverse = Fix64.One - clampedWeight;
            FixVector3 forward = (baseBasis.Forward * inverse)
                + (overlayBasis.Forward * clampedWeight);
            FixVector3 up = (baseBasis.Up * inverse)
                + (overlayBasis.Up * clampedWeight);
            return TryCreateChainBasis(
                FixVector3.Zero,
                forward,
                up,
                out result);
        }

        internal static FixVector3 Lerp(
            in FixVector3 from,
            in FixVector3 to,
            Fix64 weight)
        {
            Fix64 clampedWeight = Fix64.Clamp(weight, Fix64.Zero, Fix64.One);
            return from + ((to - from) * clampedWeight);
        }

        internal static FixVector3 Cross(in FixVector3 left, in FixVector3 right)
        {
            return new FixVector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        private static FixVector3 RootPosition(in CharacterTestCharacterTransformComponent root)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.YMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.ZMillimeters));
        }

        private static FixVector3 RotateYaw(in FixVector3 value, int yawMilliDegrees)
        {
            CharacterTestDeterministicMath.SinCosMilliDegrees(
                yawMilliDegrees,
                out int sinScaled,
                out int cosScaled);
            Fix64 sin = Fix64.FromRaw(
                (long)sinScaled * Fix64.Scale / CharacterTestDeterministicMath.TrigScale);
            Fix64 cos = Fix64.FromRaw(
                (long)cosScaled * Fix64.Scale / CharacterTestDeterministicMath.TrigScale);
            return new FixVector3(
                value.X * cos + value.Z * sin,
                value.Y,
                -value.X * sin + value.Z * cos);
        }
    }
}
