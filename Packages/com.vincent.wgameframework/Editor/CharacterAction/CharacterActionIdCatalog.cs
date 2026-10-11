using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Editor-only transactional CharacterAction id allocation entry.</summary>
    public static class CharacterActionIdCatalogApi
    {
        public static int Allocate(string libraryId, string sourcePath)
        {
            return CharacterActionIdCatalog.Allocate(libraryId, sourcePath);
        }

        public static void Validate()
        {
            CharacterActionIdCatalog.Validate();
        }
    }

    internal static class CharacterActionIdCatalog
    {
        internal const string Schema = "mx.character-action.id-catalog.v1";
        internal const string DefaultPath = "CharacterActionSource/action_id_catalog.json";

        internal static int Allocate(string libraryId, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
                throw new InvalidOperationException("CharacterAction ID allocation requires a library id.");

            return Mutate(document =>
            {
                JObject library = FindLibrary(document, libraryId)
                    ?? throw new InvalidOperationException(
                        "CharacterAction ID catalog has no reserved range for library '" + libraryId + "'.");
                JArray allocations = (JArray)document["allocations"];
                var used = new HashSet<int>(
                    allocations.OfType<JObject>().Select(item => (int?)item["actionId"] ?? 0));
                foreach (JObject range in ((JArray)library["ranges"]).OfType<JObject>())
                {
                    int start = (int?)range["start"] ?? 0;
                    int end = (int?)range["end"] ?? 0;
                    int candidate = Math.Max(start, (int?)range["nextActionId"] ?? start);
                    while (candidate <= end && used.Contains(candidate))
                        candidate++;
                    if (candidate <= 0 || candidate > end)
                        continue;

                    range["nextActionId"] = candidate + 1;
                    allocations.Add(new JObject
                    {
                        ["actionId"] = candidate,
                        ["libraryId"] = libraryId,
                        ["sourcePath"] = NormalizePath(sourcePath),
                        ["status"] = "active",
                    });
                    return candidate;
                }

                throw new InvalidOperationException(
                    "CharacterAction ID ranges are exhausted for library '" + libraryId + "'.");
            });
        }

        internal static void Retire(int actionId)
        {
            if (actionId <= 0)
                return;
            Mutate(document =>
            {
                JObject allocation = ((JArray)document["allocations"])
                    .OfType<JObject>()
                    .FirstOrDefault(item => (int?)item["actionId"] == actionId);
                if (allocation == null)
                    throw new InvalidOperationException("CharacterAction ID is not registered: " + actionId);
                allocation["status"] = "retired";
                return 0;
            });
        }

        internal static void Validate()
        {
            LoadValidated(out _, out _);
        }

        private static T Mutate<T>(Func<JObject, T> mutation)
        {
            JObject document = LoadValidated(out string path, out string expectedFileHash);
            T result = mutation(document);
            document["revision"] = ((int?)document["revision"] ?? 0) + 1;
            document["catalogHash"] = string.Empty;
            document["catalogHash"] = "sha256:" + ComputeSha256(Canonicalize(document));
            string currentFileHash = File.Exists(path)
                ? ComputeSha256(File.ReadAllBytes(path))
                : string.Empty;
            if (!string.Equals(currentFileHash, expectedFileHash, StringComparison.Ordinal))
                throw new InvalidOperationException("CharacterAction ID catalog changed during allocation.");

            string staging = path + ".staging";
            File.WriteAllText(staging, document.ToString(Formatting.Indented) + Environment.NewLine);
            if (File.Exists(path))
                File.Replace(staging, path, null);
            else
                File.Move(staging, path);
            return result;
        }

        private static JObject LoadValidated(out string path, out string fileHash)
        {
            path = Path.GetFullPath(DefaultPath);
            if (!File.Exists(path))
                throw new FileNotFoundException("CharacterAction ID catalog is missing.", path);
            byte[] bytes = File.ReadAllBytes(path);
            fileHash = ComputeSha256(bytes);
            JObject document = JObject.Parse(Encoding.UTF8.GetString(bytes));
            if (!string.Equals((string)document["schema"], Schema, StringComparison.Ordinal))
                throw new InvalidOperationException("CharacterAction ID catalog schema is unsupported.");
            if ((int?)document["revision"] <= 0
                || document["libraries"] is not JArray
                || document["allocations"] is not JArray)
            {
                throw new InvalidOperationException("CharacterAction ID catalog is malformed.");
            }
            string storedHash = (string)document["catalogHash"] ?? string.Empty;
            JObject hashInput = (JObject)document.DeepClone();
            hashInput["catalogHash"] = string.Empty;
            string actualHash = "sha256:" + ComputeSha256(Canonicalize(hashInput));
            if (!string.Equals(storedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("CharacterAction ID catalog hash mismatch.");
            ValidateUniqueness(document);
            return document;
        }

        private static void ValidateUniqueness(JObject document)
        {
            var ids = new HashSet<int>();
            var libraryIds = new HashSet<string>(StringComparer.Ordinal);
            var ranges = new List<(int Start, int End, string LibraryId)>();
            foreach (JObject library in ((JArray)document["libraries"]).OfType<JObject>())
            {
                string libraryId = (string)library["libraryId"] ?? string.Empty;
                if (libraryId.Length == 0 || !libraryIds.Add(libraryId))
                    throw new InvalidOperationException(
                        "CharacterAction ID catalog contains an invalid duplicate library: " + libraryId);
                foreach (JObject range in ((JArray)library["ranges"] ?? new JArray()).OfType<JObject>())
                {
                    int start = (int?)range["start"] ?? 0;
                    int end = (int?)range["end"] ?? 0;
                    long next = (long?)range["nextActionId"] ?? 0L;
                    if (start <= 0 || end < start || next < start || next > (long)end + 1L)
                        throw new InvalidOperationException("CharacterAction ID catalog contains an invalid reserved range.");
                    if (ranges.Any(existing => start <= existing.End && end >= existing.Start))
                        throw new InvalidOperationException("CharacterAction ID catalog contains overlapping reserved ranges.");
                    ranges.Add((start, end, libraryId));
                }
            }
            foreach (JObject item in ((JArray)document["allocations"]).OfType<JObject>())
            {
                int id = (int?)item["actionId"] ?? 0;
                if (id <= 0 || !ids.Add(id))
                    throw new InvalidOperationException("CharacterAction ID catalog contains an invalid duplicate: " + id);
                string owner = (string)item["libraryId"] ?? string.Empty;
                if (!ranges.Any(range => string.Equals(range.LibraryId, owner, StringComparison.Ordinal)
                        && id >= range.Start && id <= range.End))
                {
                    throw new InvalidOperationException(
                        "CharacterAction ID catalog allocation " + id + " is outside its owner library ranges.");
                }
            }
        }

        private static JObject FindLibrary(JObject document, string libraryId)
        {
            return ((JArray)document["libraries"])
                .OfType<JObject>()
                .FirstOrDefault(item => string.Equals(
                    (string)item["libraryId"],
                    libraryId,
                    StringComparison.Ordinal));
        }

        private static string Canonicalize(JToken token)
        {
            return CanonicalToken(token).ToString(Formatting.None);
        }

        private static JToken CanonicalToken(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                    result[property.Name] = CanonicalToken(property.Value);
                return result;
            }
            if (token is JArray array)
                return new JArray(array.Select(CanonicalToken));
            return token.DeepClone();
        }

        private static string ComputeSha256(string value) =>
            ComputeSha256(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string NormalizePath(string value) =>
            (value ?? string.Empty).Replace('\\', '/');
    }
}
