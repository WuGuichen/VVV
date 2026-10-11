namespace MxFramework.Game.CharacterTest
{
    using System.Text;

    /// <summary>Read-only projection for the dedicated Replay validation workspace.</summary>
    /// <remarks>LowFreqAlloc: rebuilt only while the Replay workspace is active.</remarks>
    internal sealed class CharacterTestRuntimeAiLabReplayWorkspaceViewModel
    {
        internal bool HasCapture { get; set; }
        internal CharacterTestReplayValidationState State { get; set; }
        internal string StateLabel { get; set; } = string.Empty;
        internal string ResultCode { get; set; } = string.Empty;
        internal string RecordText { get; set; } = string.Empty;
        internal string IdentityText { get; set; } = string.Empty;
        internal string StatusText { get; set; } = string.Empty;
        internal string DetailText { get; set; } = string.Empty;
        internal float Progress01 { get; set; }
        internal bool CanFreeze { get; set; }
        internal bool CanValidate { get; set; }
        internal bool CanCancel { get; set; }
        internal bool CanRetry { get; set; }
        internal bool CanReturn { get; set; }
        internal bool CanLocateDivergence { get; set; }
        internal bool CanOpenFacts { get; set; }
        internal bool CanOpenBehaviorCard { get; set; }
        internal CharacterTestRuntimeAiLabTimelineProjection TimelineProjection { get; set; }
        internal string RecordMetadataText { get; set; } = string.Empty;
        internal string BootstrapIdentityText { get; set; } = string.Empty;
        internal string AuthorityIdentityText { get; set; } = string.Empty;
        internal string ResultEvidenceText { get; set; } = string.Empty;
        internal string SelectedVisualStableKey { get; set; } = string.Empty;
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
    {
        internal CharacterTestRuntimeAiLabReplayWorkspaceViewModel Replay { get; set; } =
            new CharacterTestRuntimeAiLabReplayWorkspaceViewModel();
    }

    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature
    {
        private static void AppendReplaySignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel root)
        {
            CharacterTestRuntimeAiLabReplayWorkspaceViewModel replay = root.Replay;
            if (replay == null)
            {
                builder.Append("replay:null|");
                return;
            }

            builder.Append((int)replay.State).Append('|')
                .Append(replay.HasCapture ? '1' : '0')
                .Append(replay.CanFreeze ? '1' : '0')
                .Append(replay.CanValidate ? '1' : '0')
                .Append(replay.CanCancel ? '1' : '0')
                .Append(replay.CanRetry ? '1' : '0')
                .Append(replay.CanReturn ? '1' : '0')
                .Append(replay.CanLocateDivergence ? '1' : '0')
                .Append(replay.CanOpenFacts ? '1' : '0')
                .Append(replay.CanOpenBehaviorCard ? '1' : '0').Append('|')
                .Append(replay.Progress01).Append('|');
            AppendToken(builder, replay.StateLabel);
            AppendToken(builder, replay.ResultCode);
            AppendToken(builder, replay.RecordText);
            AppendToken(builder, replay.IdentityText);
            AppendToken(builder, replay.StatusText);
            AppendToken(builder, replay.DetailText);
            AppendToken(builder, replay.RecordMetadataText);
            AppendToken(builder, replay.BootstrapIdentityText);
            AppendToken(builder, replay.AuthorityIdentityText);
            AppendToken(builder, replay.ResultEvidenceText);
            AppendToken(builder, replay.SelectedVisualStableKey);
        }
    }
}
