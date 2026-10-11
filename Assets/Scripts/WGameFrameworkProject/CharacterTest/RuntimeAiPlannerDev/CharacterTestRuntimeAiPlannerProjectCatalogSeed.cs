namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerProjectCatalogSeedResult
    {
        public CharacterTestRuntimeAiPlannerProjectCatalogSeedResult(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cards,
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog groups,
            string diagnostic)
        {
            Cards = cards;
            Groups = groups;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Cards { get; }
        public CharacterTestRuntimeAiPlannerBehaviorGroupCatalog Groups { get; }
        public string Diagnostic { get; }
        public bool Success => Cards != null && Groups != null && Diagnostic.Length == 0;
    }

    /// <summary>
    /// Validates the versioned two-card project catalog used as the only foundation content.
    /// Obsolete module-derived cards are intentionally not regenerated.
    /// </summary>
    internal static class CharacterTestRuntimeAiPlannerProjectCatalogSeed
    {
        /// <remarks>LowFreqAlloc: explicit Editor validation/recovery entry only.</remarks>
        public static CharacterTestRuntimeAiPlannerProjectCatalogSeedResult Create()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (state?.Success != true)
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogSeedResult(
                    null,
                    null,
                    state?.Diagnostic
                        ?? "版本化基础行为卡目录不可用；不会重新生成已退役行为卡。");
            }

            return new CharacterTestRuntimeAiPlannerProjectCatalogSeedResult(
                state.CardCatalog,
                state.GroupCatalog,
                string.Empty);
        }
    }
}
