using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabFactsStatus
    {
        Neutral,
        Active,
        Success,
        Warning,
        Error
    }

    internal enum CharacterTestRuntimeAiLabFactsTableTab
    {
        Facts,
        Candidates
    }

    internal sealed class CharacterTestRuntimeAiLabFactsAgentViewModel
    {
        public int EntityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabFactsFactViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupText { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;
        public string PreviousValueText { get; set; } = string.Empty;
        public string SourceText { get; set; } = string.Empty;
        public string FreshnessText { get; set; } = string.Empty;
        public string AuthorityText { get; set; } = string.Empty;
        public string ChangeText { get; set; } = string.Empty;
        public string ParticipationText { get; set; } = string.Empty;
        public int LastChangedFrame { get; set; }
        public CharacterTestRuntimeAiLabDecisionTraceLayer ParticipatedLayers { get; set; }
        public bool Changed { get; set; }
        public bool Pinned { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
        public CharacterTestRuntimeAiLabFactsStatus Status { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabFactsCandidateViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string LayerText { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public string ScoreText { get; set; } = string.Empty;
        public string PriorityText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public string RelatedFactsText { get; set; } = string.Empty;
        public string[] RelatedFactStableIds { get; set; } = Array.Empty<string>();
        public int StableRank { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
        public CharacterTestRuntimeAiLabFactsStatus Status { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabFactsCausalStepViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public string StepNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PrimaryText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public CharacterTestRuntimeAiLabFactsStatus Status { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabFactsCommitPhaseViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public int EvidenceFrame { get; set; } = -1;
        public bool Selected { get; set; }
        public CharacterTestRuntimeAiLabFactsStatus Status { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabFactsWorkspaceViewModel
    {
        public bool Available { get; set; }
        public int Frame { get; set; }
        public int DecisionFrame { get; set; }
        public int LatestFrame { get; set; }
        public int RetainedStartFrame { get; set; }
        public int CommittedAgentEntityId { get; set; }
        public int PreferredAgentEntityId { get; set; }
        public long CommitGeneration { get; set; }
        public long AuthorityHash { get; set; }
        public long SessionGeneration { get; set; }
        public long AppliedRevisionHash { get; set; }
        public string RevisionText { get; set; } = string.Empty;
        public string HeaderText { get; set; } = "事实与决策";
        public string ContextText { get; set; } = string.Empty;
        public string HistoryStateText { get; set; } = string.Empty;
        public string DiagnosticCode { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public bool ChangedOnly { get; set; }
        public bool ParticipatedOnly { get; set; }
        public bool DeltaExpanded { get; set; }
        public int FactScrollOffset { get; set; }
        public int CandidateScrollOffset { get; set; }
        public int DeltaScrollOffset { get; set; }
        public CharacterTestRuntimeAiLabFactsTableTab ActiveTableTab { get; set; }
        public string SelectedStableId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabFactsAgentViewModel[] Agents { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsAgentViewModel>();
        public CharacterTestRuntimeAiLabFactsCausalStepViewModel[] CausalFlow { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsCausalStepViewModel>();
        public CharacterTestRuntimeAiLabFactsCommitPhaseViewModel[] CommitPhases { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsCommitPhaseViewModel>();
        public CharacterTestRuntimeAiLabFactsFactViewModel[] FactRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsFactViewModel>();
        public CharacterTestRuntimeAiLabFactsCandidateViewModel[] CandidateRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsCandidateViewModel>();
        public CharacterTestRuntimeAiLabFactsFactViewModel[] DeltaRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabFactsFactViewModel>();
        public bool CanLocateBehaviorNode { get; set; }
        public bool CanLocateRuntimeFrame { get; set; }
        public bool CanReturnSourceFrame { get; set; }
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
    {
        internal CharacterTestRuntimeAiLabFactsWorkspaceViewModel Facts { get; set; } =
            new CharacterTestRuntimeAiLabFactsWorkspaceViewModel();
    }
}
