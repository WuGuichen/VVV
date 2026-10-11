using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Publishes the machine-local playtest override JSON and refreshes its runtime source atomically.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Library files have no Unity metadata.</remarks>
    internal sealed class VfxPlaytestOverridePublishTransaction : IDisposable
    {
        private readonly string _path;
        private readonly Action<string, string> _writer;
        private readonly Func<string, string, bool> _publisher;
        private readonly Func<bool> _runtimeRefresher;
        private readonly List<string> _createdDirectories;
        private bool _snapshotTaken;
        private bool _publishAttempted;
        private bool _completed;
        private bool _hadFile;
        private byte[] _originalBytes;

        internal VfxPlaytestOverridePublishTransaction(
            string path,
            Action<string, string> writer = null,
            Func<string, string, bool> publisher = null,
            Func<bool> runtimeRefresher = null)
        {
            _path = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("Playtest override directory is missing.");
            _createdDirectories = FindMissingDirectories(directory);
            TemporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            _writer = writer ?? WriteText;
            _publisher = publisher ?? PublishFile;
            _runtimeRefresher = runtimeRefresher ?? RefreshRuntime;
        }

        internal string TemporaryPath { get; }

        internal bool Publish(VfxPlaytestOverrideDocument document, out string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
                document.ProjectId = Application.dataPath;
                string json = JsonConvert.SerializeObject(document, Formatting.Indented) + Environment.NewLine;
                _writer(TemporaryPath, json);
                ValidateRoundTrip(TemporaryPath, document);
                _hadFile = File.Exists(_path);
                _originalBytes = _hadFile ? File.ReadAllBytes(_path) : null;
                _snapshotTaken = true;
                _publishAttempted = true;
                if (!_publisher(TemporaryPath, _path))
                    throw new IOException("Playtest override publish seam rejected the replacement.");
                ValidateRoundTrip(_path, document);
                if (!_runtimeRefresher())
                    throw new InvalidOperationException("Runtime playtest override refresh failed.");
                ValidateRoundTrip(_path, document);
                _completed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Playtest override publish failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Playtest override publish failed: " + exception.Message
                        + " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private static void ValidateRoundTrip(string path, VfxPlaytestOverrideDocument expected)
        {
            VfxPlaytestOverrideDocument roundTrip =
                JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(File.ReadAllText(path));
            JObject expectedJson = JObject.FromObject(expected);
            JObject actualJson = JObject.Parse(File.ReadAllText(path));
            if (roundTrip == null
                || !string.Equals(roundTrip.ProjectId, Application.dataPath, StringComparison.OrdinalIgnoreCase)
                || !JToken.DeepEquals(expectedJson, actualJson))
                throw new InvalidDataException("Playtest override JSON roundtrip/project validation failed.");
        }

        private void RollBack()
        {
            var failures = new List<Exception>();
            if (_snapshotTaken && _publishAttempted)
            {
                try
                {
                    if (_hadFile) File.WriteAllBytes(_path, _originalBytes);
                    else if (File.Exists(_path)) File.Delete(_path);
                }
                catch (Exception exception) { failures.Add(exception); }
                try
                {
                    if (!_runtimeRefresher())
                        throw new InvalidOperationException("Runtime playtest override rollback refresh failed.");
                    if (File.Exists(_path) != _hadFile
                        || (_hadFile && !File.ReadAllBytes(_path).AsSpan().SequenceEqual(_originalBytes)))
                        throw new IOException("Restored playtest override bytes did not match the original file.");
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            DeleteFile(TemporaryPath, failures);
            DeleteCreatedEmptyDirectories(failures);
            _publishAttempted = false;
            if (failures.Count > 0)
                throw new AggregateException("Playtest override rollback or cleanup did not complete.", failures);
        }

        private void DeleteCreatedEmptyDirectories(ICollection<Exception> failures)
        {
            for (int i = 0; i < _createdDirectories.Count; i++)
            {
                string directory = _createdDirectories[i];
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
            DeleteFile(TemporaryPath, failures);
            if (!_completed) DeleteCreatedEmptyDirectories(failures);
            if (failures.Count > 0)
                throw new AggregateException("Playtest override transaction cleanup failed.", failures);
        }
    }
}
