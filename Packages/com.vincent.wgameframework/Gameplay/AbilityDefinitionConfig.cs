using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using System.Collections.ObjectModel;
using System.IO;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Gameplay
{
    public sealed class AbilityDefinitionConfig
    {
        public AbilityDefinitionConfig(
            int abilityId,
            string abilityStableId,
            string displayName,
            AbilityInputBindingConfig input,
            AbilityCooldownConfig cooldown,
            AbilityCostConfig[] costs,
            int targetPolicyId,
            AbilityActionBindingConfig actionBinding,
            AbilityDamageConfig damage,
            int presentationProfileId,
            int effectTimelineId,
            string targetPolicyLabel = "",
            string presentationProfileLabel = "",
            string effectTimelineLabel = "")
        {
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Ability id must be positive.");

            AbilityId = abilityId;
            AbilityStableId = abilityStableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Input = input ?? AbilityInputBindingConfig.Empty;
            Cooldown = cooldown ?? AbilityCooldownConfig.None;
            Costs = Array.AsReadOnly(Copy(costs));
            TargetPolicyId = targetPolicyId;
            TargetPolicyLabel = targetPolicyLabel ?? string.Empty;
            ActionBinding = actionBinding ?? AbilityActionBindingConfig.Empty;
            Damage = damage;
            PresentationProfileId = presentationProfileId;
            PresentationProfileLabel = presentationProfileLabel ?? string.Empty;
            EffectTimelineId = effectTimelineId;
            EffectTimelineLabel = effectTimelineLabel ?? string.Empty;
        }

        public int AbilityId { get; }
        public string AbilityStableId { get; }
        public string DisplayName { get; }
        public AbilityInputBindingConfig Input { get; }
        public AbilityCooldownConfig Cooldown { get; }
        public IReadOnlyList<AbilityCostConfig> Costs { get; }
        public int TargetPolicyId { get; }
        public string TargetPolicyLabel { get; }
        public AbilityActionBindingConfig ActionBinding { get; }
        public AbilityDamageConfig Damage { get; }
        public int PresentationProfileId { get; }
        public string PresentationProfileLabel { get; }
        public int EffectTimelineId { get; }
        public string EffectTimelineLabel { get; }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".abilityId", AbilityId);
            accumulator.AddStringStable(prefix + ".stableId", AbilityStableId);
            accumulator.AddStringStable(prefix + ".displayName", DisplayName);
            accumulator.AddStringStable(prefix + ".input.intent", Input.InputIntentLabel);
            accumulator.AddStringStable(prefix + ".input.slot", Input.SlotLabel);
            accumulator.AddLong(prefix + ".cooldown.frames", Cooldown.Frames);
            accumulator.AddStringStable(prefix + ".cooldown.group", Cooldown.GroupLabel);
            accumulator.AddInt(prefix + ".cost.count", Costs.Count);
            for (int i = 0; i < Costs.Count; i++)
            {
                AbilityCostConfig cost = Costs[i];
                accumulator.AddInt(prefix + ".cost." + i + ".attributeId", cost.AttributeId);
                accumulator.AddInt(prefix + ".cost." + i + ".amount", cost.Amount);
            }

            accumulator.AddStringStable(prefix + ".targetPolicyId", TargetPolicyLabel);
            accumulator.AddInt(prefix + ".action.id", ActionBinding.ActionId.Value);
            accumulator.AddStringStable(prefix + ".action.intent", ActionBinding.ActionIntentLabel);
            Damage.Contribute(accumulator, prefix + ".damage");
            accumulator.AddStringStable(prefix + ".presentationProfileId", PresentationProfileLabel);
            accumulator.AddStringStable(prefix + ".effectTimelineId", EffectTimelineLabel);
        }

        private static AbilityCostConfig[] Copy(AbilityCostConfig[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<AbilityCostConfig>();

            var copy = new AbilityCostConfig[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy, CompareCosts);
            return copy;
        }

        private static int CompareCosts(AbilityCostConfig left, AbilityCostConfig right)
        {
            return left.AttributeId.CompareTo(right.AttributeId);
        }
    }

    public readonly struct AbilityDamageAttributeScale : IEquatable<AbilityDamageAttributeScale>
    {
        public AbilityDamageAttributeScale(int attributeId, float scale)
        {
            AttributeId = attributeId;
            Scale = scale;
        }
        public int AttributeId { get; }
        public float Scale { get; }
        public bool Equals(AbilityDamageAttributeScale other) => AttributeId == other.AttributeId && Scale.Equals(other.Scale);
        public override bool Equals(object obj) => obj is AbilityDamageAttributeScale other && Equals(other);
        public override int GetHashCode() => (AttributeId * 397) ^ Scale.GetHashCode();
    }

    public readonly struct AbilityDamageConfig : IEquatable<AbilityDamageConfig>
    {
        private readonly int[] _tags;
        private readonly string[] _tagLabels;
        private readonly IReadOnlyList<int> _tagsView;
        private readonly IReadOnlyList<string> _tagLabelsView;
        private readonly AbilityDamageAttributeScale[] _sourceAttributeScales;
        private readonly IReadOnlyList<AbilityDamageAttributeScale> _sourceAttributeScalesView;

        public AbilityDamageConfig(
            int damageTypeId,
            int baseDamage,
            int impactPower,
            int[] tags,
            int analysisCategory,
            int launchPower = 0,
            string damageTypeLabel = "",
            string analysisCategoryLabel = "",
            string[] tagLabels = null,
            AbilityDamageAttributeScale[] sourceAttributeScales = null)
        {
            DamageTypeId = damageTypeId;
            BaseDamage = baseDamage < 0 ? 0 : baseDamage;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            _tags = tags == null || tags.Length == 0 ? Array.Empty<int>() : (int[])tags.Clone();
            Array.Sort(_tags);
            _tagLabels = CopyCanonicalTagLabels(tagLabels);
            _tagsView = Array.AsReadOnly(_tags);
            _tagLabelsView = Array.AsReadOnly(_tagLabels);
            AnalysisCategory = analysisCategory;
            LaunchPower = launchPower < 0 ? 0 : launchPower;
            DamageTypeLabel = damageTypeLabel ?? string.Empty;
            AnalysisCategoryLabel = analysisCategoryLabel ?? string.Empty;
            _sourceAttributeScales = sourceAttributeScales == null
                ? Array.Empty<AbilityDamageAttributeScale>()
                : (AbilityDamageAttributeScale[])sourceAttributeScales.Clone();
            _sourceAttributeScalesView = Array.AsReadOnly(_sourceAttributeScales);
        }

        private static string[] CopyCanonicalTagLabels(string[] labels)
        {
            if (labels == null || labels.Length == 0) return Array.Empty<string>();
            var values = new List<string>(labels.Length);
            for (int i = 0; i < labels.Length; i++)
                if (!string.IsNullOrWhiteSpace(labels[i])) values.Add(labels[i].Trim());
            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        public int DamageTypeId { get; }
        public string DamageTypeLabel { get; }
        public int BaseDamage { get; }
        public int ImpactPower { get; }
        public IReadOnlyList<int> Tags => _tagsView ?? Array.Empty<int>();
        public IReadOnlyList<string> TagLabels => _tagLabelsView ?? Array.Empty<string>();
        public int AnalysisCategory { get; }
        public string AnalysisCategoryLabel { get; }
        public int LaunchPower { get; }
        public IReadOnlyList<AbilityDamageAttributeScale> SourceAttributeScales =>
            _sourceAttributeScalesView ?? Array.Empty<AbilityDamageAttributeScale>();

        public bool HasDamage => DamageTypeId > 0
            || BaseDamage > 0
            || ImpactPower > 0
            || LaunchPower > 0
            || (_tags != null && _tags.Length > 0)
            || (_sourceAttributeScales != null && _sourceAttributeScales.Length > 0)
            || AnalysisCategory > 0;

        public bool Equals(AbilityDamageConfig other)
        {
            if (DamageTypeId != other.DamageTypeId
                || BaseDamage != other.BaseDamage
                || ImpactPower != other.ImpactPower
                || LaunchPower != other.LaunchPower
                || AnalysisCategory != other.AnalysisCategory)
            {
                return false;
            }

            int leftCount = _tags?.Length ?? 0;
            int rightCount = other._tags?.Length ?? 0;
            if (leftCount != rightCount)
                return false;
            for (int i = 0; i < leftCount; i++)
            {
                if (_tags[i] != other._tags[i])
                    return false;
            }

            int scaleCount = _sourceAttributeScales?.Length ?? 0;
            if (scaleCount != (other._sourceAttributeScales?.Length ?? 0)) return false;
            for (int i = 0; i < scaleCount; i++)
                if (!_sourceAttributeScales[i].Equals(other._sourceAttributeScales[i])) return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityDamageConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DamageTypeId;
                hash = (hash * 397) ^ BaseDamage;
                hash = (hash * 397) ^ ImpactPower;
                hash = (hash * 397) ^ LaunchPower;
                hash = (hash * 397) ^ AnalysisCategory;
                int tagCount = _tags?.Length ?? 0;
                for (int i = 0; i < tagCount; i++)
                    hash = (hash * 397) ^ _tags[i];
                for (int i = 0; _sourceAttributeScales != null && i < _sourceAttributeScales.Length; i++)
                    hash = (hash * 397) ^ _sourceAttributeScales[i].GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".damageTypeId", DamageTypeLabel);
            accumulator.AddInt(prefix + ".baseDamage", BaseDamage);
            accumulator.AddInt(prefix + ".impactPower", ImpactPower);
            accumulator.AddInt(prefix + ".launchPower", LaunchPower);
            accumulator.AddInt(prefix + ".tag.count", _tags?.Length ?? 0);
            for (int i = 0; _tags != null && i < _tags.Length; i++)
            {
                string label = _tagLabels != null && i < _tagLabels.Length
                    ? _tagLabels[i]
                    : string.Empty;
                accumulator.AddStringStable(prefix + ".tag." + i, label);
            }
            accumulator.AddStringStable(prefix + ".analysisCategory", AnalysisCategoryLabel);
            accumulator.AddInt(prefix + ".sourceAttributeScales.count", _sourceAttributeScales?.Length ?? 0);
            for (int i = 0; _sourceAttributeScales != null && i < _sourceAttributeScales.Length; i++)
            {
                accumulator.AddInt(prefix + ".sourceAttributeScales." + i + ".attributeId", _sourceAttributeScales[i].AttributeId);
                accumulator.AddDoubleQuantized(prefix + ".sourceAttributeScales." + i + ".scale", _sourceAttributeScales[i].Scale, 1000000d);
            }
        }
    }

    public sealed class AbilityInputBindingConfig
    {
        public static readonly AbilityInputBindingConfig Empty =
            new AbilityInputBindingConfig(0, 0);

        public AbilityInputBindingConfig(int inputIntentId, int slotId, string inputIntentLabel = "", string slotLabel = "")
        {
            InputIntentId = inputIntentId;
            InputIntentLabel = inputIntentLabel ?? string.Empty;
            SlotId = slotId;
            SlotLabel = slotLabel ?? string.Empty;
        }

        public int InputIntentId { get; }
        public string InputIntentLabel { get; }
        public int SlotId { get; }
        public string SlotLabel { get; }
    }

    public sealed class AbilityCostConfig
    {
        public AbilityCostConfig(int attributeId, int amount)
        {
            if (attributeId < 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Ability cost attribute id cannot be negative.");
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Ability cost amount cannot be negative.");

            AttributeId = attributeId;
            Amount = amount;
        }

        public int AttributeId { get; }
        public int Amount { get; }
    }

    public sealed class AbilityCooldownConfig
    {
        public static readonly AbilityCooldownConfig None = new AbilityCooldownConfig(0L, 0);

        public AbilityCooldownConfig(long frames, int groupId, string groupLabel = "")
        {
            if (frames < 0L)
                throw new ArgumentOutOfRangeException(nameof(frames), "Ability cooldown frames cannot be negative.");

            Frames = frames;
            GroupId = groupId;
            GroupLabel = groupLabel ?? string.Empty;
        }

        public long Frames { get; }
        public int GroupId { get; }
        public string GroupLabel { get; }
    }

    public sealed class AbilityActionBindingConfig
    {
        public static readonly AbilityActionBindingConfig Empty =
            new AbilityActionBindingConfig(CharacterActionId.None, 0);

        public AbilityActionBindingConfig(CharacterActionId actionId, int actionIntentId, string actionIntentLabel = "")
        {
            ActionId = actionId;
            ActionIntentId = actionIntentId;
            ActionIntentLabel = actionIntentLabel ?? string.Empty;
        }

        public CharacterActionId ActionId { get; }
        public int ActionIntentId { get; }
        public string ActionIntentLabel { get; }
    }

    public sealed class AbilityDefinitionRegistry
    {
        private readonly Dictionary<int, AbilityDefinitionConfig> _byId =
            new Dictionary<int, AbilityDefinitionConfig>();
        private readonly Dictionary<string, AbilityDefinitionConfig> _byStableId =
            new Dictionary<string, AbilityDefinitionConfig>(StringComparer.Ordinal);
        private readonly Dictionary<int, AbilityDefinitionConfig> _byInputIntent =
            new Dictionary<int, AbilityDefinitionConfig>();

        private bool _hasStableHash;
        private long _stableHash;
        public int Count => _byId.Count;

        public bool TryRegister(AbilityDefinitionConfig definition, out string failure)
        {
            if (definition == null)
            {
                failure = "Ability definition is missing.";
                return false;
            }

            if (_byId.ContainsKey(definition.AbilityId))
            {
                failure = "Duplicate ability id: " + definition.AbilityId + ".";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.AbilityStableId))
            {
                failure = "Ability stable id is required.";
                return false;
            }

            if (_byStableId.ContainsKey(definition.AbilityStableId))
            {
                failure = "Duplicate ability stable id: " + definition.AbilityStableId + ".";
                return false;
            }

            _hasStableHash = false;
            _byId.Add(definition.AbilityId, definition);
            _byStableId.Add(definition.AbilityStableId, definition);
            if (definition.Input.InputIntentId > 0 &&
                !_byInputIntent.ContainsKey(definition.Input.InputIntentId))
            {
                _byInputIntent.Add(definition.Input.InputIntentId, definition);
            }

            failure = string.Empty;
            return true;
        }

        public bool TryGet(int abilityId, out AbilityDefinitionConfig definition)
        {
            return _byId.TryGetValue(abilityId, out definition);
        }

        public bool TryGet(string abilityStableId, out AbilityDefinitionConfig definition)
        {
            if (string.IsNullOrWhiteSpace(abilityStableId))
            {
                definition = null;
                return false;
            }

            return _byStableId.TryGetValue(abilityStableId, out definition);
        }

        public bool TryGetByInputIntent(int inputIntentId, out AbilityDefinitionConfig definition)
        {
            if (inputIntentId <= 0)
            {
                definition = null;
                return false;
            }

            return _byInputIntent.TryGetValue(inputIntentId, out definition);
        }

        public IReadOnlyList<AbilityDefinitionConfig> CreateSnapshot()
        {
            var values = new List<AbilityDefinitionConfig>(_byId.Values);
            values.Sort((left, right) => left.AbilityId.CompareTo(right.AbilityId));
            return new ReadOnlyCollection<AbilityDefinitionConfig>(values);
        }

        public long ComputeStableHash()
        {
            if (_hasStableHash) return _stableHash;
            var accumulator = new RuntimeContentHashAccumulator();
            IReadOnlyList<AbilityDefinitionConfig> definitions = CreateSnapshot();
            accumulator.AddInt("ability.definition.count", definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
                definitions[i].Contribute(accumulator, "ability.definition." + i);

            _stableHash = accumulator.ToHash(); _hasStableHash = true;
            return _stableHash;
        }
    }

    public enum AbilityActivationReason
    {
        None = 0,
        MissingInputBinding = 1,
        InvalidActivation = 2,
        MissingActionIntent = 3,
        OnCooldown = 4,
        MissingCostAuthority = 5,
        InsufficientResource = 6
    }

    public readonly struct AbilityActivationFailure
    {
        public AbilityActivationFailure(
            AbilityActivationReason reasonCode,
            int attributeId = 0)
        {
            if ((uint)reasonCode > (uint)AbilityActivationReason.InsufficientResource)
                throw new ArgumentOutOfRangeException(nameof(reasonCode));
            if (reasonCode == AbilityActivationReason.InsufficientResource)
            {
                if (attributeId < 0)
                    throw new ArgumentOutOfRangeException(nameof(attributeId));
            }
            else if (attributeId != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attributeId));
            }

            ReasonCode = reasonCode;
            AttributeId = attributeId;
        }

        public AbilityActivationReason ReasonCode { get; }
        public int AttributeId { get; }
        public string Reason => AbilityActivationResult.FormatReason(ReasonCode, AttributeId);
    }

    public readonly struct AbilityActivationResult
    {
        public AbilityActivationResult(
            bool success,
            AbilityDefinitionConfig ability,
            AbilityActionBindingConfig actionBinding,
            AbilityActivationReason reasonCode,
            long remainingCooldownFrames,
            int detailId = 0)
        {
            _ = new AbilityActivationFailure(reasonCode, detailId);
            Success = success;
            Ability = ability;
            ActionBinding = actionBinding ?? AbilityActionBindingConfig.Empty;
            ReasonCode = reasonCode;
            DetailId = detailId;
            RemainingCooldownFrames = remainingCooldownFrames < 0L ? 0L : remainingCooldownFrames;
        }

        public bool Success { get; }
        public AbilityDefinitionConfig Ability { get; }
        public AbilityActionBindingConfig ActionBinding { get; }
        public string Reason => FormatReason(ReasonCode, DetailId);
        public AbilityActivationReason ReasonCode { get; }
        public int DetailId { get; }
        public long RemainingCooldownFrames { get; }

        public static string FormatReason(AbilityActivationReason reason, int detailId = 0)
        {
            switch (reason)
            {
                case AbilityActivationReason.None: return string.Empty;
                case AbilityActivationReason.MissingInputBinding: return "MissingAbilityInputBinding";
                case AbilityActivationReason.InvalidActivation: return "InvalidAbilityActivation";
                case AbilityActivationReason.MissingActionIntent: return "MissingActionIntent";
                case AbilityActivationReason.OnCooldown: return "OnCooldown";
                case AbilityActivationReason.MissingCostAuthority: return "MissingAbilityCostAuthority";
                case AbilityActivationReason.InsufficientResource:
                    return "InsufficientAbilityResource:" + detailId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }

    /// <summary>
    /// Authoritative resource seam used by action-only abilities. Implementations
    /// must evaluate and commit the complete sorted cost list atomically.
    /// </summary>
    public interface IAbilityCostAuthority
    {
        /// <remarks>NoAlloc on the steady-state path.</remarks>
        bool CanAfford(
            int casterEntityId,
            IReadOnlyList<AbilityCostConfig> costs,
            out int missingAttributeId);

        /// <remarks>NoAlloc on the steady-state path.</remarks>
        bool TryCommitCosts(
            int casterEntityId,
            IReadOnlyList<AbilityCostConfig> costs,
            out int missingAttributeId);
    }

    public sealed class AbilityRuntimeModule : RuntimeModule
    {
        private readonly Dictionary<CooldownKey, long> _cooldownEndFrames =
            new Dictionary<CooldownKey, long>();
        private readonly RuntimeHashDigest _moduleIdDigest;
        private IAbilityCostAuthority _costAuthority;

        public AbilityRuntimeModule(
            AbilityDefinitionRegistry definitions,
            string moduleId = "mxframework.ability.runtime",
            RuntimeTickStage tickStage = RuntimeTickStage.PreSimulation,
            int priority = 80,
            IAbilityCostAuthority costAuthority = null)
            : base(moduleId, tickStage, priority)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _moduleIdDigest = RuntimeHashDigest.ComputeContentDigest(ModuleId);
            _costAuthority = costAuthority;
        }

        public AbilityDefinitionRegistry Definitions { get; }
        /// <summary>Prepared digest of the diagnostic module label, computed at module construction.</summary>
        public RuntimeHashDigest ModuleIdDigest => _moduleIdDigest;
        public int ActiveCooldownCount => _cooldownEndFrames.Count;

        /// <summary>Binds the runtime resource authority used by configured ability costs.</summary>
        /// <remarks>Low-frequency composition-root operation.</remarks>
        public void SetCostAuthority(IAbilityCostAuthority costAuthority)
        {
            _costAuthority = costAuthority;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            RemoveExpired(new RuntimeFrame(context.FrameIndex));
        }

        public bool TryResolveInputIntent(
            int casterEntityId,
            int inputIntentId,
            RuntimeFrame frame,
            out AbilityActivationResult result)
        {
            if (!Definitions.TryGetByInputIntent(inputIntentId, out AbilityDefinitionConfig ability))
            {
                result = new AbilityActivationResult(
                    false,
                    null,
                    AbilityActionBindingConfig.Empty,
                    AbilityActivationReason.MissingInputBinding,
                    0L);
                return false;
            }

            return TryEvaluate(casterEntityId, ability, frame, out result);
        }

        public bool TryEvaluate(
            int casterEntityId,
            AbilityDefinitionConfig ability,
            RuntimeFrame frame,
            out AbilityActivationResult result)
        {
            if (casterEntityId <= 0 || ability == null)
            {
                result = new AbilityActivationResult(
                    false,
                    ability,
                    ability?.ActionBinding,
                    AbilityActivationReason.InvalidActivation,
                    0L);
                return false;
            }

            if (ability.ActionBinding.ActionIntentId <= 0)
            {
                result = new AbilityActivationResult(
                    false,
                    ability,
                    ability.ActionBinding,
                    AbilityActivationReason.MissingActionIntent,
                    0L);
                return false;
            }

            var key = new CooldownKey(casterEntityId, ability.AbilityId);
            if (_cooldownEndFrames.TryGetValue(key, out long endFrame) && endFrame > frame.Value)
            {
                long remaining = endFrame - frame.Value;
                result = new AbilityActivationResult(
                    false,
                    ability,
                    ability.ActionBinding,
                    AbilityActivationReason.OnCooldown,
                    remaining);
                return false;
            }

            if (ability.Costs.Count > 0)
            {
                if (_costAuthority == null)
                {
                    result = new AbilityActivationResult(
                        false,
                        ability,
                        ability.ActionBinding,
                        AbilityActivationReason.MissingCostAuthority,
                        0L);
                    return false;
                }

                if (!_costAuthority.CanAfford(casterEntityId, ability.Costs, out int missingAttributeId))
                {
                    result = new AbilityActivationResult(
                        false,
                        ability,
                        ability.ActionBinding,
                        AbilityActivationReason.InsufficientResource,
                        0L,
                        missingAttributeId);
                    return false;
                }
            }

            result = new AbilityActivationResult(
                true,
                ability,
                ability.ActionBinding,
                AbilityActivationReason.None,
                0L);
            return true;
        }

        public void CommitActivation(int casterEntityId, AbilityDefinitionConfig ability, RuntimeFrame frame)
        {
            TryCommitActivation(casterEntityId, ability, frame, out _);
        }

        /// <summary>Commits configured resource costs and cooldown after the bound action was accepted.</summary>
        /// <remarks>NoAlloc on success after initialization.</remarks>
        public bool TryCommitActivation(
            int casterEntityId,
            AbilityDefinitionConfig ability,
            RuntimeFrame frame,
            out AbilityActivationFailure failure)
        {
            failure = new AbilityActivationFailure(AbilityActivationReason.None);
            if (casterEntityId <= 0 || ability == null)
            {
                failure = new AbilityActivationFailure(AbilityActivationReason.InvalidActivation);
                return false;
            }

            // Final admission is live. Reject before resource publication or frame arithmetic.
            if (ability.ActionBinding.ActionIntentId <= 0)
            {
                failure = new AbilityActivationFailure(AbilityActivationReason.MissingActionIntent);
                return false;
            }
            var key = new CooldownKey(casterEntityId, ability.AbilityId);
            if (_cooldownEndFrames.TryGetValue(key, out long previousEnd) && previousEnd > frame.Value)
            {
                failure = new AbilityActivationFailure(AbilityActivationReason.OnCooldown);
                return false;
            }
            if (ability.Cooldown.Frames > long.MaxValue - frame.Value)
            {
                failure = new AbilityActivationFailure(AbilityActivationReason.InvalidActivation);
                return false;
            }

            if (ability.Costs.Count > 0)
            {
                if (_costAuthority == null)
                {
                    failure = new AbilityActivationFailure(AbilityActivationReason.MissingCostAuthority);
                    return false;
                }

                if (!_costAuthority.TryCommitCosts(casterEntityId, ability.Costs, out int missingAttributeId))
                {
                    failure = new AbilityActivationFailure(
                        AbilityActivationReason.InsufficientResource,
                        missingAttributeId);
                    return false;
                }
            }

            if (ability.Cooldown.Frames > 0L)
            {
                long endFrame = frame.Value + ability.Cooldown.Frames;
                _cooldownEndFrames[new CooldownKey(casterEntityId, ability.AbilityId)] = endFrame;
            }
            return true;
        }

        public long GetRemainingCooldownFrames(int casterEntityId, AbilityDefinitionConfig ability, RuntimeFrame frame)
        {
            if (casterEntityId <= 0 || ability == null)
                return 0L;

            return _cooldownEndFrames.TryGetValue(new CooldownKey(casterEntityId, ability.AbilityId), out long endFrame) &&
                endFrame > frame.Value
                ? endFrame - frame.Value
                : 0L;
        }

        public int CopyCooldownEntries(List<AbilityRuntimeCooldownEntry> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            foreach (KeyValuePair<CooldownKey, long> pair in _cooldownEndFrames)
            {
                output.Add(new AbilityRuntimeCooldownEntry(
                    pair.Key.EntityId,
                    pair.Key.AbilityId,
                    pair.Value));
            }

            output.Sort(start, output.Count - start, AbilityRuntimeCooldownEntryComparer.Instance);
            return output.Count - start;
        }

        public AbilityRuntimeState CreateStateSnapshot()
        {
            return CaptureRuntimeState();
        }

        public AbilityRuntimeDebugSnapshot CreateDebugSnapshot()
        {
            AbilityRuntimeState state = CaptureRuntimeState();
            return new AbilityRuntimeDebugSnapshot(
                state.ModuleId,
                state.DefinitionHash,
                state.Cooldowns);
        }

        internal AbilityRuntimeState CaptureRuntimeState()
        {
            var cooldowns = new List<AbilityRuntimeCooldownEntry>();
            CopyCooldownEntries(cooldowns);
            return new AbilityRuntimeState(
                AbilityRuntimeState.CurrentSchemaVersion,
                ModuleId,
                Definitions.ComputeStableHash(),
                cooldowns);
        }

        internal void RestoreRuntimeState(AbilityRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _cooldownEndFrames.Clear();
            for (int i = 0; i < state.Cooldowns.Count; i++)
            {
                AbilityRuntimeCooldownEntry entry = state.Cooldowns[i];
                if (entry.CasterEntityId > 0 && entry.AbilityId > 0 && entry.EndFrame > 0L)
                    _cooldownEndFrames[new CooldownKey(entry.CasterEntityId, entry.AbilityId)] = entry.EndFrame;
            }
        }

        private void RemoveExpired(RuntimeFrame frame)
        {
            if (_cooldownEndFrames.Count == 0)
                return;

            var remove = new List<CooldownKey>();
            foreach (KeyValuePair<CooldownKey, long> pair in _cooldownEndFrames)
            {
                if (pair.Value <= frame.Value)
                    remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                _cooldownEndFrames.Remove(remove[i]);
        }

        private readonly struct CooldownKey : IEquatable<CooldownKey>
        {
            public CooldownKey(int entityId, int abilityId)
            {
                EntityId = entityId;
                AbilityId = abilityId;
            }

            public int EntityId { get; }
            public int AbilityId { get; }

            public bool Equals(CooldownKey other)
            {
                return EntityId == other.EntityId && AbilityId == other.AbilityId;
            }

            public override bool Equals(object obj)
            {
                return obj is CooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ AbilityId;
                }
            }
        }
    }

    public sealed class AbilityRuntimeCooldownEntry
    {
        public AbilityRuntimeCooldownEntry(int casterEntityId, int abilityId, long endFrame)
        {
            if (casterEntityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(casterEntityId), "Ability cooldown caster entity id must be positive.");
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Ability cooldown ability id must be positive.");
            if (endFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Ability cooldown end frame cannot be negative.");

            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            EndFrame = endFrame;
        }

        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public long EndFrame { get; }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(AbilityRuntimeHashFields.CasterEntityId, CasterEntityId);
            accumulator.AddInt(AbilityRuntimeHashFields.AbilityId, AbilityId);
            accumulator.AddLong(AbilityRuntimeHashFields.EndFrame, EndFrame);
        }
    }

    internal static class AbilityRuntimeHashFields
    {
        public const int ModuleId = 1;
        public const int DefinitionHash = 2;
        public const int Cooldowns = 3;
        public const int CasterEntityId = 1;
        public const int AbilityId = 2;
        public const int EndFrame = 3;
    }

    public sealed class AbilityRuntimeState
    {
        public const int CurrentSchemaVersion = 0;

        public AbilityRuntimeState(
            int schemaVersion,
            string moduleId,
            long definitionHash,
            IReadOnlyList<AbilityRuntimeCooldownEntry> cooldowns)
        {
            SchemaVersion = schemaVersion;
            ModuleId = moduleId ?? string.Empty;
            DefinitionHash = definitionHash;
            Cooldowns = CopyCooldowns(cooldowns);
        }

        public int SchemaVersion { get; }
        public string ModuleId { get; }
        public long DefinitionHash { get; }
        public IReadOnlyList<AbilityRuntimeCooldownEntry> Cooldowns { get; }

        private static ReadOnlyCollection<AbilityRuntimeCooldownEntry> CopyCooldowns(
            IReadOnlyList<AbilityRuntimeCooldownEntry> source)
        {
            var values = source == null
                ? new List<AbilityRuntimeCooldownEntry>()
                : new List<AbilityRuntimeCooldownEntry>(source);
            values.Sort(AbilityRuntimeCooldownEntryComparer.Instance);
            return new ReadOnlyCollection<AbilityRuntimeCooldownEntry>(values);
        }
    }

    public sealed class AbilityRuntimeDebugSnapshot
    {
        public AbilityRuntimeDebugSnapshot(
            string moduleId,
            long definitionHash,
            IReadOnlyList<AbilityRuntimeCooldownEntry> cooldowns)
        {
            ModuleId = moduleId ?? string.Empty;
            DefinitionHash = definitionHash;
            Cooldowns = cooldowns == null
                ? new ReadOnlyCollection<AbilityRuntimeCooldownEntry>(new List<AbilityRuntimeCooldownEntry>())
                : new ReadOnlyCollection<AbilityRuntimeCooldownEntry>(new List<AbilityRuntimeCooldownEntry>(cooldowns));
        }

        public string ModuleId { get; }
        public long DefinitionHash { get; }
        public IReadOnlyList<AbilityRuntimeCooldownEntry> Cooldowns { get; }
        public int ActiveCooldownCount => Cooldowns.Count;
    }

    public sealed class AbilityRuntimeHashContributor : IRuntimeHashContributor
    {
        private readonly AbilityRuntimeModule _module;

        public AbilityRuntimeHashContributor(
            AbilityRuntimeModule module,
            string contributorId = "mxframework.ability")
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            DiagnosticContributorId = contributorId ?? string.Empty;
        }

        public int ContributorId => RuntimeHashContributorIds.AbilityRuntime;
        public int HashSchemaId => RuntimeHashSchemaIds.AbilityRuntime;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;
        public string DiagnosticContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            AbilityRuntimeState state = _module.CaptureRuntimeState();
            RuntimeHashDigest moduleIdDigest = _module.ModuleIdDigest;
            accumulator.AddDigest(AbilityRuntimeHashFields.ModuleId, in moduleIdDigest);
            accumulator.AddLong(AbilityRuntimeHashFields.DefinitionHash, state.DefinitionHash);
            accumulator.BeginArray(AbilityRuntimeHashFields.Cooldowns, state.Cooldowns.Count);
            for (int i = 0; i < state.Cooldowns.Count; i++)
            {
                accumulator.BeginElement(i);
                state.Cooldowns[i].Contribute(accumulator);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    public sealed class AbilityRuntimeSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string CustomStateTypeId = "mxframework.ability.runtime";
        public const int CustomStateSchemaVersion = AbilityRuntimeState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        private readonly AbilityRuntimeModule _module;

        public AbilityRuntimeSaveStateProvider(AbilityRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            AbilityRuntimeState state = _module.CaptureRuntimeState();
            string payloadJson = JsonConvert.SerializeObject(AbilityRuntimeStateDto.FromState(state), JsonSettings);
            var customState = new RuntimeCustomState(3310, CustomStateTypeId, CustomStateSchemaVersion, payloadJson);
            var moduleState = new RuntimeModuleSaveState(_module.ModuleId, CustomStateSchemaVersion, customState);
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                frameworkVersion: string.Empty,
                configVersion: string.Empty,
                resourceCatalogVersion: string.Empty,
                frame: 0L,
                entities: null,
                globalCounters: null,
                moduleStates: new[] { moduleState });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Ability restore state is missing."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate != null && string.Equals(candidate.ModuleId, _module.ModuleId, StringComparison.Ordinal))
                {
                    moduleState = candidate;
                    break;
                }
            }

            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "Ability module state was not found."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + _module.ModuleId + "']",
                    "Ability custom state type mismatch."));
            }

            if (moduleState.CustomState.SchemaVersion != CustomStateSchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + _module.ModuleId + "']",
                    "Unsupported Ability custom state schema.",
                    moduleState.CustomState.SchemaVersion,
                    CustomStateSchemaVersion));
            }

            try
            {
                AbilityRuntimeStateDto dto = JsonConvert.DeserializeObject<AbilityRuntimeStateDto>(
                    moduleState.CustomState.PayloadJson,
                    JsonSettings);
                if (dto == null)
                    throw new JsonException("Ability state payload was empty.");

                AbilityRuntimeState state = dto.ToState(_module.ModuleId);
                if (state.SchemaVersion != AbilityRuntimeState.CurrentSchemaVersion)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + _module.ModuleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported Ability runtime state schema.",
                        state.SchemaVersion,
                        AbilityRuntimeState.CurrentSchemaVersion));
                }

                long currentDefinitionHash = _module.Definitions.ComputeStableHash();
                if (state.DefinitionHash != currentDefinitionHash)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.CustomStateMismatch,
                        "$.moduleStates['" + _module.ModuleId + "'].customState.definitionHash",
                        "Ability definition hash mismatch."));
                }

                _module.RestoreRuntimeState(state);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + _module.ModuleId + "'].customState.payloadJson",
                    "Ability custom state could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private sealed class AbilityRuntimeStateDto
        {
            public int schemaVersion;
            public long definitionHash;
            public AbilityRuntimeCooldownEntryDto[] cooldowns;

            public static AbilityRuntimeStateDto FromState(AbilityRuntimeState state)
            {
                return new AbilityRuntimeStateDto
                {
                    schemaVersion = state.SchemaVersion,
                    definitionHash = state.DefinitionHash,
                    cooldowns = Convert(state.Cooldowns)
                };
            }

            public AbilityRuntimeState ToState(string moduleId)
            {
                return new AbilityRuntimeState(
                    schemaVersion,
                    moduleId,
                    definitionHash,
                    Convert(cooldowns));
            }

            private static AbilityRuntimeCooldownEntryDto[] Convert(IReadOnlyList<AbilityRuntimeCooldownEntry> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<AbilityRuntimeCooldownEntryDto>();

                var result = new AbilityRuntimeCooldownEntryDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = AbilityRuntimeCooldownEntryDto.From(values[i]);

                return result;
            }

            private static List<AbilityRuntimeCooldownEntry> Convert(AbilityRuntimeCooldownEntryDto[] values)
            {
                var result = new List<AbilityRuntimeCooldownEntry>();
                if (values == null)
                    return result;

                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToEntry());

                return result;
            }
        }

        private sealed class AbilityRuntimeCooldownEntryDto
        {
            public int casterEntityId;
            public int abilityId;
            public long endFrame;

            public static AbilityRuntimeCooldownEntryDto From(AbilityRuntimeCooldownEntry entry)
            {
                return new AbilityRuntimeCooldownEntryDto
                {
                    casterEntityId = entry.CasterEntityId,
                    abilityId = entry.AbilityId,
                    endFrame = entry.EndFrame
                };
            }

            public AbilityRuntimeCooldownEntry ToEntry()
            {
                return new AbilityRuntimeCooldownEntry(casterEntityId, abilityId, endFrame);
            }
        }
    }

    internal sealed class AbilityRuntimeCooldownEntryComparer : IComparer<AbilityRuntimeCooldownEntry>
    {
        public static readonly AbilityRuntimeCooldownEntryComparer Instance =
            new AbilityRuntimeCooldownEntryComparer();

        public int Compare(AbilityRuntimeCooldownEntry left, AbilityRuntimeCooldownEntry right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int compare = left.CasterEntityId.CompareTo(right.CasterEntityId);
            if (compare != 0)
                return compare;
            return left.AbilityId.CompareTo(right.AbilityId);
        }
    }

    public static class AbilityDefinitionJsonImporter
    {
        public const string CurrentSchemaVersion = "3";

        public static AbilityDefinitionRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new AbilityDefinitionRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static AbilityDefinitionRegistry Import(string json, string sourcePath = "")
        {
            var registry = new AbilityDefinitionRegistry();
            if (string.IsNullOrWhiteSpace(json))
                return registry;

            JObject root = JObject.Parse(json);
            JArray abilities = root["abilities"] as JArray;
            JToken schema = root["schemaVersion"];
            if (schema == null || schema.Type != JTokenType.String
                || schema.Value<string>() != CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported ability catalog schema; expected " + CurrentSchemaVersion + ". Regenerate legacy content before import.");
            for (int i = 0; abilities != null && i < abilities.Count; i++)
            {
                if (!(abilities[i] is JObject fields))
                    continue;

                AbilityDefinitionConfig definition = ReadAbility(fields);
                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid ability catalog entry at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
                }
            }

            return registry;
        }

        private static AbilityDefinitionConfig ReadAbility(JObject fields)
        {
            if (fields?["damageProfileId"] != null)
                throw new InvalidOperationException("ability.damageProfileId is no longer supported; configure ability.damage.");
            return new AbilityDefinitionConfig(
                ReadInt(fields, "abilityId"),
                ReadString(fields, "abilityStableId", ReadString(fields, "stableId")),
                ReadString(fields, "displayName"),
                ReadInput(fields["input"] as JObject),
                ReadCooldown(fields["cooldown"] as JObject),
                ReadCosts(fields["costs"] as JArray),
                ReadDefinitionId(fields, "targetPolicyId"),
                ReadActionBinding(fields["actionBinding"] as JObject),
                ReadDamage(fields["damage"] as JObject),
                ReadDefinitionId(fields, "presentationProfileId"),
                ReadDefinitionId(fields, "effectTimelineId"),
                ReadString(fields, "targetPolicyLabel"),
                ReadString(fields, "presentationProfileLabel"),
                ReadString(fields, "effectTimelineLabel"));
        }

        private static AbilityDamageConfig ReadDamage(JObject fields)
        {
            if (fields == null)
                return new AbilityDamageConfig(
                    0,
                    0,
                    0,
                    Array.Empty<int>(),
                    0);

            return new AbilityDamageConfig(
                ReadDefinitionId(fields, "damageTypeId"),
                ReadInt(fields, "baseDamage"),
                ReadInt(fields, "impactPower"),
                ReadIdArray(fields["tags"] as JArray),
                ReadDefinitionId(fields, "analysisCategory"),
                ReadInt(fields, "launchPower"),
                ReadString(fields, "damageTypeLabel"),
                ReadString(fields, "analysisCategoryLabel"),
                ReadStringArray(fields["tagLabels"] as JArray),
                ReadDamageAttributeScales(fields["sourceAttributeScales"] as JArray));
        }

        private static AbilityDamageAttributeScale[] ReadDamageAttributeScales(JArray fields)
        {
            if (fields == null || fields.Count == 0)
                return Array.Empty<AbilityDamageAttributeScale>();
            var values = new AbilityDamageAttributeScale[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                if (!(fields[i] is JObject item))
                    throw new ArgumentException("sourceAttributeScales entry must be an object.");
                int attributeId = ReadDefinitionId(item, "attributeId");
                float scale = item["scale"]?.Value<float>() ?? 0f;
                if (attributeId <= 0 || scale < 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                    throw new ArgumentException("sourceAttributeScales entry is invalid.");
                values[i] = new AbilityDamageAttributeScale(attributeId, scale);
            }
            return values;
        }

        private static AbilityInputBindingConfig ReadInput(JObject fields)
        {
            if (fields == null)
                return AbilityInputBindingConfig.Empty;

            return new AbilityInputBindingConfig(
                ReadDefinitionId(fields, "inputIntentId"),
                ReadDefinitionId(fields, "slotId"),
                ReadString(fields, "inputIntentLabel"),
                ReadString(fields, "slotLabel"));
        }

        private static AbilityCooldownConfig ReadCooldown(JObject fields)
        {
            if (fields == null)
                return AbilityCooldownConfig.None;

            return new AbilityCooldownConfig(
                ReadLong(fields, "frames"),
                ReadDefinitionId(fields, "groupId"),
                ReadString(fields, "groupLabel"));
        }

        private static AbilityCostConfig[] ReadCosts(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<AbilityCostConfig>();

            var costs = new List<AbilityCostConfig>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject fields))
                    continue;

                costs.Add(new AbilityCostConfig(
                    ReadInt(fields, "attributeId"),
                    ReadInt(fields, "amount")));
            }

            return costs.ToArray();
        }

        private static AbilityActionBindingConfig ReadActionBinding(JObject fields)
        {
            if (fields == null)
                return AbilityActionBindingConfig.Empty;

            return new AbilityActionBindingConfig(
                new CharacterActionId(ReadInt(fields, "actionId")),
                ReadDefinitionId(fields, "actionIntentId"),
                ReadString(fields, "actionIntentLabel"));
        }

        private static int ReadDefinitionId(JObject fields, string name)
        {
            JToken value = fields?[name];
            if (value == null) return 0;
            if (value.Type != JTokenType.Integer) throw new ArgumentException("Ability identity must be an authored integer: " + name);
            int id = value.Value<int>();
            if (id < 0) throw new ArgumentException("Ability identity cannot be negative: " + name);
            return id;
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static string[] ReadStringArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<string>();

            var values = new List<string>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }

            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        private static int[] ReadIdArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<int>();

            var values = new List<int>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                JToken token = array[i];
                if (token == null)
                    continue;
                if (token.Type != JTokenType.Integer)
                    throw new ArgumentException("Ability identity must be an authored integer: tags[" + i + "]");
                int value = token.Value<int>();
                if (value < 0)
                    throw new ArgumentException("Ability identity cannot be negative: tags[" + i + "]");
                values.Add(value);
            }

            return values.ToArray();
        }

        private static int ReadInt(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return 0;

            JToken token = fields[name];
            return token == null ? 0 : token.Value<int>();
        }

        private static long ReadLong(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return 0L;

            JToken token = fields[name];
            return token == null ? 0L : token.Value<long>();
        }
    }
}
