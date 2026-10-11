using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Config.Levels
{
    public sealed class MxLevelAuthoringDocument
    {
        public const string CurrentFormat = "mx.level.authoring.v3";
        public const int CurrentSchemaVersion = 3;
        public const string CharacterTestWorldObjectsModuleId = "characterTest.worldObjects";
        public const string CharacterTestCharactersModuleId = "characterTest.characters";

        public MxLevelAuthoringDocument()
        {
            Format = CurrentFormat;
            SchemaVersion = CurrentSchemaVersion;
            LevelId = string.Empty;
            ScenePath = string.Empty;
            RootPath = string.Empty;
            RootTransformRef = new LevelTransformReference();
            Modules = new List<MxLevelAuthoringModuleDocument>();
            ContentHash = string.Empty;
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string Format { get; set; }

        public int SchemaVersion { get; set; }

        public string LevelId { get; set; }

        public string ScenePath { get; set; }

        public string RootPath { get; set; }

        public LevelTransformReference RootTransformRef { get; set; }

        public List<MxLevelAuthoringModuleDocument> Modules { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        [JsonIgnore]
        public string ContentHash { get; set; }

        public bool ShouldSerializeRootPath()
        {
            return false;
        }

        public MxLevelAuthoringDocument Normalize()
        {
            LevelTransformReference rootRef = RootTransformRef == null
                ? new LevelTransformReference()
                : RootTransformRef.Normalize();
            if (string.IsNullOrWhiteSpace(rootRef.Path) && !string.IsNullOrWhiteSpace(RootPath))
                rootRef.Path = NormalizePath(RootPath);

            var normalized = new MxLevelAuthoringDocument
            {
                Format = CurrentFormat,
                SchemaVersion = CurrentSchemaVersion,
                LevelId = Trim(LevelId),
                ScenePath = NormalizePath(ScenePath),
                RootPath = rootRef.Path,
                RootTransformRef = rootRef,
                Modules = new List<MxLevelAuthoringModuleDocument>(),
                Metadata = NormalizeMetadata(Metadata)
            };

            if (Modules != null)
            {
                for (int i = 0; i < Modules.Count; i++)
                {
                    if (Modules[i] != null)
                        normalized.Modules.Add(Modules[i].Normalize());
                }
            }

            return normalized;
        }

        public bool TryGetModule(
            string moduleId,
            out MxLevelAuthoringModuleDocument module)
        {
            module = null;
            if (Modules == null || string.IsNullOrWhiteSpace(moduleId))
                return false;

            string normalizedId = moduleId.Trim();
            for (int i = 0; i < Modules.Count; i++)
            {
                MxLevelAuthoringModuleDocument candidate = Modules[i];
                if (candidate != null
                    && string.Equals(candidate.ModuleId, normalizedId, StringComparison.Ordinal))
                {
                    module = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryReadModulePayload<T>(
            string moduleId,
            out T payload)
            where T : class
        {
            payload = null;
            if (!TryGetModule(moduleId, out MxLevelAuthoringModuleDocument module))
                return false;

            payload = module.ReadPayload<T>();
            return payload != null;
        }

        public void UpsertModulePayload(
            string moduleId,
            int schemaVersion,
            object payload,
            string displayName = "")
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module id is required.", nameof(moduleId));

            Modules ??= new List<MxLevelAuthoringModuleDocument>();
            string normalizedId = moduleId.Trim();
            MxLevelAuthoringModuleDocument module = null;
            for (int i = 0; i < Modules.Count; i++)
            {
                if (Modules[i] != null
                    && string.Equals(Modules[i].ModuleId, normalizedId, StringComparison.Ordinal))
                {
                    module = Modules[i];
                    break;
                }
            }

            if (module == null)
            {
                module = new MxLevelAuthoringModuleDocument();
                Modules.Add(module);
            }

            module.ModuleId = normalizedId;
            module.SchemaVersion = Math.Max(1, schemaVersion);
            module.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? module.DisplayName
                : displayName.Trim();
            module.Payload = payload == null
                ? new JObject()
                : JObject.FromObject(payload, JsonSerializer.Create(MxLevelAuthoringJson.Settings));
        }

        private static Dictionary<string, string> NormalizeMetadata(
            IDictionary<string, string> values)
        {
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values == null)
                return normalized;

            foreach (KeyValuePair<string, string> pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                normalized[pair.Key.Trim()] = Trim(pair.Value);
            }

            return normalized;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizePath(string value)
        {
            return Trim(value).Replace('\\', '/');
        }
    }

    public sealed class MxLevelAuthoringModuleDocument
    {
        public MxLevelAuthoringModuleDocument()
        {
            ModuleId = string.Empty;
            SchemaVersion = 1;
            DisplayName = string.Empty;
            Payload = new JObject();
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string ModuleId { get; set; }

        public int SchemaVersion { get; set; }

        public string DisplayName { get; set; }

        public JObject Payload { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        public MxLevelAuthoringModuleDocument Normalize()
        {
            return new MxLevelAuthoringModuleDocument
            {
                ModuleId = string.IsNullOrWhiteSpace(ModuleId) ? string.Empty : ModuleId.Trim(),
                SchemaVersion = Math.Max(1, SchemaVersion),
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim(),
                Payload = Payload == null ? new JObject() : (JObject)Payload.DeepClone(),
                Metadata = NormalizeMetadata(Metadata)
            };
        }

        public T ReadPayload<T>()
            where T : class
        {
            if (Payload == null)
                return null;

            return Payload.ToObject<T>(JsonSerializer.Create(MxLevelAuthoringJson.Settings));
        }

        private static Dictionary<string, string> NormalizeMetadata(
            IDictionary<string, string> values)
        {
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values == null)
                return normalized;

            foreach (KeyValuePair<string, string> pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                normalized[pair.Key.Trim()] = string.IsNullOrWhiteSpace(pair.Value)
                    ? string.Empty
                    : pair.Value.Trim();
            }

            return normalized;
        }
    }

    public sealed class MxLevelAuthoringSession
    {
        public MxLevelAuthoringSession(
            string documentPath,
            MxLevelAuthoringDocument document,
            bool isDirty,
            bool isStale,
            MxLevelAuthoringValidationReport validationReport)
        {
            DocumentPath = string.IsNullOrWhiteSpace(documentPath) ? string.Empty : documentPath.Trim();
            Document = document == null ? new MxLevelAuthoringDocument() : document.Normalize();
            IsDirty = isDirty;
            IsStale = isStale;
            ValidationReport = validationReport ?? MxLevelAuthoringValidationReport.Empty;
        }

        public string DocumentPath { get; }

        public MxLevelAuthoringDocument Document { get; }

        public bool IsDirty { get; }

        public bool IsStale { get; }

        public MxLevelAuthoringValidationReport ValidationReport { get; }
    }

    public sealed class MxLevelAuthoringIssue
    {
        public MxLevelAuthoringIssue(
            LevelWorldObjectAuthoringSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = string.IsNullOrWhiteSpace(code) ? "Unknown" : code.Trim();
            Path = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public LevelWorldObjectAuthoringSeverity Severity { get; }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public LevelWorldObjectAuthoringIssue ToWorldObjectIssue()
        {
            return new LevelWorldObjectAuthoringIssue(Severity, Code, Path, Message);
        }
    }

    public sealed class MxLevelAuthoringValidationReport
    {
        public MxLevelAuthoringValidationReport(IReadOnlyList<MxLevelAuthoringIssue> issues)
        {
            Issues = CopyIssues(issues);
            int errorCount = 0;
            int warningCount = 0;
            for (int i = 0; i < Issues.Count; i++)
            {
                switch (Issues[i].Severity)
                {
                    case LevelWorldObjectAuthoringSeverity.Error:
                        errorCount++;
                        break;
                    case LevelWorldObjectAuthoringSeverity.Warning:
                        warningCount++;
                        break;
                }
            }

            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        public IReadOnlyList<MxLevelAuthoringIssue> Issues { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public bool HasErrors => ErrorCount > 0;

        public static MxLevelAuthoringValidationReport Empty { get; } =
            new MxLevelAuthoringValidationReport(Array.Empty<MxLevelAuthoringIssue>());

        public LevelWorldObjectAuthoringValidationReport ToWorldObjectReport()
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>(Issues.Count);
            for (int i = 0; i < Issues.Count; i++)
                issues.Add(Issues[i].ToWorldObjectIssue());
            return new LevelWorldObjectAuthoringValidationReport(issues);
        }

        private static ReadOnlyCollection<MxLevelAuthoringIssue> CopyIssues(
            IReadOnlyList<MxLevelAuthoringIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return new ReadOnlyCollection<MxLevelAuthoringIssue>(new List<MxLevelAuthoringIssue>());

            var copy = new List<MxLevelAuthoringIssue>(issues.Count);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null)
                    copy.Add(issues[i]);
            }

            return new ReadOnlyCollection<MxLevelAuthoringIssue>(copy);
        }
    }

    public static class MxLevelAuthoringValidator
    {
        public const string MissingDocument = "MissingDocument";
        public const string UnsupportedFormat = "UnsupportedFormat";
        public const string UnsupportedSchemaVersion = "UnsupportedSchemaVersion";
        public const string MissingLevelId = "MissingLevelId";
        public const string MissingScenePath = "MissingScenePath";
        public const string MissingRootTransformRef = "MissingRootTransformRef";
        public const string MissingModuleId = "MissingModuleId";
        public const string DuplicateModuleId = "DuplicateModuleId";
        public const string MissingModulePayload = "MissingModulePayload";

        public static MxLevelAuthoringValidationReport Validate(MxLevelAuthoringDocument document)
        {
            var issues = new List<MxLevelAuthoringIssue>();
            if (document == null)
            {
                issues.Add(Error(MissingDocument, string.Empty, "Level authoring document is missing."));
                return new MxLevelAuthoringValidationReport(issues);
            }

            if (!string.Equals(MxLevelAuthoringDocument.CurrentFormat, document.Format, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    UnsupportedFormat,
                    "format",
                    "Expected format " + MxLevelAuthoringDocument.CurrentFormat + "."));
            }

            if (document.SchemaVersion != MxLevelAuthoringDocument.CurrentSchemaVersion)
            {
                issues.Add(Error(
                    UnsupportedSchemaVersion,
                    "schemaVersion",
                    "Expected schema version " + MxLevelAuthoringDocument.CurrentSchemaVersion + "."));
            }

            MxLevelAuthoringDocument normalized = document.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.LevelId))
                issues.Add(Error(MissingLevelId, "levelId", "Level id is required."));
            if (string.IsNullOrWhiteSpace(normalized.ScenePath))
                issues.Add(Error(MissingScenePath, "scenePath", "Scene path is required."));
            if (normalized.RootTransformRef == null || normalized.RootTransformRef.IsEmpty)
            {
                issues.Add(Error(
                    MissingRootTransformRef,
                    "rootTransformRef",
                    "Root transform reference is required."));
            }

            ValidateModules(normalized.Modules, issues);
            return new MxLevelAuthoringValidationReport(issues);
        }

        private static void ValidateModules(
            IReadOnlyList<MxLevelAuthoringModuleDocument> modules,
            List<MxLevelAuthoringIssue> issues)
        {
            var moduleIds = new Dictionary<string, int>(StringComparer.Ordinal);
            if (modules == null)
                return;

            for (int i = 0; i < modules.Count; i++)
            {
                MxLevelAuthoringModuleDocument module = modules[i];
                if (module == null)
                    continue;

                string path = "modules[" + i + "]";
                if (string.IsNullOrWhiteSpace(module.ModuleId))
                {
                    issues.Add(Error(MissingModuleId, path + ".moduleId", "Module id is required."));
                    continue;
                }

                string moduleId = module.ModuleId.Trim();
                if (moduleIds.TryGetValue(moduleId, out int firstIndex))
                {
                    issues.Add(Error(
                        DuplicateModuleId,
                        path + ".moduleId",
                        "Module id duplicates modules[" + firstIndex + "]."));
                }
                else
                {
                    moduleIds.Add(moduleId, i);
                }

                if (module.Payload == null)
                    issues.Add(Error(MissingModulePayload, path + ".payload", "Module payload is required."));
            }
        }

        private static MxLevelAuthoringIssue Error(string code, string path, string message)
        {
            return new MxLevelAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }
    }

    public static class MxLevelAuthoringJson
    {
        internal static readonly JsonSerializerSettings Settings = CreateSettings();

        public static string SaveToJson(MxLevelAuthoringDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return JsonConvert.SerializeObject(document.Normalize(), Settings);
        }

        public static MxLevelAuthoringDocument LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "Level authoring json cannot be null or empty.",
                    nameof(json));
            }

            JObject root = JObject.Parse(json);
            string format = root.Value<string>("format") ?? string.Empty;
            if (string.Equals(format, MxLevelAuthoringDocument.CurrentFormat, StringComparison.Ordinal))
            {
                MxLevelAuthoringDocument document =
                    root.ToObject<MxLevelAuthoringDocument>(JsonSerializer.Create(Settings));
                return document == null ? new MxLevelAuthoringDocument() : document.Normalize();
            }

            if (string.Equals(format, LevelWorldObjectAuthoringDocument.CurrentFormat, StringComparison.Ordinal)
                || string.Equals(format, LevelWorldObjectAuthoringDocument.LegacyFormat, StringComparison.Ordinal))
            {
                LevelWorldObjectAuthoringDocument legacy =
                    LevelWorldObjectAuthoringJson.LoadFromJson(json);
                return WrapWorldObjectDocument(legacy);
            }

            MxLevelAuthoringDocument fallback =
                root.ToObject<MxLevelAuthoringDocument>(JsonSerializer.Create(Settings));
            return fallback == null ? new MxLevelAuthoringDocument() : fallback.Normalize();
        }

        public static MxLevelAuthoringDocument WrapWorldObjectDocument(
            LevelWorldObjectAuthoringDocument document)
        {
            LevelWorldObjectAuthoringDocument normalized =
                document == null ? new LevelWorldObjectAuthoringDocument() : document.Normalize();
            var envelope = new MxLevelAuthoringDocument
            {
                LevelId = normalized.LevelId,
                ScenePath = normalized.ScenePath,
                RootPath = normalized.RootPath,
                RootTransformRef = LevelTransformReference.FromPath(normalized.RootPath)
            };
            envelope.UpsertModulePayload(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                1,
                normalized,
                "CharacterTest World Objects");
            return envelope.Normalize();
        }

        public static string ComputeContentHash(MxLevelAuthoringDocument document)
        {
            if (document == null)
                return string.Empty;

            string json = JsonConvert.SerializeObject(NormalizeForHash(document), Settings);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static MxLevelAuthoringDocument NormalizeForHash(MxLevelAuthoringDocument document)
        {
            MxLevelAuthoringDocument normalized = document.Normalize();
            normalized.ContentHash = string.Empty;
            return normalized;
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }
    }
}
