using System;
using System.Globalization;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>
    /// CharacterTest 关卡编辑器的中文展示映射。
    /// 仅用于低频 Editor UI；未知值保留原文，不参与任何序列化或 hash 计算。
    /// </summary>
    internal static class CharacterTestLevelEditorChineseText
    {
        private const string UnknownDiagnosticMessage = "未翻译的校验问题。";

        public static string DefinitionName(
            int definitionId,
            string fallback = null)
        {
            switch (definitionId)
            {
                case CharacterTestWorldObjectDefinitionIds.MovableSlickLight:
                    return "轻型滑动物";
                case CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy:
                    return "粗糙重物";
                case CharacterTestWorldObjectDefinitionIds.MovableElasticSeed:
                    return "弹性种子";
                case CharacterTestWorldObjectDefinitionIds.MovableContactA:
                    return "接触物 A";
                case CharacterTestWorldObjectDefinitionIds.MovableContactB:
                    return "接触物 B";
                case CharacterTestWorldObjectDefinitionIds.MovableSphere:
                    return "球体";
                case CharacterTestWorldObjectDefinitionIds.MovableCapsule:
                    return "胶囊体";
                case CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary:
                    return "默认边界";
                case CharacterTestWorldObjectDefinitionIds.StaticRoughBoundary:
                    return "粗糙边界";
                case CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface:
                    return "硬滑表面";
                case CharacterTestWorldObjectDefinitionIds.StaticRoughSurface:
                    return "粗糙表面";
                case CharacterTestWorldObjectDefinitionIds.StaticElasticSurface:
                    return "弹性表面";
                case CharacterTestWorldObjectDefinitionIds.StaticBouncePadSurface:
                    return "弹跳垫表面";
                case CharacterTestWorldObjectDefinitionIds.StaticSphereProbe:
                    return "静态球体探针";
                case CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe:
                    return "静态胶囊体探针";
                case CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn:
                    return "玩家出生点";
                case CharacterTestWorldObjectDefinitionIds.PointAiSpawn:
                    return "Runtime AI Planner 出生点";
                case CharacterTestWorldObjectDefinitionIds.PointAiSpawnNearby:
                    return "附近 Runtime AI Planner 出生点";
                case CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint:
                    return "验证检查点";
                case CharacterTestWorldObjectDefinitionIds.PointValidationMarker:
                    return "验证标记点";
                case CharacterTestWorldObjectDefinitionIds.RegionStartArea:
                    return "起始区域";
                case CharacterTestWorldObjectDefinitionIds.RegionTestCourse:
                    return "测试路线区域";
                default:
                    return fallback ?? definitionId.ToString();
            }
        }

        public static string DefinitionLabel(
            CharacterTestWorldObjectDefinition definition)
        {
            return definition.DefinitionId
                + " "
                + DefinitionName(definition.DefinitionId, definition.DisplayName);
        }

        public static string EffectAttributionName(
            long sourceId,
            string fallback = null)
        {
            switch (sourceId)
            {
                case CharacterTestGuardAuraIds.AuraId:
                    return "守护光环";
                case CharacterTestAreaConfinementIds.MechanicSourceId:
                    return "区域禁锢";
                case CharacterTestPoisonIds.PoisonBottlePoolSourceId:
                    return "毒瓶池";
                default:
                    return fallback ?? sourceId.ToString();
            }
        }

        public static string BuffName(
            int buffId,
            string fallback = null)
        {
            switch (buffId)
            {
                case CharacterTestGuardAuraIds.BuffId:
                    return "守护光环";
                case CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId:
                    return "防御削弱";
                case CharacterTestAreaConfinementIds.RootedBuffId:
                    return "区域禁锢";
                case CharacterTestStasisIds.StasisBuffId:
                    return "停滞";
                case CharacterTestPoisonIds.PoisonedBuffId:
                    return "中毒";
                case CharacterTestStunIds.StunBuffId:
                    return "眩晕";
                default:
                    return fallback ?? buffId.ToString(
                        CultureInfo.InvariantCulture);
            }
        }

        public static string BuffGameplaySummary(
            int buffId,
            string fallback = null)
        {
            switch (buffId)
            {
                case CharacterTestGuardAuraIds.BuffId:
                    return "防御提高 "
                        + CharacterTestGuardAuraIds.DefenseBonus.ToString(
                            CultureInfo.InvariantCulture);
                case CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId:
                    return "防御降低 "
                        + Math.Abs(
                            CharacterTestGuardAuraIds.OnHitDefensePenalty)
                            .ToString(CultureInfo.InvariantCulture);
                case CharacterTestAreaConfinementIds.RootedBuffId:
                    return "限制主动移动，仍可转向并受外力影响";
                case CharacterTestStasisIds.StasisBuffId:
                    return "暂停自主动作、移动和转向，仍受外力影响";
                case CharacterTestPoisonIds.PoisonedBuffId:
                    return "每 "
                        + FormatFrameDurationSeconds(
                            CharacterTestPoisonIds
                                .PoisonedDamageTickIntervalFrames)
                        + "造成 "
                        + CharacterTestPoisonIds.PoisonedDamagePerTick
                            .ToString(CultureInfo.InvariantCulture)
                        + " 点毒素伤害，并附加“中毒”状态";
                case CharacterTestStunIds.StunBuffId:
                    return "无法移动、转向或开始动作";
                default:
                    return fallback ?? string.Empty;
            }
        }

        private static string FormatFrameDurationSeconds(long frames)
        {
            double seconds = frames
                / (double)CharacterTestDeterministicUnits.DefaultTickRate;
            string format = Math.Abs(seconds - Math.Round(seconds)) < 0.0001d
                ? "0"
                : "0.##";
            return seconds.ToString(format, CultureInfo.InvariantCulture)
                + " 秒";
        }

        public static string Role(LevelWorldObjectRole value)
        {
            switch (value)
            {
                case LevelWorldObjectRole.Unknown:
                    return "未指定";
                case LevelWorldObjectRole.Movable:
                    return "可移动物体";
                case LevelWorldObjectRole.StaticCollider:
                    return "静态碰撞体";
                default:
                    return value.ToString();
            }
        }

        public static string Usage(LevelAuthoringDefinitionUsage value)
        {
            switch (value)
            {
                case LevelAuthoringDefinitionUsage.Unknown:
                    return "未指定";
                case LevelAuthoringDefinitionUsage.Movable:
                    return "可移动物体";
                case LevelAuthoringDefinitionUsage.StaticCollider:
                    return "静态碰撞体";
                case LevelAuthoringDefinitionUsage.Point:
                    return "关卡点";
                case LevelAuthoringDefinitionUsage.Region:
                    return "区域";
                default:
                    return value.ToString();
            }
        }

        public static string ScaleSource(LevelWorldObjectScaleSource value)
        {
            switch (value)
            {
                case LevelWorldObjectScaleSource.SceneTransform:
                    return "场景 Transform";
                case LevelWorldObjectScaleSource.Definition:
                    return "对象定义";
                default:
                    return value.ToString();
            }
        }

        public static string PointKind(LevelWorldObjectPointKind value)
        {
            switch (value)
            {
                case LevelWorldObjectPointKind.Unknown:
                    return "未指定";
                case LevelWorldObjectPointKind.PlayerSpawn:
                    return "玩家出生点";
                case LevelWorldObjectPointKind.AiSpawn:
                    return "Runtime AI Planner 出生点";
                case LevelWorldObjectPointKind.Checkpoint:
                    return "检查点";
                case LevelWorldObjectPointKind.Marker:
                    return "标记点";
                default:
                    return value.ToString();
            }
        }

        public static string RegionKind(LevelWorldObjectRegionKind value)
        {
            switch (value)
            {
                case LevelWorldObjectRegionKind.Unknown:
                    return "未指定";
                case LevelWorldObjectRegionKind.TestRegion:
                    return "测试区域";
                case LevelWorldObjectRegionKind.TriggerRegion:
                    return "触发区域";
                default:
                    return value.ToString();
            }
        }

        public static string Shape(CharacterTestMovableNaturalObjectShapeKind value)
        {
            switch (value)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Aabb:
                    return "AABB 盒体";
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    return "球体";
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return "胶囊体";
                default:
                    return value.ToString();
            }
        }

        public static string Material(CombatSurfaceMaterialKind value)
        {
            switch (value)
            {
                case CombatSurfaceMaterialKind.Default:
                    return "默认材质";
                case CombatSurfaceMaterialKind.HardSlick:
                    return "硬滑材质";
                case CombatSurfaceMaterialKind.Rough:
                    return "粗糙材质";
                case CombatSurfaceMaterialKind.Elastic:
                    return "弹性材质";
                case CombatSurfaceMaterialKind.BouncePad:
                    return "弹跳垫材质";
                default:
                    return value.ToString();
            }
        }

        public static string Severity(LevelWorldObjectAuthoringSeverity value)
        {
            switch (value)
            {
                case LevelWorldObjectAuthoringSeverity.Info:
                    return "信息";
                case LevelWorldObjectAuthoringSeverity.Warning:
                    return "警告";
                case LevelWorldObjectAuthoringSeverity.Error:
                    return "错误";
                default:
                    return value.ToString();
            }
        }

        public static string EnumValue(Enum value)
        {
            if (value is LevelWorldObjectRole role)
                return Role(role);
            if (value is LevelWorldObjectScaleSource scaleSource)
                return ScaleSource(scaleSource);
            if (value is LevelWorldObjectPointKind pointKind)
                return PointKind(pointKind);
            if (value is LevelWorldObjectRegionKind regionKind)
                return RegionKind(regionKind);
            if (value is LevelWorldObjectAuthoringSeverity severity)
                return Severity(severity);
            if (value is LevelAuthoringDefinitionUsage usage)
                return Usage(usage);
            if (value is CharacterTestMovableNaturalObjectShapeKind shape)
                return Shape(shape);
            if (value is CombatSurfaceMaterialKind material)
                return Material(material);
            return value?.ToString() ?? string.Empty;
        }

        public static string DiagnosticMessage(string code)
        {
            switch (code)
            {
                case "MissingDocument":
                    return "缺少关卡制作文档。";
                case "UnsupportedFormat":
                    return "文档格式不受支持。";
                case "UnsupportedSchemaVersion":
                    return "文档 schema 版本不受支持。";
                case "MissingLevelId":
                    return "缺少关卡 ID。";
                case "MissingScenePath":
                    return "缺少场景路径。";
                case "MissingRootPath":
                case "MissingRootTransformRef":
                    return "缺少根对象路径。";
                case "RootPathMismatch":
                    return "文档根对象路径与当前场景不匹配。";
                case "MissingModuleId":
                    return "缺少模块 ID。";
                case "DuplicateModuleId":
                    return "模块 ID 重复。";
                case "MissingModulePayload":
                    return "缺少模块数据。";
                case "MissingWorldObjectsModule":
                    return "缺少 CharacterTest 世界对象模块。";
                case "InvalidWorldObjectsModulePayload":
                    return "CharacterTest 世界对象模块数据无法读取。";
                case "MissingCharactersModule":
                    return "缺少 CharacterTest 场景角色模块。";
                case "InvalidCharactersModulePayload":
                    return "CharacterTest 场景角色模块数据无法读取。";
                case "MissingStableId":
                    return "缺少稳定 ID。";
                case "DuplicateStableId":
                    return "稳定 ID 重复。";
                case "InvalidCharacterId":
                    return "角色 ID 无效。";
                case "CharacterDefinitionNotFound":
                    return "找不到角色定义。";
                case "InvalidTeamId":
                    return "队伍 ID 无效。";
                case "InvalidControlOwner":
                    return "控制来源无效。";
                case "MissingLocalPlayer":
                    return "没有配置已启用的本地玩家角色。";
                case "MissingAiControlled":
                    return "没有配置已启用的 Runtime AI Planner 控制角色。";
                case "MissingTransformPath":
                    return "缺少场景对象路径。";
                case "MissingContainerTransformPath":
                    return "缺少批次容器路径。";
                case "InvalidRole":
                    return "对象类型无效。";
                case "InvalidDefinitionId":
                case "DefinitionNotFound":
                    return "对象定义无效或不存在。";
                case "DefinitionRoleMismatch":
                    return "对象定义与当前类型不匹配。";
                case "InvalidMovableNumericId":
                    return "可移动物体 ID 无效。";
                case "InvalidStaticBodyId":
                    return "刚体 ID 无效。";
                case "InvalidStaticBodyIdStart":
                    return "起始刚体 ID 无效。";
                case "DuplicateTransformPath":
                    return "场景对象路径重复。";
                case "DuplicateMovableNumericId":
                    return "可移动物体 ID 重复。";
                case "DuplicateStaticBodyColliderId":
                    return "刚体 ID 与碰撞体 ID 组合重复。";
                case "DuplicatePointStableId":
                    return "关卡点稳定 ID 重复。";
                case "DuplicateRegionStableId":
                    return "区域稳定 ID 重复。";
                case "TransformNotFound":
                    return "场景对象无法解析。";
                default:
                    return UnknownDiagnosticMessage;
            }
        }

        public static string DiagnosticText(LevelWorldObjectAuthoringIssue issue)
        {
            if (issue == null)
                return string.Empty;

            string path = string.IsNullOrWhiteSpace(issue.Path)
                ? "(文档)"
                : issue.Path;
            string message = DiagnosticMessage(issue.Code);
            if (string.Equals(message, UnknownDiagnosticMessage, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(issue.Message))
            {
                message += " 原始信息：" + issue.Message;
            }

            return Severity(issue.Severity)
                + " · "
                + issue.Code
                + " · "
                + path
                + "："
                + message;
        }

        public static string DiagnosticTooltip(LevelWorldObjectAuthoringIssue issue)
        {
            if (issue == null)
                return string.Empty;

            return issue.Severity
                + " "
                + issue.Code
                + " "
                + (issue.Path ?? string.Empty)
                + Environment.NewLine
                + (issue.Message ?? string.Empty);
        }
    }
}
