using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability
    {
        Available = 0,
        Unavailable = 1,
        Incompatible = 2
    }

    internal enum CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason
    {
        None = 0,
        SessionLocked = 1,
        NotUnlocked = 2,
        AlreadyInstalled = 3,
        SlotLimit = 4,
        CostLimit = 5,
        Unavailable = 6,
        Incompatible = 7,
        DeckUnresolved = 8
    }

    internal sealed class CharacterTestRuntimeAiPlannerPlayerDeckCardView
    {
        private readonly CharacterTestRuntimeAiLabBehaviorCardLogicPayload _logicPayload;

        public CharacterTestRuntimeAiPlannerPlayerDeckCardView(
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card,
            CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability,
            bool isUnlocked,
            bool isInstalled,
            bool canInstall,
            bool canRemove,
            bool canReplace,
            bool canUseAsReplacement,
            CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason blockReason,
            int pinnedRevision = 0)
        {
            StableCardId = stableCardId;
            Revision = card?.Revision ?? 0;
            DisplayName = card?.Metadata.DisplayName ?? string.Empty;
            IconKey = card?.Metadata.IconKey ?? string.Empty;
            Cost = card?.Metadata.Cost ?? 0;
            ShortDescription = card?.Metadata.ShortDescription ?? string.Empty;
            UseSummary = card?.Metadata.UseSummary ?? string.Empty;
            RiskSummary = card?.Metadata.RiskSummary ?? string.Empty;
            Tags = card?.Metadata.Tags ?? Array.AsReadOnly(Array.Empty<string>());
            RequiredCapabilities = card?.Metadata.RequiredCapabilities
                ?? Array.AsReadOnly(Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>());
            _logicPayload = card?.CloneLogic();
            Availability = availability;
            IsUnlocked = isUnlocked;
            IsInstalled = isInstalled;
            CanInstall = canInstall;
            CanRemove = canRemove;
            CanReplace = canReplace;
            CanUseAsReplacement = canUseAsReplacement;
            BlockReason = blockReason;
            PinnedRevision = pinnedRevision;
        }

        public string StableCardId { get; }
        public int Revision { get; }
        public string DisplayName { get; }
        public string IconKey { get; }
        public int Cost { get; }
        public string ShortDescription { get; }
        public string UseSummary { get; }
        public string RiskSummary { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> RequiredCapabilities { get; }
        public CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability Availability { get; }
        public bool IsUnlocked { get; }
        public bool IsInstalled { get; }
        public bool CanInstall { get; }
        public bool CanRemove { get; }
        public bool CanReplace { get; }
        public bool CanUseAsReplacement { get; }
        public CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason BlockReason { get; }
        public int PinnedRevision { get; }
        public bool HasNewerRevision => PinnedRevision > 0 && Revision > PinnedRevision;

        /// <remarks>LowFreqAlloc: only used when the explicit behavior-card library projection is rebuilt.</remarks>
        public CharacterTestRuntimeAiLabBehaviorCardLogicPayload CloneLogicPayload()
        {
            return _logicPayload?.Clone();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView
    {
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
            string characterStableId,
            int slotCount,
            int knownCost,
            bool isKnownCostComplete,
            long deckHash,
            bool isResolved,
            CharacterTestRuntimeAiPlannerPlayerDeckCardView[] cards,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] deckDiagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] lastCommandDiagnostics,
            bool isSessionLocked)
        {
            CharacterStableId = characterStableId;
            SlotCount = slotCount;
            MaximumSlots = CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots;
            KnownCost = knownCost;
            IsKnownCostComplete = isKnownCostComplete;
            MaximumCost = CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost;
            DeckHash = deckHash;
            IsResolved = isResolved;
            RequiresReplacement = !isResolved;
            IsSessionLocked = isSessionLocked;
            Cards = Array.AsReadOnly(cards ?? Array.Empty<CharacterTestRuntimeAiPlannerPlayerDeckCardView>());
            DeckDiagnostics = Array.AsReadOnly(
                deckDiagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            LastCommandDiagnostics = Array.AsReadOnly(
                lastCommandDiagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public string CharacterStableId { get; }
        public int SlotCount { get; }
        public int MaximumSlots { get; }
        public int TotalCost => KnownCost;
        public int KnownCost { get; }
        public bool IsKnownCostComplete { get; }
        public int MaximumCost { get; }
        public long DeckHash { get; }
        public bool IsResolved { get; }
        public bool RequiresReplacement { get; }
        public bool IsSessionLocked { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> Cards { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> DeckDiagnostics { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics => DeckDiagnostics;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> LastCommandDiagnostics { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult
    {
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
        {
            Workbench = workbench;
            View = workbench?.View;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbench Workbench { get; }
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView View { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success => Workbench != null;
    }

    internal sealed class CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult
    {
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics,
            bool success)
        {
            View = view;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            Success = success;
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView View { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success { get; }
    }
}
