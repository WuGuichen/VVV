using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
    {
        None = 0,
        AfterCardFilesInstalled = 1,
        AfterCardIndexInstalled = 2,
        AfterGroupCatalogInstalled = 3,
        AfterCommittedMarkerPersisted = 4
    }

    internal sealed class CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException : Exception
    {
        public CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException(string message)
            : base(message)
        {
        }
    }

    internal static partial class CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
    {
        private const string RecoverySchema =
            "mx.character-test.runtime-ai-planner.project-catalog-transaction.v1";
        private const string TransactionsDirectoryName = "transactions";
        private const string TransactionDirectoryPrefix = "transaction-";
        private const string JournalFileName = "transaction.json";
        private const string StagingDirectoryName = "staging";
        private const string BackupDirectoryName = "backup";
        private const string CardIndexBackupFileName = "card-index.json";
        private const string GroupCatalogBackupFileName = "group-catalog.json";
        private const string CardFilesBackupDirectoryName = "card-files";

        private static string _testStreamingRootOverride;
        private static string _testRecoveryRootOverride;
        private static Action<CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint>
            _testFaultInjection;

        private enum ProjectCatalogTransactionPhase
        {
            Prepared = 0,
            CardFilesInstalled = 1,
            CardIndexInstalled = 2,
            GroupCatalogInstalled = 3,
            Committed = 4
        }

        [Serializable]
        private sealed class ProjectCatalogTransactionCardFileBackup
        {
            [JsonProperty("fileName")]
            public string FileName { get; set; } = string.Empty;

            [JsonProperty("sha256")]
            public string Sha256 { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class ProjectCatalogTransactionJournal
        {
            [JsonProperty("schema")]
            public string Schema { get; set; } = RecoverySchema;

            [JsonProperty("transactionId")]
            public string TransactionId { get; set; } = string.Empty;

            [JsonProperty("phase")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ProjectCatalogTransactionPhase Phase { get; set; }

            [JsonProperty("catalogDirectory")]
            public string CatalogDirectory { get; set; } = string.Empty;

            [JsonProperty("cardCatalogPath")]
            public string CardCatalogPath { get; set; } = string.Empty;

            [JsonProperty("groupCatalogPath")]
            public string GroupCatalogPath { get; set; } = string.Empty;

            [JsonProperty("cardFilesDirectory")]
            public string CardFilesDirectory { get; set; } = string.Empty;

            [JsonProperty("catalogDirectoryExisted")]
            public bool CatalogDirectoryExisted { get; set; }

            [JsonProperty("cardCatalogExisted")]
            public bool CardCatalogExisted { get; set; }

            [JsonProperty("groupCatalogExisted")]
            public bool GroupCatalogExisted { get; set; }

            [JsonProperty("cardFilesDirectoryExisted")]
            public bool CardFilesDirectoryExisted { get; set; }

            [JsonProperty("cardCatalogSha256")]
            public string CardCatalogSha256 { get; set; } = string.Empty;

            [JsonProperty("groupCatalogSha256")]
            public string GroupCatalogSha256 { get; set; } = string.Empty;

            [JsonProperty("cardFiles")]
            public List<ProjectCatalogTransactionCardFileBackup> CardFiles { get; set; } =
                new List<ProjectCatalogTransactionCardFileBackup>();
        }

        private sealed class ProjectCatalogTransactionContext
        {
            public string TransactionId { get; set; }
            public string RecoveryRoot { get; set; }
            public string TransactionRoot { get; set; }
            public string JournalPath { get; set; }
            public string StagingRoot { get; set; }
            public string BackupRoot { get; set; }
            public string TemporaryCardPath { get; set; }
            public string TemporaryGroupPath { get; set; }
            public string CardIndexBackupPath { get; set; }
            public string GroupCatalogBackupPath { get; set; }
            public string CardFilesBackupRoot { get; set; }
        }

        private sealed class ProjectCatalogRecoveryTestScope : IDisposable
        {
            private readonly string _previousStreamingRoot;
            private readonly string _previousRecoveryRoot;
            private readonly Action<CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint>
                _previousFaultInjection;
            private bool _disposed;

            public ProjectCatalogRecoveryTestScope(
                string streamingRoot,
                string recoveryRoot,
                Action<CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint> faultInjection)
            {
                _previousStreamingRoot = _testStreamingRootOverride;
                _previousRecoveryRoot = _testRecoveryRootOverride;
                _previousFaultInjection = _testFaultInjection;
                _testStreamingRootOverride = Path.GetFullPath(streamingRoot ?? string.Empty);
                _testRecoveryRootOverride = Path.GetFullPath(recoveryRoot ?? string.Empty);
                _testFaultInjection = faultInjection;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _testStreamingRootOverride = _previousStreamingRoot;
                _testRecoveryRootOverride = _previousRecoveryRoot;
                _testFaultInjection = _previousFaultInjection;
            }
        }

        internal static IDisposable BeginRecoveryTestScope(
            string streamingRoot,
            string recoveryRoot,
            Action<CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint> faultInjection = null)
        {
            return new ProjectCatalogRecoveryTestScope(streamingRoot, recoveryRoot, faultInjection);
        }

        internal static CharacterTestRuntimeAiPlannerProjectCatalogWriteResult WriteForRecoveryTests(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            return Write(request);
        }

        internal static bool RecoverPendingTransactionsForTests(out string diagnostic)
        {
            return TryRecoverPendingTransactions(out _, out diagnostic);
        }

        private static string GetEffectiveStreamingRoot()
        {
            return !string.IsNullOrWhiteSpace(_testStreamingRootOverride)
                ? Path.GetFullPath(_testStreamingRootOverride)
                : Path.GetFullPath(Application.streamingAssetsPath);
        }

        private static string GetEffectiveRecoveryRoot()
        {
            if (!string.IsNullOrWhiteSpace(_testRecoveryRootOverride))
                return Path.GetFullPath(_testRecoveryRootOverride);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(
                projectRoot,
                "UserSettings",
                "RuntimeAiLab",
                "ProjectCatalogRecovery");
        }

        private static void RefreshAssetsAfterRecovery()
        {
            if (string.IsNullOrWhiteSpace(_testStreamingRootOverride))
                AssetDatabase.Refresh();
        }

        private static void RefreshAssetsAfterCatalogMutation()
        {
            if (string.IsNullOrWhiteSpace(_testStreamingRootOverride))
                AssetDatabase.Refresh();
        }

        private static bool TryNormalizePath(string path, out string normalized, out string diagnostic)
        {
            try
            {
                normalized = Path.GetFullPath(path ?? string.Empty);
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                normalized = string.Empty;
                diagnostic = exception.Message;
                return false;
            }
        }

        private static bool IsPathWithinRoot(string path, string root, bool allowRoot = false)
        {
            if (!TryNormalizePath(path, out string normalizedPath, out _)
                || !TryNormalizePath(root, out string normalizedRoot, out _))
            {
                return false;
            }

            normalizedRoot = normalizedRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (allowRoot && string.Equals(
                    normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryValidateCatalogPaths(
            string requestedCardPath,
            string requestedGroupPath,
            out string cardPath,
            out string groupPath,
            out string catalogDirectory,
            out string diagnostic)
        {
            cardPath = string.Empty;
            groupPath = string.Empty;
            catalogDirectory = string.Empty;
            bool cardPathValid = TryNormalizePath(
                requestedCardPath,
                out cardPath,
                out string cardDiagnostic);
            bool groupPathValid = TryNormalizePath(
                requestedGroupPath,
                out groupPath,
                out string groupDiagnostic);
            if (!cardPathValid || !groupPathValid)
            {
                diagnostic = "项目目录路径无效："
                    + (cardDiagnostic.Length > 0 ? cardDiagnostic : groupDiagnostic);
                return false;
            }

            string streamingRoot = GetEffectiveStreamingRoot();
            if (!IsPathWithinRoot(cardPath, streamingRoot)
                || !IsPathWithinRoot(groupPath, streamingRoot))
            {
                diagnostic = "拒绝写入 StreamingAssets 之外的项目文件。";
                return false;
            }

            string cardDirectory = Path.GetDirectoryName(cardPath) ?? string.Empty;
            string groupDirectory = Path.GetDirectoryName(groupPath) ?? string.Empty;
            if (!string.Equals(cardDirectory, groupDirectory, StringComparison.OrdinalIgnoreCase))
            {
                diagnostic = "行为卡与行为组目录必须位于同一个项目目录中。";
                return false;
            }

            catalogDirectory = Path.GetFullPath(cardDirectory);
            string cardFilesDirectory = Path.Combine(
                catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName);
            if (!IsPathWithinRoot(cardFilesDirectory, streamingRoot))
            {
                diagnostic = "行为卡独立文件目录必须位于 StreamingAssets 内。";
                return false;
            }

            string recoveryRoot = GetEffectiveRecoveryRoot();
            if (!TryNormalizePath(recoveryRoot, out recoveryRoot, out string recoveryDiagnostic))
            {
                diagnostic = "项目目录恢复根路径无效：" + recoveryDiagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static ProjectCatalogTransactionContext CreateTransactionContext(string transactionId)
        {
            string recoveryRoot = GetEffectiveRecoveryRoot();
            string transactionsRoot = Path.Combine(recoveryRoot, TransactionsDirectoryName);
            string transactionRoot = Path.Combine(
                transactionsRoot,
                TransactionDirectoryPrefix + transactionId);
            var context = new ProjectCatalogTransactionContext
            {
                TransactionId = transactionId,
                RecoveryRoot = recoveryRoot,
                TransactionRoot = transactionRoot,
                JournalPath = Path.Combine(transactionRoot, JournalFileName),
                StagingRoot = Path.Combine(transactionRoot, StagingDirectoryName),
                BackupRoot = Path.Combine(transactionRoot, BackupDirectoryName)
            };
            context.TemporaryCardPath = Path.Combine(
                context.StagingRoot,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            context.TemporaryGroupPath = Path.Combine(
                context.StagingRoot,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            context.CardIndexBackupPath = Path.Combine(context.BackupRoot, CardIndexBackupFileName);
            context.GroupCatalogBackupPath = Path.Combine(context.BackupRoot, GroupCatalogBackupFileName);
            context.CardFilesBackupRoot = Path.Combine(
                context.BackupRoot,
                CardFilesBackupDirectoryName);
            return context;
        }

        private static bool IsSafeTransactionContext(ProjectCatalogTransactionContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.TransactionId))
                return false;
            string expectedRoot = Path.Combine(
                context.RecoveryRoot,
                TransactionsDirectoryName,
                TransactionDirectoryPrefix + context.TransactionId);
            return string.Equals(
                    Path.GetFullPath(context.TransactionRoot),
                    Path.GetFullPath(expectedRoot),
                    StringComparison.OrdinalIgnoreCase)
                && IsPathWithinRoot(context.TransactionRoot, context.RecoveryRoot)
                && IsPathWithinRoot(context.JournalPath, context.TransactionRoot)
                && IsPathWithinRoot(context.StagingRoot, context.TransactionRoot)
                && IsPathWithinRoot(context.BackupRoot, context.TransactionRoot)
                && IsPathWithinRoot(context.CardIndexBackupPath, context.BackupRoot)
                && IsPathWithinRoot(context.GroupCatalogBackupPath, context.BackupRoot)
                && IsPathWithinRoot(context.CardFilesBackupRoot, context.BackupRoot);
        }

        private static void WriteTextDurable(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            byte[] bytes = new UTF8Encoding(false).GetBytes(text ?? string.Empty);
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void CopyFileDurable(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);
            using (var source = new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            using (var destination = new FileStream(
                       destinationPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       81920,
                       FileOptions.WriteThrough))
            {
                source.CopyTo(destination);
                destination.Flush(true);
            }
        }

        private static void PersistJournal(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal)
        {
            if (!IsSafeTransactionContext(context))
                throw new InvalidDataException("项目目录事务恢复路径越界，拒绝持久化 journal。");
            string json = JsonConvert.SerializeObject(journal, Formatting.Indented);
            string nextPath = context.JournalPath + ".next";
            if (!IsPathWithinRoot(nextPath, context.TransactionRoot))
                throw new InvalidDataException("项目目录事务 journal 临时路径越界。");
            if (File.Exists(nextPath))
                File.Delete(nextPath);
            WriteTextDurable(nextPath, json);
            if (File.Exists(context.JournalPath))
                File.Replace(nextPath, context.JournalPath, null, true);
            else
                File.Move(nextPath, context.JournalPath);
        }

        private static void PersistPhase(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            ProjectCatalogTransactionPhase phase)
        {
            journal.Phase = phase;
            PersistJournal(context, journal);
        }

        private static ProjectCatalogTransactionJournal CreateDurableBackups(
            ProjectCatalogTransactionContext context,
            string catalogDirectory,
            string cardPath,
            string groupPath)
        {
            if (!IsSafeTransactionContext(context))
                throw new InvalidDataException("项目目录事务路径不在恢复根内。");
            string streamingRoot = GetEffectiveStreamingRoot();
            string cardFilesDirectory = Path.Combine(
                catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName);
            if (!IsPathWithinRoot(cardPath, streamingRoot)
                || !IsPathWithinRoot(groupPath, streamingRoot)
                || !IsPathWithinRoot(cardFilesDirectory, streamingRoot))
            {
                throw new InvalidDataException("项目目录事务目标不在 StreamingAssets 内。");
            }

            var journal = new ProjectCatalogTransactionJournal
            {
                TransactionId = context.TransactionId,
                Phase = ProjectCatalogTransactionPhase.Prepared,
                CatalogDirectory = Path.GetFullPath(catalogDirectory),
                CardCatalogPath = Path.GetFullPath(cardPath),
                GroupCatalogPath = Path.GetFullPath(groupPath),
                CardFilesDirectory = Path.GetFullPath(cardFilesDirectory),
                CatalogDirectoryExisted = Directory.Exists(catalogDirectory),
                CardCatalogExisted = File.Exists(cardPath),
                GroupCatalogExisted = File.Exists(groupPath),
                CardFilesDirectoryExisted = Directory.Exists(cardFilesDirectory)
            };

            Directory.CreateDirectory(context.BackupRoot);
            if (journal.CardCatalogExisted)
            {
                journal.CardCatalogSha256 =
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardPath);
                CopyFileDurable(cardPath, context.CardIndexBackupPath);
            }
            if (journal.GroupCatalogExisted)
            {
                journal.GroupCatalogSha256 =
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupPath);
                CopyFileDurable(groupPath, context.GroupCatalogBackupPath);
            }

            Directory.CreateDirectory(context.CardFilesBackupRoot);
            string[] sourceFiles = Directory.Exists(cardFilesDirectory)
                ? Directory.GetFiles(cardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string fileName = Path.GetFileName(sourceFiles[i]);
                if (string.IsNullOrWhiteSpace(fileName)
                    || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException("行为卡备份文件名无效：" + fileName);
                }
                string sourcePath = Path.GetFullPath(sourceFiles[i]);
                if (!IsPathWithinRoot(sourcePath, cardFilesDirectory))
                    throw new InvalidDataException("行为卡备份源路径越界：" + sourcePath);
                string backupPath = Path.Combine(context.CardFilesBackupRoot, fileName);
                string hash = CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(sourcePath);
                CopyFileDurable(sourcePath, backupPath);
                journal.CardFiles.Add(new ProjectCatalogTransactionCardFileBackup
                {
                    FileName = fileName,
                    Sha256 = hash
                });
            }

            if (!VerifyBackupSnapshot(context, journal, out string backupDiagnostic))
                throw new InvalidDataException("项目目录事务备份未能完整持久化：" + backupDiagnostic);
            PersistJournal(context, journal);
            return journal;
        }

        private static bool VerifyBackupSnapshot(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            if (journal.CardCatalogExisted)
            {
                if (!VerifyFileHash(context.CardIndexBackupPath, journal.CardCatalogSha256)
                    || !VerifyFileHash(journal.CardCatalogPath, journal.CardCatalogSha256))
                {
                    diagnostic = "行为卡索引备份或源文件 hash 不一致。";
                    return false;
                }
            }
            else if (File.Exists(journal.CardCatalogPath))
            {
                diagnostic = "行为卡索引在备份期间被并发创建。";
                return false;
            }

            if (journal.GroupCatalogExisted)
            {
                if (!VerifyFileHash(context.GroupCatalogBackupPath, journal.GroupCatalogSha256)
                    || !VerifyFileHash(journal.GroupCatalogPath, journal.GroupCatalogSha256))
                {
                    diagnostic = "行为组目录备份或源文件 hash 不一致。";
                    return false;
                }
            }
            else if (File.Exists(journal.GroupCatalogPath))
            {
                diagnostic = "行为组目录在备份期间被并发创建。";
                return false;
            }

            string[] currentFiles = Directory.Exists(journal.CardFilesDirectory)
                ? Directory.GetFiles(journal.CardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(currentFiles, StringComparer.OrdinalIgnoreCase);
            if (currentFiles.Length != journal.CardFiles.Count)
            {
                diagnostic = "独立行为卡文件集合在备份期间发生变化。";
                return false;
            }

            for (int i = 0; i < journal.CardFiles.Count; i++)
            {
                ProjectCatalogTransactionCardFileBackup file = journal.CardFiles[i];
                if (!TryResolveCardBackupPaths(
                        context,
                        journal,
                        file,
                        out string sourcePath,
                        out string backupPath,
                        out diagnostic))
                {
                    return false;
                }
                if (!string.Equals(
                        Path.GetFileName(currentFiles[i]),
                        file.FileName,
                        StringComparison.OrdinalIgnoreCase)
                    || !VerifyFileHash(sourcePath, file.Sha256)
                    || !VerifyFileHash(backupPath, file.Sha256))
                {
                    diagnostic = "独立行为卡备份 hash 不一致：" + file.FileName;
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool VerifyFileHash(string path, string expectedHash)
        {
            return File.Exists(path)
                && !string.IsNullOrWhiteSpace(expectedHash)
                && string.Equals(
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(path),
                    expectedHash,
                    StringComparison.Ordinal);
        }

        private static bool TryResolveCardBackupPaths(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            ProjectCatalogTransactionCardFileBackup file,
            out string targetPath,
            out string backupPath,
            out string diagnostic)
        {
            targetPath = string.Empty;
            backupPath = string.Empty;
            string fileName = file?.FileName ?? string.Empty;
            if (file == null
                || string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                diagnostic = "事务 journal 包含无效行为卡备份文件名。";
                return false;
            }

            targetPath = Path.GetFullPath(Path.Combine(journal.CardFilesDirectory, fileName));
            backupPath = Path.GetFullPath(Path.Combine(context.CardFilesBackupRoot, fileName));
            if (!IsPathWithinRoot(targetPath, journal.CardFilesDirectory)
                || !IsPathWithinRoot(backupPath, context.CardFilesBackupRoot))
            {
                diagnostic = "事务 journal 的行为卡备份路径越界。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static void InjectFault(
            CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint point)
        {
            _testFaultInjection?.Invoke(point);
        }

        private static bool TryRecoverPendingTransactions(
            out bool recoveredAny,
            out string diagnostic)
        {
            recoveredAny = false;
            string recoveryRoot;
            string transactionsRoot;
            try
            {
                recoveryRoot = Path.GetFullPath(GetEffectiveRecoveryRoot());
                transactionsRoot = Path.GetFullPath(Path.Combine(
                    recoveryRoot,
                    TransactionsDirectoryName));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                diagnostic = "恢复根路径无效：" + exception.Message;
                return false;
            }

            if (!IsPathWithinRoot(transactionsRoot, recoveryRoot) || !Directory.Exists(transactionsRoot))
            {
                diagnostic = string.Empty;
                return true;
            }

            string[] transactionDirectories;
            try
            {
                transactionDirectories = Directory.GetDirectories(
                    transactionsRoot,
                    TransactionDirectoryPrefix + "*",
                    SearchOption.TopDirectoryOnly);
                Array.Sort(transactionDirectories, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                diagnostic = "无法枚举项目目录恢复事务：" + exception.Message;
                return false;
            }

            var failures = new StringBuilder();
            for (int i = 0; i < transactionDirectories.Length; i++)
            {
                string transactionRoot = Path.GetFullPath(transactionDirectories[i]);
                if (!IsPathWithinRoot(transactionRoot, recoveryRoot))
                {
                    AppendRecoveryFailure(failures, "恢复事务目录越界：" + transactionRoot);
                    continue;
                }

                string journalPath = Path.Combine(transactionRoot, JournalFileName);
                if (!File.Exists(journalPath))
                {
                    if (!TryDeleteTransactionDirectory(
                            transactionRoot,
                            recoveryRoot,
                            out string orphanDiagnostic))
                    {
                        AppendRecoveryFailure(failures, orphanDiagnostic);
                    }
                    continue;
                }

                if (!TryReadJournal(
                        journalPath,
                        recoveryRoot,
                        out ProjectCatalogTransactionContext context,
                        out ProjectCatalogTransactionJournal journal,
                        out string readDiagnostic))
                {
                    AppendRecoveryFailure(failures, readDiagnostic);
                    continue;
                }

                if (journal.Phase == ProjectCatalogTransactionPhase.Committed)
                {
                    if (!TryDeleteTransactionDirectory(
                            context.TransactionRoot,
                            recoveryRoot,
                            out string committedCleanupDiagnostic))
                    {
                        AppendRecoveryFailure(
                            failures,
                            "已提交事务的恢复记录无法清理：" + committedCleanupDiagnostic);
                    }
                    continue;
                }

                if (!TryRestoreTransaction(
                        context,
                        journal,
                        out string restoreDiagnostic))
                {
                    AppendRecoveryFailure(failures, restoreDiagnostic);
                    continue;
                }

                recoveredAny = true;
            }

            diagnostic = failures.ToString();
            return diagnostic.Length == 0;
        }

        private static bool TryReadJournal(
            string journalPath,
            string recoveryRoot,
            out ProjectCatalogTransactionContext context,
            out ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            context = null;
            journal = null;
            string transactionRoot = Path.GetDirectoryName(Path.GetFullPath(journalPath)) ?? string.Empty;
            if (!IsPathWithinRoot(transactionRoot, recoveryRoot))
            {
                diagnostic = "事务 journal 不在 ProjectCatalogRecovery 根内：" + journalPath;
                return false;
            }

            try
            {
                journal = JsonConvert.DeserializeObject<ProjectCatalogTransactionJournal>(
                    File.ReadAllText(journalPath, Encoding.UTF8));
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException)
            {
                diagnostic = "事务 journal 无法读取；已保留以供人工恢复："
                    + journalPath
                    + "；"
                    + exception.Message;
                return false;
            }

            if (journal == null
                || !string.Equals(journal.Schema, RecoverySchema, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(journal.TransactionId)
                || journal.CardFiles == null)
            {
                diagnostic = "事务 journal schema 或身份无效；已保留：" + journalPath;
                return false;
            }

            context = CreateTransactionContext(journal.TransactionId);
            if (!string.Equals(
                    Path.GetFullPath(context.TransactionRoot),
                    transactionRoot,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    Path.GetFullPath(context.JournalPath),
                    Path.GetFullPath(journalPath),
                    StringComparison.OrdinalIgnoreCase)
                || !IsSafeTransactionContext(context))
            {
                diagnostic = "事务 journal 与恢复目录身份不一致；已保留：" + journalPath;
                return false;
            }

            if (!TryValidateRecoveryTargets(context, journal, out diagnostic))
                return false;
            return true;
        }

        private static bool TryValidateRecoveryTargets(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            string streamingRoot = GetEffectiveStreamingRoot();
            if (!TryNormalizePath(journal.CatalogDirectory, out string catalogDirectory, out _)
                || !TryNormalizePath(journal.CardCatalogPath, out string cardPath, out _)
                || !TryNormalizePath(journal.GroupCatalogPath, out string groupPath, out _)
                || !TryNormalizePath(journal.CardFilesDirectory, out string cardFilesDirectory, out _))
            {
                diagnostic = "事务 journal 包含无效目标路径；已保留：" + context.JournalPath;
                return false;
            }

            string expectedCardDirectory = Path.GetDirectoryName(cardPath) ?? string.Empty;
            string expectedGroupDirectory = Path.GetDirectoryName(groupPath) ?? string.Empty;
            string expectedCardFilesDirectory = Path.GetFullPath(Path.Combine(
                catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName));
            if (!IsPathWithinRoot(catalogDirectory, streamingRoot)
                || !IsPathWithinRoot(cardPath, streamingRoot)
                || !IsPathWithinRoot(groupPath, streamingRoot)
                || !IsPathWithinRoot(cardFilesDirectory, streamingRoot)
                || !string.Equals(catalogDirectory, expectedCardDirectory, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(catalogDirectory, expectedGroupDirectory, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    cardFilesDirectory,
                    expectedCardFilesDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                diagnostic = "事务 journal 的正式目标不在当前 StreamingAssets 根内；已保留："
                    + context.JournalPath;
                return false;
            }

            journal.CatalogDirectory = catalogDirectory;
            journal.CardCatalogPath = cardPath;
            journal.GroupCatalogPath = groupPath;
            journal.CardFilesDirectory = cardFilesDirectory;
            diagnostic = string.Empty;
            return true;
        }

        private static bool TryRestoreTransaction(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            if (!VerifyRecoveryBackups(context, journal, out diagnostic))
            {
                diagnostic = "无法证明项目目录备份完整，事务 journal 已保留："
                    + context.JournalPath
                    + "；"
                    + diagnostic;
                return false;
            }

            try
            {
                RestoreCardFilesFromTransaction(context, journal);
                RestoreCatalogFile(
                    journal.CardCatalogPath,
                    context.CardIndexBackupPath,
                    journal.CardCatalogExisted);
                RestoreCatalogFile(
                    journal.GroupCatalogPath,
                    context.GroupCatalogBackupPath,
                    journal.GroupCatalogExisted);
                RestoreOriginalDirectoryShape(journal);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidDataException)
            {
                diagnostic = "项目目录恢复执行失败，事务 journal 已保留："
                    + context.JournalPath
                    + "；"
                    + exception.Message;
                return false;
            }

            if (!VerifyRestoredState(context, journal, out string verificationDiagnostic))
            {
                diagnostic = "无法证明项目目录已完整恢复，事务 journal 已保留："
                    + context.JournalPath
                    + "；"
                    + verificationDiagnostic;
                return false;
            }

            if (!TryDeleteTransactionDirectory(
                    context.TransactionRoot,
                    context.RecoveryRoot,
                    out string cleanupDiagnostic))
            {
                diagnostic = "项目目录已恢复，但事务 journal 无法清理：" + cleanupDiagnostic;
                return false;
            }

            RefreshAssetsAfterCatalogMutation();
            diagnostic = string.Empty;
            return true;
        }

        private static bool VerifyRecoveryBackups(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            if (journal.CardCatalogExisted
                && !VerifyFileHash(context.CardIndexBackupPath, journal.CardCatalogSha256))
            {
                diagnostic = "行为卡索引事务备份缺失或 hash 不一致。";
                return false;
            }
            if (journal.GroupCatalogExisted
                && !VerifyFileHash(context.GroupCatalogBackupPath, journal.GroupCatalogSha256))
            {
                diagnostic = "行为组目录事务备份缺失或 hash 不一致。";
                return false;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < journal.CardFiles.Count; i++)
            {
                ProjectCatalogTransactionCardFileBackup file = journal.CardFiles[i];
                if (!TryResolveCardBackupPaths(
                        context,
                        journal,
                        file,
                        out _,
                        out string backupPath,
                        out diagnostic))
                {
                    return false;
                }
                if (!names.Add(file.FileName) || !VerifyFileHash(backupPath, file.Sha256))
                {
                    diagnostic = "独立行为卡事务备份缺失、重复或 hash 不一致："
                        + (file?.FileName ?? "<unknown>");
                    return false;
                }
            }

            string[] actualBackups = Directory.Exists(context.CardFilesBackupRoot)
                ? Directory.GetFiles(context.CardFilesBackupRoot, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            if (actualBackups.Length != journal.CardFiles.Count)
            {
                diagnostic = "独立行为卡事务备份集合与 journal 不一致。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static void RestoreCardFilesFromTransaction(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal)
        {
            Directory.CreateDirectory(journal.CardFilesDirectory);
            for (int i = 0; i < journal.CardFiles.Count; i++)
            {
                ProjectCatalogTransactionCardFileBackup file = journal.CardFiles[i];
                if (!TryResolveCardBackupPaths(
                        context,
                        journal,
                        file,
                        out string targetPath,
                        out string backupPath,
                        out string diagnostic))
                {
                    throw new InvalidDataException(diagnostic);
                }
                CopyFileDurable(backupPath, targetPath);
            }

            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < journal.CardFiles.Count; i++)
                expected.Add(journal.CardFiles[i].FileName);
            string[] current = Directory.GetFiles(
                journal.CardFilesDirectory,
                "*.json",
                SearchOption.TopDirectoryOnly);
            for (int i = 0; i < current.Length; i++)
            {
                if (!expected.Contains(Path.GetFileName(current[i])))
                    File.Delete(current[i]);
            }
        }

        private static void RestoreCatalogFile(
            string targetPath,
            string backupPath,
            bool originallyExisted)
        {
            if (originallyExisted)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
                CopyFileDurable(backupPath, targetPath);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        private static void RestoreOriginalDirectoryShape(ProjectCatalogTransactionJournal journal)
        {
            if (!journal.CardFilesDirectoryExisted && Directory.Exists(journal.CardFilesDirectory))
            {
                if (Directory.GetFileSystemEntries(journal.CardFilesDirectory).Length > 0)
                {
                    throw new InvalidDataException(
                        "原行为卡目录不存在，但恢复后仍有无法归属的文件。" );
                }
                Directory.Delete(journal.CardFilesDirectory, false);
            }

            if (!journal.CatalogDirectoryExisted && Directory.Exists(journal.CatalogDirectory))
            {
                if (Directory.GetFileSystemEntries(journal.CatalogDirectory).Length > 0)
                    throw new InvalidDataException("原 Catalog 目录不存在，但恢复后仍有无法归属的文件。");
                Directory.Delete(journal.CatalogDirectory, false);
            }
        }

        private static bool VerifyRestoredState(
            ProjectCatalogTransactionContext context,
            ProjectCatalogTransactionJournal journal,
            out string diagnostic)
        {
            if (journal.CardCatalogExisted != File.Exists(journal.CardCatalogPath)
                || (journal.CardCatalogExisted
                    && !VerifyFileHash(journal.CardCatalogPath, journal.CardCatalogSha256)))
            {
                diagnostic = "行为卡索引未恢复到事务前 hash。";
                return false;
            }
            if (journal.GroupCatalogExisted != File.Exists(journal.GroupCatalogPath)
                || (journal.GroupCatalogExisted
                    && !VerifyFileHash(journal.GroupCatalogPath, journal.GroupCatalogSha256)))
            {
                diagnostic = "行为组目录未恢复到事务前 hash。";
                return false;
            }
            if (journal.CardFilesDirectoryExisted != Directory.Exists(journal.CardFilesDirectory))
            {
                diagnostic = "独立行为卡目录存在性未恢复。";
                return false;
            }

            string[] currentFiles = Directory.Exists(journal.CardFilesDirectory)
                ? Directory.GetFiles(journal.CardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            if (currentFiles.Length != journal.CardFiles.Count)
            {
                diagnostic = "独立行为卡文件数量未恢复。";
                return false;
            }
            for (int i = 0; i < journal.CardFiles.Count; i++)
            {
                ProjectCatalogTransactionCardFileBackup file = journal.CardFiles[i];
                if (!TryResolveCardBackupPaths(
                        context,
                        journal,
                        file,
                        out string targetPath,
                        out _,
                        out diagnostic))
                {
                    return false;
                }
                if (!VerifyFileHash(targetPath, file.Sha256))
                {
                    diagnostic = "独立行为卡文件未恢复到事务前 hash：" + file.FileName;
                    return false;
                }
            }
            if (journal.CatalogDirectoryExisted != Directory.Exists(journal.CatalogDirectory))
            {
                diagnostic = "Catalog 目录存在性未恢复。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool TryDeleteTransactionDirectory(
            string transactionRoot,
            string recoveryRoot,
            out string diagnostic)
        {
            try
            {
                string normalizedTransactionRoot = Path.GetFullPath(transactionRoot);
                if (!IsPathWithinRoot(normalizedTransactionRoot, recoveryRoot))
                {
                    diagnostic = "拒绝清理 ProjectCatalogRecovery 之外的事务目录："
                        + normalizedTransactionRoot;
                    return false;
                }
                if (Directory.Exists(normalizedTransactionRoot))
                    Directory.Delete(normalizedTransactionRoot, true);
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                diagnostic = transactionRoot + "；" + exception.Message;
                return false;
            }
        }

        private static void AppendRecoveryFailure(StringBuilder failures, string message)
        {
            if (failures.Length > 0)
                failures.Append(" | ");
            failures.Append(message);
        }

        private static void InstallCatalogFile(
            string stagedPath,
            string targetPath,
            string legacyPreviousPath,
            bool targetExisted,
            string recoveryRoot)
        {
            if (!IsPathWithinRoot(targetPath, GetEffectiveStreamingRoot())
                || !IsPathWithinRoot(legacyPreviousPath, recoveryRoot))
            {
                throw new InvalidDataException("项目目录安装路径越界。");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPreviousPath) ?? string.Empty);
            if (targetExisted)
                File.Replace(stagedPath, targetPath, legacyPreviousPath, true);
            else
                File.Move(stagedPath, targetPath);
        }
    }
}
