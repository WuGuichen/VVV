using System;
using System.IO;
using System.Reflection;
using MxFramework.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests
{
    public sealed class VfxLibraryDraftSessionCommitTests
    {
        private const string TestRoot = "Assets/MxFrameworkGenerated/Vfx/LibraryDraftSessionCommitTests";
        private static readonly FieldInfo PreparedCatalogField = typeof(VfxLibraryDraftSession)
            .GetField("_preparedCatalog", BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void CommitPrepared_ImportFailureRestoresBytesMetaAndGuid()
        {
            Fixture fixture = CreateFixture();
            int importCount = 0;
            string temporaryPath = string.Empty;
            string backupPath = string.Empty;
            var session = new VfxLibraryDraftSession(fixture.Original, fixture.Path, path =>
            {
                var transaction = new VfxCatalogAssetPublishTransaction(
                    path,
                    importer: assetPath =>
                    {
                        AssetDatabase.ImportAsset(
                            assetPath,
                            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        importCount++;
                        return importCount > 1;
                    });
                temporaryPath = transaction.TemporaryPath;
                backupPath = transaction.BackupPath;
                return transaction;
            });
            SetPrepared(session, fixture.Prepared);

            Assert.That(session.CommitPrepared(fixture.Path, out string message), Is.False);
            Assert.That(message, Does.Contain("restored"));
            Assert.That(importCount, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(fixture.Path), Is.EqualTo(fixture.OriginalBytes));
            Assert.That(File.ReadAllBytes(fixture.Path + ".meta"), Is.EqualTo(fixture.OriginalMetaBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.Path), Is.EqualTo(fixture.OriginalGuid));
            Assert.That(GetPrepared(session), Is.Not.Null, "A recoverable publish failure should keep the validated plan.");
            Assert.That(File.Exists(temporaryPath), Is.False);
            Assert.That(File.Exists(backupPath), Is.False);
        }

        [Test]
        public void CommitPrepared_SuccessPublishesPreparedContentAndClearsPlan()
        {
            Fixture fixture = CreateFixture();
            var session = new VfxLibraryDraftSession(fixture.Original, fixture.Path);
            SetPrepared(session, fixture.Prepared);

            Assert.That(session.CommitPrepared(fixture.Path, out string message), Is.True, message);
            Assert.That(JToken.DeepEquals(JObject.Parse(File.ReadAllText(fixture.Path)), fixture.Prepared), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.Path), Is.EqualTo(fixture.OriginalGuid));
            Assert.That(GetPrepared(session), Is.Null);
            AssertNoTransactionFiles(fixture.Path);
        }

        [Test]
        public void CommitPrepared_StaleHashDoesNotPublishAndClearsPlan()
        {
            Fixture fixture = CreateFixture();
            int factoryCalls = 0;
            var session = new VfxLibraryDraftSession(fixture.Original, fixture.Path, path =>
            {
                factoryCalls++;
                return new VfxCatalogAssetPublishTransaction(path);
            });
            SetPrepared(session, fixture.Prepared);
            byte[] staleBytes = System.Text.Encoding.UTF8.GetBytes("{\"stale\":true}");
            File.WriteAllBytes(fixture.Path, staleBytes);

            Assert.That(session.CommitPrepared(fixture.Path, out _), Is.False);
            Assert.That(factoryCalls, Is.Zero);
            Assert.That(File.ReadAllBytes(fixture.Path), Is.EqualTo(staleBytes));
            Assert.That(GetPrepared(session), Is.Null);
        }

        [Test]
        public void CommitPrepared_WithoutPreparedPlanDoesNotPublish()
        {
            Fixture fixture = CreateFixture();
            int factoryCalls = 0;
            var session = new VfxLibraryDraftSession(fixture.Original, fixture.Path, path =>
            {
                factoryCalls++;
                return new VfxCatalogAssetPublishTransaction(path);
            });

            Assert.That(session.CommitPrepared(fixture.Path, out _), Is.False);
            Assert.That(factoryCalls, Is.Zero);
            Assert.That(File.ReadAllBytes(fixture.Path), Is.EqualTo(fixture.OriginalBytes));
        }

        private static Fixture CreateFixture()
        {
            EnsureFolder(TestRoot);
            string path = TestRoot + "/catalog_" + Guid.NewGuid().ToString("N") + ".json";
            var original = new JObject
            {
                ["schemaVersion"] = "test.v1",
                ["effects"] = new JArray(new JObject { ["effectId"] = "vfx.test", ["description"] = "old" })
            };
            File.WriteAllText(path, original.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prepared = (JObject)original.DeepClone();
            prepared["effects"][0]["description"] = "updated";
            return new Fixture
            {
                Path = path,
                Original = original,
                Prepared = prepared,
                OriginalBytes = File.ReadAllBytes(path),
                OriginalMetaBytes = File.ReadAllBytes(path + ".meta"),
                OriginalGuid = AssetDatabase.AssetPathToGUID(path)
            };
        }

        private static void SetPrepared(VfxLibraryDraftSession session, JObject catalog)
        {
            PreparedCatalogField.SetValue(
                session,
                System.Text.Encoding.UTF8.GetBytes(catalog.ToString(Formatting.Indented) + Environment.NewLine));
        }

        private static byte[] GetPrepared(VfxLibraryDraftSession session)
        {
            return (byte[])PreparedCatalogField.GetValue(session);
        }

        private static void AssertNoTransactionFiles(string catalogPath)
        {
            string directory = Path.GetDirectoryName(catalogPath);
            string fileName = Path.GetFileName(catalogPath);
            Assert.That(Directory.GetFiles(directory, "." + fileName + ".*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(directory, "." + fileName + ".*.bak"), Is.Empty);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private sealed class Fixture
        {
            internal string Path;
            internal JObject Original;
            internal JObject Prepared;
            internal byte[] OriginalBytes;
            internal byte[] OriginalMetaBytes;
            internal string OriginalGuid;
        }
    }
}
