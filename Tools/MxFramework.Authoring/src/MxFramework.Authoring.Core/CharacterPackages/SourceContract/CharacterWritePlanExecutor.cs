using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxFramework.Authoring
{
    /// <summary>单个写操作的执行结果。</summary>
    public enum CharacterWritePlanOperationOutcome
    {
        Unknown = 0,

        /// <summary>已写入。</summary>
        Applied = 1,

        /// <summary>目标内容已与载荷一致，幂等跳过。</summary>
        SkippedIdentical = 2,

        /// <summary>已删除。</summary>
        Deleted = 3,

        /// <summary>目标本就不存在，删除幂等成功。</summary>
        SkippedMissing = 4
    }

    /// <summary>单操作执行明细。</summary>
    public sealed class CharacterWritePlanOperationResult
    {
        public CharacterWritePlanOperationResult(
            string operationId,
            CharacterWritePlanOperationKind kind,
            string targetRelativePath,
            string absolutePath,
            CharacterWritePlanOperationOutcome outcome)
        {
            OperationId = operationId ?? string.Empty;
            Kind = kind;
            TargetRelativePath = targetRelativePath ?? string.Empty;
            AbsolutePath = absolutePath ?? string.Empty;
            Outcome = outcome;
        }

        public string OperationId { get; }

        public CharacterWritePlanOperationKind Kind { get; }

        public string TargetRelativePath { get; }

        public string AbsolutePath { get; }

        public CharacterWritePlanOperationOutcome Outcome { get; }
    }

    /// <summary>WritePlan 执行结果。</summary>
    public sealed class CharacterWritePlanExecutionResult
    {
        public CharacterWritePlanExecutionResult(
            string planHash,
            IReadOnlyList<CharacterWritePlanOperationResult> results)
        {
            PlanHash = planHash ?? string.Empty;
            Results = results ?? Array.Empty<CharacterWritePlanOperationResult>();
        }

        public string PlanHash { get; }

        public IReadOnlyList<CharacterWritePlanOperationResult> Results { get; }

        public int AppliedCount => Results.Count(item =>
            item.Outcome == CharacterWritePlanOperationOutcome.Applied ||
            item.Outcome == CharacterWritePlanOperationOutcome.Deleted);

        public int SkippedCount => Results.Count(item =>
            item.Outcome == CharacterWritePlanOperationOutcome.SkippedIdentical ||
            item.Outcome == CharacterWritePlanOperationOutcome.SkippedMissing);
    }

    /// <summary>
    /// WritePlan 严格执行器。对应规范 v1.4 第 6.3 节。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与规范的三点差异（安全语义修正）：
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// 所有目标路径在执行前统一经 <see cref="SecurePathSandboxEnforcer"/> 解析。
    /// 规范把 <c>E-SEC-701</c> 列为 Fatal/BlockAll，但未说明执行器在哪一步调用沙箱；
    /// 本实现做<b>写入前的一次性预检</b>，避免路径违规导致"写了一半"的中间状态。
    /// </description></item>
    /// <item><description>
    /// <see cref="CharacterWritePlanStatus.VerificationPending"/> 与
    /// <see cref="CharacterWritePlanStatus.Failed"/> 的计划一律拒绝执行。
    /// </description></item>
    /// <item><description>
    /// 规范未定义 <c>UpdateFile</c> 目标缺失时的错误码，本实现暂用
    /// <c>E-PLAN-401</c> 并显式标注（属规范缺口，待确认）。
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class CharacterWritePlanExecutor
    {
        /// <summary>
        /// 静态校验计划结构。返回的问题不阻断调用方读取计划，但阻断执行。
        /// </summary>
        public static IReadOnlyList<CharacterAuthoringValidationIssue> Validate(CharacterWritePlan plan)
        {
            var issues = new List<CharacterAuthoringValidationIssue>();
            if (plan == null)
            {
                issues.Add(CharacterGateCodes.CreateIssue(
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    string.Empty,
                    "plan",
                    string.Empty,
                    "WritePlan is null."));
                return issues;
            }

            if (plan.PendingChecks.Count > 0 && plan.Status == CharacterWritePlanStatus.ReadyToApply)
            {
                issues.Add(CharacterGateCodes.CreateIssue(
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    string.Empty,
                    "plan.status",
                    "status",
                    "Plan declares pending checks but Status is ReadyToApply. Status must be VerificationPending.",
                    "Set Status = VerificationPending or clear PendingChecks."));
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = plan.Operations[i];
                string path = "plan.operations[" + i + "]";

                if (operation == null)
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path,
                        string.Empty,
                        "Operation is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(operation.OperationId))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path + ".operationId",
                        "operationId",
                        "OperationId is required."));
                }
                else if (!seenIds.Add(operation.OperationId))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path + ".operationId",
                        "operationId",
                        $"Duplicate OperationId '{operation.OperationId}'."));
                }

                if (operation.Kind == CharacterWritePlanOperationKind.Unknown)
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path + ".kind",
                        "kind",
                        "Operation kind must be CreateFile, UpdateFile or DeleteFile."));
                }

                if (string.IsNullOrWhiteSpace(operation.TargetRelativePath))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path + ".targetRelativePath",
                        "targetRelativePath",
                        "TargetRelativePath is required."));
                }

                if (operation.Kind == CharacterWritePlanOperationKind.UpdateFile &&
                    string.IsNullOrWhiteSpace(operation.PreconditionExpectedSha256))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        string.Empty,
                        path + ".preconditionExpectedSha256",
                        "preconditionExpectedSha256",
                        "UpdateFile requires PreconditionExpectedSha256."));
                }
            }

            for (int i = 0; i < plan.Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = plan.Operations[i];
                if (operation == null)
                {
                    continue;
                }

                for (int j = 0; j < operation.DependsOnOperations.Count; j++)
                {
                    string dependencyId = operation.DependsOnOperations[j];
                    if (string.Equals(dependencyId, operation.OperationId, StringComparison.Ordinal))
                    {
                        issues.Add(CharacterGateCodes.CreateIssue(
                            CharacterGateCodes.WritePlanCircularDependency,
                            string.Empty,
                            "plan.operations[" + i + "].dependsOnOperations[" + j + "]",
                            "dependsOnOperations",
                            $"Operation '{operation.OperationId}' depends on itself."));
                        continue;
                    }

                    if (!seenIds.Contains(dependencyId))
                    {
                        issues.Add(CharacterGateCodes.CreateIssue(
                            CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                            string.Empty,
                            "plan.operations[" + i + "].dependsOnOperations[" + j + "]",
                            "dependsOnOperations",
                            $"Operation '{operation.OperationId}' depends on unknown operation '{dependencyId}'."));
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Kahn 拓扑排序。无依赖节点按 <c>OperationId</c> 字典序稳定执行；
        /// 检测到依赖环立即抛出 <c>E-PLAN-501</c>。
        /// </summary>
        public static IReadOnlyList<string> TopologicalSort(CharacterWritePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < plan.Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = plan.Operations[i];
                if (operation == null || string.IsNullOrWhiteSpace(operation.OperationId))
                {
                    continue;
                }

                if (!indegree.ContainsKey(operation.OperationId))
                {
                    indegree[operation.OperationId] = 0;
                    dependents[operation.OperationId] = new List<string>();
                }
            }

            for (int i = 0; i < plan.Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = plan.Operations[i];
                if (operation == null || string.IsNullOrWhiteSpace(operation.OperationId))
                {
                    continue;
                }

                for (int j = 0; j < operation.DependsOnOperations.Count; j++)
                {
                    string dependencyId = operation.DependsOnOperations[j];
                    if (!indegree.ContainsKey(dependencyId))
                    {
                        throw new CharacterDiagnosticException(
                            "plan.operations[" + i + "].dependsOnOperations[" + j + "]",
                            $"Operation '{operation.OperationId}' depends on unknown operation '{dependencyId}'.",
                            CharacterGateCodes.WritePlanCircularDependency,
                            field: "dependsOnOperations");
                    }

                    if (string.Equals(dependencyId, operation.OperationId, StringComparison.Ordinal))
                    {
                        throw new CharacterDiagnosticException(
                            "plan.operations[" + i + "].dependsOnOperations[" + j + "]",
                            $"Operation '{operation.OperationId}' depends on itself.",
                            CharacterGateCodes.WritePlanCircularDependency,
                            field: "dependsOnOperations");
                    }

                    indegree[operation.OperationId]++;
                    dependents[dependencyId].Add(operation.OperationId);
                }
            }

            var ready = indegree.Where(item => item.Value == 0).Select(item => item.Key).ToList();
            ready.Sort(StringComparer.Ordinal);

            var ordered = new List<string>(indegree.Count);
            while (ready.Count > 0)
            {
                string current = ready[0];
                ready.RemoveAt(0);
                ordered.Add(current);

                List<string> children = dependents[current];
                var newlyReady = new List<string>();
                for (int i = 0; i < children.Count; i++)
                {
                    string child = children[i];
                    indegree[child]--;
                    if (indegree[child] == 0)
                    {
                        newlyReady.Add(child);
                    }
                }

                if (newlyReady.Count > 0)
                {
                    ready.AddRange(newlyReady);
                    ready.Sort(StringComparer.Ordinal);
                }
            }

            if (ordered.Count != indegree.Count)
            {
                var cyclic = indegree.Where(item => item.Value > 0).Select(item => item.Key).ToList();
                cyclic.Sort(StringComparer.Ordinal);

                throw new CharacterDiagnosticException(
                    "plan.operations",
                    "CircularDependencyDetected: " + string.Join(", ", cyclic),
                    CharacterGateCodes.WritePlanCircularDependency,
                    field: "dependsOnOperations");
            }

            return ordered;
        }

        /// <summary>
        /// 执行计划。所有路径先经沙箱预检，再按拓扑序幂等执行。
        /// </summary>
        /// <param name="plan">待执行计划。</param>
        /// <param name="trustedWorkspaceRoot">受信任工作区根目录。</param>
        public static CharacterWritePlanExecutionResult Execute(
            CharacterWritePlan plan,
            string trustedWorkspaceRoot)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (plan.Status != CharacterWritePlanStatus.ReadyToApply)
            {
                throw new CharacterDiagnosticException(
                    "plan.status",
                    $"WritePlan cannot be executed while Status is {plan.Status}. " +
                    "VerificationPending and Failed plans are blocked.",
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    field: "status");
            }

            IReadOnlyList<string> orderedIds = TopologicalSort(plan);

            var byId = new Dictionary<string, CharacterWritePlanOperation>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Operations.Count; i++)
            {
                CharacterWritePlanOperation operation = plan.Operations[i];
                if (operation != null && !string.IsNullOrWhiteSpace(operation.OperationId))
                {
                    byId[operation.OperationId] = operation;
                }
            }

            // 写入前一次性沙箱预检：路径违规必须阻断整批写入，不能留下半成品。
            var resolvedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < orderedIds.Count; i++)
            {
                CharacterWritePlanOperation operation = byId[orderedIds[i]];
                resolvedPaths[operation.OperationId] = SecurePathSandboxEnforcer.ResolveAndVerifyPath(
                    trustedWorkspaceRoot,
                    plan.PackageId,
                    operation.TargetRelativePath);
            }

            var results = new List<CharacterWritePlanOperationResult>(orderedIds.Count);
            for (int i = 0; i < orderedIds.Count; i++)
            {
                string operationId = orderedIds[i];
                CharacterWritePlanOperation operation = byId[operationId];
                string absolutePath = resolvedPaths[operationId];

                CharacterWritePlanOperationOutcome outcome = Apply(operation, absolutePath);
                results.Add(new CharacterWritePlanOperationResult(
                    operation.OperationId,
                    operation.Kind,
                    operation.TargetRelativePath,
                    absolutePath,
                    outcome));
            }

            return new CharacterWritePlanExecutionResult(plan.ComputeCanonicalHash(), results);
        }

        private static CharacterWritePlanOperationOutcome Apply(
            CharacterWritePlanOperation operation,
            string absolutePath)
        {
            switch (operation.Kind)
            {
                case CharacterWritePlanOperationKind.CreateFile:
                    return ApplyCreate(operation, absolutePath);

                case CharacterWritePlanOperationKind.UpdateFile:
                    return ApplyUpdate(operation, absolutePath);

                case CharacterWritePlanOperationKind.DeleteFile:
                    return ApplyDelete(operation, absolutePath);

                default:
                    throw new CharacterDiagnosticException(
                        operation.TargetRelativePath,
                        $"Unsupported write operation kind: {operation.Kind}.",
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        field: "kind");
            }
        }

        private static CharacterWritePlanOperationOutcome ApplyCreate(
            CharacterWritePlanOperation operation,
            string absolutePath)
        {
            if (File.Exists(absolutePath))
            {
                string currentHash = CharacterSourcePackageCanonicalDigest.ComputeFileHashHex(absolutePath);

                if (HashEquals(currentHash, operation.PayloadSha256))
                {
                    return CharacterWritePlanOperationOutcome.SkippedIdentical;
                }

                if (operation.OverwritePolicy == CharacterWritePlanOverwritePolicy.FailIfExists)
                {
                    throw new CharacterDiagnosticException(
                        operation.TargetRelativePath,
                        $"CreateFile target already exists with different content and OverwritePolicy is FailIfExists: '{operation.TargetRelativePath}'.",
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        field: "overwritePolicy");
                }
            }

            WritePayload(operation, absolutePath);
            return CharacterWritePlanOperationOutcome.Applied;
        }

        private static CharacterWritePlanOperationOutcome ApplyUpdate(
            CharacterWritePlanOperation operation,
            string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                throw new CharacterDiagnosticException(
                    operation.TargetRelativePath,
                    $"UpdateFile target does not exist: '{operation.TargetRelativePath}'.",
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    field: "targetRelativePath");
            }

            string currentHash = CharacterSourcePackageCanonicalDigest.ComputeFileHashHex(absolutePath);

            if (HashEquals(currentHash, operation.PayloadSha256))
            {
                return CharacterWritePlanOperationOutcome.SkippedIdentical;
            }

            if (!HashEquals(currentHash, operation.PreconditionExpectedSha256))
            {
                throw new CharacterDiagnosticException(
                    operation.TargetRelativePath,
                    $"UpdateFile precondition failed for '{operation.TargetRelativePath}'. " +
                    $"Expected {operation.PreconditionExpectedSha256}, actual {currentHash}.",
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    field: "preconditionExpectedSha256");
            }

            WritePayload(operation, absolutePath);
            return CharacterWritePlanOperationOutcome.Applied;
        }

        private static CharacterWritePlanOperationOutcome ApplyDelete(
            CharacterWritePlanOperation operation,
            string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                return CharacterWritePlanOperationOutcome.SkippedMissing;
            }

            if (!string.IsNullOrWhiteSpace(operation.PreconditionExpectedSha256))
            {
                string currentHash = CharacterSourcePackageCanonicalDigest.ComputeFileHashHex(absolutePath);
                if (!HashEquals(currentHash, operation.PreconditionExpectedSha256))
                {
                    throw new CharacterDiagnosticException(
                        operation.TargetRelativePath,
                        $"DeleteFile precondition failed for '{operation.TargetRelativePath}'. " +
                        $"Expected {operation.PreconditionExpectedSha256}, actual {currentHash}.",
                        CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        field: "preconditionExpectedSha256");
                }
            }

            File.Delete(absolutePath);
            return CharacterWritePlanOperationOutcome.Deleted;
        }

        private static void WritePayload(CharacterWritePlanOperation operation, string absolutePath)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] payload = operation.PayloadBytes ?? Array.Empty<byte>();
            File.WriteAllBytes(absolutePath, payload);

            string writtenHash = CharacterSourcePackageCanonicalDigest.ComputeFileHashHex(absolutePath);
            if (!string.IsNullOrEmpty(operation.PayloadSha256) && !HashEquals(writtenHash, operation.PayloadSha256))
            {
                throw new CharacterDiagnosticException(
                    operation.TargetRelativePath,
                    $"Payload verification failed after writing '{operation.TargetRelativePath}'. " +
                    $"Expected {operation.PayloadSha256}, actual {writtenHash}.",
                    CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                    field: "payloadSha256");
            }
        }

        /// <summary>比较两个摘要，容忍可选的 <c>sha256:</c> 前缀与大小写差异。</summary>
        public static bool HashEquals(string left, string right)
        {
            return string.Equals(StripPrefix(left), StripPrefix(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string StripPrefix(string hash)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return string.Empty;
            }

            const string prefix = CharacterSourcePackageCanonicalDigest.HashPrefix;
            return hash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? hash.Substring(prefix.Length)
                : hash;
        }
    }
}
