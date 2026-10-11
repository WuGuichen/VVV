using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Publishes a Ready approval receipt and its Authoring Catalog reference as one pair.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Existing bytes, metadata, and GUIDs are restored on failure.</remarks>
    internal sealed class VfxReadyApprovalPublishTransaction : IDisposable
    {
        private readonly string _projectRoot;
        private readonly string _receiptAssetPath;
        private readonly string _catalogAssetPath;
        private readonly string _prefabAssetPath;
        private readonly string _receiptPath;
        private readonly string _catalogPath;
        private readonly Action<string, string> _receiptWriter;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<string, bool> _importer;
        private readonly List<string> _createdFolders = new List<string>();
        private FileSnapshot _receiptSnapshot;
        private FileSnapshot _catalogSnapshot;
        private bool _snapshotTaken;
        private bool _publishAttempted;
        private bool _completed;

        internal VfxReadyApprovalPublishTransaction(
            string receiptAssetPath,
            string catalogAssetPath,
            string prefabAssetPath,
            Action<string, string> receiptWriter = null,
            Func<string, string, bool> publisher = null,
            Func<string, bool> importer = null)
        {
            _receiptAssetPath = receiptAssetPath.Replace('\\', '/');
            _catalogAssetPath = catalogAssetPath.Replace('\\', '/');
            _prefabAssetPath = prefabAssetPath.Replace('\\', '/');
            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            _receiptPath = Absolute(_receiptAssetPath);
            _catalogPath = Absolute(_catalogAssetPath);
            string token = Guid.NewGuid().ToString("N");
            ReceiptTemporaryPath = _receiptPath + "." + token + ".tmp";
            CatalogTemporaryPath = _catalogPath + "." + token + ".tmp";
            _receiptWriter = receiptWriter ?? WriteText;
            _publisher = publisher ?? PublishFile;
            _importer = importer ?? ImportTextAsset;
        }

        internal string ReceiptTemporaryPath { get; }
        internal string CatalogTemporaryPath { get; }

        internal bool Publish(
            VfxReadyApprovalReceipt receipt,
            JObject catalog,
            out string message)
        {
            try
            {
                EnsureAssetFolders(Path.GetDirectoryName(_receiptAssetPath)?.Replace('\\', '/'));
                Prepare(receipt, catalog);
                _receiptSnapshot = Capture(_receiptAssetPath, _receiptPath);
                _catalogSnapshot = Capture(_catalogAssetPath, _catalogPath);
                _snapshotTaken = true;
                _publishAttempted = true;
                if (!_publisher(ReceiptTemporaryPath, _receiptPath))
                    throw new IOException("Ready approval receipt publish seam rejected the replacement.");
                if (!_publisher(CatalogTemporaryPath, _catalogPath))
                    throw new IOException("Authoring Catalog publish seam rejected the replacement.");
                if (!_importer(_receiptAssetPath))
                    throw new IOException("Unity failed to import the Ready approval receipt.");
                if (!_importer(_catalogAssetPath))
                    throw new IOException("Unity failed to import the updated Authoring Catalog.");
                ValidatePublished(receipt, catalog);
                _completed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Ready approval publish failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Ready approval publish failed: " + exception.Message
                        + " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void Prepare(VfxReadyApprovalReceipt receipt, JObject catalog)
        {
            if (receipt == null || catalog == null)
                throw new InvalidDataException("Ready approval receipt/catalog is missing.");
            string receiptJson = JsonConvert.SerializeObject(receipt, Formatting.Indented) + Environment.NewLine;
            _receiptWriter(ReceiptTemporaryPath, receiptJson);
            VfxReadyApprovalReceipt receiptRoundTrip = JsonConvert.DeserializeObject<VfxReadyApprovalReceipt>(
                File.ReadAllText(ReceiptTemporaryPath));
            if (receiptRoundTrip == null
                || !string.Equals(receiptRoundTrip.ReceiptHash, VfxCanonicalJson.Hash(receiptRoundTrip, "receiptHash"), StringComparison.Ordinal)
                || !string.Equals(receiptRoundTrip.PrefabPath, _prefabAssetPath, StringComparison.Ordinal)
                || !string.Equals(receiptRoundTrip.PrefabGuid, AssetDatabase.AssetPathToGUID(_prefabAssetPath), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receiptRoundTrip.PrefabContentHash, VfxCompositionDraftService.HashFile(_prefabAssetPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ready approval receipt temp seal/prefab validation failed.");

            WriteText(CatalogTemporaryPath, catalog.ToString(Formatting.Indented) + Environment.NewLine);
            JObject catalogRoundTrip = JObject.Parse(File.ReadAllText(CatalogTemporaryPath));
            if (!JToken.DeepEquals(catalog, catalogRoundTrip))
                throw new InvalidDataException("Authoring Catalog temp roundtrip changed its content.");
            ValidateCatalogLink(catalogRoundTrip, receiptRoundTrip);
        }

        private void ValidatePublished(VfxReadyApprovalReceipt expectedReceipt, JObject expectedCatalog)
        {
            VfxReadyApprovalReceipt receipt = JsonConvert.DeserializeObject<VfxReadyApprovalReceipt>(
                File.ReadAllText(_receiptPath));
            JObject catalog = JObject.Parse(File.ReadAllText(_catalogPath));
            if (receipt == null
                || !string.Equals(receipt.ReceiptHash, VfxCanonicalJson.Hash(receipt, "receiptHash"), StringComparison.Ordinal)
                || !string.Equals(receipt.ReceiptHash, expectedReceipt.ReceiptHash, StringComparison.Ordinal)
                || !JToken.DeepEquals(expectedCatalog, catalog)
                || !string.Equals(receipt.PrefabGuid, AssetDatabase.AssetPathToGUID(_prefabAssetPath), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.PrefabContentHash, VfxCompositionDraftService.HashFile(_prefabAssetPath), StringComparison.OrdinalIgnoreCase)
                || AssetDatabase.LoadAssetAtPath<TextAsset>(_receiptAssetPath) == null
                || AssetDatabase.LoadAssetAtPath<TextAsset>(_catalogAssetPath) == null)
                throw new InvalidDataException("Published Ready approval receipt/catalog cross-validation failed.");
            ValidateCatalogLink(catalog, receipt);
            AssertGuidPreserved(_receiptAssetPath, _receiptSnapshot.Guid, "receipt");
            AssertGuidPreserved(_catalogAssetPath, _catalogSnapshot.Guid, "catalog");
        }

        private void ValidateCatalogLink(JObject catalog, VfxReadyApprovalReceipt receipt)
        {
            JObject effect = (catalog["effects"] as JArray)?.OfType<JObject>().FirstOrDefault(
                value => string.Equals(value["resourceKey"]?.Value<string>("id"), receipt.EffectId, StringComparison.Ordinal));
            JObject approval = effect?["approval"] as JObject;
            if (effect == null
                || !string.Equals(effect.Value<string>("visualMaturity"), "Production", StringComparison.Ordinal)
                || !string.Equals(effect.Value<string>("reviewStatus"), "Ready", StringComparison.Ordinal)
                || !string.Equals(approval?.Value<string>("receiptPath"), _receiptAssetPath, StringComparison.Ordinal)
                || !string.Equals(approval?.Value<string>("receiptHash"), receipt.ReceiptHash, StringComparison.Ordinal))
                throw new InvalidDataException("Authoring Catalog approval does not reference the sealed receipt.");
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken && _publishAttempted)
            {
                Restore(_receiptSnapshot, failures);
                Restore(_catalogSnapshot, failures);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (_receiptSnapshot.Existed && !_importer(_receiptAssetPath))
                        throw new IOException("Unity failed to reimport the restored Ready approval receipt.");
                    if (_catalogSnapshot.Existed && !_importer(_catalogAssetPath))
                        throw new IOException("Unity failed to reimport the restored Authoring Catalog.");
                    VerifyRestored(_receiptSnapshot);
                    VerifyRestored(_catalogSnapshot);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            CleanupTemps(failures);
            DeleteCreatedEmptyFolders(failures);
            _publishAttempted = false;
            if (failures.Count > 0)
                throw new AggregateException("Ready approval rollback or cleanup did not complete.", failures);
        }

        private FileSnapshot Capture(string assetPath, string absolutePath)
        {
            bool existed = File.Exists(absolutePath);
            string metaPath = absolutePath + ".meta";
            return new FileSnapshot
            {
                AssetPath = assetPath,
                Path = absolutePath,
                Existed = existed,
                Bytes = existed ? File.ReadAllBytes(absolutePath) : null,
                MetaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null,
                Guid = AssetDatabase.AssetPathToGUID(assetPath)
            };
        }

        private static void Restore(FileSnapshot snapshot, ICollection<Exception> failures)
        {
            RestoreFile(snapshot.Path, snapshot.Existed, snapshot.Bytes, failures);
            RestoreFile(snapshot.Path + ".meta", snapshot.MetaBytes != null, snapshot.MetaBytes, failures);
        }

        private static void VerifyRestored(FileSnapshot snapshot)
        {
            VerifyFile(snapshot.Path, snapshot.Existed, snapshot.Bytes);
            VerifyFile(snapshot.Path + ".meta", snapshot.MetaBytes != null, snapshot.MetaBytes);
            AssertGuidPreserved(snapshot.AssetPath, snapshot.Guid, Path.GetFileName(snapshot.Path));
        }

        private void EnsureAssetFolders(string folder)
        {
            string[] segments = (folder ?? string.Empty).Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[i]);
                    if (string.IsNullOrWhiteSpace(guid) || !AssetDatabase.IsValidFolder(next))
                        throw new IOException("Could not create Ready approval folder: " + next);
                    _createdFolders.Add(next);
                }
                current = next;
            }
        }

        private void DeleteCreatedEmptyFolders(ICollection<Exception> failures)
        {
            for (int i = _createdFolders.Count - 1; i >= 0; i--)
            {
                string assetFolder = _createdFolders[i];
                try
                {
                    string absolute = Absolute(assetFolder);
                    if (Directory.Exists(absolute)
                        && Directory.GetFileSystemEntries(absolute).Length == 0
                        && !AssetDatabase.DeleteAsset(assetFolder))
                        throw new IOException("Could not delete empty Ready approval folder: " + assetFolder);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
        }

        private string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(_projectRoot, assetPath));

        private void CleanupTemps(ICollection<Exception> failures)
        {
            DeleteFile(ReceiptTemporaryPath, failures);
            DeleteFile(ReceiptTemporaryPath + ".meta", failures);
            DeleteFile(CatalogTemporaryPath, failures);
            DeleteFile(CatalogTemporaryPath + ".meta", failures);
        }

        private static void RestoreFile(string path, bool existed, byte[] bytes, ICollection<Exception> failures)
        {
            try
            {
                if (existed) File.WriteAllBytes(path, bytes);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception) { failures.Add(exception); }
        }

        private static void VerifyFile(string path, bool existed, byte[] bytes)
        {
            if (File.Exists(path) != existed
                || (existed && !File.ReadAllBytes(path).SequenceEqual(bytes)))
                throw new IOException("Restored approval transaction file did not match original bytes: " + path);
        }

        private static void AssertGuidPreserved(string assetPath, string guid, string label)
        {
            if (!string.IsNullOrWhiteSpace(guid)
                && !string.Equals(AssetDatabase.AssetPathToGUID(assetPath), guid, StringComparison.Ordinal))
                throw new InvalidDataException("Ready approval " + label + " GUID changed during publish.");
        }

        private static void WriteText(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static bool PublishFile(string temporaryPath, string outputPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null, true);
            else File.Move(temporaryPath, outputPath);
            return true;
        }

        private static bool ImportTextAsset(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) != null;
        }

        private static void DeleteFile(string path, ICollection<Exception> failures)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception) { failures.Add(exception); }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            if (!_completed && _publishAttempted)
            {
                try { RollBack(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            CleanupTemps(failures);
            if (!_completed) DeleteCreatedEmptyFolders(failures);
            if (failures.Count > 0)
                throw new AggregateException("Ready approval transaction cleanup failed.", failures);
        }

        private sealed class FileSnapshot
        {
            public string AssetPath;
            public string Path;
            public bool Existed;
            public byte[] Bytes;
            public byte[] MetaBytes;
            public string Guid;
        }
    }
}
