using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Publishes the generated Unity VFX projection and its scene-profile reference atomically.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Existing asset bytes, metadata, GUID, and profile reference are restored on failure.</remarks>
    internal sealed class VfxUnityProjectionPublishTransaction : IDisposable
    {
        private readonly string _projectRoot;
        private readonly string _projectionAssetPath;
        private readonly string _projectionPath;
        private readonly UnityEngine.Object _profile;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<string, bool> _importer;
        private readonly Func<bool> _runtimeRefresher;
        private readonly List<string> _createdFolders = new List<string>();
        private bool _snapshotTaken;
        private bool _publishAttempted;
        private bool _completed;
        private bool _hadProjection;
        private byte[] _originalProjectionBytes;
        private byte[] _originalProjectionMeta;
        private string _originalProjectionGuid;
        private UnityEngine.Object _originalProfileReference;
        private string _originalProfileReferencePath;

        internal VfxUnityProjectionPublishTransaction(
            string projectionAssetPath,
            UnityEngine.Object profile,
            Func<string, string, bool> publisher = null,
            Func<string, bool> importer = null,
            Func<bool> runtimeRefresher = null)
        {
            _projectionAssetPath = projectionAssetPath.Replace('\\', '/');
            _profile = profile;
            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            _projectionPath = Absolute(_projectionAssetPath);
            string directory = Path.GetDirectoryName(_projectionAssetPath)?.Replace('\\', '/')
                ?? throw new InvalidOperationException("Projection directory is missing.");
            ProvisionalAssetPath = directory + "/__"
                + Path.GetFileNameWithoutExtension(_projectionAssetPath) + "."
                + Guid.NewGuid().ToString("N") + ".tmp.asset";
            ProvisionalPath = Absolute(ProvisionalAssetPath);
            _publisher = publisher ?? PublishFile;
            _importer = importer ?? ImportProjection;
            _runtimeRefresher = runtimeRefresher ?? RefreshRuntime;
        }

        internal string ProvisionalAssetPath { get; }
        internal string ProvisionalPath { get; }

        internal bool Publish(
            string manifestHash,
            List<VfxUnityPrefabBinding> bindings,
            out string message)
        {
            try
            {
                EnsureAssetFolders(Path.GetDirectoryName(_projectionAssetPath)?.Replace('\\', '/'));
                PrepareProvisional(manifestHash, bindings);
                SnapshotOriginalState();
                _publishAttempted = true;
                if (!_publisher(ProvisionalPath, _projectionPath))
                    throw new IOException("Projection publish seam rejected the replacement.");
                if (File.Exists(ProvisionalPath + ".meta"))
                    File.Delete(ProvisionalPath + ".meta");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!_importer(_projectionAssetPath))
                    throw new IOException("Unity failed to import the generated VFX projection.");
                VfxPublishedUnityBindingAsset published =
                    AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(_projectionAssetPath);
                ValidateProjection(published, manifestHash, bindings);
                published.name = Path.GetFileNameWithoutExtension(_projectionAssetPath);
                EditorUtility.SetDirty(published);
                SetProfileReference(published);
                AssetDatabase.SaveAssets();
                if (!_runtimeRefresher())
                    throw new InvalidOperationException("Runtime VFX binding source refresh failed.");
                ValidateProjection(
                    AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(_projectionAssetPath),
                    manifestHash,
                    bindings);
                ValidateProfileReference(published);
                AssertGuidPreserved();
                _completed = true;
                CleanupProvisional();
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Published VFX projection failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Published VFX projection failed: " + exception.Message
                        + " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void PrepareProvisional(string manifestHash, List<VfxUnityPrefabBinding> bindings)
        {
            if (string.IsNullOrWhiteSpace(manifestHash) || bindings == null)
                throw new InvalidDataException("Projection manifest hash or bindings are missing.");
            var provisional = ScriptableObject.CreateInstance<VfxPublishedUnityBindingAsset>();
            try
            {
                provisional.Configure(manifestHash, bindings);
                AssetDatabase.CreateAsset(provisional, ProvisionalAssetPath);
                provisional.name = Path.GetFileNameWithoutExtension(_projectionAssetPath);
                EditorUtility.SetDirty(provisional);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    ProvisionalAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                ValidateProjection(
                    AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(ProvisionalAssetPath),
                    manifestHash,
                    bindings);
            }
            finally
            {
                if (provisional != null && !AssetDatabase.Contains(provisional))
                    UnityEngine.Object.DestroyImmediate(provisional);
            }
        }

        private void SnapshotOriginalState()
        {
            _hadProjection = File.Exists(_projectionPath);
            _originalProjectionBytes = _hadProjection ? File.ReadAllBytes(_projectionPath) : null;
            _originalProjectionMeta = File.Exists(_projectionPath + ".meta")
                ? File.ReadAllBytes(_projectionPath + ".meta")
                : null;
            _originalProjectionGuid = AssetDatabase.AssetPathToGUID(_projectionAssetPath);
            _originalProfileReference = ReadProfileReference();
            _originalProfileReferencePath = AssetDatabase.GetAssetPath(_originalProfileReference);
            _snapshotTaken = true;
        }

        private void ValidateProjection(
            VfxPublishedUnityBindingAsset asset,
            string manifestHash,
            IReadOnlyList<VfxUnityPrefabBinding> expected)
        {
            if (asset == null
                || !string.Equals(asset.ManifestHash, manifestHash, StringComparison.Ordinal)
                || asset.Bindings.Count != expected.Count)
                throw new InvalidDataException("Generated VFX projection manifest hash or binding count is invalid.");
            for (int i = 0; i < expected.Count; i++)
            {
                VfxUnityPrefabBinding actual = asset.Bindings[i];
                VfxUnityPrefabBinding wanted = expected[i];
                if (actual == null
                    || wanted == null
                    || actual.ResourceKey != wanted.ResourceKey
                    || !string.Equals(actual.Address, wanted.Address, StringComparison.Ordinal)
                    || !string.Equals(
                        AssetDatabase.GetAssetPath(actual.Prefab),
                        AssetDatabase.GetAssetPath(wanted.Prefab),
                        StringComparison.Ordinal))
                    throw new InvalidDataException("Generated VFX projection binding content is invalid at index " + i + ".");
            }
        }

        private UnityEngine.Object ReadProfileReference()
        {
            if (_profile == null) return null;
            var serialized = new SerializedObject(_profile);
            SerializedProperty property = serialized.FindProperty("_publishedVfxBindings")
                ?? throw new InvalidOperationException("CharacterTest profile projection property is missing.");
            return property.objectReferenceValue;
        }

        private void SetProfileReference(UnityEngine.Object value)
        {
            if (_profile == null) return;
            var serialized = new SerializedObject(_profile);
            SerializedProperty property = serialized.FindProperty("_publishedVfxBindings")
                ?? throw new InvalidOperationException("CharacterTest profile projection property is missing.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_profile);
        }

        private void ValidateProfileReference(VfxPublishedUnityBindingAsset published)
        {
            if (_profile != null && ReadProfileReference() != published)
                throw new InvalidDataException("CharacterTest profile does not reference the published VFX projection.");
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken && _publishAttempted)
            {
                RestoreFile(_projectionPath, _hadProjection, _originalProjectionBytes, failures);
                RestoreFile(
                    _projectionPath + ".meta",
                    _originalProjectionMeta != null,
                    _originalProjectionMeta,
                    failures);
                CleanupProvisional(failures);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (_hadProjection && !_importer(_projectionAssetPath))
                        throw new IOException("Unity failed to reimport the restored VFX projection.");
                    UnityEngine.Object restoredReference = string.IsNullOrWhiteSpace(_originalProfileReferencePath)
                        ? _originalProfileReference
                        : AssetDatabase.LoadMainAssetAtPath(_originalProfileReferencePath);
                    SetProfileReference(restoredReference);
                    AssetDatabase.SaveAssets();
                    if (!_runtimeRefresher())
                        throw new InvalidOperationException("Runtime VFX binding source rollback refresh failed.");
                    VerifyRestored(restoredReference);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            CleanupProvisional(failures);
            DeleteCreatedEmptyFolders(failures);
            _publishAttempted = false;
            if (failures.Count > 0)
                throw new AggregateException("Projection rollback or cleanup did not complete.", failures);
        }

        private void VerifyRestored(UnityEngine.Object restoredReference)
        {
            VerifyFile(_projectionPath, _hadProjection, _originalProjectionBytes);
            VerifyFile(
                _projectionPath + ".meta",
                _originalProjectionMeta != null,
                _originalProjectionMeta);
            AssertGuidPreserved();
            if (_profile != null && ReadProfileReference() != restoredReference)
                throw new IOException("CharacterTest profile projection reference was not restored.");
        }

        private void AssertGuidPreserved()
        {
            if (!string.IsNullOrWhiteSpace(_originalProjectionGuid)
                && !string.Equals(
                    AssetDatabase.AssetPathToGUID(_projectionAssetPath),
                    _originalProjectionGuid,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Generated VFX projection GUID changed during publish.");
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
                        throw new IOException("Could not create projection asset folder: " + next);
                    _createdFolders.Add(next);
                }
                current = next;
            }
        }

        private void DeleteCreatedEmptyFolders(ICollection<Exception> failures)
        {
            for (int i = _createdFolders.Count - 1; i >= 0; i--)
            {
                string folder = _createdFolders[i];
                try
                {
                    string absolute = Absolute(folder);
                    if (Directory.Exists(absolute)
                        && Directory.GetFileSystemEntries(absolute).Length == 0
                        && !AssetDatabase.DeleteAsset(folder))
                        throw new IOException("Could not delete empty projection folder: " + folder);
                }
                catch (Exception exception) { failures.Add(exception); }
            }
        }

        private string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(_projectRoot, assetPath));

        private void CleanupProvisional()
        {
            var failures = new List<Exception>();
            CleanupProvisional(failures);
            if (failures.Count > 0)
                throw new AggregateException("Could not clean provisional projection.", failures);
        }

        private void CleanupProvisional(ICollection<Exception> failures)
        {
            DeleteFile(ProvisionalPath, failures);
            DeleteFile(ProvisionalPath + ".meta", failures);
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
                throw new IOException("Restored projection file did not match original bytes: " + path);
        }

        private static bool PublishFile(string temporaryPath, string outputPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null, true);
            else File.Move(temporaryPath, outputPath);
            return true;
        }

        private static bool ImportProjection(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(assetPath) != null;
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
            if (!_completed && _publishAttempted)
            {
                try { RollBack(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            CleanupProvisional(failures);
            if (!_completed) DeleteCreatedEmptyFolders(failures);
            if (failures.Count > 0)
                throw new AggregateException("Projection transaction cleanup failed.", failures);
        }
    }
}
