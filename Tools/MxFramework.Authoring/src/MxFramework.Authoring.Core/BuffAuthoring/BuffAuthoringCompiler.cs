using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    public static class BuffAuthoringCompiler
    {
        public const string BuffAuthoringDocumentRelativePath = "config/buff_authoring.json";
        public const int DefaultDescriptionFrameRate = 60;
        public const int DefaultInstantFrameRateBase = 30;
        private static readonly string[] KnownBuffTypes =
        {
            "empower",
            "weaken",
            "damage",
            "restore",
            "control",
            "damageReduction",
            "mechanic"
        };

        private static readonly HashSet<string> KnownBuffTypeSet =
            new HashSet<string>(KnownBuffTypes, StringComparer.Ordinal);

        private static readonly HashSet<string> KnownStackPolicies =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RefreshDuration",
                "Replace",
                "Stack"
            };

        private static readonly HashSet<string> KnownEffectKinds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AttributeModifier",
                "Status"
            };

        private static readonly HashSet<string> KnownRelationFilters =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Any",
                "Self",
                "SameTeam",
                "Enemy"
            };

        private static readonly HashSet<string> KnownDescriptionModes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Auto",
                "Manual"
            };

        public static BuffAuthoringCompileResult Compile(BuffAuthoringDocument document)
        {
            document ??= new BuffAuthoringDocument();
            Normalize(document);
            BuffAuthoringValidationReport validation = Validate(document);

            return new BuffAuthoringCompileResult
            {
                PackageId = document.PackageId ?? string.Empty,
                ValidationReport = validation,
                BuffDefinitions = BuildBuffDefinitionCatalog(document),
                AuraBuffDefinitions = BuildAuraBuffDefinitionCatalog(document),
                BuffPresentationDefinitions = BuildBuffPresentationDefinitionCatalog(document)
            };
        }

        public static BuffAuthoringValidationReport Validate(BuffAuthoringDocument document)
        {
            document ??= new BuffAuthoringDocument();
            Normalize(document);

            var report = new BuffAuthoringValidationReport
            {
                PackageId = document.PackageId ?? string.Empty
            };

            if (!string.Equals(document.SchemaVersion, "1", StringComparison.Ordinal))
                Error(report, "BUFF_SCHEMA_UNSUPPORTED", "schemaVersion", "Buff authoring schemaVersion must be 1.");

            var buffIds = new HashSet<int>();
            var buffStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.Buffs.Count; i++)
            {
                BuffDefinitionConfig buff = document.Buffs[i];
                string path = "buffs[" + i + "]";
                if (buff == null)
                {
                    Error(report, "BUFF_DEFINITION_NULL", path, "Buff definition entry is null.");
                    continue;
                }

                if (buff.BuffId <= 0)
                    Error(report, "BUFF_ID_INVALID", path + ".buffId", "Buff id must be positive.");
                else if (!buffIds.Add(buff.BuffId))
                    Error(report, "BUFF_ID_DUPLICATE", path + ".buffId", "Duplicate buff id: " + buff.BuffId + ".");

                if (string.IsNullOrWhiteSpace(buff.StableId))
                    Error(report, "BUFF_STABLE_ID_REQUIRED", path + ".stableId", "Buff stable id is required.");
                else if (!buffStableIds.Add(buff.StableId))
                    Error(report, "BUFF_STABLE_ID_DUPLICATE", path + ".stableId", "Duplicate buff stable id: " + buff.StableId + ".");

                if (!buff.IsPermanent && buff.DefaultDurationFrames <= 0L)
                    Error(report, "BUFF_DURATION_INVALID", path + ".defaultDurationFrames", "Non-permanent buffs require a positive default duration.");
                if (buff.DefaultDurationFrames < 0L)
                    Error(report, "BUFF_DURATION_INVALID", path + ".defaultDurationFrames", "Buff default duration cannot be negative.");
                if (buff.MaxStackCount <= 0)
                    Error(report, "BUFF_MAX_STACK_INVALID", path + ".maxStackCount", "Buff maxStackCount must be positive.");
                if (buff.ApplyStackCount <= 0)
                    Error(report, "BUFF_APPLY_STACK_INVALID", path + ".applyStackCount", "Buff applyStackCount must be positive.");
                if (buff.MaxStackCount > 0 && buff.ApplyStackCount > buff.MaxStackCount)
                    Error(report, "BUFF_APPLY_STACK_INVALID", path + ".applyStackCount", "Buff applyStackCount cannot exceed maxStackCount.");
                if (!KnownStackPolicies.Contains(buff.StackPolicy ?? string.Empty))
                    Error(report, "BUFF_STACK_POLICY_INVALID", path + ".stackPolicy", "Buff stackPolicy is unsupported: " + (buff.StackPolicy ?? string.Empty) + ".");

                ValidateBuffTypes(report, buff.Types, path + ".types");
                ValidateEffects(report, buff.Effects, path + ".effects");
                ValidatePresentation(report, buff.Presentation, path + ".presentation");
            }

            var auraIds = new HashSet<int>();
            for (int i = 0; i < document.Auras.Count; i++)
            {
                AuraBuffDefinitionConfig aura = document.Auras[i];
                string path = "auras[" + i + "]";
                if (aura == null)
                {
                    Error(report, "BUFF_AURA_NULL", path, "Aura definition entry is null.");
                    continue;
                }

                if (aura.AuraId <= 0)
                    Error(report, "BUFF_AURA_ID_INVALID", path + ".auraId", "Aura id must be positive.");
                else if (!auraIds.Add(aura.AuraId))
                    Error(report, "BUFF_AURA_ID_DUPLICATE", path + ".auraId", "Duplicate aura id: " + aura.AuraId + ".");
                if (aura.BuffId <= 0)
                    Error(report, "BUFF_AURA_BUFF_ID_INVALID", path + ".buffId", "Aura buffId must be positive.");
                else if (!buffIds.Contains(aura.BuffId))
                    Error(report, "BUFF_AURA_BUFF_UNKNOWN", path + ".buffId", "Aura references an unknown buff id: " + aura.BuffId + ".");
                if (aura.RadiusMeters <= 0f)
                    Error(report, "BUFF_AURA_RADIUS_INVALID", path + ".radiusMeters", "Aura radiusMeters must be positive.");
                if (aura.LayerMask <= 0)
                    Error(report, "BUFF_AURA_LAYER_MASK_INVALID", path + ".layerMask", "Aura layerMask must be positive.");
                if (!KnownRelationFilters.Contains(aura.RelationFilter ?? string.Empty))
                    Error(report, "BUFF_AURA_RELATION_INVALID", path + ".relationFilter", "Aura relationFilter is unsupported: " + (aura.RelationFilter ?? string.Empty) + ".");
            }

            report.HasBlockingIssues = report.Diagnostics.Exists(diagnostic =>
                string.Equals(diagnostic.Severity, "Error", StringComparison.Ordinal));
            report.Status = report.HasBlockingIssues ? "Blocked" : "Ready";
            return report;
        }

        public static string ResolveDocumentPath(string packageOrDocumentPath)
        {
            string resolved = Path.GetFullPath(packageOrDocumentPath ?? string.Empty);
            if (File.Exists(resolved) || string.Equals(Path.GetExtension(resolved), ".json", StringComparison.OrdinalIgnoreCase))
                return resolved;

            return Path.Combine(resolved, BuffAuthoringDocumentRelativePath);
        }

        public static string ResolvePackageRootPath(string packageOrDocumentPath)
        {
            string documentPath = ResolveDocumentPath(packageOrDocumentPath);
            string full = Path.GetFullPath(documentPath);
            if (string.Equals(Path.GetFileName(full), Path.GetFileName(BuffAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(Path.GetDirectoryName(full));

            return Path.GetDirectoryName(full);
        }

        private static void ValidateEffects(
            BuffAuthoringValidationReport report,
            List<BuffEffectConfig> effects,
            string path)
        {
            var effectIds = new HashSet<int>();
            for (int i = 0; effects != null && i < effects.Count; i++)
            {
                BuffEffectConfig effect = effects[i];
                string effectPath = path + "[" + i + "]";
                if (effect == null)
                {
                    Error(report, "BUFF_EFFECT_NULL", effectPath, "Buff effect entry is null.");
                    continue;
                }

                if (effect.EffectId <= 0 || effect.EffectId > 999)
                    Error(report, "BUFF_EFFECT_ID_INVALID", effectPath + ".effectId", "Buff effectId must be in the 1..999 range.");
                else if (!effectIds.Add(effect.EffectId))
                    Error(report, "BUFF_EFFECT_ID_DUPLICATE", effectPath + ".effectId", "Duplicate buff effect id: " + effect.EffectId + ".");

                if (!KnownEffectKinds.Contains(effect.Kind ?? string.Empty))
                {
                    Error(report, "BUFF_EFFECT_KIND_INVALID", effectPath + ".kind", "Buff effect kind is unsupported: " + (effect.Kind ?? string.Empty) + ".");
                    continue;
                }

                if (string.Equals(effect.Kind, "AttributeModifier", StringComparison.Ordinal) && effect.AttributeId <= 0)
                    Error(report, "BUFF_EFFECT_ATTRIBUTE_INVALID", effectPath + ".attributeId", "AttributeModifier effect requires a positive attributeId.");
                if (string.Equals(effect.Kind, "Status", StringComparison.Ordinal) && effect.StatusId <= 0)
                    Error(report, "BUFF_EFFECT_STATUS_INVALID", effectPath + ".statusId", "Status effect requires a positive statusId.");
            }
        }

        private static void Normalize(BuffAuthoringDocument document)
        {
            document.SchemaVersion = string.IsNullOrWhiteSpace(document.SchemaVersion) ? "1" : document.SchemaVersion;
            document.PackageId ??= string.Empty;
            document.Buffs ??= new List<BuffDefinitionConfig>();
            document.Auras ??= new List<AuraBuffDefinitionConfig>();

            for (int i = 0; i < document.Buffs.Count; i++)
            {
                BuffDefinitionConfig buff = document.Buffs[i];
                if (buff == null)
                    continue;

                buff.StableId ??= string.Empty;
                buff.DisplayName ??= string.Empty;
                buff.Types = NormalizeBuffTypes(buff.Types);
                buff.StackPolicy = string.IsNullOrWhiteSpace(buff.StackPolicy) ? "RefreshDuration" : buff.StackPolicy;
                buff.Effects ??= new List<BuffEffectConfig>();
                if (buff.Presentation != null)
                {
                    buff.Presentation.NameKey ??= string.Empty;
                    buff.Presentation.ShortName ??= string.Empty;
                    buff.Presentation.DescriptionMode = string.IsNullOrWhiteSpace(buff.Presentation.DescriptionMode)
                        ? InferDescriptionMode(buff.Presentation)
                        : buff.Presentation.DescriptionMode;
                    buff.Presentation.Description ??= string.Empty;
                    buff.Presentation.IconResourceKey ??= string.Empty;
                    buff.Presentation.Polarity = null;
                }

                for (int effectIndex = 0; effectIndex < buff.Effects.Count; effectIndex++)
                {
                    BuffEffectConfig effect = buff.Effects[effectIndex];
                    if (effect == null)
                        continue;
                    effect.Kind ??= string.Empty;
                }
            }

            for (int i = 0; i < document.Auras.Count; i++)
            {
                AuraBuffDefinitionConfig aura = document.Auras[i];
                if (aura == null)
                    continue;
                aura.RelationFilter = string.IsNullOrWhiteSpace(aura.RelationFilter) ? "Any" : aura.RelationFilter;
            }
        }

        private static BuffDefinitionCatalogDocument BuildBuffDefinitionCatalog(BuffAuthoringDocument document)
        {
            var definitions = CopyBuffs(document.Buffs);
            definitions.Sort(CompareBuffs);
            return new BuffDefinitionCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty,
                Definitions = definitions
            };
        }

        private static AuraBuffDefinitionCatalogDocument BuildAuraBuffDefinitionCatalog(BuffAuthoringDocument document)
        {
            var definitions = CopyAuras(document.Auras);
            definitions.Sort(CompareAuras);
            return new AuraBuffDefinitionCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty,
                Definitions = definitions
            };
        }

        private static BuffPresentationDefinitionCatalogDocument BuildBuffPresentationDefinitionCatalog(
            BuffAuthoringDocument document)
        {
            var definitions = CopyPresentations(document.Buffs);
            definitions.Sort(ComparePresentations);
            return new BuffPresentationDefinitionCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty,
                Definitions = definitions
            };
        }

        private static List<BuffDefinitionConfig> CopyBuffs(List<BuffDefinitionConfig> source)
        {
            var copy = new List<BuffDefinitionConfig>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                BuffDefinitionConfig buff = source[i];
                if (buff == null)
                    continue;

                var effects = new List<BuffEffectConfig>();
                for (int effectIndex = 0; buff.Effects != null && effectIndex < buff.Effects.Count; effectIndex++)
                {
                    BuffEffectConfig effect = buff.Effects[effectIndex];
                    if (effect == null)
                        continue;
                    effects.Add(new BuffEffectConfig
                    {
                        EffectId = effect.EffectId,
                        Kind = effect.Kind ?? string.Empty,
                        AttributeId = effect.AttributeId,
                        AddValue = effect.AddValue,
                        StatusId = effect.StatusId
                    });
                }
                effects.Sort(CompareEffects);

                copy.Add(new BuffDefinitionConfig
                {
                    BuffId = buff.BuffId,
                    StableId = buff.StableId ?? string.Empty,
                    DisplayName = buff.DisplayName ?? string.Empty,
                    DefaultDurationFrames = buff.DefaultDurationFrames,
                    MaxStackCount = buff.MaxStackCount,
                    ApplyStackCount = buff.ApplyStackCount,
                    StackPolicy = buff.StackPolicy ?? string.Empty,
                    IsPermanent = buff.IsPermanent,
                    Effects = effects,
                    Types = null,
                    Presentation = null
                });
            }

            return copy;
        }

        private static List<BuffPresentationDefinitionConfig> CopyPresentations(
            List<BuffDefinitionConfig> source)
        {
            var copy = new List<BuffPresentationDefinitionConfig>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                BuffDefinitionConfig buff = source[i];
                if (buff == null)
                    continue;

                BuffPresentationConfig presentation = buff.Presentation;
                string polarity = InferPresentationPolarity(buff.Types);
                int priority = presentation != null && presentation.Priority.HasValue
                    ? presentation.Priority.Value
                    : DefaultPresentationPriority(polarity);
                string displayName = !string.IsNullOrWhiteSpace(buff.DisplayName)
                    ? buff.DisplayName
                    : buff.StableId ?? string.Empty;
                string descriptionMode = presentation != null
                    ? InferDescriptionMode(presentation)
                    : "Auto";

                copy.Add(new BuffPresentationDefinitionConfig
                {
                    BuffId = buff.BuffId,
                    StableId = buff.StableId ?? string.Empty,
                    DisplayName = displayName,
                    NameKey = presentation?.NameKey ?? string.Empty,
                    ShortName = presentation != null && !string.IsNullOrWhiteSpace(presentation.ShortName)
                        ? presentation.ShortName
                        : BuildShortName(displayName, buff.BuffId),
                    DescriptionMode = descriptionMode,
                    Description = string.Equals(descriptionMode, "Manual", StringComparison.Ordinal)
                        ? presentation?.Description ?? string.Empty
                        : FormatAutoDescription(buff, "zh-CN", DefaultDescriptionFrameRate),
                    IconResourceKey = presentation?.IconResourceKey ?? string.Empty,
                    Polarity = polarity,
                    Priority = priority,
                    ShowOnPlayerHud = presentation?.ShowOnPlayerHud ?? true,
                    ShowOnTargetHud = presentation?.ShowOnTargetHud ?? true,
                    ShowOnHeadPad = presentation?.ShowOnHeadPad ?? true,
                    EffectSummary = FormatEffects(buff.Effects)
                });
            }

            return copy;
        }

        private static List<AuraBuffDefinitionConfig> CopyAuras(List<AuraBuffDefinitionConfig> source)
        {
            var copy = new List<AuraBuffDefinitionConfig>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                AuraBuffDefinitionConfig aura = source[i];
                if (aura == null)
                    continue;
                copy.Add(new AuraBuffDefinitionConfig
                {
                    AuraId = aura.AuraId,
                    BuffId = aura.BuffId,
                    RadiusMeters = aura.RadiusMeters,
                    LayerMask = aura.LayerMask,
                    RelationFilter = aura.RelationFilter ?? string.Empty,
                    IncludeCaster = aura.IncludeCaster
                });
            }

            return copy;
        }

        private static int CompareBuffs(BuffDefinitionConfig left, BuffDefinitionConfig right)
        {
            return (left?.BuffId ?? 0).CompareTo(right?.BuffId ?? 0);
        }

        private static int CompareEffects(BuffEffectConfig left, BuffEffectConfig right)
        {
            return (left?.EffectId ?? 0).CompareTo(right?.EffectId ?? 0);
        }

        private static int CompareAuras(AuraBuffDefinitionConfig left, AuraBuffDefinitionConfig right)
        {
            return (left?.AuraId ?? 0).CompareTo(right?.AuraId ?? 0);
        }

        private static int ComparePresentations(
            BuffPresentationDefinitionConfig left,
            BuffPresentationDefinitionConfig right)
        {
            return (left?.BuffId ?? 0).CompareTo(right?.BuffId ?? 0);
        }

        private static void ValidatePresentation(
            BuffAuthoringValidationReport report,
            BuffPresentationConfig presentation,
            string path)
        {
            if (presentation == null)
            {
                Warning(
                    report,
                    "BUFF_PRESENTATION_NAME_KEY_MISSING",
                    path + ".nameKey",
                    "Buff presentation nameKey is recommended for localized display names.");
                Warning(
                    report,
                    "BUFF_PRESENTATION_ICON_MISSING",
                    path + ".iconResourceKey",
                    "Buff presentation iconResourceKey is recommended for badge icons.");
                return;
            }

            if (string.IsNullOrWhiteSpace(presentation.NameKey))
            {
                Warning(
                    report,
                    "BUFF_PRESENTATION_NAME_KEY_MISSING",
                    path + ".nameKey",
                    "Buff presentation nameKey is recommended for localized display names.");
            }

            if (string.IsNullOrWhiteSpace(presentation.IconResourceKey))
            {
                Warning(
                    report,
                    "BUFF_PRESENTATION_ICON_MISSING",
                    path + ".iconResourceKey",
                    "Buff presentation iconResourceKey is recommended for badge icons.");
            }

            if (!KnownDescriptionModes.Contains(presentation.DescriptionMode ?? string.Empty))
            {
                Error(
                    report,
                    "BUFF_PRESENTATION_DESCRIPTION_MODE_INVALID",
                    path + ".descriptionMode",
                    "Buff presentation descriptionMode is unsupported: " + (presentation.DescriptionMode ?? string.Empty) + ".");
            }
        }

        private static void ValidateBuffTypes(
            BuffAuthoringValidationReport report,
            List<string> types,
            string path)
        {
            if (types == null || types.Count == 0)
            {
                Error(
                    report,
                    "BUFF_TYPES_REQUIRED",
                    path,
                    "Buff requires at least one player-facing type.");
                return;
            }

            for (int i = 0; i < types.Count; i++)
            {
                string type = types[i] ?? string.Empty;
                if (!KnownBuffTypeSet.Contains(type))
                    Error(
                        report,
                        "BUFF_TYPE_INVALID",
                        path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "Buff type is unsupported: " + type + ".");
            }
        }

        private static string InferPresentationPolarity(List<string> types)
        {
            if (ContainsBuffType(types, "damage")
                || ContainsBuffType(types, "weaken")
                || ContainsBuffType(types, "control"))
            {
                return "Debuff";
            }

            if (ContainsBuffType(types, "empower")
                || ContainsBuffType(types, "restore")
                || ContainsBuffType(types, "damageReduction"))
            {
                return "Buff";
            }

            return "Neutral";
        }

        private static int DefaultPresentationPriority(string polarity)
        {
            if (string.Equals(polarity, "Debuff", StringComparison.Ordinal))
                return 200;
            if (string.Equals(polarity, "Aura", StringComparison.Ordinal))
                return 150;
            if (string.Equals(polarity, "Buff", StringComparison.Ordinal))
                return 100;
            return 0;
        }

        private static bool ContainsBuffType(List<string> types, string value)
        {
            for (int i = 0; types != null && i < types.Count; i++)
            {
                if (string.Equals(types[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static List<string> NormalizeBuffTypes(List<string> source)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sourceValues = new List<string>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                string value = source[i]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                    sourceValues.Add(value);
            }

            var result = new List<string>();
            for (int i = 0; i < KnownBuffTypes.Length; i++)
            {
                string known = KnownBuffTypes[i];
                if (sourceValues.Contains(known))
                    result.Add(known);
            }

            for (int i = 0; i < sourceValues.Count; i++)
            {
                string value = sourceValues[i];
                if (!KnownBuffTypeSet.Contains(value))
                    result.Add(value);
            }

            return result;
        }

        public static string FormatAutoDescription(
            BuffDefinitionConfig buff,
            string localeId = "zh-CN",
            int frameRate = DefaultDescriptionFrameRate)
        {
            return BuffAutoDescriptionFormatter.Format(buff, localeId, frameRate);
        }

        private static string InferDescriptionMode(BuffPresentationConfig presentation)
        {
            if (presentation == null)
                return "Auto";
            if (!string.IsNullOrWhiteSpace(presentation.DescriptionMode))
                return presentation.DescriptionMode;
            return string.IsNullOrWhiteSpace(presentation.Description) ? "Auto" : "Manual";
        }

        private static string FormatEffectsForDescription(List<BuffEffectConfig> effects, bool english)
        {
            if (effects == null || effects.Count == 0)
                return english ? "No effect" : "无效果";

            var sorted = new List<BuffEffectConfig>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null)
                    sorted.Add(effects[i]);
            }
            sorted.Sort(CompareEffects);

            string separator = english ? "; " : "；";
            var parts = new List<string>();
            for (int i = 0; i < sorted.Count; i++)
            {
                BuffEffectConfig effect = sorted[i];
                if (string.Equals(effect.Kind, "AttributeModifier", StringComparison.Ordinal))
                {
                    parts.Add(
                        FormatAttributeForDescription(effect.AttributeId, english)
                        + (english ? " " : string.Empty)
                        + (effect.AddValue >= 0 ? "+" : string.Empty)
                        + effect.AddValue.ToString(CultureInfo.InvariantCulture));
                }
                else if (string.Equals(effect.Kind, "Status", StringComparison.Ordinal))
                {
                    parts.Add((english ? "Status " : "状态 ") + effect.StatusId.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    parts.Add((english ? "Effect " : "效果 ") + effect.EffectId.ToString(CultureInfo.InvariantCulture));
                }
            }

            return string.Join(separator, parts);
        }

        private static string FormatDurationForDescription(
            long frames,
            bool isPermanent,
            bool english,
            int frameRate)
        {
            if (isPermanent)
                return english ? "permanent" : "永久";

            int effectiveFrameRate = frameRate > 0 ? frameRate : DefaultDescriptionFrameRate;
            int instantFrames = Math.Max(1, (int)Math.Ceiling(effectiveFrameRate / (double)DefaultInstantFrameRateBase));
            if (frames <= instantFrames)
                return english ? "instant" : "瞬时";

            string seconds = FormatSeconds(frames / (double)effectiveFrameRate);
            return english ? "lasts " + seconds + "s" : "持续" + seconds + "秒";
        }

        private static string FormatStackForDescription(string stackPolicy, int maxStackCount, bool english)
        {
            bool stacking = string.Equals(stackPolicy, "Stack", StringComparison.Ordinal) && maxStackCount > 1;
            if (stacking)
                return english
                    ? "up to " + maxStackCount.ToString(CultureInfo.InvariantCulture) + " stacks"
                    : "最多" + maxStackCount.ToString(CultureInfo.InvariantCulture) + "层";
            return english ? "not stackable" : "不可叠加";
        }

        private static string FormatAttributeForDescription(int attributeId, bool english)
        {
            switch (attributeId)
            {
                case 1:
                    return english ? "HP" : "生命";
                case 2:
                    return english ? "Attack" : "攻击";
                case 3:
                    return english ? "Defense" : "防御";
                case 4:
                    return english ? "Posture" : "韧性";
                default:
                    return english
                        ? "Attribute " + attributeId.ToString(CultureInfo.InvariantCulture)
                        : "属性" + attributeId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string FormatSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
                return "0";

            double rounded = Math.Round(seconds * 1000d) / 1000d;
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool IsEnglish(string localeId)
        {
            return !string.IsNullOrWhiteSpace(localeId)
                && localeId.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatEffects(List<BuffEffectConfig> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var sorted = new List<BuffEffectConfig>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null)
                    sorted.Add(effects[i]);
            }
            sorted.Sort(CompareEffects);

            var builder = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0)
                    builder.Append("; ");

                BuffEffectConfig effect = sorted[i];
                if (string.Equals(effect.Kind, "AttributeModifier", StringComparison.Ordinal))
                {
                    builder.Append(FormatAttribute(effect.AttributeId))
                        .Append(' ')
                        .Append(effect.AddValue >= 0 ? "+" : string.Empty)
                        .Append(effect.AddValue.ToString(CultureInfo.InvariantCulture));
                }
                else if (string.Equals(effect.Kind, "Status", StringComparison.Ordinal))
                {
                    builder.Append("Status(")
                        .Append(effect.StatusId.ToString(CultureInfo.InvariantCulture))
                        .Append(')');
                }
                else
                {
                    builder.Append("Effect(")
                        .Append(effect.EffectId.ToString(CultureInfo.InvariantCulture))
                        .Append(')');
                }
            }

            return builder.ToString();
        }

        private static string FormatAttribute(int attributeId)
        {
            switch (attributeId)
            {
                case 1:
                    return "HP(1)";
                case 2:
                    return "Attack(2)";
                case 3:
                    return "Defense(3)";
                case 4:
                    return "Posture(4)";
                default:
                    return "Attribute(" + attributeId.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static string BuildShortName(string value, int fallbackId)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallbackId.ToString(CultureInfo.InvariantCulture);

            var builder = new StringBuilder(4);
            bool takeNext = true;
            for (int i = 0; i < value.Length && builder.Length < 4; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c))
                {
                    takeNext = true;
                    continue;
                }

                if (takeNext || char.IsUpper(c))
                {
                    builder.Append(char.ToUpperInvariant(c));
                    takeNext = false;
                }
            }

            if (builder.Length == 0)
            {
                for (int i = 0; i < value.Length && builder.Length < 4; i++)
                {
                    char c = value[i];
                    if (char.IsLetterOrDigit(c))
                        builder.Append(char.ToUpperInvariant(c));
                }
            }

            return builder.Length > 0
                ? builder.ToString()
                : fallbackId.ToString(CultureInfo.InvariantCulture);
        }

        private static void Error(BuffAuthoringValidationReport report, string code, string path, string message)
        {
            report.Diagnostics.Add(new BuffAuthoringDiagnostic
            {
                Severity = "Error",
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }

        private static void Warning(BuffAuthoringValidationReport report, string code, string path, string message)
        {
            report.Diagnostics.Add(new BuffAuthoringDiagnostic
            {
                Severity = "Warning",
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }
}
