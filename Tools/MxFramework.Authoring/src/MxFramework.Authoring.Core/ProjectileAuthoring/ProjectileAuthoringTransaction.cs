using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    /// <summary>Provides guarded, recoverable authoring file transactions.</summary>
    public static class ProjectileAuthoringTransaction
    {
        /// <summary>Commits a guarded file set and rolls back every prior replacement on failure.</summary>
        /// <remarks>LowFreqAlloc: authoring-time staging, hashing, backup and receipt creation.</remarks>
        public static ProjectileAuthoringTransactionReceipt Execute(IReadOnlyList<ProjectileAuthoringTransactionItem> items)
        {
            return Execute(items, null);
        }

        /// <summary>Commits a guarded file set with an optional deterministic fault seam.</summary>
        /// <remarks>LowFreqAlloc: intended for authoring integration validation of rollback behavior.</remarks>
        public static ProjectileAuthoringTransactionReceipt Execute(
            IReadOnlyList<ProjectileAuthoringTransactionItem> items,
            IProjectileAuthoringTransactionFaultInjector faultInjector)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            var receipt = new ProjectileAuthoringTransactionReceipt();
            var states = new List<CommitState>(items.Count);
            string transactionId = Guid.NewGuid().ToString("N");
            try
            {
                ValidateAndStage(items, states, transactionId, receipt);
            }
            catch (ProjectileAuthoringHashConflictException)
            {
                receipt.Status = "Blocked";
                receipt.DiagnosticCodes.Add("PROJAUTH006");
                Cleanup(states, false);
                return receipt;
            }
            catch
            {
                receipt.Status = "Blocked";
                receipt.DiagnosticCodes.Add("PROJAUTH_TRANSACTION_STAGE_FAILED");
                Cleanup(states, false);
                return receipt;
            }

            bool preserveRecovery = false;
            try
            {
                for (int i = 0; i < states.Count; i++)
                {
                    CommitState state = states[i];
                    faultInjector?.BeforeCommit(i, state.Item.ReceiptPath);
                    if (!MatchesOriginalState(state))
                        throw new ProjectileAuthoringHashConflictException();
                    if (state.Item.Delete)
                    {
                        if (!state.Existed)
                            throw new IOException("Delete target does not exist.");
                        File.Move(state.TargetPath, state.BackupPath);
                    }
                    else if (state.Existed)
                        File.Replace(state.StagePath, state.TargetPath, state.BackupPath, true);
                    else
                        File.Move(state.StagePath, state.TargetPath);
                    state.Committed = true;
                }
                for (int i = 0; i < states.Count; i++)
                {
                    if (!MatchesCommittedState(states[i]))
                        throw new IOException("Committed authoring target failed final hash verification.");
                }
                receipt.Status = "Committed";
                receipt.RollbackResult = "NotRequired";
                DeleteBackups(states);
                return receipt;
            }
            catch (ProjectileAuthoringHashConflictException)
            {
                bool restored = Rollback(states, faultInjector, receipt);
                preserveRecovery = !restored;
                receipt.Status = restored ? "Blocked" : "RecoveryRequired";
                receipt.RollbackResult = restored ? "Succeeded" : "Failed";
                receipt.DiagnosticCodes.Add("PROJAUTH006");
                return receipt;
            }
            catch
            {
                bool restored = Rollback(states, faultInjector, receipt);
                preserveRecovery = !restored;
                receipt.Status = restored ? "RolledBack" : "RecoveryRequired";
                receipt.RollbackResult = restored ? "Succeeded" : "Failed";
                receipt.DiagnosticCodes.Add("PROJAUTH_TRANSACTION_COMMIT_FAILED");
                return receipt;
            }
            finally
            {
                Cleanup(states, preserveRecovery);
            }
        }

        private static void ValidateAndStage(
            IReadOnlyList<ProjectileAuthoringTransactionItem> items,
            List<CommitState> states,
            string transactionId,
            ProjectileAuthoringTransactionReceipt receipt)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                ProjectileAuthoringTransactionItem item = items[i] ?? throw new ArgumentException("Transaction item cannot be null.", nameof(items));
                if (string.IsNullOrWhiteSpace(item.TargetPath) || string.IsNullOrWhiteSpace(item.ReceiptPath))
                    throw new ArgumentException("Transaction target and receipt path are required.", nameof(items));
                string target = Path.GetFullPath(item.TargetPath);
                if (!targets.Add(target))
                    throw new ArgumentException("Duplicate transaction target: " + item.ReceiptPath, nameof(items));
                bool existed = File.Exists(target);
                string before = HashCurrent(target);
                string expected = string.IsNullOrWhiteSpace(item.ExpectedSha256) ? ProjectileAuthoringHashes.Missing : item.ExpectedSha256;
                string directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("Target has no parent directory.");
                string fileName = Path.GetFileName(target);
                string stage = Path.Combine(directory, "." + fileName + "." + transactionId + ".stage");
                string backup = Path.Combine(directory, "." + fileName + "." + transactionId + ".backup");
                string rollbackCurrent = Path.Combine(directory, "." + fileName + "." + transactionId + ".rollback-current");
                string after = item.Delete ? ProjectileAuthoringHashes.Missing : ProjectileAuthoringHashes.ComputeSha256(item.Content);
                states.Add(new CommitState(item, target, stage, backup, rollbackCurrent, existed, before, after));
                receipt.TouchedFiles.Add(new ProjectileAuthoringTransactionFileReceipt
                {
                    Path = NormalizeReceiptPath(item.ReceiptPath),
                    BeforeSha256 = before,
                    AfterSha256 = after,
                });
                if (!string.Equals(before, expected, StringComparison.OrdinalIgnoreCase))
                    throw new ProjectileAuthoringHashConflictException();
            }

            for (int i = 0; i < states.Count; i++)
            {
                CommitState state = states[i];
                Directory.CreateDirectory(Path.GetDirectoryName(state.TargetPath) ?? string.Empty);
                if (!state.Item.Delete)
                    File.WriteAllBytes(state.StagePath, state.Item.Content ?? Array.Empty<byte>());
            }
        }

        private static bool MatchesOriginalState(CommitState state)
        {
            return string.Equals(HashCurrent(state.TargetPath), state.BeforeSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesCommittedState(CommitState state)
        {
            return string.Equals(HashCurrent(state.TargetPath), state.AfterSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashCurrent(string path)
        {
            return File.Exists(path)
                ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path))
                : ProjectileAuthoringHashes.Missing;
        }

        private static bool Rollback(
            List<CommitState> states,
            IProjectileAuthoringTransactionFaultInjector faultInjector,
            ProjectileAuthoringTransactionReceipt receipt)
        {
            bool success = true;
            for (int i = states.Count - 1; i >= 0; i--)
            {
                CommitState state = states[i];
                if (!state.Committed)
                    continue;
                try
                {
                    faultInjector?.BeforeRollback(state.Item.ReceiptPath);
                    if (!string.Equals(HashCurrent(state.TargetPath), state.AfterSha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Committed target changed after this transaction wrote it.");
                    if (state.Item.Delete)
                    {
                        if (!File.Exists(state.BackupPath))
                            throw new IOException("Transaction backup is missing.");
                        File.Move(state.BackupPath, state.TargetPath);
                    }
                    else if (state.Existed)
                    {
                        if (!File.Exists(state.BackupPath))
                            throw new IOException("Transaction backup is missing.");
                        if (File.Exists(state.TargetPath))
                        {
                            File.Replace(state.BackupPath, state.TargetPath, state.RollbackCurrentPath, true);
                            TryDelete(state.RollbackCurrentPath);
                        }
                        else
                        {
                            File.Move(state.BackupPath, state.TargetPath);
                        }
                    }
                    else if (File.Exists(state.TargetPath))
                    {
                        File.Delete(state.TargetPath);
                    }
                    state.Restored = true;
                }
                catch
                {
                    success = false;
                    AddRecoveryInformation(state, receipt);
                }
            }
            return success;
        }

        private static void AddRecoveryInformation(CommitState state, ProjectileAuthoringTransactionReceipt receipt)
        {
            string parent = NormalizeReceiptPath(Path.GetDirectoryName(state.Item.ReceiptPath) ?? string.Empty);
            if (state.Existed && File.Exists(state.BackupPath))
                AddUnique(receipt.RecoveryFiles, CombineReceiptPath(parent, Path.GetFileName(state.BackupPath)));
            else if (!state.Existed && File.Exists(state.TargetPath))
                AddUnique(receipt.RecoveryFiles, NormalizeReceiptPath(state.Item.ReceiptPath));
            if (File.Exists(state.RollbackCurrentPath))
                AddUnique(receipt.RecoveryFiles, CombineReceiptPath(parent, Path.GetFileName(state.RollbackCurrentPath)));
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }

        private static string CombineReceiptPath(string parent, string file)
        {
            return string.IsNullOrEmpty(parent) ? file : parent.TrimEnd('/') + "/" + file;
        }

        private static string NormalizeReceiptPath(string value) => (value ?? string.Empty).Replace('\\', '/');

        private static void DeleteBackups(List<CommitState> states)
        {
            for (int i = 0; i < states.Count; i++)
                TryDelete(states[i].BackupPath);
        }

        private static void Cleanup(List<CommitState> states, bool preserveRecovery)
        {
            for (int i = 0; i < states.Count; i++)
            {
                TryDelete(states[i].StagePath);
                if (!preserveRecovery || states[i].Restored || !states[i].Committed)
                {
                    TryDelete(states[i].BackupPath);
                    TryDelete(states[i].RollbackCurrentPath);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup; recovery files are retained when rollback did not succeed.
            }
        }

        private sealed class CommitState
        {
            public CommitState(
                ProjectileAuthoringTransactionItem item,
                string targetPath,
                string stagePath,
                string backupPath,
                string rollbackCurrentPath,
                bool existed,
                string beforeSha256,
                string afterSha256)
            {
                Item = item;
                TargetPath = targetPath;
                StagePath = stagePath;
                BackupPath = backupPath;
                RollbackCurrentPath = rollbackCurrentPath;
                Existed = existed;
                BeforeSha256 = beforeSha256;
                AfterSha256 = afterSha256;
            }

            public ProjectileAuthoringTransactionItem Item { get; }
            public string TargetPath { get; }
            public string StagePath { get; }
            public string BackupPath { get; }
            public string RollbackCurrentPath { get; }
            public bool Existed { get; }
            public string BeforeSha256 { get; }
            public string AfterSha256 { get; }
            public bool Committed { get; set; }
            public bool Restored { get; set; }
        }

        private sealed class ProjectileAuthoringHashConflictException : Exception
        {
        }
    }
}
