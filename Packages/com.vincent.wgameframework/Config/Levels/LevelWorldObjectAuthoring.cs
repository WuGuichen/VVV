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
    public enum LevelWorldObjectRole
    {
        Unknown = 0,
        Movable = 1,
        StaticCollider = 2
    }

    public enum LevelWorldObjectScaleSource
    {
        SceneTransform = 0,
        Definition = 1
    }

    public enum LevelWorldObjectPointKind
    {
        Unknown = 0,
        PlayerSpawn = 1,
        AiSpawn = 2,
        Checkpoint = 3,
        Marker = 4
    }

    public enum LevelWorldObjectRegionKind
    {
        Unknown = 0,
        TestRegion = 1,
        TriggerRegion = 2
    }

    public enum LevelWorldObjectAuthoringSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum LevelAuthoringDefinitionUsage
    {
        Unknown = 0,
        Movable = 1,
        StaticCollider = 2,
        Point = 3,
        Region = 4
    }

    public readonly struct LevelAuthoringDefinitionSummary
    {
        public LevelAuthoringDefinitionSummary(
            int definitionId,
            string stableId,
            string displayName,
            LevelAuthoringDefinitionUsage usage)
        {
            DefinitionId = Math.Max(0, definitionId);
            StableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StableId : displayName.Trim();
            Usage = usage;
        }

        public int DefinitionId { get; }

        public string StableId { get; }

        public string DisplayName { get; }

        public LevelAuthoringDefinitionUsage Usage { get; }
    }

    public interface ILevelAuthoringDefinitionProvider
    {
        bool TryGetDefinition(int definitionId, out LevelAuthoringDefinitionSummary definition);
    }

    public sealed class LevelTransformReference
    {
        public LevelTransformReference()
        {
            Path = string.Empty;
            GlobalObjectId = string.Empty;
        }

        public LevelTransformReference(string path, string globalObjectId = "")
        {
            Path = NormalizePath(path);
            GlobalObjectId = Trim(globalObjectId);
        }

        public string Path { get; set; }

        public string GlobalObjectId { get; set; }

        public LevelTransformReference Normalize()
        {
            return new LevelTransformReference(Path, GlobalObjectId);
        }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Path) && string.IsNullOrWhiteSpace(GlobalObjectId);

        public bool ShouldSerializeIsEmpty()
        {
            return false;
        }

        public static LevelTransformReference FromPath(string path)
        {
            return new LevelTransformReference(path, string.Empty);
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

    public sealed class LevelWorldObjectAuthoringDocument
    {
        public const string LegacyFormat = "mx.level.worldObjects.v1";
        public const int LegacySchemaVersion = 1;
        public const string CurrentFormat = "mx.level.worldObjects.v2";
        public const int CurrentSchemaVersion = 2;

        public LevelWorldObjectAuthoringDocument()
        {
            Format = CurrentFormat;
            SchemaVersion = CurrentSchemaVersion;
            LevelId = string.Empty;
            ScenePath = string.Empty;
            RootPath = string.Empty;
            Instances = new List<LevelWorldObjectInstanceDefinition>();
            MovableInstances = new List<LevelWorldObjectInstanceDefinition>();
            SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>();
            StaticColliderBatches = new List<LevelWorldObjectStaticColliderBatchDefinition>();
            Points = new List<LevelWorldObjectPointDefinition>();
            Regions = new List<LevelWorldObjectRegionDefinition>();
        }

        public string Format { get; set; }

        public int SchemaVersion { get; set; }

        public string LevelId { get; set; }

        public string ScenePath { get; set; }

        public string RootPath { get; set; }

        public List<LevelWorldObjectInstanceDefinition> Instances { get; set; }

        public List<LevelWorldObjectInstanceDefinition> MovableInstances { get; set; }

        public List<LevelWorldObjectInstanceDefinition> SingleStaticColliders { get; set; }

        public List<LevelWorldObjectStaticColliderBatchDefinition> StaticColliderBatches { get; set; }

        public List<LevelWorldObjectPointDefinition> Points { get; set; }

        public List<LevelWorldObjectRegionDefinition> Regions { get; set; }

        public bool ShouldSerializeInstances()
        {
            return IsLegacyFormat(Format) || SchemaVersion <= LegacySchemaVersion;
        }

        public LevelWorldObjectAuthoringDocument Normalize()
        {
            var normalized = new LevelWorldObjectAuthoringDocument
            {
                Format = CurrentFormat,
                SchemaVersion = CurrentSchemaVersion,
                LevelId = Trim(LevelId),
                ScenePath = NormalizePath(ScenePath),
                RootPath = Trim(RootPath),
                Instances = new List<LevelWorldObjectInstanceDefinition>(),
                MovableInstances = new List<LevelWorldObjectInstanceDefinition>(),
                SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>(),
                StaticColliderBatches = new List<LevelWorldObjectStaticColliderBatchDefinition>(),
                Points = new List<LevelWorldObjectPointDefinition>(),
                Regions = new List<LevelWorldObjectRegionDefinition>()
            };

            bool migrateLegacyInstances =
                IsLegacyFormat(Format)
                || SchemaVersion <= LegacySchemaVersion
                || !HasAnyCurrentCollections();
            if (migrateLegacyInstances)
                AddLegacyInstances(Instances, normalized.MovableInstances, normalized.SingleStaticColliders);
            AddInstances(MovableInstances, LevelWorldObjectRole.Movable, normalized.MovableInstances);
            AddInstances(SingleStaticColliders, LevelWorldObjectRole.StaticCollider, normalized.SingleStaticColliders);
            AddBatches(StaticColliderBatches, normalized.StaticColliderBatches);
            AddPoints(Points, normalized.Points);
            AddRegions(Regions, normalized.Regions);

            normalized.MovableInstances.Sort(LevelWorldObjectInstanceDefinitionComparer.Instance);
            normalized.SingleStaticColliders.Sort(LevelWorldObjectInstanceDefinitionComparer.Instance);
            normalized.StaticColliderBatches.Sort(LevelWorldObjectStaticColliderBatchDefinitionComparer.Instance);
            normalized.Points.Sort(LevelWorldObjectPointDefinitionComparer.Instance);
            normalized.Regions.Sort(LevelWorldObjectRegionDefinitionComparer.Instance);

            normalized.Instances.AddRange(normalized.MovableInstances);
            normalized.Instances.AddRange(normalized.SingleStaticColliders);
            normalized.Instances.Sort(LevelWorldObjectInstanceDefinitionComparer.Instance);
            return normalized;
        }

        internal static bool IsLegacyFormat(string format)
        {
            return string.Equals(LegacyFormat, format, StringComparison.Ordinal);
        }

        private bool HasAnyCurrentCollections()
        {
            return HasAny(MovableInstances)
                || HasAny(SingleStaticColliders)
                || HasAny(StaticColliderBatches)
                || HasAny(Points)
                || HasAny(Regions);
        }

        private static bool HasAny<T>(IReadOnlyList<T> values)
        {
            return values != null && values.Count > 0;
        }

        private static void AddLegacyInstances(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            List<LevelWorldObjectInstanceDefinition> movableInstances,
            List<LevelWorldObjectInstanceDefinition> singleStaticColliders)
        {
            if (instances == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;

                LevelWorldObjectInstanceDefinition normalized = instance.Normalize();
                if (normalized.Role == LevelWorldObjectRole.Movable)
                    movableInstances.Add(normalized);
                else if (normalized.Role == LevelWorldObjectRole.StaticCollider)
                    singleStaticColliders.Add(normalized);
            }
        }

        private static void AddInstances(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            LevelWorldObjectRole role,
            List<LevelWorldObjectInstanceDefinition> target)
        {
            if (instances == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] == null)
                    continue;

                LevelWorldObjectInstanceDefinition normalized = instances[i].Normalize();
                normalized.Role = role;
                target.Add(normalized);
            }
        }

        private static void AddBatches(
            IReadOnlyList<LevelWorldObjectStaticColliderBatchDefinition> batches,
            List<LevelWorldObjectStaticColliderBatchDefinition> target)
        {
            if (batches == null)
                return;

            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i] != null)
                    target.Add(batches[i].Normalize());
            }
        }

        private static void AddPoints(
            IReadOnlyList<LevelWorldObjectPointDefinition> points,
            List<LevelWorldObjectPointDefinition> target)
        {
            if (points == null)
                return;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != null)
                    target.Add(points[i].Normalize());
            }
        }

        private static void AddRegions(
            IReadOnlyList<LevelWorldObjectRegionDefinition> regions,
            List<LevelWorldObjectRegionDefinition> target)
        {
            if (regions == null)
                return;

            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i] != null)
                    target.Add(regions[i].Normalize());
            }
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

    public sealed class LevelWorldObjectInstanceDefinition
    {
        public LevelWorldObjectInstanceDefinition()
        {
            Enabled = true;
            Role = LevelWorldObjectRole.Unknown;
            DefinitionId = 0;
            TransformPath = string.Empty;
            TransformRef = new LevelTransformReference();
            ScaleSource = LevelWorldObjectScaleSource.SceneTransform;
            NumericInstanceId = 0;
            InstanceStableId = string.Empty;
            BodyId = 0;
            ColliderId = 0;
            Group = string.Empty;
            Notes = string.Empty;
        }

        public bool Enabled { get; set; }

        public LevelWorldObjectRole Role { get; set; }

        public int DefinitionId { get; set; }

        public string TransformPath { get; set; }

        public LevelTransformReference TransformRef { get; set; }

        public LevelWorldObjectScaleSource ScaleSource { get; set; }

        public int NumericInstanceId { get; set; }

        public string InstanceStableId { get; set; }

        public int BodyId { get; set; }

        public int ColliderId { get; set; }

        public string Group { get; set; }

        public string Notes { get; set; }

        public bool ShouldSerializeTransformPath()
        {
            return false;
        }

        public LevelWorldObjectInstanceDefinition Normalize()
        {
            LevelTransformReference transformRef = NormalizeTransformRef(TransformRef, TransformPath);
            return new LevelWorldObjectInstanceDefinition
            {
                Enabled = Enabled,
                Role = Role == LevelWorldObjectRole.Movable || Role == LevelWorldObjectRole.StaticCollider
                    ? Role
                    : LevelWorldObjectRole.Unknown,
                DefinitionId = Math.Max(0, DefinitionId),
                TransformPath = transformRef.Path,
                TransformRef = transformRef,
                ScaleSource = ScaleSource == LevelWorldObjectScaleSource.Definition
                    ? LevelWorldObjectScaleSource.Definition
                    : LevelWorldObjectScaleSource.SceneTransform,
                NumericInstanceId = Math.Max(0, NumericInstanceId),
                InstanceStableId = Trim(InstanceStableId),
                BodyId = Math.Max(0, BodyId),
                ColliderId = Math.Max(0, ColliderId),
                Group = Trim(Group),
                Notes = Trim(Notes)
            };
        }

        private static LevelTransformReference NormalizeTransformRef(
            LevelTransformReference transformRef,
            string legacyPath)
        {
            LevelTransformReference normalized = transformRef == null
                ? new LevelTransformReference()
                : transformRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.Path) && !string.IsNullOrWhiteSpace(legacyPath))
                normalized.Path = NormalizePath(legacyPath);
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

    public sealed class LevelWorldObjectStaticColliderBatchDefinition
    {
        public LevelWorldObjectStaticColliderBatchDefinition()
        {
            Enabled = true;
            DefinitionId = 0;
            ContainerTransformPath = string.Empty;
            ContainerTransformRef = new LevelTransformReference();
            ScaleSource = LevelWorldObjectScaleSource.SceneTransform;
            BodyIdStart = 0;
            ColliderId = 1;
            Group = string.Empty;
            Notes = string.Empty;
        }

        public bool Enabled { get; set; }

        public int DefinitionId { get; set; }

        public string ContainerTransformPath { get; set; }

        public LevelTransformReference ContainerTransformRef { get; set; }

        public LevelWorldObjectScaleSource ScaleSource { get; set; }

        public int BodyIdStart { get; set; }

        public int ColliderId { get; set; }

        public string Group { get; set; }

        public string Notes { get; set; }

        public bool ShouldSerializeContainerTransformPath()
        {
            return false;
        }

        public LevelWorldObjectStaticColliderBatchDefinition Normalize()
        {
            LevelTransformReference transformRef = NormalizeTransformRef(ContainerTransformRef, ContainerTransformPath);
            return new LevelWorldObjectStaticColliderBatchDefinition
            {
                Enabled = Enabled,
                DefinitionId = Math.Max(0, DefinitionId),
                ContainerTransformPath = transformRef.Path,
                ContainerTransformRef = transformRef,
                ScaleSource = ScaleSource == LevelWorldObjectScaleSource.Definition
                    ? LevelWorldObjectScaleSource.Definition
                    : LevelWorldObjectScaleSource.SceneTransform,
                BodyIdStart = Math.Max(0, BodyIdStart),
                ColliderId = ColliderId > 0 ? ColliderId : 1,
                Group = Trim(Group),
                Notes = Trim(Notes)
            };
        }

        private static LevelTransformReference NormalizeTransformRef(
            LevelTransformReference transformRef,
            string legacyPath)
        {
            LevelTransformReference normalized = transformRef == null
                ? new LevelTransformReference()
                : transformRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.Path) && !string.IsNullOrWhiteSpace(legacyPath))
                normalized.Path = NormalizePath(legacyPath);
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

    public sealed class LevelWorldObjectPointDefinition
    {
        public LevelWorldObjectPointDefinition()
        {
            Enabled = true;
            DefinitionId = 0;
            StableId = string.Empty;
            PointKind = LevelWorldObjectPointKind.Unknown;
            TransformPath = string.Empty;
            TransformRef = new LevelTransformReference();
            Group = string.Empty;
            Notes = string.Empty;
        }

        public bool Enabled { get; set; }

        public int DefinitionId { get; set; }

        public string StableId { get; set; }

        public LevelWorldObjectPointKind PointKind { get; set; }

        public string TransformPath { get; set; }

        public LevelTransformReference TransformRef { get; set; }

        public string Group { get; set; }

        public string Notes { get; set; }

        public bool ShouldSerializeTransformPath()
        {
            return false;
        }

        public LevelWorldObjectPointDefinition Normalize()
        {
            LevelTransformReference transformRef = NormalizeTransformRef(TransformRef, TransformPath);
            return new LevelWorldObjectPointDefinition
            {
                Enabled = Enabled,
                DefinitionId = Math.Max(0, DefinitionId),
                StableId = Trim(StableId),
                PointKind = Enum.IsDefined(typeof(LevelWorldObjectPointKind), PointKind)
                    ? PointKind
                    : LevelWorldObjectPointKind.Unknown,
                TransformPath = transformRef.Path,
                TransformRef = transformRef,
                Group = Trim(Group),
                Notes = Trim(Notes)
            };
        }

        private static LevelTransformReference NormalizeTransformRef(
            LevelTransformReference transformRef,
            string legacyPath)
        {
            LevelTransformReference normalized = transformRef == null
                ? new LevelTransformReference()
                : transformRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.Path) && !string.IsNullOrWhiteSpace(legacyPath))
                normalized.Path = NormalizePath(legacyPath);
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

    public sealed class LevelWorldObjectRegionDefinition
    {
        public LevelWorldObjectRegionDefinition()
        {
            Enabled = true;
            DefinitionId = 0;
            StableId = string.Empty;
            RegionKind = LevelWorldObjectRegionKind.Unknown;
            TransformPath = string.Empty;
            TransformRef = new LevelTransformReference();
            ScaleSource = LevelWorldObjectScaleSource.SceneTransform;
            Group = string.Empty;
            Notes = string.Empty;
        }

        public bool Enabled { get; set; }

        public int DefinitionId { get; set; }

        public string StableId { get; set; }

        public LevelWorldObjectRegionKind RegionKind { get; set; }

        public string TransformPath { get; set; }

        public LevelTransformReference TransformRef { get; set; }

        public LevelWorldObjectScaleSource ScaleSource { get; set; }

        public string Group { get; set; }

        public string Notes { get; set; }

        public bool ShouldSerializeTransformPath()
        {
            return false;
        }

        public LevelWorldObjectRegionDefinition Normalize()
        {
            LevelTransformReference transformRef = NormalizeTransformRef(TransformRef, TransformPath);
            return new LevelWorldObjectRegionDefinition
            {
                Enabled = Enabled,
                DefinitionId = Math.Max(0, DefinitionId),
                StableId = Trim(StableId),
                RegionKind = Enum.IsDefined(typeof(LevelWorldObjectRegionKind), RegionKind)
                    ? RegionKind
                    : LevelWorldObjectRegionKind.Unknown,
                TransformPath = transformRef.Path,
                TransformRef = transformRef,
                ScaleSource = ScaleSource == LevelWorldObjectScaleSource.Definition
                    ? LevelWorldObjectScaleSource.Definition
                    : LevelWorldObjectScaleSource.SceneTransform,
                Group = Trim(Group),
                Notes = Trim(Notes)
            };
        }

        private static LevelTransformReference NormalizeTransformRef(
            LevelTransformReference transformRef,
            string legacyPath)
        {
            LevelTransformReference normalized = transformRef == null
                ? new LevelTransformReference()
                : transformRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.Path) && !string.IsNullOrWhiteSpace(legacyPath))
                normalized.Path = NormalizePath(legacyPath);
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

    public sealed class LevelWorldObjectAuthoringIssue
    {
        public LevelWorldObjectAuthoringIssue(
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
    }

    public sealed class LevelWorldObjectAuthoringValidationReport
    {
        public LevelWorldObjectAuthoringValidationReport(IReadOnlyList<LevelWorldObjectAuthoringIssue> issues)
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

        public IReadOnlyList<LevelWorldObjectAuthoringIssue> Issues { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public bool HasErrors => ErrorCount > 0;

        public static LevelWorldObjectAuthoringValidationReport Empty { get; } =
            new LevelWorldObjectAuthoringValidationReport(Array.Empty<LevelWorldObjectAuthoringIssue>());

        public LevelWorldObjectAuthoringValidationReport Append(
            IReadOnlyList<LevelWorldObjectAuthoringIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return this;

            var merged = new List<LevelWorldObjectAuthoringIssue>(Issues.Count + issues.Count);
            for (int i = 0; i < Issues.Count; i++)
                merged.Add(Issues[i]);
            for (int i = 0; i < issues.Count; i++)
                merged.Add(issues[i]);
            return new LevelWorldObjectAuthoringValidationReport(merged);
        }

        private static ReadOnlyCollection<LevelWorldObjectAuthoringIssue> CopyIssues(
            IReadOnlyList<LevelWorldObjectAuthoringIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return new ReadOnlyCollection<LevelWorldObjectAuthoringIssue>(
                    new List<LevelWorldObjectAuthoringIssue>());

            var copy = new List<LevelWorldObjectAuthoringIssue>(issues.Count);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null)
                    copy.Add(issues[i]);
            }

            return new ReadOnlyCollection<LevelWorldObjectAuthoringIssue>(copy);
        }
    }

    public static class LevelWorldObjectAuthoringResolver
    {
        public delegate IReadOnlyList<LevelTransformReference> StaticBatchChildrenResolver(
            LevelWorldObjectStaticColliderBatchDefinition batch);

        public static List<LevelWorldObjectInstanceDefinition> ExpandStaticColliderBatches(
            LevelWorldObjectAuthoringDocument document,
            StaticBatchChildrenResolver childrenResolver)
        {
            var expanded = new List<LevelWorldObjectInstanceDefinition>();
            if (document == null || childrenResolver == null)
                return expanded;

            LevelWorldObjectAuthoringDocument normalized = document.Normalize();
            for (int i = 0; i < normalized.SingleStaticColliders.Count; i++)
                expanded.Add(normalized.SingleStaticColliders[i].Normalize());

            for (int i = 0; i < normalized.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch =
                    normalized.StaticColliderBatches[i].Normalize();
                if (!batch.Enabled)
                    continue;

                IReadOnlyList<LevelTransformReference> children = childrenResolver(batch);
                if (children == null)
                    continue;

                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    LevelTransformReference child = children[childIndex];
                    if (child == null || child.IsEmpty)
                        continue;

                    expanded.Add(new LevelWorldObjectInstanceDefinition
                    {
                        Enabled = true,
                        Role = LevelWorldObjectRole.StaticCollider,
                        DefinitionId = batch.DefinitionId,
                        TransformRef = child.Normalize(),
                        TransformPath = child.Path,
                        ScaleSource = batch.ScaleSource,
                        BodyId = batch.BodyIdStart + childIndex,
                        ColliderId = batch.ColliderId,
                        Group = batch.Group,
                        Notes = string.IsNullOrWhiteSpace(batch.Notes)
                            ? "generated from batch"
                            : batch.Notes
                    }.Normalize());
                }
            }

            expanded.Sort(LevelWorldObjectInstanceDefinitionComparer.Instance);
            return expanded;
        }
    }

    public static class LevelWorldObjectAuthoringValidator
    {
        public const string UnsupportedFormat = "UnsupportedFormat";
        public const string UnsupportedSchemaVersion = "UnsupportedSchemaVersion";
        public const string MissingLevelId = "MissingLevelId";
        public const string MissingScenePath = "MissingScenePath";
        public const string MissingRootPath = "MissingRootPath";
        public const string MissingTransformPath = "MissingTransformPath";
        public const string MissingContainerTransformPath = "MissingContainerTransformPath";
        public const string InvalidRole = "InvalidRole";
        public const string InvalidDefinitionId = "InvalidDefinitionId";
        public const string InvalidMovableNumericId = "InvalidMovableNumericId";
        public const string InvalidStaticBodyId = "InvalidStaticBodyId";
        public const string InvalidStaticBodyIdStart = "InvalidStaticBodyIdStart";
        public const string DuplicateTransformPath = "DuplicateTransformPath";
        public const string DuplicateMovableNumericId = "DuplicateMovableNumericId";
        public const string DuplicateStaticBodyColliderId = "DuplicateStaticBodyColliderId";
        public const string DuplicatePointStableId = "DuplicatePointStableId";
        public const string DuplicateRegionStableId = "DuplicateRegionStableId";

        public static LevelWorldObjectAuthoringValidationReport Validate(
            LevelWorldObjectAuthoringDocument document)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (document == null)
            {
                issues.Add(Error("MissingDocument", string.Empty, "World object authoring document is missing."));
                return new LevelWorldObjectAuthoringValidationReport(issues);
            }

            if (!string.Equals(LevelWorldObjectAuthoringDocument.CurrentFormat, document.Format, StringComparison.Ordinal)
                && !string.Equals(LevelWorldObjectAuthoringDocument.LegacyFormat, document.Format, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    UnsupportedFormat,
                    "format",
                    "Expected format " + LevelWorldObjectAuthoringDocument.CurrentFormat + "."));
            }

            if (document.SchemaVersion != LevelWorldObjectAuthoringDocument.CurrentSchemaVersion
                && document.SchemaVersion != LevelWorldObjectAuthoringDocument.LegacySchemaVersion)
            {
                issues.Add(Error(
                    UnsupportedSchemaVersion,
                    "schemaVersion",
                    "Expected schema version " + LevelWorldObjectAuthoringDocument.CurrentSchemaVersion + "."));
            }

            LevelWorldObjectAuthoringDocument normalized = document.Normalize();
            if (string.IsNullOrWhiteSpace(normalized.LevelId))
                issues.Add(Error(MissingLevelId, "levelId", "Level id is required."));
            if (string.IsNullOrWhiteSpace(normalized.ScenePath))
                issues.Add(Error(MissingScenePath, "scenePath", "Scene path is required."));
            if (string.IsNullOrWhiteSpace(normalized.RootPath))
                issues.Add(Warning(MissingRootPath, "rootPath", "Root path is empty."));

            ValidateInstances(
                normalized.MovableInstances,
                "movableInstances",
                LevelWorldObjectRole.Movable,
                issues,
                new Dictionary<string, int>(StringComparer.Ordinal),
                new Dictionary<int, int>(),
                null);
            ValidateInstances(
                normalized.SingleStaticColliders,
                "singleStaticColliders",
                LevelWorldObjectRole.StaticCollider,
                issues,
                new Dictionary<string, int>(StringComparer.Ordinal),
                null,
                new Dictionary<string, int>(StringComparer.Ordinal));
            ValidateBatches(normalized.StaticColliderBatches, issues);
            ValidatePoints(normalized.Points, issues);
            ValidateRegions(normalized.Regions, issues);
            return new LevelWorldObjectAuthoringValidationReport(issues);
        }

        private static void ValidateInstances(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            string collectionPath,
            LevelWorldObjectRole expectedRole,
            List<LevelWorldObjectAuthoringIssue> issues,
            Dictionary<string, int> transformPaths,
            Dictionary<int, int> movableIds,
            Dictionary<string, int> staticBodyColliderIds)
        {
            if (instances == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;

                string path = collectionPath + "[" + i + "]";
                if (!instance.Enabled)
                    continue;

                if (instance.Role != expectedRole)
                {
                    issues.Add(Error(
                        InvalidRole,
                        path + ".role",
                        "Instance role must be " + expectedRole + "."));
                }

                if (instance.DefinitionId <= 0)
                {
                    issues.Add(Error(
                        InvalidDefinitionId,
                        path + ".definitionId",
                        "Definition id must be greater than 0."));
                }

                ValidateTransformPath(instance.TransformRef, path + ".transformRef", issues, transformPaths, i);

                if (expectedRole == LevelWorldObjectRole.Movable && movableIds != null)
                {
                    if (instance.NumericInstanceId <= 0)
                    {
                        issues.Add(Error(
                            InvalidMovableNumericId,
                            path + ".numericInstanceId",
                            "Movable instances require a numeric instance id."));
                    }
                    else if (movableIds.TryGetValue(instance.NumericInstanceId, out int firstMovableIndex))
                    {
                        issues.Add(Error(
                            DuplicateMovableNumericId,
                            path + ".numericInstanceId",
                            "Movable numeric id duplicates movableInstances[" + firstMovableIndex + "]."));
                    }
                    else
                    {
                        movableIds.Add(instance.NumericInstanceId, i);
                    }
                }

                if (expectedRole == LevelWorldObjectRole.StaticCollider && staticBodyColliderIds != null)
                {
                    if (instance.BodyId <= 0)
                    {
                        issues.Add(Error(
                            InvalidStaticBodyId,
                            path + ".bodyId",
                            "Static collider instances require a body id."));
                    }
                    else
                    {
                        AddStaticBodyColliderId(
                            staticBodyColliderIds,
                            instance.BodyId,
                            instance.ColliderId > 0 ? instance.ColliderId : 1,
                            path + ".bodyId",
                            i,
                            issues);
                    }
                }
            }
        }

        private static void ValidateBatches(
            IReadOnlyList<LevelWorldObjectStaticColliderBatchDefinition> batches,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (batches == null)
                return;

            var containerPaths = new Dictionary<string, int>(StringComparer.Ordinal);
            var batchStarts = new Dictionary<int, int>();
            for (int i = 0; i < batches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = batches[i];
                if (batch == null || !batch.Enabled)
                    continue;

                string path = "staticColliderBatches[" + i + "]";
                if (batch.DefinitionId <= 0)
                {
                    issues.Add(Error(
                        InvalidDefinitionId,
                        path + ".definitionId",
                        "Definition id must be greater than 0."));
                }

                ValidateTransformPath(
                    batch.ContainerTransformRef,
                    path + ".containerTransformRef",
                    issues,
                    containerPaths,
                    i,
                    MissingContainerTransformPath);

                if (batch.BodyIdStart <= 0)
                {
                    issues.Add(Error(
                        InvalidStaticBodyIdStart,
                        path + ".bodyIdStart",
                        "Static collider batches require a positive body id start."));
                }
                else if (batchStarts.TryGetValue(batch.BodyIdStart, out int firstBatchIndex))
                {
                    issues.Add(Warning(
                        DuplicateStaticBodyColliderId,
                        path + ".bodyIdStart",
                        "Static batch body id start matches staticColliderBatches[" + firstBatchIndex + "]."));
                }
                else
                {
                    batchStarts.Add(batch.BodyIdStart, i);
                }
            }
        }

        private static void ValidatePoints(
            IReadOnlyList<LevelWorldObjectPointDefinition> points,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (points == null)
                return;

            var transformPaths = new Dictionary<string, int>(StringComparer.Ordinal);
            var stableIds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = points[i];
                if (point == null || !point.Enabled)
                    continue;

                string path = "points[" + i + "]";
                if (point.DefinitionId <= 0)
                {
                    issues.Add(Error(
                        InvalidDefinitionId,
                        path + ".definitionId",
                        "Definition id must be greater than 0."));
                }

                ValidateTransformPath(point.TransformRef, path + ".transformRef", issues, transformPaths, i);
                AddStableId(stableIds, point.StableId, DuplicatePointStableId, path + ".stableId", i, issues);
            }
        }

        private static void ValidateRegions(
            IReadOnlyList<LevelWorldObjectRegionDefinition> regions,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (regions == null)
                return;

            var transformPaths = new Dictionary<string, int>(StringComparer.Ordinal);
            var stableIds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = regions[i];
                if (region == null || !region.Enabled)
                    continue;

                string path = "regions[" + i + "]";
                if (region.DefinitionId <= 0)
                {
                    issues.Add(Error(
                        InvalidDefinitionId,
                        path + ".definitionId",
                        "Definition id must be greater than 0."));
                }

                ValidateTransformPath(region.TransformRef, path + ".transformRef", issues, transformPaths, i);
                AddStableId(stableIds, region.StableId, DuplicateRegionStableId, path + ".stableId", i, issues);
            }
        }

        private static void ValidateTransformPath(
            LevelTransformReference transformRef,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues,
            Dictionary<string, int> transformPaths,
            int index,
            string missingCode = MissingTransformPath)
        {
            string transformPath = transformRef == null ? string.Empty : NormalizePath(transformRef.Path);
            if (string.IsNullOrEmpty(transformPath))
            {
                issues.Add(Error(missingCode, path + ".path", "Transform path is required."));
            }
            else if (transformPaths != null && transformPaths.TryGetValue(transformPath, out int firstIndex))
            {
                issues.Add(Error(
                    DuplicateTransformPath,
                    path + ".path",
                    "Transform path duplicates index " + firstIndex + "."));
            }
            else if (transformPaths != null)
            {
                transformPaths.Add(transformPath, index);
            }
        }

        private static void AddStaticBodyColliderId(
            Dictionary<string, int> ids,
            int bodyId,
            int colliderId,
            string path,
            int index,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            string key = bodyId + ":" + colliderId;
            if (ids.TryGetValue(key, out int firstIndex))
            {
                issues.Add(Error(
                    DuplicateStaticBodyColliderId,
                    path,
                    "Static body/collider id duplicates index " + firstIndex + "."));
            }
            else
            {
                ids.Add(key, index);
            }
        }

        private static void AddStableId(
            Dictionary<string, int> stableIds,
            string stableId,
            string duplicateCode,
            string path,
            int index,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return;

            string key = stableId.Trim();
            if (stableIds.TryGetValue(key, out int firstIndex))
            {
                issues.Add(Error(
                    duplicateCode,
                    path,
                    "Stable id duplicates index " + firstIndex + "."));
            }
            else
            {
                stableIds.Add(key, index);
            }
        }

        private static LevelWorldObjectAuthoringIssue Error(string code, string path, string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }

        private static LevelWorldObjectAuthoringIssue Warning(string code, string path, string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Warning,
                code,
                path,
                message);
        }

        private static string NormalizePath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
        }
    }

    public static class LevelWorldObjectAuthoringJson
    {
        private static readonly JsonSerializerSettings Settings = CreateSettings();

        public static string SaveToJson(LevelWorldObjectAuthoringDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return JsonConvert.SerializeObject(document.Normalize(), Settings);
        }

        public static LevelWorldObjectAuthoringDocument LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "World object authoring json cannot be null or empty.",
                    nameof(json));
            }

            JObject root = JObject.Parse(json);
            LevelWorldObjectAuthoringDocument document =
                root.ToObject<LevelWorldObjectAuthoringDocument>(JsonSerializer.Create(Settings));
            return document == null ? new LevelWorldObjectAuthoringDocument() : document.Normalize();
        }

        public static string ComputeContentHash(LevelWorldObjectAuthoringDocument document)
        {
            string json = SaveToJson(document);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
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

    internal sealed class LevelWorldObjectInstanceDefinitionComparer :
        IComparer<LevelWorldObjectInstanceDefinition>
    {
        public static readonly LevelWorldObjectInstanceDefinitionComparer Instance =
            new LevelWorldObjectInstanceDefinitionComparer();

        public int Compare(
            LevelWorldObjectInstanceDefinition x,
            LevelWorldObjectInstanceDefinition y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            int group = string.CompareOrdinal(x.Group ?? string.Empty, y.Group ?? string.Empty);
            if (group != 0)
                return group;
            int role = x.Role.CompareTo(y.Role);
            if (role != 0)
                return role;
            int definition = x.DefinitionId.CompareTo(y.DefinitionId);
            if (definition != 0)
                return definition;
            int transform = string.CompareOrdinal(
                GetTransformPath(x),
                GetTransformPath(y));
            if (transform != 0)
                return transform;
            return x.NumericInstanceId.CompareTo(y.NumericInstanceId);
        }

        private static string GetTransformPath(LevelWorldObjectInstanceDefinition instance)
        {
            return instance?.TransformRef?.Path ?? instance?.TransformPath ?? string.Empty;
        }
    }

    internal sealed class LevelWorldObjectStaticColliderBatchDefinitionComparer :
        IComparer<LevelWorldObjectStaticColliderBatchDefinition>
    {
        public static readonly LevelWorldObjectStaticColliderBatchDefinitionComparer Instance =
            new LevelWorldObjectStaticColliderBatchDefinitionComparer();

        public int Compare(
            LevelWorldObjectStaticColliderBatchDefinition x,
            LevelWorldObjectStaticColliderBatchDefinition y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            int group = string.CompareOrdinal(x.Group ?? string.Empty, y.Group ?? string.Empty);
            if (group != 0)
                return group;
            int definition = x.DefinitionId.CompareTo(y.DefinitionId);
            if (definition != 0)
                return definition;
            int body = x.BodyIdStart.CompareTo(y.BodyIdStart);
            if (body != 0)
                return body;
            return string.CompareOrdinal(
                x.ContainerTransformRef?.Path ?? x.ContainerTransformPath ?? string.Empty,
                y.ContainerTransformRef?.Path ?? y.ContainerTransformPath ?? string.Empty);
        }
    }

    internal sealed class LevelWorldObjectPointDefinitionComparer :
        IComparer<LevelWorldObjectPointDefinition>
    {
        public static readonly LevelWorldObjectPointDefinitionComparer Instance =
            new LevelWorldObjectPointDefinitionComparer();

        public int Compare(LevelWorldObjectPointDefinition x, LevelWorldObjectPointDefinition y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            int group = string.CompareOrdinal(x.Group ?? string.Empty, y.Group ?? string.Empty);
            if (group != 0)
                return group;
            int kind = x.PointKind.CompareTo(y.PointKind);
            if (kind != 0)
                return kind;
            int definition = x.DefinitionId.CompareTo(y.DefinitionId);
            if (definition != 0)
                return definition;
            return string.CompareOrdinal(
                x.TransformRef?.Path ?? x.TransformPath ?? string.Empty,
                y.TransformRef?.Path ?? y.TransformPath ?? string.Empty);
        }
    }

    internal sealed class LevelWorldObjectRegionDefinitionComparer :
        IComparer<LevelWorldObjectRegionDefinition>
    {
        public static readonly LevelWorldObjectRegionDefinitionComparer Instance =
            new LevelWorldObjectRegionDefinitionComparer();

        public int Compare(LevelWorldObjectRegionDefinition x, LevelWorldObjectRegionDefinition y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            int group = string.CompareOrdinal(x.Group ?? string.Empty, y.Group ?? string.Empty);
            if (group != 0)
                return group;
            int kind = x.RegionKind.CompareTo(y.RegionKind);
            if (kind != 0)
                return kind;
            int definition = x.DefinitionId.CompareTo(y.DefinitionId);
            if (definition != 0)
                return definition;
            return string.CompareOrdinal(
                x.TransformRef?.Path ?? x.TransformPath ?? string.Empty,
                y.TransformRef?.Path ?? y.TransformPath ?? string.Empty);
        }
    }
}
