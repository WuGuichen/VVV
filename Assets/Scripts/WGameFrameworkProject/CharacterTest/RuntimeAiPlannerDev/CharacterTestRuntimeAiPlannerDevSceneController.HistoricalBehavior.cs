namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly CharacterTestRuntimeAiLabHistoricalBehaviorProjection _historicalBehaviorProjection =
            new CharacterTestRuntimeAiLabHistoricalBehaviorProjection();
        private CharacterTestRuntimeAiLabPageContext _latestBehaviorCardPresentationContext;
        private CharacterTestRuntimeAiLabBehaviorCardViewport _historicalBehaviorCardViewport =
            CharacterTestRuntimeAiLabBehaviorCardViewport.Default;
        private CharacterTestRuntimeAiLabBehaviorCardLibraryState _historicalBehaviorCardLibraryState =
            CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;
        private bool _historicalBehaviorPresentationActive;

        internal bool IsHistoricalFrameSelected =>
            _timeline != null
            && _timeline.HasSelection
            && _timeline.SelectedFrame < _timeline.LatestFrame;

        internal bool HistoricalBehaviorPresentationActive =>
            _historicalBehaviorPresentationActive;

        internal CharacterTestRuntimeAiLabBehaviorCardViewport BehaviorCardPresentationViewport =>
            _historicalBehaviorPresentationActive
                ? _historicalBehaviorCardViewport
                : ShellCoordinator?.CurrentState?.PageContext?.BehaviorCardViewport
                    ?? CharacterTestRuntimeAiLabBehaviorCardViewport.Default;

        internal CharacterTestRuntimeAiLabBehaviorCardLibraryState BehaviorCardPresentationLibraryState =>
            _historicalBehaviorPresentationActive
                ? _historicalBehaviorCardLibraryState
                : ShellCoordinator?.CurrentState?.PageContext?.BehaviorCardLibraryState
                    ?? CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;

        internal void BeginHistoricalBehaviorPresentation()
        {
            if (_historicalBehaviorPresentationActive)
                return;
            CharacterTestRuntimeAiLabPageContext page =
                ShellCoordinator?.CurrentState?.PageContext;
            _latestBehaviorCardPresentationContext = page;
            _historicalBehaviorCardViewport = page?.BehaviorCardViewport
                ?? CharacterTestRuntimeAiLabBehaviorCardViewport.Default;
            _historicalBehaviorCardLibraryState = page?.BehaviorCardLibraryState
                ?? CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;
            _historicalBehaviorPresentationActive = true;
        }

        internal bool EndHistoricalBehaviorPresentation(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_historicalBehaviorPresentationActive)
                return true;
            CharacterTestRuntimeAiLabPageContext restore = _latestBehaviorCardPresentationContext;
            if (restore != null && ShellCoordinator != null)
            {
                CharacterTestRuntimeAiLabNavigationResult result =
                    ShellCoordinator.TryRestoreBehaviorCardPresentationContext(restore);
                diagnostic = result.Message;
                if (!result.Success)
                    return false;
            }
            ClearHistoricalBehaviorPresentation();
            return true;
        }

        private void ClearHistoricalBehaviorPresentation()
        {
            _historicalBehaviorPresentationActive = false;
            _latestBehaviorCardPresentationContext = null;
            _historicalBehaviorCardViewport = CharacterTestRuntimeAiLabBehaviorCardViewport.Default;
            _historicalBehaviorCardLibraryState =
                CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;
        }

        internal void UpdateHistoricalBehaviorCardViewport(
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport)
        {
            if (_historicalBehaviorPresentationActive)
                _historicalBehaviorCardViewport = viewport;
        }

        internal void UpdateHistoricalBehaviorCardLibraryState(
            CharacterTestRuntimeAiLabBehaviorCardLibraryState libraryState)
        {
            if (_historicalBehaviorPresentationActive)
                _historicalBehaviorCardLibraryState = libraryState;
        }

        /// <remarks>NoAlloc after the reusable projection has initialized its event buffer.</remarks>
        internal CharacterTestRuntimeAiLabHistoricalBehaviorProjection ResolveHistoricalBehaviorProjection(
            int selectedFrame,
            int latestFrame,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime)
        {
            if (selectedFrame >= latestFrame)
            {
                _historicalBehaviorProjection.Reset();
                return _historicalBehaviorProjection;
            }
            if (_timeline == null
                || !_timeline.TryGetFrame(selectedFrame, out _)
                || !_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot selected)
                || selected == null
                || selected.Frame != selectedFrame)
            {
                _historicalBehaviorProjection.Reset();
                _historicalBehaviorProjection.Status =
                    CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Unavailable;
                _historicalBehaviorProjection.DiagnosticCode = "HISTORY_FRAME_DOMAIN_INVALID";
                _historicalBehaviorProjection.Diagnostic = "历史 frame buffer 与 timeline record 不一致。";
                return _historicalBehaviorProjection;
            }
            return CharacterTestRuntimeAiLabHistoricalBehaviorProjector.ResolveInto(
                _historicalBehaviorProjection,
                _timelineEventIndex,
                _timelineSessionGeneration,
                _timeline.RetainedStartFrame,
                selectedFrame,
                latestFrame,
                runtime);
        }
    }
}
