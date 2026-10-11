using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    internal sealed class VfxAgentHumanReviewCapability
    {
        public readonly string Nonce = Guid.NewGuid().ToString("N");
        public readonly string TaskHash;
        public readonly string ReceiptHash;
        public readonly string CandidateId;
        public readonly string CatalogContentHash;
        public readonly string SourcePrefabHash;
        public readonly string TargetRoot;
        public bool Consumed;

        internal VfxAgentHumanReviewCapability(string taskHash, string receiptHash, string candidateId, string catalogContentHash, string sourcePrefabHash, string targetRoot)
        {
            TaskHash = taskHash;
            ReceiptHash = receiptHash;
            CandidateId = candidateId;
            CatalogContentHash = catalogContentHash;
            SourcePrefabHash = sourcePrefabHash;
            TargetRoot = targetRoot;
        }
    }

    /// <summary>Issues non-serializable, one-use authority only after the UI has observed human playback and confirmation.</summary>
    internal static class VfxAgentHumanReviewAuthority
    {
        public static VfxAgentHumanReviewCapability Issue(VfxDevelopmentAgentTaskSession session, VfxAgentCandidateReceiptView candidate, string catalogContentHash, string targetRoot)
        {
            return new VfxAgentHumanReviewCapability(session.Task.TaskHash, session.Receipt.ReceiptHash, candidate.CandidateId, catalogContentHash, VfxCandidateSandbox.ComputeAssetFileHash(candidate.PrefabAssetPath), targetRoot);
        }
    }

    internal sealed class VfxAgentReviewPromotionResult
    {
        public bool Success;
        public string Message = string.Empty;
        public string DestinationPrefabPath = string.Empty;
        public string PrimitiveId = string.Empty;
    }

    /// <summary>Moves one validated Agent candidate and registers it as a ReviewNeeded Primitive under a guarded Catalog hash.</summary>
    /// <remarks>Editor-only, AllocByDesign. Never writes Ready and rolls the asset move back when Catalog validation or the final hash guard fails.</remarks>
    internal sealed class VfxAgentReviewPromotionService
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string CliProject = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";

        public VfxAgentReviewPromotionResult Promote(VfxDevelopmentAgentTaskSession session, VfxAgentTaskCandidateView work, VfxAgentCandidateReceiptView candidate, VfxAgentHumanReviewCapability capability)
        {
            if (work == null) return Fail("候选工作项不存在。");
            if (!ValidateCapability(session, candidate, capability, out string error)) return Fail(error);
            if (!ValidateUnityDependencyClosure(candidate.PrefabAssetPath, work.OutputRoot, out error)) return Fail(error);
            capability.Consumed = true;
            string sourceRoot = work.OutputRoot.TrimEnd('/');
            string destinationRoot = capability.TargetRoot.TrimEnd('/');
            if (!candidate.PrefabAssetPath.StartsWith(sourceRoot + "/", StringComparison.Ordinal))
                return Fail("候选 Prefab 不属于任务声明的候选输出目录。");
            string destinationPrefab = destinationRoot + candidate.PrefabAssetPath.Substring(sourceRoot.Length);
            if (!AssetDatabase.IsValidFolder(sourceRoot) || AssetDatabase.IsValidFolder(destinationRoot) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destinationRoot) != null)
                return Fail("候选源目录不存在或正式待评审目标已被占用。");
            VfxCandidateSandbox.EnsureAssetFolder(Path.GetDirectoryName(destinationRoot)?.Replace('\\', '/'));

            string moveError = AssetDatabase.MoveAsset(sourceRoot, destinationRoot);

            if (!string.IsNullOrEmpty(moveError)) return Fail("Unity 无法移动候选目录：" + moveError);
            try
            {
                string primitiveId = "vfx.primitive.agent.fireball_impact." + candidate.CandidateId.Replace('-', '_');
                string proposedPath = session.DirectoryPath + "/catalog.review-proposed.json";
                JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
                JArray primitives = catalog["primitives"] as JArray ?? throw new InvalidOperationException("VFX Catalog 缺少 primitives 数组。");
                for (int i = 0; i < primitives.Count; i++)
                    if (string.Equals(primitives[i]?["primitiveId"]?.Value<string>(), primitiveId, StringComparison.Ordinal)) throw new InvalidOperationException("待评审 Primitive ID 已存在：" + primitiveId);
                primitives.Add(new JObject
                {
                    ["primitiveId"] = primitiveId,
                    ["displayName"] = "Fireball Impact · " + candidate.Direction,
                    ["description"] = "Development Agent candidate promoted after explicit human playback. Receipt: " + session.Receipt.ReceiptHash,
                    ["assetRef"] = new JObject { ["kind"] = "UnityAsset", ["path"] = destinationPrefab },
                    ["backend"] = "ParticleSystem",
                    ["roles"] = new JArray("Impact"),
                    ["compatibleAnchors"] = new JArray("World"),
                    ["parameters"] = new JArray(),
                    ["reviewStatus"] = "ReviewNeeded",
                    ["visualMaturity"] = "Candidate",
                    ["exemplar"] = false,
                    ["approval"] = null,
                    ["tags"] = new JArray("agent-candidate", "fire", "impact", "human-played")
                });
                File.WriteAllText(proposedPath, catalog.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
                if (!RunCatalogValidation(proposedPath, out string validation)) throw new InvalidOperationException("Catalog 校验阻断：" + validation);
                if (!string.Equals(HashFile(CatalogPath), capability.CatalogContentHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Catalog 在人工确认后发生变化，授权已失效。");
                JObject receipt = BuildReceipt(
                    session,
                    candidate,
                    capability,
                    primitiveId,
                    sourceRoot,
                    destinationRoot,
                    destinationPrefab);
                using var transaction = new VfxAgentReviewPromotionTransaction(
                    sourceRoot,
                    destinationRoot,
                    CatalogPath,
                    session.DirectoryPath + "/promotion-receipt.json",
                    moveAlreadyApplied: true);
                bool published = transaction.Publish(
                    catalog,
                    receipt,
                    primitiveId,
                    destinationPrefab,
                    postMoveGuard: null,
                    out string publishMessage);
                if (!published) return Fail(publishMessage);
                return new VfxAgentReviewPromotionResult { Success = true, Message = "候选已事务化晋升并登记为 ReviewNeeded；Ready 仍被人工审批门禁阻断。", DestinationPrefabPath = destinationPrefab, PrimitiveId = primitiveId };
            }
            catch (Exception exception)
            {
                string rollback = AssetDatabase.MoveAsset(destinationRoot, sourceRoot);
                if (!string.IsNullOrWhiteSpace(rollback))
                    return Fail(exception.Message + " RecoveryRequired: candidate directory rollback move failed: " + rollback);
                return Fail(exception.Message);
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static bool ValidateCapability(VfxDevelopmentAgentTaskSession session, VfxAgentCandidateReceiptView candidate, VfxAgentHumanReviewCapability capability, out string error)
        {
            error = string.Empty;
            if (session?.Task == null || session.Receipt == null || !session.HasValidatedReceipt || candidate == null || capability == null || capability.Consumed) { error = "缺少有效的一次性人工评审授权。"; return false; }
            if (!string.Equals(capability.TaskHash, session.Task.TaskHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(capability.ReceiptHash, session.Receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(capability.CandidateId, candidate.CandidateId, StringComparison.Ordinal)) { error = "任务、回执或候选已变化，授权失效。"; return false; }
            if (!string.Equals(capability.SourcePrefabHash, VfxCandidateSandbox.ComputeAssetFileHash(candidate.PrefabAssetPath), StringComparison.OrdinalIgnoreCase)) { error = "候选 Prefab 在确认后发生变化，授权失效。"; return false; }
            return true;
        }

        private static bool ValidateUnityDependencyClosure(string prefabPath, string candidateRoot, out string error)
        {
            error = string.Empty;
            string normalizedCandidateRoot = (candidateRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/') + "/";
            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = (dependencies[i] ?? string.Empty).Replace('\\', '/');
                if (path.StartsWith(normalizedCandidateRoot, StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                if (path.IndexOf("Piloto Studio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.StartsWith("Assets/VFX/MxFramework/", StringComparison.Ordinal) && !path.StartsWith("Assets/VFX/MxFramework/Production/", StringComparison.Ordinal))
                {
                    error = "候选存在禁止的占位或 ReferenceOnly 传递依赖：" + path;
                    return false;
                }
            }
            return true;
        }

        private static bool RunCatalogValidation(string proposedPath, out string message)
        {
            string args = "run -c Release --no-restore --project \"" + CliProject + "\" -- vfx validate --project-root . --catalog \"" + proposedPath + "\"";
            var start = new ProcessStartInfo("dotnet", args) { WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using Process process = Process.Start(start);
            if (process == null) { message = "无法启动 Authoring CLI。"; return false; }
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(180000)) { process.Kill(); message = "Authoring CLI 超时。"; return false; }
            message = (output + "\n" + error).Trim();
            return process.ExitCode == 0;
        }

        private static JObject BuildReceipt(VfxDevelopmentAgentTaskSession session, VfxAgentCandidateReceiptView candidate, VfxAgentHumanReviewCapability capability, string primitiveId, string sourceRoot, string destinationRoot, string destinationPrefab)
        {
            return new JObject
            {
                ["schemaVersion"] = "mx.vfx.agent-review-promotion.v1", ["taskId"] = session.Task.TaskId, ["taskHash"] = session.Task.TaskHash,
                ["candidateId"] = candidate.CandidateId, ["agentReceiptHash"] = session.Receipt.ReceiptHash, ["authorityNonceHash"] = HashText(capability.Nonce),
                ["sourceRoot"] = sourceRoot, ["destinationRoot"] = destinationRoot, ["destinationPrefab"] = destinationPrefab,
                ["primitiveId"] = primitiveId, ["reviewStatus"] = "ReviewNeeded", ["readyGranted"] = false
            };
        }

        private static string HashFile(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(); }
        private static string HashText(string value) { using SHA256 sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant(); }
        private static VfxAgentReviewPromotionResult Fail(string message) => new VfxAgentReviewPromotionResult { Message = message ?? string.Empty };
    }
}
