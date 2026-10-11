using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestActionCommitFailureKind
    {
        None = 0, Qualification = 1, AbilityOwnerResolution = 2, AbilityOwnerMismatch = 3,
        AbilityUnavailable = 4, Reentered = 5, TargetUnavailable = 6, TargetRejected = 7,
        GatewayDiagnostic = 8, Activation = 9
    }

    /// <summary>Preserves commit failure category and all former diagnostic parameters as numbers.</summary>
    public readonly struct CharacterTestActionUseFailure : IEquatable<CharacterTestActionUseFailure>
    {
        public const int SchemaId = RuntimeHashSchemaIds.CharacterTestActionUseFailure;
        public const int SchemaVersion = 1;
        private const int KindKey = 1, StatusKey = 2, RequirementKey = 3, AttributeKey = 4,
            AbilityKey = 5, DiagnosticKey = 6, ActivationKey = 7;
        [JsonConstructor]
        public CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind kind,
            CharacterTestActionUseStatus status = CharacterTestActionUseStatus.Available,
            int requirementIndex = -1, int attributeId = 0, int abilityId = 0,
            CharacterActionDiagnosticCode diagnosticCode = CharacterActionDiagnosticCode.None,
            AbilityActivationReason activationReason = AbilityActivationReason.None)
        {
            Kind = kind; Status = status; RequirementIndex = requirementIndex; AttributeId = attributeId;
            AbilityId = abilityId; DiagnosticCode = diagnosticCode; ActivationReason = activationReason;
        }
        public CharacterTestActionCommitFailureKind Kind { get; }
        public CharacterTestActionUseStatus Status { get; }
        public int RequirementIndex { get; }
        public int AttributeId { get; }
        public int AbilityId { get; }
        public CharacterActionDiagnosticCode DiagnosticCode { get; }
        public AbilityActivationReason ActivationReason { get; }
        [JsonIgnore]
        public bool IsValid => (int)Kind >= 0 && (int)Kind <= 9
            && (int)Status >= 0 && (int)Status <= 13 && RequirementIndex >= -1
            && AttributeId >= 0 && AbilityId >= 0
            && Enum.IsDefined(typeof(CharacterActionDiagnosticCode), DiagnosticCode)
            && Enum.IsDefined(typeof(AbilityActivationReason), ActivationReason)
            && (Kind != CharacterTestActionCommitFailureKind.GatewayDiagnostic || DiagnosticCode != CharacterActionDiagnosticCode.None)
            && (Kind == CharacterTestActionCommitFailureKind.GatewayDiagnostic || DiagnosticCode == CharacterActionDiagnosticCode.None)
            && (Kind == CharacterTestActionCommitFailureKind.Activation
                ? (ActivationReason == AbilityActivationReason.InvalidActivation
                    || ActivationReason == AbilityActivationReason.MissingCostAuthority
                    || ActivationReason == AbilityActivationReason.InsufficientResource)
                : ActivationReason == AbilityActivationReason.None)
            && (Kind != CharacterTestActionCommitFailureKind.None
                || (Status == CharacterTestActionUseStatus.Available && RequirementIndex == 0 && AttributeId == 0
                    && AbilityId == 0 && DiagnosticCode == CharacterActionDiagnosticCode.None && ActivationReason == AbilityActivationReason.None));

        public void ContributeHash(RuntimeHashAccumulator hash, int key)
        {
            hash.BeginObject(key, SchemaId, SchemaVersion);
            hash.AddInt(KindKey, (int)Kind); hash.AddInt(StatusKey, (int)Status);
            hash.AddInt(RequirementKey, RequirementIndex); hash.AddInt(AttributeKey, AttributeId); hash.AddInt(AbilityKey, AbilityId);
            hash.AddInt(DiagnosticKey, (int)DiagnosticCode); hash.AddInt(ActivationKey, (int)ActivationReason);
            hash.EndObject();
        }
        public bool Equals(CharacterTestActionUseFailure other) => Kind == other.Kind && Status == other.Status
            && RequirementIndex == other.RequirementIndex && AttributeId == other.AttributeId
            && AbilityId == other.AbilityId && DiagnosticCode == other.DiagnosticCode && ActivationReason == other.ActivationReason;
        public override bool Equals(object obj) => obj is CharacterTestActionUseFailure other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ (int)Status; hash = hash * 397 ^ RequirementIndex;
                hash = hash * 397 ^ AttributeId; hash = hash * 397 ^ AbilityId;
                hash = hash * 397 ^ (int)DiagnosticCode; return hash * 397 ^ (int)ActivationReason;
            }
        }
        [JsonIgnore]
        public string Reason
        {
            get
            {
                if (Kind == CharacterTestActionCommitFailureKind.None) return string.Empty;
                string detail;
                switch (Kind)
                {
                    case CharacterTestActionCommitFailureKind.Qualification:
                        detail = Status + ":requirement=" + RequirementIndex + ":attribute=" + AttributeId; break;
                    case CharacterTestActionCommitFailureKind.AbilityOwnerResolution: detail = Status.ToString(); break;
                    case CharacterTestActionCommitFailureKind.AbilityOwnerMismatch: detail = "AbilityOwnerMismatch"; break;
                    case CharacterTestActionCommitFailureKind.AbilityUnavailable: detail = "AbilityUnavailable"; break;
                    case CharacterTestActionCommitFailureKind.Reentered: detail = "ActionCommitReentered"; break;
                    case CharacterTestActionCommitFailureKind.TargetUnavailable: detail = "ActionCommitTargetUnavailable"; break;
                    case CharacterTestActionCommitFailureKind.TargetRejected: detail = "ActionCommitTargetRejected"; break;
                    case CharacterTestActionCommitFailureKind.GatewayDiagnostic: detail = CharacterActionDiagnosticCodeText.Get(DiagnosticCode); break;
                    case CharacterTestActionCommitFailureKind.Activation: detail = AbilityActivationResult.FormatReason(ActivationReason, AttributeId); break;
                    default: throw new InvalidOperationException("Unknown action commit failure kind.");
                }
                return "ActionUse.CommitFailed:" + detail;
            }
        }
    }
}
