using System;

namespace MxFramework.CharacterAction
{
    public static class CharacterActionDiagnosticCodes
    {
        public const string ReactionContextMissingSource = "ACT_REACTION_CONTEXT_MISSING_SOURCE";
        public const string ReactionContextIncomplete = "ACT_REACTION_CONTEXT_INCOMPLETE";
        public const string ReactionRuleRequiresHitContext = "ACT_REACTION_RULE_REQUIRES_HIT_CONTEXT";
        public const string ReactionRuleNoTarget = "ACT_REACTION_RULE_NO_TARGET";
        public const string PhaseCombatAnchorMissing = "ACT_PHASE_COMBAT_ANCHOR_MISSING";
        public const string PhaseCombatRangeMismatch = "ACT_PHASE_COMBAT_RANGE_MISMATCH";
        public const string CharacterCancelRejected = "ACT_CHARACTER_CANCEL_REJECTED";
        public const string CombatCancelRejected = "ACT_COMBAT_CANCEL_REJECTED";
    }

    public enum CharacterActionDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct CharacterActionDiagnostic : IEquatable<CharacterActionDiagnostic>
    {
        public CharacterActionDiagnostic(
            string code,
            CharacterActionDiagnosticSeverity severity,
            string message)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Diagnostic code cannot be empty.", nameof(code));

            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public bool Equals(CharacterActionDiagnostic other)
        {
            return string.Equals(Code, other.Code, StringComparison.Ordinal)
                && Severity == other.Severity
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Code == null ? 0 : Code.GetHashCode();
                hash = (hash * 397) ^ (int)Severity;
                hash = (hash * 397) ^ (Message == null ? 0 : Message.GetHashCode());
                return hash;
            }
        }

        public static CharacterActionDiagnostic Warning(string code, string message)
        {
            return new CharacterActionDiagnostic(code, CharacterActionDiagnosticSeverity.Warning, message);
        }

        public static CharacterActionDiagnostic Error(string code, string message)
        {
            return new CharacterActionDiagnostic(code, CharacterActionDiagnosticSeverity.Error, message);
        }
    }
}
