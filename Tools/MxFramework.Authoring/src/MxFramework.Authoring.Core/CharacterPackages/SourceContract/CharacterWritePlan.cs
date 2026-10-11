using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>WritePlan 状态机。对应规范 v1.4 第 6.3 节。</summary>
    public enum CharacterWritePlanStatus
    {
        ReadyToApply = 0,

        /// <summary>存在待决项（如语义重绑定），禁止正式发布。</summary>
        VerificationPending = 1,

        Failed = 2
    }

    /// <summary>覆写策略。对应规范 v1.4 第 6.3 节。</summary>
    public enum CharacterWritePlanOverwritePolicy
    {
        FailIfExists = 0,
        OverwriteAllowed = 1
    }

    /// <summary>写操作种类。对应规范 v1.4 第 6.3 节。</summary>
    public enum CharacterWritePlanOperationKind
    {
        Unknown = 0,
        CreateFile = 1,
        UpdateFile = 2,
        DeleteFile = 3
    }

    /// <summary>单个写操作。对应规范 v1.4 第 6.3 节 <c>WritePlanOperation</c>。</summary>
    public sealed class CharacterWritePlanOperation
    {
        public string OperationId { get; set; } = string.Empty;

        public CharacterWritePlanOperationKind Kind { get; set; } = CharacterWritePlanOperationKind.Unknown;

        /// <summary>相对包沙箱根目录的目标路径。</summary>
        public string TargetRelativePath { get; set; } = string.Empty;

        public CharacterWritePlanOverwritePolicy OverwritePolicy { get; set; } =
            CharacterWritePlanOverwritePolicy.FailIfExists;

        /// <summary>
        /// 前置期望摘要。<see cref="CharacterWritePlanOperationKind.UpdateFile"/> 必填，
        /// <see cref="CharacterWritePlanOperationKind.DeleteFile"/> 选填（填写则必须匹配）。
        /// </summary>
        public string PreconditionExpectedSha256 { get; set; }

        /// <summary>期望写入内容的摘要。</summary>
        public string PayloadSha256 { get; set; } = string.Empty;

        /// <summary>
        /// 写入载荷。
        /// </summary>
        /// <remarks>
        /// 规范澄清项：把原始字节内联进 JSON 计划会让计划体积随资产线性膨胀
        /// （base64 编码再放大 4/3），且与"计划本身可哈希"的目标冲突。
        /// 因此 <see cref="CharacterWritePlan.ComputeCanonicalHash"/> 只对
        /// <see cref="PayloadSha256"/> 求值，不对本字段求值。
        /// 是否需要改为外置载荷引用需规范确认。
        /// </remarks>
        public byte[] PayloadBytes { get; set; } = Array.Empty<byte>();

        /// <summary>依赖的操作 ID 列表。</summary>
        public List<string> DependsOnOperations { get; set; } = new List<string>();
    }

    /// <summary>
    /// 完整 WritePlan。对应规范 v1.4 第 6.3 节 <c>CharacterUnityImportWritePlan</c>。
    /// </summary>
    /// <remarks>
    /// 命名偏差说明：仓库中已存在一个同语义但结构完全不同的
    /// <c>CharacterUnityImportWritePlan</c>（扁平 <c>Writes[]</c> 结构），
    /// 且被 <c>CharacterAuthoringCompiler</c>、<c>CharacterUnityImportBridge</c>、
    /// <c>CharacterPackageCommands</c> 三处消费。为避免未经裁决就破坏该公共契约，
    /// 本类型以新名称并行落地；合并（替换旧类型）属于待裁决项。
    /// </remarks>
    public sealed class CharacterWritePlan
    {
        public string PlanVersion { get; set; } = "2.4.0";

        public string PackageId { get; set; } = string.Empty;

        public string SourceSnapshotId { get; set; } = string.Empty;

        public CharacterWritePlanStatus Status { get; set; } = CharacterWritePlanStatus.ReadyToApply;

        /// <summary>待决校验清单。非空时 <see cref="Status"/> 必须为 VerificationPending。</summary>
        public List<string> PendingChecks { get; set; } = new List<string>();

        public List<CharacterWritePlanOperation> Operations { get; set; } = new List<CharacterWritePlanOperation>();

        /// <summary>
        /// 计划自身的规范化摘要。只对语义字段求值，不对 <c>PayloadBytes</c> 求值。
        /// </summary>
        public string ComputeCanonicalHash()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("planVersion|").Append(PlanVersion ?? string.Empty).Append('\n');
            builder.Append("packageId|").Append(PackageId ?? string.Empty).Append('\n');
            builder.Append("sourceSnapshotId|").Append(SourceSnapshotId ?? string.Empty).Append('\n');
            builder.Append("status|").Append(Status).Append('\n');

            for (int i = 0; i < PendingChecks.Count; i++)
            {
                builder.Append("pendingCheck|").Append(PendingChecks[i] ?? string.Empty).Append('\n');
            }

            var operationLines = new List<string>(Operations.Count);
            for (int i = 0; i < Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = Operations[i];
                if (operation == null)
                {
                    continue;
                }

                var dependencies = new List<string>(operation.DependsOnOperations);
                dependencies.Sort(StringComparer.Ordinal);

                operationLines.Add(
                    string.Join(
                        "|",
                        operation.OperationId ?? string.Empty,
                        operation.Kind.ToString(),
                        operation.TargetRelativePath ?? string.Empty,
                        operation.OverwritePolicy.ToString(),
                        operation.PreconditionExpectedSha256 ?? string.Empty,
                        operation.PayloadSha256 ?? string.Empty,
                        string.Join(",", dependencies)));
            }

            operationLines.Sort(StringComparer.Ordinal);
            for (int i = 0; i < operationLines.Count; i++)
            {
                builder.Append("operation|").Append(operationLines[i]).Append('\n');
            }

            byte[] payload = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(payload);

            var hex = new System.Text.StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return CharacterSourcePackageCanonicalDigest.HashPrefix + hex;
        }
    }
}
