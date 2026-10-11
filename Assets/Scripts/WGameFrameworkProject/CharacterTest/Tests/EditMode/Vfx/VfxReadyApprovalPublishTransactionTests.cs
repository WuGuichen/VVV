using System;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxReadyApprovalPublishTransactionTests
    {
        private const string TestRoot = "Assets/MxFrameworkGenerated/Vfx/ReadyApprovalTransactionTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void ReceiptPartialWriteFailure_PreservesExistingReceiptAndCatalog()
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            PairSnapshot original = Snapshot(fixture);
            using var transaction = new VfxReadyApprovalPublishTransaction(
                fixture.ReceiptPath,
                fixture.CatalogPath,
                fixture.PrefabPath,
                receiptWriter: (path, _) =>
                {
                    File.WriteAllText(path, "{");
                    throw new IOException("simulated partial receipt write");
                });

            Assert.That(transaction.Publish(fixture.Receipt, fixture.UpdatedCatalog, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            AssertUnchanged(fixture, original);
            AssertNoTemporaryFiles(fixture.CaseRoot);
        }

        [TestCase("publish")]
        [TestCase("import")]
        public void CatalogSecondStageFailure_RestoresExistingApprovalAndCatalog(string failure)
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            PairSnapshot original = Snapshot(fixture);
            int publishCount = 0;
            int importCount = 0;
            Func<string, string, bool> publisher = failure == "publish"
                ? (temporary, output) =>
                {
                    publishCount++;
                    if (publishCount == 2)
                    {
                        File.WriteAllText(output, "partial catalog");
                        return false;
                    }
                    PublishFile(temporary, output);
                    return true;
                }
                : null;
            Func<string, bool> importer = failure == "import"
                ? path =>
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    importCount++;
                    return importCount != 2;
                }
                : null;
            using var transaction = new VfxReadyApprovalPublishTransaction(
                fixture.ReceiptPath,
                fixture.CatalogPath,
                fixture.PrefabPath,
                publisher: publisher,
                importer: importer);

            Assert.That(transaction.Publish(fixture.Receipt, fixture.UpdatedCatalog, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            if (failure == "import")
                Assert.That(importCount, Is.EqualTo(4), "Both restored assets must be reimported.");
            AssertUnchanged(fixture, original);
            AssertNoTemporaryFiles(fixture.CaseRoot);
        }

        [Test]
        public void FirstReceiptCatalogPublishFailure_LeavesNoOrphanReceiptOrFolder()
        {
            Fixture fixture = CreateFixture(existingReceipt: false);
            PairSnapshot original = Snapshot(fixture);
            int publishCount = 0;
            using var transaction = new VfxReadyApprovalPublishTransaction(
                fixture.ReceiptPath,
                fixture.CatalogPath,
                fixture.PrefabPath,
                publisher: (temporary, output) =>
                {
                    publishCount++;
                    if (publishCount == 2) return false;
                    PublishFile(temporary, output);
                    return true;
                });

            Assert.That(transaction.Publish(fixture.Receipt, fixture.UpdatedCatalog, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(File.Exists(fixture.ReceiptPath), Is.False);
            Assert.That(File.Exists(fixture.ReceiptPath + ".meta"), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(fixture.ReceiptPath) ?? string.Empty), Is.False);
            Assert.That(File.ReadAllBytes(fixture.CatalogPath), Is.EqualTo(original.CatalogBytes));
            Assert.That(File.ReadAllBytes(fixture.CatalogPath + ".meta"), Is.EqualTo(original.CatalogMeta));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(original.CatalogGuid));
            AssertNoTemporaryFiles(fixture.CaseRoot);
        }

        [Test]
        public void Success_PublishesSameGenerationReceiptAndCatalogAndPreservesGuids()
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            PairSnapshot original = Snapshot(fixture);
            using var transaction = new VfxReadyApprovalPublishTransaction(
                fixture.ReceiptPath,
                fixture.CatalogPath,
                fixture.PrefabPath);

            Assert.That(transaction.Publish(fixture.Receipt, fixture.UpdatedCatalog, out string message), Is.True, message);
            VfxReadyApprovalReceipt receipt = JsonConvert.DeserializeObject<VfxReadyApprovalReceipt>(
                File.ReadAllText(fixture.ReceiptPath));
            JObject catalog = JObject.Parse(File.ReadAllText(fixture.CatalogPath));
            JObject effect = (catalog["effects"] as JArray)?.OfType<JObject>().Single();
            Assert.That(receipt.ReceiptHash, Is.EqualTo(VfxCanonicalJson.Hash(receipt, "receiptHash")));
            Assert.That(receipt.PrefabGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(fixture.PrefabPath)));
            Assert.That(receipt.PrefabContentHash, Is.EqualTo(VfxCompositionDraftService.HashFile(fixture.PrefabPath)));
            Assert.That(effect?["approval"]?.Value<string>("receiptPath"), Is.EqualTo(fixture.ReceiptPath));
            Assert.That(effect?["approval"]?.Value<string>("receiptHash"), Is.EqualTo(receipt.ReceiptHash));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.ReceiptPath), Is.EqualTo(original.ReceiptGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(original.CatalogGuid));
            AssertNoTemporaryFiles(fixture.CaseRoot);
        }

        private static Fixture CreateFixture(bool existingReceipt)
        {
            string token = Guid.NewGuid().ToString("N");
            string caseRoot = TestRoot + "/" + token;
            EnsureAssetFolder(caseRoot);
            string prefabPath = caseRoot + "/effect.prefab";
            string catalogPath = caseRoot + "/catalog.json";
            string receiptPath = caseRoot + "/Approvals/impact.json";
            var prefab = new GameObject("ready-approval-transaction-prefab");
            try { PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }

            string effectId = "vfx.effect.ready." + token;
            var oldCatalog = new JObject
            {
                ["effects"] = new JArray
                {
                    new JObject
                    {
                        ["resourceKey"] = new JObject { ["id"] = effectId },
                        ["visualMaturity"] = "Candidate",
                        ["reviewStatus"] = "ReviewNeeded",
                        ["approval"] = null
                    }
                }
            };
            File.WriteAllText(catalogPath, oldCatalog.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (existingReceipt)
            {
                EnsureAssetFolder(Path.GetDirectoryName(receiptPath)?.Replace('\\', '/'));
                File.WriteAllText(receiptPath, "{\"generation\":\"old\"}");
                AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            var receipt = new VfxReadyApprovalReceipt
            {
                EffectId = effectId,
                ResourceKey = "Vfx:" + effectId,
                PrefabPath = prefabPath,
                PrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                PrefabContentHash = VfxCompositionDraftService.HashFile(prefabPath),
                ConsumerContractHash = "sha256:contract",
                ApprovedBy = "test",
                ApprovedUtc = "2026-07-18T00:00:00Z"
            };
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            JObject updatedCatalog = (JObject)oldCatalog.DeepClone();
            JObject effect = (updatedCatalog["effects"] as JArray)?[0] as JObject;
            effect["visualMaturity"] = "Production";
            effect["reviewStatus"] = "Ready";
            effect["approval"] = new JObject
            {
                ["kind"] = "HumanVfxApproval",
                ["receiptPath"] = receiptPath,
                ["receiptHash"] = receipt.ReceiptHash
            };
            return new Fixture
            {
                CaseRoot = caseRoot,
                PrefabPath = prefabPath,
                CatalogPath = catalogPath,
                ReceiptPath = receiptPath,
                Receipt = receipt,
                UpdatedCatalog = updatedCatalog,
                ExistingReceipt = existingReceipt
            };
        }

        private static PairSnapshot Snapshot(Fixture fixture)
        {
            return new PairSnapshot
            {
                ReceiptBytes = fixture.ExistingReceipt ? File.ReadAllBytes(fixture.ReceiptPath) : null,
                ReceiptMeta = fixture.ExistingReceipt ? File.ReadAllBytes(fixture.ReceiptPath + ".meta") : null,
                ReceiptGuid = AssetDatabase.AssetPathToGUID(fixture.ReceiptPath),
                CatalogBytes = File.ReadAllBytes(fixture.CatalogPath),
                CatalogMeta = File.ReadAllBytes(fixture.CatalogPath + ".meta"),
                CatalogGuid = AssetDatabase.AssetPathToGUID(fixture.CatalogPath)
            };
        }

        private static void AssertUnchanged(Fixture fixture, PairSnapshot expected)
        {
            Assert.That(File.ReadAllBytes(fixture.ReceiptPath), Is.EqualTo(expected.ReceiptBytes));
            Assert.That(File.ReadAllBytes(fixture.ReceiptPath + ".meta"), Is.EqualTo(expected.ReceiptMeta));
            Assert.That(File.ReadAllBytes(fixture.CatalogPath), Is.EqualTo(expected.CatalogBytes));
            Assert.That(File.ReadAllBytes(fixture.CatalogPath + ".meta"), Is.EqualTo(expected.CatalogMeta));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.ReceiptPath), Is.EqualTo(expected.ReceiptGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(expected.CatalogGuid));
        }

        private static void AssertNoTemporaryFiles(string root)
        {
            Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Any(path => path.IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        private static void PublishFile(string temporary, string output)
        {
            if (File.Exists(output)) File.Replace(temporary, output, null, true);
            else File.Move(temporary, output);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] segments = (folder ?? string.Empty).Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private sealed class Fixture
        {
            public string CaseRoot;
            public string PrefabPath;
            public string CatalogPath;
            public string ReceiptPath;
            public VfxReadyApprovalReceipt Receipt;
            public JObject UpdatedCatalog;
            public bool ExistingReceipt;
        }

        private sealed class PairSnapshot
        {
            public byte[] ReceiptBytes;
            public byte[] ReceiptMeta;
            public string ReceiptGuid;
            public byte[] CatalogBytes;
            public byte[] CatalogMeta;
            public string CatalogGuid;
        }
    }
}
