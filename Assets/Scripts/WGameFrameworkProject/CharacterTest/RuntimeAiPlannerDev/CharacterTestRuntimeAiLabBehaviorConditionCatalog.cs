using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorConditionCategory
    {
        State = 0,
        Perception = 1,
        Geometry = 2,
        AttackThreat = 3,
        Navigation = 4,
        Action = 5,
        Ability = 6,
        Danger = 7
    }

    internal enum CharacterTestRuntimeAiLabBehaviorConditionValueKind
    {
        Boolean = 0,
        Number = 1,
        Ability = 2,
        CharacterAction = 3,
        TargetStateMask = 4
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorConditionDescriptor
    {
        public CharacterTestRuntimeAiLabBehaviorConditionDescriptor(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabBehaviorConditionCategory category,
            CharacterTestRuntimeAiLabBehaviorConditionValueKind valueKind,
            string displayName,
            string unit,
            CharacterTestRuntimeAiLabCardConditionOperator defaultOperator,
            bool defaultBoolean,
            float defaultValue)
        {
            Fact = fact;
            Subject = subject;
            Category = category;
            ValueKind = valueKind;
            DisplayName = displayName ?? string.Empty;
            Unit = unit ?? string.Empty;
            DefaultOperator = defaultOperator;
            DefaultBoolean = defaultBoolean;
            DefaultValue = defaultValue;
        }

        public CharacterTestRuntimeAiLabCardConditionFact Fact { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Subject { get; }
        public CharacterTestRuntimeAiLabBehaviorConditionCategory Category { get; }
        public CharacterTestRuntimeAiLabBehaviorConditionValueKind ValueKind { get; }
        public string DisplayName { get; }
        public string Unit { get; }
        public CharacterTestRuntimeAiLabCardConditionOperator DefaultOperator { get; }
        public bool DefaultBoolean { get; }
        public float DefaultValue { get; }
        public bool IsSubjectAvailability => false;
    }

    internal static class CharacterTestRuntimeAiLabBehaviorConditionCatalog
    {
        private static readonly CharacterTestRuntimeAiLabBehaviorConditionDescriptor[] Descriptors =
        {
            Bool(CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded, Subject.CurrentCombatTarget, Category.Action, "进入等待时捕获的目标攻击判定已全部退出"),
            TargetState(CharacterTestRuntimeAiLabCardConditionFact.TargetState, Subject.CurrentCombatTarget, Category.State, "目标状态"),
            Number(CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance, Subject.CurrentCombatTarget, Category.Geometry, "目标根节点平面距离", "米", CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, 1.5f),
            Number(CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle, Subject.CurrentCombatTarget, Category.Geometry, "面向目标的夹角", "度", CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, 5f),
            Number(CharacterTestRuntimeAiLabCardConditionFact.TargetDistance, Subject.CurrentCombatTarget, Category.Geometry, "目标 Hurtbox 表面距离", "米", CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, 1.5f),
            Bool(CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible, Subject.Self, Category.Action, "自身动作可打断"),
            Number(CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio, Subject.Self, Category.State, "自身生命比例", "比例", CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, 0.3f),
            Number(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames, Subject.CurrentCombatTarget, Category.AttackThreat, "距离下一攻击窗口剩余帧", "帧", CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, 10f)
        };

        private static class Subject
        {
            public const CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind CurrentCombatTarget = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget;
            public const CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Self = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self;
            public const CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Environment = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment;
        }

        private static class Category
        {
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory State = CharacterTestRuntimeAiLabBehaviorConditionCategory.State;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Perception = CharacterTestRuntimeAiLabBehaviorConditionCategory.Perception;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Geometry = CharacterTestRuntimeAiLabBehaviorConditionCategory.Geometry;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory AttackThreat = CharacterTestRuntimeAiLabBehaviorConditionCategory.AttackThreat;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Navigation = CharacterTestRuntimeAiLabBehaviorConditionCategory.Navigation;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Action = CharacterTestRuntimeAiLabBehaviorConditionCategory.Action;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Ability = CharacterTestRuntimeAiLabBehaviorConditionCategory.Ability;
            public const CharacterTestRuntimeAiLabBehaviorConditionCategory Danger = CharacterTestRuntimeAiLabBehaviorConditionCategory.Danger;
        }

        public static IReadOnlyList<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> All => Descriptors;

        public static bool TryGet(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
        {
            for (int i = 0; i < Descriptors.Length; i++)
            {
                if (Descriptors[i].Fact != fact)
                    continue;
                descriptor = Descriptors[i];
                return true;
            }
            descriptor = default;
            return false;
        }

        public static bool RequiresSubjectScope(CharacterTestRuntimeAiLabCardConditionFact fact) =>
            TryGet(fact, out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
            && !descriptor.IsSubjectAvailability;

        public static CharacterTestRuntimeAiLabCardCondition CreateDefault(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            string conditionId)
        {
            if (!TryGet(fact, out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor))
                throw new ArgumentOutOfRangeException(nameof(fact));
            return new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = conditionId ?? string.Empty,
                Fact = fact,
                Operator = descriptor.DefaultOperator,
                BoolValue = descriptor.DefaultBoolean,
                Value = descriptor.DefaultValue,
                AllowedTargetPhases = descriptor.ValueKind
                        == CharacterTestRuntimeAiLabBehaviorConditionValueKind.TargetStateMask
                    ? (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                    : 0u
            };
        }

        public static string SubjectText(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject) => subject switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget => "目标",
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self => "自身",
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment => "环境",
            _ => "未知对象"
        };

        public static string CategoryText(CharacterTestRuntimeAiLabBehaviorConditionCategory category) => category switch
        {
            CharacterTestRuntimeAiLabBehaviorConditionCategory.State => "状态",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Perception => "感知",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Geometry => "几何",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.AttackThreat => "攻击威胁",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Navigation => "导航",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Action => "动作",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Ability => "能力",
            CharacterTestRuntimeAiLabBehaviorConditionCategory.Danger => "危险",
            _ => "其他"
        };

        private static CharacterTestRuntimeAiLabBehaviorConditionDescriptor Bool(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabBehaviorConditionCategory category,
            string displayName) =>
            new CharacterTestRuntimeAiLabBehaviorConditionDescriptor(
                fact, subject, category, CharacterTestRuntimeAiLabBehaviorConditionValueKind.Boolean,
                displayName, string.Empty, CharacterTestRuntimeAiLabCardConditionOperator.Equals, true, 0f);

        private static CharacterTestRuntimeAiLabBehaviorConditionDescriptor Number(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabBehaviorConditionCategory category,
            string displayName,
            string unit,
            CharacterTestRuntimeAiLabCardConditionOperator defaultOperator,
            float defaultValue) =>
            new CharacterTestRuntimeAiLabBehaviorConditionDescriptor(
                fact, subject, category, CharacterTestRuntimeAiLabBehaviorConditionValueKind.Number,
                displayName, unit, defaultOperator, true, defaultValue);

        private static CharacterTestRuntimeAiLabBehaviorConditionDescriptor TargetState(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            CharacterTestRuntimeAiLabBehaviorConditionCategory category,
            string displayName) =>
            new CharacterTestRuntimeAiLabBehaviorConditionDescriptor(
                fact, subject, category, CharacterTestRuntimeAiLabBehaviorConditionValueKind.TargetStateMask,
                displayName, string.Empty, CharacterTestRuntimeAiLabCardConditionOperator.Equals, true, 0f);
    }
}
