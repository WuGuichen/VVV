using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal sealed class ClipGitBaseline
    {
        internal string Path;
        internal string Head;
        internal string Branch;
        internal string Blob;
        internal string MetaBlob;
    }

    internal sealed class ClipUndoRevision
    {
        internal string Commit;
        internal string Previous;
        internal string Blob;
        internal string MetaBlob;
    }

    /// <summary>Git operations are restricted to a tracked, clean clip. Never resets a branch or commits the shared index.</summary>
    internal sealed class AnimationClipGitRepository
    {
        private const string OperationTag = "Animation-Designer-Operation: ";
        private const string AssetTag = "Animation-Designer-Asset: ";
        private const string ActionTag = "Animation-Designer-Action: ";
        private const string RevertsTag = "Animation-Designer-Reverts: ";
        internal string Root { get; }

        internal AnimationClipGitRepository(string root)
        {
            Root = System.IO.Path.GetFullPath(root);
        }

        internal ClipGitBaseline Prepare(string path)
        {
            ValidatePath(path);
            string top = Git("rev-parse", "--show-toplevel").Trim();
            if (!string.Equals(System.IO.Path.GetFullPath(top), Root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("目标不是当前项目 Git 仓库根目录。");
            string branch = Git("symbolic-ref", "--quiet", "HEAD").Trim();
            foreach (string state in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-apply", "rebase-merge", "sequencer" })
            {
                string statePath = Git("rev-parse", "--git-path", state).Trim();
                if (!System.IO.Path.IsPathRooted(statePath)) statePath = System.IO.Path.Combine(Root, statePath);
                if (File.Exists(statePath) || Directory.Exists(statePath))
                    throw new InvalidOperationException("Git 正在合并、变基或恢复提交，请先完成该操作。");
            }
            RequireUnlockedIndex();
            string head = Git("rev-parse", "--verify", "HEAD").Trim();
            string blob = Blob(head, path);
            string meta = Blob(head, path + ".meta");
            string flags = Git("ls-files", "-v", "--", path, path + ".meta");
            foreach (string line in flags.Split('\n'))
                if (line.Length > 0 && line[0] != 'H')
                    throw new InvalidOperationException("目标动画有特殊 Git 索引标记，请先取消 assume-unchanged / skip-worktree。");
            if (!string.IsNullOrWhiteSpace(Git("--no-optional-locks", "status", "--porcelain=v1", "--untracked-files=all", "--", path, path + ".meta"))
                || WorktreeBlob(path) != blob || WorktreeBlob(path + ".meta") != meta)
                throw new InvalidOperationException("目标动画或 .meta 有未提交修改。请先提交或还原这个动画，再执行快速替换。");
            return new ClipGitBaseline { Path = path, Head = head, Branch = branch, Blob = blob, MetaBlob = meta };
        }

        internal void VerifyBaseline(ClipGitBaseline baseline)
        {
            RequireUnlockedIndex();
            if (Git("rev-parse", "HEAD").Trim() != baseline.Head
                || Git("symbolic-ref", "--quiet", "HEAD").Trim() != baseline.Branch
                || WorktreeBlob(baseline.Path) != baseline.Blob
                || WorktreeBlob(baseline.Path + ".meta") != baseline.MetaBlob
                || !string.IsNullOrWhiteSpace(Git("diff", "--cached", "--name-only", "--", baseline.Path, baseline.Path + ".meta")))
                throw new InvalidOperationException("准备期间 Git 分支、提交或目标动画发生变化，请重试。");
        }

        internal string WorktreeBlob(string path) => Git("hash-object", "--path=" + path, "--", path).Trim();
        internal string Blob(string revision, string path) => Git("rev-parse", "--verify", revision + ":" + path).Trim();
        internal string FileBlob(string file, string path) => Git("hash-object", "--path=" + path, "--", file).Trim();

        internal bool HasIndexLock()
        {
            string lockPath = Git("rev-parse", "--git-path", "index.lock").Trim();
            if (!System.IO.Path.IsPathRooted(lockPath)) lockPath = System.IO.Path.Combine(Root, lockPath);
            return File.Exists(lockPath);
        }

        private void RequireUnlockedIndex()
        {
            if (HasIndexLock())
                throw new InvalidOperationException("Git 索引被 index.lock 锁定，无法提交动画。请先结束占用仓库的 Git 操作；若确认没有 Git 进程，再清理过期锁文件后重试。");
        }

        internal void RequireUntrackedExport(string path)
        {
            ValidatePath(path);
            if (!string.IsNullOrWhiteSpace(Git("ls-files", "--", path, path + ".meta")))
                throw new InvalidOperationException("新版已被 Git 跟踪，不能作为临时导出副本删除。请另存导出一个新副本后再替换。");
        }

        internal string Commit(ClipGitBaseline baseline, string newBlob, string assetGuid, string operation, string revertedCommit = null)
        {
            if (Git("rev-parse", "HEAD").Trim() != baseline.Head
                || Git("symbolic-ref", "--quiet", "HEAD").Trim() != baseline.Branch
                || WorktreeBlob(baseline.Path) != newBlob || WorktreeBlob(baseline.Path + ".meta") != baseline.MetaBlob
                || !string.IsNullOrWhiteSpace(Git("diff", "--cached", "--name-only", "--", baseline.Path, baseline.Path + ".meta")))
                throw new InvalidOperationException("提交前目标或 Git 状态发生变化；未自动覆盖并发修改。");
            string action = revertedCommit == null ? "replace" : "undo";
            string message = "animation: " + action + " " + System.IO.Path.GetFileNameWithoutExtension(baseline.Path)
                + "\n\n" + OperationTag + operation + "\n" + AssetTag + assetGuid + "\n" + ActionTag + action
                + (revertedCommit == null ? string.Empty : "\n" + RevertsTag + revertedCommit);
            GitResult result = Run("commit", "--only", "-m", message, "--", baseline.Path);
            // Hooks can fail after creating a commit; identify our receipt before attempting any recovery.
            string committed = FindOperation(operation);
            if (!string.IsNullOrEmpty(committed))
            {
                if (Git("show", "-s", "--format=%P", committed).Trim() != baseline.Head
                    || !OnlyChangedPath(baseline.Head, committed, baseline.Path)
                    || Blob(committed, baseline.Path) != newBlob
                    || Blob(committed, baseline.Path + ".meta") != baseline.MetaBlob)
                    throw new InvalidOperationException("已发现操作提交，但内容或父提交与预期不一致，请检查 Git 记录。没有回退分支。");
                return committed;
            }
            throw new InvalidOperationException("Git 提交失败：" + result.ErrorText);
        }

        internal ClipUndoRevision FindUndo(string path, string guid)
        {
            ValidatePath(path);
            string log = Git("log", "--format=%H%x00%P%x00%B%x00", "--", path);
            string[] fields = log.Split('\0');
            var undone = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i + 2 < fields.Length; i += 3)
            {
                string commit = fields[i].Trim();
                string parents = fields[i + 1].Trim();
                string message = fields[i + 2];
                if (Tag(message, AssetTag) != guid) continue;
                if (Tag(message, ActionTag) == "undo")
                {
                    undone.Add(Tag(message, RevertsTag));
                    continue;
                }
                if (Tag(message, ActionTag) != "replace" || undone.Contains(commit)) continue;
                if (string.IsNullOrEmpty(parents) || parents.Contains(" ") || !OnlyChangedPath(parents, commit, path))
                    throw new InvalidOperationException("该替换提交不是单动画提交，不能自动撤回。");
                return new ClipUndoRevision { Commit = commit, Previous = parents, Blob = Blob(commit, path), MetaBlob = Blob(commit, path + ".meta") };
            }
            return null;
        }

        internal void RestorePrevious(ClipGitBaseline baseline, ClipUndoRevision undo)
        {
            VerifyBaseline(baseline);
            if (baseline.Blob != undo.Blob || baseline.MetaBlob != undo.MetaBlob
                || Blob(undo.Previous, baseline.Path + ".meta") != baseline.MetaBlob)
                throw new InvalidOperationException("这段动画在替换后又被修改，或资源身份已改变，不能直接撤回旧替换。");
            Git("merge-base", "--is-ancestor", undo.Commit, baseline.Head);
            Git("restore", "--source=" + undo.Previous, "--worktree", "--", baseline.Path);
        }

        internal bool TryRollback(ClipGitBaseline baseline, string ownedBlob, string operation, out string diagnostic, string originalBackup = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(FindOperation(operation)))
                { diagnostic = "操作已经产生提交，请检查记录；没有重写历史。"; return false; }
                if (!CanRollback(baseline, ownedBlob))
                { diagnostic = "检测到并发变更，保留现场；替换前提交为 " + baseline.Head; return false; }
                try
                {
                    Git("restore", "--source=" + baseline.Head, "--staged", "--worktree", "--", baseline.Path);
                    diagnostic = "已恢复该动画到操作前状态，其他文件未改动。";
                }
                catch (InvalidOperationException ex) when (originalBackup != null && ex.Message.IndexOf("index.lock", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Git restore also needs index.lock. Restore only our worktree file from its verified snapshot.
                    if (!CanRollback(baseline, ownedBlob) || FileBlob(originalBackup, baseline.Path) != baseline.Blob)
                    { diagnostic = "索引锁出现后状态或备份发生变化，保留现场；替换前提交为 " + baseline.Head; return false; }
                    File.Copy(originalBackup, System.IO.Path.Combine(Root, baseline.Path), true);
                    if (WorktreeBlob(baseline.Path) != baseline.Blob)
                        throw new InvalidOperationException("备份写回后目标动画与操作前版本不一致。");
                    diagnostic = "Git 索引锁阻止恢复命令；已从本地备份恢复该动画，其他文件未改动。";
                }
                return true;
            }
            catch (Exception ex) { diagnostic = "自动恢复未完成：" + ex.Message + "；原提交 " + baseline.Head; return false; }
        }

        private bool CanRollback(ClipGitBaseline baseline, string ownedBlob)
        {
            return Git("rev-parse", "HEAD").Trim() == baseline.Head
                && Git("symbolic-ref", "--quiet", "HEAD").Trim() == baseline.Branch
                && WorktreeBlob(baseline.Path) == ownedBlob && WorktreeBlob(baseline.Path + ".meta") == baseline.MetaBlob
                && string.IsNullOrWhiteSpace(Git("diff", "--cached", "--name-only", "--", baseline.Path, baseline.Path + ".meta"));
        }

        private string FindOperation(string operation) => Git("log", "-1", "--format=%H", "--fixed-strings", "--grep=" + OperationTag + operation).Trim();

        private bool OnlyChangedPath(string before, string after, string path)
        {
            string[] paths = Git("diff-tree", "--no-commit-id", "--name-only", "-r", "-z", before, after).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            return paths.Length == 1 && paths[0] == path;
        }

        private static string Tag(string message, string prefix)
        {
            foreach (string line in message.Split('\n'))
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line.Substring(prefix.Length).Trim();
            return string.Empty;
        }

        private void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || System.IO.Path.IsPathRooted(path) || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只支持仓库内的独立 .anim 文件。");
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, path));
            if (!full.StartsWith(Root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("动画路径必须位于当前仓库内。");
        }

        private string Git(params string[] args)
        {
            GitResult result = Run(args);
            if (result.ExitCode != 0) throw new InvalidOperationException(result.ErrorText);
            return result.Output;
        }

        private GitResult Run(params string[] args)
        {
            var arguments = new StringBuilder("--literal-pathspecs");
            foreach (string arg in args) arguments.Append(' ').Append(Quote(arg));
            var start = new ProcessStartInfo("git", arguments.ToString())
            {
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables.Remove("GIT_INDEX_FILE");
            start.EnvironmentVariables.Remove("GIT_DIR");
            start.EnvironmentVariables.Remove("GIT_WORK_TREE");
            using (var process = new Process { StartInfo = start })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000))
                {
                    process.Kill();
                    return new GitResult(-1, string.Empty, "Git 操作超时，请检查签名或 hooks。");
                }
                return new GitResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
            }
        }

        // Process arguments, not shell commands: preserve spaces, quotes and trailing backslashes on Windows.
        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1).Append(c); slashes = 0; continue; }
                result.Append('\\', slashes).Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private readonly struct GitResult
        {
            internal GitResult(int exitCode, string output, string error) { ExitCode = exitCode; Output = output; Error = error; }
            internal readonly int ExitCode;
            internal readonly string Output;
            private readonly string Error;
            internal string ErrorText => string.IsNullOrWhiteSpace(Error) ? Output.Trim() : Error.Trim();
        }
    }
}
