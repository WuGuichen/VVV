using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Authoring
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ValidationIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string RowId { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
    }

    public sealed class ValidationReport
    {
        public string PackageId { get; set; } = string.Empty;
        public bool RequiresUpgrade { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Authoring Validation Report\n");
            builder.Append("package=").Append(PackageId).Append('\n');
            builder.Append("status=").Append(HasErrors ? "blocked" : "ready").Append('\n');
            builder.Append("requiresUpgrade=").Append(RequiresUpgrade ? "true" : "false").Append('\n');
            for (int i = 0; i < Issues.Count; i++)
            {
                ValidationIssue issue = Issues[i];
                builder.Append(issue.Severity).Append(" code=").Append(issue.Code)
                    .Append(" source=").Append(issue.Source)
                    .Append(" row=").Append(issue.RowId)
                    .Append(" field=").Append(issue.Field)
                    .Append(" message=").Append(issue.Message)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }

    internal static class ValidationHelpers
    {
        public static void Add(ValidationReport report, IssueSeverity severity, string code, string message, string source, string rowId, string field)
        {
            report.Issues.Add(new ValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                Source = source ?? string.Empty,
                RowId = rowId ?? string.Empty,
                Field = field ?? string.Empty
            });
        }

        public static bool IsVisibleForBuffType(SchemaField field, string buffType)
        {
            if (field.VisibleWhenBuffTypes.Count == 0)
                return true;

            for (int i = 0; i < field.VisibleWhenBuffTypes.Count; i++)
            {
                if (field.VisibleWhenBuffTypes[i] == buffType)
                    return true;
            }

            return false;
        }

        public static void CheckSchemaVersion(ValidationReport report, string version, string code, string source, string rowId)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Add(report, IssueSeverity.Error, code, "schemaVersion is required.", source, rowId, "schemaVersion");
                return;
            }

            if (AuthoringSchemaVersions.IsSupported(version))
                return;

            if (AuthoringSchemaVersions.IsHigherThanLatest(version))
            {
                Add(report, IssueSeverity.Warning, "patch.schemaVersion.unknown",
                    "schemaVersion " + version + " is newer than supported; treating as forward-compatible.", source, rowId, "schemaVersion");
                report.RequiresUpgrade = true;
            }
            else
            {
                Add(report, IssueSeverity.Warning, "patch.schemaVersion.unknown",
                    "schemaVersion " + version + " is not in the supported list; reading in compatibility mode.", source, rowId, "schemaVersion");
            }
        }
    }

    public static class PackageValidator
    {
        private const string RuntimePatchFormat = "mx.runtimeConfigPatch.v1";

        public static ValidationReport Validate(ModPackageManifest manifest, IEnumerable<PatchDocument> patches)
        {
            var report = new ValidationReport { PackageId = manifest != null ? manifest.PackageId : string.Empty };
            if (manifest == null)
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "manifest.missing", "mod.json is missing.", "", "", "");
                return report;
            }

            if (string.IsNullOrWhiteSpace(manifest.PackageId))
                ValidationHelpers.Add(report, IssueSeverity.Error, "manifest.packageId", "packageId is required.", "", "", "packageId");
            ValidationHelpers.CheckSchemaVersion(report, manifest.SchemaVersion, "manifest.schemaVersion", "", "");
            if (manifest.Kind == PackageKind.Preview && manifest.GameVersionRange == "*")
                ValidationHelpers.Add(report, IssueSeverity.Warning, "manifest.previewRange", "Preview package uses wildcard gameVersionRange.", "", "", "gameVersionRange");

            if (patches == null)
            {
                ValidationHelpers.Add(report, IssueSeverity.Warning, "patch.none", "No patch documents were found.", "", "", "");
                return report;
            }

            int patchCount = 0;
            foreach (PatchDocument patch in patches)
            {
                patchCount++;
                ValidatePatch(report, patch);
            }

            if (patchCount == 0)
                ValidationHelpers.Add(report, IssueSeverity.Warning, "patch.none", "No patch documents were found.", "", "", "");
            return report;
        }

        private static void ValidatePatch(ValidationReport report, PatchDocument patch)
        {
            if (patch == null)
                return;

            ValidationHelpers.CheckSchemaVersion(report, patch.SchemaVersion, "patch.schemaVersion", patch.Source, "");
            if (string.IsNullOrWhiteSpace(patch.Source))
                ValidationHelpers.Add(report, IssueSeverity.Error, "patch.source", "Patch source is required.", patch.Source, "", "source");

            for (int i = 0; i < patch.Entries.Count; i++)
            {
                PatchEntry entry = patch.Entries[i];
                if (string.IsNullOrWhiteSpace(entry.Id))
                    ValidationHelpers.Add(report, IssueSeverity.Error, "entry.id", "Patch entry id is required.", entry.Source, entry.Id, "id");
                if (string.IsNullOrWhiteSpace(entry.Layer))
                    ValidationHelpers.Add(report, IssueSeverity.Error, "entry.layer", "Patch entry layer is required.", entry.Source, entry.Id, "layer");
                if (entry.Layer == "Base")
                    ValidationHelpers.Add(report, IssueSeverity.Error, "entry.baseWrite", "Patch entry must not write Base layer.", entry.Source, entry.Id, "layer");
                if (entry.Operation == PatchOperation.Upsert && entry.Fields.Count == 0)
                    ValidationHelpers.Add(report, IssueSeverity.Warning, "entry.empty", "Upsert entry has no fields.", entry.Source, entry.Id, "");
            }
        }

        /// <summary>
        /// Validate runtime patch entry: path syntax, format, and kind-layer match.
        /// Called by CLI <c>package validate</c> after file I/O resolution.
        /// </summary>
        /// <param name="report">Validation report to populate.</param>
        /// <param name="manifest">The package manifest.</param>
        /// <param name="resolvedPatchPath">The absolute file path resolved from mod.json runtimePatch field.</param>
        /// <param name="runtimePatchFormat">The 'format' field read from the runtime patch JSON file.</param>
        /// <param name="runtimePatchLayer">The 'layer' field read from the runtime patch JSON file.</param>
        public static void ValidateRuntimePatch(
            ValidationReport report,
            ModPackageManifest manifest,
            string resolvedPatchPath,
            string runtimePatchFormat,
            string runtimePatchLayer)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.RuntimePatch))
                return;

            // Validate format
            if (runtimePatchFormat != RuntimePatchFormat)
                ValidationHelpers.Add(report, IssueSeverity.Error, "package.runtimePatch.format",
                    "Runtime Patch format must be '" + RuntimePatchFormat + "', but got '" + (runtimePatchFormat ?? "(null)") + "'.",
                    manifest.PackageId, "", "runtimePatch");

            // Validate kind-layer match
            if (manifest.Kind == PackageKind.Mod && runtimePatchLayer != "Mod")
                ValidationHelpers.Add(report, IssueSeverity.Error, "package.kindLayerMismatch",
                    "Package kind is 'Mod' but Runtime Patch layer is '" + (runtimePatchLayer ?? "(null)") + "'; expected 'Mod'.",
                    manifest.PackageId, "", "kind/layer");

            if (manifest.Kind == PackageKind.Preview && runtimePatchLayer != "Patch")
                ValidationHelpers.Add(report, IssueSeverity.Error, "package.kindLayerMismatch",
                    "Package kind is 'Preview' but Runtime Patch layer is '" + (runtimePatchLayer ?? "(null)") + "'; expected 'Patch'.",
                    manifest.PackageId, "", "kind/layer");
        }
    }

    public static class AuthoringValidate
    {
        public static ValidationReport Run(ProjectAuthoringManifest manifest, ModPackageManifest mod, IEnumerable<PatchDocument> patches)
        {
            if (manifest != null)
                return ManifestAwareValidator.Validate(manifest, mod, patches);
            return PackageValidator.Validate(mod, patches);
        }
    }

    public static class AuthoringPrecommit
    {
        public static string Status(int exit)
        {
            switch (exit)
            {
                case AuthoringExitCodes.Ready: return "ready";
                case AuthoringExitCodes.ValidationBlocked: return "blocked";
                case AuthoringExitCodes.SchemaIncompatible: return "upgrade";
                default: return "error";
            }
        }

        public static string BuildText(string packagePath, string status, int exit, ValidationReport report)
        {
            var sb = new StringBuilder();
            sb.Append("MxFramework Authoring Precommit\n");
            sb.Append("package=").Append(packagePath).Append('\n');
            sb.Append("status=").Append(status).Append('\n');
            sb.Append("exit=").Append(exit).Append('\n');
            int errors = 0;
            int warnings = 0;
            var codeCounts = new Dictionary<string, int>();
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                if (issue.Severity == IssueSeverity.Error) errors++;
                else if (issue.Severity == IssueSeverity.Warning) warnings++;
                if (string.IsNullOrEmpty(issue.Code)) continue;
                codeCounts.TryGetValue(issue.Code, out int c);
                codeCounts[issue.Code] = c + 1;
            }
            sb.Append("errors=").Append(errors).Append('\n');
            sb.Append("warnings=").Append(warnings).Append('\n');
            foreach (KeyValuePair<string, int> kv in codeCounts)
                sb.Append("code.").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            return sb.ToString();
        }
    }

    public static class ManifestAwareValidator
    {
        private static readonly string[] DefaultAssetPrefixes = new[] { "Effects/", "Audio/", "UI/" };

        public static ValidationReport Validate(ProjectAuthoringManifest projectManifest, ModPackageManifest mod, IEnumerable<PatchDocument> patches)
        {
            ValidationReport report = PackageValidator.Validate(mod, patches);
            if (projectManifest == null || patches == null)
                return report;

            HashSet<string> localeKeys = BuildLocaleKeys(projectManifest);
            Dictionary<string, ConfigSchema> schemasBySource = BuildSchemaIndex(projectManifest);
            Dictionary<string, EnumDomain> enumsById = BuildEnumIndex(projectManifest);
            Dictionary<string, HashSet<string>> referenceIds = BuildReferenceIndex(projectManifest);
            string[] assetPrefixes = ResolveAssetPrefixes(projectManifest);

            foreach (PatchDocument patch in patches)
            {
                if (patch == null) continue;
                for (int i = 0; i < patch.Entries.Count; i++)
                {
                    PatchEntry entry = patch.Entries[i];
                    if (entry.Operation != PatchOperation.Upsert) continue;
                    string sourceName = !string.IsNullOrEmpty(entry.Source) ? entry.Source : patch.Source;
                    if (!schemasBySource.TryGetValue(sourceName, out ConfigSchema schema)) continue;
                    string buffType = entry.Fields.GetScalar("Type");
                    for (int f = 0; f < schema.Fields.Count; f++)
                    {
                        SchemaField sf = schema.Fields[f];
                        if (!ValidationHelpers.IsVisibleForBuffType(sf, buffType)) continue;
                        if (sf.Required && IsFieldMissing(entry.Fields, sf.Name))
                        {
                            ValidationHelpers.Add(report, IssueSeverity.Error, "entry.requiredField",
                                (sf.DisplayName.Length > 0 ? sf.DisplayName : sf.Name) + " is required.",
                                entry.Source, entry.Id, sf.Name);
                        }
                    }
                    foreach (KeyValuePair<string, FieldValue> kv in entry.Fields)
                    {
                        SchemaField field = FindField(schema, kv.Key);
                        if (field == null) continue;
                        if (kv.Value == null) continue;

                        if (field.Type == FieldType.Reference)
                        {
                            ValidateReference(report, entry, field, kv.Value, referenceIds);
                            continue;
                        }

                        if (kv.Value.Kind != FieldValueKind.Scalar) continue;
                        string raw = kv.Value.Scalar ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        switch (field.Type)
                        {
                            case FieldType.Integer:
                                ValidateInteger(report, entry, field, raw);
                                break;
                            case FieldType.Float:
                                ValidateFloat(report, entry, field, raw);
                                break;
                            case FieldType.Enum:
                                ValidateEnum(report, entry, field, raw, enumsById);
                                break;
                            case FieldType.LocalizedText:
                                ValidateLocalizedText(report, entry, field, raw, localeKeys);
                                break;
                            case FieldType.AssetPath:
                                ValidateAssetPath(report, entry, field, raw, assetPrefixes);
                                break;
                        }
                    }

                    if (buffType == "DamageByAttr")
                        ValidateDamageByAttr(report, entry);
                }
            }

            return report;
        }

        private static SchemaField FindField(ConfigSchema schema, string name)
        {
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                if (schema.Fields[i].Name == name) return schema.Fields[i];
            }
            return null;
        }

        private static void ValidateReference(ValidationReport report, PatchEntry entry, SchemaField field, FieldValue value, Dictionary<string, HashSet<string>> references)
        {
            if (string.IsNullOrEmpty(field.ReferenceSource)) return;
            if (!references.TryGetValue(field.ReferenceSource, out HashSet<string> ids))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "entry.referenceMissing",
                    "Reference source " + field.ReferenceSource + " is not declared in manifest.",
                    entry.Source, entry.Id, field.Name);
                return;
            }

            if (field.IsList && value.Kind == FieldValueKind.List && value.List != null)
            {
                for (int i = 0; i < value.List.Count; i++)
                {
                    FieldValue item = value.List[i];
                    if (item == null || item.Kind != FieldValueKind.Scalar) continue;
                    string raw = item.Scalar ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    CheckReferenceToken(report, entry, field, raw.Trim(), ids);
                }
                return;
            }

            if (value.Kind != FieldValueKind.Scalar) return;
            string scalar = value.Scalar ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scalar)) return;

            if (field.IsList)
            {
                string[] tokens = scalar.Split(',');
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i].Trim();
                    if (string.IsNullOrEmpty(token)) continue;
                    CheckReferenceToken(report, entry, field, token, ids);
                }
                return;
            }

            CheckReferenceToken(report, entry, field, scalar.Trim(), ids);
        }

        private static void CheckReferenceToken(ValidationReport report, PatchEntry entry, SchemaField field, string token, HashSet<string> ids)
        {
            if (string.IsNullOrEmpty(token)) return;
            if (!ids.Contains(token))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "entry.referenceMissing",
                    field.Name + " references missing id '" + token + "' in " + field.ReferenceSource + ".",
                    entry.Source, entry.Id, field.Name);
            }
        }

        private static bool IsFieldMissing(IDictionary<string, FieldValue> fields, string name)
        {
            if (fields == null || string.IsNullOrEmpty(name)) return true;
            if (!fields.TryGetValue(name, out FieldValue v) || v == null) return true;
            switch (v.Kind)
            {
                case FieldValueKind.Scalar:
                    return string.IsNullOrWhiteSpace(v.Scalar);
                case FieldValueKind.List:
                    return v.List == null || v.List.Count == 0;
                case FieldValueKind.Map:
                    return v.Map == null || v.Map.Count == 0;
                default:
                    return true;
            }
        }

        private static void ValidateEnum(ValidationReport report, PatchEntry entry, SchemaField field, string raw, Dictionary<string, EnumDomain> enums)
        {
            if (string.IsNullOrEmpty(field.EnumId)) return;
            if (!enums.TryGetValue(field.EnumId, out EnumDomain domain))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "entry.enumInvalid",
                    "Enum domain " + field.EnumId + " is not declared in manifest.",
                    entry.Source, entry.Id, field.Name);
                return;
            }

            string[] tokens = domain.IsFlags ? raw.Split('|') : new[] { raw };
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token)) continue;
                bool found = false;
                for (int j = 0; j < domain.Options.Count; j++)
                {
                    if (domain.Options[j].Name == token) { found = true; break; }
                }
                if (!found)
                {
                    ValidationHelpers.Add(report, IssueSeverity.Error, "entry.enumInvalid",
                        field.Name + " uses invalid enum value '" + token + "' for " + field.EnumId + ".",
                        entry.Source, entry.Id, field.Name);
                }
            }
        }

        private static void ValidateInteger(ValidationReport report, PatchEntry entry, SchemaField field, string raw)
        {
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "entry.integerInvalid",
                    field.Name + " must be an integer.",
                    entry.Source, entry.Id, field.Name);
            }
        }

        private static void ValidateFloat(ValidationReport report, PatchEntry entry, SchemaField field, string raw)
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "entry.floatInvalid",
                    field.Name + " must be a number.",
                    entry.Source, entry.Id, field.Name);
            }
        }

        private static void ValidateDamageByAttr(ValidationReport report, PatchEntry entry)
        {
            string values = entry.Fields.GetScalar("Values");
            if (!string.IsNullOrWhiteSpace(values) && !IsSupportedDamageByAttrFormula(values))
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "buff.damageByAttr.valuesUnsupported",
                    "DamageByAttr Values currently supports a number or 'caster.Attack * factor'.",
                    entry.Source, entry.Id, "Values");
            }

            if (TryReadPositiveMilliseconds(entry, "Duration", out long durationMs) && durationMs <= 0)
            {
                ValidationHelpers.Add(report, IssueSeverity.Error, "buff.damageByAttr.durationInvalid",
                    "DamageByAttr Duration must be greater than 0 ms.",
                    entry.Source, entry.Id, "Duration");
            }

            if (TryReadPositiveMilliseconds(entry, "HitCooldown", out long hitCooldownMs))
            {
                if (hitCooldownMs <= 0)
                {
                    ValidationHelpers.Add(report, IssueSeverity.Error, "buff.damageByAttr.hitCooldownInvalid",
                        "DamageByAttr HitCooldown must be greater than 0 ms.",
                        entry.Source, entry.Id, "HitCooldown");
                }
                else if (durationMs > 0 && hitCooldownMs > durationMs)
                {
                    ValidationHelpers.Add(report, IssueSeverity.Warning, "buff.damageByAttr.hitCooldownLongerThanDuration",
                        "HitCooldown is longer than Duration, so this Buff may never tick in preview.",
                        entry.Source, entry.Id, "HitCooldown");
                }
            }
        }

        private static bool TryReadPositiveMilliseconds(PatchEntry entry, string field, out long value)
        {
            value = 0;
            string raw = entry.Fields.GetScalar(field);
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsSupportedDamageByAttrFormula(string raw)
        {
            string text = (raw ?? string.Empty).Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return true;

            const string token = "caster.Attack";
            if (!text.StartsWith(token, System.StringComparison.OrdinalIgnoreCase))
                return false;

            string rest = text.Substring(token.Length).Trim();
            if (rest.Length == 0)
                return true;
            if (!rest.StartsWith("*"))
                return false;
            string factor = rest.Substring(1).Trim();
            return double.TryParse(factor, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static void ValidateLocalizedText(ValidationReport report, PatchEntry entry, SchemaField field, string raw, HashSet<string> localeKeys)
        {
            if (localeKeys.Contains(raw)) return;
            ValidationHelpers.Add(report, IssueSeverity.Warning, "entry.localeMissing",
                field.Name + " value '" + raw + "' is not declared in localization manifest.",
                entry.Source, entry.Id, field.Name);
        }

        private static void ValidateAssetPath(ValidationReport report, PatchEntry entry, SchemaField field, string raw, string[] prefixes)
        {
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (raw.StartsWith(prefixes[i])) return;
            }
            ValidationHelpers.Add(report, IssueSeverity.Error, "entry.assetPathDenied",
                field.Name + " path '" + raw + "' is not in asset whitelist.",
                entry.Source, entry.Id, field.Name);
        }

        private static HashSet<string> BuildLocaleKeys(ProjectAuthoringManifest manifest)
        {
            var set = new HashSet<string>();
            for (int i = 0; i < manifest.Localization.Count; i++)
            {
                if (!string.IsNullOrEmpty(manifest.Localization[i].Key))
                    set.Add(manifest.Localization[i].Key);
            }
            return set;
        }

        private static Dictionary<string, ConfigSchema> BuildSchemaIndex(ProjectAuthoringManifest manifest)
        {
            var dict = new Dictionary<string, ConfigSchema>();
            for (int i = 0; i < manifest.Schemas.Count; i++)
            {
                dict[manifest.Schemas[i].SchemaId] = manifest.Schemas[i];
            }
            return dict;
        }

        private static Dictionary<string, EnumDomain> BuildEnumIndex(ProjectAuthoringManifest manifest)
        {
            var dict = new Dictionary<string, EnumDomain>();
            for (int i = 0; i < manifest.Enums.Count; i++)
            {
                dict[manifest.Enums[i].EnumId] = manifest.Enums[i];
            }
            return dict;
        }

        private static Dictionary<string, HashSet<string>> BuildReferenceIndex(ProjectAuthoringManifest manifest)
        {
            var dict = new Dictionary<string, HashSet<string>>();
            for (int i = 0; i < manifest.References.Count; i++)
            {
                ReferenceIndex idx = manifest.References[i];
                var set = new HashSet<string>();
                for (int j = 0; j < idx.Entries.Count; j++)
                {
                    if (!string.IsNullOrEmpty(idx.Entries[j].Id))
                        set.Add(idx.Entries[j].Id);
                }
                dict[idx.Source] = set;
            }
            return dict;
        }

        private static string[] ResolveAssetPrefixes(ProjectAuthoringManifest manifest)
        {
            if (manifest.AssetWhitelistPrefixes != null && manifest.AssetWhitelistPrefixes.Count > 0)
                return manifest.AssetWhitelistPrefixes.ToArray();
            return DefaultAssetPrefixes;
        }
    }
}
