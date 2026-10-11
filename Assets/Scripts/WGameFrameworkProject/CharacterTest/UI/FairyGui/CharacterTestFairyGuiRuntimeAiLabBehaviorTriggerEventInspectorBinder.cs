using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Formal unified v6 Recipe/Timeline/Event Inspector binder.</summary>
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private static readonly string[] SignalReasons =
        {
            "ActionFailed", "ConfigurationInvalid", "AbilityUnavailable", "CooldownActive",
            "TargetUnavailable", "RequestRejected", "OrchestrationRejected"
        };

        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly IMxFairyGuiPopupService _popupService;
        private readonly Func<string, bool> _locateTimeline;
        private readonly Action _closeInspector;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private readonly Dictionary<Fgui.GObject, bool> _tacticalVisibility = new Dictionary<Fgui.GObject, bool>();
        private Fgui.GComponent _root;
        private BehaviorRecipeTriggerEventInspectorViewModel _model;
        private string _pendingDeleteEventId = string.Empty;
        private string _selectedConditionNodeId = string.Empty;
        private float _conditionScrollY;
        private string _layoutEventId = string.Empty;
        private string _layoutTimelineId = string.Empty;
        private BehaviorRecipeInspectorMode _layoutInspectorMode;
        private bool _selectionChanged;
        private Fgui.GComponent _conditionDropdown;
        private Fgui.GGraph _conditionDropdownBackground;
        private readonly Fgui.GButton[] _conditionDropdownButtons = new Fgui.GButton[16];
        private Fgui.GObject _conditionDropdownAnchor;
        private bool _conditionDropdownUsesPopupService;
        private Fgui.GComponent _characterActionPicker;
        private Fgui.GObject _characterActionPickerAnchor;
        private bool _characterActionPickerUsesPopupService;
        private int[] _filteredCharacterActionIndices = Array.Empty<int>();
        private int _filteredCharacterActionCount;
        private Action<CharacterActionId> _characterActionSelection;
        private RangeGesture _rangeGesture;
        private readonly Dictionary<string, RangeEditorDomain> _rangeDomains =
            new(StringComparer.Ordinal);

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxFairyGuiPopupService popupService,
            Func<string, bool> locateTimeline,
            Action closeInspector,
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _popupService = popupService;
            _locateTimeline = locateTimeline;
            _closeInspector = closeInspector;
            _bindTooltip = bindTooltip;
        }

        private void BindCharacterActionTooltip(Fgui.GObject target, string text)
        {
            if (target == null)
                return;
            if (_bindTooltip != null)
            {
                _bindTooltip(target, text ?? string.Empty);
                return;
            }
            target.tooltips = string.Empty;
        }

        internal bool HasFocusedInput => _root?.root?.focus is Fgui.GTextInput input
            && IsDescendantOf(input, _root);

        internal void Bind(
            Fgui.GComponent root,
            BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            if (!ReferenceEquals(_root, root))
            {
                _root = root;
                ConfigureTypography();
                ConfigureStaticHandlers();
            }
            if (_tacticalVisibility.Count > 0 && model?.TacticalEditor == null) { RestoreTacticalInputs(); ConfigureStaticHandlers(); }
            foreach (var entry in _tacticalVisibility) if (!entry.Key.isDisposed) entry.Key.visible = entry.Value;
            _tacticalVisibility.Clear();
            ReconcileTacticalOwner(model);
            _model = model;
            if (_root == null)
                return;
            if (RequiresPointMoveForm(model))
            {
                ClearEventSetInspector();
                BindTacticalForm(model);
                return;
            }
            if (model?.TacticalEditor != null && !model.TacticalEditor.Preview.IsConditionProjection
                && (model.TacticalToken.Target == CharacterTestTacticalEditTarget.SharedDefinition
                    || model.TacticalToken.Target == CharacterTestTacticalEditTarget.LegacySource))
            {
                BindEventSetInspector(model);
                return;
            }
            ClearEventSetInspector();
            if (model?.TacticalDescription != null)
            {
                BindTacticalForm(model);
                return;
            }
            if (model?.TacticalEditor != null && model.TacticalEditor.ConditionsOpen)
            {
                ConfigureStaticHandlers();
                _root.visible = true;
                _root.SetSize(_activationOverview ? _root.width : 520, model.TacticalEditor.InspectorHeight);
                BindConditionOwner(model);
                BindTacticalConditionLeases(_root);
                BindTacticalDataConditions();
                string conditionLabel = model.TacticalToken.Target == CharacterTestTacticalEditTarget.NodeEnd ? "持续局部结束条件"
                    : model.TacticalToken.Target == CharacterTestTacticalEditTarget.NodeCondition ? "入口／等待条件"
                    : model.TacticalConditionEvent == "$activation" ? "整卡启用条件" : "所选对象条件";
                SetText("headerTitle", (model.TacticalEditor.Preview.IsFrozen ? "历史 · " : "") + conditionLabel);
                BindButton("btnCloseInspector", () => model.TacticalEditor.ShowConditions(false));
                SetVisible("btnCloseInspector", true);
                Place("background", 0, 0, 520, UnityEngine.Mathf.Max(600, _root.height));
                Place("headerTitle", 20, 8, 340, 32);
                SetText("ruleWhenLabel", conditionLabel);
                _root.GetChild("ruleWhenLabel")?.SetSize(150, 28);
                return;
            }
            bool visible = model?.Available == true;
            _root.visible = visible;
            SetRecipeResponsibilitiesVisible(
                visible
                && model?.ConditionOwnerOnly == true
                && model.InspectorMode == BehaviorRecipeInspectorMode.Recipe);
            if (!visible)
            {
                CloseConditionDropdown();
                CloseFormalConditionPicker();
                CloseTargetStatePicker();
                CloseCharacterActionPicker();
                CloseCharacterActionTimelinePreview();
                return;
            }
            if (!string.Equals(_pendingDeleteEventId, model.EventId, StringComparison.Ordinal))
                _pendingDeleteEventId = string.Empty;
            _selectionChanged =
                !string.Equals(_layoutEventId, model.EventId, StringComparison.Ordinal) ||
                !string.Equals(_layoutTimelineId, model.TimelineId, StringComparison.Ordinal) ||
                _layoutInspectorMode != model.InspectorMode;
            if (_selectionChanged)
            {
                CloseConditionDropdown();
                CloseFormalConditionPicker();
                CloseTargetStatePicker();
                CloseCharacterActionPicker();
                CloseCharacterActionTimelinePreview();
                _selectedConditionNodeId = string.Empty;
                _layoutEventId = model.EventId;
                _layoutTimelineId = model.TimelineId;
                _layoutInspectorMode = model.InspectorMode;
            }
            RestoreEmbeddedCharacterActionWorkspaceIfOpen();
            if (model.UnifiedV6)
            {
                if (model.ConditionOwnerOnly)
                {
                    BindConditionOwner(model);
                    return;
                }
                BindUnifiedEvent(model);
                return;
            }

            SetVisible("operationSecondaryParameterLabel", false);
            SetVisible("inputOperationSecondaryParameter", false);

            SetText("headerTitle", "规则");
            SetText("runtimeState", "● " + model.RuntimeStateText);
            SetText("timelineText", model.TimelineTitle
                + (model.LegacyShared ? " · 整卡共享监听" : string.Empty));
            SetButtonTitle("btnCloseInspector", "X");
            SetInput("inputLocalFrame", model.LocalFrame.ToString(CultureInfo.InvariantCulture));
            SetInput("inputPriority", model.Priority.ToString(CultureInfo.InvariantCulture));

            bool predicate = model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate;
            bool frame = model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame;
            bool signal = model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal;
            bool multi = predicate && model.Conditions.Length > 1;
            SetVisible("btnGroupAll", multi);
            SetVisible("btnGroupAnyNot", multi);
            SetSelected("btnGroupAll", model.PredicateGroupMode
                == CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AllConditionsMatch);
            SetSelected("btnGroupAnyNot", model.PredicateGroupMode
                == CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AnyConditionDoesNotMatch);
            SetVisible("conditionList", predicate);
            SetVisible("btnAddCondition", predicate);
            SetVisible("btnAddConditionGroup", false);
            BindConditionList(predicate ? model.Conditions : Array.Empty<BehaviorRecipeTriggerConditionViewModel>());

            SetVisible("btnFrameAbsolute", frame);
            SetVisible("btnFrameActionEnd", frame);
            SetVisible("inputLocalFrame", frame
                && model.LocalFrameAnchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame);
            SetSelected("btnFrameAbsolute", model.LocalFrameAnchor
                == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame);
            SetSelected("btnFrameActionEnd", model.LocalFrameAnchor
                == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd);
            SetEnabled("btnFrameActionEnd", model.CanEdit && model.CanUsePrimaryActionEnd);

            SetVisible("btnSignal", signal);
            SetVisible("btnSignalReason", signal);
            SetButtonTitle("btnSignal", ResolveSignalText(model.ActionSignal));
            bool needsReason = signal && model.ActionSignal
                != CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed;
            SetVisible("btnSignalReason", needsReason);
            SetButtonTitle("btnSignalReason", "原因：" + (string.IsNullOrWhiteSpace(model.ActionSignalReasonCode)
                ? "请选择" : ResolveReasonText(model.ActionSignalReasonCode)));

            bool enterTimeline = model.OutcomeKind
                == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline;
            SetButtonTitle("btnOutcomeReturn", enterTimeline ? "进入时间轴" : "显式退出行为卡");
            SetVisible("btnTargetTimeline", enterTimeline);
            SetVisible("btnLocateTarget", enterTimeline);
            SetButtonTitle("btnTargetTimeline", ResolveTargetTimelineText(model));
            SetButtonTitle("btnLocateTarget", "定位");
            SetVisible("priorityLabel", true);
            SetVisible("btnPriorityMinus", true);
            SetVisible("inputPriority", true);
            SetVisible("btnPriorityPlus", true);
            SetVisible("priorityHint", true);

            string validationText = ResolveValidationText(model);
            bool hasValidation = !string.IsNullOrWhiteSpace(validationText);
            SetText("validationText", validationText);
            SetVisible("validationText", hasValidation);
            SetText("ruleWhenLabel", frame ? "在" : "当");
            SetText("ruleThenLabel", "则");
            ApplyCompactLayout(predicate, frame, signal, multi, enterTimeline, hasValidation);
            ConfigureDeleteAction(string.Equals(_pendingDeleteEventId, model.EventId, StringComparison.Ordinal));

            SetEnabled("btnGroupAll", model.CanEdit);
            SetEnabled("btnGroupAnyNot", model.CanEdit);
            SetEnabled("btnAddCondition", model.CanEdit);
            SetEnabled("btnAddConditionGroup", false);
            SetEnabled("btnFrameAbsolute", model.CanEdit);
            SetEnabled("inputLocalFrame", model.CanEdit);
            SetEnabled("btnSignal", model.CanEdit && model.CanUseActionSignal);
            SetEnabled("btnSignalReason", model.CanEdit && model.CanUseActionSignal);
            SetEnabled("btnOutcomeReturn", model.CanEdit);
            SetEnabled("btnTargetTimeline", model.CanEdit);
            SetEnabled("inputPriority", model.CanEdit);
            SetEnabled("btnPriorityMinus", model.CanEdit);
            SetEnabled("btnPriorityPlus", model.CanEdit);
            SetEnabled("btnCloseInspector", true);
            SetEnabled("btnDeleteEvent", model.CanEdit);
        }

        internal void Hide(Fgui.GComponent root)
        {
            if (root != null)
                root.visible = false;
        }

        internal void RefreshRuntimeState(
            Fgui.GComponent root,
            BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            if (root == null
                || !ReferenceEquals(root, _root)
                || root.visible != true
                || model?.Available != true
                || _model?.Available != true
                || model.InspectorMode != _model.InspectorMode
                || !string.Equals(model.TimelineId, _model.TimelineId, StringComparison.Ordinal)
                || !string.Equals(model.EventId, _model.EventId, StringComparison.Ordinal))
            {
                return;
            }

            // Runtime overlay updates intentionally avoid a full Inspector bind so text input,
            // popup and scroll state remain untouched. Keep the command model current and refresh
            // only the read-only runtime evidence that can change every RuntimeFrame.
            RefreshConditionObservations(model.Conditions);
            _model = model;
            SetText("runtimeState", "● " + model.RuntimeStateText);
        }

        private void BindUnifiedEvent(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            if (model.CharacterActionEvent)
            {
                BindCharacterActionEvent(model);
                return;
            }
            SetText("headerTitle", "事件");
            SetText("runtimeState", "● " + model.RuntimeStateText);
            string groupSuffix = !string.IsNullOrEmpty(model.EventGroupText)
                ? " · " + model.EventGroupText
                : string.Empty;
            SetText("timelineText", model.TimelineTitle
                + (model.LegacyShared ? " · 配方共享" : string.Empty)
                + groupSuffix);
            SetInput("inputTitle", model.DisplayTitle);
            SetInput("inputLocalFrame", model.StartFrame.ToString(CultureInfo.InvariantCulture));
            SetInput("inputDurationFrames", model.DurationFrames.ToString(CultureInfo.InvariantCulture));
            bool instant = model.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            bool waitControl = model.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition;
            bool loopControl = model.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion;
            SetInput("inputMaxExecutions", (loopControl ? model.MaxLoopRepeats : model.MaxExecutionsPerPass).ToString(CultureInfo.InvariantCulture));
            SetInput("inputCooldown", model.CooldownFrames.ToString(CultureInfo.InvariantCulture));
            SetInput("inputPriority", model.Priority.ToString(CultureInfo.InvariantCulture));

            bool hasConditions = model.Conditions.Length > 0;
            SetVisible("inputTitle", true);
            SetVisible("ruleWhenLabel", true);
            SetVisible("conditionList", hasConditions);
            SetVisible("btnAddCondition", true);
            SetVisible("btnAddConditionGroup", !model.CharacterActionEvent);
            SetVisible("btnGroupAll", false);
            SetVisible("btnGroupAnyNot", false);
            SetVisible("btnFrameAbsolute", false);
            SetVisible("btnFrameActionEnd", false);
            SetVisible("btnSignal", false);
            SetVisible("btnSignalReason", false);
            BindConditionList(model.Conditions);
            SetButtonTitle("btnAddCondition", hasConditions
                ? "+ 添加条件"
                : "+ 添加条件 (当前无附加条件)");
            SetButtonTitle("btnAddConditionGroup", "+ 逻辑组");

            SetVisible("ruleDivider", true);
            SetVisible("ruleThenLabel", true);
            SetVisible("btnOutcomeReturn", true);
            SetButtonTitle("btnOutcomeReturn", ResolveOperationText(model.OperationKind));
            bool targetTimeline = model.OperationKind
                == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline;
            SetVisible("btnTargetTimeline", targetTimeline);
            SetVisible("btnLocateTarget", targetTimeline);
            SetButtonTitle("btnTargetTimeline", ResolveTargetTimelineText(model));
            SetButtonTitle("btnLocateTarget", "定位");

            SetVisible("timeLabel", false);
            SetVisible("inputLocalFrame", false);
            SetVisible("btnDurationMode", false);
            SetVisible("durationFramesLabel", false);
            SetVisible("inputDurationFrames", false);
            SetVisible("repeatLabel", !instant);
            SetVisible("btnMaxExecutionsMinus", !instant);
            SetVisible("inputMaxExecutions", !instant);
            SetVisible("btnMaxExecutionsPlus", !instant);
            SetVisible("btnCooldownMinus", !instant && !loopControl);
            SetVisible("inputCooldown", !instant && !loopControl);
            SetVisible("btnCooldownPlus", !instant && !loopControl);
            SetVisible("executionLimitHint", !instant);
            SetVisible("cooldownHint", !instant && !loopControl);
            SetVisible("operationParameterLabel", false);
            SetVisible("inputOperationParameter", false);
            SetVisible("operationSecondaryParameterLabel", false);
            SetVisible("inputOperationSecondaryParameter", false);
            SetVisible("btnMoveSpeedMinus", false);
            SetVisible("btnMoveSpeedPlus", false);
            SetVisible("moveSpeedHint", false);
            SetVisible("btnCharacterActionParameter", false);
            SetVisible("btnViewCharacterActionTimeline", false);
            BindUnifiedOperationParameter(model);

            SetVisible("advancedDivider", true);
            SetVisible("priorityLabel", true);
            SetVisible("btnPriorityMinus", true);
            SetVisible("inputPriority", true);
            SetVisible("btnPriorityPlus", true);
            SetVisible("priorityHint", !instant && !loopControl);
            SetVisible("deleteHint", false);
            SetVisible("btnDeleteEvent", true);
            SetVisible("btnCloseInspector", true);
            SetText("ruleWhenLabel", "当");
            SetText("ruleThenLabel", "则");
            SetText("repeatLabel", loopControl ? "最多重复" : "最多执行");
            SetText("priorityLabel", "优先级");
            SetText("priorityHint", "冷却 (帧)");
            SetText("executionLimitHint", loopControl ? "0 = 无限循环" : "0 = 不限次数");
            SetText("cooldownHint", "两次执行的最小间隔");
            SetButtonTitle("btnMaxExecutionsMinus", "-");
            SetButtonTitle("btnMaxExecutionsPlus", "+");
            SetButtonTitle("btnCooldownMinus", "-");
            SetButtonTitle("btnCooldownPlus", "+");
            SetButtonTitle("btnPriorityMinus", "-10");
            SetButtonTitle("btnPriorityPlus", "+10");
            SetButtonTitle("btnCloseInspector", "✕");
            UpdateAccentColor(model);

            string validationText = ResolveValidationText(model);
            bool hasValidation = !string.IsNullOrWhiteSpace(validationText);
            SetText("validationText", hasValidation ? "⚠  " + validationText : string.Empty);
            SetVisible("validationText", hasValidation);
            ApplyUnifiedLayout(model, targetTimeline, hasValidation);
            ConfigureDeleteAction(string.Equals(_pendingDeleteEventId, model.EventId, StringComparison.Ordinal));

            string[] editable =
            {
                "inputTitle", "btnAddCondition", "btnAddConditionGroup", "btnOutcomeReturn", "btnTargetTimeline",
                "btnMaxExecutionsMinus", "inputMaxExecutions", "btnMaxExecutionsPlus",
                "btnCooldownMinus", "inputCooldown", "btnCooldownPlus", "inputOperationParameter",
                "inputOperationSecondaryParameter", "btnMoveSpeedMinus", "btnMoveSpeedPlus",
                "btnCharacterActionParameter", "btnViewCharacterActionTimeline",
                "inputPriority", "btnPriorityMinus", "btnPriorityPlus", "btnDeleteEvent"
            };
            for (int i = 0; i < editable.Length; i++)
                SetEnabled(editable[i], model.CanEdit);
            SetEnabled("btnLocateTarget", true);
            SetEnabled("btnCloseInspector", true);
        }

        private void BindCharacterActionEvent(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            SetText("headerTitle", "角色动作事件");
            SetText("runtimeState", "● " + model.RuntimeStateText);
            string groupSuffix = !string.IsNullOrEmpty(model.EventGroupText)
                ? " · " + model.EventGroupText
                : string.Empty;
            SetText("timelineText", model.TimelineTitle + groupSuffix);
            SetVisible("inputTitle", false);

            string[] hidden =
            {
                "ruleWhenLabel", "conditionList", "btnGroupAll", "btnGroupAnyNot",
                "btnFrameAbsolute", "btnFrameActionEnd", "inputLocalFrame", "btnSignal",
                "btnSignalReason", "ruleDivider", "ruleThenLabel", "btnOutcomeReturn",
                "btnTargetTimeline", "btnLocateTarget", "inputOperationParameter",
                "operationSecondaryParameterLabel", "inputOperationSecondaryParameter",
                "btnMoveSpeedMinus", "btnMoveSpeedPlus", "moveSpeedHint",
                "btnDurationMode", "inputDurationFrames", "repeatLabel",
                "btnMaxExecutionsMinus", "inputMaxExecutions", "btnMaxExecutionsPlus",
                "btnCooldownMinus", "inputCooldown", "btnCooldownPlus",
                "executionLimitHint", "cooldownHint", "priorityHint", "deleteHint"
            };
            for (int i = 0; i < hidden.Length; i++)
                SetVisible(hidden[i], false);

            SetVisible("operationParameterLabel", true);
            SetText("operationParameterLabel", "动作");
            SetVisible("btnCharacterActionParameter", true);
            SetVisible("btnViewCharacterActionTimeline", true);
            string actionText = !string.IsNullOrWhiteSpace(model.SelectedCharacterActionDisplayName)
                ? model.SelectedCharacterActionDisplayName + " / " + model.OperationCharacterActionId
                : !model.OperationCharacterActionId.IsValid
                    ? "选择角色动作…"
                    : model.OperationCharacterActionId + "（引用不存在）";
            SetButtonTitle("btnCharacterActionParameter", actionText);
            SetButtonTitle("btnViewCharacterActionTimeline", "查看动作时间轴");

            SetVisible("timeLabel", true);
            SetText("timeLabel", "开始：" + model.StartFrame.ToString(CultureInfo.InvariantCulture) + "f");
            SetVisible("durationFramesLabel", true);
            SetText("durationFramesLabel", "长度："
                + Math.Max(0, model.SelectedCharacterActionDurationFrames).ToString(CultureInfo.InvariantCulture)
                + " 帧（来自动作时间轴）");

            SetVisible("advancedDivider", true);
            SetVisible("priorityLabel", true);
            SetText("priorityLabel", "优先级");
            SetVisible("btnPriorityMinus", true);
            SetVisible("inputPriority", true);
            SetInput("inputPriority", model.Priority.ToString(CultureInfo.InvariantCulture));
            SetVisible("btnPriorityPlus", true);
            SetButtonTitle("btnPriorityMinus", "-10");
            SetButtonTitle("btnPriorityPlus", "+10");
            SetButtonTitle("btnCloseInspector", "✕");
            SetVisible("btnDeleteEvent", true);
            SetVisible("btnCloseInspector", true);
            UpdateAccentColor(model);

            bool repair = model.CharacterActionConditionRepairRequired;
            SetVisible("btnAddCondition", repair);
            SetVisible("btnAddConditionGroup", false);
            if (repair)
                SetButtonTitle("btnAddCondition", "移除条件并转为角色动作事件");
            bool hasValidation = !string.IsNullOrWhiteSpace(model.ValidationText);
            SetVisible("validationText", hasValidation);
            SetText("validationText", hasValidation ? "⚠  " + model.ValidationText : string.Empty);

            const float labelX = 30f;
            const float labelWidth = 86f;
            const float contentX = 116f;
            const float contentWidth = 370f;
            float y = 92f;
            Place("operationParameterLabel", labelX, y, 80f, 38f);
            Place("btnCharacterActionParameter", contentX, y, contentWidth, 38f);
            y += 52f;
            Place("btnViewCharacterActionTimeline", contentX, y, contentWidth, 36f);
            y += 44f;
            Place("timeLabel", labelX, y, 456f, 30f);
            y += 34f;
            Place("durationFramesLabel", labelX, y, 456f, 30f);
            y += 42f;
            Place("advancedDivider", labelX, y, 456f, 1f);
            y += 12f;
            Place("priorityLabel", labelX, y, labelWidth, 34f);
            PlaceStepperButton("btnPriorityMinus", 120f, y, "-10");
            Place("inputPriority", 162f, y, 68f, 34f);
            PlaceStepperButton("btnPriorityPlus", 234f, y, "+10");
            Place("deleteHint", 300f, y, 140f, 36f);
            Place("btnDeleteEvent", 448f, y, 36f, 36f);
            y += 44f;
            if (hasValidation)
            {
                Place("validationText", labelX, y, 456f, 36f);
                y += 42f;
            }
            if (repair)
            {
                Place("btnAddCondition", contentX, y, contentWidth, 36f);
                y += 44f;
            }
            Place("bodyBackground", 16f, 72f, 488f, Mathf.Clamp(y - 54f, 260f, 540f));

            SetEnabled("btnCharacterActionParameter", model.CanEdit);
            SetEnabled(
                "btnViewCharacterActionTimeline",
                model.CharacterActionReferenceValid && model.CharacterActionTimelinePreview?.Available == true);
            SetEnabled("btnPriorityMinus", model.CanEdit);
            SetEnabled("inputPriority", model.CanEdit);
            SetEnabled("btnPriorityPlus", model.CanEdit);
            SetEnabled("btnAddCondition", model.CanEdit && repair);
            SetEnabled("btnDeleteEvent", model.CanEdit);
            SetEnabled("btnCloseInspector", true);
            ConfigureDeleteAction(string.Equals(_pendingDeleteEventId, model.EventId, StringComparison.Ordinal));
            if (_characterActionTimelinePreview != null
                && (_popupService?.IsOpen(_characterActionTimelinePreview) == true
                    || _characterActionTimelinePreview.parent != null))
            {
                _actionTimelinePreviewModel = model.CharacterActionTimelinePreview;
                RefreshCharacterActionTimelinePreview();
            }
        }

        private void BindConditionOwner(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            bool timelineMode = model.InspectorMode == BehaviorRecipeInspectorMode.Timeline;
            bool recipeMode = model.InspectorMode == BehaviorRecipeInspectorMode.Recipe;
            SetText("headerTitle", timelineMode ? "时间轴" : "行为配方");
            SetText("runtimeState", "● " + model.RuntimeStateText);
            SetText("timelineText", timelineMode
                ? model.EffectiveTriggerSummary
                : model.TimelineTitle);
            SetInput("inputTitle", model.DisplayTitle);
            SetVisible("inputTitle", timelineMode || recipeMode);
            Fgui.GTextInput titleInput = _root.GetChild("inputTitle")?.asTextInput;
            if (titleInput != null)
                titleInput.promptText = recipeMode ? "行为卡名称" : "时间轴名称";
            BindRecipeResponsibilities(model, recipeMode);
            SetVisible("conditionList", true);
            SetVisible("btnAddCondition", true);
            SetVisible("btnAddConditionGroup", true);
            BindConditionList(model.Conditions);
            SetButtonTitle("btnAddCondition", "+ 添加条件");
            SetButtonTitle("btnAddConditionGroup", "+ 逻辑组");

            string[] hidden =
            {
                "btnGroupAll", "btnGroupAnyNot", "btnFrameAbsolute", "btnFrameActionEnd",
                "inputLocalFrame", "btnSignal", "btnSignalReason", "ruleDivider",
                "ruleThenLabel", "btnTargetTimeline", "btnLocateTarget",
                "operationParameterLabel", "inputOperationParameter", "advancedDivider",
                "operationSecondaryParameterLabel", "inputOperationSecondaryParameter",
                "btnCharacterActionParameter", "btnViewCharacterActionTimeline",
                "timeLabel", "btnDurationMode", "durationFramesLabel", "inputDurationFrames", "repeatLabel",
                "btnMaxExecutionsMinus", "inputMaxExecutions", "btnMaxExecutionsPlus",
                "btnCooldownMinus", "inputCooldown", "btnCooldownPlus",
                "btnMoveSpeedMinus", "btnMoveSpeedPlus", "moveSpeedHint",
                "executionLimitHint", "cooldownHint", "priorityLabel", "btnPriorityMinus",
                "inputPriority", "btnPriorityPlus", "priorityHint", "deleteHint", "btnDeleteEvent"
            };
            for (int i = 0; i < hidden.Length; i++)
                SetVisible(hidden[i], false);
            SetVisible("btnOutcomeReturn", timelineMode);
            bool hasValidation = !string.IsNullOrWhiteSpace(model.ValidationText);
            SetText("validationText", model.ValidationText);
            SetVisible("validationText", hasValidation);
            SetButtonTitle("btnOutcomeReturn", model.IsEntryTimeline
                ? "● 行为从此开始"
                : "设为行为入口");
            SetText("ruleWhenLabel", timelineMode ? "进入条件" : "启用条件");
            SetVisible("ruleWhenLabel", true);
            SetVisible("btnCloseInspector", false);
            UpdateAccentColor(model);
            SetEnabled("inputTitle", (timelineMode || recipeMode) && model.CanEditDisplayTitle);
            SetEnabled("btnAddCondition", model.CanEdit);
            SetEnabled("btnAddConditionGroup", model.CanEdit);
            SetEnabled("btnOutcomeReturn", timelineMode && model.CanEdit && !model.IsEntryTimeline);

            const float labelX = 30f;
            const float contentX = 72f;
            const float contentWidth = 414f;
            float y = 78f;
            if (timelineMode || recipeMode)
            {
                Place("inputTitle", contentX, y, contentWidth, 36f);
                y += 42f;
            }
            if (recipeMode)
            {
                Place("responsibilityLabel", labelX, y, 40f, 34f);
                for (int i = 0; i < 5; i++)
                    Place("responsibilityOption" + i, contentX + i * 83f, y, 78f, 34f);
                y += 42f;
            }
            if (timelineMode)
            {
                Place("btnOutcomeReturn", contentX, y, contentWidth, 38f);
                y += 50f;
            }
            Place("ruleWhenLabel", labelX, y, 100f, 28f);
            y += 30f;
            float rootHeight = Mathf.Max(600f, _root?.height ?? 600f);
            float listHeight = ResolveConditionOwnerListHeight(rootHeight, y, hasValidation);
            Place("conditionList", contentX, y, contentWidth, listHeight);
            y += listHeight + 8f;
            Place("btnAddCondition", contentX, y, 200f, 32f);
            Place("btnAddConditionGroup", contentX + 208f, y, 206f, 32f);
            y += 40f;
            if (hasValidation)
            {
                Place("validationText", labelX, y, 456f, 42f);
                y += 46f;
            }
            Place("bodyBackground", 16f, 64f, 488f, Mathf.Max(230f, rootHeight - 80f));
        }

        internal static float ResolveConditionOwnerListHeight(
            float rootHeight,
            float listY,
            bool hasValidation)
        {
            const float gapAfterList = 8f;
            const float addButtonsAndGap = 40f;
            const float bottomPadding = 18f;
            float validationHeight = hasValidation ? 46f : 0f;
            return Mathf.Max(
                162f,
                rootHeight - listY - gapAfterList - addButtonsAndGap
                    - validationHeight - bottomPadding);
        }

        private void ApplyUnifiedLayout(
            BehaviorRecipeTriggerEventInspectorViewModel model,
            bool targetTimeline,
            bool hasValidation)
        {
            const float labelX = 30f;
            const float labelWidth = 86f;
            const float contentX = 72f;
            const float contentWidth = 414f;
            float y = 78f;
            Place("inputTitle", contentX, y, contentWidth, 34f);
            y += 40f;
            Place("ruleWhenLabel", labelX, y, 34f, 36f);
            bool operationParameterVisible = _root.GetChild("inputOperationParameter")?.visible == true
                || _root.GetChild("btnCharacterActionParameter")?.visible == true;
            bool secondaryParameterVisible =
                _root.GetChild("inputOperationSecondaryParameter")?.visible == true;
            bool repeatVisible = _root.GetChild("repeatLabel")?.visible == true;
            bool cooldownVisible = repeatVisible && _root.GetChild("inputCooldown")?.visible == true;
            int optionalFooterRows = (targetTimeline ? 1 : 0)
                + (operationParameterVisible ? 1 : 0)
                + (secondaryParameterVisible ? 1 : 0)
                + (repeatVisible ? 1 : 0)
                + (cooldownVisible ? 1 : 0);
            float rootHeight = Mathf.Max(600f, _root?.height ?? 600f);
            if (model.Conditions.Length > 0)
            {
                float listHeight = ResolveUnifiedEventConditionListHeight(
                    rootHeight,
                    y,
                    optionalFooterRows,
                    hasValidation);
                Place("conditionList", contentX, y, contentWidth, listHeight);
                y += listHeight + 6f;
                Place("btnAddCondition", contentX, y, 200f, 30f);
                Place("btnAddConditionGroup", contentX + 208f, y, 206f, 30f);
            }
            else
            {
                Place("conditionList", contentX, y, contentWidth, 0f);
                Place("btnAddCondition", contentX, y, 200f, 36f);
                Place("btnAddConditionGroup", contentX + 208f, y, 206f, 36f);
            }
            y += 44f;
            Place("ruleDivider", labelX, y, 456f, 1f);
            y += 12f;
            Place("ruleThenLabel", labelX, y, 34f, 38f);
            Place("btnOutcomeReturn", contentX, y, contentWidth, 38f);
            y += 46f;
            if (targetTimeline)
            {
                Place("btnTargetTimeline", contentX, y, 324f, 34f);
                Place("btnLocateTarget", 402f, y, 84f, 34f);
                y += 42f;
            }
            if (_root.GetChild("inputOperationParameter")?.visible == true)
            {
                Place("operationParameterLabel", labelX, y, 80f, 34f);
                Place("inputOperationParameter", 116f, y, 370f, 34f);
                y += 42f;
            }
            else if (_root.GetChild("btnCharacterActionParameter")?.visible == true)
            {
                Place("operationParameterLabel", labelX, y, 80f, 34f);
                Place("btnCharacterActionParameter", 116f, y, 370f, 34f);
                y += 42f;
            }
            if (secondaryParameterVisible)
            {
                Place("operationSecondaryParameterLabel", labelX, y, labelWidth, 34f);
                PlaceStepperButton("btnMoveSpeedMinus", 120f, y, "-");
                Place("inputOperationSecondaryParameter", 162f, y, 68f, 34f);
                PlaceStepperButton("btnMoveSpeedPlus", 234f, y, "+");
                Place("moveSpeedHint", 282f, y, 200f, 34f);
                y += 42f;
            }
            Place("advancedDivider", labelX, y, 456f, 1f);
            y += 12f;
            if (repeatVisible)
            {
                Place("repeatLabel", labelX, y, labelWidth, 34f);
                PlaceStepperButton("btnMaxExecutionsMinus", 120f, y, "-");
                Place("inputMaxExecutions", 162f, y, 68f, 34f);
                PlaceStepperButton("btnMaxExecutionsPlus", 234f, y, "+");
                Place("executionLimitHint", 282f, y, 200f, 34f);
                y += 42f;
                if (cooldownVisible)
                {
                    Place("priorityHint", labelX, y, labelWidth, 34f);
                    PlaceStepperButton("btnCooldownMinus", 120f, y, "-");
                    Place("inputCooldown", 162f, y, 68f, 34f);
                    PlaceStepperButton("btnCooldownPlus", 234f, y, "+");
                    Place("cooldownHint", 282f, y, 200f, 34f);
                    y += 42f;
                }
            }
            Place("priorityLabel", labelX, y, labelWidth, 34f);
            PlaceStepperButton("btnPriorityMinus", 120f, y, "-10");
            Place("inputPriority", 162f, y, 68f, 34f);
            PlaceStepperButton("btnPriorityPlus", 234f, y, "+10");
            Place("deleteHint", 300f, y, 140f, 36f);
            Place("btnDeleteEvent", 448f, y, 36f, 36f);
            y += 44f;
            if (hasValidation)
            {
                Place("validationText", labelX, y, 456f, 36f);
                y += 42f;
            }
            Place("bodyBackground", 16f, 64f, 488f, Mathf.Max(320f, rootHeight - 80f));
        }

        internal static float ResolveUnifiedEventConditionListHeight(
            float rootHeight,
            float listY,
            int optionalFooterRows,
            bool hasValidation)
        {
            const float baseFooterHeight = 184f;
            const float optionalFooterRowHeight = 42f;
            const float validationHeight = 42f;
            float footerHeight = baseFooterHeight
                + Mathf.Max(0, optionalFooterRows) * optionalFooterRowHeight
                + (hasValidation ? validationHeight : 0f);
            return Mathf.Max(108f, rootHeight - listY - footerHeight);
        }

        private void ConfigureStaticHandlers()
        {
            if (_root == null)
                return;
            BindInput("inputLocalFrame", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame);
            BindInput("inputPriority", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority);
            BindInput("inputTitle", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title);
            BindInput("inputMaxExecutions", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerPass);
            BindInput("inputCooldown", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.CooldownFrames);
            BindOperationParameterInput();
            BindOperationSecondaryParameterInput();
            BindButton("btnGroupAll", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.PredicateGroupMode,
                intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AllConditionsMatch));
            BindButton("btnGroupAnyNot", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.PredicateGroupMode,
                intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AnyConditionDoesNotMatch));
            BindButton("btnAddCondition", () =>
            {
                if (_model?.CharacterActionEvent == true
                    && _model.CharacterActionConditionRepairRequired)
                {
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConvertCharacterActionEvent);
                    return;
                }
                if (_model?.UnifiedV6 == true)
                    ShowConditionSubjectPicker(_root.GetChild("btnAddCondition"));
                else
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddCondition);
            });
            BindButton("btnAddConditionGroup", () =>
                ShowConditionAddDropdown(_root.GetChild("btnAddConditionGroup")));
            Fgui.GObject addCondition = _root.GetChild("btnAddCondition");
            if (addCondition != null)
            {
                addCondition.onRightClick.Set(context =>
                {
                    context.StopPropagation();
                    if (_model?.UnifiedV6 == true && _model.CanEdit)
                        ShowConditionAddDropdown(addCondition);
                });
            }
            BindButton("btnFrameAbsolute", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrameAnchor,
                intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame));
            BindButton("btnFrameActionEnd", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrameAnchor,
                intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd));
            BindButton("btnSignal", () => ShowSignalDropdown(_root.GetChild("btnSignal")));
            BindButton("btnSignalReason", () => ShowSignalReasonDropdown(
                _root.GetChild("btnSignalReason")));
            BindButton("btnOutcomeReturn", () =>
            {
                if (_model?.InspectorMode == BehaviorRecipeInspectorMode.Timeline)
                {
                    if (!_model.IsEntryTimeline)
                    {
                        Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetEntryTimeline);
                    }
                    return;
                }
                ShowOutcomeDropdown(_root.GetChild("btnOutcomeReturn"));
            });
            BindButton("btnTargetTimeline", () => ShowTargetTimelineDropdown(
                _root.GetChild("btnTargetTimeline")));
            BindButton("btnCharacterActionParameter", () => ShowCharacterActionPicker(
                _root.GetChild("btnCharacterActionParameter"),
                _model?.OperationCharacterActionId ?? CharacterActionId.None,
                actionId => Submit(
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationCharacterActionId,
                    stringValue: actionId.Value.ToString(CultureInfo.InvariantCulture))));
            BindButton("btnViewCharacterActionTimeline", ShowCharacterActionTimelinePreview);
            BindButton("btnCloseInspector", () => _closeInspector?.Invoke());
            BindButton("btnPriorityMinus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority,
                intValue: unchecked((_model?.Priority ?? 0) - 10)));
            BindButton("btnPriorityPlus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority,
                intValue: unchecked((_model?.Priority ?? 0) + 10)));
            BindButton("btnMaxExecutionsMinus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerPass,
                intValue: Math.Max(0, ResolveExecutionLimit() - 1)));
            BindButton("btnMaxExecutionsPlus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerPass,
                intValue: ResolveExecutionLimit() == 0 ? 1 : ResolveExecutionLimit() + 1));
            BindButton("btnCooldownMinus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.CooldownFrames,
                intValue: Math.Max(0, (_model?.CooldownFrames ?? 0) - 1)));
            BindButton("btnCooldownPlus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.CooldownFrames,
                intValue: unchecked((_model?.CooldownFrames ?? 0) + 1)));
            BindButton("btnMoveSpeedMinus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier,
                floatValue: ResolveSteppedMoveSpeed(-0.1f)));
            BindButton("btnMoveSpeedPlus", () => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier,
                floatValue: ResolveSteppedMoveSpeed(0.1f)));
            BindButton("btnLocateTarget", () =>
            {
                if (!string.IsNullOrEmpty(_model?.TargetTimelineId))
                    _locateTimeline?.Invoke(_model.TargetTimelineId);
            });
            BindButton("btnDeleteEvent", RequestDelete);
        }

        private int ResolveExecutionLimit()
        {
            if (_model == null)
                return 1;
            return _model.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion
                ? _model.MaxLoopRepeats
                : _model.MaxExecutionsPerPass;
        }

        private float ResolveSteppedMoveSpeed(float delta)
        {
            float current = _model?.OperationMoveSpeedMultiplier
                ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
            float stepped = (float)Math.Round(current + delta, 1, MidpointRounding.AwayFromZero);
            return Mathf.Clamp(
                stepped,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MinimumMoveSpeedMultiplier,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MaximumMoveSpeedMultiplier);
        }

    }
}
