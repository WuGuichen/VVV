using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    /// <summary>Tool-facing Ability contract versioned for JSON and editor forms.</summary>
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
        DamageByAttackDefense = 1
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
                Add(issues, AbilityAuthoringValidationCode.UnsupportedContractVersion, "ContractVersion", "Unsupported ability contract version.");

            if (contract.AbilityId == 0)
                Add(issues, AbilityAuthoringValidationCode.MissingAbilityId, "AbilityId", "AbilityId is required.");
            else if (contract.AbilityId < 300000 || contract.AbilityId > 399999)
                Add(issues, AbilityAuthoringValidationCode.InvalidAbilityId, "AbilityId", "AbilityId must be in range 300000-399999.");

            if (string.IsNullOrWhiteSpace(contract.DisplayName))
                Add(issues, AbilityAuthoringValidationCode.MissingDisplayName, "DisplayName", "DisplayName is required.");

            if (!IsSupportedTargetSelector(contract.TargetSelectorKind))
                Add(issues, AbilityAuthoringValidationCode.UnknownTargetSelector, "TargetSelectorKind", "Unsupported target selector.");

            if (contract.Effects == null || contract.Effects.Length == 0)
            {
                Add(issues, AbilityAuthoringValidationCode.MissingEffect, "Effects", "At least one effect is required.");
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
                Add(issues, AbilityAuthoringValidationCode.MissingEffect, prefix, "Effect cannot be null.");
                return;
            }

            switch (effect.Kind)
            {
                case AbilityAuthoringEffectKind.DamageByAttackDefense:
                    ValidateAttributeId(effect.AttackAttributeId, prefix + ".AttackAttributeId", issues);
                    ValidateAttributeId(effect.DefenseAttributeId, prefix + ".DefenseAttributeId", issues);
                    ValidateAttributeId(effect.HpAttributeId, prefix + ".HpAttributeId", issues);
                    break;
                default:
                    Add(issues, AbilityAuthoringValidationCode.UnknownEffectKind, prefix + ".Kind", "Unsupported effect kind.");
                    break;
            }
        }

        private static void ValidateAttributeId(
            int attributeId,
            string fieldPath,
            List<AbilityAuthoringValidationIssue> issues)
        {
            if (attributeId == 0)
                Add(issues, AbilityAuthoringValidationCode.MissingEffectParameter, fieldPath, "Attribute id is required.");
            else if (attributeId < 0)
                Add(issues, AbilityAuthoringValidationCode.InvalidAttributeId, fieldPath, "Attribute id must be positive.");
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
        private static readonly string[] EffectKindValues = { "DamageByAttackDefense" };

        public static AbilityAuthoringSchemaSummary CreateSummary()
        {
            return new AbilityAuthoringSchemaSummary(GetFields(), GetErrorCodes());
        }

        public static IReadOnlyList<AbilityAuthoringFieldDescriptor> GetFields()
        {
            return new[]
            {
                new AbilityAuthoringFieldDescriptor("ContractVersion", "Contract Version", "int", "Current supported version is 1.", true),
                new AbilityAuthoringFieldDescriptor("AbilityId", "Ability ID", "int", "Runtime Ability ID in range 300000-399999.", true),
                new AbilityAuthoringFieldDescriptor("DisplayName", "Display Name", "string", "Display name used by BasicAbilityConfig.NameText.", true),
                new AbilityAuthoringFieldDescriptor("Description", "Description", "string", "Description used by BasicAbilityConfig.DescriptionText.", false),
                new AbilityAuthoringFieldDescriptor("TargetSelectorKind", "Target Selector", "enum", "Supported values: Self, SingleEnemy.", true, TargetSelectorValues),
                new AbilityAuthoringFieldDescriptor("Effects", "Effects", "AbilityAuthoringEffectContract[]", "Ordered effect list. At least one effect is required.", true),
                new AbilityAuthoringFieldDescriptor("Effects[].Kind", "Effect Kind", "enum", "Supported values: DamageByAttackDefense.", true, EffectKindValues),
                new AbilityAuthoringFieldDescriptor("Effects[].AttackAttributeId", "Attack Attribute ID", "int", "Attack attribute used by DamageByAttackDefense.", false),
                new AbilityAuthoringFieldDescriptor("Effects[].DefenseAttributeId", "Defense Attribute ID", "int", "Defense attribute used by DamageByAttackDefense.", false),
                new AbilityAuthoringFieldDescriptor("Effects[].HpAttributeId", "HP Attribute ID", "int", "HP attribute reduced by DamageByAttackDefense.", false)
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
                AbilityAuthoringValidationCode.UnsupportedContractVersion
            };
        }
    }
}
