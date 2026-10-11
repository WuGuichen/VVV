using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Gameplay.Triggers.Config;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGameplayTriggerSceneCacheCompiler
    {
        /// <remarks>
        /// AllocByDesign. Editor/startup-only projection from level Authoring and Scene Transform
        /// facts into immutable fixed Combat sensor rows.
        /// </remarks>
        internal static bool TryCompile(
            MxLevelAuthoringDocument envelope,
            LevelWorldObjectAuthoringDocument worldObjects,
            Transform sceneRoot,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out CharacterTestGameplayTriggerSceneCacheData data,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            data = null;
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (envelope == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_CACHE_ENVELOPE_MISSING",
                    "$",
                    "Level authoring envelope is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (!CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument triggerDocument,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport extractReport))
            {
                report = extractReport;
                return false;
            }

            if (!modulePresent)
            {
                data = CreateEmptyData(envelope);
                report = LevelWorldObjectAuthoringValidationReport.Empty;
                return true;
            }

            LevelWorldObjectAuthoringValidationReport compileReport =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.ValidateAndCompile(
                    triggerDocument,
                    worldObjects,
                    out GameplayTriggerCompileResult compileResult);
            if (compileReport.HasErrors)
            {
                report = compileReport;
                return false;
            }

            int triggerCount = triggerDocument?.Entries?.Count ?? 0;
            if (triggerCount == 0)
            {
                data = CreateData(
                    envelope,
                    triggerDocument,
                    Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>(),
                    Array.Empty<
                        CharacterTestGameplayTriggerAttributeThresholdCacheRow>(),
                    0L,
                    0L,
                    0L,
                    0L);
                report = LevelWorldObjectAuthoringValidationReport.Empty;
                return true;
            }

            if (compileResult?.Succeeded != true
                || compileResult.CompiledCatalog == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_CACHE_COMPILE_RESULT_MISSING",
                    TriggerRootPath,
                    "Trigger compiler did not produce a runtime catalog plan."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }
            int regionBindingCount =
                triggerDocument?.RegionBindings?.Count ?? 0;
            if (regionBindingCount > 0 && sceneRoot == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_CACHE_SCENE_ROOT_MISSING",
                    TriggerRootPath,
                    "CharacterTest scene root is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }
            if (regionBindingCount > 0 && definitionProvider == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_CACHE_DEFINITION_PROVIDER_MISSING",
                    TriggerRootPath,
                    "World-object definition catalog is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            CharacterTestGameplayTriggerSensorCacheRow[] rows =
                BuildSensorRows(
                    triggerDocument,
                    worldObjects,
                    sceneRoot,
                    definitionProvider,
                    issues);
            CharacterTestGameplayTriggerAttributeThresholdCacheRow[]
                attributeRows = BuildAttributeThresholdRows(
                    triggerDocument,
                    issues);
            if (issues.Count > 0)
            {
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }
            if (rows.Length + attributeRows.Length
                != compileResult.CompiledCatalog.SourceRequirementCount)
            {
                issues.Add(Error(
                    "CT_TRIGGER_CACHE_SOURCE_COUNT_MISMATCH",
                    TriggerRootPath + ".regionBindings",
                    "Every compiled Trigger source must have one trusted provider row."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            long sensorCatalogHash = 0L;
            if (rows.Length > 0)
            {
                GameplayTriggerCombatSensorDescriptor[] descriptors =
                    CreateDescriptors(rows);
                if (!GameplayTriggerCombatSensorCatalog.TryCreate(
                        descriptors,
                        compileResult.CompiledCatalog,
                        out GameplayTriggerCombatSensorCatalog sensorCatalog,
                        out GameplayTriggerCombatSensorCatalogResult
                            sensorResult))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_SENSOR_" + sensorResult.ErrorCode,
                        TriggerRootPath + ".regionBindings",
                        "Combat sensor catalog rejected the projected Trigger regions."));
                    report =
                        new LevelWorldObjectAuthoringValidationReport(issues);
                    return false;
                }
                sensorCatalogHash = sensorCatalog.SensorCatalogHash;
            }

            long attributeThresholdCatalogHash = 0L;
            if (attributeRows.Length > 0)
            {
                CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
                    descriptors =
                        CreateAttributeThresholdDescriptors(attributeRows);
                if (!CharacterTestGameplayTriggerAttributeThresholdCatalog
                        .TryCreate(
                            descriptors,
                            compileResult.CompiledCatalog,
                            out
                                CharacterTestGameplayTriggerAttributeThresholdCatalog
                                attributeCatalog,
                            out string error))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_ATTRIBUTE_THRESHOLD_INVALID",
                        TriggerRootPath + ".entries",
                        error));
                    report =
                        new LevelWorldObjectAuthoringValidationReport(issues);
                    return false;
                }
                attributeThresholdCatalogHash =
                    attributeCatalog.CatalogHash;
            }

            long triggerCatalogHash =
                compileResult.CompiledCatalog.GameplayTriggerCatalogHash;
            long combinedCatalogHash =
                CharacterTestGameplayTriggerSourceCatalogHash.Compute(
                    triggerCatalogHash,
                    sensorCatalogHash,
                    attributeThresholdCatalogHash);
            data = CreateData(
                envelope,
                triggerDocument,
                rows,
                attributeRows,
                triggerCatalogHash,
                sensorCatalogHash,
                attributeThresholdCatalogHash,
                combinedCatalogHash);
            report = LevelWorldObjectAuthoringValidationReport.Empty;
            return true;
        }

        internal static GameplayTriggerCombatSensorDescriptor[] CreateDescriptors(
            IReadOnlyList<CharacterTestGameplayTriggerSensorCacheRow> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<GameplayTriggerCombatSensorDescriptor>();

            var descriptors =
                new GameplayTriggerCombatSensorDescriptor[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null)
                    throw new InvalidOperationException(
                        "Trigger sensor cache row cannot be null.");
                descriptors[i] = rows[i].ToDescriptor();
            }
            return descriptors;
        }

        internal static
            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
            CreateAttributeThresholdDescriptors(
                IReadOnlyList<
                    CharacterTestGameplayTriggerAttributeThresholdCacheRow>
                    rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return Array.Empty<
                    CharacterTestGameplayTriggerAttributeThresholdDescriptor>();
            }

            var descriptors =
                new CharacterTestGameplayTriggerAttributeThresholdDescriptor[
                    rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null)
                    throw new InvalidOperationException(
                        "Trigger attribute Threshold cache row cannot be null.");
                descriptors[i] = rows[i].ToDescriptor();
            }
            return descriptors;
        }

        private static CharacterTestGameplayTriggerSensorCacheRow[]
            BuildSensorRows(
                CharacterTestLevelGameplayTriggerAuthoringDocument triggerDocument,
                LevelWorldObjectAuthoringDocument worldObjects,
                Transform sceneRoot,
                ICharacterTestWorldObjectDefinitionProvider definitionProvider,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            int count = triggerDocument?.RegionBindings?.Count ?? 0;
            var rows =
                new CharacterTestGameplayTriggerSensorCacheRow[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    triggerDocument.RegionBindings[i];
                string path = TriggerRootPath + ".regionBindings[" + i + "]";
                LevelWorldObjectRegionDefinition region =
                    FindRegion(worldObjects, binding?.RegionStableId);
                if (binding == null || region == null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_REGION_MISSING",
                        path + ".regionStableId",
                        "Trigger region cannot be resolved."));
                    continue;
                }

                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        sceneRoot,
                        region.TransformRef,
                        out Transform transform)
                    || transform == null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_TRANSFORM_MISSING",
                        path + ".regionStableId",
                        "Trigger region Transform cannot be resolved."));
                    continue;
                }
                if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_ROTATION_UNSUPPORTED",
                        path + ".regionStableId",
                        "Combat AABB Trigger regions require identity world rotation."));
                    continue;
                }

                if (!TryResolveHalfExtents(
                        region,
                        transform,
                        definitionProvider,
                        out Vector3 halfExtents))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_EXTENTS_INVALID",
                        path + ".regionStableId",
                        "Trigger region bounds are missing or invalid."));
                    continue;
                }

                FixVector3 center =
                    CharacterTestCombatPhysicsSceneBridge.ToFixVector3(
                        transform.position);
                FixVector3 fixedHalfExtents =
                    CharacterTestCombatPhysicsSceneBridge.ToFixVector3(
                        halfExtents);
                FixVector3 min = center - fixedHalfExtents;
                FixVector3 max = center + fixedHalfExtents;
                GameplayComponentTargetFilter subjectFilter;
                try
                {
                    subjectFilter = binding.SubjectFilter.ToRuntimeFilter();
                }
                catch (ArgumentException exception)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_SUBJECT_FILTER_INVALID",
                        path + ".subjectFilter",
                        exception.Message));
                    continue;
                }
                rows[i] = new CharacterTestGameplayTriggerSensorCacheRow(
                    binding.SourceBindingId,
                    binding.RegionStableId,
                    binding.ProviderId,
                    binding.SourceBindingId,
                    binding.LayerMask,
                    binding.MaxQueryHits,
                    binding.MaxMappedSubjects,
                    subjectFilter,
                    in min,
                    in max);
            }

            Array.Sort(
                rows,
                (left, right) =>
                    (left?.SourceBindingId ?? int.MaxValue).CompareTo(
                        right?.SourceBindingId ?? int.MaxValue));
            return rows;
        }

        private static
            CharacterTestGameplayTriggerAttributeThresholdCacheRow[]
            BuildAttributeThresholdRows(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    triggerDocument,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            int count = 0;
            for (int i = 0; i < triggerDocument.Entries.Count; i++)
            {
                if (CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(
                            triggerDocument.Entries[i]?.TemplateId))
                {
                    count++;
                }
            }

            var rows =
                new CharacterTestGameplayTriggerAttributeThresholdCacheRow[
                    count];
            int writeIndex = 0;
            for (int i = 0; i < triggerDocument.Entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    triggerDocument.Entries[i];
                if (!CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry?.TemplateId))
                {
                    continue;
                }
                if (entry.SourceBindingId <= 0)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CACHE_ATTRIBUTE_THRESHOLD_BINDING_INVALID",
                        TriggerRootPath + ".entries[" + i
                        + "].sourceBindingId",
                        "Player HP Threshold source binding must be positive."));
                    continue;
                }
                rows[writeIndex++] =
                    new CharacterTestGameplayTriggerAttributeThresholdCacheRow(
                        entry.SourceBindingId,
                        CharacterTestLevelGameplayTriggerAuthoringAdapter
                            .PlayerAttributeThresholdProviderId,
                        CharacterTestCharacterAttributeIds.Hp);
            }
            Array.Sort(
                rows,
                (left, right) =>
                    (left?.SourceBindingId ?? int.MaxValue).CompareTo(
                        right?.SourceBindingId ?? int.MaxValue));
            return rows;
        }

        private static bool TryResolveHalfExtents(
            LevelWorldObjectRegionDefinition region,
            Transform transform,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out Vector3 halfExtents)
        {
            if (region.ScaleSource == LevelWorldObjectScaleSource.Definition)
            {
                if (!definitionProvider.TryGetDefinition(
                        region.DefinitionId,
                        out CharacterTestWorldObjectDefinition definition))
                {
                    halfExtents = default;
                    return false;
                }

                halfExtents = new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentZMillimeters));
            }
            else
            {
                Vector3 scale = transform.lossyScale;
                halfExtents = new Vector3(
                    Mathf.Abs(scale.x) * 0.5f,
                    Mathf.Abs(scale.y) * 0.5f,
                    Mathf.Abs(scale.z) * 0.5f);
            }

            return IsFinitePositive(halfExtents.x)
                && IsFinitePositive(halfExtents.y)
                && IsFinitePositive(halfExtents.z);
        }

        private static LevelWorldObjectRegionDefinition FindRegion(
            LevelWorldObjectAuthoringDocument worldObjects,
            string stableId)
        {
            if (worldObjects?.Regions == null
                || string.IsNullOrWhiteSpace(stableId))
            {
                return null;
            }

            for (int i = 0; i < worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition candidate =
                    worldObjects.Regions[i];
                if (candidate != null
                    && string.Equals(
                        candidate.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static CharacterTestGameplayTriggerSceneCacheData CreateEmptyData(
            MxLevelAuthoringDocument envelope)
        {
            return new CharacterTestGameplayTriggerSceneCacheData
            {
                LevelFormat = envelope.Format,
                LevelSchemaVersion = envelope.SchemaVersion,
                ModuleSchemaVersion =
                    CharacterTestLevelGameplayTriggerAuthoringAdapter
                        .ModuleSchemaVersion,
                AuthoringContentHash =
                    MxLevelAuthoringJson.ComputeContentHash(envelope),
                LastAppliedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static CharacterTestGameplayTriggerSceneCacheData CreateData(
            MxLevelAuthoringDocument envelope,
            CharacterTestLevelGameplayTriggerAuthoringDocument triggerDocument,
            CharacterTestGameplayTriggerSensorCacheRow[] rows,
            CharacterTestGameplayTriggerAttributeThresholdCacheRow[]
                attributeRows,
            long triggerCatalogHash,
            long sensorCatalogHash,
            long attributeThresholdCatalogHash,
            long combinedCatalogHash)
        {
            MxLevelAuthoringDocument triggerEnvelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    new MxLevelAuthoringDocument
                    {
                        LevelId = envelope.LevelId,
                        ScenePath = envelope.ScenePath
                    },
                    triggerDocument);
            string canonicalJson =
                MxLevelAuthoringJson.SaveToJson(triggerEnvelope);
            return new CharacterTestGameplayTriggerSceneCacheData
            {
                LevelFormat = envelope.Format,
                LevelSchemaVersion = envelope.SchemaVersion,
                ModuleSchemaVersion =
                    CharacterTestLevelGameplayTriggerAuthoringAdapter
                        .ModuleSchemaVersion,
                TriggerEntryCount = triggerDocument?.Entries?.Count ?? 0,
                AuthoringContentHash =
                    MxLevelAuthoringJson.ComputeContentHash(envelope),
                TriggerSourceHash =
                    MxLevelAuthoringJson.ComputeContentHash(triggerEnvelope),
                CanonicalTriggerEnvelopeJson = canonicalJson,
                SensorRows = rows
                    ?? Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>(),
                AttributeThresholdRows = attributeRows
                    ?? Array.Empty<
                        CharacterTestGameplayTriggerAttributeThresholdCacheRow>(),
                TriggerCatalogHash = triggerCatalogHash,
                SensorCatalogHash = sensorCatalogHash,
                AttributeThresholdCatalogHash =
                    attributeThresholdCatalogHash,
                CombinedCatalogHash = combinedCatalogHash,
                LastAppliedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > 0f;
        }

        private static LevelWorldObjectAuthoringIssue Error(
            string code,
            string path,
            string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }

        private static string TriggerRootPath =>
            "$.modules."
            + CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId
            + ".payload";
    }
}
