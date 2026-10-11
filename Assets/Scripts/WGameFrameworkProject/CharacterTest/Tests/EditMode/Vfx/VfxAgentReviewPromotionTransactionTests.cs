using System;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxAgentReviewPromotionTransactionTests
    {
        private const string AssetRoot = "Assets/MxFrameworkGenerated/Vfx/AgentReviewPromotionTransactionTests";
        private static readonly string ReceiptRoot =
            Path.GetFullPath("Temp/MxFramework/VfxAgentReviewPromotionTransactionTests");

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(AssetRoot)) AssetDatabase.DeleteAsset(AssetRoot);
            if (Directory.Exists(ReceiptRoot)) Directory.Delete(ReceiptRoot, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void ReceiptPartialWriteFailure_PreservesHistoryCatalogAndSourceDirectory()
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            Snapshot original = Capture(fixture);
            Assert.That(AssetDatabase.MoveAsset(fixture.SourceRoot, fixture.DestinationRoot), Is.Empty);
            using var transaction = new VfxAgentReviewPromotionTransaction(
                fixture.SourceRoot,
                fixture.DestinationRoot,
                fixture.CatalogPath,
                fixture.ReceiptPath,
                receiptWriter: (path, _) =>
                {
                    File.WriteAllText(path, "{");
                    throw new IOException("simulated partial receipt write");
                },
                moveAlreadyApplied: true);

            Assert.That(Publish(transaction, fixture, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            AssertRestored(fixture, original);
            AssertNoTemps(fixture);
        }

        [TestCase("publish")]
        [TestCase("import")]
        public void CatalogFailure_RestoresExistingReceiptCatalogAndMove(string failure)
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            Snapshot original = Capture(fixture);
            int importCount = 0;
            Func<string, string, bool> publisher = failure == "publish"
                ? (_, output) =>
                {
                    File.WriteAllText(output, "partial catalog");
                    return false;
                }
                : null;
            Func<string, bool> importer = failure == "import"
                ? path =>
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    importCount++;
                    return importCount != 1;
                }
                : null;
            using var transaction = new VfxAgentReviewPromotionTransaction(
                fixture.SourceRoot,
                fixture.DestinationRoot,
                fixture.CatalogPath,
                fixture.ReceiptPath,
                publisher: publisher,
                catalogImporter: importer);

            Assert.That(Publish(transaction, fixture, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            if (failure == "import") Assert.That(importCount, Is.EqualTo(2));
            AssertRestored(fixture, original);
            AssertNoTemps(fixture);
        }

        [Test]
        public void FirstReceiptPublishFailure_LeavesNoOrphanAndRestoresCatalogAndMove()
        {
            Fixture fixture = CreateFixture(existingReceipt: false);
            Snapshot original = Capture(fixture);
            int publishCount = 0;
            using var transaction = new VfxAgentReviewPromotionTransaction(
                fixture.SourceRoot,
                fixture.DestinationRoot,
                fixture.CatalogPath,
                fixture.ReceiptPath,
                publisher: (temporary, output) =>
                {
                    publishCount++;
                    if (publishCount == 2)
                    {
                        File.WriteAllText(output, "partial receipt");
                        return false;
                    }
                    PublishFile(temporary, output);
                    return true;
                });

            Assert.That(Publish(transaction, fixture, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(File.Exists(fixture.ReceiptPath), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(fixture.ReceiptPath) ?? string.Empty), Is.False);
            AssertRestoredCatalogAndMove(fixture, original);
            AssertNoTemps(fixture);
        }

        [Test]
        public void RollbackMoveFailure_ReportsRecoveryRequired()
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            int moveCount = 0;
            using var transaction = new VfxAgentReviewPromotionTransaction(
                fixture.SourceRoot,
                fixture.DestinationRoot,
                fixture.CatalogPath,
                fixture.ReceiptPath,
                publisher: (_, _) => false,
                mover: (source, destination) =>
                {
                    moveCount++;
                    return moveCount == 1
                        ? AssetDatabase.MoveAsset(source, destination)
                        : "simulated rollback move failure";
                });

            Assert.That(Publish(transaction, fixture, out string message), Is.False);
            Assert.That(message, Does.Contain("RecoveryRequired"));
            Assert.That(message, Does.Contain("rollback move failed"));
            Assert.That(AssetDatabase.IsValidFolder(fixture.DestinationRoot), Is.True);
            AssertNoTemps(fixture);
        }

        [Test]
        public void Success_PublishesMoveCatalogAndReceiptAsSameGeneration()
        {
            Fixture fixture = CreateFixture(existingReceipt: true);
            Snapshot original = Capture(fixture);
            using var transaction = new VfxAgentReviewPromotionTransaction(
                fixture.SourceRoot,
                fixture.DestinationRoot,
                fixture.CatalogPath,
                fixture.ReceiptPath);

            Assert.That(Publish(transaction, fixture, out string message), Is.True, message);
            JObject catalog = JObject.Parse(File.ReadAllText(fixture.CatalogPath));
            JObject receipt = JObject.Parse(File.ReadAllText(fixture.ReceiptPath));
            JObject primitive = (catalog["primitives"] as JArray)?.OfType<JObject>().Single();
            Assert.That(AssetDatabase.IsValidFolder(fixture.SourceRoot), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(fixture.DestinationRoot), Is.True);
            Assert.That(primitive?["assetRef"]?.Value<string>("path"), Is.EqualTo(fixture.DestinationPrefab));
            Assert.That(receipt.Value<string>("primitiveId"), Is.EqualTo(fixture.PrimitiveId));
            Assert.That(receipt.Value<string>("destinationPrefab"), Is.EqualTo(fixture.DestinationPrefab));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(original.CatalogGuid));
            AssertNoTemps(fixture);
        }

        private static bool Publish(
            VfxAgentReviewPromotionTransaction transaction,
            Fixture fixture,
            out string message)
        {
            return transaction.Publish(
                fixture.UpdatedCatalog,
                fixture.NewReceipt,
                fixture.PrimitiveId,
                fixture.DestinationPrefab,
                postMoveGuard: null,
                out message);
        }

        private static Fixture CreateFixture(bool existingReceipt)
        {
            string token = Guid.NewGuid().ToString("N");
            string caseRoot = AssetRoot + "/" + token;
            string sourceRoot = caseRoot + "/Source";
            string destinationRoot = caseRoot + "/Destination";
            EnsureAssetFolder(sourceRoot);
            string sourcePrefab = sourceRoot + "/effect.prefab";
            var prefab = new GameObject("promotion-transaction-prefab");
            try { PrefabUtility.SaveAsPrefabAsset(prefab, sourcePrefab); }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
            string catalogPath = caseRoot + "/catalog.json";
            var oldCatalog = new JObject { ["primitives"] = new JArray() };
            File.WriteAllText(catalogPath, oldCatalog.ToString());
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            string primitiveId = "vfx.primitive.agent.test." + token;
            string destinationPrefab = destinationRoot + "/effect.prefab";
            JObject updatedCatalog = (JObject)oldCatalog.DeepClone();
            ((JArray)updatedCatalog["primitives"]).Add(new JObject
            {
                ["primitiveId"] = primitiveId,
                ["assetRef"] = new JObject { ["kind"] = "UnityAsset", ["path"] = destinationPrefab },
                ["reviewStatus"] = "ReviewNeeded",
                ["visualMaturity"] = "Candidate"
            });
            string receiptPath = Path.Combine(ReceiptRoot, token, "promotion-receipt.json");
            if (existingReceipt)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(receiptPath) ?? string.Empty);
                File.WriteAllText(receiptPath, "{\"generation\":\"old\"}");
            }
            return new Fixture
            {
                SourceRoot = sourceRoot,
                DestinationRoot = destinationRoot,
                DestinationPrefab = destinationPrefab,
                CatalogPath = catalogPath,
                ReceiptPath = receiptPath,
                PrimitiveId = primitiveId,
                UpdatedCatalog = updatedCatalog,
                NewReceipt = new JObject
                {
                    ["schemaVersion"] = "mx.vfx.agent-review-promotion.v1",
                    ["taskId"] = "task.test",
                    ["destinationPrefab"] = destinationPrefab,
                    ["primitiveId"] = primitiveId,
                    ["reviewStatus"] = "ReviewNeeded",
                    ["readyGranted"] = false
                },
                ExistingReceipt = existingReceipt
            };
        }

        private static Snapshot Capture(Fixture fixture)
        {
            return new Snapshot
            {
                CatalogBytes = File.ReadAllBytes(fixture.CatalogPath),
                CatalogMeta = File.ReadAllBytes(fixture.CatalogPath + ".meta"),
                CatalogGuid = AssetDatabase.AssetPathToGUID(fixture.CatalogPath),
                ReceiptBytes = fixture.ExistingReceipt ? File.ReadAllBytes(fixture.ReceiptPath) : null
            };
        }

        private static void AssertRestored(Fixture fixture, Snapshot original)
        {
            AssertRestoredCatalogAndMove(fixture, original);
            Assert.That(File.ReadAllBytes(fixture.ReceiptPath), Is.EqualTo(original.ReceiptBytes));
        }

        private static void AssertRestoredCatalogAndMove(Fixture fixture, Snapshot original)
        {
            Assert.That(File.ReadAllBytes(fixture.CatalogPath), Is.EqualTo(original.CatalogBytes));
            Assert.That(File.ReadAllBytes(fixture.CatalogPath + ".meta"), Is.EqualTo(original.CatalogMeta));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(original.CatalogGuid));
            Assert.That(AssetDatabase.IsValidFolder(fixture.SourceRoot), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(fixture.DestinationRoot), Is.False);
        }

        private static void AssertNoTemps(Fixture fixture)
        {
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(fixture.CatalogPath) ?? string.Empty, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            string receiptDirectory = Path.GetDirectoryName(fixture.ReceiptPath);
            if (Directory.Exists(receiptDirectory))
                Assert.That(Directory.GetFiles(receiptDirectory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        private static void PublishFile(string temporary, string output)
        {
            if (File.Exists(output)) File.Replace(temporary, output, null, true);
            else File.Move(temporary, output);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] segments = folder.Split('/');
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
            public string SourceRoot;
            public string DestinationRoot;
            public string DestinationPrefab;
            public string CatalogPath;
            public string ReceiptPath;
            public string PrimitiveId;
            public JObject UpdatedCatalog;
            public JObject NewReceipt;
            public bool ExistingReceipt;
        }

        private sealed class Snapshot
        {
            public byte[] CatalogBytes;
            public byte[] CatalogMeta;
            public string CatalogGuid;
            public byte[] ReceiptBytes;
        }
    }
}
