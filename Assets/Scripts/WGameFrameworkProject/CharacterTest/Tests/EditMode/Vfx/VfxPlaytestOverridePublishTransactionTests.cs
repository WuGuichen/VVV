using System;
using System.IO;
using MxFramework.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxPlaytestOverridePublishTransactionTests
    {
        private static readonly string TestRoot =
            Path.GetFullPath("Library/MxFramework/VfxPlaytestOverrideTransactionTests");

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
        }

        [Test]
        public void PartialWriteFailure_FirstPublishLeavesNoFileOrDirectory()
        {
            string caseRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            string path = Path.Combine(caseRoot, "Nested", "overrides.json");
            using var transaction = new VfxPlaytestOverridePublishTransaction(
                path,
                writer: (temporary, _) =>
                {
                    File.WriteAllText(temporary, "{");
                    throw new IOException("simulated partial write");
                },
                runtimeRefresher: () => true);

            Assert.That(transaction.Publish(CreateDocument(1), out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.Exists(caseRoot), Is.False);
            Assert.That(File.Exists(transaction.TemporaryPath), Is.False);
        }

        [Test]
        public void PublishFailure_RestoresExistingBytesAndRefreshesRestoredRuntime()
        {
            string caseRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            string path = Path.Combine(caseRoot, "overrides.json");
            Directory.CreateDirectory(caseRoot);
            byte[] original = System.Text.Encoding.UTF8.GetBytes("{\"generation\":\"old\"}");
            File.WriteAllBytes(path, original);
            int refreshCount = 0;
            using var transaction = new VfxPlaytestOverridePublishTransaction(
                path,
                publisher: (_, output) =>
                {
                    File.WriteAllText(output, "partial replacement");
                    return false;
                },
                runtimeRefresher: () =>
                {
                    refreshCount++;
                    return true;
                });

            Assert.That(transaction.Publish(CreateDocument(1), out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(refreshCount, Is.EqualTo(1));
            Assert.That(File.Exists(transaction.TemporaryPath), Is.False);
        }

        [Test]
        public void RefreshFailure_RestoresExistingBytesAndRefreshesTwice()
        {
            string caseRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            string path = Path.Combine(caseRoot, "overrides.json");
            Directory.CreateDirectory(caseRoot);
            byte[] original = System.Text.Encoding.UTF8.GetBytes("{\"generation\":\"old\"}");
            File.WriteAllBytes(path, original);
            int refreshCount = 0;
            using var transaction = new VfxPlaytestOverridePublishTransaction(
                path,
                runtimeRefresher: () =>
                {
                    refreshCount++;
                    return refreshCount != 1;
                });

            Assert.That(transaction.Publish(CreateDocument(2), out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(refreshCount, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(File.Exists(transaction.TemporaryPath), Is.False);
        }

        [Test]
        public void ThreeEntryPublishSuccess_WritesCurrentProjectAndRefreshesOnce()
        {
            string caseRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            string path = Path.Combine(caseRoot, "overrides.json");
            int refreshCount = 0;
            using var transaction = new VfxPlaytestOverridePublishTransaction(
                path,
                runtimeRefresher: () =>
                {
                    refreshCount++;
                    return true;
                });

            Assert.That(transaction.Publish(CreateDocument(3), out string message), Is.True, message);
            VfxPlaytestOverrideDocument saved = Newtonsoft.Json.JsonConvert
                .DeserializeObject<VfxPlaytestOverrideDocument>(File.ReadAllText(path));
            Assert.That(saved.ProjectId, Is.EqualTo(Application.dataPath));
            Assert.That(saved.Entries, Has.Count.EqualTo(3));
            Assert.That(refreshCount, Is.EqualTo(1));
            Assert.That(File.Exists(transaction.TemporaryPath), Is.False);
        }

        [Test]
        public void ClearPublishSuccess_WritesEmptyDocumentAndRefreshesOnce()
        {
            string caseRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            string path = Path.Combine(caseRoot, "overrides.json");
            Directory.CreateDirectory(caseRoot);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(CreateDocument(3)));
            int refreshCount = 0;
            using var transaction = new VfxPlaytestOverridePublishTransaction(
                path,
                runtimeRefresher: () =>
                {
                    refreshCount++;
                    return true;
                });

            Assert.That(transaction.Publish(CreateDocument(0), out string message), Is.True, message);
            VfxPlaytestOverrideDocument saved = Newtonsoft.Json.JsonConvert
                .DeserializeObject<VfxPlaytestOverrideDocument>(File.ReadAllText(path));
            Assert.That(saved.Entries, Is.Empty);
            Assert.That(refreshCount, Is.EqualTo(1));
        }

        private static VfxPlaytestOverrideDocument CreateDocument(int count)
        {
            var document = new VfxPlaytestOverrideDocument { ProjectId = Application.dataPath };
            for (int i = 0; i < count; i++)
            {
                document.Entries.Add(new VfxPlaytestOverrideRecord
                {
                    ResourceKey = new VfxPublishedResourceKey
                    {
                        Id = "vfx.test." + i,
                        TypeId = "GameObject"
                    },
                    CandidateAssetPath = "Assets/Test/" + i + ".prefab",
                    CandidateAssetGuid = "guid-" + i,
                    CandidateContentHash = "sha256:content-" + i,
                    ReceiptPath = "Temp/receipt-" + i + ".json",
                    ReceiptHash = "sha256:receipt-" + i,
                    ConsumerContractHash = "sha256:contract-" + i
                });
            }
            return document;
        }
    }
}
