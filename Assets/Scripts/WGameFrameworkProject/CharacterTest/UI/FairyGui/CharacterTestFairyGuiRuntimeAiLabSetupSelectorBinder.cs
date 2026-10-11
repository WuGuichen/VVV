using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns setup character and behavior-group selector popup state.</summary>
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly IMxFairyGuiPopupService _popupService;
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession _rootSession;
        private readonly Func<bool> _tooltipsSuppressed;
        private Fgui.GComponent _setupSelectorPopup;
        private Fgui.GComponent _setupCharacterPopupHost;
        private Fgui.GComponent _setupBehaviorGroupPopupHost;
        private bool _setupBehaviorGroupPickerOpen;
        private bool _setupCharacterPickerOpen;
        private string _setupBehaviorGroupPickerSearch = string.Empty;
        private int _setupBehaviorGroupPickerFilter;
        private int _setupBehaviorGroupPickerOffset;
        private int _setupBehaviorGroupPickerMaxOffset;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _setupBehaviorGroupPickerViewModel;
        private string _setupBehaviorGroupEditorSourceKey = string.Empty;
        private string _setupBehaviorGroupEditorDisplayName = string.Empty;
        private string _setupBehaviorGroupEditorDescription = string.Empty;
        private string _setupBehaviorGroupEditorTagsText = string.Empty;
        private string _setupBehaviorGroupEditorStatus = string.Empty;
        private bool _setupBehaviorGroupEditorBindingInput;
        private bool _setupBehaviorGroupEditorDirty;
        private bool _setupBehaviorGroupEditorForceInputRefresh;
        private readonly List<string> _recentSetupBehaviorGroupDraftIds = new List<string>(8);
        private readonly List<int> _recentSetupCharacterIds = new List<int>(8);

        private const int SetupBehaviorGroupPickerRowCapacity = 7;
        private const float SetupBehaviorGroupPickerPanelWidth = 570f;
        private const float SetupBehaviorGroupPickerExpandedWidth = 960f;
        private const float SetupBehaviorGroupEditorX = 570f;
        private const float SetupBehaviorGroupEditorWidth = 390f;
        private const float SetupBehaviorGroupPickerMinimumEditorHeight = 544f;
        private const float SetupBehaviorGroupPickerRowsY = 126f;
        private const float SetupBehaviorGroupPickerRowHeight = 58f;
        private const float SetupBehaviorGroupPickerRowStride = 66f;
        private const float SetupBehaviorGroupPickerPanelBottomPadding = 20f;
        private const float SetupBehaviorGroupGroupPickerActionBarHeight = 64f;
        private const float SetupBehaviorGroupPickerActionBarGap = 12f;
        private const float SetupBehaviorGroupPickerScrollTrackY = 126f;
        private const string SetupBehaviorGroupPickerBackground = "setupBehaviorGroupPickerBackground";
        private const string SetupBehaviorGroupPickerSearchIcon = "setupBehaviorGroupPickerSearchIcon";
        private const string SetupBehaviorGroupPickerCount = "setupBehaviorGroupPickerCount";
        private const string SetupBehaviorGroupPickerSearchBackground =
            "setupBehaviorGroupPickerSearchBackground";
        private const string SetupBehaviorGroupPickerSearchInput = "setupBehaviorGroupPickerSearchInput";
        private const string SetupBehaviorGroupPickerEmpty = "setupBehaviorGroupPickerEmpty";
        private const string SetupBehaviorGroupPickerScrollTrack =
            "setupBehaviorGroupPickerScrollTrack";
        private const string SetupBehaviorGroupPickerScrollThumb =
            "setupBehaviorGroupPickerScrollThumb";
        private const string SetupBehaviorGroupPickerActionBar = "setupBehaviorGroupPickerActionBar";
        private const string SetupBehaviorGroupEditorNameBackground =
            "setupBehaviorGroupEditorNameBackground";
        private const string SetupBehaviorGroupEditorDescriptionBackground =
            "setupBehaviorGroupEditorDescriptionBackground";
        private const string SetupBehaviorGroupEditorTagsBackground =
            "setupBehaviorGroupEditorTagsBackground";
        private const string WorkbenchEditButton = "btnWorkbenchEdit";
        private const string WorkbenchMoreButton = "btnWorkbenchMore";
        private const string WorkbenchListPanel = "timelinePanel";
        private const string WorkbenchCountBadge = "workbenchCountBadge";
        private const string RuntimeFrameSlotBadge = "runtimeFrameSlotBadge";
        private const string WorkbenchDetailSurface = "workbenchDetailSurface";
        private const string WorkbenchDetailIconBackground = "workbenchDetailIconBackground";
        private const string WorkbenchDetailIcon = "workbenchDetailIcon";
        private const string WorkbenchDiagnosticBackground = "workbenchDiagnosticBackground";
        private const string WorkbenchDiagnosticText = "workbenchDiagnosticText";
        internal const float TimelineModeHostX =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostX;
        internal const float TimelineModeHostPlanY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY;
        internal const float TimelineModeHostRuntimeY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY;
        internal const float TimelineModeHostWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DesignWidth;
        internal const float TimelineModeStripHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.HeaderHeight;
        internal const float BehaviorPlanModeStripHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.HeaderHeight;
        internal const float BehaviorPlanTimelineX = 0f;
        internal const float BehaviorPlanTimelineY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY;
        internal const float BehaviorPlanTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DesignWidth;
        internal const float BehaviorPlanTimelineHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchHeight;
        internal const float RuntimeTimelineX = 0f;
        internal const float RuntimeTimelineY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeTimelineY;
        internal const float RuntimeTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DesignWidth;
        internal const float RuntimeTimelineHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeTimelineHeight;
        private const float BehaviorGroupDrawerWidth = 650f;
        private const float BehaviorGroupDrawerHeight = 930f;
        private static readonly string[] BehaviorGroupDrawerChildNames =
        {
            WorkbenchListPanel,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
            WorkbenchCountBadge,
            WorkbenchDetailSurface,
            WorkbenchDetailIconBackground,
            WorkbenchDetailIcon,
            WorkbenchDiagnosticBackground,
            WorkbenchDiagnosticText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard,
            WorkbenchEditButton,
            WorkbenchMoreButton,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft
        };
        private static readonly string[] SetupBehaviorGroupEditorChildNames =
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorPanel,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTitle,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorIdentity,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorNameLabel,
            SetupBehaviorGroupEditorNameBackground,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescriptionLabel,
            SetupBehaviorGroupEditorDescriptionBackground,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTagsLabel,
            SetupBehaviorGroupEditorTagsBackground,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorStatus,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave
        };

        internal CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxFairyGuiPopupService popupService,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession rootSession,
            Func<bool> tooltipsSuppressed)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _popupService = popupService;
            _rootSession = rootSession;
            _tooltipsSuppressed = tooltipsSuppressed ?? (() => false);
        }

        private void BindSetupBehaviorGroupPicker(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            EnsureSetupSelectorPopup(root);
            _setupBehaviorGroupPickerViewModel = viewModel;
            bool setup = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup;
            if (!setup)
            {
                _setupBehaviorGroupPickerOpen = false;
                _setupCharacterPickerOpen = false;
            }

            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupBehaviorGroup,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup,
                setup && viewModel?.CanOpenBehaviorGroup == true);
            SetObjectVisible(root, "setupCharacterIcon", false);
            SetObjectVisible(root, "setupBehaviorIcon", false);

            Fgui.GButton characterSelector = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter)?.asButton;
            if (characterSelector != null)
            {
                characterSelector.text = viewModel?.SetupCharacterText ?? string.Empty;
                BindTooltip(characterSelector, string.Empty);
                characterSelector.onClick.Set(() =>
                {
                    bool samePickerOpen = _setupCharacterPickerOpen
                        && _popupService?.IsOpen(_setupCharacterPopupHost) == true;
                    if (samePickerOpen)
                    {
                        _popupService.Hide(
                            _setupCharacterPopupHost,
                            MxFairyGuiPopupCloseReason.Toggle);
                        return;
                    }

                    bool opening =
                        _popupService?.IsOpen(_setupCharacterPopupHost) != true;
                    _setupCharacterPickerOpen = true;
                    _setupBehaviorGroupPickerOpen = false;
                    if (opening)
                    {
                        DiscardSetupBehaviorGroupEditorBuffer();
                        _setupBehaviorGroupPickerSearch = string.Empty;
                        _setupBehaviorGroupPickerFilter = 0;
                        Fgui.GTextInput input = GetSetupPickerChild(
                            root,
                            SetupBehaviorGroupPickerSearchInput) as Fgui.GTextInput;
                        if (input != null)
                            input.text = string.Empty;
                    }
                    _setupBehaviorGroupPickerOffset = 0;
                    RenderSetupBehaviorGroupPicker(root);
                    if (opening)
                    {
                        bool shown = ShowSetupSelectorPopup(
                            characterSelector,
                            _setupCharacterPopupHost);
                        _setupCharacterPickerOpen = shown;
                        _setupBehaviorGroupPickerOpen = false;
                    }
                });
                CenterButtonContent(characterSelector, 10f);
            }

            Fgui.GButton groupSelector = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup)?.asButton;
            if (groupSelector != null)
            {
                groupSelector.text = viewModel?.SetupBehaviorGroupText ?? string.Empty;
                BindTooltip(groupSelector, "选择行为组");
                groupSelector.onClick.Set(() =>
                {
                    bool samePickerOpen = _setupBehaviorGroupPickerOpen
                        && _popupService?.IsOpen(_setupBehaviorGroupPopupHost) == true;
                    if (samePickerOpen)
                    {
                        _popupService.Hide(
                            _setupBehaviorGroupPopupHost,
                            MxFairyGuiPopupCloseReason.Toggle);
                        return;
                    }

                    bool opening =
                        _popupService?.IsOpen(_setupBehaviorGroupPopupHost) != true;
                    _setupBehaviorGroupPickerOpen = true;
                    _setupCharacterPickerOpen = false;
                    if (opening)
                    {
                        ResetSetupBehaviorGroupEditorBuffer(viewModel);
                        _setupBehaviorGroupPickerSearch = string.Empty;
                        _setupBehaviorGroupPickerFilter = 0;
                        Fgui.GTextInput input = GetSetupPickerChild(
                            root,
                            SetupBehaviorGroupPickerSearchInput) as Fgui.GTextInput;
                        if (input != null)
                            input.text = string.Empty;
                    }
                    _setupBehaviorGroupPickerOffset = 0;
                    RenderSetupBehaviorGroupPicker(root);
                    if (opening)
                    {
                        bool shown = ShowSetupSelectorPopup(
                            groupSelector,
                            _setupBehaviorGroupPopupHost);
                        _setupBehaviorGroupPickerOpen = shown;
                        _setupCharacterPickerOpen = false;
                    }
                });
                CenterButtonContent(groupSelector, 10f);
            }

            Fgui.GButton createButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate)?.asButton;
            if (createButton != null)
            {
                createButton.text = viewModel?.SetupBehaviorGroupPickerCreateLabel ?? "新建副本";
                createButton.grayed = viewModel?.CanCreateSetupBehaviorGroup != true;
                createButton.touchable = viewModel?.CanCreateSetupBehaviorGroup == true;
                BindTooltip(
                    createButton,
                    viewModel?.CanCreateSetupBehaviorGroup == true
                        ? string.Empty
                        : viewModel?.SetupBehaviorGroupPickerCreateDisabledReasonText);
                createButton.onClick.Set(() =>
                {
                    if (viewModel?.CanCreateSetupBehaviorGroup != true)
                        return;
                    DiscardSetupBehaviorGroupEditorBuffer();
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CreateSetupBehaviorGroup,
                        null));
                });
            }

            Fgui.GButton deleteButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete)?.asButton;
            if (deleteButton != null)
            {
                bool confirming = viewModel?.SetupBehaviorGroupDeleteConfirming == true;
                bool canDelete = viewModel?.CanDeleteSetupBehaviorGroup == true || confirming;
                deleteButton.text = confirming
                    ? viewModel.SetupBehaviorGroupPickerDeleteConfirmLabel
                    : viewModel?.SetupBehaviorGroupPickerDeleteLabel ?? "删除选中";
                deleteButton.grayed = !canDelete;
                deleteButton.touchable = canDelete;
                BindTooltip(
                    deleteButton,
                    canDelete
                        ? string.Empty
                        : viewModel?.SetupBehaviorGroupPickerDeleteDisabledReasonText);
                deleteButton.onClick.Set(() =>
                {
                    if (!canDelete)
                        return;
                    DiscardSetupBehaviorGroupEditorBuffer();
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteSetupBehaviorGroup,
                        null));
                });
            }

            BindSetupBehaviorGroupEditor(root);

            Fgui.GTextInput search = GetSetupPickerChild(root, SetupBehaviorGroupPickerSearchInput)
                as Fgui.GTextInput;
            if (search != null)
            {
                if (!Application.isPlaying || !search.focused)
                    search.text = _setupBehaviorGroupPickerSearch;
                search.promptText = string.Empty;
                search.onChanged.Set(() =>
                {
                    _setupBehaviorGroupPickerSearch = search.text ?? string.Empty;
                    _setupBehaviorGroupPickerOffset = 0;
                    RenderSetupBehaviorGroupPicker(root);
                });
            }

            BindSetupBehaviorGroupPickerFilter(root, 0, "全部");
            BindSetupBehaviorGroupPickerFilter(root, 1, "最近");
            BindSetupBehaviorGroupPickerFilter(root, 2, "当前");
            BindSetupBehaviorGroupPickerFilter(root, 3, "可用");
            Fgui.GObject pickerBackground = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerBackground);
            if (pickerBackground?.displayObject != null)
            {
                pickerBackground.displayObject.onMouseWheel.Set(context =>
                {
                    float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
                    if (Mathf.Approximately(delta, 0f))
                        return;
                    _setupBehaviorGroupPickerOffset += delta < 0f ? 1 : -1;
                    RenderSetupBehaviorGroupPicker(root);
                    context.StopPropagation();
                });
            }

            Fgui.GObject scrollTrack = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerScrollTrack);
            if (scrollTrack != null)
            {
                scrollTrack.onClick.Set(() =>
                {
                    if (_setupBehaviorGroupPickerMaxOffset <= 0)
                        return;
                    Fgui.GObject thumb = GetSetupPickerChild(
                        root,
                        SetupBehaviorGroupPickerScrollThumb);
                    Vector2 pointer = _setupSelectorPopup.GlobalToLocal(
                        Fgui.Stage.inst.touchPosition);
                    float thumbHeight = thumb != null ? thumb.height : 24f;
                    float travel = Mathf.Max(1f, scrollTrack.height - thumbHeight);
                    float normalized = Mathf.Clamp01(
                        (pointer.y - scrollTrack.y - thumbHeight * 0.5f) / travel);
                    _setupBehaviorGroupPickerOffset = Mathf.RoundToInt(
                        normalized * _setupBehaviorGroupPickerMaxOffset);
                    RenderSetupBehaviorGroupPicker(root);
                });
            }

            Fgui.GObject scrollThumb = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerScrollThumb);
            if (scrollThumb != null)
            {
                scrollThumb.draggable = true;
                scrollThumb.onDragMove.Set(() =>
                {
                    Fgui.GObject track = GetSetupPickerChild(
                        root,
                        SetupBehaviorGroupPickerScrollTrack);
                    if (track == null || _setupBehaviorGroupPickerMaxOffset <= 0)
                        return;
                    float travel = Mathf.Max(1f, track.height - scrollThumb.height);
                    scrollThumb.x = track.x;
                    scrollThumb.y = Mathf.Clamp(
                        scrollThumb.y,
                        track.y,
                        track.y + travel);
                    float normalized = (scrollThumb.y - track.y) / travel;
                    _setupBehaviorGroupPickerOffset = Mathf.RoundToInt(
                        normalized * _setupBehaviorGroupPickerMaxOffset);
                    RenderSetupBehaviorGroupPicker(root);
                });
            }

            RenderSetupBehaviorGroupPicker(root);
        }

        private void BindSetupBehaviorGroupPickerFilter(
            Fgui.GComponent root,
            int filter,
            string label)
        {
            string childName = "btnSetupBehaviorGroupPickerFilter" + filter;
            Fgui.GButton button = GetSetupPickerChild(root, childName)?.asButton;
            if (button == null)
                return;
            button.text = label;
            button.mode = Fgui.ButtonMode.Check;
            button.selected = _setupBehaviorGroupPickerFilter == filter;
            button.onClick.Set(() =>
            {
                _setupBehaviorGroupPickerFilter = filter;
                _setupBehaviorGroupPickerOffset = 0;
                RenderSetupBehaviorGroupPicker(root);
            });
            CenterButtonContent(button, 8f);
        }

        internal void EnsureSetupSelectorPopup(Fgui.GComponent root)
        {
            if (_setupSelectorPopup != null)
                return;

            _setupSelectorPopup = new Fgui.GComponent
            {
                name = "setupSelectorPopoverContent",
                opaque = true
            };
            _setupSelectorPopup.SetSize(SetupBehaviorGroupPickerPanelWidth, 600f);
            _setupCharacterPopupHost = CreateSetupSelectorPopupHost(
                "setupCharacterPopover");
            _setupBehaviorGroupPopupHost = CreateSetupSelectorPopupHost(
                "setupBehaviorGroupPopover");
            _setupCharacterPopupHost.AddChild(_setupSelectorPopup);

            string[] childNames =
            {
                SetupBehaviorGroupPickerBackground,
                SetupBehaviorGroupPickerSearchBackground,
                SetupBehaviorGroupPickerSearchIcon,
                SetupBehaviorGroupPickerCount,
                SetupBehaviorGroupPickerSearchInput,
                SetupBehaviorGroupPickerEmpty,
                SetupBehaviorGroupPickerScrollTrack,
                SetupBehaviorGroupPickerScrollThumb,
                SetupBehaviorGroupPickerActionBar,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorPanel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTitle,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorIdentity,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorNameLabel,
                SetupBehaviorGroupEditorNameBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescriptionLabel,
                SetupBehaviorGroupEditorDescriptionBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTagsLabel,
                SetupBehaviorGroupEditorTagsBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorStatus,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave,
                "btnSetupBehaviorGroupPickerFilter0",
                "btnSetupBehaviorGroupPickerFilter1",
                "btnSetupBehaviorGroupPickerFilter2",
                "btnSetupBehaviorGroupPickerFilter3"
            };
            for (int i = 0; i < childNames.Length; i++)
                MoveSetupPickerChild(root, childNames[i]);
            for (int i = 0; i < SetupBehaviorGroupPickerRowCapacity; i++)
            {
                MoveSetupPickerChild(root, SetupBehaviorGroupPickerRow(i));
                MoveSetupPickerChild(root, SetupBehaviorGroupPickerRowVisual(i));
            }

            Fgui.GObject obsoleteDismissArea = root.GetChild(
                "setupBehaviorGroupPickerDismissArea");
            if (obsoleteDismissArea != null)
            {
                obsoleteDismissArea.visible = false;
                obsoleteDismissArea.touchable = false;
            }
        }

        private static Fgui.GComponent CreateSetupSelectorPopupHost(string name)
        {
            var host = new Fgui.GComponent
            {
                name = name,
                opaque = false
            };
            host.SetSize(SetupBehaviorGroupPickerPanelWidth, 600f);
            return host;
        }

        private void MoveSetupPickerChild(Fgui.GComponent root, string childName)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child == null)
                return;

            float localX = child.x - 780f;
            float localY = child.y - 230f;
            child.RemoveFromParent();
            _setupSelectorPopup.AddChild(child);
            child.SetPosition(localX, localY, child.z);
        }

        private bool ShowSetupSelectorPopup(
            Fgui.GObject anchor,
            Fgui.GComponent popupHost)
        {
            if (_popupService == null
                || _setupSelectorPopup == null
                || popupHost == null
                || anchor == null)
            {
                return false;
            }

            if (!ReferenceEquals(_setupSelectorPopup.parent, popupHost))
            {
                _setupSelectorPopup.RemoveFromParent();
                popupHost.AddChild(_setupSelectorPopup);
                _setupSelectorPopup.SetPosition(0f, 0f, _setupSelectorPopup.z);
            }
            popupHost.SetSize(_setupSelectorPopup.width, _setupSelectorPopup.height);

            return _popupService.Show(
                _viewId,
                popupHost,
                anchor,
                new MxFairyGuiPopupOptions(
                    MxFairyGuiPopupPlacement.Left,
                    safeMargin: 16f,
                    anchorGap: 18f),
                _ =>
                {
                    _setupBehaviorGroupPickerOpen = false;
                    _setupCharacterPickerOpen = false;
                    DiscardSetupBehaviorGroupEditorBuffer();
                });
        }

        private void ResizeSetupSelectorPopup(float width, float height)
        {
            _setupSelectorPopup?.SetSize(width, height);
            _setupCharacterPopupHost?.SetSize(width, height);
            _setupBehaviorGroupPopupHost?.SetSize(
                width,
                height);
        }

        internal Fgui.GObject GetSetupPickerChild(
            Fgui.GComponent root,
            string childName)
        {
            return _setupSelectorPopup?.GetChild(childName) ?? root?.GetChild(childName);
        }

        private void SetSetupPickerObjectVisible(
            Fgui.GComponent root,
            string childName,
            bool visible)
        {
            Fgui.GObject child = GetSetupPickerChild(root, childName);
            if (child != null)
                child.visible = visible;
        }

        internal void SetSetupPickerText(
            Fgui.GComponent root,
            string childName,
            string text)
        {
            Fgui.GTextField field = GetSetupPickerChild(root, childName)?.asTextField;
            if (field != null)
                field.text = text ?? string.Empty;
        }

    }
}
