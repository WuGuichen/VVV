using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayScatterPressureReason : IEquatable<GameplayScatterPressureReason>
    {
        public const int ApplyPressureCode = 1;
        public const int UpdateCohesionLimitCode = 2;
        public const int ImpactPowerCode = 3;
        public const int RecoveryCode = 4;
        public GameplayScatterPressureReason(int code, int argumentId = 0)
        {
            if (code < 0 || argumentId < 0) throw new ArgumentOutOfRangeException(nameof(code));
            if (code == 0 && argumentId != 0) throw new ArgumentException("An absent reason cannot contain an identity argument.");
            Code = code; ArgumentId = argumentId;
        }
        public int Code { get; }
        public int ArgumentId { get; }
        public bool IsNone => Code == 0;
        public bool Equals(GameplayScatterPressureReason other) => Code == other.Code && ArgumentId == other.ArgumentId;
        public override bool Equals(object obj) => obj is GameplayScatterPressureReason other && Equals(other);
        public override int GetHashCode() => unchecked(Code * 397 ^ ArgumentId);
        public static bool operator ==(GameplayScatterPressureReason left, GameplayScatterPressureReason right) => left.Equals(right);
        public static bool operator !=(GameplayScatterPressureReason left, GameplayScatterPressureReason right) => !left.Equals(right);
        public static implicit operator GameplayScatterPressureReason(int code) => new GameplayScatterPressureReason(code);
        /// <remarks>Diagnostic display only; numeric fields carry state semantics.</remarks>
        public override string ToString()
        {
            switch (Code)
            {
                case 0: return string.Empty;
                case ApplyPressureCode: return "ApplyScatterPressure";
                case UpdateCohesionLimitCode: return "UpdateCohesionLimit";
                case ImpactPowerCode: return ArgumentId == 0 ? "ImpactPower" : "ImpactPower:" + ArgumentId;
                case RecoveryCode: return "RecoverScatterPressure";
                case 5: return "GuardPressureApplied";
                case 6: return "GuardPressureRecovered";
                case 7: return "GuardBreak";
                default: return "Reason:" + Code + ":" + ArgumentId;
            }
        }
    }
}
