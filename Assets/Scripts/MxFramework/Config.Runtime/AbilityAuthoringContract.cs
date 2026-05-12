using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    /// <summary>Tool-facing Ability contract versioned for JSON, editor forms, and AI generation.</summary>
    public sealed class AbilityAuthoringContract
    {
        public const int CurrentVersion = 1;

        public AbilityAuthoringContract()
        {
            ContractVersion = CurrentVersion;
            Effects = Array.Empty<AbilityAuthoringEffectContract>();
        }

        public int ContractVersion { get; set; }
        public int AbilityId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public AbilityAuthoringTargetSelectorKind TargetSelectorKind { get; set; }
        public AbilityAuthoringEffectContract[] Effects { get; set; }
    }

    /// <summary>Tool-facing effect contract with named parameters instead of positional arrays.</summary>
    public sealed class AbilityAuthoringEffectContract
    {
        public AbilityAuthoringEffectKind Kind { get; set; }
        public int AttackAttributeId { get; set; }
        public int DefenseAttributeId { get; set; }
        public int HpAttributeId { get; set; }
        public int BuffId { get; set; }

        public static AbilityAuthoringEffectContract DamageByAttackDefense(
            int attackAttributeId,
            int defenseAttributeId,
            int hpAttributeId)
        {
            return new AbilityAuthoringEffectContract
            {
                Kind = AbilityAuthoringEffectKind.DamageByAttackDefense,
                AttackAttributeId = attackAttributeId,
                DefenseAttributeId = defenseAttributeId,
                HpAttributeId = hpAttributeId
            };
        }

        public static AbilityAuthoringEffectContract ApplyBuff(int buffId)
        {
            return new AbilityAuthoringEffectContract
            {
                Kind = AbilityAuthoringEffectKind.ApplyBuff,
                BuffId = buffId
            };
        }
    }

    public enum AbilityAuthoringTargetSelectorKind
    {
        Unknown = 0,
        Self = 1,
        SingleEnemy = 2
    }

    public enum AbilityAuthoringEffectKind
    {
        Unknown = 0,
        DamageByAttackDefense = 1,
        ApplyBuff = 2
    }

    public enum AbilityAuthoringValidationCode
    {
        MissingAbilityId = 1,
        InvalidAbilityId = 2,
        MissingDisplayName = 3,
        UnknownTargetSelector = 4,
        MissingEffect = 5,
        UnknownEffectKind = 6,
        MissingEffectParameter = 7,
        InvalidAttributeId = 8,
        InvalidBuffId = 9,
        UnsupportedContractVersion = 10
    }

    public readonly struct AbilityAuthoringValidationIssue
    {
        public AbilityAuthoringValidationIssue(
            AbilityAuthoringValidationCode code,
            string fieldPath,
            string message)
        {
            Code = code;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public AbilityAuthoringValidationCode Code { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class AbilityAuthoringValidationReport
    {
        private readonly AbilityAuthoringValidationIssue[] _issues;

        public AbilityAuthoringValidationReport(IReadOnlyList<AbilityAuthoringValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                _issues = Array.Empty<AbilityAuthoringValidationIssue>();
                return;
            }

            _issues = new AbilityAuthoringValidationIssue[issues.Count];
            for (int i = 0; i < issues.Count; i++)
                _issues[i] = issues[i];
        }

        public IReadOnlyList<AbilityAuthoringValidationIssue> Issues => _issues;
        public bool IsValid => _issues.Length == 0;

        public bool Contains(AbilityAuthoringValidationCode code)
        {
            for (int i = 0; i < _issues.Length; i++)
            {
                if (_issues[i].Code == code)
                    return true;
            }

            return false;
        }
    }

    public static class AbilityAuthoringContractValidator
    {
        public static AbilityAuthoringValidationReport Validate(AbilityAuthoringContract contract)
        {
            var issues = new List<AbilityAuthoringValidationIssue>();

            if (contract == null)
            {
                Add(issues, AbilityAuthoringValidationCode.UnsupportedContractVersion, "ContractVersion", "Ability authoring contract is null.");
                return new AbilityAuthoringValidationReport(issues);
            }

            if (contract.ContractVersion != AbilityAuthoringContract.CurrentVersion)
                Add(issues, AbilityAuthoringValidationCode.UnsupportedContractVersion, "ContractVersion", "不支持的 Ability contract 版本。");

            if (contract.AbilityId == 0)
                Add(issues, AbilityAuthoringValidationCode.MissingAbilityId, "AbilityId", "缺少 Ability ID。");
            else if (contract.AbilityId < 300000 || contract.AbilityId > 399999)
                Add(issues, AbilityAuthoringValidationCode.InvalidAbilityId, "AbilityId", "Ability ID 必须位于 300000-399999。");

            if (string.IsNullOrWhiteSpace(contract.DisplayName))
                Add(issues, AbilityAuthoringValidationCode.MissingDisplayName, "DisplayName", "缺少显示名。");

            if (!IsSupportedTargetSelector(contract.TargetSelectorKind))
                Add(issues, AbilityAuthoringValidationCode.UnknownTargetSelector, "TargetSelectorKind", "未知目标选择类型。");

            if (contract.Effects == null || contract.Effects.Length == 0)
            {
                Add(issues, AbilityAuthoringValidationCode.MissingEffect, "Effects", "至少需要一个效果。");
                return new AbilityAuthoringValidationReport(issues);
            }

            for (int i = 0; i < contract.Effects.Length; i++)
                ValidateEffect(contract.Effects[i], i, issues);

            return new AbilityAuthoringValidationReport(issues);
        }

        private static void ValidateEffect(
            AbilityAuthoringEffectContract effect,
            int index,
            List<AbilityAuthoringValidationIssue> issues)
        {
            string prefix = "Effects[" + index + "]";
            if (effect == null)
            {
                Add(issues, AbilityAuthoringValidationCode.MissingEffect, prefix, "效果不能为空。");
                return;
            }

            switch (effect.Kind)
            {
                case AbilityAuthoringEffectKind.DamageByAttackDefense:
                    ValidateAttributeId(effect.AttackAttributeId, prefix + ".AttackAttributeId", issues);
                    ValidateAttributeId(effect.DefenseAttributeId, prefix + ".DefenseAttributeId", issues);
                    ValidateAttributeId(effect.HpAttributeId, prefix + ".HpAttributeId", issues);
                    break;
                case AbilityAuthoringEffectKind.ApplyBuff:
                    if (effect.BuffId == 0)
                        Add(issues, AbilityAuthoringValidationCode.MissingEffectParameter, prefix + ".BuffId", "ApplyBuff 缺少 buffId。");
                    else if (effect.BuffId < 0)
                        Add(issues, AbilityAuthoringValidationCode.InvalidBuffId, prefix + ".BuffId", "buffId 必须为正整数。");
                    break;
                default:
                    Add(issues, AbilityAuthoringValidationCode.UnknownEffectKind, prefix + ".Kind", "未知效果类型。");
                    break;
            }
        }

        private static void ValidateAttributeId(
            int attributeId,
            string fieldPath,
            List<AbilityAuthoringValidationIssue> issues)
        {
            if (attributeId == 0)
                Add(issues, AbilityAuthoringValidationCode.MissingEffectParameter, fieldPath, "DamageByAttackDefense 缺少属性 ID。");
            else if (attributeId < 0)
                Add(issues, AbilityAuthoringValidationCode.InvalidAttributeId, fieldPath, "属性 ID 必须为正整数。");
        }

        private static bool IsSupportedTargetSelector(AbilityAuthoringTargetSelectorKind kind)
        {
            return kind == AbilityAuthoringTargetSelectorKind.Self
                || kind == AbilityAuthoringTargetSelectorKind.SingleEnemy;
        }

        private static void Add(
            List<AbilityAuthoringValidationIssue> issues,
            AbilityAuthoringValidationCode code,
            string fieldPath,
            string message)
        {
            issues.Add(new AbilityAuthoringValidationIssue(code, fieldPath, message));
        }
    }

    public static class AbilityAuthoringContractMapper
    {
        public static bool TryMap(
            AbilityAuthoringContract contract,
            out BasicAbilityConfig config,
            out AbilityAuthoringValidationReport report)
        {
            config = null;
            report = AbilityAuthoringContractValidator.Validate(contract);
            if (!report.IsValid)
                return false;

            var effects = new AbilityEffectConfig[contract.Effects.Length];
            for (int i = 0; i < contract.Effects.Length; i++)
                effects[i] = MapEffect(contract.Effects[i]);

            config = new BasicAbilityConfig(
                contract.AbilityId,
                new LocalizedTextKey(contract.DisplayName),
                new LocalizedTextKey(contract.Description),
                MapTargetSelector(contract.TargetSelectorKind),
                effects);
            return true;
        }

        private static AbilityTargetSelectorKind MapTargetSelector(AbilityAuthoringTargetSelectorKind kind)
        {
            switch (kind)
            {
                case AbilityAuthoringTargetSelectorKind.Self:
                    return AbilityTargetSelectorKind.Self;
                case AbilityAuthoringTargetSelectorKind.SingleEnemy:
                    return AbilityTargetSelectorKind.SingleEnemy;
                default:
                    return AbilityTargetSelectorKind.Unknown;
            }
        }

        private static AbilityEffectConfig MapEffect(AbilityAuthoringEffectContract effect)
        {
            switch (effect.Kind)
            {
                case AbilityAuthoringEffectKind.DamageByAttackDefense:
                    return AbilityEffectConfig.DamageByAttackDefense(
                        effect.AttackAttributeId,
                        effect.DefenseAttributeId,
                        effect.HpAttributeId);
                case AbilityAuthoringEffectKind.ApplyBuff:
                    return AbilityEffectConfig.ApplyBuff(effect.BuffId);
                default:
                    return new AbilityEffectConfig(AbilityEffectKind.Unknown, Array.Empty<int>());
            }
        }
    }

    public readonly struct AbilityAuthoringFieldDescriptor
    {
        public AbilityAuthoringFieldDescriptor(
            string fieldPath,
            string displayNameZh,
            string typeName,
            string descriptionZh,
            bool required,
            IReadOnlyList<string> allowedValues = null)
        {
            FieldPath = fieldPath ?? string.Empty;
            DisplayNameZh = displayNameZh ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            DescriptionZh = descriptionZh ?? string.Empty;
            Required = required;
            AllowedValues = allowedValues ?? Array.Empty<string>();
        }

        public string FieldPath { get; }
        public string DisplayNameZh { get; }
        public string TypeName { get; }
        public string DescriptionZh { get; }
        public bool Required { get; }
        public IReadOnlyList<string> AllowedValues { get; }
    }

    public sealed class AbilityAuthoringSchemaSummary
    {
        private readonly AbilityAuthoringFieldDescriptor[] _fields;
        private readonly AbilityAuthoringValidationCode[] _errorCodes;

        public AbilityAuthoringSchemaSummary(
            IReadOnlyList<AbilityAuthoringFieldDescriptor> fields,
            IReadOnlyList<AbilityAuthoringValidationCode> errorCodes)
        {
            _fields = Copy(fields);
            _errorCodes = Copy(errorCodes);
        }

        public int ContractVersion => AbilityAuthoringContract.CurrentVersion;
        public string ContractName => nameof(AbilityAuthoringContract);
        public IReadOnlyList<AbilityAuthoringFieldDescriptor> Fields => _fields;
        public IReadOnlyList<AbilityAuthoringValidationCode> ErrorCodes => _errorCodes;

        private static AbilityAuthoringFieldDescriptor[] Copy(IReadOnlyList<AbilityAuthoringFieldDescriptor> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<AbilityAuthoringFieldDescriptor>();

            var copy = new AbilityAuthoringFieldDescriptor[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }

        private static AbilityAuthoringValidationCode[] Copy(IReadOnlyList<AbilityAuthoringValidationCode> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<AbilityAuthoringValidationCode>();

            var copy = new AbilityAuthoringValidationCode[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    public static class AbilityAuthoringSchema
    {
        private static readonly string[] TargetSelectorValues = { "Self", "SingleEnemy" };
        private static readonly string[] EffectKindValues = { "DamageByAttackDefense", "ApplyBuff" };

        public static AbilityAuthoringSchemaSummary CreateSummary()
        {
            return new AbilityAuthoringSchemaSummary(GetFields(), GetErrorCodes());
        }

        public static IReadOnlyList<AbilityAuthoringFieldDescriptor> GetFields()
        {
            return new[]
            {
                new AbilityAuthoringFieldDescriptor("ContractVersion", "契约版本", "int", "当前支持版本为 1。", true),
                new AbilityAuthoringFieldDescriptor("AbilityId", "技能 ID", "int", "运行时 Ability ID，范围 300000-399999。", true),
                new AbilityAuthoringFieldDescriptor("DisplayName", "显示名", "string", "给编辑器、AI 上下文和 BasicAbilityConfig.NameText 使用的名称。", true),
                new AbilityAuthoringFieldDescriptor("Description", "说明", "string", "给编辑器、AI 上下文和 BasicAbilityConfig.DescriptionText 使用的说明。", false),
                new AbilityAuthoringFieldDescriptor("TargetSelectorKind", "目标选择", "enum", "选择施法者自身或第一个敌方目标。", true, TargetSelectorValues),
                new AbilityAuthoringFieldDescriptor("Effects", "效果列表", "AbilityAuthoringEffectContract[]", "按顺序执行的效果列表，至少一个。", true),
                new AbilityAuthoringFieldDescriptor("Effects[].Kind", "效果类型", "enum", "当前支持伤害和挂 Buff。", true, EffectKindValues),
                new AbilityAuthoringFieldDescriptor("Effects[].AttackAttributeId", "攻击属性 ID", "int", "DamageByAttackDefense 使用的攻击属性。", false),
                new AbilityAuthoringFieldDescriptor("Effects[].DefenseAttributeId", "防御属性 ID", "int", "DamageByAttackDefense 使用的防御属性。", false),
                new AbilityAuthoringFieldDescriptor("Effects[].HpAttributeId", "生命属性 ID", "int", "DamageByAttackDefense 扣减的 HP 属性。", false),
                new AbilityAuthoringFieldDescriptor("Effects[].BuffId", "Buff ID", "int", "ApplyBuff 使用的 Buff 配置 ID。", false)
            };
        }

        public static IReadOnlyList<AbilityAuthoringValidationCode> GetErrorCodes()
        {
            return new[]
            {
                AbilityAuthoringValidationCode.MissingAbilityId,
                AbilityAuthoringValidationCode.InvalidAbilityId,
                AbilityAuthoringValidationCode.MissingDisplayName,
                AbilityAuthoringValidationCode.UnknownTargetSelector,
                AbilityAuthoringValidationCode.MissingEffect,
                AbilityAuthoringValidationCode.UnknownEffectKind,
                AbilityAuthoringValidationCode.MissingEffectParameter,
                AbilityAuthoringValidationCode.InvalidAttributeId,
                AbilityAuthoringValidationCode.InvalidBuffId,
                AbilityAuthoringValidationCode.UnsupportedContractVersion
            };
        }
    }
}
