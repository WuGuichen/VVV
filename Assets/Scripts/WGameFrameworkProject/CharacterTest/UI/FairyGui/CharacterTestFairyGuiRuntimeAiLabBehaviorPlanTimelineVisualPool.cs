using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class GameplayPlanBarSlot
    {
        internal Fgui.GComponent Visual;
        internal Fgui.GGraph Background;
        internal Fgui.GGraph ProgressFill;
        internal Fgui.GGraph IndeterminateSweep;
        internal Fgui.GTextField Label;
        internal Fgui.GTextField StateBadge;
        internal Fgui.GTextField MetricTargetBadge;
        internal Fgui.GTextField RestartBadge;
        internal int SourceIndex = -1;
        internal string StableId = string.Empty;
        internal GameplayReadablePlanDetailKind DetailKind;
        internal Color32 Fill;
        internal Color32 Border;
        internal bool Selected;
        internal bool Current;
        internal bool Conflict;
        internal long ObservedActionInstanceId;
        internal long RestartActionInstanceId;
    }

    internal sealed class GameplayPlanExitSlot
    {
        internal Fgui.GComponent Visual;
        internal Fgui.GGraph Background;
        internal Fgui.GGraph ProgressFill;
        internal Fgui.GGraph IndeterminateSweep;
        internal Fgui.GGraph ToneAccent;
        internal Fgui.GTextField Label;
        internal Fgui.GTextField StateBadge;
        internal GameplayReadablePlanExitProjection Projection;
        internal Color32 Fill;
        internal Color32 Border;
        internal string RuntimeLabel = string.Empty;
        internal string TriggeredRuntimeLabel = string.Empty;
        internal string CommittedRuntimeLabel = string.Empty;
    }

    internal sealed class GameplayPlanBranchSlot
    {
        internal Fgui.GComponent Visual;
        internal Fgui.GGraph Diamond;
        internal Fgui.GTextField Badge;
    }

    internal sealed class GameplayPlanCandidateSlot
    {
        internal Fgui.GComponent Visual;
        internal Fgui.GGraph Background;
        internal Fgui.GTextField Title;
        internal Fgui.GTextField Score;
        internal Fgui.GTextField ConditionHeader;
        internal readonly Fgui.GTextField[] Conditions =
            new Fgui.GTextField[CharacterTestRuntimeAiLabBehaviorCandidateViewModel.MaxVisibleConditions];
        internal int EntryIndex = -1;
    }

    /// <summary>Owns all high-water FairyGUI visuals used by the read-only plan timeline.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineVisualPool
    {
        internal readonly List<GameplayPlanBarSlot> StageBars = new List<GameplayPlanBarSlot>(16);
        internal readonly List<GameplayPlanBarSlot> ActionBars = new List<GameplayPlanBarSlot>(16);
        internal readonly List<GameplayPlanExitSlot> ExitCards = new List<GameplayPlanExitSlot>(24);
        internal readonly List<GameplayPlanBranchSlot> BranchNodes =
            new List<GameplayPlanBranchSlot>(16);
        internal readonly List<Fgui.GGraph> ConnectorLines = new List<Fgui.GGraph>(96);
        internal readonly List<Fgui.GComponent> GlobalBands = new List<Fgui.GComponent>(8);
        internal readonly List<GameplayPlanCandidateSlot> CandidateCards =
            new List<GameplayPlanCandidateSlot>(16);

        internal void ResetForRoot()
        {
            StageBars.Clear();
            ActionBars.Clear();
            ExitCards.Clear();
            BranchNodes.Clear();
            ConnectorLines.Clear();
            GlobalBands.Clear();
            CandidateCards.Clear();
        }
    }
}
