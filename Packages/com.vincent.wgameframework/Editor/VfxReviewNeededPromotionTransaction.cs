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
    /// <summary>Coordinates ReviewNeeded candidate move, Authoring Catalog, and playtest override publication.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Rollback failures are reported as RecoveryRequired.</remarks>
    internal sealed class VfxReviewNeededPromotionTransaction : IDisposable
    {
        private readonly string _sourceAssetPath;
        private readonly string _targetAssetPath;
        private readonly string _catalogAssetPath;
        private readonly string _catalogPath;
        private readonly string _overridePath;
        private readonly Action<string, string> _overrideWriter;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<string, bool> _catalogImporter;
        private readonly Func<bool> _runtimeRefresher;
        private readonly Func<string, string, string> _mover;
        private readonly List<string> _createdAssetFolders = new List<string>();
        private readonly List<string> _createdOverrideDirectories;
        private bool _snapshotTaken;
        private bool _moveApplied;
        private bool _publishAttempted;
        private bool _completed;
        private byte[] _catalogBytes;
        private byte[] _catalogMeta;
        private string _catalogGuid;
        private bool _hadOverride;
        private byte[] _overrideBytes;

        internal VfxReviewNeededPromotionTransaction(
            string sourceAssetPath,
            string targetAssetPath,
            string catalogAssetPath,
            string overridePath,
            Action<string, string> overrideWriter = null,
            Func<string, string, bool> publisher = null,
            Func<string, bool> catalogImporter = null,
            Func<bool> runtimeRefresher = null,
            Func<string, string, string> mover = null)
        {
            _sourceAssetPath = sourceAssetPath.Replace('\\', '/');
            _targetAssetPath = targetAssetPath.Replace('\\', '/');
            _catalogAssetPath = catalogAssetPath.Replace('\\', '/');
            _catalogPath = Path.GetFullPath(_catalogAssetPath);
            _overridePath = Path.GetFullPath(overridePath);
            string token = Guid.NewGuid().ToString("N");
            CatalogTemporaryPath = _catalogPath + "." + token + ".tmp";
            OverrideTemporaryPath = _overridePath + "." + token + ".tmp";
            _createdOverrideDirectories = FindMissingDirectories(
                Path.GetDirectoryName(_overridePath) ?? string.Empty);
            _overrideWriter = overrideWriter ?? WriteText;
            _publisher = publisher ?? PublishFile;
            _catalogImporter = catalogImporter ?? ImportCatalog;
            _runtimeRefresher = runtimeRefresher ?? RefreshRuntime;
            _mover = mover ?? AssetDatabase.MoveAsset;
        }

        internal string CatalogTemporaryPath { get; }
        internal string OverrideTemporaryPath { get; }

        internal bool Publish(
            JObject catalog,
            VfxPlaytestOverrideDocument overrides,
            string resourceId,
            string expectedGuid,
            string expectedContentHash,
            out string message)
        {
            try
            {
                EnsureAssetFolders(Path.GetDirectoryName(_targetAssetPath)?.Replace('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(_overridePath) ?? string.Empty);
                Prepare(catalog, overrides, resourceId, expectedGuid, expectedContentHash);
                Snapshot();
                string moveError = _mover(_sourceAssetPath, _targetAssetPath);
                _moveApplied = AssetDatabase.LoadAssetAtPath<GameObject>(_sourceAssetPath) == null
                    && AssetDatabase.LoadAssetAtPath<GameObject>(_targetAssetPath) != null;
                if (!string.IsNullOrWhiteSpace(moveError))
                    throw new IOException("ReviewNeeded candidate move failed: " + moveError);
                _moveApplied = true;
                _publishAttempted = true;
                if (!_publisher(CatalogTemporaryPath, _catalogPath))
                    throw new IOException("Authoring Catalog publish seam rejected the replacement.");
                if (!_catalogImporter(_catalogAssetPath))
                    throw new IOException("Unity failed to import the ReviewNeeded Authoring Catalog.");
                ValidateCatalog(_catalogPath, catalog, resourceId, expectedGuid, expectedContentHash);
                if (!_publisher(OverrideTemporaryPath, _overridePath))
                    throw new IOException("Playtest override publish seam rejected the replacement.");
                ValidateOverrides(_overridePath, overrides, resourceId, expectedGuid, expectedContentHash);
                if (!_runtimeRefresher())
                    throw new InvalidOperationException("Runtime binding source refresh failed.");
                ValidateFinalState(catalog, overrides, resourceId, expectedGuid, expectedContentHash);
                _completed = true;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "ReviewNeeded promotion failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "ReviewNeeded promotion failed: " + exception.Message
                        + " RecoveryRequired: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void Prepare(
            JObject catalog,
            VfxPlaytestOverrideDocument overrides,
            string resourceId,
            string expectedGuid,
            string expectedContentHash)
        {
            WriteText(CatalogTemporaryPath, catalog.ToString(Formatting.Indented) + Environment.NewLine);
            ValidateCatalog(CatalogTemporaryPath, catalog, resourceId, expectedGuid, expectedContentHash);
            overrides.ProjectId = Application.dataPath;
            _overrideWriter(
                OverrideTemporaryPath,
                JsonConvert.SerializeObject(overrides, Formatting.Indented) + Environment.NewLine);
            ValidateOverrides(OverrideTemporaryPath, overrides, resourceId, expectedGuid, expectedContentHash);
        }

        private void Snapshot()
        {
            _catalogBytes = File.ReadAllBytes(_catalogPath);
            _catalogMeta = File.Exists(_catalogPath + ".meta")
                ? File.ReadAllBytes(_catalogPath + ".meta")
                : null;
            _catalogGuid = AssetDatabase.AssetPathToGUID(_catalogAssetPath);
            _hadOverride = File.Exists(_overridePath);
            _overrideBytes = _hadOverride ? File.ReadAllBytes(_overridePath) : null;
            _snapshotTaken = true;
        }

        private void ValidateFinalState(
            JObject catalog,
            VfxPlaytestOverrideDocument overrides,
            string resourceId,
            string expectedGuid,
            string expectedContentHash)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(_sourceAssetPath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(_targetAssetPath) == null
                || !string.Equals(AssetDatabase.AssetPathToGUID(_targetAssetPath), expectedGuid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(VfxCompositionDraftService.HashFile(_targetAssetPath), expectedContentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ReviewNeeded candidate final path/GUID/content hash validation failed.");
            ValidateCatalog(_catalogPath, catalog, resourceId, expectedGuid, expectedContentHash);
            ValidateOverrides(_overridePath, overrides, resourceId, expectedGuid, expectedContentHash);
            AssertCatalogGuidPreserved();
        }

        private void ValidateCatalog(
            string path,
            JObject expected,
            string resourceId,
            string expectedGuid,
            string expectedContentHash)
        {
            JObject roundTrip = JObject.Parse(File.ReadAllText(path));
            JObject effect = (roundTrip["effects"] as JArray)?.OfType<JObject>().FirstOrDefault(
                value => string.Equals(value["resourceKey"]?.Value<string>("id"), resourceId, StringComparison.Ordinal));
            JObject published = effect?["published"] as JObject;
            if (!JToken.DeepEquals(expected, roundTrip)
                || effect == null
                || !string.Equals(effect.Value<string>("reviewStatus"), "ReviewNeeded", StringComparison.Ordinal)
                || !string.Equals(effect.Value<string>("visualMaturity"), "Candidate", StringComparison.Ordinal)
                || !string.Equals(published?.Value<string>("assetPath"), _targetAssetPath, StringComparison.Ordinal)
                || !string.Equals(published?.Value<string>("assetGuid"), expectedGuid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(published?.Value<string>("assetContentHash"), expectedContentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ReviewNeeded Authoring Catalog validation failed.");
        }

        private void ValidateOverrides(
            string path,
            VfxPlaytestOverrideDocument expected,
            string resourceId,
            string expectedGuid,
            string expectedContentHash)
        {
            VfxPlaytestOverrideDocument roundTrip =
                JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(File.ReadAllText(path));
            JObject expectedJson = JObject.FromObject(expected);
            JObject actualJson = JObject.Parse(File.ReadAllText(path));
            VfxPlaytestOverrideRecord entry = roundTrip?.Entries.FirstOrDefault(
                value => string.Equals(value?.ResourceKey?.Id, resourceId, StringComparison.Ordinal));
            if (roundTrip == null
                || !string.Equals(roundTrip.ProjectId, Application.dataPath, StringComparison.OrdinalIgnoreCase)
                || !JToken.DeepEquals(expectedJson, actualJson)
                || entry == null
                || !string.Equals(entry.CandidateAssetPath, _targetAssetPath, StringComparison.Ordinal)
                || !string.Equals(entry.CandidateAssetGuid, expectedGuid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(entry.CandidateContentHash, expectedContentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ReviewNeeded playtest override validation failed.");
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken)
            {
                RestoreFile(_catalogPath, true, _catalogBytes, failures);
                RestoreFile(_catalogPath + ".meta", _catalogMeta != null, _catalogMeta, failures);
                RestoreFile(_overridePath, _hadOverride, _overrideBytes, failures);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (!_catalogImporter(_catalogAssetPath))
                        throw new IOException("Unity failed to import the restored Authoring Catalog.");
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            if (_moveApplied)
            {
                try
                {
                    string moveError = _mover(_targetAssetPath, _sourceAssetPath);
                    if (!string.IsNullOrWhiteSpace(moveError))
                        throw new IOException("ReviewNeeded candidate rollback move failed: " + moveError);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            try
            {
                if (_snapshotTaken && !_runtimeRefresher())
                    throw new InvalidOperationException("Runtime binding source rollback refresh failed.");
            }
            catch (Exception exception) { failures.Add(exception); }
            CleanupTemps(failures);
            DeleteCreatedEmptyState(failures);
            _moveApplied = false;
            _publishAttempted = false;
            if (failures.Count == 0 && _snapshotTaken) VerifyRestored();
            if (failures.Count > 0)
                throw new AggregateException("ReviewNeeded rollback did not complete.", failures);
        }

        private void VerifyRestored()
        {
            if (!File.ReadAllBytes(_catalogPath).SequenceEqual(_catalogBytes)
                || (_catalogMeta != null && !File.ReadAllBytes(_catalogPath + ".meta").SequenceEqual(_catalogMeta))
                || File.Exists(_overridePath) != _hadOverride
                || (_hadOverride && !File.ReadAllBytes(_overridePath).SequenceEqual(_overrideBytes))
                || AssetDatabase.LoadAssetAtPath<GameObject>(_sourceAssetPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(_targetAssetPath) != null)
                throw new IOException("ReviewNeeded rollback verification failed.");
            AssertCatalogGuidPreserved();
        }

        private void AssertCatalogGuidPreserved()
        {
            if (!string.IsNullOrWhiteSpace(_catalogGuid)
                && !string.Equals(AssetDatabase.AssetPathToGUID(_catalogAssetPath), _catalogGuid, StringComparison.Ordinal))
                throw new InvalidDataException("ReviewNeeded Authoring Catalog GUID changed.");
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
                    if (string.IsNullOrWhiteSpace(guid)) throw new IOException("Could not create ReviewNeeded folder: " + next);
                    _createdAssetFolders.Add(next);
                }
                current = next;
            }
        }

        private void DeleteCreatedEmptyState(ICollection<Exception> failures)
        {
            for (int i = _createdAssetFolders.Count - 1; i >= 0; i--)
            {
                string folder = _createdAssetFolders[i];
                try
                {
                    string absolute = Path.GetFullPath(folder);
                    if (Directory.Exists(absolute)
                        && Directory.GetFileSystemEntries(absolute).Length == 0
                        && !AssetDatabase.DeleteAsset(folder))
                        throw new IOException("Could not delete empty ReviewNeeded folder: " + folder);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            for (int i = 0; i < _createdOverrideDirectories.Count; i++)
            {
                string directory = _createdOverrideDirectories[i];
                try
                {
                    if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
        }

        private void CleanupTemps(ICollection<Exception> failures)
        {
            DeleteFile(CatalogTemporaryPath, failures);
            DeleteFile(CatalogTemporaryPath + ".meta", failures);
            DeleteFile(OverrideTemporaryPath, failures);
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

        private static bool RefreshRuntime()
        {
            VfxPublishedBindingEditorService.RefreshRuntimeBindingSource();
            return true;
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
            if (!_completed) DeleteCreatedEmptyState(failures);
        }
    }
}
