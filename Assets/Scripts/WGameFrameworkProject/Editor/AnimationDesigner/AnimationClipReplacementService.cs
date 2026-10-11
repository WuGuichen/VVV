using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal sealed class ClipReplacementResult
    {
        internal bool Success;
        internal bool Changed;
        internal string Message;
        internal string Commit;
    }

    internal sealed class ClipReplacementHistory
    {
        internal bool CanUndo;
        internal string Message;
    }

    internal static class AnimationClipReplacementService
    {
        internal static bool Busy { get; private set; }
        internal static string Message { get; private set; } = "自动保存新版并替换提交；无引用的新版副本会清理，被引用的副本会保留。";
        internal static event Action Changed;

        internal static Task<ClipReplacementResult> ReplaceAsync(AnimationClip target, AnimationClip source) => ExecuteAsync(target, source, false);
        internal static Task<ClipReplacementResult> UndoAsync(AnimationClip target) => ExecuteAsync(target, null, true);

        internal static async Task<ClipReplacementHistory> ReadHistoryAsync(AnimationClip target)
        {
            try
            {
                string path = ClipPath(target);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long _);
                var git = new AnimationClipGitRepository(ProjectRoot);
                return await Task.Run(() =>
                {
                    var baseline = git.Prepare(path);
                    var undo = git.FindUndo(path, guid);
                    if (undo == null) return new ClipReplacementHistory { Message = "这个动画还没有可撤回的快速替换提交。" };
                    bool current = undo.Blob == baseline.Blob && undo.MetaBlob == baseline.MetaBlob;
                    return new ClipReplacementHistory { CanUndo = current, Message = current
                        ? "可撤回替换 " + undo.Commit.Substring(0, 8) + "；仅恢复这个动画并生成恢复提交。"
                        : "动画在该次替换后又发生变化，不能直接撤回旧替换。" };
                });
            }
            catch (Exception ex) { return new ClipReplacementHistory { Message = ex.Message }; }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string ClipPath(AnimationClip clip, bool requireSaved = true)
        {
            string path = clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty;
            if (clip == null || AssetDatabase.IsSubAsset(clip) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择 Assets 下独立的 .anim 文件；FBX 子片段需要先导出。");
            if (requireSaved && EditorUtility.IsDirty(clip)) throw new InvalidOperationException("旧动画仍有未保存修改，请先保存该动画；新版会在替换时自动保存。");
            return path;
        }

        private static async Task<ClipReplacementResult> ExecuteAsync(AnimationClip target, AnimationClip source, bool undoing)
        {
            if (Busy) return new ClipReplacementResult { Message = "另一个动画替换操作尚未结束。" };
            AnimationClip prepared = null;
            ClipGitBaseline baseline = null;
            Action<bool> resume = null;
            FileStream lease = null;
            var git = new AnimationClipGitRepository(ProjectRoot);
            var receipt = new Receipt { operation = Guid.NewGuid().ToString("N"), action = undoing ? "undo" : "replace" };
            string ownedBlob = null;
            string receiptPath = null;
            string originalBackup = null;
            AnimationClipExportCleanup cleanup = null;
            bool cleanupPending = false;
            bool locked = false, refreshBlocked = false, resetPreview = false, commitCreated = false;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("请在 Editor 就绪且未运行游戏时替换动画。");
                receipt.targetPath = ClipPath(target);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out receipt.targetGuid, out long localId))
                    throw new InvalidOperationException("无法读取目标动画的资源身份。");
                if (!undoing)
                {
                    receipt.sourcePath = ClipPath(source, requireSaved: false);
                    if (string.Equals(receipt.targetPath, receipt.sourcePath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("新版与旧版不能是同一个资源，请先另存导出新版。");
                    if (target.isHumanMotion != source.isHumanMotion || target.legacy != source.legacy)
                        throw new InvalidOperationException("新版与旧版的动画类型不同，请先核对 Humanoid / Generic / Legacy 导出模式。");
                }
                Busy = true;
                SetMessage(undoing ? "正在检查撤回版本…" : "正在检查目标动画与 Git 状态…");
                EditorApplication.LockReloadAssemblies(); locked = true;
                AssetDatabase.DisallowAutoRefresh(); refreshBlocked = true;
                string directory = Path.Combine(git.Root, "Library/AnimationDesignerReplacements");
                Directory.CreateDirectory(directory);
                lease = new FileStream(Path.Combine(directory, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                receiptPath = Path.Combine(directory, receipt.operation + ".json");
                baseline = await Task.Run(() => git.Prepare(receipt.targetPath));
                if (!undoing)
                {
                    await Task.Run(() => git.RequireUntrackedExport(receipt.sourcePath));
                    SetMessage("正在保存新版并检查副本引用…");
                    SaveReplacementSource(source);
                    cleanup = AnimationClipExportCleanup.Capture(source, allowReferenced: true);
                    receipt.sourceRetentionReason = cleanup.ReferenceDiagnostic;
                    prepared = Object.Instantiate(source);
                }
                receipt.previousHead = baseline.Head;
                receipt.previousBlob = baseline.Blob;
                ClipUndoRevision undo = undoing ? await Task.Run(() => git.FindUndo(receipt.targetPath, receipt.targetGuid)) : null;
                if (undoing && undo == null) throw new InvalidOperationException("没有可撤回的快速替换提交。");
                await Task.Run(() => git.VerifyBaseline(baseline));
                target = AssetDatabase.LoadAssetAtPath<AnimationClip>(receipt.targetPath);
                ClipPath(target);
                receipt.targetBackup = receipt.operation + ".target.anim";
                originalBackup = Path.Combine(directory, receipt.targetBackup);
                File.Copy(Path.Combine(git.Root, receipt.targetPath), originalBackup);
                if (await Task.Run(() => git.FileBlob(originalBackup, receipt.targetPath)) != baseline.Blob)
                    throw new InvalidOperationException("目标动画备份与已提交版本不一致，未开始替换。");
                await Task.Run(() => git.VerifyBaseline(baseline));
                string setup = AnimationDesignerSession.CaptureSetupForClip(target) ?? AnimationDesignerSession.CaptureSetupForClip(source);
                if (!string.IsNullOrEmpty(setup))
                {
                    receipt.designerSetupBackup = receipt.operation + ".designer.json";
                    File.WriteAllText(Path.Combine(directory, receipt.designerSetupBackup), setup);
                }
                receipt.state = "prepared"; SaveReceipt(receiptPath, receipt);
                resume = AnimationDesignerSession.SuspendForClipChange(target, source);
                if (undoing)
                {
                    receipt.reverts = undo.Commit;
                    await Task.Run(() => git.RestorePrevious(baseline, undo));
                }
                else
                {
                    CopyClipContent(prepared, target);
                }
                ownedBlob = await Task.Run(() => git.WorktreeBlob(receipt.targetPath));
                receipt.newBlob = ownedBlob;
                SaveReceipt(receiptPath, receipt);
                if (!undoing)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string actualGuid, out long actualId);
                    if (actualGuid != receipt.targetGuid || actualId != localId)
                        throw new InvalidOperationException("动画资源身份检查失败，停止提交。");
                }
                if (ownedBlob == baseline.Blob)
                {
                    if (cleanup != null && cleanup.ReferenceDiagnostic == null)
                    {
                        await Task.Run(() => git.RequireUntrackedExport(receipt.sourcePath));
                        cleanup.DeleteAfterReplacement();
                        receipt.sourceDeleted = true;
                        resetPreview = true;
                    }
                    receipt.state = "unchanged"; SaveReceipt(receiptPath, receipt);
                    SetMessage(receipt.sourceRetentionReason != null
                        ? "动画内容已一致，无需提交；新版副本已保留。" + receipt.sourceRetentionReason
                        : cleanup != null ? "动画内容已一致，已删除新版副本，只保留原名文件。" : "动画内容一致，无需提交。");
                    return new ClipReplacementResult { Success = true, Message = Message };
                }
                SetMessage(undoing ? "正在提交该动画的恢复版本…" : "正在提交替换后的动画…");
                receipt.commit = await Task.Run(() => git.Commit(baseline, ownedBlob, receipt.targetGuid, receipt.operation, receipt.reverts));
                // The Git commit is authoritative even if writing a local receipt fails afterward.
                resetPreview = true;
                commitCreated = true;
                receipt.state = "committed";
                try { SaveReceipt(receiptPath, receipt); }
                catch (IOException) { /* Undo history can be reconstructed from Git commit trailers. */ }
                AssetDatabase.ImportAsset(receipt.targetPath, ImportAssetOptions.ForceUpdate);
                if (cleanup != null && cleanup.ReferenceDiagnostic == null)
                {
                    cleanupPending = true;
                    SetMessage("动画已提交，正在清理新版副本…");
                    await Task.Run(() => git.RequireUntrackedExport(receipt.sourcePath));
                    cleanup.DeleteAfterReplacement();
                    receipt.sourceDeleted = true;
                    cleanupPending = false;
                }
                receipt.indexLockedAfterCommit = await Task.Run(() => git.HasIndexLock());
                try { SaveReceipt(receiptPath, receipt); } catch (IOException) { }
                SetMessage((undoing ? "已撤回并提交 " : "已替换并提交 ") + receipt.commit.Substring(0, 8) + " · " + Path.GetFileName(receipt.targetPath)
                    + (receipt.sourceDeleted ? "；新版副本已删除。" : string.Empty)
                    + (receipt.sourceRetentionReason != null ? "；新版副本已保留。" + receipt.sourceRetentionReason : string.Empty)
                    + (receipt.indexLockedAfterCommit ? "；检测到 Git 索引锁，请确认 Git 操作已结束后再清理。" : string.Empty));
                return new ClipReplacementResult { Success = true, Changed = true, Message = Message, Commit = receipt.commit };
            }
            catch (Exception ex)
            {
                string recovery = string.Empty;
                if (baseline != null && ownedBlob != null && !commitCreated)
                {
                    var result = await Task.Run(() =>
                    {
                        bool restored = git.TryRollback(baseline, ownedBlob, receipt.operation, out string detail, originalBackup);
                        return Tuple.Create(restored, detail);
                    });
                    recovery = " " + result.Item2;
                    if (result.Item1) AssetDatabase.ImportAsset(receipt.targetPath, ImportAssetOptions.ForceUpdate);
                }
                receipt.state = commitCreated ? (cleanupPending ? "committed-needs-cleanup" : "committed-needs-refresh") : "failed";
                receipt.message = ex.GetBaseException().Message + recovery;
                if (receiptPath != null)
                    try { SaveReceipt(receiptPath, receipt); } catch (IOException) { }
                SetMessage((commitCreated ? (cleanupPending ? "动画已提交，但新版副本尚未清理：" : "Git 提交已完成，但编辑器刷新未完成：") : "操作未完成：") + receipt.message);
                return new ClipReplacementResult { Success = false, Changed = commitCreated, Commit = receipt.commit, Message = Message };
            }
            finally
            {
                try { resume?.Invoke(resetPreview); }
                catch (Exception ex) { SetMessage(Message + " 预览恢复失败：" + ex.GetBaseException().Message); }
                if (prepared != null) Object.DestroyImmediate(prepared);
                lease?.Dispose();
                if (refreshBlocked) AssetDatabase.AllowAutoRefresh();
                if (locked) EditorApplication.UnlockReloadAssemblies();
                Busy = false;
                Changed?.Invoke();
            }
        }

        private static void SetMessage(string message) { Message = message; Changed?.Invoke(); }

        internal static void SaveReplacementSource(AnimationClip source)
        {
            ClipPath(source, requireSaved: false);
            AssetDatabase.SaveAssetIfDirty(source);
            if (EditorUtility.IsDirty(source)) throw new InvalidOperationException("新版动画保存失败，未开始替换。");
        }

        internal static void CopyClipContent(AnimationClip source, AnimationClip target)
        {
            string name = target.name;
            HideFlags flags = target.hideFlags;
            EditorUtility.CopySerialized(source, target);
            target.name = name;
            target.hideFlags = flags;
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        private static void SaveReceipt(string path, Receipt receipt)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(receipt, true));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        [Serializable]
        private sealed class Receipt
        {
            public string operation, action, targetPath, targetGuid, sourcePath, previousHead, previousBlob;
            public string newBlob, reverts, commit, state, message;
            public string designerSetupBackup, targetBackup, sourceRetentionReason;
            public bool sourceDeleted, indexLockedAfterCommit;
        }
    }
}
