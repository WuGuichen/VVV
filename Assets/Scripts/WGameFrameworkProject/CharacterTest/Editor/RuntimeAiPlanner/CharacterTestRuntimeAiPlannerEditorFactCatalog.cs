using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerEditorFactValueType
    {
        Bool,
        Float,
        Int,
        SurfaceKind
    }

    internal readonly struct CharacterTestRuntimeAiPlannerEditorFactMetadata
    {
        private readonly CharacterTestRuntimeAiPlannerConditionOperator[] _operators;

        public CharacterTestRuntimeAiPlannerEditorFactMetadata(
            CharacterTestRuntimeAiPlannerFactKind fact,
            string category,
            string displayName,
            string key,
            string description,
            CharacterTestRuntimeAiPlannerEditorFactValueType valueType,
            CharacterTestRuntimeAiPlannerConditionOperator[] operators,
            bool defaultBool,
            float defaultFloat)
        {
            Fact = fact;
            Category = category ?? string.Empty;
            DisplayName = displayName ?? fact.ToString();
            Key = key ?? fact.ToString();
            Description = description ?? string.Empty;
            ValueType = valueType;
            _operators = operators ?? Array.Empty<CharacterTestRuntimeAiPlannerConditionOperator>();
            DefaultBool = defaultBool;
            DefaultFloat = defaultFloat;
        }

        public CharacterTestRuntimeAiPlannerFactKind Fact { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Key { get; }
        public string Description { get; }
        public CharacterTestRuntimeAiPlannerEditorFactValueType ValueType { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerConditionOperator> Operators => _operators;
        public bool DefaultBool { get; }
        public float DefaultFloat { get; }
        public string ChoiceLabel => Category + " / " + DisplayName + " (" + Key + ")";
    }

    internal enum CharacterTestRuntimeAiPlannerConditionTemplate
    {
        TargetVisible,
        TargetBlocked,
        TargetRemembered,
        InAttackRange,
        ActionNotRunning,
        NearHazard,
        NearUsefulObject
    }

    internal static class CharacterTestRuntimeAiPlannerEditorFactCatalog
    {
        private static readonly CharacterTestRuntimeAiPlannerConditionOperator[] BoolOperators =
        {
            CharacterTestRuntimeAiPlannerConditionOperator.Equals,
            CharacterTestRuntimeAiPlannerConditionOperator.NotEquals
        };

        private static readonly CharacterTestRuntimeAiPlannerConditionOperator[] ComparableOperators =
        {
            CharacterTestRuntimeAiPlannerConditionOperator.Equals,
            CharacterTestRuntimeAiPlannerConditionOperator.NotEquals,
            CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual,
            CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual
        };

        private static readonly CharacterTestRuntimeAiPlannerEditorFactMetadata[] Facts =
        {
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetExists, "目标", "目标存在", "target.exists", "是否找到可决策目标。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, "目标", "目标可见", "target.visible", "目标在视野内且没有被遮挡。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, "目标", "目标被遮挡", "target.lineOfSightBlocked", "视线或直连路径被障碍阻挡。", true),
            Int(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, "目标", "上次看见帧数", "target.lastSeenAgeFrames", "目标离开视线后的记忆帧数。", CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultTargetMemoryFrames),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable, "目标", "受击框距离可用", "target.distanceAvailable", "锁定受击框的完成帧几何距离是否可用。", true),
            Float(CharacterTestRuntimeAiPlannerFactKind.TargetDistance, "目标", "锁定受击框净空", "target.distance", "自身姿态胶囊到锁定受击框表面的三维净空。", CharacterTestAiBehaviorProfileSource.DefaultAttackRange),
            Float(CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance, "目标", "目标根距离", "target.rootDistance", "两个角色 Transform 根节点之间的 XZ 距离。", CharacterTestAiBehaviorProfileSource.DefaultAttackRange),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, "目标", "进入攻击距离", "target.inAttackRange", "目标是否已经进入攻击范围。", true),
            Float(CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore, "目标", "威胁评分", "target.threatScore", "按距离、可见性和记忆计算的简单威胁评分。", 0f),
            Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, "自身", "动作执行中", "self.actionRunning", "自身是否正在执行不可打断动作。", false),
            Surface(CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind, "自身", "当前地表", "self.surfaceKind", "当前地表材质枚举。", CombatSurfaceMaterialKind.Default),
            Bool(CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick, "自身", "位于硬滑地表", "self.onHardSlick", "自身是否站在 HardSlick 地表上。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject, "自身", "靠近可移动物体", "self.nearMovableObject", "附近是否存在可移动自然物体。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, "移动", "有可用路径", "move.pathAvailable", "当前是否有可用移动方向。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear, "移动", "直线路径无阻挡", "move.directPathClear", "目标方向上的直线路径是否清晰。", true),
            Int(CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex, "移动", "绕行候选索引", "move.candidateIndex", "当前选择的绕行方向候选索引。", 0),
            Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle, "环境", "靠近障碍", "environment.nearObstacle", "附近是否存在静态障碍。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard, "环境", "靠近危险", "environment.nearHazard", "附近是否存在危险环境物。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, "环境", "靠近可利用物", "environment.nearUsefulObject", "附近是否存在可利用环境物。", true),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetAttackWillHitSelf, "目标", "攻击将命中自身", "target.attackWillHitSelf", "目标当前攻击是否预计会命中自身。", true),
            Int(CharacterTestRuntimeAiPlannerFactKind.TargetAttackImpactFrames, "目标", "攻击命中剩余帧", "target.attackImpactFrames", "目标当前攻击距离预计命中的剩余帧数。", 0),
            Bool(CharacterTestRuntimeAiPlannerFactKind.TargetAttackInstanceResponded, "目标", "当前攻击已响应", "target.attackInstanceResponded", "自身是否已经响应目标的当前攻击实例。", false),
            Bool(CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady, "自身", "闪避冷却就绪", "self.dodgeCooldownReady", "自身的闪避冷却是否已经就绪。", true)
        };

        private static readonly CombatSurfaceMaterialKind[] SurfaceKinds =
        {
            CombatSurfaceMaterialKind.Default,
            CombatSurfaceMaterialKind.HardSlick,
            CombatSurfaceMaterialKind.Rough,
            CombatSurfaceMaterialKind.Elastic,
            CombatSurfaceMaterialKind.BouncePad
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerEditorFactMetadata> All => Facts;
        public static int Count => Facts.Length;

        public static bool TryGet(
            CharacterTestRuntimeAiPlannerFactKind fact,
            out CharacterTestRuntimeAiPlannerEditorFactMetadata metadata)
        {
            for (int i = 0; i < Facts.Length; i++)
            {
                if (Facts[i].Fact == fact)
                {
                    metadata = Facts[i];
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        public static CharacterTestRuntimeAiPlannerEditorFactMetadata Get(
            CharacterTestRuntimeAiPlannerFactKind fact)
        {
            return TryGet(fact, out CharacterTestRuntimeAiPlannerEditorFactMetadata metadata)
                ? metadata
                : Facts[0];
        }

        public static List<string> CreateFactChoices()
        {
            var choices = new List<string>(Facts.Length);
            for (int i = 0; i < Facts.Length; i++)
                choices.Add(Facts[i].ChoiceLabel);
            return choices;
        }

        public static bool TryGetFactFromChoice(
            string choice,
            out CharacterTestRuntimeAiPlannerFactKind fact)
        {
            for (int i = 0; i < Facts.Length; i++)
            {
                if (string.Equals(Facts[i].ChoiceLabel, choice, StringComparison.Ordinal))
                {
                    fact = Facts[i].Fact;
                    return true;
                }
            }

            fact = default;
            return false;
        }

        public static string GetFactChoice(CharacterTestRuntimeAiPlannerFactKind fact)
        {
            return Get(fact).ChoiceLabel;
        }

        public static List<string> CreateOperatorChoices(
            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata)
        {
            var choices = new List<string>(metadata.Operators.Count);
            for (int i = 0; i < metadata.Operators.Count; i++)
                choices.Add(GetOperatorLabel(metadata.Operators[i]));
            return choices;
        }

        public static string GetOperatorLabel(CharacterTestRuntimeAiPlannerConditionOperator op)
        {
            switch (op)
            {
                case CharacterTestRuntimeAiPlannerConditionOperator.NotEquals:
                    return "不等于 (!=)";
                case CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual:
                    return "大于等于 (>=)";
                case CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual:
                    return "小于等于 (<=)";
                default:
                    return "等于 (==)";
            }
        }

        public static bool TryGetOperatorFromLabel(
            string label,
            out CharacterTestRuntimeAiPlannerConditionOperator op)
        {
            for (int i = 0; i < BoolOperators.Length; i++)
            {
                if (string.Equals(GetOperatorLabel(BoolOperators[i]), label, StringComparison.Ordinal))
                {
                    op = BoolOperators[i];
                    return true;
                }
            }

            for (int i = 0; i < ComparableOperators.Length; i++)
            {
                if (string.Equals(GetOperatorLabel(ComparableOperators[i]), label, StringComparison.Ordinal))
                {
                    op = ComparableOperators[i];
                    return true;
                }
            }

            op = default;
            return false;
        }

        public static bool IsOperatorAllowed(
            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata,
            CharacterTestRuntimeAiPlannerConditionOperator op)
        {
            for (int i = 0; i < metadata.Operators.Count; i++)
            {
                if (metadata.Operators[i] == op)
                    return true;
            }

            return false;
        }

        public static void NormalizeCondition(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            if (condition == null)
                return;

            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata = Get(condition.Fact);
            if (!IsOperatorAllowed(metadata, condition.Operator))
                condition.Operator = metadata.Operators.Count > 0
                    ? metadata.Operators[0]
                    : CharacterTestRuntimeAiPlannerConditionOperator.Equals;
        }

        public static void ApplyFactDefault(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            if (condition == null)
                return;

            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata = Get(condition.Fact);
            condition.Operator = metadata.Operators.Count > 0
                ? metadata.Operators[0]
                : CharacterTestRuntimeAiPlannerConditionOperator.Equals;
            condition.BoolValue = metadata.DefaultBool;
            condition.FloatValue = metadata.DefaultFloat;
        }

        public static void ApplyTemplate(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            CharacterTestRuntimeAiPlannerConditionTemplate template,
            int memoryFrames)
        {
            if (condition == null)
                return;

            switch (template)
            {
                case CharacterTestRuntimeAiPlannerConditionTemplate.TargetBlocked:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true, 0f);
                    break;
                case CharacterTestRuntimeAiPlannerConditionTemplate.TargetRemembered:
                    condition.Fact = CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames;
                    condition.Operator = CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual;
                    condition.BoolValue = true;
                    condition.FloatValue = memoryFrames < 0 ? 0 : memoryFrames;
                    break;
                case CharacterTestRuntimeAiPlannerConditionTemplate.InAttackRange:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true, 0f);
                    break;
                case CharacterTestRuntimeAiPlannerConditionTemplate.ActionNotRunning:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false, 0f);
                    break;
                case CharacterTestRuntimeAiPlannerConditionTemplate.NearHazard:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard, true, 0f);
                    break;
                case CharacterTestRuntimeAiPlannerConditionTemplate.NearUsefulObject:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true, 0f);
                    break;
                default:
                    Apply(condition, CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true, 0f);
                    break;
            }
        }

        public static List<string> CreateSurfaceChoices()
        {
            var choices = new List<string>(SurfaceKinds.Length);
            for (int i = 0; i < SurfaceKinds.Length; i++)
                choices.Add(FormatSurfaceKind((int)SurfaceKinds[i]));
            return choices;
        }

        public static bool TryGetSurfaceKindFromChoice(string choice, out int value)
        {
            for (int i = 0; i < SurfaceKinds.Length; i++)
            {
                int numericValue = (int)SurfaceKinds[i];
                if (string.Equals(FormatSurfaceKind(numericValue), choice, StringComparison.Ordinal))
                {
                    value = numericValue;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        public static string FormatSurfaceKind(int value)
        {
            CombatSurfaceMaterialKind kind = (CombatSurfaceMaterialKind)value;
            switch (kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                    return "硬滑 HardSlick (1)";
                case CombatSurfaceMaterialKind.Rough:
                    return "粗糙 Rough (2)";
                case CombatSurfaceMaterialKind.Elastic:
                    return "弹性 Elastic (3)";
                case CombatSurfaceMaterialKind.BouncePad:
                    return "弹跳 BouncePad (4)";
                default:
                    return "默认 Default (0)";
            }
        }

        public static string FormatCondition(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            if (condition == null)
                return "condition=none";

            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata = Get(condition.Fact);
            string value;
            switch (metadata.ValueType)
            {
                case CharacterTestRuntimeAiPlannerEditorFactValueType.Bool:
                    value = condition.BoolValue ? "true" : "false";
                    break;
                case CharacterTestRuntimeAiPlannerEditorFactValueType.Int:
                    value = ((int)Math.Round(condition.FloatValue)).ToString();
                    break;
                case CharacterTestRuntimeAiPlannerEditorFactValueType.SurfaceKind:
                    value = FormatSurfaceKind((int)Math.Round(condition.FloatValue));
                    break;
                default:
                    value = condition.FloatValue.ToString("0.###");
                    break;
            }

            return metadata.DisplayName + " (" + metadata.Key + ") "
                + GetOperatorLabel(condition.Operator) + " " + value;
        }

        public static string GetTemplateLabel(CharacterTestRuntimeAiPlannerConditionTemplate template)
        {
            switch (template)
            {
                case CharacterTestRuntimeAiPlannerConditionTemplate.TargetBlocked:
                    return "目标被遮挡";
                case CharacterTestRuntimeAiPlannerConditionTemplate.TargetRemembered:
                    return "目标记忆中";
                case CharacterTestRuntimeAiPlannerConditionTemplate.InAttackRange:
                    return "进入攻击范围";
                case CharacterTestRuntimeAiPlannerConditionTemplate.ActionNotRunning:
                    return "动作未执行";
                case CharacterTestRuntimeAiPlannerConditionTemplate.NearHazard:
                    return "靠近危险";
                case CharacterTestRuntimeAiPlannerConditionTemplate.NearUsefulObject:
                    return "靠近可利用物";
                default:
                    return "目标可见";
            }
        }

        private static void Apply(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool boolValue,
            float floatValue)
        {
            condition.Fact = fact;
            condition.Operator = CharacterTestRuntimeAiPlannerConditionOperator.Equals;
            condition.BoolValue = boolValue;
            condition.FloatValue = floatValue;
        }

        private static CharacterTestRuntimeAiPlannerEditorFactMetadata Bool(
            CharacterTestRuntimeAiPlannerFactKind fact,
            string category,
            string displayName,
            string key,
            string description,
            bool defaultValue)
        {
            return new CharacterTestRuntimeAiPlannerEditorFactMetadata(
                fact,
                category,
                displayName,
                key,
                description,
                CharacterTestRuntimeAiPlannerEditorFactValueType.Bool,
                BoolOperators,
                defaultValue,
                0f);
        }

        private static CharacterTestRuntimeAiPlannerEditorFactMetadata Float(
            CharacterTestRuntimeAiPlannerFactKind fact,
            string category,
            string displayName,
            string key,
            string description,
            float defaultValue)
        {
            return new CharacterTestRuntimeAiPlannerEditorFactMetadata(
                fact,
                category,
                displayName,
                key,
                description,
                CharacterTestRuntimeAiPlannerEditorFactValueType.Float,
                ComparableOperators,
                true,
                defaultValue);
        }

        private static CharacterTestRuntimeAiPlannerEditorFactMetadata Int(
            CharacterTestRuntimeAiPlannerFactKind fact,
            string category,
            string displayName,
            string key,
            string description,
            int defaultValue)
        {
            return new CharacterTestRuntimeAiPlannerEditorFactMetadata(
                fact,
                category,
                displayName,
                key,
                description,
                CharacterTestRuntimeAiPlannerEditorFactValueType.Int,
                ComparableOperators,
                true,
                defaultValue);
        }

        private static CharacterTestRuntimeAiPlannerEditorFactMetadata Surface(
            CharacterTestRuntimeAiPlannerFactKind fact,
            string category,
            string displayName,
            string key,
            string description,
            CombatSurfaceMaterialKind defaultValue)
        {
            return new CharacterTestRuntimeAiPlannerEditorFactMetadata(
                fact,
                category,
                displayName,
                key,
                description,
                CharacterTestRuntimeAiPlannerEditorFactValueType.SurfaceKind,
                BoolOperators,
                true,
                (int)defaultValue);
        }
    }
}
