using System;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Shared read-only gate for projectile requests that exist in compiled runtime catalogs.</summary>
    internal static class CharacterActionProjectileRequestEligibility
    {
        public static bool IsCompiledReferenceable(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            return record != null
                && !string.IsNullOrWhiteSpace(record.RequestId)
                && record.HasSpawnProfile
                && record.HasGeneratedSpawnProfile
                && record.HasGeneratedDefinition;
        }
    }
}
