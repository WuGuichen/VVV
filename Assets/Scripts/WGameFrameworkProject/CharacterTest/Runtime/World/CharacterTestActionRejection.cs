using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    // Values belong to the CharacterTest rejection protocol; retired values must not be reused.
    public enum CharacterTestActionRejectCode
    {
        None = 0,
        BattleStateBlocked = 2001,
        TurnInPlaceNoAttack = 2002,
        AttackOpportunityBlocked = 2003,
        CombatEvadeRecoveryDodgeWindowClosed = 2004,
        CombatEvadeRollWindowMissing = 2005,
        CombatEvadeRollAlreadyActive = 2006,
        CombatEvadeRollBuffered = 2007,
        CombatEvadeRollWindowClosed = 2008,
        CombatEvadeAbilityDefinitionMissing = 2009,
        MissingAbilityRuntime = 2010,
        MissingAbilityDefinition = 2011,
        SkillSlotEmpty = 2012,
        SkillSlotAbilityMissing = 2013,
        ActionFrozenByStasis = 2014,
        ActionFrozenByStun = 2015,
        ActionFrozenByParalysis = 2016,
        AbilityMissingInputBinding = 3001,
        AbilityInvalidActivation = 3002,
        AbilityMissingActionIntent = 3003,
        AbilityOnCooldown = 3004,
        AbilityMissingCostAuthority = 3005,
        AbilityInsufficientResource = 3006,
    }

    /// <summary>Fixed reason identity and its numeric parameter; display text is generated only on request.</summary>
    public readonly struct CharacterTestActionRejection : IEquatable<CharacterTestActionRejection>
    {
        public CharacterTestActionRejection(int code, int detailId = 0)
        {
            bool diagnostic = CharacterActionDiagnosticCodeText.IsDefined((CharacterActionDiagnosticCode)code);
            bool local = code >= 2001 && code <= 2016;
            bool ability = code >= 3001 && code <= 3006;
            if (!diagnostic && !local && !ability) throw new ArgumentOutOfRangeException(nameof(code));
            if (detailId < 0 || (code != (int)CharacterTestActionRejectCode.AbilityInsufficientResource && detailId != 0))
                throw new ArgumentOutOfRangeException(nameof(detailId));
            Code = code;
            DetailId = detailId;
        }
        public int Code { get; }
        public int DetailId { get; }
        public bool IsNone => Code == 0;
        public static implicit operator CharacterTestActionRejection(CharacterActionDiagnosticCode value)
            => new CharacterTestActionRejection((int)value);
        public static implicit operator CharacterTestActionRejection(CharacterTestActionRejectCode value)
            => new CharacterTestActionRejection((int)value);
        public static CharacterTestActionRejection FromAbility(AbilityActivationReason code, int detailId)
        {
            switch (code)
            {
                case AbilityActivationReason.None: return default;
                case AbilityActivationReason.MissingInputBinding: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityMissingInputBinding, detailId);
                case AbilityActivationReason.InvalidActivation: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityInvalidActivation, detailId);
                case AbilityActivationReason.MissingActionIntent: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityMissingActionIntent, detailId);
                case AbilityActivationReason.OnCooldown: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityOnCooldown, detailId);
                case AbilityActivationReason.MissingCostAuthority: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityMissingCostAuthority, detailId);
                case AbilityActivationReason.InsufficientResource: return new CharacterTestActionRejection((int)CharacterTestActionRejectCode.AbilityInsufficientResource, detailId);
                default: throw new ArgumentOutOfRangeException(nameof(code));
            }
        }
        public bool Equals(CharacterTestActionRejection other) => Code == other.Code && DetailId == other.DetailId;
        public override bool Equals(object obj) => obj is CharacterTestActionRejection other && Equals(other);
        public override int GetHashCode() => unchecked(Code * 397 ^ DetailId);
        public override string ToString()
        {
            switch ((CharacterTestActionRejectCode)Code)
            {
                case CharacterTestActionRejectCode.BattleStateBlocked: return "BattleStateBlocked";
                case CharacterTestActionRejectCode.TurnInPlaceNoAttack: return "TurnInPlaceNoAttack";
                case CharacterTestActionRejectCode.AttackOpportunityBlocked: return "AttackOpportunityBlocked";
                case CharacterTestActionRejectCode.CombatEvadeRecoveryDodgeWindowClosed: return "CombatEvadeRecoveryDodgeWindowClosed";
                case CharacterTestActionRejectCode.CombatEvadeRollWindowMissing: return "CombatEvadeRollWindowMissing";
                case CharacterTestActionRejectCode.CombatEvadeRollAlreadyActive: return "CombatEvadeRollAlreadyActive";
                case CharacterTestActionRejectCode.CombatEvadeRollBuffered: return "CombatEvadeRollBuffered";
                case CharacterTestActionRejectCode.CombatEvadeRollWindowClosed: return "CombatEvadeRollWindowClosed";
                case CharacterTestActionRejectCode.CombatEvadeAbilityDefinitionMissing: return "CombatEvadeAbilityDefinitionMissing";
                case CharacterTestActionRejectCode.MissingAbilityRuntime: return "MissingAbilityRuntime";
                case CharacterTestActionRejectCode.MissingAbilityDefinition: return "MissingAbilityDefinition";
                case CharacterTestActionRejectCode.SkillSlotEmpty: return "SkillSlotEmpty";
                case CharacterTestActionRejectCode.SkillSlotAbilityMissing: return "SkillSlotAbilityMissing";
                case CharacterTestActionRejectCode.ActionFrozenByStasis: return "ActionFrozenByStasis";
                case CharacterTestActionRejectCode.ActionFrozenByStun: return "ActionFrozenByStun";
                case CharacterTestActionRejectCode.ActionFrozenByParalysis: return "ActionFrozenByParalysis";
                case CharacterTestActionRejectCode.AbilityMissingInputBinding: return AbilityActivationResult.FormatReason(AbilityActivationReason.MissingInputBinding, DetailId);
                case CharacterTestActionRejectCode.AbilityInvalidActivation: return AbilityActivationResult.FormatReason(AbilityActivationReason.InvalidActivation, DetailId);
                case CharacterTestActionRejectCode.AbilityMissingActionIntent: return AbilityActivationResult.FormatReason(AbilityActivationReason.MissingActionIntent, DetailId);
                case CharacterTestActionRejectCode.AbilityOnCooldown: return AbilityActivationResult.FormatReason(AbilityActivationReason.OnCooldown, DetailId);
                case CharacterTestActionRejectCode.AbilityMissingCostAuthority: return AbilityActivationResult.FormatReason(AbilityActivationReason.MissingCostAuthority, DetailId);
                case CharacterTestActionRejectCode.AbilityInsufficientResource: return AbilityActivationResult.FormatReason(AbilityActivationReason.InsufficientResource, DetailId);
                default: return CharacterActionDiagnosticCodeText.Get((CharacterActionDiagnosticCode)Code);
            }
        }
    }
}


