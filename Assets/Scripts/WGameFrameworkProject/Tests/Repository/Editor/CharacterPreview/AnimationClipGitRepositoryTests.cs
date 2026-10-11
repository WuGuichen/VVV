using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using MxFramework.Project.Editor.AnimationDesigner;
using NUnit.Framework;

namespace MxFramework.Tests.Editor.CharacterPreview
{
    public sealed class AnimationClipGitRepositoryTests
    {
        private const string Clip = "Assets/Clips/动画 clip.anim";
        private const string GuidKey = "0123456789abcdef0123456789abcdef";
        private string _root;
        private AnimationClipGitRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("Library/AnimationClipGitTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.Combine(_root, "Assets/Clips"));
            Git("init"); Git("config", "user.name", "Animation Designer Tests"); Git("config", "user.email", "animation-tests@example.invalid");
            Git("config", "commit.gpgsign", "false"); Git("config", "core.hooksPath", ".git/hooks"); Git("config", "core.autocrlf", "false");
            // Opaque Git fixtures outside AssetDatabase; Unity serialization is covered separately.
            Write(Clip, "version A\n"); Write(Clip + ".meta", "guid: " + GuidKey + "\n"); Write("notes.txt", "initial\n");
            Git("add", "."); Git("commit", "-m", "baseline");
            _repository = new AnimationClipGitRepository(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root == null || !Directory.Exists(_root)) return;
            foreach (string file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, true);
        }

        [Test]
        public void ReplaceCommitsOnlyTargetAndPreservesUnrelatedStagedAndUnstagedWork()
        {
            Write("notes.txt", "staged\n"); Git("add", "notes.txt"); Write("notes.txt", "working\n"); Write("untracked.txt", "keep\n");
            string index = Git("ls-files", "--stage", "--", "notes.txt");
            string meta = File.ReadAllText(Path.Combine(_root, Clip + ".meta"));
            string commit = Replace("version B\n");
            Assert.That(Git("diff-tree", "--no-commit-id", "--name-only", "-r", "-z", commit).TrimEnd('\0', '\n'), Is.EqualTo(Clip));
            Assert.That(Git("ls-files", "--stage", "--", "notes.txt"), Is.EqualTo(index));
            Assert.That(File.ReadAllText(Path.Combine(_root, "notes.txt")), Is.EqualTo("working\n"));
            Assert.That(File.ReadAllText(Path.Combine(_root, "untracked.txt")), Is.EqualTo("keep\n"));
            Assert.That(File.ReadAllText(Path.Combine(_root, Clip + ".meta")), Is.EqualTo(meta));
            Assert.That(Git("status", "--porcelain", "--", Clip, Clip + ".meta").Trim(), Is.Empty);
        }

        [Test]
        public void UndoUsesReplacementParentAndKeepsLaterUnrelatedCommits()
        {
            string replaced = Replace("version B\n");
            Write("notes.txt", "later commit\n"); Git("add", "notes.txt"); Git("commit", "-m", "unrelated change");
            string previousHead = Git("rev-parse", "HEAD").Trim();
            Undo();
            Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("version A\n"));
            Assert.That(Git("show", "HEAD:notes.txt"), Is.EqualTo("later commit\n"));
            Assert.That(Git("show", "-s", "--format=%P", "HEAD").Trim(), Is.EqualTo(previousHead));
            Assert.That(Git("show", "-s", "--format=%B", "HEAD"), Does.Contain(replaced));
            Assert.That(_repository.FindUndo(Clip, GuidKey), Is.Null);
        }

        [Test]
        public void RepeatedUndoWalksReplacementHistoryInsteadOfTogglingVersions()
        {
            Replace("version B\n"); Replace("version C\n");
            Undo(); Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("version B\n"));
            Undo(); Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("version A\n"));
            Assert.That(_repository.FindUndo(Clip, GuidKey), Is.Null);
        }

        [Test]
        public void DirtyTargetIsRejectedWithoutChangingContentOrHead()
        {
            string head = Git("rev-parse", "HEAD");
            Write(Clip, "user edit\n");
            Assert.Throws<InvalidOperationException>(() => _repository.Prepare(Clip));
            Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("user edit\n"));
            Assert.That(Git("rev-parse", "HEAD"), Is.EqualTo(head));
        }

        [Test]
        [Platform("Win")]
        public void RejectedCommitRestoresOnlyOwnedClipAndLeavesIndexWorkIntact()
        {
            Write("notes.txt", "staged\n"); Git("add", "notes.txt");
            string index = Git("ls-files", "--stage", "--", "notes.txt");
            var baseline = _repository.Prepare(Clip);
            Write(Clip, "version B\n");
            string blob = _repository.WorktreeBlob(Clip);
            string operation = Guid.NewGuid().ToString("N");
            Write(".git/hooks/pre-commit", "#!/bin/sh\nexit 1\n");
            Assert.Throws<InvalidOperationException>(() => _repository.Commit(baseline, blob, GuidKey, operation));
            Assert.That(_repository.TryRollback(baseline, blob, operation, out string diagnostic), Is.True, diagnostic);
            Assert.That(Git("rev-parse", "HEAD").Trim(), Is.EqualTo(baseline.Head));
            Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("version A\n"));
            Assert.That(Git("ls-files", "--stage", "--", "notes.txt"), Is.EqualTo(index));
        }

        [Test]
        public void ConcurrentCommitBlocksAutomaticRollbackInsteadOfResettingTheRepository()
        {
            var baseline = _repository.Prepare(Clip);
            Write(Clip, "version B\n");
            string blob = _repository.WorktreeBlob(Clip);
            Write("notes.txt", "concurrent\n"); Git("commit", "--only", "-m", "concurrent", "--", "notes.txt");
            string head = Git("rev-parse", "HEAD");
            string operation = Guid.NewGuid().ToString("N");
            Assert.Throws<InvalidOperationException>(() => _repository.Commit(baseline, blob, GuidKey, operation));
            Assert.That(_repository.TryRollback(baseline, blob, operation, out _), Is.False);
            Assert.That(Git("rev-parse", "HEAD"), Is.EqualTo(head));
            Assert.That(File.ReadAllText(Path.Combine(_root, Clip)), Is.EqualTo("version B\n"));
        }

        private string Replace(string contents)
        {
            var baseline = _repository.Prepare(Clip);
            Write(Clip, contents);
            return _repository.Commit(baseline, _repository.WorktreeBlob(Clip), GuidKey, Guid.NewGuid().ToString("N"));
        }

        [Test]
        public void OnlyUntrackedExportsMayBeConsumed()
        {
            const string export = "Assets/Clips/export.anim";
            Write(export, "export\n"); Write(export + ".meta", "guid: export\n");
            Assert.DoesNotThrow(() => _repository.RequireUntrackedExport(export));
            Git("add", "--", export, export + ".meta");
            Assert.Throws<InvalidOperationException>(() => _repository.RequireUntrackedExport(export));
            Assert.Throws<InvalidOperationException>(() => _repository.RequireUntrackedExport(Clip));
        }

        [Test]
        public void ConcurrentTargetStagingIsNeverDiscardedByFailureRecovery()
        {
            var baseline = _repository.Prepare(Clip);
            Write(Clip, "another staged version\n"); Git("add", "--", Clip);
            string staged = Git("ls-files", "--stage", "--", Clip);
            Write(Clip, "our replacement\n");
            string blob = _repository.WorktreeBlob(Clip);
            string operation = Guid.NewGuid().ToString("N");
            Assert.Throws<InvalidOperationException>(() => _repository.Commit(baseline, blob, GuidKey, operation));
            Assert.That(_repository.TryRollback(baseline, blob, operation, out _), Is.False);
            Assert.That(Git("ls-files", "--stage", "--", Clip), Is.EqualTo(staged));
        }

        private void Undo()
        {
            var baseline = _repository.Prepare(Clip);
            var undo = _repository.FindUndo(Clip, GuidKey);
            Assert.That(undo, Is.Not.Null);
            _repository.RestorePrevious(baseline, undo);
            _repository.Commit(baseline, _repository.WorktreeBlob(Clip), GuidKey, Guid.NewGuid().ToString("N"), undo.Commit);
        }

        private void Write(string path, string content) => File.WriteAllText(Path.Combine(_root, path), content, new UTF8Encoding(false));

        private string Git(params string[] args)
        {
            var text = new StringBuilder(); foreach (string arg in args) text.Append(AnimationClipGitRepository.Quote(arg)).Append(' ');
            using (var process = Process.Start(new ProcessStartInfo("git", text.ToString())
            { WorkingDirectory = _root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 }))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.That(process.WaitForExit(30000), Is.True, "Git fixture timed out.");
                Assert.That(process.ExitCode, Is.Zero, error.GetAwaiter().GetResult());
                return output.GetAwaiter().GetResult();
            }
        }
    }
}
