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
    /// <summary>Publishes one manually-authored Element icon PNG and its metadata as a coordinated pair.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Existing asset metadata is restored byte-for-byte on failure.</remarks>
    internal sealed class VfxElementManualIconPairTransaction : IDisposable
    {
        private readonly string _projectRoot;
        private readonly string _iconAssetPath;
        private readonly string _metadataAssetPath;
        private readonly string _iconPath;
        private readonly string _metadataPath;
        private readonly string _iconMetaPath;
        private readonly string _metadataMetaPath;
        private readonly Action<string, byte[]> _metadataWriter;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<string, bool> _importer;
        private readonly List<string> _createdAssetFolders = new List<string>();
        private bool _snapshotTaken;
        private bool _publishAttempted;
        private bool _completed;
        private bool _hadIcon;
        private bool _hadMetadata;
        private byte[] _originalIcon;
        private byte[] _originalMetadata;
        private byte[] _originalIconMeta;
        private byte[] _originalMetadataMeta;
        private string _originalIconGuid;
        private string _originalMetadataGuid;

        internal VfxElementManualIconPairTransaction(
            string iconAssetPath,
            string metadataAssetPath,
            Action<string, byte[]> metadataWriter = null,
            Func<string, string, bool> publisher = null,
            Func<string, bool> importer = null)
        {
            _iconAssetPath = iconAssetPath.Replace('\\', '/');
            _metadataAssetPath = metadataAssetPath.Replace('\\', '/');
            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            _iconPath = Path.GetFullPath(Path.Combine(_projectRoot, _iconAssetPath));
            _metadataPath = Path.GetFullPath(Path.Combine(_projectRoot, _metadataAssetPath));
            if (!string.Equals(
                    Path.GetDirectoryName(_iconPath),
                    Path.GetDirectoryName(_metadataPath),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Element icon pair must share one directory.");
            _iconMetaPath = _iconPath + ".meta";
            _metadataMetaPath = _metadataPath + ".meta";
            string token = Guid.NewGuid().ToString("N");
            IconTemporaryPath = _iconPath + "." + token + ".tmp";
            MetadataTemporaryPath = _metadataPath + "." + token + ".tmp";
            _metadataWriter = metadataWriter ?? File.WriteAllBytes;
            _publisher = publisher ?? PublishFile;
            _importer = importer ?? ImportAsset;
        }

        internal string IconTemporaryPath { get; }
        internal string MetadataTemporaryPath { get; }

        internal bool Publish(byte[] pngBytes, JObject metadata, out string message)
        {
            try
            {
                EnsureAssetFolders(Path.GetDirectoryName(_iconAssetPath)?.Replace('\\', '/'));
                Prepare(pngBytes, metadata);
                SnapshotOriginalPair();
                _publishAttempted = true;
                if (!_publisher(IconTemporaryPath, _iconPath))
                    throw new IOException("Icon PNG publish seam rejected the replacement.");
                if (!_publisher(MetadataTemporaryPath, _metadataPath))
                    throw new IOException("Icon metadata publish seam rejected the replacement.");
                if (!_importer(_iconAssetPath))
                    throw new IOException("Unity failed to import the icon PNG.");
                if (!_importer(_metadataAssetPath))
                    throw new IOException("Unity failed to import the icon metadata.");
                ValidatePublishedPair(pngBytes, metadata);
                _completed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Element icon pair publish failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Element icon pair publish failed: " + exception.Message
                        + " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void Prepare(byte[] pngBytes, JObject metadata)
        {
            File.WriteAllBytes(IconTemporaryPath, pngBytes);
            byte[] pngRoundTrip = File.ReadAllBytes(IconTemporaryPath);
            string expectedHash = metadata?.Value<string>("contentHash") ?? string.Empty;
            if (!pngRoundTrip.SequenceEqual(pngBytes)
                || !string.Equals(HashPng(pngRoundTrip), expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Element icon PNG temp bytes/hash validation failed.");

            byte[] metadataBytes = new UTF8Encoding(false).GetBytes(
                metadata.ToString(Formatting.Indented) + Environment.NewLine);
            _metadataWriter(MetadataTemporaryPath, metadataBytes);
            JObject roundTrip = JObject.Parse(File.ReadAllText(MetadataTemporaryPath));
            if (!JsonDocumentsMatch(metadata, roundTrip)
                || string.IsNullOrWhiteSpace(roundTrip.Value<string>("elementId"))
                || !string.Equals(roundTrip.Value<string>("iconPath"), _iconAssetPath, StringComparison.Ordinal)
                || !string.Equals(roundTrip.Value<string>("contentHash"), expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Element icon metadata temp roundtrip validation failed.");
        }

        private void SnapshotOriginalPair()
        {
            _hadIcon = File.Exists(_iconPath);
            _hadMetadata = File.Exists(_metadataPath);
            _originalIcon = _hadIcon ? File.ReadAllBytes(_iconPath) : null;
            _originalMetadata = _hadMetadata ? File.ReadAllBytes(_metadataPath) : null;
            _originalIconMeta = File.Exists(_iconMetaPath) ? File.ReadAllBytes(_iconMetaPath) : null;
            _originalMetadataMeta = File.Exists(_metadataMetaPath) ? File.ReadAllBytes(_metadataMetaPath) : null;
            _originalIconGuid = AssetDatabase.AssetPathToGUID(_iconAssetPath);
            _originalMetadataGuid = AssetDatabase.AssetPathToGUID(_metadataAssetPath);
            _snapshotTaken = true;
        }

        private void ValidatePublishedPair(byte[] pngBytes, JObject expectedMetadata)
        {
            byte[] publishedPng = File.ReadAllBytes(_iconPath);
            JObject publishedMetadata = JObject.Parse(File.ReadAllText(_metadataPath));
            string publishedHash = HashPng(publishedPng);
            if (!publishedPng.SequenceEqual(pngBytes)
                || !JsonDocumentsMatch(expectedMetadata, publishedMetadata)
                || !string.Equals(publishedMetadata.Value<string>("iconPath"), _iconAssetPath, StringComparison.Ordinal)
                || !string.Equals(publishedMetadata.Value<string>("contentHash"), publishedHash, StringComparison.Ordinal)
                || AssetDatabase.LoadAssetAtPath<Texture2D>(_iconAssetPath) == null
                || AssetDatabase.LoadAssetAtPath<TextAsset>(_metadataAssetPath) == null)
                throw new InvalidDataException("Published Element icon PNG/metadata cross-validation failed.");
            AssertGuidPreserved(_iconAssetPath, _originalIconGuid, "PNG");
            AssertGuidPreserved(_metadataAssetPath, _originalMetadataGuid, "metadata");
        }

        private static bool JsonDocumentsMatch(JObject expected, JObject actual)
        {
            // Normalize both sides through JSON parsing before comparing. In-memory capture metadata
            // contains Single values and ISO timestamp strings; Json.NET reads those back as Double
            // and Date tokens even though their serialized JSON values are unchanged.
            JObject normalizedExpected = JObject.Parse(expected.ToString(Formatting.None));
            JObject normalizedActual = JObject.Parse(actual.ToString(Formatting.None));
            return JToken.DeepEquals(normalizedExpected, normalizedActual);
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken && _publishAttempted)
            {
                RestoreFile(_iconPath, _hadIcon, _originalIcon, failures);
                RestoreFile(_metadataPath, _hadMetadata, _originalMetadata, failures);
                RestoreFile(_iconMetaPath, _originalIconMeta != null, _originalIconMeta, failures);
                RestoreFile(_metadataMetaPath, _originalMetadataMeta != null, _originalMetadataMeta, failures);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (_hadIcon && !_importer(_iconAssetPath))
                        throw new IOException("Unity failed to reimport the restored icon PNG.");
                    if (_hadMetadata && !_importer(_metadataAssetPath))
                        throw new IOException("Unity failed to reimport the restored icon metadata.");
                    VerifyRestoredPair();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            CleanupTemporaryFiles(failures);
            DeleteCreatedEmptyFolders(failures);
            _publishAttempted = false;
            if (failures.Count > 0)
                throw new AggregateException("Element icon pair rollback or cleanup did not complete.", failures);
        }

        private void VerifyRestoredPair()
        {
            VerifyFile(_iconPath, _hadIcon, _originalIcon, "icon PNG");
            VerifyFile(_metadataPath, _hadMetadata, _originalMetadata, "icon metadata");
            VerifyFile(_iconMetaPath, _originalIconMeta != null, _originalIconMeta, "icon PNG meta");
            VerifyFile(_metadataMetaPath, _originalMetadataMeta != null, _originalMetadataMeta, "icon metadata meta");
            AssertGuidPreserved(_iconAssetPath, _originalIconGuid, "PNG");
            AssertGuidPreserved(_metadataAssetPath, _originalMetadataGuid, "metadata");
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
                        throw new IOException("Could not create Element icon asset folder: " + next);
                    _createdAssetFolders.Add(next);
                }
                current = next;
            }
        }

        private void DeleteCreatedEmptyFolders(ICollection<Exception> failures)
        {
            for (int i = _createdAssetFolders.Count - 1; i >= 0; i--)
            {
                string assetFolder = _createdAssetFolders[i];
                try
                {
                    string absoluteFolder = Path.GetFullPath(Path.Combine(_projectRoot, assetFolder));
                    if (Directory.Exists(absoluteFolder)
                        && Directory.GetFileSystemEntries(absoluteFolder).Length == 0
                        && !AssetDatabase.DeleteAsset(assetFolder))
                        throw new IOException("Could not delete empty Element icon folder: " + assetFolder);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }

        private void CleanupTemporaryFiles(ICollection<Exception> failures)
        {
            DeleteFile(IconTemporaryPath, failures);
            DeleteFile(IconTemporaryPath + ".meta", failures);
            DeleteFile(MetadataTemporaryPath, failures);
            DeleteFile(MetadataTemporaryPath + ".meta", failures);
        }

        private static void RestoreFile(string path, bool existed, byte[] bytes, ICollection<Exception> failures)
        {
            try
            {
                if (existed) File.WriteAllBytes(path, bytes);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private static void VerifyFile(string path, bool shouldExist, byte[] expected, string label)
        {
            if (File.Exists(path) != shouldExist
                || (shouldExist && !File.ReadAllBytes(path).SequenceEqual(expected)))
                throw new IOException("Restored " + label + " did not match its original bytes.");
        }

        private static void AssertGuidPreserved(string assetPath, string originalGuid, string label)
        {
            if (!string.IsNullOrWhiteSpace(originalGuid)
                && !string.Equals(AssetDatabase.AssetPathToGUID(assetPath), originalGuid, StringComparison.Ordinal))
                throw new InvalidDataException("Element icon " + label + " GUID changed during pair publish.");
        }

        private static string HashPng(byte[] bytes)
        {
            return VfxCanonicalJson.HashUtf8(Convert.ToBase64String(bytes));
        }

        private static bool PublishFile(string temporaryPath, string outputPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null, true);
            else File.Move(temporaryPath, outputPath);
            return true;
        }

        private static bool ImportAsset(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null
                : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) != null;
        }

        private static void DeleteFile(string path, ICollection<Exception> failures)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            if (!_completed && _publishAttempted)
            {
                try { RollBack(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            CleanupTemporaryFiles(failures);
            if (!_completed) DeleteCreatedEmptyFolders(failures);
            if (failures.Count > 0)
                throw new AggregateException("Element icon pair transaction cleanup failed.", failures);
        }
    }
}
