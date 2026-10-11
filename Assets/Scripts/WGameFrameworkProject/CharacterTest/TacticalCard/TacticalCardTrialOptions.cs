using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Explicit opt-in for one isolated Lab run. Never serialized into a player profile or default deck.</summary>
    internal sealed class TacticalCardTrialOptions
    {
        internal readonly ulong StageRunId;
        internal readonly int HandCapacity, Seed, InitialHandCount;
        internal readonly CharacterTestRuntimeAiPlannerProjectCatalogState PublishedCatalog;
        internal TacticalCardTrialOptions(ulong stageRunId, int handCapacity, int seed,
            CharacterTestRuntimeAiPlannerProjectCatalogState publishedCatalog)
        {
            if (stageRunId == 0) throw new ArgumentOutOfRangeException(nameof(stageRunId));
            if (handCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(handCapacity));
            StageRunId = stageRunId; HandCapacity = handCapacity; InitialHandCount = handCapacity; Seed = seed;
            PublishedCatalog = publishedCatalog ?? throw new ArgumentNullException(nameof(publishedCatalog));
        }
    }
}
