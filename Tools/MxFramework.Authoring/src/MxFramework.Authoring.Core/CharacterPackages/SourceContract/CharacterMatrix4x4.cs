using System;

namespace MxFramework.Authoring
{
    /// <summary>
    /// noEngine 4x4 变换矩阵，为第 4.1 节骨骼双锚点重绑定提供数学基础。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 规范澄清项（存储布局）：第 1.2 节的 <c>bindPoseWorldMatrix</c> 只规定
    /// "16 个 number"，<b>未指定行列主序</b>。而 glTF / FBX / Blender 的导出惯例
    /// 是列主序。本实现显式固定为<b>行主序（row-major）</b>且<b>列向量</b>约定，
    /// 即 <c>v' = M · v</c>，下标为 <c>m[row * 4 + col]</c>，平移分量位于
    /// <c>[0,3] [1,3] [2,3]</c>。该选择必须由规范确认，否则与外部提取器
    /// 产出的矩阵可能发生转置错配（表现为姿态镜像或绕原点旋转）。
    /// </para>
    /// <para>
    /// 不使用 Unity 的 <c>Matrix4x4</c>：Core 必须保持零引擎引用。
    /// </para>
    /// </remarks>
    public sealed class CharacterMatrix4x4
    {
        /// <summary>矩阵元素个数。</summary>
        public const int ElementCount = 16;

        private readonly float[] _values;

        private CharacterMatrix4x4(float[] values)
        {
            _values = values;
        }

        /// <summary>行主序元素数组的只读视图。</summary>
        public float[] Values => _values;

        /// <summary>按 <c>m[row, col]</c> 取值。</summary>
        public float this[int row, int col]
        {
            get
            {
                if (row < 0 || row > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(row));
                }

                if (col < 0 || col > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(col));
                }

                return _values[row * 4 + col];
            }
        }

        public static CharacterMatrix4x4 Identity()
        {
            return new CharacterMatrix4x4(new[]
            {
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f
            });
        }

        /// <summary>
        /// 由 16 个元素构造。长度不符或含非有限分量时抛出诊断异常。
        /// </summary>
        public static CharacterMatrix4x4 FromArray(float[] values, string objectPath)
        {
            if (values == null)
            {
                throw new CharacterDiagnosticException(objectPath, "Matrix array cannot be null.");
            }

            if (values.Length != ElementCount)
            {
                throw new CharacterDiagnosticException(
                    objectPath,
                    $"Matrix must contain exactly {ElementCount} elements, got {values.Length}.");
            }

            var copy = new float[ElementCount];
            for (int i = 0; i < ElementCount; i++)
            {
                if (!float.IsFinite(values[i]))
                {
                    throw new CharacterDiagnosticException(
                        objectPath,
                        $"Matrix element [{i}] is not finite ({values[i]}).");
                }

                copy[i] = values[i];
            }

            return new CharacterMatrix4x4(copy);
        }

        public bool IsFinite()
        {
            for (int i = 0; i < ElementCount; i++)
            {
                if (!float.IsFinite(_values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>矩阵乘法：<c>result = left · right</c>（列向量约定，先作用 right）。</summary>
        public static CharacterMatrix4x4 Multiply(CharacterMatrix4x4 left, CharacterMatrix4x4 right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var result = new float[ElementCount];
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += left[row, k] * right[k, col];
                    }

                    result[row * 4 + col] = sum;
                }
            }

            return new CharacterMatrix4x4(result);
        }

        /// <summary>
        /// 求逆。采用带部分主元选取的 Gauss-Jordan 消元，对奇异矩阵抛出诊断异常。
        /// </summary>
        public CharacterMatrix4x4 Inverse(string objectPath)
        {
            const int size = 4;
            var augmented = new float[size, size * 2];

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    augmented[row, col] = _values[row * 4 + col];
                }

                augmented[row, size + row] = 1f;
            }

            for (int pivot = 0; pivot < size; pivot++)
            {
                // 部分主元选取：保证确定性并避免用零做除数。
                int bestRow = pivot;
                float bestMagnitude = MathF.Abs(augmented[pivot, pivot]);
                for (int row = pivot + 1; row < size; row++)
                {
                    float magnitude = MathF.Abs(augmented[row, pivot]);
                    if (magnitude > bestMagnitude)
                    {
                        bestMagnitude = magnitude;
                        bestRow = row;
                    }
                }

                if (bestMagnitude <= 1e-12f)
                {
                    throw new CharacterDiagnosticException(
                        objectPath,
                        "Matrix is singular and cannot be inverted.");
                }

                if (bestRow != pivot)
                {
                    for (int col = 0; col < size * 2; col++)
                    {
                        float swap = augmented[pivot, col];
                        augmented[pivot, col] = augmented[bestRow, col];
                        augmented[bestRow, col] = swap;
                    }
                }

                float divisor = augmented[pivot, pivot];
                for (int col = 0; col < size * 2; col++)
                {
                    augmented[pivot, col] /= divisor;
                }

                for (int row = 0; row < size; row++)
                {
                    if (row == pivot)
                    {
                        continue;
                    }

                    float factor = augmented[row, pivot];
                    if (factor == 0f)
                    {
                        continue;
                    }

                    for (int col = 0; col < size * 2; col++)
                    {
                        augmented[row, col] -= factor * augmented[pivot, col];
                    }
                }
            }

            var inverse = new float[ElementCount];
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    inverse[row * 4 + col] = augmented[row, size + col];
                }
            }

            return new CharacterMatrix4x4(inverse);
        }

        /// <summary>
        /// 两个矩阵的逐元素最大绝对偏差。规范第 4.1 节的 Δ_B 即此度量
        /// （"矩阵最大绝对偏差"）。
        /// </summary>
        public static float MaxAbsoluteDeviation(CharacterMatrix4x4 left, CharacterMatrix4x4 right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            float worst = 0f;
            for (int i = 0; i < ElementCount; i++)
            {
                float deviation = MathF.Abs(left._values[i] - right._values[i]);
                if (deviation > worst)
                {
                    worst = deviation;
                }
            }

            return worst;
        }

        /// <summary>把姿态转换为 <c>T · R · S</c> 矩阵（列向量约定）。</summary>
        public static CharacterMatrix4x4 FromPose(CharacterAuthoringPose pose)
        {
            if (pose == null)
            {
                throw new ArgumentNullException(nameof(pose));
            }

            float x = pose.Rotation.X;
            float y = pose.Rotation.Y;
            float z = pose.Rotation.Z;
            float w = pose.Rotation.W;

            float sx = pose.Scale != null ? pose.Scale.X : 1f;
            float sy = pose.Scale != null ? pose.Scale.Y : 1f;
            float sz = pose.Scale != null ? pose.Scale.Z : 1f;

            // 旋转矩阵（行主序）
            float r00 = 1f - 2f * (y * y + z * z);
            float r01 = 2f * (x * y - z * w);
            float r02 = 2f * (x * z + y * w);
            float r10 = 2f * (x * y + z * w);
            float r11 = 1f - 2f * (x * x + z * z);
            float r12 = 2f * (y * z - x * w);
            float r20 = 2f * (x * z - y * w);
            float r21 = 2f * (y * z + x * w);
            float r22 = 1f - 2f * (x * x + y * y);

            return new CharacterMatrix4x4(new[]
            {
                r00 * sx, r01 * sy, r02 * sz, pose.Position.X,
                r10 * sx, r11 * sy, r12 * sz, pose.Position.Y,
                r20 * sx, r21 * sy, r22 * sz, pose.Position.Z,
                0f, 0f, 0f, 1f
            });
        }

        /// <summary>
        /// 把矩阵分解回姿态。缩放由三个基向量的长度还原，旋转归一化后输出。
        /// </summary>
        public CharacterAuthoringPose ToPose(string objectPath)
        {
            float sx = Length(_values[0], _values[4], _values[8]);
            float sy = Length(_values[1], _values[5], _values[9]);
            float sz = Length(_values[2], _values[6], _values[10]);

            if (sx <= 1e-8f || sy <= 1e-8f || sz <= 1e-8f)
            {
                throw new CharacterDiagnosticException(
                    objectPath,
                    "Matrix cannot be decomposed into a pose: a basis vector has zero length.");
            }

            float m00 = _values[0] / sx;
            float m10 = _values[4] / sx;
            float m20 = _values[8] / sx;
            float m01 = _values[1] / sy;
            float m11 = _values[5] / sy;
            float m21 = _values[9] / sy;
            float m02 = _values[2] / sz;
            float m12 = _values[6] / sz;
            float m22 = _values[10] / sz;

            float trace = m00 + m11 + m22;
            float qx;
            float qy;
            float qz;
            float qw;

            if (trace > 0f)
            {
                float s = MathF.Sqrt(trace + 1f) * 2f;
                qw = 0.25f * s;
                qx = (m21 - m12) / s;
                qy = (m02 - m20) / s;
                qz = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
                qw = (m21 - m12) / s;
                qx = 0.25f * s;
                qy = (m01 + m10) / s;
                qz = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
                qw = (m02 - m20) / s;
                qx = (m01 + m10) / s;
                qy = 0.25f * s;
                qz = (m12 + m21) / s;
            }
            else
            {
                float s = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
                qw = (m10 - m01) / s;
                qx = (m02 + m20) / s;
                qy = (m12 + m21) / s;
                qz = 0.25f * s;
            }

            float norm = Length(qx, qy, qz, qw);
            if (norm <= 1e-8f)
            {
                throw new CharacterDiagnosticException(
                    objectPath,
                    "Matrix rotation part could not be decomposed into a unit quaternion.");
            }

            return new CharacterAuthoringPose
            {
                Position = new CharacterAuthoringVector3(_values[3], _values[7], _values[11]),
                Rotation = new CharacterAuthoringQuaternion(qx / norm, qy / norm, qz / norm, qw / norm),
                Scale = new CharacterAuthoringVector3(sx, sy, sz),
                EulerHint = null
            };
        }

        /// <summary>提取平移分量。</summary>
        public CharacterAuthoringVector3 GetTranslation()
        {
            return new CharacterAuthoringVector3(_values[3], _values[7], _values[11]);
        }

        private static float Length(float a, float b, float c)
        {
            return MathF.Sqrt(a * a + b * b + c * c);
        }

        private static float Length(float a, float b, float c, float d)
        {
            return MathF.Sqrt(a * a + b * b + c * c + d * d);
        }
    }
}
