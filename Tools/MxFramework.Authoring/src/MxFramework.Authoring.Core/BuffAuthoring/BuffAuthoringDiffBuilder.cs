using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public static class BuffAuthoringDiffBuilder
    {
        public static BuffAuthoringDiffReport BuildAuthoringDiff(
            BuffAuthoringDocument saved,
            BuffAuthoringDocument draft)
        {
            BuffAuthoringDocument left = NormalizeClone(saved);
            BuffAuthoringDocument right = NormalizeClone(draft);
            var builder = new DiffAccumulator("Authoring");

            CompareValue(builder, "AuthoringDocument", "Package", left.PackageId, "Package", string.Empty, "schemaVersion", "schemaVersion", left.SchemaVersion, right.SchemaVersion);
            CompareValue(builder, "AuthoringDocument", "Package", left.PackageId, "Package", string.Empty, "packageId", "packageId", left.PackageId, right.PackageId);
            CompareBuffs(builder, "AuthoringDocument", left.Buffs, right.Buffs, "buffs");
            CompareAuras(builder, "AuthoringDocument", left.Auras, right.Auras, "auras");
            return builder.ToReport();
        }

        public static BuffAuthoringDiffReport BuildGeneratedDiff(
            BuffDefinitionCatalogDocument currentBuffDefinitions,
            BuffPresentationDefinitionCatalogDocument currentPresentationDefinitions,
            AuraBuffDefinitionCatalogDocument currentAuraDefinitions,
            BuffAuthoringCompileResult compiled)
        {
            compiled ??= new BuffAuthoringCompileResult();
            var builder = new DiffAccumulator("Generated");

            CompareGeneratedBuffCatalog(
                builder,
                "BuffDefinitions",
                currentBuffDefinitions ?? new BuffDefinitionCatalogDocument(),
                compiled.BuffDefinitions ?? new BuffDefinitionCatalogDocument());
            CompareGeneratedPresentationCatalog(
                builder,
                "BuffPresentationDefinitions",
                currentPresentationDefinitions ?? new BuffPresentationDefinitionCatalogDocument(),
                compiled.BuffPresentationDefinitions ?? new BuffPresentationDefinitionCatalogDocument());
            CompareGeneratedAuraCatalog(
                builder,
                "AuraBuffDefinitions",
                currentAuraDefinitions ?? new AuraBuffDefinitionCatalogDocument(),
                compiled.AuraBuffDefinitions ?? new AuraBuffDefinitionCatalogDocument());

            return builder.ToReport();
        }

        private static BuffAuthoringDocument NormalizeClone(BuffAuthoringDocument document)
        {
            if (document == null)
                document = new BuffAuthoringDocument();
            string json = JsonSerializer.Serialize(document);
            BuffAuthoringDocument clone = JsonSerializer.Deserialize<BuffAuthoringDocument>(json) ?? new BuffAuthoringDocument();
            BuffAuthoringCompiler.Validate(clone);
            return clone;
        }

        private static void CompareBuffs(
            DiffAccumulator builder,
            string artifactKind,
            List<BuffDefinitionConfig> saved,
            List<BuffDefinitionConfig> draft,
            string listPath)
        {
            var savedMap = BuildBuffMap(saved);
            var draftMap = BuildBuffMap(draft);
            foreach (KeyValuePair<string, Indexed<BuffDefinitionConfig>> item in savedMap)
            {
                if (!draftMap.ContainsKey(item.Key))
                {
                    BuffDefinitionConfig buff = item.Value.Value;
                    builder.Add(
                        artifactKind,
                        "Buff",
                        BuffOwnerId(buff),
                        BuffOwnerLabel(buff),
                        listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Removed",
                        "definition",
                        listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]",
                        Summary(buff),
                        string.Empty);
                }
            }

            foreach (KeyValuePair<string, Indexed<BuffDefinitionConfig>> item in draftMap)
            {
                BuffDefinitionConfig draftBuff = item.Value.Value;
                string ownerPath = listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                if (!savedMap.TryGetValue(item.Key, out Indexed<BuffDefinitionConfig> savedItem))
                {
                    builder.Add(
                        artifactKind,
                        "Buff",
                        BuffOwnerId(draftBuff),
                        BuffOwnerLabel(draftBuff),
                        ownerPath,
                        "Added",
                        "definition",
                        ownerPath,
                        string.Empty,
                        Summary(draftBuff));
                    continue;
                }

                CompareBuff(builder, artifactKind, savedItem.Value, draftBuff, ownerPath);
            }
        }

        private static void CompareBuff(
            DiffAccumulator builder,
            string artifactKind,
            BuffDefinitionConfig saved,
            BuffDefinitionConfig draft,
            string ownerPath)
        {
            string ownerId = BuffOwnerId(draft);
            string ownerLabel = BuffOwnerLabel(draft);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "buffId", ownerPath + ".buffId", saved.BuffId, draft.BuffId);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "stableId", ownerPath + ".stableId", saved.StableId, draft.StableId);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "displayName", ownerPath + ".displayName", saved.DisplayName, draft.DisplayName);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "types", ownerPath + ".types", FormatStringList(saved.Types), FormatStringList(draft.Types));
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "defaultDurationFrames", ownerPath + ".defaultDurationFrames", saved.DefaultDurationFrames, draft.DefaultDurationFrames);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "maxStackCount", ownerPath + ".maxStackCount", saved.MaxStackCount, draft.MaxStackCount);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "applyStackCount", ownerPath + ".applyStackCount", saved.ApplyStackCount, draft.ApplyStackCount);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "stackPolicy", ownerPath + ".stackPolicy", saved.StackPolicy, draft.StackPolicy);
            CompareValue(builder, artifactKind, "Buff", ownerId, ownerLabel, ownerPath, "isPermanent", ownerPath + ".isPermanent", saved.IsPermanent, draft.IsPermanent);
            CompareEffects(builder, artifactKind, saved.Effects, draft.Effects, ownerPath + ".effects", ownerId, ownerLabel);
            ComparePresentation(builder, artifactKind, saved.Presentation, draft.Presentation, ownerPath + ".presentation", ownerId, ownerLabel);
        }

        private static void CompareEffects(
            DiffAccumulator builder,
            string artifactKind,
            List<BuffEffectConfig> saved,
            List<BuffEffectConfig> draft,
            string listPath,
            string buffOwnerId,
            string buffOwnerLabel)
        {
            var savedMap = BuildEffectMap(saved);
            var draftMap = BuildEffectMap(draft);
            foreach (KeyValuePair<string, Indexed<BuffEffectConfig>> item in savedMap)
            {
                if (!draftMap.ContainsKey(item.Key))
                {
                    string path = listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                    BuffEffectConfig effect = item.Value.Value;
                    builder.Add(artifactKind, "Effect", buffOwnerId + ":" + effect.EffectId.ToString(CultureInfo.InvariantCulture), buffOwnerLabel + " / effect " + effect.EffectId.ToString(CultureInfo.InvariantCulture), path, "Removed", "definition", path, Summary(effect), string.Empty);
                }
            }

            foreach (KeyValuePair<string, Indexed<BuffEffectConfig>> item in draftMap)
            {
                BuffEffectConfig draftEffect = item.Value.Value;
                string path = listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                string ownerId = buffOwnerId + ":" + draftEffect.EffectId.ToString(CultureInfo.InvariantCulture);
                string ownerLabel = buffOwnerLabel + " / effect " + draftEffect.EffectId.ToString(CultureInfo.InvariantCulture);
                if (!savedMap.TryGetValue(item.Key, out Indexed<BuffEffectConfig> savedItem))
                {
                    builder.Add(artifactKind, "Effect", ownerId, ownerLabel, path, "Added", "definition", path, string.Empty, Summary(draftEffect));
                    continue;
                }

                BuffEffectConfig savedEffect = savedItem.Value;
                CompareValue(builder, artifactKind, "Effect", ownerId, ownerLabel, path, "effectId", path + ".effectId", savedEffect.EffectId, draftEffect.EffectId);
                CompareValue(builder, artifactKind, "Effect", ownerId, ownerLabel, path, "kind", path + ".kind", savedEffect.Kind, draftEffect.Kind);
                CompareValue(builder, artifactKind, "Effect", ownerId, ownerLabel, path, "attributeId", path + ".attributeId", savedEffect.AttributeId, draftEffect.AttributeId);
                CompareValue(builder, artifactKind, "Effect", ownerId, ownerLabel, path, "addValue", path + ".addValue", savedEffect.AddValue, draftEffect.AddValue);
                CompareValue(builder, artifactKind, "Effect", ownerId, ownerLabel, path, "statusId", path + ".statusId", savedEffect.StatusId, draftEffect.StatusId);
            }
        }

        private static void ComparePresentation(
            DiffAccumulator builder,
            string artifactKind,
            BuffPresentationConfig saved,
            BuffPresentationConfig draft,
            string path,
            string buffOwnerId,
            string buffOwnerLabel)
        {
            if (saved == null && draft == null)
                return;
            if (saved == null)
            {
                builder.Add(artifactKind, "Presentation", buffOwnerId + ":presentation", buffOwnerLabel + " / presentation", path, "Added", "definition", path, string.Empty, Summary(draft));
                return;
            }

            if (draft == null)
            {
                builder.Add(artifactKind, "Presentation", buffOwnerId + ":presentation", buffOwnerLabel + " / presentation", path, "Removed", "definition", path, Summary(saved), string.Empty);
                return;
            }

            string ownerId = buffOwnerId + ":presentation";
            string ownerLabel = buffOwnerLabel + " / presentation";
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "nameKey", path + ".nameKey", saved.NameKey, draft.NameKey);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "shortName", path + ".shortName", saved.ShortName, draft.ShortName);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "descriptionMode", path + ".descriptionMode", saved.DescriptionMode, draft.DescriptionMode);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "description", path + ".description", saved.Description, draft.Description);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "iconResourceKey", path + ".iconResourceKey", saved.IconResourceKey, draft.IconResourceKey);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "priority", path + ".priority", saved.Priority, draft.Priority);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "showOnPlayerHud", path + ".showOnPlayerHud", saved.ShowOnPlayerHud, draft.ShowOnPlayerHud);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "showOnTargetHud", path + ".showOnTargetHud", saved.ShowOnTargetHud, draft.ShowOnTargetHud);
            CompareValue(builder, artifactKind, "Presentation", ownerId, ownerLabel, path, "showOnHeadPad", path + ".showOnHeadPad", saved.ShowOnHeadPad, draft.ShowOnHeadPad);
        }

        private static void CompareAuras(
            DiffAccumulator builder,
            string artifactKind,
            List<AuraBuffDefinitionConfig> saved,
            List<AuraBuffDefinitionConfig> draft,
            string listPath)
        {
            var savedMap = BuildAuraMap(saved);
            var draftMap = BuildAuraMap(draft);
            foreach (KeyValuePair<string, Indexed<AuraBuffDefinitionConfig>> item in savedMap)
            {
                if (!draftMap.ContainsKey(item.Key))
                {
                    string path = listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                    builder.Add(artifactKind, "Aura", AuraOwnerId(item.Value.Value), AuraOwnerLabel(item.Value.Value), path, "Removed", "definition", path, Summary(item.Value.Value), string.Empty);
                }
            }

            foreach (KeyValuePair<string, Indexed<AuraBuffDefinitionConfig>> item in draftMap)
            {
                AuraBuffDefinitionConfig draftAura = item.Value.Value;
                string path = listPath + "[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                if (!savedMap.TryGetValue(item.Key, out Indexed<AuraBuffDefinitionConfig> savedItem))
                {
                    builder.Add(artifactKind, "Aura", AuraOwnerId(draftAura), AuraOwnerLabel(draftAura), path, "Added", "definition", path, string.Empty, Summary(draftAura));
                    continue;
                }

                CompareAura(builder, artifactKind, savedItem.Value, draftAura, path);
            }
        }

        private static void CompareAura(
            DiffAccumulator builder,
            string artifactKind,
            AuraBuffDefinitionConfig saved,
            AuraBuffDefinitionConfig draft,
            string ownerPath)
        {
            string ownerId = AuraOwnerId(draft);
            string ownerLabel = AuraOwnerLabel(draft);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "auraId", ownerPath + ".auraId", saved.AuraId, draft.AuraId);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "buffId", ownerPath + ".buffId", saved.BuffId, draft.BuffId);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "radiusMeters", ownerPath + ".radiusMeters", saved.RadiusMeters, draft.RadiusMeters);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "layerMask", ownerPath + ".layerMask", saved.LayerMask, draft.LayerMask);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "relationFilter", ownerPath + ".relationFilter", saved.RelationFilter, draft.RelationFilter);
            CompareValue(builder, artifactKind, "Aura", ownerId, ownerLabel, ownerPath, "includeCaster", ownerPath + ".includeCaster", saved.IncludeCaster, draft.IncludeCaster);
        }

        private static void CompareGeneratedBuffCatalog(
            DiffAccumulator builder,
            string artifactKind,
            BuffDefinitionCatalogDocument current,
            BuffDefinitionCatalogDocument compiled)
        {
            CompareValue(builder, artifactKind, "Artifact", artifactKind, artifactKind, string.Empty, "packageId", "packageId", current.PackageId, compiled.PackageId);
            CompareBuffs(builder, artifactKind, current.Definitions, compiled.Definitions, "definitions");
        }

        private static void CompareGeneratedPresentationCatalog(
            DiffAccumulator builder,
            string artifactKind,
            BuffPresentationDefinitionCatalogDocument current,
            BuffPresentationDefinitionCatalogDocument compiled)
        {
            CompareValue(builder, artifactKind, "Artifact", artifactKind, artifactKind, string.Empty, "packageId", "packageId", current.PackageId, compiled.PackageId);
            var currentMap = BuildPresentationMap(current.Definitions);
            var compiledMap = BuildPresentationMap(compiled.Definitions);
            foreach (KeyValuePair<string, Indexed<BuffPresentationDefinitionConfig>> item in currentMap)
            {
                if (!compiledMap.ContainsKey(item.Key))
                {
                    string path = "definitions[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                    builder.Add(artifactKind, "PresentationDefinition", PresentationOwnerId(item.Value.Value), PresentationOwnerLabel(item.Value.Value), path, "Removed", "definition", path, Summary(item.Value.Value), string.Empty);
                }
            }

            foreach (KeyValuePair<string, Indexed<BuffPresentationDefinitionConfig>> item in compiledMap)
            {
                BuffPresentationDefinitionConfig draft = item.Value.Value;
                string path = "definitions[" + item.Value.Index.ToString(CultureInfo.InvariantCulture) + "]";
                string ownerId = PresentationOwnerId(draft);
                string ownerLabel = PresentationOwnerLabel(draft);
                if (!currentMap.TryGetValue(item.Key, out Indexed<BuffPresentationDefinitionConfig> savedItem))
                {
                    builder.Add(artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "Added", "definition", path, string.Empty, Summary(draft));
                    continue;
                }

                BuffPresentationDefinitionConfig saved = savedItem.Value;
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "buffId", path + ".buffId", saved.BuffId, draft.BuffId);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "stableId", path + ".stableId", saved.StableId, draft.StableId);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "displayName", path + ".displayName", saved.DisplayName, draft.DisplayName);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "nameKey", path + ".nameKey", saved.NameKey, draft.NameKey);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "shortName", path + ".shortName", saved.ShortName, draft.ShortName);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "descriptionMode", path + ".descriptionMode", saved.DescriptionMode, draft.DescriptionMode);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "description", path + ".description", saved.Description, draft.Description);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "iconResourceKey", path + ".iconResourceKey", saved.IconResourceKey, draft.IconResourceKey);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "polarity", path + ".polarity", saved.Polarity, draft.Polarity);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "priority", path + ".priority", saved.Priority, draft.Priority);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "showOnPlayerHud", path + ".showOnPlayerHud", saved.ShowOnPlayerHud, draft.ShowOnPlayerHud);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "showOnTargetHud", path + ".showOnTargetHud", saved.ShowOnTargetHud, draft.ShowOnTargetHud);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "showOnHeadPad", path + ".showOnHeadPad", saved.ShowOnHeadPad, draft.ShowOnHeadPad);
                CompareValue(builder, artifactKind, "PresentationDefinition", ownerId, ownerLabel, path, "effectSummary", path + ".effectSummary", saved.EffectSummary, draft.EffectSummary);
            }
        }

        private static void CompareGeneratedAuraCatalog(
            DiffAccumulator builder,
            string artifactKind,
            AuraBuffDefinitionCatalogDocument current,
            AuraBuffDefinitionCatalogDocument compiled)
        {
            CompareValue(builder, artifactKind, "Artifact", artifactKind, artifactKind, string.Empty, "packageId", "packageId", current.PackageId, compiled.PackageId);
            CompareAuras(builder, artifactKind, current.Definitions, compiled.Definitions, "definitions");
        }

        private static void CompareValue(
            DiffAccumulator builder,
            string artifactKind,
            string ownerKind,
            string ownerId,
            string ownerLabel,
            string ownerPath,
            string field,
            string path,
            object before,
            object after)
        {
            if (object.Equals(before, after))
                return;

            builder.Add(
                artifactKind,
                ownerKind,
                ownerId,
                ownerLabel,
                ownerPath,
                "Changed",
                field,
                path,
                FormatValue(before),
                FormatValue(after));
        }

        private static Dictionary<string, Indexed<BuffDefinitionConfig>> BuildBuffMap(List<BuffDefinitionConfig> values)
        {
            var result = new Dictionary<string, Indexed<BuffDefinitionConfig>>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                BuffDefinitionConfig value = values[i];
                if (value == null)
                    continue;
                result[BuffKey(value, i)] = new Indexed<BuffDefinitionConfig>(i, value);
            }
            return result;
        }

        private static Dictionary<string, Indexed<BuffEffectConfig>> BuildEffectMap(List<BuffEffectConfig> values)
        {
            var result = new Dictionary<string, Indexed<BuffEffectConfig>>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                BuffEffectConfig value = values[i];
                if (value == null)
                    continue;
                result[EffectKey(value, i)] = new Indexed<BuffEffectConfig>(i, value);
            }
            return result;
        }

        private static Dictionary<string, Indexed<AuraBuffDefinitionConfig>> BuildAuraMap(List<AuraBuffDefinitionConfig> values)
        {
            var result = new Dictionary<string, Indexed<AuraBuffDefinitionConfig>>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                AuraBuffDefinitionConfig value = values[i];
                if (value == null)
                    continue;
                result[AuraKey(value, i)] = new Indexed<AuraBuffDefinitionConfig>(i, value);
            }
            return result;
        }

        private static Dictionary<string, Indexed<BuffPresentationDefinitionConfig>> BuildPresentationMap(
            List<BuffPresentationDefinitionConfig> values)
        {
            var result = new Dictionary<string, Indexed<BuffPresentationDefinitionConfig>>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                BuffPresentationDefinitionConfig value = values[i];
                if (value == null)
                    continue;
                result[PresentationKey(value, i)] = new Indexed<BuffPresentationDefinitionConfig>(i, value);
            }
            return result;
        }

        private static string BuffKey(BuffDefinitionConfig value, int index)
        {
            if (value.BuffId > 0)
                return "id:" + value.BuffId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(value.StableId))
                return "stable:" + value.StableId;
            return "index:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string EffectKey(BuffEffectConfig value, int index)
        {
            return value.EffectId > 0
                ? "id:" + value.EffectId.ToString(CultureInfo.InvariantCulture)
                : "index:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string AuraKey(AuraBuffDefinitionConfig value, int index)
        {
            return value.AuraId > 0
                ? "id:" + value.AuraId.ToString(CultureInfo.InvariantCulture)
                : "index:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string PresentationKey(BuffPresentationDefinitionConfig value, int index)
        {
            return value.BuffId > 0
                ? "id:" + value.BuffId.ToString(CultureInfo.InvariantCulture)
                : "index:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuffOwnerId(BuffDefinitionConfig value)
        {
            if (value == null)
                return string.Empty;
            return value.BuffId > 0
                ? value.BuffId.ToString(CultureInfo.InvariantCulture)
                : value.StableId ?? string.Empty;
        }

        private static string BuffOwnerLabel(BuffDefinitionConfig value)
        {
            if (value == null)
                return "Buff";
            string id = value.BuffId > 0 ? value.BuffId.ToString(CultureInfo.InvariantCulture) : "Buff";
            string name = !string.IsNullOrWhiteSpace(value.DisplayName) ? value.DisplayName : value.StableId;
            return string.IsNullOrWhiteSpace(name) ? id : id + " · " + name;
        }

        private static string AuraOwnerId(AuraBuffDefinitionConfig value)
        {
            return value != null && value.AuraId > 0
                ? value.AuraId.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string AuraOwnerLabel(AuraBuffDefinitionConfig value)
        {
            if (value == null)
                return "Aura";
            return value.AuraId.ToString(CultureInfo.InvariantCulture)
                + " · Aura -> Buff "
                + value.BuffId.ToString(CultureInfo.InvariantCulture);
        }

        private static string PresentationOwnerId(BuffPresentationDefinitionConfig value)
        {
            return value != null && value.BuffId > 0
                ? value.BuffId.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string PresentationOwnerLabel(BuffPresentationDefinitionConfig value)
        {
            if (value == null)
                return "Presentation";
            string name = !string.IsNullOrWhiteSpace(value.DisplayName) ? value.DisplayName : value.StableId;
            return value.BuffId.ToString(CultureInfo.InvariantCulture)
                + " · "
                + (string.IsNullOrWhiteSpace(name) ? "Presentation" : name);
        }

        private static string Summary(BuffDefinitionConfig value)
        {
            if (value == null)
                return string.Empty;
            return BuffOwnerLabel(value);
        }

        private static string Summary(BuffEffectConfig value)
        {
            if (value == null)
                return string.Empty;
            return "effectId=" + value.EffectId.ToString(CultureInfo.InvariantCulture)
                + ", kind=" + (value.Kind ?? string.Empty);
        }

        private static string Summary(BuffPresentationConfig value)
        {
            if (value == null)
                return string.Empty;
            return "shortName=" + (value.ShortName ?? string.Empty)
                + ", nameKey=" + (value.NameKey ?? string.Empty)
                + ", descriptionMode=" + (value.DescriptionMode ?? string.Empty);
        }

        private static string Summary(AuraBuffDefinitionConfig value)
        {
            if (value == null)
                return string.Empty;
            return AuraOwnerLabel(value);
        }

        private static string Summary(BuffPresentationDefinitionConfig value)
        {
            if (value == null)
                return string.Empty;
            return PresentationOwnerLabel(value)
                + " nameKey=" + (value.NameKey ?? string.Empty)
                + " descriptionMode=" + (value.DescriptionMode ?? string.Empty);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;
            if (value is float floatValue)
                return floatValue.ToString("0.###", CultureInfo.InvariantCulture);
            if (value is double doubleValue)
                return doubleValue.ToString("0.###", CultureInfo.InvariantCulture);
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }

        private static string FormatStringList(List<string> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;
            return string.Join(", ", values);
        }

        private readonly struct Indexed<T>
        {
            public Indexed(int index, T value)
            {
                Index = index;
                Value = value;
            }

            public int Index { get; }
            public T Value { get; }
        }

        private sealed class DiffAccumulator
        {
            private readonly string _scope;
            private readonly List<BuffAuthoringDiffGroup> _groups = new List<BuffAuthoringDiffGroup>();
            private readonly Dictionary<string, BuffAuthoringDiffGroup> _byKey = new Dictionary<string, BuffAuthoringDiffGroup>(StringComparer.Ordinal);
            private int _changeCount;

            public DiffAccumulator(string scope)
            {
                _scope = scope ?? string.Empty;
            }

            public void Add(
                string artifactKind,
                string ownerKind,
                string ownerId,
                string ownerLabel,
                string ownerPath,
                string changeKind,
                string field,
                string path,
                string before,
                string after)
            {
                artifactKind ??= string.Empty;
                ownerKind ??= string.Empty;
                ownerId ??= string.Empty;
                ownerLabel ??= string.Empty;
                ownerPath ??= string.Empty;
                string key = artifactKind + "|" + ownerKind + "|" + ownerId + "|" + ownerPath;
                if (!_byKey.TryGetValue(key, out BuffAuthoringDiffGroup group))
                {
                    group = new BuffAuthoringDiffGroup
                    {
                        ArtifactKind = artifactKind,
                        OwnerKind = ownerKind,
                        OwnerId = ownerId,
                        OwnerLabel = ownerLabel,
                        Path = ownerPath
                    };
                    _byKey.Add(key, group);
                    _groups.Add(group);
                }

                group.Changes.Add(new BuffAuthoringDiffEntry
                {
                    ArtifactKind = artifactKind,
                    OwnerKind = ownerKind,
                    OwnerId = ownerId,
                    OwnerLabel = ownerLabel,
                    ChangeKind = changeKind ?? "Changed",
                    Field = field ?? string.Empty,
                    Path = path ?? string.Empty,
                    BeforeValue = before ?? string.Empty,
                    AfterValue = after ?? string.Empty
                });
                _changeCount++;
            }

            public BuffAuthoringDiffReport ToReport()
            {
                return new BuffAuthoringDiffReport
                {
                    Scope = _scope,
                    HasChanges = _changeCount > 0,
                    ChangeCount = _changeCount,
                    Groups = _groups
                };
            }
        }
    }

    public sealed class BuffAuthoringDiffReport
    {
        public string Scope { get; set; } = string.Empty;
        public bool HasChanges { get; set; }
        public int ChangeCount { get; set; }
        public List<BuffAuthoringDiffGroup> Groups { get; set; } = new List<BuffAuthoringDiffGroup>();
    }

    public sealed class BuffAuthoringDiffGroup
    {
        public string ArtifactKind { get; set; } = string.Empty;
        public string OwnerKind { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerLabel { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public List<BuffAuthoringDiffEntry> Changes { get; set; } = new List<BuffAuthoringDiffEntry>();
    }

    public sealed class BuffAuthoringDiffEntry
    {
        public string ArtifactKind { get; set; } = string.Empty;
        public string OwnerKind { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerLabel { get; set; } = string.Empty;
        public string ChangeKind { get; set; } = "Changed";
        public string Field { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string BeforeValue { get; set; } = string.Empty;
        public string AfterValue { get; set; } = string.Empty;
    }
}
