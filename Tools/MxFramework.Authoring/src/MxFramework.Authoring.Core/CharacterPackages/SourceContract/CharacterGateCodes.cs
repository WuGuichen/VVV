using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 门禁发生阶段。定义见规范 v1.4 第 7.1 节。
    /// </summary>
    public enum CharacterGatePhase
    {
        Unknown = 0,
        Preflight = 1,
        Snapshot = 2,
        CoreValidation = 3,
        WriteExecution = 4
    }

    /// <summary>
    /// 门禁严重性。定义见规范 v1.4 第 7.1 节。
    /// </summary>
    public enum CharacterGateSeverity
    {
        Unknown = 0,
        Fatal = 1,
        Error = 2
    }

    /// <summary>
    /// 门禁阻断的目标操作。定义见规范 v1.4 第 7.1 节。
    /// </summary>
    public enum CharacterGateTargetAction
    {
        Unknown = 0,

        /// <summary>阻断全部后续阶段。</summary>
        BlockAll = 1,

        /// <summary>阻断导出产物生成。</summary>
        BlockExport = 2,

        /// <summary>阻断运行时生成。</summary>
        BlockSpawn = 3,

        /// <summary>阻断写入 Unity 工程。</summary>
        BlockImport = 4
    }

    /// <summary>
    /// 单条门禁定义。
    /// </summary>
    public sealed class CharacterGateDefinition
    {
        public CharacterGateDefinition(
            string code,
            CharacterGatePhase phase,
            CharacterGateSeverity severity,
            CharacterGateTargetAction targetAction,
            string triggerCondition,
            bool declaredBySpec71 = true)
        {
            Code = code ?? string.Empty;
            Phase = phase;
            Severity = severity;
            TargetAction = targetAction;
            TriggerCondition = triggerCondition ?? string.Empty;
            DeclaredBySpec71 = declaredBySpec71;
        }

        /// <summary>门禁代码，例如 <c>E-GEO-303</c>。</summary>
        public string Code { get; }

        public CharacterGatePhase Phase { get; }

        public CharacterGateSeverity Severity { get; }

        public CharacterGateTargetAction TargetAction { get; }

        /// <summary>详细触发条件。</summary>
        public string TriggerCondition { get; }

        /// <summary>
        /// 该代码是否在规范 v1.4 第 7.1 节字典中显式声明。
        /// 为 false 表示该代码仅在规范第 1.1 节被引用，第 7.1 节遗漏，
        /// 此处按同组兄弟门禁（E-SRC-101 / E-SRC-109）的相位与严重性补齐，待规范确认。
        /// </summary>
        public bool DeclaredBySpec71 { get; }

        /// <summary>是否阻断全部后续阶段。</summary>
        public bool BlocksAll => TargetAction == CharacterGateTargetAction.BlockAll;

        /// <summary>是否为致命级门禁。</summary>
        public bool IsFatal => Severity == CharacterGateSeverity.Fatal;
    }

    /// <summary>
    /// 规范 v1.4 第 7.1 节门禁字典的代码化表示。
    /// 该类型是门禁码的唯一权威来源：任何产生门禁诊断的代码都必须通过
    /// <see cref="Resolve"/> 取得定义，禁止在调用点硬编码相位与严重性。
    /// </summary>
    public static class CharacterGateCodes
    {
        public const string ManifestSchemaInvalid = "E-SRC-101";
        public const string ResourceCatalogMissing = "E-SRC-102";
        public const string BodyCollidersMissing = "E-SRC-103";
        public const string BodyPartsMissing = "E-SRC-104";
        public const string ApplicationConfigMissing = "E-SRC-105";
        public const string AttributesConfigMissing = "E-SRC-106";
        public const string ActionsConfigMissing = "E-SRC-107";
        public const string BaseMeshMissing = "E-SRC-108";
        public const string SkeletonHierarchyMissing = "E-SRC-109";

        public const string SkeletonSourceMeshHashMismatch = "E-SKEL-101";

        public const string RequiredAssetPhysicallyMissing = "E-SRC-201";

        public const string ResourceCatalogDuplicateKey = "E-CAT-201";

        public const string WeaponSlotSocketDangling = "E-SLOT-301";

        public const string GeometryOutOfRange = "E-GEO-301";
        public const string CapsuleHeightBelowDiameter = "E-GEO-302";
        public const string LegacySizeDeviationExceeded = "E-GEO-303";

        public const string BonePathUnresolvedWithoutSemantic = "E-BONE-201";
        public const string BonePathSemanticConflict = "E-BONE-202";

        public const string DamageZoneConfigMissingOrDangling = "E-DMG-401";

        public const string PathTraversalOrSymbolicLink = "E-SEC-701";

        public const string CreateFileExistsWithoutOverwrite = "E-PLAN-401";
        public const string WritePlanCircularDependency = "E-PLAN-501";

        private static readonly CharacterGateDefinition[] Definitions = BuildDefinitions();

        private static readonly Dictionary<string, CharacterGateDefinition> Index = BuildIndex();

        /// <summary>全部门禁定义，按代码字典序。</summary>
        public static IReadOnlyList<CharacterGateDefinition> All => Definitions;

        /// <summary>
        /// 按代码解析门禁定义。未知代码抛出 <see cref="ArgumentException"/>，
        /// 以保证新增门禁码必须先登记到本字典。
        /// </summary>
        public static CharacterGateDefinition Resolve(string code)
        {
            if (string.IsNullOrEmpty(code) || !Index.TryGetValue(code, out CharacterGateDefinition definition))
            {
                throw new ArgumentException(
                    $"Unknown character gate code '{code}'. Register it in {nameof(CharacterGateCodes)} before use.",
                    nameof(code));
            }

            return definition;
        }

        public static bool TryResolve(string code, out CharacterGateDefinition definition)
        {
            definition = null;
            return !string.IsNullOrEmpty(code) && Index.TryGetValue(code, out definition);
        }

        /// <summary>把门禁目标操作映射到既有的校验门枚举，供旧诊断通道复用。</summary>
        public static CharacterAuthoringValidationGate ToLegacyGate(CharacterGateTargetAction targetAction)
        {
            switch (targetAction)
            {
                case CharacterGateTargetAction.BlockExport:
                    return CharacterAuthoringValidationGate.ExportBlocked;
                case CharacterGateTargetAction.BlockSpawn:
                    return CharacterAuthoringValidationGate.SpawnBlocked;
                case CharacterGateTargetAction.BlockImport:
                    return CharacterAuthoringValidationGate.ImportBlocked;
                case CharacterGateTargetAction.BlockAll:
                    return CharacterAuthoringValidationGate.ExportBlocked;
                default:
                    return CharacterAuthoringValidationGate.WarningOnly;
            }
        }

        /// <summary>把门禁严重性映射到既有的校验严重性枚举。</summary>
        public static CharacterAuthoringValidationSeverity ToLegacySeverity(CharacterGateSeverity severity)
        {
            return severity == CharacterGateSeverity.Fatal
                ? CharacterAuthoringValidationSeverity.Error
                : CharacterAuthoringValidationSeverity.Error;
        }

        /// <summary>构造一条可接入既有诊断通道的门禁问题记录。</summary>
        public static CharacterAuthoringValidationIssue CreateIssue(
            string code,
            string sourcePath,
            string sourceObjectPath,
            string field,
            string message,
            string suggestedFix = "")
        {
            CharacterGateDefinition definition = Resolve(code);
            return new CharacterAuthoringValidationIssue
            {
                Severity = ToLegacySeverity(definition.Severity),
                Gate = ToLegacyGate(definition.TargetAction),
                Code = definition.Code,
                SourcePath = sourcePath ?? string.Empty,
                SourceObjectPath = sourceObjectPath ?? string.Empty,
                Field = field ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static CharacterGateDefinition[] BuildDefinitions()
        {
            var definitions = new List<CharacterGateDefinition>
            {
                new CharacterGateDefinition(
                    ManifestSchemaInvalid,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "manifest.json 结构不符合 Draft-07 Schema"),

                new CharacterGateDefinition(
                    ResourceCatalogMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "resource_catalog.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    BodyCollidersMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "geometry/body_colliders.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    BodyPartsMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "geometry/body_parts.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    ApplicationConfigMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "config/character_application.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    AttributesConfigMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "config/character_attributes.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    ActionsConfigMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "config/character_actions.json 缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    BaseMeshMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "基础网格 FBX（resources/models/*.fbx）缺失",
                    declaredBySpec71: false),

                new CharacterGateDefinition(
                    SkeletonHierarchyMissing,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "skeleton_hierarchy.json 缺失，Core 无法进行骨骼校验"),

                new CharacterGateDefinition(
                    SkeletonSourceMeshHashMismatch,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockAll,
                    "skeleton_hierarchy 的源网格哈希与模型 FBX 哈希不匹配"),

                new CharacterGateDefinition(
                    RequiredAssetPhysicallyMissing,
                    CharacterGatePhase.Snapshot,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockAll,
                    "manifest 声明的必需资产在快照捕获时物理缺失"),

                new CharacterGateDefinition(
                    ResourceCatalogDuplicateKey,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockAll,
                    "resource_catalog.json 中出现重复的 resourceKey"),

                new CharacterGateDefinition(
                    WeaponSlotSocketDangling,
                    CharacterGatePhase.Preflight,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockAll,
                    "weapon_slots.json 引用的 attachSocketId 不存在"),

                new CharacterGateDefinition(
                    GeometryOutOfRange,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockExport,
                    "尺寸全长超出安全阈值 [0.005, 10.0] 或非有限浮点数"),

                new CharacterGateDefinition(
                    CapsuleHeightBelowDiameter,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockExport,
                    "胶囊体总高度严格小于两倍半径"),

                new CharacterGateDefinition(
                    LegacySizeDeviationExceeded,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockExport,
                    "遗留 size 全三维尺寸与权威几何偏差超过 10 mm"),

                new CharacterGateDefinition(
                    BonePathUnresolvedWithoutSemantic,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockSpawn,
                    "骨骼路径无法在骨架树解析，且未配置 Humanoid 语义备用锚点"),

                new CharacterGateDefinition(
                    BonePathSemanticConflict,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockExport,
                    "骨骼路径指向骨骼 A 与 Humanoid 语义指向骨骼 B 发生冲突"),

                new CharacterGateDefinition(
                    DamageZoneConfigMissingOrDangling,
                    CharacterGatePhase.CoreValidation,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockSpawn,
                    "伤害战术区域启用但配置文件缺失或命中区 ID 悬空"),

                new CharacterGateDefinition(
                    PathTraversalOrSymbolicLink,
                    CharacterGatePhase.WriteExecution,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "目标路径包含 .. 越界逃逸或检测到符号链接"),

                new CharacterGateDefinition(
                    CreateFileExistsWithoutOverwrite,
                    CharacterGatePhase.WriteExecution,
                    CharacterGateSeverity.Error,
                    CharacterGateTargetAction.BlockImport,
                    "创建文件已存在且未指定覆写策略"),

                new CharacterGateDefinition(
                    WritePlanCircularDependency,
                    CharacterGatePhase.WriteExecution,
                    CharacterGateSeverity.Fatal,
                    CharacterGateTargetAction.BlockAll,
                    "WritePlan 操作依赖存在拓扑环")
            };

            definitions.Sort((left, right) => string.CompareOrdinal(left.Code, right.Code));
            return definitions.ToArray();
        }

        private static Dictionary<string, CharacterGateDefinition> BuildIndex()
        {
            var index = new Dictionary<string, CharacterGateDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < Definitions.Length; i++)
            {
                index[Definitions[i].Code] = Definitions[i];
            }

            return index;
        }
    }
}
