using MxFramework.Animation;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>
    /// Shared authority-pose basis math validation. Used by both the binary v3 reader and the
    /// legacy JSON v2 test-side reader so the two formats cannot drift apart in tolerance.
    /// </summary>
    public static class CharacterSocketBakeBasisValidation
    {
        /// <summary>
        /// Quantized basis components must stay inside +/- the quantization scale.
        /// </summary>
        public static bool IsComponentInRange(long component, int quantizationScale)
        {
            if (quantizationScale <= 0)
                return false;
            long limit = quantizationScale;
            return component >= -limit && component <= limit;
        }

        /// <summary>
        /// Validates length, orthogonality and handedness of a quantized basis frame.
        /// Returns an empty string when valid, otherwise the failing constraint name.
        /// </summary>
        public static string ValidateFrame(
            MxAnimationBakedVector3 right,
            MxAnimationBakedVector3 up,
            MxAnimationBakedVector3 forward,
            int quantizationScale)
        {
            if (quantizationScale <= 0)
                return "quantization scale";

            double inverseScale = 1d / quantizationScale;
            double rightX = right.X * inverseScale;
            double rightY = right.Y * inverseScale;
            double rightZ = right.Z * inverseScale;
            double upX = up.X * inverseScale;
            double upY = up.Y * inverseScale;
            double upZ = up.Z * inverseScale;
            double forwardX = forward.X * inverseScale;
            double forwardY = forward.Y * inverseScale;
            double forwardZ = forward.Z * inverseScale;
            double tolerance = System.Math.Min(0.05d, 8d * inverseScale);
            double rightLengthSquared = rightX * rightX + rightY * rightY + rightZ * rightZ;
            double upLengthSquared = upX * upX + upY * upY + upZ * upZ;
            double forwardLengthSquared = forwardX * forwardX + forwardY * forwardY + forwardZ * forwardZ;
            if (System.Math.Abs(rightLengthSquared - 1d) > tolerance
                || System.Math.Abs(upLengthSquared - 1d) > tolerance
                || System.Math.Abs(forwardLengthSquared - 1d) > tolerance)
                return "vector length";
            double rightUpDot = rightX * upX + rightY * upY + rightZ * upZ;
            double rightForwardDot = rightX * forwardX + rightY * forwardY + rightZ * forwardZ;
            double upForwardDot = upX * forwardX + upY * forwardY + upZ * forwardZ;
            if (System.Math.Abs(rightUpDot) > tolerance
                || System.Math.Abs(rightForwardDot) > tolerance
                || System.Math.Abs(upForwardDot) > tolerance)
                return "orthogonality";
            double crossX = rightY * upZ - rightZ * upY;
            double crossY = rightZ * upX - rightX * upZ;
            double crossZ = rightX * upY - rightY * upX;
            double handedness = crossX * forwardX + crossY * forwardY + crossZ * forwardZ;
            return handedness > 0d ? string.Empty : "handedness";
        }
    }
}
