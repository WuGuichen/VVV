using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns root-scoped FairyGUI services for the Runtime AI Planner development panel.</summary>
    /// <remarks>LowFreqAlloc: state is rebuilt only when the bound FairyGUI root changes.</remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession
    {
        private readonly MxUiViewId _viewId;
        private MxFairyGuiTooltipService _tooltipService = new MxFairyGuiTooltipService();
        private MxFairyGuiFocusNavigationMetadata _focusNavigationMetadata;
        private Fgui.GComponent _root;
        private bool _tooltipsSuppressed;

        internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession(MxUiViewId viewId)
        {
            _viewId = viewId;
        }

        internal bool IsCurrent(Fgui.GComponent root) => ReferenceEquals(_root, root);

        internal void ResetForRoot(Fgui.GComponent root)
        {
            if (ReferenceEquals(_root, root))
                return;

            _tooltipService.Dispose();
            _tooltipService = new MxFairyGuiTooltipService();
            MxFairyGuiFocusNavigation.Clear(_root);
            _root = root;
        }

        internal void BindTooltip(
            Fgui.GObject target,
            string text,
            MxFairyGuiTooltipOptions options = default)
        {
            if (_tooltipsSuppressed)
            {
                _tooltipService.Unbind(target);
                _tooltipService.Hide();
                return;
            }
            _tooltipService.Bind(target, text, options);
        }

        internal void SetTooltipsSuppressed(bool suppressed)
        {
            _tooltipsSuppressed = suppressed;
            if (suppressed)
                _tooltipService.Hide();
        }

        internal void UnbindTooltip(Fgui.GObject target)
        {
            _tooltipService.Unbind(target);
        }

        internal void HideTooltip()
        {
            _tooltipService.Hide();
        }

        internal void ConfigureFocus(Fgui.GComponent root)
        {
            if (_focusNavigationMetadata == null)
                _focusNavigationMetadata = CreateFocusNavigationMetadata();

            MxFairyGuiFocusNavigation.Configure(root, _focusNavigationMetadata);
        }

        private MxFairyGuiFocusNavigationMetadata CreateFocusNavigationMetadata()
        {
            var names = new List<string>
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LatestFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopStart,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopEnd,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleLoop,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEditGate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close
            };
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0;
                    slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam;
                    slotIndex++)
                {
                    names.Add(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex));
                }
            }

            return new MxFairyGuiFocusNavigationMetadata(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                names);
        }
    }
}
