using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabPublishResult
    {
        public CharacterTestRuntimeAiLabPublishResult(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            PublishedCard = publishedCard;
            Catalog = catalog;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCard PublishedCard { get; }
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics { get; }
        public bool Success => PublishedCard != null
            && Catalog != null
            && !CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(Diagnostics);
    }

    internal interface ICharacterTestRuntimeAiLabPublishSource
    {
        CharacterTestRuntimeAiLabPublishResult TryPublish(
            int characterId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            string cardDraftId);
    }
}
