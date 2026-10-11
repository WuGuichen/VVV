using System;
using System.Collections.Generic;
using MxFramework.Combat.Levels;
using MxFramework.Config.Levels;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestWorldObjectAuthoringAdapter
    {
        public const string DefaultLevelId = "character_test_scene";
        public const string DefaultScenePath = "Assets/Scenes/CharacterTestScene.unity";
        public const string DefaultDocumentPath =
            "Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json";
        public const string WorldObjectsModuleId =
            MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId;
        public const int WorldObjectsModuleSchemaVersion = 1;

        public const string DefinitionNotFound = "DefinitionNotFound";
        public const string DefinitionRoleMismatch = "DefinitionRoleMismatch";
        public const string TransformNotFound = "TransformNotFound";
        public const string RootPathMismatch = "RootPathMismatch";
        public const string MissingWorldObjectsModule = "MissingWorldObjectsModule";
        public const string InvalidWorldObjectsModulePayload = "InvalidWorldObjectsModulePayload";

        public static MxLevelAuthoringDocument CreateEnvelopeFromSource(
            CharacterTestWorldObjectSceneSource source,
            string levelId = null,
            string scenePath = null)
        {
            LevelWorldObjectAuthoringDocument worldObjects =
                CreateDocumentFromSource(source, levelId, scenePath);
            return CreateEnvelopeFromDocument(worldObjects, source == null ? null : source.transform);
        }

        public static MxLevelAuthoringDocument CreateEnvelopeFromGameManager(
            GameManager gameManager,
            string levelId = null,
            string scenePath = null)
        {
            if (gameManager == null)
                throw new ArgumentNullException(nameof(gameManager));

            return CreateEnvelopeFromCache(
                gameManager.WorldObjectSceneCache,
                gameManager.WorldObjectSceneRoot,
                levelId,
                scenePath);
        }

        public static MxLevelAuthoringDocument CreateEnvelopeFromCache(
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            string levelId = null,
            string scenePath = null)
        {
            LevelWorldObjectAuthoringDocument worldObjects =
                CreateDocumentFromCache(cache, root, levelId, scenePath);
            return CreateEnvelopeFromDocument(worldObjects, root);
        }

        public static MxLevelAuthoringDocument CreateEnvelopeFromDocument(
            LevelWorldObjectAuthoringDocument document,
            Transform rootTransform = null)
        {
            return UpsertWorldObjectsModule(new MxLevelAuthoringDocument(), document, rootTransform);
        }

        public static MxLevelAuthoringDocument UpsertWorldObjectsModule(
            MxLevelAuthoringDocument envelope,
            LevelWorldObjectAuthoringDocument document,
            Transform rootTransform = null)
        {
            LevelWorldObjectAuthoringDocument normalized =
                document == null ? new LevelWorldObjectAuthoringDocument() : document.Normalize();
            LevelTransformReference rootRef = rootTransform == null
                ? LevelTransformReference.FromPath(normalized.RootPath)
                : new LevelTransformReference(rootTransform.name, TryCreateGlobalObjectId(rootTransform));

            envelope ??= new MxLevelAuthoringDocument();
            envelope = envelope.Normalize();
            envelope.LevelId = string.IsNullOrWhiteSpace(normalized.LevelId)
                ? DefaultLevelId
                : normalized.LevelId;
            envelope.ScenePath = string.IsNullOrWhiteSpace(normalized.ScenePath)
                ? DefaultScenePath
                : normalized.ScenePath;
            envelope.RootPath = string.IsNullOrWhiteSpace(rootRef.Path) ? normalized.RootPath : rootRef.Path;
            envelope.RootTransformRef = rootRef;
            envelope.UpsertModulePayload(
                WorldObjectsModuleId,
                WorldObjectsModuleSchemaVersion,
                normalized,
                "CharacterTest World Objects");
            return envelope.Normalize();
        }

        public static bool TryExtractWorldObjectsDocument(
            MxLevelAuthoringDocument envelope,
            out LevelWorldObjectAuthoringDocument document,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            document = null;
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (envelope == null)
            {
                issues.Add(Error(
                    InvalidWorldObjectsModulePayload,
                    "document",
                    "Level authoring envelope is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            MxLevelAuthoringDocument normalized = envelope.Normalize();
            if (!normalized.TryGetModule(WorldObjectsModuleId, out MxLevelAuthoringModuleDocument module))
            {
                issues.Add(Error(
                    MissingWorldObjectsModule,
                    "modules",
                    "Level authoring envelope does not contain the CharacterTest world objects module."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            LevelWorldObjectAuthoringDocument payload = module.ReadPayload<LevelWorldObjectAuthoringDocument>();
            if (payload == null)
            {
                issues.Add(Error(
                    InvalidWorldObjectsModulePayload,
                    "modules." + WorldObjectsModuleId + ".payload",
                    "CharacterTest world objects module payload could not be decoded."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            document = payload.Normalize();
            if (string.IsNullOrWhiteSpace(document.LevelId))
                document.LevelId = normalized.LevelId;
            if (string.IsNullOrWhiteSpace(document.ScenePath))
                document.ScenePath = normalized.ScenePath;
            if (string.IsNullOrWhiteSpace(document.RootPath))
                document.RootPath = normalized.RootTransformRef?.Path ?? normalized.RootPath;
            report = LevelWorldObjectAuthoringValidationReport.Empty;
            return true;
        }

        public static LevelWorldObjectAuthoringDocument CreateDocumentFromSource(
            CharacterTestWorldObjectSceneSource source,
            string levelId = null,
            string scenePath = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = string.IsNullOrWhiteSpace(levelId) ? DefaultLevelId : levelId.Trim(),
                ScenePath = string.IsNullOrWhiteSpace(scenePath) ? DefaultScenePath : scenePath.Trim().Replace('\\', '/'),
                RootPath = source.transform.name,
                MovableInstances = new List<LevelWorldObjectInstanceDefinition>(),
                SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>(),
                StaticColliderBatches = new List<LevelWorldObjectStaticColliderBatchDefinition>(),
                Points = new List<LevelWorldObjectPointDefinition>(),
                Regions = new List<LevelWorldObjectRegionDefinition>()
            };

            IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances =
                source.MovableInstances;
            for (int i = 0; i < movableInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.MovableInstance instance = movableInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(source.transform, instance.Transform);
                document.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
                {
                    Enabled = instance.IsEnabled,
                    Role = LevelWorldObjectRole.Movable,
                    DefinitionId = instance.DefinitionId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    NumericInstanceId = instance.NumericInstanceId,
                    InstanceStableId = instance.InstanceStableId,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances =
                source.StaticColliderInstances;
            for (int i = 0; i < staticInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.StaticColliderInstance instance = staticInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(source.transform, instance.Transform);
                document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
                {
                    Enabled = instance.IsEnabled,
                    Role = LevelWorldObjectRole.StaticCollider,
                    DefinitionId = instance.DefinitionId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    BodyId = instance.BodyId,
                    ColliderId = instance.ColliderId,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances =
                source.PointInstances;
            for (int i = 0; i < pointInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelPointInstance instance = pointInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(source.transform, instance.Transform);
                document.Points.Add(new LevelWorldObjectPointDefinition
                {
                    Enabled = instance.IsEnabled,
                    DefinitionId = instance.DefinitionId,
                    StableId = instance.StableId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances =
                source.RegionInstances;
            for (int i = 0; i < regionInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelRegionInstance instance = regionInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(source.transform, instance.Transform);
                document.Regions.Add(new LevelWorldObjectRegionDefinition
                {
                    Enabled = instance.IsEnabled,
                    DefinitionId = instance.DefinitionId,
                    StableId = instance.StableId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            return document.Normalize();
        }

        public static LevelWorldObjectAuthoringDocument CreateDocumentFromCache(
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            string levelId = null,
            string scenePath = null)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = string.IsNullOrWhiteSpace(levelId) ? DefaultLevelId : levelId.Trim(),
                ScenePath = string.IsNullOrWhiteSpace(scenePath) ? DefaultScenePath : scenePath.Trim().Replace('\\', '/'),
                RootPath = root.name,
                MovableInstances = new List<LevelWorldObjectInstanceDefinition>(),
                SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>(),
                StaticColliderBatches = new List<LevelWorldObjectStaticColliderBatchDefinition>(),
                Points = new List<LevelWorldObjectPointDefinition>(),
                Regions = new List<LevelWorldObjectRegionDefinition>()
            };

            IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances =
                cache.MovableInstances;
            for (int i = 0; i < movableInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.MovableInstance instance = movableInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(root, instance.Transform);
                document.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
                {
                    Enabled = instance.IsEnabled,
                    Role = LevelWorldObjectRole.Movable,
                    DefinitionId = instance.DefinitionId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    NumericInstanceId = instance.NumericInstanceId,
                    InstanceStableId = instance.InstanceStableId,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances =
                cache.StaticColliderInstances;
            for (int i = 0; i < staticInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.StaticColliderInstance instance = staticInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(root, instance.Transform);
                document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
                {
                    Enabled = instance.IsEnabled,
                    Role = LevelWorldObjectRole.StaticCollider,
                    DefinitionId = instance.DefinitionId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    BodyId = instance.BodyId,
                    ColliderId = instance.ColliderId,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances =
                cache.PointInstances;
            for (int i = 0; i < pointInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelPointInstance instance = pointInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(root, instance.Transform);
                document.Points.Add(new LevelWorldObjectPointDefinition
                {
                    Enabled = instance.IsEnabled,
                    DefinitionId = instance.DefinitionId,
                    StableId = instance.StableId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances =
                cache.RegionInstances;
            for (int i = 0; i < regionInstances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelRegionInstance instance = regionInstances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef = CreateTransformRef(root, instance.Transform);
                document.Regions.Add(new LevelWorldObjectRegionDefinition
                {
                    Enabled = instance.IsEnabled,
                    DefinitionId = instance.DefinitionId,
                    StableId = instance.StableId,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = ToAuthoringScaleSource(instance.ScaleSource),
                    Group = ResolveGroup(transformRef.Path)
                });
            }

            return document.Normalize();
        }

        public static bool ApplyDocumentToSource(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            string authoringJsonPath,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            LevelWorldObjectAuthoringDocument normalized = document == null ? null : document.Normalize();
            bool built = TryBuildCache(
                normalized,
                source.transform,
                definitionProvider,
                out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
                out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
                out report);
            if (!built)
                return false;

            source.ConfigureFromAuthoring(
                movableInstances,
                staticInstances,
                pointInstances,
                regionInstances,
                authoringJsonPath,
                normalized == null ? string.Empty : LevelWorldObjectAuthoringJson.ComputeContentHash(normalized),
                DateTime.UtcNow.ToString("o"));
            return true;
        }

        public static bool ApplyDocumentToCache(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            string authoringJsonPath,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            LevelWorldObjectAuthoringDocument normalized = document == null ? null : document.Normalize();
            bool built = TryBuildCache(
                normalized,
                root,
                definitionProvider,
                out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
                out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
                out report);
            if (!built)
                return false;

            cache.ConfigureFromAuthoring(
                movableInstances,
                staticInstances,
                pointInstances,
                regionInstances,
                authoringJsonPath,
                normalized == null ? string.Empty : LevelWorldObjectAuthoringJson.ComputeContentHash(normalized),
                DateTime.UtcNow.ToString("o"));
            return true;
        }

        public static bool ApplyEnvelopeToSource(
            MxLevelAuthoringDocument envelope,
            CharacterTestWorldObjectSceneSource source,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            string authoringJsonPath,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            if (!TryExtractWorldObjectsDocument(envelope, out LevelWorldObjectAuthoringDocument document, out report))
                return false;

            MxLevelAuthoringValidationReport envelopeReport =
                MxLevelAuthoringValidator.Validate(envelope);
            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForSource(document, source, definitionProvider);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
            if (report.HasErrors)
                return false;

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            LevelWorldObjectAuthoringDocument normalized = document.Normalize();
            bool built = TryBuildCache(
                normalized,
                source.transform,
                definitionProvider,
                out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
                out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
                out LevelWorldObjectAuthoringValidationReport buildReport);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), buildReport);
            if (!built)
                return false;

            source.ConfigureFromAuthoring(
                movableInstances,
                staticInstances,
                pointInstances,
                regionInstances,
                authoringJsonPath,
                envelope == null ? string.Empty : MxLevelAuthoringJson.ComputeContentHash(envelope),
                DateTime.UtcNow.ToString("o"));
            return true;
        }

        public static bool ApplyEnvelopeToCache(
            MxLevelAuthoringDocument envelope,
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            string authoringJsonPath,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            if (!TryExtractWorldObjectsDocument(envelope, out LevelWorldObjectAuthoringDocument document, out report))
                return false;

            MxLevelAuthoringValidationReport envelopeReport =
                MxLevelAuthoringValidator.Validate(envelope);
            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForCache(document, cache, root, definitionProvider);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
            if (report.HasErrors)
                return false;

            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            LevelWorldObjectAuthoringDocument normalized = document.Normalize();
            bool built = TryBuildCache(
                normalized,
                root,
                definitionProvider,
                out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
                out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
                out LevelWorldObjectAuthoringValidationReport buildReport);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), buildReport);
            if (!built)
                return false;

            cache.ConfigureFromAuthoring(
                movableInstances,
                staticInstances,
                pointInstances,
                regionInstances,
                authoringJsonPath,
                envelope == null ? string.Empty : MxLevelAuthoringJson.ComputeContentHash(envelope),
                DateTime.UtcNow.ToString("o"));
            return true;
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateDocumentForSource(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider)
        {
            TryBuildCache(
                document == null ? null : document.Normalize(),
                source == null ? null : source.transform,
                definitionProvider,
                out _,
                out _,
                out _,
                out _,
                out LevelWorldObjectAuthoringValidationReport report);
            return report;
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateDocumentForCache(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider)
        {
            TryBuildCache(
                document == null ? null : document.Normalize(),
                root,
                definitionProvider,
                out _,
                out _,
                out _,
                out _,
                out LevelWorldObjectAuthoringValidationReport report);
            return report;
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateEnvelopeForSource(
            MxLevelAuthoringDocument envelope,
            CharacterTestWorldObjectSceneSource source,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider)
        {
            MxLevelAuthoringValidationReport envelopeReport =
                MxLevelAuthoringValidator.Validate(envelope);
            if (!TryExtractWorldObjectsDocument(envelope, out LevelWorldObjectAuthoringDocument document, out LevelWorldObjectAuthoringValidationReport extractReport))
                return MergeReports(envelopeReport.ToWorldObjectReport(), extractReport);

            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForSource(document, source, definitionProvider);
            return MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateEnvelopeForCache(
            MxLevelAuthoringDocument envelope,
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider)
        {
            MxLevelAuthoringValidationReport envelopeReport =
                MxLevelAuthoringValidator.Validate(envelope);
            if (!TryExtractWorldObjectsDocument(envelope, out LevelWorldObjectAuthoringDocument document, out LevelWorldObjectAuthoringValidationReport extractReport))
                return MergeReports(envelopeReport.ToWorldObjectReport(), extractReport);

            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForCache(document, cache, root, definitionProvider);
            return MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
        }

        public static bool TryCompileCombatLevelCandidate(
            MxLevelAuthoringDocument envelope,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out CharacterTestCombatLevelSceneAdapterResult compiledResult,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            compiledResult = null;
            MxLevelAuthoringValidationReport envelopeReport = MxLevelAuthoringValidator.Validate(envelope);
            if (!TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument document,
                    out LevelWorldObjectAuthoringValidationReport extractReport))
            {
                report = MergeReports(envelopeReport.ToWorldObjectReport(), extractReport);
                return false;
            }

            bool built = TryBuildCache(
                document.Normalize(),
                root,
                definitionProvider,
                out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
                out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
                out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
                out LevelWorldObjectAuthoringValidationReport buildReport);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), buildReport);
            if (!built || report.HasErrors)
                return false;

            var candidate = new CharacterTestWorldObjectSceneCache();
            candidate.Configure(movableInstances, staticInstances, pointInstances, regionInstances);
            compiledResult = CharacterTestCombatLevelSceneAdapter.BuildLevel(root, candidate);
            var compileIssues = new List<LevelWorldObjectAuthoringIssue>();
            AppendCompileIssues(compiledResult.AdapterReport, compileIssues);
            AppendCompileIssues(compiledResult.DefinitionReport, compileIssues);
            report = report.Append(compileIssues);
            return !report.HasErrors;
        }

        private static void AppendCompileIssues(
            CombatLevelValidationReport source,
            List<LevelWorldObjectAuthoringIssue> output)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Issues.Count; i++)
            {
                CombatLevelValidationIssue issue = source.Issues[i];
                LevelWorldObjectAuthoringSeverity severity = issue.Severity == CombatLevelValidationSeverity.Error
                    ? LevelWorldObjectAuthoringSeverity.Error
                    : issue.Severity == CombatLevelValidationSeverity.Warning
                        ? LevelWorldObjectAuthoringSeverity.Warning
                        : LevelWorldObjectAuthoringSeverity.Info;
                output.Add(new LevelWorldObjectAuthoringIssue(
                    severity,
                    issue.Code,
                    issue.Path,
                    issue.Message));
            }
        }

        public static bool TryResolveTransform(
            Transform root,
            LevelTransformReference transformRef,
            out Transform transform)
        {
            transform = null;
            if (root == null || transformRef == null)
                return false;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(transformRef.GlobalObjectId)
                && GlobalObjectId.TryParse(transformRef.GlobalObjectId.Trim(), out GlobalObjectId objectId))
            {
                UnityEngine.Object resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId);
                Transform resolvedTransform = null;
                if (resolved is GameObject gameObject)
                    resolvedTransform = gameObject.transform;
                else if (resolved is Component component)
                    resolvedTransform = component.transform;

                if (resolvedTransform != null && IsChildOrSelf(root, resolvedTransform))
                {
                    transform = resolvedTransform;
                    return true;
                }
            }
#endif

            return TryResolveTransform(root, transformRef.Path, out transform);
        }

        public static bool TryResolveTransform(
            Transform root,
            string transformPath,
            out Transform transform)
        {
            transform = null;
            if (root == null || string.IsNullOrWhiteSpace(transformPath))
                return false;

            string[] segments = transformPath.Trim().Replace('\\', '/').Split('/');
            Transform current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(segments[i]))
                    return false;

                if (!TryResolveChild(current, segments[i], out current))
                    return false;
            }

            transform = current;
            return transform != null;
        }

        public static LevelTransformReference CreateTransformRef(Transform root, Transform transform)
        {
            string path = CharacterTestWorldObjectSceneSource.ResolveTransformPath(root, transform);
            return new LevelTransformReference(path, TryCreateGlobalObjectId(transform));
        }

        public static string ResolveGroup(string transformPath)
        {
            if (string.IsNullOrWhiteSpace(transformPath))
                return string.Empty;

            string normalized = transformPath.Trim().Replace('\\', '/');
            int slashIndex = normalized.IndexOf('/');
            string firstSegment = slashIndex < 0 ? normalized : normalized.Substring(0, slashIndex);
            int bracketIndex = firstSegment.LastIndexOf('[');
            return bracketIndex > 0 ? firstSegment.Substring(0, bracketIndex) : firstSegment;
        }

        public static LevelWorldObjectScaleSource ToAuthoringScaleSource(
            CharacterTestWorldObjectInstanceScaleSource source)
        {
            return source == CharacterTestWorldObjectInstanceScaleSource.Definition
                ? LevelWorldObjectScaleSource.Definition
                : LevelWorldObjectScaleSource.SceneTransform;
        }

        public static CharacterTestWorldObjectInstanceScaleSource ToSceneScaleSource(
            LevelWorldObjectScaleSource source)
        {
            return source == LevelWorldObjectScaleSource.Definition
                ? CharacterTestWorldObjectInstanceScaleSource.Definition
                : CharacterTestWorldObjectInstanceScaleSource.SceneTransform;
        }

        private static bool TryBuildCache(
            LevelWorldObjectAuthoringDocument normalized,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out List<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
            out List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticInstances,
            out List<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
            out List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            LevelWorldObjectAuthoringValidationReport baseReport =
                LevelWorldObjectAuthoringValidator.Validate(normalized);
            var localIssues = new List<LevelWorldObjectAuthoringIssue>();
            movableInstances = new List<CharacterTestWorldObjectSceneSource.MovableInstance>();
            staticInstances = new List<CharacterTestWorldObjectSceneSource.StaticColliderInstance>();
            pointInstances = new List<CharacterTestWorldObjectSceneSource.LevelPointInstance>();
            regionInstances = new List<CharacterTestWorldObjectSceneSource.LevelRegionInstance>();
            ICharacterTestWorldObjectDefinitionProvider provider =
                definitionProvider ?? CharacterTestWorldObjectSceneSource.BuiltInCatalog;

            if (root == null)
            {
                localIssues.Add(Error(
                    TransformNotFound,
                    "root",
                    "Target CharacterTest scene cache root was not found."));
                report = baseReport.Append(localIssues);
                return false;
            }

            if (normalized == null)
            {
                report = baseReport.Append(localIssues);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalized.RootPath)
                && !string.Equals(normalized.RootPath, root.name, StringComparison.Ordinal))
            {
                localIssues.Add(Warning(
                    RootPathMismatch,
                    "rootPath",
                    "Document rootPath does not match the target scene cache root transform name."));
            }

            BuildMovables(normalized, root, provider, movableInstances, localIssues);
            BuildSingleStaticColliders(normalized, root, provider, staticInstances, localIssues);
            BuildStaticBatches(normalized, root, provider, staticInstances, localIssues);
            BuildPoints(normalized, root, provider, pointInstances, localIssues);
            BuildRegions(normalized, root, provider, regionInstances, localIssues);

            report = baseReport.Append(localIssues);
            return !report.HasErrors;
        }

        private static LevelWorldObjectAuthoringValidationReport MergeReports(
            LevelWorldObjectAuthoringValidationReport first,
            LevelWorldObjectAuthoringValidationReport second)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            AddIssues(first, issues);
            AddIssues(second, issues);
            return new LevelWorldObjectAuthoringValidationReport(issues);
        }

        private static void AddIssues(
            LevelWorldObjectAuthoringValidationReport report,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (report == null || report.Issues == null)
                return;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i] != null)
                    issues.Add(report.Issues[i]);
            }
        }

        private static void BuildMovables(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider provider,
            List<CharacterTestWorldObjectSceneSource.MovableInstance> output,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances =
                document.MovableInstances != null
                    ? document.MovableInstances
                    : (IReadOnlyList<LevelWorldObjectInstanceDefinition>)Array.Empty<LevelWorldObjectInstanceDefinition>();
            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;

                string path = "movableInstances[" + i + "]";
                if (!TryResolveDefinition(provider, instance.DefinitionId, path, issues, out CharacterTestWorldObjectDefinition definition))
                    continue;
                if (instance.Enabled && !definition.CanSpawnMovableNaturalObject)
                {
                    issues.Add(Error(DefinitionRoleMismatch, path + ".definitionId", "Definition is not a movable natural object."));
                    continue;
                }
                if (!TryResolveTransformForEnabled(root, instance.Enabled, instance.TransformRef, path, issues, out Transform transform))
                    continue;

                output.Add(new CharacterTestWorldObjectSceneSource.MovableInstance(
                    instance.Enabled,
                    instance.DefinitionId,
                    instance.NumericInstanceId,
                    instance.InstanceStableId,
                    transform,
                    ToSceneScaleSource(instance.ScaleSource)));
            }
        }

        private static void BuildSingleStaticColliders(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider provider,
            List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> output,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances =
                document.SingleStaticColliders != null
                    ? document.SingleStaticColliders
                    : (IReadOnlyList<LevelWorldObjectInstanceDefinition>)Array.Empty<LevelWorldObjectInstanceDefinition>();
            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;

                string path = "singleStaticColliders[" + i + "]";
                if (!TryResolveStaticDefinition(provider, instance.DefinitionId, path, instance.Enabled, issues))
                    continue;
                if (!TryResolveTransformForEnabled(root, instance.Enabled, instance.TransformRef, path, issues, out Transform transform))
                    continue;

                output.Add(new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                    instance.Enabled,
                    instance.DefinitionId,
                    transform,
                    instance.BodyId,
                    instance.ColliderId,
                    ToSceneScaleSource(instance.ScaleSource)));
            }
        }

        private static void BuildStaticBatches(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider provider,
            List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> output,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            IReadOnlyList<LevelWorldObjectStaticColliderBatchDefinition> batches =
                document.StaticColliderBatches != null
                    ? document.StaticColliderBatches
                    : (IReadOnlyList<LevelWorldObjectStaticColliderBatchDefinition>)Array.Empty<LevelWorldObjectStaticColliderBatchDefinition>();
            for (int i = 0; i < batches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = batches[i];
                if (batch == null)
                    continue;

                string path = "staticColliderBatches[" + i + "]";
                if (!TryResolveStaticDefinition(provider, batch.DefinitionId, path, batch.Enabled, issues))
                    continue;
                if (!TryResolveTransformForEnabled(
                        root,
                        batch.Enabled,
                        batch.ContainerTransformRef,
                        path,
                        issues,
                        out Transform container))
                    continue;
                if (!batch.Enabled)
                    continue;

                for (int childIndex = 0; childIndex < container.childCount; childIndex++)
                {
                    Transform child = container.GetChild(childIndex);
                    if (child == null || !child.gameObject.activeSelf)
                        continue;

                    output.Add(new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                        true,
                        batch.DefinitionId,
                        child,
                        batch.BodyIdStart + child.GetSiblingIndex(),
                        batch.ColliderId,
                        ToSceneScaleSource(batch.ScaleSource)));
                }
            }
        }

        private static void BuildPoints(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider provider,
            List<CharacterTestWorldObjectSceneSource.LevelPointInstance> output,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            IReadOnlyList<LevelWorldObjectPointDefinition> points =
                document.Points != null
                    ? document.Points
                    : (IReadOnlyList<LevelWorldObjectPointDefinition>)Array.Empty<LevelWorldObjectPointDefinition>();
            for (int i = 0; i < points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = points[i];
                if (point == null)
                    continue;

                string path = "points[" + i + "]";
                if (!TryResolveDefinition(provider, point.DefinitionId, path, issues, out CharacterTestWorldObjectDefinition definition))
                    continue;
                if (point.Enabled && !definition.CanSpawnPoint)
                {
                    issues.Add(Error(DefinitionRoleMismatch, path + ".definitionId", "Definition is not a level point."));
                    continue;
                }
                if (!TryResolveTransformForEnabled(root, point.Enabled, point.TransformRef, path, issues, out Transform transform))
                    continue;

                output.Add(new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                    point.Enabled,
                    point.DefinitionId,
                    point.StableId,
                    transform));
            }
        }

        private static void BuildRegions(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            ICharacterTestWorldObjectDefinitionProvider provider,
            List<CharacterTestWorldObjectSceneSource.LevelRegionInstance> output,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            IReadOnlyList<LevelWorldObjectRegionDefinition> regions =
                document.Regions != null
                    ? document.Regions
                    : (IReadOnlyList<LevelWorldObjectRegionDefinition>)Array.Empty<LevelWorldObjectRegionDefinition>();
            for (int i = 0; i < regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = regions[i];
                if (region == null)
                    continue;

                string path = "regions[" + i + "]";
                if (!TryResolveDefinition(provider, region.DefinitionId, path, issues, out CharacterTestWorldObjectDefinition definition))
                    continue;
                if (region.Enabled && !definition.CanSpawnRegion)
                {
                    issues.Add(Error(DefinitionRoleMismatch, path + ".definitionId", "Definition is not a level region."));
                    continue;
                }
                if (!TryResolveTransformForEnabled(root, region.Enabled, region.TransformRef, path, issues, out Transform transform))
                    continue;

                output.Add(new CharacterTestWorldObjectSceneSource.LevelRegionInstance(
                    region.Enabled,
                    region.DefinitionId,
                    region.StableId,
                    transform,
                    ToSceneScaleSource(region.ScaleSource)));
            }
        }

        private static bool TryResolveStaticDefinition(
            ICharacterTestWorldObjectDefinitionProvider provider,
            int definitionId,
            string path,
            bool enabled,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (!TryResolveDefinition(provider, definitionId, path, issues, out CharacterTestWorldObjectDefinition definition))
                return false;
            if (enabled && !definition.CanSpawnStaticCollider)
            {
                issues.Add(Error(DefinitionRoleMismatch, path + ".definitionId", "Definition is not a static collider."));
                return false;
            }

            return true;
        }

        private static bool TryResolveDefinition(
            ICharacterTestWorldObjectDefinitionProvider provider,
            int definitionId,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues,
            out CharacterTestWorldObjectDefinition definition)
        {
            definition = default;
            if (!provider.TryGetDefinition(definitionId, out definition))
            {
                issues.Add(Error(
                    DefinitionNotFound,
                    path + ".definitionId",
                    "Definition id " + definitionId + " was not found."));
                return false;
            }

            return true;
        }

        private static bool TryResolveTransformForEnabled(
            Transform root,
            bool enabled,
            LevelTransformReference transformRef,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues,
            out Transform transform)
        {
            transform = null;
            if (!TryResolveTransform(root, transformRef, out transform))
            {
                if (enabled)
                {
                    issues.Add(Error(
                        TransformNotFound,
                        path + ".transformRef",
                        "Transform reference could not be resolved under the target source root."));
                }

                return false;
            }

            return true;
        }

        private static bool TryResolveChild(
            Transform parent,
            string segment,
            out Transform child)
        {
            child = null;
            if (parent == null)
                return false;

            ParseSegment(segment, out string name, out int siblingIndex);
            if (siblingIndex >= 0
                && siblingIndex < parent.childCount
                && string.Equals(parent.GetChild(siblingIndex).name, name, StringComparison.Ordinal))
            {
                child = parent.GetChild(siblingIndex);
                return true;
            }

            Transform match = null;
            int matchCount = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform candidate = parent.GetChild(i);
                if (!string.Equals(candidate.name, name, StringComparison.Ordinal))
                    continue;

                match = candidate;
                matchCount++;
            }

            if (matchCount == 1)
            {
                child = match;
                return true;
            }

            return false;
        }

        private static void ParseSegment(string segment, out string name, out int siblingIndex)
        {
            name = segment ?? string.Empty;
            siblingIndex = -1;
            int bracketStart = name.LastIndexOf('[');
            if (bracketStart <= 0 || !name.EndsWith("]", StringComparison.Ordinal))
                return;

            string indexText = name.Substring(bracketStart + 1, name.Length - bracketStart - 2);
            if (int.TryParse(indexText, out int parsedIndex))
            {
                siblingIndex = parsedIndex;
                name = name.Substring(0, bracketStart);
            }
        }

        private static bool IsChildOrSelf(Transform root, Transform candidate)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static string TryCreateGlobalObjectId(Transform transform)
        {
#if UNITY_EDITOR
            if (transform == null)
                return string.Empty;

            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
#else
            return string.Empty;
#endif
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

        private static LevelWorldObjectAuthoringIssue Warning(
            string code,
            string path,
            string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Warning,
                code,
                path,
                message);
        }
    }
}
