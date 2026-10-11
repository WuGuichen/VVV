using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Editor.CharacterImport;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MxFramework.Tests.CharacterTest
{
    public sealed class CharacterTestWeaponTraceAutoBakeTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MxBakeSignature-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [Test]
        public void InputNotification_CancelsActiveSamplingAndKeepsPendingPackage()
        {
            const string id = "000003";
            const string activeKey = "MxFramework.CharacterTest.WeaponTraceAutoBake.Active.v1";
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type scheduler = typeof(CharacterTestWeaponTraceAutoBake);
            FieldInfo jobField = scheduler.GetField("_job", flags);
            Assert.IsNull(jobField.GetValue(null), "A live bake must finish before this isolated scheduler test.");
            string[] fields = { "_job", "_jobId", "_running", "_cancelRequested", "_inputBatch", "_runAfter" };
            var snapshot = new Dictionary<FieldInfo, object>();
            foreach (string field in fields)
            {
                FieldInfo info = scheduler.GetField(field, flags);
                snapshot.Add(info, info.GetValue(null));
            }
            var pending = (SortedSet<string>)scheduler.GetField("Pending", flags).GetValue(null);
            string[] originalPending = new List<string>(pending).ToArray();
            string originalActive = SessionState.GetString(activeKey, "");
            int previews = EditorSceneManager.previewSceneCount;
            using (var job = CharacterImportedPackagePrefabBuilder.CreateWeaponTraceBakeJob(
                "Assets/MxFrameworkGenerated/CharacterPackages/" + id, false))
            {
                try
                {
                    for (int i = 0; i < 256 && EditorSceneManager.previewSceneCount == previews; i++)
                        job.Step(1, 10000);
                    Assert.Greater(EditorSceneManager.previewSceneCount, previews);
                    pending.Add(id);
                    jobField.SetValue(null, job);
                    scheduler.GetField("_jobId", flags).SetValue(null, id);
                    scheduler.GetField("_running", flags).SetValue(null, true);
                    scheduler.GetField("_cancelRequested", flags).SetValue(null, false);
                    CharacterTestWeaponTraceAutoBake.Notify(new[] {
                        "Assets/MxFrameworkGenerated/CharacterPackages/" + id + "/config/animation_clip_registry.json" });
                    Assert.IsTrue((bool)scheduler.GetField("_cancelRequested", flags).GetValue(null));
                    scheduler.GetMethod("AdvanceActiveJob", flags).Invoke(null, null);
                    Assert.IsNull(jobField.GetValue(null));
                    Assert.AreEqual(previews, EditorSceneManager.previewSceneCount);
                    Assert.IsTrue(pending.Contains(id));
                    Assert.Throws<ObjectDisposedException>(() => job.Publish());
                }
                finally
                {
                    foreach (var field in snapshot) field.Key.SetValue(null, field.Value);
                    pending.Clear();
                    pending.UnionWith(originalPending);
                    SessionState.SetString(activeKey, originalActive);
                }
            }
        }

        [Test]
        public void SamplingSignature_IgnoresNonSamplingLibraryEditsButRetainsSourceGuard()
        {
            string library = Write("character_actions.json", "original action data");
            string animation = Write("clip.anim", "animation data");
            var inputs = new Dictionary<string, string[]> { ["package"] = new[] { library, animation } };
            var plan = new Dictionary<string, string> { ["package"] = "action=1|clip=A|trace=1|frames=10..20" };
            var before = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs, plan);
            File.WriteAllText(library, "new display name, damage and control scale");
            var changed = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs, plan);
            Assert.AreNotEqual(before.GetSignature("package"), changed.GetSignature("package"));
            Assert.AreEqual(before.GetBakeSignature("package"), changed.GetBakeSignature("package"));
            plan["package"] = "action=1|clip=A|trace=1|frames=10..21";
            var changedWindow = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs, plan);
            Assert.AreNotEqual(changed.GetBakeSignature("package"), changedWindow.GetBakeSignature("package"));
            File.WriteAllText(animation, "changed animation pose");
            var changedAnimation = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs, plan);
            Assert.AreNotEqual(changedWindow.GetBakeSignature("package"), changedAnimation.GetBakeSignature("package"));
        }

        [Test]
        public void SharedInputs_AreReadOnceAndMatchFreshPackageSignatures()
        {
            string shared = Write("shared.anim", "shared animation");
            string first = Write("first.json", "first config");
            string second = Write("second.json", "second config");
            var inputs = new Dictionary<string, string[]>
            {
                ["first"] = new[] { shared, first },
                ["second"] = new[] { second, shared },
            };
            var batch = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs);
            Assert.AreEqual(3, batch.ReadFileCount);
            Assert.AreEqual(1, batch.CacheHitCount);
            Assert.AreEqual(new FileInfo(shared).Length + new FileInfo(first).Length + new FileInfo(second).Length, batch.ReadBytes);
            Assert.IsTrue(batch.TryGetOwners(shared, out string[] owners));
            CollectionAssert.AreEqual(new[] { "first", "second" }, owners);
            foreach (var input in inputs)
            {
                var fresh = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(
                    new Dictionary<string, string[]> { [input.Key] = input.Value });
                Assert.AreEqual(fresh.GetSignature(input.Key), batch.GetSignature(input.Key));
            }
            Assert.IsTrue(batch.IsCurrent());
            batch.Invalidate();
            Assert.IsFalse(batch.IsCurrent());
        }

        [TestCase("modify")]
        [TestCase("create")]
        [TestCase("delete")]
        public void ChangedInputs_InvalidateTheSnapshot(string operation)
        {
            string path = Path.Combine(_directory, "input.anim");
            if (operation != "create") File.WriteAllText(path, "original");
            var inputs = new Dictionary<string, string[]> { ["package"] = new[] { path } };
            var before = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs);
            Assert.IsTrue(before.IsCurrent());
            if (operation == "delete") File.Delete(path);
            else File.WriteAllText(path, "changed animation content");
            Assert.IsFalse(before.IsCurrent());
            Assert.AreNotEqual(before.GetSignature("package"),
                CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs).GetSignature("package"));
        }

        [Test]
        public void FreshVerification_DetectsSameSizeContentChangeWithRestoredTimestamp()
        {
            string path = Write("input.anim", "AAAA");
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            var inputs = new Dictionary<string, string[]> { ["package"] = new[] { path } };
            var before = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs);
            File.WriteAllText(path, "BBBB");
            File.SetLastWriteTimeUtc(path, timestamp);
            var after = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(inputs);
            Assert.AreNotEqual(before.GetSignature("package"), after.GetSignature("package"));
            Assert.AreEqual(1, after.ReadFileCount);
        }

        [Test]
        public void FailedInput_DoesNotPreventOtherPackageSignatures()
        {
            string valid = Write("valid.anim", "valid");
            string locked = Write("locked.anim", "locked");
            using (File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var batch = CharacterTestWeaponTraceAutoBake.InputSignatureBatch.Build(
                    new Dictionary<string, string[]> { ["valid"] = new[] { valid }, ["locked"] = new[] { locked } });
                Assert.IsNotEmpty(batch.GetSignature("valid"));
                Assert.Throws<InvalidDataException>(() => batch.GetSignature("locked"));
            }
        }

        private string Write(string name, string contents)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
