using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Loads preserved projectile sources and package-wide compiled projections.</summary>
    /// <remarks>LowFreqAlloc: workspace lifetime is limited to an authoring session or command.</remarks>
    public sealed class ProjectileAuthoringWorkspace : IDisposable
    {
        private readonly List<ProjectileAuthoringSourceDocument> sourceDocuments;

        private ProjectileAuthoringWorkspace(
            string packageRoot,
            string generatedRoot,
            string vfxDefinitionCatalogPath,
            AbilityAuthoringDocument abilityDocument,
            List<VfxDefinitionConfig> vfxDefinitions,
            List<ProjectileAuthoringSourceDocument> sources,
            List<ProjectileAuthoringDiagnostic> diagnostics,
            List<ProjectileAuthoringGeneratedOnlyEntry> generatedOnly,
            HashSet<int> authoritativeDefinitionIds)
        {
            PackageRoot = packageRoot;
            GeneratedRoot = generatedRoot;
            VfxDefinitionCatalogPath = vfxDefinitionCatalogPath;
            ProjectileSourceRoot = AbilityAuthoringPackageLoader.ProjectileRequestDirectoryRelativePath;
            AbilityDocument = abilityDocument;
            VfxDefinitions = vfxDefinitions.AsReadOnly();
            sourceDocuments = sources;
            SourceDocuments = sources.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
            GeneratedOnly = generatedOnly.AsReadOnly();
            AuthoritativeDefinitionIds = authoritativeDefinitionIds;
            ReferenceIndex = ProjectileAuthoringReferenceIndexBuilder.Build(sources, abilityDocument, diagnostics);
        }

        public string PackageRoot { get; }
        public string GeneratedRoot { get; }
        public string VfxDefinitionCatalogPath { get; }
        public string ProjectileSourceRoot { get; }
        public AbilityAuthoringDocument AbilityDocument { get; }
        public IReadOnlyList<VfxDefinitionConfig> VfxDefinitions { get; }
        public IReadOnlyList<ProjectileAuthoringSourceDocument> SourceDocuments { get; }
        public IReadOnlyList<ProjectileAuthoringDiagnostic> Diagnostics { get; }
        public ProjectileAuthoringReferenceIndex ReferenceIndex { get; }
        public IReadOnlyList<ProjectileAuthoringGeneratedOnlyEntry> GeneratedOnly { get; }
        public IReadOnlyCollection<int> AuthoritativeDefinitionIds { get; }
        public bool HasBlockingIssues => HasErrors(Diagnostics);

        /// <summary>Loads source bytes, preserved JSON and the compiler projection for one package.</summary>
        /// <remarks>LowFreqAlloc: package IO, JSON parsing and index construction.</remarks>
        public static ProjectileAuthoringWorkspace Load(string packagePath, JsonSerializerOptions options)
            => Load(packagePath, null, null, options);

        /// <summary>Loads source authority and, when supplied, the generated catalogs used for drift/adopt inventory.</summary>
        /// <remarks>LowFreqAlloc: recursively scans package JSON identity authority and parses generated catalogs.</remarks>
        public static ProjectileAuthoringWorkspace Load(string packagePath, string generatedRoot, JsonSerializerOptions options)
            => Load(packagePath, generatedRoot, null, options);

        /// <summary>Loads source authority with optional generated projectile and global VFX definition catalogs.</summary>
        /// <remarks>LowFreqAlloc: parses authoring JSON catalogs once per editor command.</remarks>
        public static ProjectileAuthoringWorkspace Load(
            string packagePath,
            string generatedRoot,
            string vfxDefinitionCatalogPath,
            JsonSerializerOptions options)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Package path is required.", nameof(packagePath));
            options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string packageRoot = Path.GetFullPath(AbilityAuthoringCompiler.ResolvePackageRootPath(packagePath));
            var diagnostics = new List<ProjectileAuthoringDiagnostic>();
            var sources = new List<ProjectileAuthoringSourceDocument>();
            string sourceRoot = Path.Combine(packageRoot, AbilityAuthoringPackageLoader.ProjectileRequestDirectoryRelativePath);
            string[] files = Directory.Exists(sourceRoot)
                ? Directory.GetFiles(sourceRoot, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
                LoadSource(packageRoot, files[i], options, sources, diagnostics);

            AbilityAuthoringDocument ability;
            try
            {
                ability = AbilityAuthoringPackageLoader.Load(packageRoot, options);
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                DisposeAll(sources);
                throw new InvalidDataException("Projectile authoring package could not be loaded.", ex);
            }

            string resolvedVfxDefinitionCatalogPath = string.IsNullOrWhiteSpace(vfxDefinitionCatalogPath)
                ? string.Empty
                : Path.GetFullPath(vfxDefinitionCatalogPath);
            List<VfxDefinitionConfig> vfxDefinitions = LoadVfxDefinitions(
                ability.VfxDefinitions,
                resolvedVfxDefinitionCatalogPath,
                options,
                diagnostics);
            AbilityAuthoringValidationReport validation = AbilityAuthoringCompiler.Validate(ability, vfxDefinitions);
            for (int i = 0; i < validation.Diagnostics.Count; i++)
            {
                AbilityAuthoringDiagnostic item = validation.Diagnostics[i];
                if (item == null)
                    continue;
                MapPresentationDiagnostic(ability, sources, item.Path, out string presentationSource, out string presentationObject, out string presentationField, out List<string> presentationRelated);
                diagnostics.Add(new ProjectileAuthoringDiagnostic
                {
                    Code = item.Code ?? string.Empty,
                    Severity = item.Severity ?? "Error",
                    Message = item.Message ?? string.Empty,
                    SourcePath = string.IsNullOrEmpty(presentationSource) ? MakeRelativeDiagnosticPath(packageRoot, item.Path) : presentationSource,
                    ObjectPath = presentationObject,
                    FieldPath = string.IsNullOrEmpty(presentationField) ? MakeRelativeDiagnosticPath(packageRoot, item.Path) : presentationField,
                    RelatedStableIds = presentationRelated,
                });
            }

            var authority = CollectDefinitionIds(packageRoot);
            string resolvedGeneratedRoot = string.IsNullOrWhiteSpace(generatedRoot) ? Path.Combine(packageRoot, "generated") : Path.GetFullPath(generatedRoot);
            var generatedOnly = LoadGeneratedOnly(packageRoot, resolvedGeneratedRoot, sources, options, authority, diagnostics);
            return new ProjectileAuthoringWorkspace(
                packageRoot,
                resolvedGeneratedRoot,
                resolvedVfxDefinitionCatalogPath,
                ability,
                vfxDefinitions,
                sources,
                diagnostics,
                generatedOnly,
                authority);
        }

        private static List<VfxDefinitionConfig> LoadVfxDefinitions(
            IReadOnlyList<VfxDefinitionConfig> localDefinitions,
            string catalogPath,
            JsonSerializerOptions options,
            List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            var definitions = new List<VfxDefinitionConfig>();
            if (!string.IsNullOrWhiteSpace(catalogPath))
            {
                try
                {
                    if (!File.Exists(catalogPath))
                        throw new FileNotFoundException("Global VFX definition catalog was not found.", catalogPath);
                    AbilityVfxDefinitionCatalogDocument catalog = JsonSerializer.Deserialize<AbilityVfxDefinitionCatalogDocument>(
                        File.ReadAllBytes(catalogPath),
                        options);
                    if (catalog == null || !string.Equals(catalog.SchemaVersion, "1", StringComparison.Ordinal))
                        throw new JsonException("Global VFX definition catalog must use schemaVersion 1.");
                    definitions.AddRange(catalog.Definitions ?? new List<VfxDefinitionConfig>());
                }
                catch (Exception ex) when (ex is IOException || ex is JsonException)
                {
                    diagnostics.Add(new ProjectileAuthoringDiagnostic
                    {
                        Code = "PROJAUTH_VFX_CATALOG_INVALID",
                        Severity = "Error",
                        Message = "Global VFX definition catalog could not be loaded: " + ex.Message,
                        SourcePath = catalogPath,
                        FieldPath = "vfxDefinitions",
                        SuggestedFix = "Bake and publish the VFX catalog before assigning the VFX to a projectile.",
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(catalogPath))
                return definitions;

            for (int i = 0; localDefinitions != null && i < localDefinitions.Count; i++)
            {
                VfxDefinitionConfig local = localDefinitions[i];
                if (local == null)
                    continue;
                definitions.Add(local);
            }
            return definitions;
        }

        private static HashSet<int> CollectDefinitionIds(string packageRoot)
        {
            var ids = new HashSet<int>();
            string configRoot = Path.Combine(packageRoot, "config");
            if (!Directory.Exists(configRoot)) return ids;
            string[] files = Directory.GetFiles(configRoot, "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                try { using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(files[i])); CollectDefinitionIds(json.RootElement, ids); }
                catch (JsonException) { }
            }
            return ids;
        }

        private static void CollectDefinitionIds(JsonElement value, HashSet<int> ids)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    if ((string.Equals(property.Name, "definitionId", StringComparison.OrdinalIgnoreCase)
                        || property.Name.EndsWith("DefinitionId", StringComparison.OrdinalIgnoreCase))
                        && property.Value.TryGetInt32(out int id) && id > 0) ids.Add(id);
                    CollectDefinitionIds(property.Value, ids);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in value.EnumerateArray()) CollectDefinitionIds(item, ids);
        }

        private static List<ProjectileAuthoringGeneratedOnlyEntry> LoadGeneratedOnly(string packageRoot, string generatedRoot, IReadOnlyList<ProjectileAuthoringSourceDocument> sources, JsonSerializerOptions options, HashSet<int> authority, List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            var output = new List<ProjectileAuthoringGeneratedOnlyEntry>();
            if (string.IsNullOrWhiteSpace(generatedRoot)) generatedRoot = Path.Combine(packageRoot, "generated");
            string definitionsPath = Path.Combine(generatedRoot, "projectile_definitions.json");
            if (!File.Exists(definitionsPath)) return output;
            try
            {
                AbilityProjectileDefinitionCatalogDocument definitions = ReadGenerated<AbilityProjectileDefinitionCatalogDocument>(generatedRoot, "projectile_definitions.json", "3|4|5", "definitions", options);
                AbilityProjectileSpawnProfileCatalogDocument spawns = ReadGenerated<AbilityProjectileSpawnProfileCatalogDocument>(generatedRoot, "projectile_spawn_profiles.json", "1|2", "profiles", options);
                AbilityProjectilePresentationBindingDocument bindings = ReadGenerated<AbilityProjectilePresentationBindingDocument>(generatedRoot, "projectile_presentation_bindings.json", "1|2", "bindings", options);
                var sourceIds = new HashSet<string>(sources.Where(x => x.Projection.Definition != null).Select(x => x.Projection.Definition.StableId), StringComparer.Ordinal);
                foreach (ProjectileDefinitionConfig definition in definitions?.Definitions ?? new List<ProjectileDefinitionConfig>())
                {
                    authority.Add(definition.DefinitionId);
                    if (sourceIds.Contains(definition.StableId)) continue;
                    ProjectilePresentationBindingConfig binding = bindings?.Bindings?.FirstOrDefault(x => x.ProjectileDefinitionId == definition.DefinitionId);
                    ProjectileSpawnProfileConfig spawn = spawns?.Profiles?.FirstOrDefault(x => string.Equals(x.ProjectileStableId, definition.StableId, StringComparison.Ordinal));
                    var entry = new ProjectileAuthoringGeneratedOnlyEntry { StableId = definition.StableId ?? string.Empty, DefinitionId = definition.DefinitionId, Definition = definition, SpawnProfile = spawn, PresentationBinding = binding };
                    if (definition.DefinitionId <= 0 || string.IsNullOrWhiteSpace(definition.StableId) || definition.Motion == null || definition.Collision == null || definition.Impact?.HitEvent == null) entry.Diagnostics.Add("definition-v3-incomplete");
                    if (binding == null) entry.Diagnostics.Add("presentation-binding-missing");
                    entry.Complete = entry.Diagnostics.Count == 0;
                    entry.Status = entry.Complete ? "GeneratedOnly" : "Incomplete";
                    output.Add(entry);
                }
            }
            catch (JsonException ex)
            {
                diagnostics.Add(new ProjectileAuthoringDiagnostic
                {
                    Code = "PROJAUTH_GENERATED_CATALOG_INVALID",
                    Severity = "Error",
                    Message = "Generated projectile inventory is malformed and cannot be adopted: " + ex.Message,
                    SourcePath = GeneratedDiagnosticPath(packageRoot, definitionsPath),
                    FieldPath = string.Empty,
                    SuggestedFix = "Regenerate all projectile catalogs before adopting generated-only entries.",
                });
            }
            output.Sort((a, b) => string.CompareOrdinal(a.StableId, b.StableId));
            return output;
        }

        private static T ReadGenerated<T>(string root, string name, string expectedSchema, string collectionProperty, JsonSerializerOptions options) where T : class
        {
            string path = Path.Combine(root, name);
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            using JsonDocument json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("schemaVersion", out JsonElement schema)
                || !ExpectedSchemaMatches(schema.GetString(), expectedSchema)
                || !json.RootElement.TryGetProperty(collectionProperty, out JsonElement collection)
                || collection.ValueKind != JsonValueKind.Array)
                throw new JsonException(name + " does not match the expected generated catalog schema.");
            return JsonSerializer.Deserialize<T>(bytes, options) ?? throw new JsonException(name + " is empty.");
        }

        private static bool ExpectedSchemaMatches(string actual, string expected)
        {
            string[] values = (expected ?? string.Empty).Split('|');
            for (int i = 0; i < values.Length; i++)
                if (string.Equals(actual, values[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static string GeneratedDiagnosticPath(string packageRoot, string path)
        {
            string fullRoot = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? NormalizeRelative(Path.GetRelativePath(packageRoot, full))
                : "generated/" + Path.GetFileName(full);
        }

        /// <summary>Compiles only projectile projections through the current Ability compiler.</summary>
        /// <remarks>LowFreqAlloc: normalization, validation, sorting and catalog allocation.</remarks>
        public ProjectileAuthoringCompileResult Compile()
        {
            AbilityAuthoringCompileResult compiled = AbilityAuthoringCompiler.Compile(AbilityDocument, VfxDefinitions);
            var output = new ProjectileAuthoringCompileResult
            {
                Definitions = compiled.ProjectileDefinitionCatalog,
                SpawnProfiles = compiled.ProjectileSpawnProfileCatalog,
                PresentationBindings = compiled.ProjectilePresentationBindings,
                HasBlockingIssues = compiled.ValidationReport.HasBlockingIssues || HasBlockingIssues,
            };
            for (int i = 0; i < Diagnostics.Count; i++)
                output.Diagnostics.Add(Diagnostics[i]);
            return output;
        }

        /// <summary>Validates unknown fields and returns bytes for a full-document proposal.</summary>
        /// <remarks>LowFreqAlloc: recursively inspects JSON and allocates changed proposal bytes.</remarks>
        public bool TryBuildLosslessProposal(
            ProjectileAuthoringSourceDocument source,
            JsonElement proposal,
            out byte[] content,
            out ProjectileAuthoringDiagnostic diagnostic)
        {
            content = Array.Empty<byte>();
            diagnostic = null;
            if (source?.PreservedJson == null)
            {
                content = new UTF8Encoding(false).GetBytes(proposal.GetRawText());
                return true;
            }
            if (!ValidateUnknownFields(
                source.PreservedJson.RootElement,
                proposal,
                typeof(ProjectileRequestAuthoringDocument),
                string.Empty,
                out string changedPath))
            {
                diagnostic = new ProjectileAuthoringDiagnostic
                {
                    Code = "PROJAUTH005",
                    Severity = "Error",
                    Message = "The proposal changes or removes an unmodeled source field and cannot be applied losslessly.",
                    SourcePath = source.RelativePath,
                    FieldPath = changedPath,
                    RelatedStableIds = new List<string> { source.SourceStableId },
                    SuggestedFix = "Preserve the unknown field subtree exactly or add an explicit schema migration."
                };
                return false;
            }
            if (JsonSemanticEquals(source.PreservedJson.RootElement, proposal))
            {
                content = source.OriginalUtf8Bytes;
                return true;
            }
            content = new UTF8Encoding(false).GetBytes(proposal.GetRawText() + Environment.NewLine);
            return true;
        }

        public void Dispose()
        {
            DisposeAll(sourceDocuments);
        }

        private static void LoadSource(
            string packageRoot,
            string path,
            JsonSerializerOptions options,
            List<ProjectileAuthoringSourceDocument> output,
            List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string relative = NormalizeRelative(Path.GetRelativePath(packageRoot, path));
            try
            {
                JsonDocument preserved = JsonDocument.Parse(bytes);
                ProjectileRequestAuthoringDocument projection = JsonSerializer.Deserialize<ProjectileRequestAuthoringDocument>(bytes, options);
                if (projection == null)
                    throw new JsonException("Projectile request is null.");
                PopulateLegacyLabels(projection, preserved.RootElement);
                output.Add(new ProjectileAuthoringSourceDocument
                {
                    SourceStableId = projection.StableId ?? string.Empty,
                    RelativePath = relative,
                    OriginalUtf8Bytes = bytes,
                    Sha256 = ProjectileAuthoringHashes.ComputeSha256(bytes),
                    Projection = projection,
                    PreservedJson = preserved,
                });
                if (!string.Equals(projection.SchemaVersion, "1", StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic("PROJAUTH_SCHEMA_UNSUPPORTED", relative, "schemaVersion", "Only projectile source schemaVersion 1 is supported by M1."));
                }
            }
            catch (JsonException ex)
            {
                diagnostics.Add(Diagnostic("PROJAUTH_SOURCE_JSON_INVALID", relative, string.Empty, ex.Message));
            }
        }

        private static bool ValidateUnknownFields(
            JsonElement original,
            JsonElement proposal,
            Type modelType,
            string path,
            out string changedPath)
        {
            changedPath = string.Empty;
            if (original.ValueKind != JsonValueKind.Object)
                return true;
            foreach (JsonProperty property in original.EnumerateObject())
            {
                string childPath = string.IsNullOrEmpty(path) ? property.Name : path + "." + property.Name;
                PropertyInfo mapped = FindJsonProperty(modelType, property.Name);
                JsonElement proposedValue = default;
                bool hasProposal = proposal.ValueKind == JsonValueKind.Object
                    && proposal.TryGetProperty(property.Name, out proposedValue);
                if (mapped == null)
                {
                    if (!hasProposal || !JsonSemanticEquals(property.Value, proposedValue))
                    {
                        changedPath = childPath;
                        return false;
                    }
                    continue;
                }

                Type valueType = UnwrapNullable(mapped.PropertyType);
                if (TryGetListElementType(valueType, out Type elementType) && IsModeledObject(elementType))
                {
                    if (!ValidateUnknownArray(property.Value, hasProposal ? proposedValue : default, elementType, childPath, out changedPath))
                        return false;
                    continue;
                }
                if (IsModeledObject(valueType)
                    && !ValidateUnknownFields(property.Value, hasProposal ? proposedValue : default, valueType, childPath, out changedPath))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateUnknownArray(JsonElement original, JsonElement proposal, Type elementType, string path, out string changedPath)
        {
            changedPath = string.Empty;
            if (original.ValueKind != JsonValueKind.Array)
                return true;
            JsonElement.ArrayEnumerator proposed = proposal.ValueKind == JsonValueKind.Array
                ? proposal.EnumerateArray()
                : default;
            int index = 0;
            foreach (JsonElement originalElement in original.EnumerateArray())
            {
                bool hasProposal = proposal.ValueKind == JsonValueKind.Array && proposed.MoveNext();
                if (!ValidateUnknownFields(originalElement, hasProposal ? proposed.Current : default, elementType, path + "[" + index + "]", out changedPath))
                    return false;
                index++;
            }
            return true;
        }

        private static PropertyInfo FindJsonProperty(Type type, string jsonName)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                JsonIgnoreAttribute ignored = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (ignored != null && ignored.Condition == JsonIgnoreCondition.Always)
                    continue;
                JsonPropertyNameAttribute named = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                string candidate = named?.Name ?? ToCamelCase(property.Name);
                if (string.Equals(candidate, jsonName, StringComparison.OrdinalIgnoreCase))
                    return property;
            }
            return null;
        }

        private static Type UnwrapNullable(Type type) => Nullable.GetUnderlyingType(type) ?? type;

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
            elementType = null;
            return false;
        }

        private static bool IsModeledObject(Type type)
        {
            type = UnwrapNullable(type);
            return type.IsClass && type != typeof(string) && type.Namespace == typeof(ProjectileRequestAuthoringDocument).Namespace;
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
                return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static bool JsonSemanticEquals(JsonElement left, JsonElement right)
        {
            if (left.ValueKind != right.ValueKind)
                return false;
            switch (left.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    int leftCount = 0;
                    foreach (JsonProperty property in left.EnumerateObject())
                    {
                        leftCount++;
                        if (!right.TryGetProperty(property.Name, out JsonElement other)
                            || !JsonSemanticEquals(property.Value, other))
                            return false;
                    }
                    int rightCount = 0;
                    foreach (JsonProperty ignored in right.EnumerateObject())
                        rightCount++;
                    return leftCount == rightCount;
                }
                case JsonValueKind.Array:
                {
                    JsonElement.ArrayEnumerator rightValues = right.EnumerateArray();
                    foreach (JsonElement value in left.EnumerateArray())
                    {
                        if (!rightValues.MoveNext() || !JsonSemanticEquals(value, rightValues.Current))
                            return false;
                    }
                    return !rightValues.MoveNext();
                }
                case JsonValueKind.String:
                    return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
                case JsonValueKind.Number:
                    if (left.TryGetDecimal(out decimal leftDecimal) && right.TryGetDecimal(out decimal rightDecimal))
                        return leftDecimal == rightDecimal;
                    return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return left.GetBoolean() == right.GetBoolean();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return true;
                default:
                    return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
            }
        }

        private static ProjectileAuthoringDiagnostic Diagnostic(string code, string path, string field, string message)
        {
            return new ProjectileAuthoringDiagnostic
            {
                Code = code,
                Severity = "Error",
                Message = message,
                SourcePath = path,
                FieldPath = field,
            };
        }

        private static bool HasErrors(IReadOnlyList<ProjectileAuthoringDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Severity, "Error", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string MakeRelativeDiagnosticPath(string packageRoot, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string normalizedRoot = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.IsPathRooted(value) && value.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return NormalizeRelative(Path.GetRelativePath(packageRoot, value));
            return value.Replace('\\', '/');
        }

        private static void MapPresentationDiagnostic(
            AbilityAuthoringDocument ability,
            IReadOnlyList<ProjectileAuthoringSourceDocument> sources,
            string path,
            out string sourcePath,
            out string objectPath,
            out string fieldPath,
            out List<string> relatedStableIds)
        {
            sourcePath = string.Empty;
            objectPath = string.Empty;
            fieldPath = string.Empty;
            relatedStableIds = new List<string>();
            const string Prefix = "projectilePresentationBindings[";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Prefix, StringComparison.Ordinal)) return;
            int close = path.IndexOf(']', Prefix.Length);
            if (close < 0 || !int.TryParse(path.Substring(Prefix.Length, close - Prefix.Length), out int index)) return;
            if (index < 0 || index >= (ability.ProjectilePresentationBindings?.Count ?? 0)) return;
            ProjectilePresentationBindingConfig binding = ability.ProjectilePresentationBindings[index];
            ProjectileDefinitionConfig definition = ability.ProjectileDefinitions?.FirstOrDefault(value => value.DefinitionId == binding.ProjectileDefinitionId);
            string stableId = definition?.StableId ?? string.Empty;
            ProjectileAuthoringSourceDocument source = sources.FirstOrDefault(value => value.Projection?.PresentationBinding?.ProjectileDefinitionId == binding.ProjectileDefinitionId);
            sourcePath = source?.RelativePath ?? string.Empty;
            objectPath = string.IsNullOrEmpty(stableId) ? string.Empty : "definition:" + stableId;
            if (!string.IsNullOrEmpty(stableId)) relatedStableIds.Add(stableId);
            string suffix = close + 1 < path.Length ? path.Substring(close + 1).TrimStart('.') : string.Empty;
            fieldPath = string.IsNullOrEmpty(suffix) ? "presentationBinding" : "presentationBinding." + suffix;
        }

        private static string NormalizeRelative(string value) => (value ?? string.Empty).Replace('\\', '/');

        private static void DisposeAll(List<ProjectileAuthoringSourceDocument> values)
        {
            for (int i = 0; i < values.Count; i++)
                values[i]?.Dispose();
        }

        private static void PopulateLegacyLabels(ProjectileRequestAuthoringDocument projection, JsonElement root)
        {
            if (projection?.Definition?.Impact == null || root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("definition", out var defEl) && defEl.ValueKind == JsonValueKind.Object && defEl.TryGetProperty("impact", out var impEl) && impEl.ValueKind == JsonValueKind.Object)
            {
                if (impEl.TryGetProperty("presentationProfileId", out var pidEl) && pidEl.ValueKind == JsonValueKind.String)
                {
                    string s = pidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.PresentationProfileLabel))
                        projection.Definition.Impact.PresentationProfileLabel = s;
                }
                if (impEl.TryGetProperty("effectTimelineId", out var tidEl) && tidEl.ValueKind == JsonValueKind.String)
                {
                    string s = tidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.EffectTimelineLabel))
                        projection.Definition.Impact.EffectTimelineLabel = s;
                }
                if (impEl.TryGetProperty("stableEventId", out var sidEl) && sidEl.ValueKind == JsonValueKind.String)
                {
                    string s = sidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.StableEventLabel))
                        projection.Definition.Impact.StableEventLabel = s;
                }
                if (impEl.TryGetProperty("hitEvent", out var hitEl) && hitEl.ValueKind == JsonValueKind.Object)
                {
                    if (hitEl.TryGetProperty("stableEventId", out var hsidEl) && hsidEl.ValueKind == JsonValueKind.String)
                    {
                        string s = hsidEl.GetString();
                        if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.StableEventLabel))
                            projection.Definition.Impact.HitEvent.StableEventLabel = s;
                    }
                    if (hitEl.TryGetProperty("damage", out var dmgEl) && dmgEl.ValueKind == JsonValueKind.Object)
                    {
                        if (dmgEl.TryGetProperty("damageTypeId", out var dtEl) && dtEl.ValueKind == JsonValueKind.String)
                        {
                            string s = dtEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent?.Damage != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.Damage.DamageTypeLabel))
                                projection.Definition.Impact.HitEvent.Damage.DamageTypeLabel = s;
                        }
                        if (dmgEl.TryGetProperty("analysisCategory", out var acEl) && acEl.ValueKind == JsonValueKind.String)
                        {
                            string s = acEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent?.Damage != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.Damage.AnalysisCategoryLabel))
                                projection.Definition.Impact.HitEvent.Damage.AnalysisCategoryLabel = s;
                        }
                    }
                }
            }
        }
    }

    internal static class ProjectileAuthoringReferenceIndexBuilder
    {
        public static ProjectileAuthoringReferenceIndex Build(
            IReadOnlyList<ProjectileAuthoringSourceDocument> sources,
            AbilityAuthoringDocument ability,
            List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            var sourceDocuments = new Dictionary<string, ProjectileAuthoringSourceDocument>(StringComparer.Ordinal);
            var requests = new Dictionary<string, ProjectileAuthoringSourceDocument>(StringComparer.Ordinal);
            var spawns = new Dictionary<string, ProjectileSpawnProfileConfig>(StringComparer.Ordinal);
            var definitions = new Dictionary<string, ProjectileDefinitionConfig>(StringComparer.Ordinal);
            var numeric = new Dictionary<int, ProjectileDefinitionConfig>();
            var bindings = new Dictionary<int, ProjectilePresentationBindingConfig>();
            var requestLists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var vfx = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var timelines = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var presentations = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < sources.Count; i++)
            {
                ProjectileAuthoringSourceDocument source = sources[i];
                if (string.IsNullOrWhiteSpace(source.SourceStableId) || !sourceDocuments.TryAdd(source.SourceStableId, source))
                    diagnostics.Add(Error("PROJAUTH001", source.RelativePath, "stableId", source.SourceStableId));
                if (source.Projection?.SpawnProfile != null)
                    requests.TryAdd(source.SourceStableId, source);
            }
            AddDefinitions(ability.ProjectileDefinitions, definitions, numeric, diagnostics);
            AddSpawns(ability.ProjectileSpawnProfiles, definitions, spawns, requestLists, diagnostics);
            AddBindings(ability.ProjectilePresentationBindings, numeric, bindings, vfx, diagnostics);
            AddDefinitionReferences(ability.ProjectileDefinitions, timelines, presentations);

            return new ProjectileAuthoringReferenceIndex
            {
                SourceDocumentsByStableId = sourceDocuments,
                RequestsByStableId = requests,
                SpawnProfilesByStableId = spawns,
                DefinitionsByStableId = definitions,
                DefinitionsByNumericId = numeric,
                BindingsByDefinitionId = bindings,
                RequestIdsByDefinitionStableId = Freeze(requestLists),
                ReferencedVfxResourceKeys = Freeze(vfx),
                ReferencedEffectTimelineIds = Freeze(timelines),
                ReferencedPresentationProfileIds = Freeze(presentations),
            };
        }

        private static void AddDefinitions(List<ProjectileDefinitionConfig> values, Dictionary<string, ProjectileDefinitionConfig> stable, Dictionary<int, ProjectileDefinitionConfig> numeric, List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            for (int i = 0; i < values.Count; i++)
            {
                ProjectileDefinitionConfig value = values[i];
                if (value == null)
                    continue;
                if (string.IsNullOrWhiteSpace(value.StableId) || !stable.TryAdd(value.StableId, value) || value.DefinitionId <= 0 || !numeric.TryAdd(value.DefinitionId, value))
                    diagnostics.Add(Error("PROJAUTH002", string.Empty, "definition", value.StableId));
            }
        }

        private static void AddSpawns(List<ProjectileSpawnProfileConfig> values, Dictionary<string, ProjectileDefinitionConfig> definitions, Dictionary<string, ProjectileSpawnProfileConfig> spawns, Dictionary<string, List<string>> requestLists, List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            for (int i = 0; i < values.Count; i++)
            {
                ProjectileSpawnProfileConfig value = values[i];
                if (value == null)
                    continue;
                if (string.IsNullOrWhiteSpace(value.StableId) || !spawns.TryAdd(value.StableId, value))
                    diagnostics.Add(Error("PROJAUTH001", string.Empty, "spawnProfile.stableId", value.StableId));
                if (!definitions.ContainsKey(value.ProjectileStableId ?? string.Empty))
                    diagnostics.Add(Error("PROJAUTH003", string.Empty, "spawnProfile.projectileStableId", value.ProjectileStableId));
                AddRef(requestLists, value.ProjectileStableId, value.StableId);
            }
        }

        private static void AddBindings(List<ProjectilePresentationBindingConfig> values, Dictionary<int, ProjectileDefinitionConfig> definitions, Dictionary<int, ProjectilePresentationBindingConfig> bindings, Dictionary<string, List<string>> vfx, List<ProjectileAuthoringDiagnostic> diagnostics)
        {
            for (int i = 0; i < values.Count; i++)
            {
                ProjectilePresentationBindingConfig value = values[i];
                if (value == null)
                    continue;
                if (!definitions.ContainsKey(value.ProjectileDefinitionId) || !bindings.TryAdd(value.ProjectileDefinitionId, value))
                    diagnostics.Add(Error("PROJAUTH002", string.Empty, "presentationBinding.projectileDefinitionId", value.ProjectileDefinitionId.ToString()));
                AddRef(vfx, value.PrefabResource?.Id, value.ProjectileDefinitionId.ToString());
                ProjectilePresentationSlotSetConfig slots = value.Slots;
                AddRef(vfx, slots?.Spawn?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.FlightBody?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.Trail?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.DirectImpact?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.AreaImpact?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.BlockedImpact?.Resource?.Id, value.ProjectileDefinitionId.ToString());
                AddRef(vfx, slots?.Expire?.Resource?.Id, value.ProjectileDefinitionId.ToString());
            }
        }

        private static void AddDefinitionReferences(List<ProjectileDefinitionConfig> values, Dictionary<string, List<string>> timelines, Dictionary<string, List<string>> presentations)
        {
            for (int i = 0; i < values.Count; i++)
            {
                ProjectileDefinitionConfig value = values[i];
                if (value?.Impact == null)
                    continue;
                AddRef(timelines, value.Impact.EffectTimelineLabel, value.StableId);
                AddRef(presentations, value.Impact.PresentationProfileLabel, value.StableId);
            }
        }

        private static void AddRef(Dictionary<string, List<string>> map, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            if (!map.TryGetValue(key, out List<string> list))
                map.Add(key, list = new List<string>());
            list.Add(value ?? string.Empty);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> Freeze(Dictionary<string, List<string>> values)
        {
            var output = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> pair in values)
            {
                pair.Value.Sort(StringComparer.Ordinal);
                output.Add(pair.Key, pair.Value.AsReadOnly());
            }
            return output;
        }

        private static ProjectileAuthoringDiagnostic Error(string code, string source, string field, string related)
        {
            return new ProjectileAuthoringDiagnostic
            {
                Code = code,
                Severity = "Error",
                Message = code == "PROJAUTH003" ? "Spawn profile references an unknown definition." : "Projectile identity is missing or duplicated.",
                SourcePath = source ?? string.Empty,
                FieldPath = field ?? string.Empty,
                RelatedStableIds = string.IsNullOrWhiteSpace(related) ? new List<string>() : new List<string> { related },
            };
        }
    }
}
