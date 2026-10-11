using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Config.Levels;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    // Directory of references only. Geometry, actors and module settings remain in v3 envelopes.
    internal sealed class CharacterTestLevelCatalog
    {
        internal const string CurrentFormat = "mx.character-test.level-directory.v1";
        internal const string RuntimeRelativePath = "MxFramework/CharacterTest/Levels/directory.json";
        internal const string AuthoringRelativePath = "Assets/Config/MxFramework/Levels/directory.json";
        public string Format = CurrentFormat;
        public string DefaultLevelId = string.Empty;
        public List<CharacterTestLevelEntry> Levels = new List<CharacterTestLevelEntry>();

        internal static CharacterTestLevelCatalog Read(string path)
        {
            var catalog = JsonConvert.DeserializeObject<CharacterTestLevelCatalog>(File.ReadAllText(path));
            if (catalog == null || catalog.Format != CurrentFormat || catalog.Levels == null || catalog.Levels.Count == 0)
                throw new InvalidDataException("Invalid level directory: " + path);
            if (catalog.Levels.Any(e => e == null || string.IsNullOrWhiteSpace(e.LevelId) || string.IsNullOrWhiteSpace(e.Path))
                || catalog.Levels.GroupBy(e => e.LevelId, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new InvalidDataException("Missing or duplicate level identity.");
            if (!catalog.Levels.Any(e => e.LevelId == catalog.DefaultLevelId)) throw new InvalidDataException("Default level is missing.");
            return catalog;
        }

        internal MxLevelAuthoringDocument Load(string levelId, string root, bool requirePublished, out string path)
        {
            var entry = Levels.SingleOrDefault(e => e.LevelId == levelId) ?? throw new InvalidDataException("Unknown levelId: " + levelId);
            path = SafePath(root, entry.Path);
            string json = File.ReadAllText(path);
            if (requirePublished && (string.IsNullOrEmpty(entry.DocumentHash) || Hash(json) != entry.DocumentHash))
                throw new InvalidDataException(levelId + "/published-document-hash");
            var envelope = MxLevelAuthoringJson.LoadFromJson(json);
            if (envelope.LevelId != entry.LevelId) throw new InvalidDataException(levelId + "/document-identity");
            var report = MxLevelAuthoringValidator.Validate(envelope);
            if (report.HasErrors) throw new InvalidDataException(levelId + "/envelope: " + report);
            return envelope;
        }

        internal static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Relative level path required.");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!result.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Level path escapes directory.");
            for (var current = new FileInfo(result).Directory; current != null && current.FullName.Length >= fullRoot.Length - 1; current = current.Parent)
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked level directories are unsupported.");
            if (File.Exists(result) && (File.GetAttributes(result) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked level document is unsupported.");
            return result;
        }

        internal static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
        }

        // Immutable content-addressed document is written first. One atomic directory
        // replacement publishes the reference; failed preflight cannot change live data.
        internal static void Publish(MxLevelAuthoringDocument envelope, string name, string runtimeRoot,
            Action beforeDirectoryCommit = null)
        {
            string directoryPath = Path.Combine(runtimeRoot, "directory.json");
            var catalog = File.Exists(directoryPath) ? Read(directoryPath) : new CharacterTestLevelCatalog { DefaultLevelId = envelope.LevelId };
            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            string hash = Hash(json), relative = "documents/" + hash + ".level.json";
            string target = SafePath(runtimeRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target)) AtomicWrite(target, json);
            else if (Hash(File.ReadAllText(target)) != hash) throw new InvalidDataException("Published immutable document collision.");
            var entry = catalog.Levels.SingleOrDefault(e => e.LevelId == envelope.LevelId);
            if (entry == null) { entry = new CharacterTestLevelEntry { LevelId = envelope.LevelId }; catalog.Levels.Add(entry); }
            entry.Path = relative; entry.DisplayName = name; entry.DocumentHash = hash;
            beforeDirectoryCommit?.Invoke();
            AtomicWrite(directoryPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));
        }

        // The authoring file stays exclusively locked through the publication
        // commit. Source failures cannot update the runtime directory; runtime failures
        // restore the exact previous source bytes before releasing that same lock.
        internal static string PublishWithAuthoringSource(MxLevelAuthoringDocument envelope, string name,
            string runtimeRoot, string sourcePath, string expectedSourceHash, Action<string> fault = null)
        {
            var validation = MxLevelAuthoringValidator.Validate(envelope);
            if (validation.HasErrors) throw new InvalidDataException("Invalid level envelope: " + validation);
            if ((File.GetAttributes(sourcePath) & FileAttributes.ReadOnly) != 0)
                throw new IOException("Authoring source is read-only: " + sourcePath);
            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            byte[] replacement = new UTF8Encoding(false).GetBytes(json);
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var before = new MemoryStream();
            source.CopyTo(before);
            byte[] original = before.ToArray();
            string originalText = Encoding.UTF8.GetString(original).TrimStart('\uFEFF');
            if (Hash(originalText) != expectedSourceHash)
                throw new InvalidOperationException(envelope.LevelId + "/stale source; reload before publishing");
            bool sourceTouched = false;
            try
            {
                fault?.Invoke("BeforeSourceWrite");
                sourceTouched = true;
                WriteLockedSource(source, replacement, () => fault?.Invoke("DuringSourceWrite"));
                fault?.Invoke("AfterSourceWrite");
                Publish(envelope, name, runtimeRoot, () => fault?.Invoke("BeforeRuntimeDirectoryCommit"));
            }
            catch (Exception failure)
            {
                if (sourceTouched)
                {
                    try { WriteLockedSource(source, original); }
                    catch (Exception rollback) { throw new AggregateException("Level publication failed and source rollback requires recovery.", failure, rollback); }
                }
                throw;
            }
            return Hash(json);
        }

        private static void WriteLockedSource(FileStream stream, byte[] bytes, Action beforeBytes = null)
        {
            stream.Position = 0;
            stream.SetLength(0);
            beforeBytes?.Invoke();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        internal static void AtomicWrite(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal sealed class CharacterTestLevelEntry
    {
        public string LevelId = string.Empty, DisplayName = string.Empty, Path = string.Empty, DocumentHash = string.Empty;
    }
}
