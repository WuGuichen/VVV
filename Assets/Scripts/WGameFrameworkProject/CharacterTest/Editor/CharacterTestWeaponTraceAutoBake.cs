using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Editor.CharacterImport;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Rebuilds imported character trace data after its authoring inputs change.</summary>
    [InitializeOnLoad]
    internal static class CharacterTestWeaponTraceAutoBake
    {
        private const string PackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages";
        private const string ActionLibraryRoot = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/";
        private const string PendingKey = "MxFramework.CharacterTest.WeaponTraceAutoBake.Pending.v1";
        private const string ActiveKey = "MxFramework.CharacterTest.WeaponTraceAutoBake.Active.v1";
        private const string RetryPrefix = "MxFramework.CharacterTest.WeaponTraceAutoBake.Retry.v1.";
        private static readonly SortedSet<string> Pending = new(StringComparer.Ordinal);
        private static double _runAfter;
        private static bool _running;
        private static InputSignatureBatch _inputBatch;
        private static CharacterImportedPackagePrefabBuilder.WeaponTraceBakeJob _job;
        private static string _jobId;
        private static string _jobSourceSignature;
        private static string _jobBakeSignature;
        private static bool _cancelRequested;
        private static System.Diagnostics.Stopwatch _jobWatch;
        private static double _maxSliceMilliseconds;
        private static int _sliceCount;

        static CharacterTestWeaponTraceAutoBake()
        {
            foreach (string id in SessionState.GetString(PendingKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                Pending.Add(id);
            try
            {
                string pendingFile = PendingPath();
                if (File.Exists(pendingFile))
                    foreach (string id in File.ReadAllText(pendingFile).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                        if (PackageExists(id)) Pending.Add(id);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("[CharacterTest] 自动烘焙队列恢复失败：" + error.Message); }
            SavePending();
            _runAfter = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += CancelActiveJob;
            EditorApplication.quitting += CancelActiveJob;
        }

        internal static void Notify(params string[][] changes)
        {
            bool changed = false;
            bool inputChanged = false;
            foreach (string[] batch in changes)
            {
                if (batch == null) continue;
                foreach (string path in batch)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    IEnumerable<string> packages = _inputBatch != null && _inputBatch.TryGetOwners(path, out string[] owners)
                        ? owners : PackagesForInput(path);
                    foreach (string id in packages)
                    {
                        if (id == _jobId) _cancelRequested = true;
                        inputChanged = true;
                        changed |= Pending.Add(id);
                    }
                }
            }
            if (!inputChanged) return;
            _inputBatch?.Invalidate();
            _inputBatch = null;
            if (changed) SavePending();
            _runAfter = EditorApplication.timeSinceStartup + 1;
        }

        private static IEnumerable<string> PackagesForInput(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) yield break;
            path = path.Replace('\\', '/');
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 5);
            if (path.StartsWith(ActionLibraryRoot, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/character_actions.json", StringComparison.OrdinalIgnoreCase))
            {
                string library = path.Substring(ActionLibraryRoot.Length).Split('/')[0];
                foreach (string id in library.Equals("Common", StringComparison.OrdinalIgnoreCase)
                    ? AllPackageIds() : PackageExists(library) ? new[] { library } : Array.Empty<string>())
                    yield return id;
                yield break;
            }
            const string packagePrefix = PackageRoot + "/";
            if (path.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string[] segments = path.Substring(packagePrefix.Length).Split('/');
                if (segments.Length >= 3 && PackageExists(segments[0]))
                {
                    string local = string.Join("/", segments.Skip(1));
                    if (local.StartsWith("config/", StringComparison.OrdinalIgnoreCase)
                            && local.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || local.StartsWith("resources/models/", StringComparison.OrdinalIgnoreCase)
                            && local.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        yield return segments[0];
                }
                yield break;
            }
            const string actionRoot = "Assets/MxFrameworkGenerated/CharacterActions/";
            if (path.StartsWith(actionRoot, StringComparison.OrdinalIgnoreCase))
            {
                string[] segments = path.Substring(actionRoot.Length).Split('/');
                if (segments.Length >= 3 && PackageExists(segments[1]))
                    yield return segments[1];
                yield break;
            }
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || !(path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))) yield break;
            foreach (string id in AllPackageIds())
            {
                bool referenced;
                try { referenced = CollectInputFiles(id).Contains(path, StringComparer.OrdinalIgnoreCase); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                catch (ArgumentException) { continue; }
                catch (JsonException) { continue; }
                if (referenced) yield return id;
            }
        }

        private static string[] AllPackageIds()
        {
            string root = FullPath(PackageRoot);
            if (!Directory.Exists(root)) return Array.Empty<string>();
            return Directory.GetDirectories(root).Select(Path.GetFileName).Where(PackageExists)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        private static bool PackageExists(string id)
            => !string.IsNullOrWhiteSpace(id)
                && id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
                && File.Exists(FullPath(PackageRoot + "/" + id + "/config/animation_clip_registry.json"));

        private static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            {
                CancelActiveJob();
                return;
            }
            if (EditorApplication.isUpdating) return;
            if (_job != null) { AdvanceActiveJob(); return; }
            if (_running || EditorApplication.timeSinceStartup < _runAfter) return;
            RecoverInterruptedBake();
            if (Pending.Count == 0) return;
            string id = Pending.Min;
            if (!PackageExists(id)) { Complete(id); return; }
            try
            {
                if (_inputBatch == null || !_inputBatch.Contains(id) || !_inputBatch.IsCurrent())
                {
                    _inputBatch = CreateInputSignatureBatch(Pending.ToArray());
                    Debug.Log("[CharacterTest] Bake preflight: " + Pending.Count + " packages, "
                        + _inputBatch.ReadFileCount + " files, " + _inputBatch.CacheHitCount + " cache hits, "
                        + (_inputBatch.ReadBytes / 1048576d).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + " MiB, " + _inputBatch.ElapsedMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
                }
                if (!_inputBatch.IsCurrent()) { _inputBatch = null; _runAfter = EditorApplication.timeSinceStartup + 1; return; }
                string sourceSignature = _inputBatch.GetSignature(id);
                string bakeSignature = _inputBatch.GetBakeSignature(id);
                if (ReadStamp(StampPath(id)) == bakeSignature)
                {
                    InputSignatureBatch fresh = CreateInputSignatureBatch(new[] { id });
                    if (!fresh.IsCurrent()) { _inputBatch = null; _runAfter = EditorApplication.timeSinceStartup + 1; return; }
                    sourceSignature = fresh.GetSignature(id);
                    bakeSignature = fresh.GetBakeSignature(id);
                    if (ReadStamp(StampPath(id)) == bakeSignature && ExistingBakeIsValid(id))
                    {
                        Debug.Log("[CharacterTest] Package " + id + ": sampling inputs unchanged; keeping existing bake.");
                        Complete(id);
                        return;
                    }
                    _inputBatch = null;
                }
                string output = FullPath(PackageRoot + "/" + id + "/generated/weapon_trace_bake_catalog.json");
                long before = File.Exists(output) ? File.GetLastWriteTimeUtc(output).Ticks : 0;
                SessionState.SetInt(RetryPrefix + id, SessionState.GetInt(RetryPrefix + id, 0) + 1);
                SessionState.SetString(ActiveKey, id + "|" + sourceSignature + "|" + bakeSignature + "|" + before);
                _jobId = id;
                _jobSourceSignature = sourceSignature;
                _jobBakeSignature = bakeSignature;
                _cancelRequested = false;
                _sliceCount = 0;
                _maxSliceMilliseconds = 0;
                _running = true;
                _jobWatch = System.Diagnostics.Stopwatch.StartNew();
                _job = CharacterImportedPackagePrefabBuilder.CreateWeaponTraceBakeJob(PackageRoot + "/" + id);
            }
            catch (Exception error)
            {
                Fail(id, error.Message);
                FinishActiveJob(true);
            }
        }

        private static void AdvanceActiveJob()
        {
            string id = _jobId;
            try
            {
                if (_cancelRequested) { CancelActiveJob(); return; }
                var slice = System.Diagnostics.Stopwatch.StartNew();
                bool more = _job.Step(64, 8);
                slice.Stop();
                _sliceCount++;
                _maxSliceMilliseconds = Math.Max(_maxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
                if (_cancelRequested) { CancelActiveJob(); return; }
                if (more) return;
                _jobWatch.Stop();
                CharacterWeaponTraceBakeBuildReport report = _job.Report;
                if (!report.Success)
                {
                    var first = report.Issues[0];
                    Fail(id, first.Code + ": " + first.Message + " (" + report.Issues.Length + " issues)");
                    FinishActiveJob(false);
                    return;
                }
                var verifyWatch = System.Diagnostics.Stopwatch.StartNew();
                InputSignatureBatch fresh = CreateInputSignatureBatch(new[] { id });
                verifyWatch.Stop();
                if (!fresh.IsCurrent() || fresh.GetSignature(id) != _jobSourceSignature
                    || fresh.GetBakeSignature(id) != _jobBakeSignature)
                {
                    CancelActiveJob();
                    return;
                }
                _job.Publish();
                if (_cancelRequested || !fresh.IsCurrent()) { CancelActiveJob(); return; }
                WriteStamp(id, _jobBakeSignature);
                Debug.Log("[CharacterTest] Package " + id + " baked: " + report.ArtifactCount + " clips, "
                    + report.CheckedFrameCount + " frames; " + _sliceCount + " slices, max slice "
                    + _maxSliceMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    + " ms, sampling " + _jobWatch.ElapsedMilliseconds + " ms, verification " + verifyWatch.ElapsedMilliseconds + " ms.");
                Complete(id);
                FinishActiveJob(false);
            }
            catch (Exception error)
            {
                Fail(id, error.Message);
                FinishActiveJob(true);
            }
        }

        private static void CancelActiveJob()
        {
            if (_job == null && _jobId == null) return;
            FinishActiveJob(true);
        }

        private static bool ExistingBakeIsValid(string id)
        {
            string root = FullPath(PackageRoot + "/" + id);
            CharacterWeaponTraceBakeStorage.Load(root, id, false, out var weapons);
            CharacterWeaponTraceBakeStorage.Load(root, id, true, out var contacts);
            return weapons.Success && contacts.Success;
        }

        private static void FinishActiveJob(bool invalidateInputs)
        {
            _job?.Dispose();
            _job = null;
            _jobId = null;
            _running = false;
            _cancelRequested = false;
            if (invalidateInputs) _inputBatch = null;
            SessionState.EraseString(ActiveKey);
            _runAfter = EditorApplication.timeSinceStartup + 0.25;
        }

        private static void RecoverInterruptedBake()
        {
            string active = SessionState.GetString(ActiveKey, "");
            if (active.Length == 0) return;
            string[] parts = active.Split('|');
            SessionState.EraseString(ActiveKey);
            int timeIndex = parts.Length == 4 ? 3 : 2;
            if ((parts.Length != 3 && parts.Length != 4) || !long.TryParse(parts[timeIndex], out long before)) return;
            string id = parts[0];
            if (!Pending.Contains(id)) return;
            string output = FullPath(PackageRoot + "/" + id + "/generated/weapon_trace_bake_catalog.json");
            try
            {
                InputSignatureBatch fresh = CreateInputSignatureBatch(new[] { id });
                if (fresh.IsCurrent() && File.Exists(output) && File.GetLastWriteTimeUtc(output).Ticks > before
                    && fresh.GetSignature(id) == parts[1]
                    && (parts.Length == 3 || fresh.GetBakeSignature(id) == parts[2])
                    && ExistingBakeIsValid(id))
                {
                    WriteStamp(id, fresh.GetBakeSignature(id));
                    Complete(id);
                    return;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException || error is JsonException)
            { Debug.LogWarning("[CharacterTest] Interrupted bake verification unavailable: " + error.Message); }
            SessionState.SetInt(RetryPrefix + id, 0);
        }

        private static string[] CollectInputFiles(string id)
            => CharacterImportedPackagePrefabBuilder.CollectWeaponTraceBakeInputAssetPaths(PackageRoot + "/" + id);

        internal static InputSignatureBatch CreateInputSignatureBatch(string[] ids)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var inputs = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var projections = new Dictionary<string, string>(StringComparer.Ordinal);
            var errors = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                try
                {
                    inputs[id] = CharacterImportedPackagePrefabBuilder.CollectWeaponTraceBakeInputAssetPaths(
                        PackageRoot + "/" + id, out string projection);
                    projections[id] = projection;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                    || error is ArgumentException || error is JsonException)
                { errors[id] = error.Message; }
            }
            InputSignatureBatch batch = InputSignatureBatch.Build(inputs, projections);
            foreach (var error in errors) batch.AddError(error.Key, error.Value);
            batch.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            return batch;
        }

        private static string InputSignature(string id)
        {
            // Postflight and skip checks deliberately use a new cache and read file contents again.
            InputSignatureBatch fresh = CreateInputSignatureBatch(new[] { id });
            if (!fresh.IsCurrent())
                throw new IOException("Bake inputs changed while their signatures were being collected.");
            return fresh.GetSignature(id);
        }

        internal sealed class InputSignatureBatch
        {
            private readonly Dictionary<string, string> _signatures = new(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _bakeSignatures = new(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);
            private readonly Dictionary<string, FileDigest> _files = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, HashSet<string>> _owners = new(StringComparer.OrdinalIgnoreCase);
            private bool _changedDuringCollection;

            internal int ReadFileCount { get; private set; }
            internal int CacheHitCount { get; private set; }
            internal long ReadBytes { get; private set; }
            internal double ElapsedMilliseconds { get; set; }

            internal static InputSignatureBatch Build(IReadOnlyDictionary<string, string[]> inputs,
                IReadOnlyDictionary<string, string> actionProjections = null)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var batch = new InputSignatureBatch();
                foreach (var input in inputs)
                {
                    try
                    {
                        string projection = null;
                        actionProjections?.TryGetValue(input.Key, out projection);
                        batch._signatures[input.Key] = batch.ComputeSignature(input.Key, input.Value, projection);
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                        || error is ArgumentException)
                    { batch.AddError(input.Key, error.Message); }
                }
                batch.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
                return batch;
            }

            internal void AddError(string id, string message) => _errors[id] = message;
            internal void Invalidate() => _changedDuringCollection = true;
            internal bool Contains(string id) => _signatures.ContainsKey(id) || _errors.ContainsKey(id);
            internal string GetSignature(string id)
            {
                if (_errors.TryGetValue(id, out string error)) throw new InvalidDataException(error);
                return _signatures[id];
            }
            internal string GetBakeSignature(string id)
            {
                if (_errors.TryGetValue(id, out string error)) throw new InvalidDataException(error);
                return _bakeSignatures[id];
            }

            internal bool IsCurrent()
            {
                if (_changedDuringCollection) return false;
                foreach (var entry in _files)
                    if (!entry.Value.Version.Matches(entry.Key)) return false;
                return true;
            }

            internal bool TryGetOwners(string assetPath, out string[] owners)
            {
                if (_owners.TryGetValue(FullPath(assetPath), out HashSet<string> ids))
                {
                    owners = ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                    return true;
                }
                owners = Array.Empty<string>();
                return false;
            }

            private string ComputeSignature(string id, string[] assets, string actionProjection)
            {
                using var hash = SHA256.Create();
                using var bakeHash = SHA256.Create();
                byte[] version = Encoding.UTF8.GetBytes("contact-motion-independent-v1\n");
                hash.TransformBlock(version, 0, version.Length, version, 0);
                byte[] bakeVersion = Encoding.UTF8.GetBytes("character-sampling-v2\n");
                bakeHash.TransformBlock(bakeVersion, 0, bakeVersion.Length, bakeVersion, 0);
                foreach (string asset in new SortedSet<string>(assets, StringComparer.Ordinal))
                {
                    string path = FullPath(asset);
                    if (!_owners.TryGetValue(path, out HashSet<string> owners))
                        _owners[path] = owners = new HashSet<string>(StringComparer.Ordinal);
                    owners.Add(id);
                    byte[] identity = Encoding.UTF8.GetBytes(asset + "\n");
                    hash.TransformBlock(identity, 0, identity.Length, identity, 0);
                    byte[] digest = ReadDigest(path);
                    if (digest != null) hash.TransformBlock(digest, 0, digest.Length, digest, 0);
                    if (actionProjection != null && asset.Replace('\\', '/').EndsWith("/character_actions.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                    bakeHash.TransformBlock(identity, 0, identity.Length, identity, 0);
                    if (digest != null) bakeHash.TransformBlock(digest, 0, digest.Length, digest, 0);
                }
                if (actionProjection != null)
                {
                    byte[] projection = Encoding.UTF8.GetBytes(actionProjection);
                    bakeHash.TransformBlock(projection, 0, projection.Length, projection, 0);
                }
                bakeHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                _bakeSignatures[id] = BitConverter.ToString(bakeHash.Hash).Replace("-", "").ToLowerInvariant();
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }

            private byte[] ReadDigest(string path)
            {
                if (_files.TryGetValue(path, out FileDigest cached))
                {
                    if (cached.Version.Matches(path))
                    {
                        CacheHitCount++;
                        return cached.Digest;
                    }
                    _changedDuringCollection = true;
                }
                var version = new FileVersion(path);
                byte[] digest = null;
                if (version.Exists)
                {
                    using var stream = File.OpenRead(path);
                    using var content = SHA256.Create();
                    digest = content.ComputeHash(stream);
                    ReadFileCount++;
                    ReadBytes += stream.Length;
                }
                _files[path] = new FileDigest(version, digest);
                return digest;
            }

            private sealed class FileDigest
            {
                internal FileDigest(FileVersion version, byte[] digest) { Version = version; Digest = digest; }
                internal FileVersion Version { get; }
                internal byte[] Digest { get; }
            }

            private readonly struct FileVersion
            {
                internal FileVersion(string path)
                {
                    var file = new FileInfo(path);
                    Exists = file.Exists;
                    _length = Exists ? file.Length : 0;
                    _write = Exists ? file.LastWriteTimeUtc : default;
                    _creation = Exists ? file.CreationTimeUtc : default;
                }
                internal bool Exists { get; }
                private readonly long _length;
                private readonly DateTime _write;
                private readonly DateTime _creation;
                internal bool Matches(string path)
                {
                    var current = new FileVersion(path);
                    return Exists == current.Exists && _length == current._length
                        && _write == current._write && _creation == current._creation;
                }
            }
        }

        private static string FullPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static string RelativePath(string fullPath)
            => fullPath.Substring(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Length + 1)
                .Replace('\\', '/');

        private static string StampPath(string id)
            => FullPath("Library/MxFramework/CharacterTest/WeaponTraceAutoBake/" + id + ".sha256");

        private static string PendingPath()
            => FullPath("Library/MxFramework/CharacterTest/WeaponTraceAutoBake/pending.txt");

        private static string ReadStamp(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }

        private static void WriteStamp(string id, string signature)
        {
            string path = StampPath(id);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, signature, Encoding.ASCII);
        }

        private static void Complete(string id)
        {
            Pending.Remove(id);
            if (Pending.Count == 0) _inputBatch = null;
            SessionState.EraseString(ActiveKey);
            SessionState.SetInt(RetryPrefix + id, 0);
            SavePending();
        }

        private static void Fail(string id, string reason)
        {
            Debug.LogError("[CharacterTest] 角色 " + id + " 武器轨迹自动烘焙失败：" + reason);
            Complete(id);
        }

        private static void SavePending()
        {
            string value = string.Join("|", Pending);
            SessionState.SetString(PendingKey, value);
            try
            {
                string path = PendingPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value, Encoding.ASCII);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("[CharacterTest] 自动烘焙队列保存失败：" + error.Message); }
        }
    }
}
