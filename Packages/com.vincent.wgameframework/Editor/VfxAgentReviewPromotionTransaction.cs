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
    /// <summary>Coordinates an Agent candidate directory move with Catalog and promotion receipt publication.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Existing Catalog metadata/GUID and receipt history are restored on failure.</remarks>
    internal sealed class VfxAgentReviewPromotionTransaction : IDisposable
    {
        private readonly string _sourceRoot;
        private readonly string _destinationRoot;
        private readonly string _catalogAssetPath;
        private readonly string _catalogPath;
        private readonly string _receiptPath;
        private readonly Action<string, string> _receiptWriter;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<string, bool> _catalogImporter;
        private readonly Func<string, string, string> _mover;
        private readonly bool _moveAlreadyApplied;
        private readonly List<string> _createdReceiptDirectories;
        private bool _snapshotTaken;
        private bool _moveApplied;
        private bool _publishAttempted;
        private bool _completed;
        private byte[] _catalogBytes;
        private byte[] _catalogMeta;
        private string _catalogGuid;
        private bool _hadReceipt;
        private byte[] _receiptBytes;

        internal VfxAgentReviewPromotionTransaction(
            string sourceRoot,
            string destinationRoot,
            string catalogAssetPath,
            string receiptPath,
            Action<string, string> receiptWriter = null,
            Func<string, string, bool> publisher = null,
            Func<string, bool> catalogImporter = null,
            Func<string, string, string> mover = null,
            bool moveAlreadyApplied = false)
        {
            _sourceRoot = sourceRoot.Replace('\\', '/').TrimEnd('/');
            _destinationRoot = destinationRoot.Replace('\\', '/').TrimEnd('/');
            _catalogAssetPath = catalogAssetPath.Replace('\\', '/');
            _catalogPath = Path.GetFullPath(_catalogAssetPath);
            _receiptPath = Path.GetFullPath(receiptPath);
            string token = Guid.NewGuid().ToString("N");
            CatalogTemporaryPath = _catalogPath + "." + token + ".tmp";
            ReceiptTemporaryPath = _receiptPath + "." + token + ".tmp";
            _createdReceiptDirectories = FindMissingDirectories(
                Path.GetDirectoryName(_receiptPath) ?? string.Empty);
            _receiptWriter = receiptWriter ?? WriteText;
            _publisher = publisher ?? PublishFile;
            _catalogImporter = catalogImporter ?? ImportCatalog;
            _mover = mover ?? AssetDatabase.MoveAsset;
            _moveAlreadyApplied = moveAlreadyApplied;
        }

        internal string CatalogTemporaryPath { get; }
        internal string ReceiptTemporaryPath { get; }

        internal bool Publish(
            JObject catalog,
            JObject receipt,
            string primitiveId,
            string destinationPrefab,
            Func<string> postMoveGuard,
            out string message)
        {
            try
            {
                _moveApplied = _moveAlreadyApplied;
                Directory.CreateDirectory(Path.GetDirectoryName(_receiptPath) ?? string.Empty);
                Prepare(catalog, receipt, primitiveId, destinationPrefab);
                Snapshot();
                if (!_moveAlreadyApplied)
                {
                    string moveError = _mover(_sourceRoot, _destinationRoot);
                    if (!string.IsNullOrWhiteSpace(moveError))
                        throw new IOException("Candidate directory move failed: " + moveError);
                }
                _moveApplied = true;
                string guardError = postMoveGuard?.Invoke() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(guardError))
                    throw new InvalidOperationException(guardError);
                _publishAttempted = true;
                if (!_publisher(CatalogTemporaryPath, _catalogPath))
                    throw new IOException("Catalog publish seam rejected the replacement.");
                if (!_catalogImporter(_catalogAssetPath))
                    throw new IOException("Unity failed to import the promoted VFX Catalog.");
                ValidateCatalog(_catalogPath, catalog, primitiveId, destinationPrefab);
                if (!_publisher(ReceiptTemporaryPath, _receiptPath))
                    throw new IOException("Promotion receipt publish seam rejected the replacement.");
                ValidateReceipt(_receiptPath, receipt, primitiveId, destinationPrefab);
                ValidateMoveState();
                AssertCatalogGuidPreserved();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _completed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Agent promotion failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Agent promotion failed: " + exception.Message
                        + " RecoveryRequired: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void Prepare(JObject catalog, JObject receipt, string primitiveId, string destinationPrefab)
        {
            WriteText(CatalogTemporaryPath, catalog.ToString(Formatting.Indented) + Environment.NewLine);
            ValidateCatalog(CatalogTemporaryPath, catalog, primitiveId, destinationPrefab);
            _receiptWriter(ReceiptTemporaryPath, receipt.ToString(Formatting.Indented) + Environment.NewLine);
            ValidateReceipt(ReceiptTemporaryPath, receipt, primitiveId, destinationPrefab);
        }

        private void Snapshot()
        {
            _catalogBytes = File.ReadAllBytes(_catalogPath);
            _catalogMeta = File.Exists(_catalogPath + ".meta")
                ? File.ReadAllBytes(_catalogPath + ".meta")
                : null;
            _catalogGuid = AssetDatabase.AssetPathToGUID(_catalogAssetPath);
            _hadReceipt = File.Exists(_receiptPath);
            _receiptBytes = _hadReceipt ? File.ReadAllBytes(_receiptPath) : null;
            _snapshotTaken = true;
        }

        private static void ValidateCatalog(
            string path,
            JObject expected,
            string primitiveId,
            string destinationPrefab)
        {
            JObject roundTrip = JObject.Parse(File.ReadAllText(path));
            JObject primitive = (roundTrip["primitives"] as JArray)?.OfType<JObject>().FirstOrDefault(
                value => string.Equals(value.Value<string>("primitiveId"), primitiveId, StringComparison.Ordinal));
            if (!JToken.DeepEquals(expected, roundTrip)
                || primitive == null
                || !string.Equals(primitive["assetRef"]?.Value<string>("path"), destinationPrefab, StringComparison.Ordinal)
                || !string.Equals(primitive.Value<string>("reviewStatus"), "ReviewNeeded", StringComparison.Ordinal)
                || !string.Equals(primitive.Value<string>("visualMaturity"), "Candidate", StringComparison.Ordinal))
                throw new InvalidDataException("Promotion Catalog provisional/final validation failed.");
        }

        private static void ValidateReceipt(
            string path,
            JObject expected,
            string primitiveId,
            string destinationPrefab)
        {
            JObject roundTrip = JObject.Parse(File.ReadAllText(path));
            if (!JToken.DeepEquals(expected, roundTrip)
                || !string.Equals(roundTrip.Value<string>("schemaVersion"), "mx.vfx.agent-review-promotion.v1", StringComparison.Ordinal)
                || !string.Equals(roundTrip.Value<string>("primitiveId"), primitiveId, StringComparison.Ordinal)
                || !string.Equals(roundTrip.Value<string>("destinationPrefab"), destinationPrefab, StringComparison.Ordinal)
                || !string.Equals(roundTrip.Value<string>("reviewStatus"), "ReviewNeeded", StringComparison.Ordinal)
                || roundTrip.Value<bool?>("readyGranted") != false)
                throw new InvalidDataException("Promotion receipt provisional/final validation failed.");
        }

        private void ValidateMoveState()
        {
            if (AssetDatabase.IsValidFolder(_sourceRoot)
                || !AssetDatabase.IsValidFolder(_destinationRoot))
                throw new InvalidDataException("Promoted candidate directory move did not reach the expected final state.");
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken)
            {
                RestoreFile(_catalogPath, true, _catalogBytes, failures);
                RestoreFile(_catalogPath + ".meta", _catalogMeta != null, _catalogMeta, failures);
                RestoreFile(_receiptPath, _hadReceipt, _receiptBytes, failures);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (!_catalogImporter(_catalogAssetPath))
                        throw new IOException("Unity failed to import the restored VFX Catalog.");
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            if (_moveApplied)
            {
                try
                {
                    string moveError = _mover(_destinationRoot, _sourceRoot);
                    if (!string.IsNullOrWhiteSpace(moveError))
                        throw new IOException("Candidate directory rollback move failed: " + moveError);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            CleanupTemps(failures);
            DeleteCreatedEmptyReceiptDirectories(failures);
            _moveApplied = false;
            _publishAttempted = false;
            if (failures.Count == 0 && _snapshotTaken)
            {
                VerifyRestored();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            if (failures.Count > 0)
                throw new AggregateException("Promotion rollback did not complete.", failures);
        }

        private void VerifyRestored()
        {
            if (!File.ReadAllBytes(_catalogPath).SequenceEqual(_catalogBytes)
                || File.Exists(_receiptPath) != _hadReceipt
                || (_hadReceipt && !File.ReadAllBytes(_receiptPath).SequenceEqual(_receiptBytes))
                || !AssetDatabase.IsValidFolder(_sourceRoot)
                || AssetDatabase.IsValidFolder(_destinationRoot))
                throw new IOException("Promotion rollback verification failed.");
            if (_catalogMeta != null && !File.ReadAllBytes(_catalogPath + ".meta").SequenceEqual(_catalogMeta))
                throw new IOException("Promotion Catalog metadata was not restored.");
            AssertCatalogGuidPreserved();
        }

        private void AssertCatalogGuidPreserved()
        {
            if (!string.IsNullOrWhiteSpace(_catalogGuid)
                && !string.Equals(AssetDatabase.AssetPathToGUID(_catalogAssetPath), _catalogGuid, StringComparison.Ordinal))
                throw new InvalidDataException("Promotion Catalog GUID changed during publish.");
        }

        private void CleanupTemps(ICollection<Exception> failures)
        {
            DeleteFile(CatalogTemporaryPath, failures);
            DeleteFile(CatalogTemporaryPath + ".meta", failures);
            DeleteFile(ReceiptTemporaryPath, failures);
        }

        private void DeleteCreatedEmptyReceiptDirectories(ICollection<Exception> failures)
        {
            for (int i = 0; i < _createdReceiptDirectories.Count; i++)
            {
                string directory = _createdReceiptDirectories[i];
                try
                {
                    if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
        }

        private static List<string> FindMissingDirectories(string directory)
        {
            var result = new List<string>();
            string current = directory;
            while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
            {
                result.Add(current);
                current = Path.GetDirectoryName(current);
            }
            return result;
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

        private static bool ImportCatalog(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null;
        }

        private static void DeleteFile(string path, ICollection<Exception> failures)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception) { failures.Add(exception); }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            if (!_completed && (_moveApplied || _publishAttempted))
            {
                try { RollBack(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            CleanupTemps(failures);
            if (!_completed) DeleteCreatedEmptyReceiptDirectories(failures);
            if (failures.Count > 0)
                throw new AggregateException("Promotion transaction cleanup failed.", failures);
        }
    }
}
