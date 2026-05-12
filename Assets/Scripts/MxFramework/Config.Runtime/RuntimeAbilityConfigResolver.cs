using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Gameplay;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Config.Runtime
{
    public sealed class RuntimeConfigChangeSummary
    {
        private readonly List<int> _changedAbilityIds = new List<int>();
        private readonly List<int> _changedBuffIds = new List<int>();
        private readonly List<int> _changedModifierIds = new List<int>();
        private readonly List<int> _rebuiltAbilityIds = new List<int>();
        private readonly List<int> _failedAbilityIds = new List<int>();
        private readonly List<string> _errors = new List<string>();

        public RuntimeConfigChangeSummary(
            string sourceName = "",
            string previousSourceName = "",
            string applyPolicy = "RebuildOnResolve")
        {
            SourceName = sourceName ?? string.Empty;
            PreviousSourceName = previousSourceName ?? string.Empty;
            ApplyPolicy = applyPolicy ?? string.Empty;
        }

        public string SourceName { get; }
        public string PreviousSourceName { get; }
        public string ApplyPolicy { get; }
        public IReadOnlyList<int> ChangedAbilityIds => _changedAbilityIds;
        public IReadOnlyList<int> ChangedBuffIds => _changedBuffIds;
        public IReadOnlyList<int> ChangedModifierIds => _changedModifierIds;
        public IReadOnlyList<int> RebuiltAbilityIds => _rebuiltAbilityIds;
        public IReadOnlyList<int> FailedAbilityIds => _failedAbilityIds;
        public IReadOnlyList<string> Errors => _errors;
        public int ChangedAbilityCount => _changedAbilityIds.Count;
        public int ChangedBuffCount => _changedBuffIds.Count;
        public int ChangedModifierCount => _changedModifierIds.Count;
        public int RebuiltAbilityCount => _rebuiltAbilityIds.Count;
        public int FailedAbilityCount => _failedAbilityIds.Count;

        public static RuntimeConfigChangeSummary FromChangeSet(
            ConfigChangeSet changeSet,
            string sourceName = "",
            string previousSourceName = "",
            string applyPolicy = "RebuildOnResolve")
        {
            var summary = new RuntimeConfigChangeSummary(sourceName, previousSourceName, applyPolicy);
            if (changeSet == null)
                return summary;

            for (int i = 0; i < changeSet.Changes.Count; i++)
            {
                ConfigRowChange change = changeSet.Changes[i];
                if (change.ConfigType == typeof(BasicAbilityConfig))
                    summary.AddChangedAbility(change.Id);
                else if (change.ConfigType == typeof(BasicBuffConfig))
                    summary.AddChangedBuff(change.Id);
                else if (change.ConfigType == typeof(BasicModifierConfig))
                    summary.AddChangedModifier(change.Id);
            }

            return summary;
        }

        public void AddChangedAbility(int abilityId) => AddUnique(_changedAbilityIds, abilityId);
        public void AddChangedBuff(int buffId) => AddUnique(_changedBuffIds, buffId);
        public void AddChangedModifier(int modifierId) => AddUnique(_changedModifierIds, modifierId);
        public void AddRebuiltAbility(int abilityId) => AddUnique(_rebuiltAbilityIds, abilityId);

        public void AddFailedAbility(int abilityId, string error)
        {
            AddUnique(_failedAbilityIds, abilityId);
            if (!string.IsNullOrEmpty(error))
                _errors.Add(error);
        }

        public string ToSummaryText()
        {
            var builder = new StringBuilder();
            builder.Append("source=").Append(string.IsNullOrEmpty(SourceName) ? "(unknown)" : SourceName)
                .Append(", policy=").Append(ApplyPolicy)
                .Append(", changed abilities=").Append(ChangedAbilityCount)
                .Append(", buffs=").Append(ChangedBuffCount)
                .Append(", modifiers=").Append(ChangedModifierCount)
                .Append(", rebuilt=").Append(RebuiltAbilityCount)
                .Append(", failed=").Append(FailedAbilityCount);

            if (!string.IsNullOrEmpty(PreviousSourceName))
                builder.Append(", previous=").Append(PreviousSourceName);

            if (_errors.Count > 0)
                builder.Append(", error=").Append(_errors[_errors.Count - 1]);

            return builder.ToString();
        }

        private static void AddUnique(List<int> values, int id)
        {
            if (id <= 0 || values.Contains(id))
                return;

            values.Add(id);
        }
    }

    /// <summary>
    /// Resolves config-driven abilities using rebuild-on-resolve semantics.
    /// Existing ability instances are not hot-swapped when config changes.
    /// </summary>
    public sealed class RuntimeAbilityConfigResolver
    {
        private readonly IConfigProvider _configs;
        private readonly IBuffFactory _buffFactory;

        public RuntimeAbilityConfigResolver(
            IConfigProvider configs,
            IBuffFactory buffFactory = null,
            string sourceName = "",
            ConfigChangeSet changeSet = null,
            string previousSourceName = "")
        {
            _configs = configs;
            _buffFactory = buffFactory;
            SourceName = sourceName ?? string.Empty;
            ChangeSet = changeSet ?? new ConfigChangeSet();
            ChangeSummary = RuntimeConfigChangeSummary.FromChangeSet(ChangeSet, SourceName, previousSourceName, ApplyPolicy);
        }

        public string SourceName { get; }
        public ConfigChangeSet ChangeSet { get; }
        public string ApplyPolicy => "RebuildOnResolve";
        public RuntimeConfigChangeSummary ChangeSummary { get; }

        public string CreateSummary()
        {
            return ChangeSummary.ToSummaryText();
        }

        public bool TryCreate(int abilityId, out IAbility ability, out string error)
        {
            var factory = new ConfigAbilityFactory(_configs, _buffFactory);
            if (factory.TryCreate(abilityId, out ability, out error))
            {
                ChangeSummary.AddRebuiltAbility(abilityId);
                return true;
            }

            error = "Ability rebuild failed. Ability=" + abilityId + ", " + CreateSummary() + ". " + error;
            ChangeSummary.AddFailedAbility(abilityId, error);
            return false;
        }
    }
}
