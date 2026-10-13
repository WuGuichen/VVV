using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    /// <summary>Restricts the serialized HP debit after armor, using the latest remaining HP.
    /// Armor consumption is unchanged. The returned debit must be between zero and maximumDebit.</summary>
    public interface ICombatHpDebitPolicy
    {
        /// <remarks>NoAlloc. Called synchronously before the HP command, telemetry and damage event.</remarks>
        int LimitHpDebit(in HitResolveResult hit, GameplayEntityId target, int currentHp, int maximumDebit);
    }
}
