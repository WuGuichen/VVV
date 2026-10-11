using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 碰撞体几何校验与无缺陷规范化。对应规范 v1.4 第 3.2 节。
    /// </summary>
    /// <remarks>
    /// <para>调用契约（规范未显式声明，此处显式固定）：必须先
    /// <see cref="ValidateRawCollider"/>，通过后才能调用
    /// <see cref="NormalizeValidCollider"/>。规范化不做任何校验，
    /// 传入未校验数据将抛出诊断异常而非产生静默错误结果。
    /// 需要一步完成时使用 <see cref="ValidateAndNormalize"/>。</para>
    /// <para>
    /// 实现说明：规范第 3.2 节的参考代码使用了 <c>UnityEngine.Vector3</c>，
    /// 这会破坏第 6.1 节所要求并要由 <c>MxFramework.Authoring.Core</c> 自身遵守的
    /// noEngine 边界。此处一律使用既有的 noEngine 类型
    /// <see cref="CharacterAuthoringVector3"/>。
    /// </para>
    /// </remarks>
    public static class CharacterColliderValidatorAndNormalizer
    {
        /// <summary>全长边界下限：5 毫米。</summary>
        public const float MinDimension = 0.005f;

        /// <summary>全长边界上限：10 米。</summary>
        public const float MaxDimension = 10.0f;

        /// <summary>半径边界下限：2.5 毫米。</summary>
        public const float MinRadius = MinDimension * 0.5f;

        /// <summary>半径边界上限：5 米。</summary>
        public const float MaxRadius = MaxDimension * 0.5f;

        /// <summary>遗留 <c>size</c> 与权威几何的允许偏差：10 毫米。</summary>
        public const float LegacySizeTolerance = 0.01f;

        /// <summary>缩放单位化容差。</summary>
        public const float UnitScaleTolerance = 1e-4f;

        /// <summary>四元数平方范数与 1 的允许偏差。</summary>
        public const float RotationSqrNormTolerance = 1e-3f;

        public static void ValidateRawCollider(in RawColliderDto collider, string objectPath)
        {
            if (collider == null)
            {
                throw new CharacterDiagnosticException(objectPath, "Collider object cannot be null.");
            }

            ValidatePose(collider.LocalPose, objectPath);

            switch (collider.Shape)
            {
                case CharacterColliderShape.Box:
                    if (collider.Size == null)
                    {
                        throw new CharacterDiagnosticException(
                            objectPath, "Box collider must specify 'size'.", CharacterGateCodes.GeometryOutOfRange, field: "size");
                    }

                    AssertInRange(collider.Size.X, objectPath, "size.x", MinDimension, MaxDimension);
                    AssertInRange(collider.Size.Y, objectPath, "size.y", MinDimension, MaxDimension);
                    AssertInRange(collider.Size.Z, objectPath, "size.z", MinDimension, MaxDimension);
                    break;

                case CharacterColliderShape.Sphere:
                    if (!collider.Radius.HasValue)
                    {
                        throw new CharacterDiagnosticException(
                            objectPath, "Sphere collider must specify 'radius'.", CharacterGateCodes.GeometryOutOfRange, field: "radius");
                    }

                    AssertInRange(collider.Radius.Value, objectPath, "radius", MinRadius, MaxRadius);

                    if (collider.Size != null)
                    {
                        float expectedDiameter = collider.Radius.Value * 2.0f;
                        AssertTolerance(collider.Size.X, expectedDiameter, objectPath, "size.x vs 2*radius");
                        AssertTolerance(collider.Size.Y, expectedDiameter, objectPath, "size.y vs 2*radius");
                        AssertTolerance(collider.Size.Z, expectedDiameter, objectPath, "size.z vs 2*radius");
                    }

                    break;

                case CharacterColliderShape.Capsule:
                    if (!collider.Radius.HasValue)
                    {
                        throw new CharacterDiagnosticException(
                            objectPath, "Capsule collider must specify 'radius'.", CharacterGateCodes.GeometryOutOfRange, field: "radius");
                    }

                    if (!collider.Height.HasValue)
                    {
                        throw new CharacterDiagnosticException(
                            objectPath, "Capsule collider must specify 'height'.", CharacterGateCodes.GeometryOutOfRange, field: "height");
                    }

                    float radius = collider.Radius.Value;
                    float height = collider.Height.Value;

                    AssertInRange(radius, objectPath, "radius", MinRadius, MaxRadius);
                    AssertInRange(height, objectPath, "height", MinDimension, MaxDimension);

                    if (height < radius * 2.0f)
                    {
                        throw new CharacterDiagnosticException(
                            objectPath,
                            $"Capsule height ({height}m) cannot be strictly less than its diameter ({radius * 2.0f}m).",
                            CharacterGateCodes.CapsuleHeightBelowDiameter,
                            field: "height");
                    }

                    if (collider.Size != null)
                    {
                        AssertTolerance(collider.Size.X, radius * 2.0f, objectPath, "size.x vs 2*radius");
                        AssertTolerance(collider.Size.Y, height, objectPath, "size.y vs height");
                        AssertTolerance(collider.Size.Z, radius * 2.0f, objectPath, "size.z vs 2*radius");
                    }

                    break;

                default:
                    throw new CharacterDiagnosticException(
                        objectPath,
                        $"Unsupported collider shape: {collider.Shape}. Only Box, Sphere and Capsule are accepted by this contract.");
            }
        }

        public static CanonicalColliderGeometry NormalizeValidCollider(in RawColliderDto collider)
        {
            if (collider == null)
            {
                throw new CharacterDiagnosticException(string.Empty, "Collider object cannot be null.");
            }

            switch (collider.Shape)
            {
                case CharacterColliderShape.Box:
                    if (collider.Size == null)
                    {
                        throw new CharacterDiagnosticException(
                            string.Empty,
                            "NormalizeValidCollider requires a validated Box collider with 'size'. Call ValidateRawCollider first.",
                            CharacterGateCodes.GeometryOutOfRange,
                            field: "size");
                    }

                    var boxFull = new CharacterAuthoringVector3(collider.Size.X, collider.Size.Y, collider.Size.Z);
                    return CanonicalColliderGeometry.CreateBox(
                        fullDimensions: boxFull,
                        halfExtents: new CharacterAuthoringVector3(boxFull.X * 0.5f, boxFull.Y * 0.5f, boxFull.Z * 0.5f));

                case CharacterColliderShape.Sphere:
                    if (!collider.Radius.HasValue)
                    {
                        throw new CharacterDiagnosticException(
                            string.Empty,
                            "NormalizeValidCollider requires a validated Sphere collider with 'radius'. Call ValidateRawCollider first.",
                            CharacterGateCodes.GeometryOutOfRange,
                            field: "radius");
                    }

                    float sphereRadius = collider.Radius.Value;
                    return CanonicalColliderGeometry.CreateSphere(
                        radius: sphereRadius,
                        fullDimensions: new CharacterAuthoringVector3(sphereRadius * 2f, sphereRadius * 2f, sphereRadius * 2f));

                case CharacterColliderShape.Capsule:
                    if (!collider.Radius.HasValue || !collider.Height.HasValue)
                    {
                        throw new CharacterDiagnosticException(
                            string.Empty,
                            "NormalizeValidCollider requires a validated Capsule collider with 'radius' and 'height'. Call ValidateRawCollider first.",
                            CharacterGateCodes.GeometryOutOfRange);
                    }

                    float capsuleRadius = collider.Radius.Value;
                    float capsuleHeight = collider.Height.Value;

                    // 几何恒等式: totalHeight == radius * 2 + cylinderHalfHeight * 2
                    float cylinderHalfHeight = (capsuleHeight - capsuleRadius * 2.0f) * 0.5f;

                    CanonicalColliderGeometry capsule = CanonicalColliderGeometry.CreateCapsule(
                        radius: capsuleRadius,
                        totalHeight: capsuleHeight,
                        cylinderHalfHeight: cylinderHalfHeight,
                        fullDimensions: new CharacterAuthoringVector3(capsuleRadius * 2f, capsuleHeight, capsuleRadius * 2f));

                    capsule.AssertCapsuleIdentity(string.Empty);
                    return capsule;

                default:
                    throw new CharacterDiagnosticException(
                        string.Empty,
                        $"Unsupported collider shape: {collider.Shape}. Only Box, Sphere and Capsule are accepted by this contract.");
            }
        }

        /// <summary>
        /// 校验并规范化。一步完成时使用本方法，它显式保证"先校验、后规范化"的顺序。
        /// </summary>
        public static CanonicalColliderGeometry ValidateAndNormalize(in RawColliderDto collider, string objectPath)
        {
            ValidateRawCollider(collider, objectPath);
            return NormalizeValidCollider(collider);
        }

        /// <summary>
        /// 非抛出式校验，把结果追加到既有诊断通道。
        /// 返回 true 表示无 Error 级问题（此时可以安全调用 <see cref="NormalizeValidCollider"/>）。
        /// </summary>
        public static bool TryValidate(
            in RawColliderDto collider,
            string objectPath,
            string sourcePath,
            List<CharacterAuthoringValidationIssue> issues)
        {
            try
            {
                ValidateRawCollider(collider, objectPath);
                return true;
            }
            catch (CharacterDiagnosticException exception)
            {
                if (issues != null)
                {
                    CharacterAuthoringValidationIssue issue = exception.ToValidationIssue();
                    issue.SourcePath = sourcePath ?? issue.SourcePath;
                    issues.Add(issue);
                }

                return false;
            }
        }

        /// <summary>
        /// 规范化姿态：把缩放强制锁定为绝对精确的 <c>(1.0f, 1.0f, 1.0f)</c>，
        /// 并彻底丢弃 <c>EulerHint</c>。
        /// </summary>
        /// <remarks>
        /// 这是规范"校验通过后规范化阶段强制锁定为绝对精确的 (1,1,1)"
        /// 与"EulerHint 在规范化几何求值时彻底丢弃"两条要求的落点：
        /// <see cref="ValidatePose"/> 允许 <c>|s - 1| &lt;= 1e-4</c>，
        /// 因此必须在此处消除残留偏差，否则下游会看到非精确单位缩放。
        /// </remarks>
        public static CharacterAuthoringPose NormalizeValidPose(in CharacterAuthoringPose pose)
        {
            if (pose == null)
            {
                throw new CharacterDiagnosticException(string.Empty, "localPose cannot be null.");
            }

            return new CharacterAuthoringPose
            {
                ParentKind = pose.ParentKind,
                ParentPath = pose.ParentPath,
                HumanoidSemantic = pose.HumanoidSemantic,
                Position = new CharacterAuthoringVector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                Rotation = new CharacterAuthoringQuaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W),
                Scale = new CharacterAuthoringVector3(1.0f, 1.0f, 1.0f),
                EulerHint = null
            };
        }

        private static void AssertInRange(float value, string path, string field, float min, float max)
        {
            if (!float.IsFinite(value) || value < min || value > max)
            {
                throw new CharacterDiagnosticException(
                    path,
                    $"Field '{field}' ({value}) is invalid. Must be finite within [{min}, {max}].",
                    CharacterGateCodes.GeometryOutOfRange,
                    field: field);
            }
        }

        private static void AssertTolerance(float actual, float expected, string path, string context)
        {
            if (!float.IsFinite(actual) || MathF.Abs(actual - expected) > LegacySizeTolerance)
            {
                throw new CharacterDiagnosticException(
                    path,
                    $"Legacy dimension '{context}' deviation exceeds 10mm. Actual: {actual}, Expected: {expected}.",
                    CharacterGateCodes.LegacySizeDeviationExceeded,
                    field: context);
            }
        }

        private static void ValidatePose(CharacterAuthoringPose pose, string path)
        {
            if (pose == null)
            {
                throw new CharacterDiagnosticException(path, "localPose cannot be null.");
            }

            if (!float.IsFinite(pose.Position.X) || !float.IsFinite(pose.Position.Y) || !float.IsFinite(pose.Position.Z))
            {
                throw new CharacterDiagnosticException(
                    path, "localPose.position contains non-finite values (NaN or Infinity).", field: "position");
            }

            // 先排查 NaN，再比对容差。顺序不可颠倒：NaN 与 1 的比较恒为 false，
            // 若先做容差比对，NaN 会穿透门禁。
            if (pose.Scale == null ||
                !float.IsFinite(pose.Scale.X) || !float.IsFinite(pose.Scale.Y) || !float.IsFinite(pose.Scale.Z) ||
                MathF.Abs(pose.Scale.X - 1.0f) > UnitScaleTolerance ||
                MathF.Abs(pose.Scale.Y - 1.0f) > UnitScaleTolerance ||
                MathF.Abs(pose.Scale.Z - 1.0f) > UnitScaleTolerance)
            {
                throw new CharacterDiagnosticException(
                    path,
                    "Scale must be strictly finite and unit (1,1,1). Non-uniform scale is prohibited.",
                    field: "scale");
            }

            float qx = pose.Rotation.X;
            float qy = pose.Rotation.Y;
            float qz = pose.Rotation.Z;
            float qw = pose.Rotation.W;

            if (!float.IsFinite(qx) || !float.IsFinite(qy) || !float.IsFinite(qz) || !float.IsFinite(qw))
            {
                throw new CharacterDiagnosticException(
                    path, "localPose.rotation contains non-finite values (NaN or Infinity).", field: "rotation");
            }

            float sqrNorm = qx * qx + qy * qy + qz * qz + qw * qw;
            if (MathF.Abs(sqrNorm - 1.0f) > RotationSqrNormTolerance)
            {
                throw new CharacterDiagnosticException(
                    path,
                    $"localPose.rotation must be normalized. SqrNorm: {sqrNorm}",
                    field: "rotation");
            }
        }
    }
}
