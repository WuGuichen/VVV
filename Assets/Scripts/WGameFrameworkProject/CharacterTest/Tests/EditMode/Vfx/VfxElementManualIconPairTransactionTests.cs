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
    public sealed class VfxElementManualIconPairTransactionTests
    {
        private const string OutputRoot = "Assets/MxFrameworkGenerated/Vfx/ManualIconPairTransactionTests";

        [TearDown]
        public void RemoveEmptyOutputRoot()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot)
                && Directory.GetFileSystemEntries(OutputRoot).Length == 0)
                AssetDatabase.DeleteAsset(OutputRoot);
        }

        [Test]
        public void Save_TransactionConstructionFailure_ReturnsFalseWithoutAssets()
        {
            string elementId = "vfx.element.test.constructor." + Guid.NewGuid().ToString("N");
            var element = new VfxSourceElementView { ElementId = elementId };
            byte[] png = CreatePng(Color.cyan);
            string iconPath = string.Empty;
            try
            {
                bool result = VfxElementManualIconStore.Save(
                    element,
                    png,
                    new JObject(),
                    out iconPath,
                    out string message,
                    transactionFactory: (_, _) => throw new InvalidOperationException("simulated transaction construction failure"));

                Assert.That(result, Is.False);
                Assert.That(message, Does.Contain("simulated transaction construction failure"));
                string metadataPath = VfxElementManualIconStore.GetMetadataPath(elementId);
                Assert.That(File.Exists(iconPath), Is.False);
                Assert.That(File.Exists(metadataPath), Is.False);
                Assert.That(File.Exists(iconPath + ".meta"), Is.False);
                Assert.That(File.Exists(metadataPath + ".meta"), Is.False);
                Assert.That(Directory.Exists(Path.GetDirectoryName(iconPath) ?? string.Empty), Is.False);
            }
            finally
            {
                string directory = Path.GetDirectoryName(iconPath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(directory) && AssetDatabase.IsValidFolder(directory))
                    AssetDatabase.DeleteAsset(directory);
            }
        }

        [Test]
        public void MetadataPartialWriteFailure_PreservesExistingPairBytesMetaAndGuids()
        {
            string directory = OutputRoot + "/" + Guid.NewGuid().ToString("N");
            CreateExistingPair(directory, out string iconPath, out string metadataPath);
            PairSnapshot original = Snapshot(iconPath, metadataPath);
            byte[] replacement = CreatePng(Color.red);
            JObject metadata = CreateMetadata("vfx.element.test.partial", iconPath, replacement);
            try
            {
                using var transaction = new VfxElementManualIconPairTransaction(
                    iconPath,
                    metadataPath,
                    metadataWriter: (path, _) =>
                    {
                        File.WriteAllText(path, "{");
                        throw new IOException("simulated partial metadata write");
                    });

                Assert.That(transaction.Publish(replacement, metadata, out string message), Is.False);
                Assert.That(message, Does.Contain("rolled back"));
                AssertPairUnchanged(iconPath, metadataPath, original);
                AssertNoTemporaryFiles(directory);
            }
            finally
            {
                DeleteAsset(directory);
            }
        }

        [Test]
        public void SecondImportFailure_RestoresExistingPairBytesMetaAndGuids()
        {
            string directory = OutputRoot + "/" + Guid.NewGuid().ToString("N");
            CreateExistingPair(directory, out string iconPath, out string metadataPath);
            PairSnapshot original = Snapshot(iconPath, metadataPath);
            byte[] replacement = CreatePng(Color.green);
            JObject metadata = CreateMetadata("vfx.element.test.import", iconPath, replacement);
            int importCount = 0;
            try
            {
                using var transaction = new VfxElementManualIconPairTransaction(
                    iconPath,
                    metadataPath,
                    importer: path =>
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        importCount++;
                        return importCount != 2;
                    });

                Assert.That(transaction.Publish(replacement, metadata, out string message), Is.False);
                Assert.That(message, Does.Contain("rolled back"));
                Assert.That(importCount, Is.EqualTo(4), "Both restored assets must be reimported after the second new import fails.");
                AssertPairUnchanged(iconPath, metadataPath, original);
                AssertNoTemporaryFiles(directory);
            }
            finally
            {
                DeleteAsset(directory);
            }
        }

        [Test]
        public void Success_PublishesMatchingPairAndPreservesExistingGuids()
        {
            string directory = OutputRoot + "/" + Guid.NewGuid().ToString("N");
            CreateExistingPair(directory, out string iconPath, out string metadataPath);
            PairSnapshot original = Snapshot(iconPath, metadataPath);
            byte[] replacement = CreatePng(Color.blue);
            JObject metadata = CreateMetadata("vfx.element.test.success", iconPath, replacement);
            metadata["elapsedSeconds"] = 0.211489156f;
            metadata["cameraOrbit"] = new JArray(21.666687f, 8.33334351f);
            metadata["cameraDistance"] = 4.39999962f;
            metadata["capturedUtc"] = "2026-07-21T15:54:22.4381508Z";
            try
            {
                using var transaction = new VfxElementManualIconPairTransaction(iconPath, metadataPath);
                Assert.That(transaction.Publish(replacement, metadata, out string message), Is.True, message);

                JObject published = JObject.Parse(File.ReadAllText(metadataPath));
                Assert.That(File.ReadAllBytes(iconPath), Is.EqualTo(replacement));
                Assert.That(published.Value<string>("elementId"), Is.EqualTo("vfx.element.test.success"));
                Assert.That(published.Value<string>("iconPath"), Is.EqualTo(iconPath));
                Assert.That(published.Value<string>("contentHash"), Is.EqualTo(HashPng(replacement)));
                Assert.That(published.Value<float>("elapsedSeconds"), Is.EqualTo(0.211489156f));
                Assert.That(File.ReadAllText(metadataPath), Does.Contain("2026-07-21T15:54:22.4381508Z"));
                Assert.That(AssetDatabase.AssetPathToGUID(iconPath), Is.EqualTo(original.IconGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(metadataPath), Is.EqualTo(original.MetadataGuid));
                AssertNoTemporaryFiles(directory);
            }
            finally
            {
                DeleteAsset(directory);
            }
        }

        [Test]
        public void FirstSaveSecondPublishFailure_RemovesPairMetaAndCreatedDirectory()
        {
            string directory = OutputRoot + "/" + Guid.NewGuid().ToString("N") + "/Nested";
            string iconPath = directory + "/icon.png";
            string metadataPath = directory + "/icon.json";
            byte[] png = CreatePng(Color.yellow);
            JObject metadata = CreateMetadata("vfx.element.test.first", iconPath, png);
            int publishCount = 0;
            using (var transaction = new VfxElementManualIconPairTransaction(
                iconPath,
                metadataPath,
                publisher: (temporary, output) =>
                {
                    publishCount++;
                    if (publishCount == 2)
                    {
                        File.WriteAllText(output, "partial metadata");
                        return false;
                    }
                    if (File.Exists(output)) File.Replace(temporary, output, null, true);
                    else File.Move(temporary, output);
                    return true;
                }))
            {
                Assert.That(transaction.Publish(png, metadata, out string message), Is.False);
                Assert.That(message, Does.Contain("rolled back"));
            }

            string createdRoot = Path.GetDirectoryName(directory)?.Replace('\\', '/');
            Assert.That(File.Exists(iconPath), Is.False);
            Assert.That(File.Exists(metadataPath), Is.False);
            Assert.That(File.Exists(iconPath + ".meta"), Is.False);
            Assert.That(File.Exists(metadataPath + ".meta"), Is.False);
            Assert.That(Directory.Exists(createdRoot), Is.False);
            Assert.That(File.Exists(createdRoot + ".meta"), Is.False);
        }

        private static void CreateExistingPair(string directory, out string iconPath, out string metadataPath)
        {
            EnsureAssetFolder(directory);
            iconPath = directory + "/icon.png";
            metadataPath = directory + "/icon.json";
            byte[] png = CreatePng(Color.magenta);
            File.WriteAllBytes(iconPath, png);
            File.WriteAllText(metadataPath, CreateMetadata("vfx.element.test.original", iconPath, png).ToString());
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(metadataPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(metadataPath), Is.Not.Null);
        }

        private static JObject CreateMetadata(string elementId, string iconPath, byte[] png)
        {
            return new JObject
            {
                ["schemaVersion"] = "mx.vfx.element-icon.v2",
                ["elementId"] = elementId,
                ["storagePolicy"] = "TrackedAuthoringAsset",
                ["iconPath"] = iconPath,
                ["contentHash"] = HashPng(png)
            };
        }

        private static PairSnapshot Snapshot(string iconPath, string metadataPath)
        {
            return new PairSnapshot
            {
                Icon = File.ReadAllBytes(iconPath),
                Metadata = File.ReadAllBytes(metadataPath),
                IconMeta = File.ReadAllBytes(iconPath + ".meta"),
                MetadataMeta = File.ReadAllBytes(metadataPath + ".meta"),
                IconGuid = AssetDatabase.AssetPathToGUID(iconPath),
                MetadataGuid = AssetDatabase.AssetPathToGUID(metadataPath)
            };
        }

        private static void AssertPairUnchanged(string iconPath, string metadataPath, PairSnapshot expected)
        {
            Assert.That(File.ReadAllBytes(iconPath), Is.EqualTo(expected.Icon));
            Assert.That(File.ReadAllBytes(metadataPath), Is.EqualTo(expected.Metadata));
            Assert.That(File.ReadAllBytes(iconPath + ".meta"), Is.EqualTo(expected.IconMeta));
            Assert.That(File.ReadAllBytes(metadataPath + ".meta"), Is.EqualTo(expected.MetadataMeta));
            Assert.That(AssetDatabase.AssetPathToGUID(iconPath), Is.EqualTo(expected.IconGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(metadataPath), Is.EqualTo(expected.MetadataGuid));
            JObject metadata = JObject.Parse(File.ReadAllText(metadataPath));
            Assert.That(metadata.Value<string>("contentHash"), Is.EqualTo(HashPng(File.ReadAllBytes(iconPath))));
        }

        private static void AssertNoTemporaryFiles(string directory)
        {
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Any(path => path.IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        private static byte[] CreatePng(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string HashPng(byte[] png)
        {
            return VfxCanonicalJson.HashUtf8(Convert.ToBase64String(png));
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

        private static void DeleteAsset(string path)
        {
            if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private sealed class PairSnapshot
        {
            public byte[] Icon;
            public byte[] Metadata;
            public byte[] IconMeta;
            public byte[] MetadataMeta;
            public string IconGuid;
            public string MetadataGuid;
        }
    }
}
