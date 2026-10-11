// Offline-only boundaries for authored discovery and visual execution. Never compiled into Assets.
// Actual player, inventory, ledger, preparation, deployment and published card definitions are linked sources.
using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Resources;
namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterDefinition
    {
        public CharacterTestCharacterDefinition(int characterId, string displayName, ResourceKey prefabKey,
            int defaultTeamId, object animationBinding, object physicsProfile, string generatedPackageRoot = "",
            object gameplayCameraProfile = null, object attributes = null, CharacterTacticalDefaults tacticalDefaults = null,
            object presentationImages = null)
        { CharacterId = characterId; DisplayName = displayName; TacticalDefaults = tacticalDefaults; }
        public int CharacterId { get; }
        public string DisplayName { get; }
        public CharacterTacticalDefaults TacticalDefaults { get; }
    }
    internal static class CharacterTestPlayableCharacterCatalog
    { internal static IEnumerable<CharacterTestCharacterDefinition> Discover() => throw new NotSupportedException("Offline authored discovery is outside these fixtures."); }
    internal sealed class CharacterTestRuntimeAiPlannerProjectCatalogState { }
    internal enum CharacterTestPositionRecallOperation { None = 0, Record = 1, Return = 2 }
    internal sealed class CharacterTestTacticalPublishedSource
    { internal bool TryValidatePlayerExecution(out string issue) { issue = "Offline visual execution is unavailable."; throw new NotSupportedException(issue); } }
}
namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal static class CharacterTacticalServices
    {
        internal static CharacterTacticalPlayerService Player => throw new NotSupportedException("Use the explicit player fixture; no live composition root.");
        internal static string Issue => "Offline composition root unavailable.";
    }
    internal sealed partial class CharacterTacticalCardDefinition
    {
        private T NoPresentation<T>() where T : class
        { if (PresentationData != null) throw new NotSupportedException("Offline fixtures do not validate authored visual execution."); return null; }
        internal TacticalPointMoveRules PointMove => NoPresentation<TacticalPointMoveRules>();
        internal TacticalPositionRecallRules PositionRecall => NoPresentation<TacticalPositionRecallRules>();
        internal InteractionPreparationPayload InteractionPreparation => NoPresentation<InteractionPreparationPayload>();
        internal CharacterTestTacticalPublishedSource PresentationSource => NoPresentation<CharacterTestTacticalPublishedSource>();
    }
}
