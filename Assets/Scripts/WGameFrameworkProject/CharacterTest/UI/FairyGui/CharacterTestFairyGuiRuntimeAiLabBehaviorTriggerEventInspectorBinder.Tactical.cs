using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        internal static bool RequiresPointMoveForm(BehaviorRecipeTriggerEventInspectorViewModel model)
            => model?.TacticalEditor?.Source?.ExecutionInput != null
                && model.TacticalToken.Target == CharacterTestTacticalEditTarget.LegacySource;

        private string _tacticalOwner, _tacticalTarget;
        private bool _tacticalConditionsMode;
        private void ReconcileTacticalOwner(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            string owner = model?.TacticalEditor?.CardId ?? string.Empty;
            if (model?.TacticalEditor != null)
            {
                var token = model.TacticalToken;
                owner += ":" + token.WorkspaceId + ":" + token.WorkspaceRevision + ":" + token.ContextSerial + ":" + token.NodeId
                    + ":" + token.DefinitionId + ":" + token.DefinitionRevision + ":" + token.Target + ":" + model.TacticalEditor.Preview.IsFrozen;
            }
            string target = model?.TacticalConditionEvent ?? model?.EventId ?? string.Empty;
            bool conditions = model?.TacticalEditor?.ConditionsOpen == true;
            if (owner != _tacticalOwner || target != _tacticalTarget || conditions != _tacticalConditionsMode)
            {
                CloseConditionDropdown(); CloseFormalConditionPicker(); CloseTargetStatePicker(); CloseCharacterActionPicker(); CloseActionLibraryPicker();
                _selectedConditionNodeId = string.Empty; _tacticalDeleteKey = null;
            }
            _tacticalOwner = owner; _tacticalTarget = target; _tacticalConditionsMode = conditions;
        }

        private sealed class TacticalFieldLease
        {
            internal CharacterTestTacticalEditToken Token;
            internal string Text;
            internal Func<CharacterTestTacticalEditToken, string, bool> Commit;
        }
        private readonly Dictionary<Fgui.GTextInput, TacticalFieldLease> _tacticalFieldLeases = new Dictionary<Fgui.GTextInput, TacticalFieldLease>();
        private readonly Dictionary<Fgui.GButton, string> _tacticalButtonIcons = new Dictionary<Fgui.GButton, string>();
        private readonly HashSet<Fgui.GTextInput> _tacticalInputs = new HashSet<Fgui.GTextInput>();
        private void RestoreTacticalInputs()
        {
            foreach (var input in _tacticalInputs) if (!input.isDisposed) { input.onFocusIn.Clear(); input.onFocusOut.Clear(); input.onKeyDown.Clear(); input.onChanged.Clear(); input.onSubmit.Clear(); }
            _tacticalFieldLeases.Clear(); _tacticalInputs.Clear();
            foreach (var item in _tacticalButtonIcons) if (!item.Key.isDisposed) item.Key.icon = item.Value;
            _tacticalButtonIcons.Clear();
        }
        private string _tacticalDeleteKey;
        private CharacterTestTacticalEditToken _tacticalDeleteToken;

        private sealed class EventSetRow
        {
            internal string Key, Timeline, Event;
            internal Fgui.GComponent Root;
            internal CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder Binder;
            internal Fgui.GComponent TimingRoot;
            internal CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder TimingBinder;
            internal CharacterTestTacticalEditingSession Editor;
            internal bool Collapsed;
            internal bool Advanced;
            internal Fgui.GComponent AdvancedRoot;
            internal CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder AdvancedBinder;
            internal float ExpandedHeight;
        }
        private readonly List<EventSetRow> _eventSetRows = new List<EventSetRow>();
        private readonly Dictionary<string, bool> _eventSetCollapsed = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> _eventSetScroll = new Dictionary<string, float>();
        private Fgui.GList _eventSetList;
        private string _eventSetOwner, _eventSetVersion, _eventSetSelection;
        private float _eventSetWidth;
        private bool _eventSetAutoResize;
        private Fgui.ListSelectionMode _eventSetSelectionMode;

        private void ClearEventSetInspector()
        {
            if (_eventSetList != null && !_eventSetList.isDisposed && _eventSetOwner != null)
                _eventSetScroll[_eventSetOwner] = _eventSetList.scrollPane?.posY ?? 0;
            foreach (var row in _eventSetRows)
            {
                row.Binder.CloseConditionDropdown(); row.Binder.CloseFormalConditionPicker();
                row.Binder.CloseTargetStatePicker(); row.Binder.CloseCharacterActionPicker();
                row.Binder.CloseActionLibraryPicker();
                row.Binder.RestoreTacticalInputs();
                row.Binder.CloseCharacterActionTimelinePreview();
                row.TimingBinder?.CloseConditionDropdown();
                row.TimingBinder?.RestoreTacticalInputs();
                row.AdvancedBinder?.RestoreTacticalInputs();
                if (!row.Root.isDisposed) row.Root.Dispose();
            }
            if (_eventSetList != null && !_eventSetList.isDisposed)
            {
                _eventSetList.autoResizeItem = _eventSetAutoResize;
                _eventSetList.selectionMode = _eventSetSelectionMode;
            }
            _eventSetRows.Clear(); _eventSetList = null;
            _eventSetOwner = _eventSetVersion = _eventSetSelection = null;
        }

        private void BindEventSetInspector(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            var editor = model.TacticalEditor;
            var token = editor.Token;
            string owner = token.WorkspaceId + ":" + editor.CardId + ":" + token.NodeId + ":" + token.DefinitionId + ":" + editor.Preview.FocusBlockId;
            string version = token.WorkspaceRevision + ":" + token.ContextSerial + ":" + token.DefinitionRevision
                + ":" + editor.CanEdit + ":" + editor.Preview.SourceFingerprint + ":" + editor.Preview.SimulationEnd
                + ":" + editor.Controller?.TacticalActionBindingRevision + ":" + editor.Controller?.TacticalPreviewBindingContextKey
                + ":" + editor.Preview.Compilation?.DependencyFingerprint
                + ":" + model.EventId + ":" + model.InspectorMode;
            bool changedOwner = _eventSetOwner != owner || _eventSetList?.parent != _root;
            if (changedOwner)
            {
                ClearEventSetInspector();
                _eventSetOwner = owner;
                _eventSetList = _root.GetChild("conditionList")?.asList;
                if (_eventSetList == null) return;
                _eventSetAutoResize = _eventSetList.autoResizeItem;
                _eventSetSelectionMode = _eventSetList.selectionMode;
                _eventSetList.itemRenderer = null;
                _eventSetList.RemoveChildrenToPool();
                _eventSetList.layout = Fgui.ListLayoutType.SingleColumn;
                _eventSetList.selectionMode = Fgui.ListSelectionMode.None;
                _eventSetList.autoResizeItem = false;
                _eventSetList.lineGap = 6;
            }
            _root.visible = true;
            foreach (var child in _root.GetChildren())
            { _tacticalVisibility[child] = child.visible; child.visible = false; }
            float width = Math.Max(280, _root.width), content = width - 24;
            _root.SetSize(width, editor.InspectorHeight);
            SetVisible("background", true); Place("background", 0, 0, width, _root.height);
            bool compact = editor.Preview.FocusBlockId != null;
            var selectedEvent = compact ? CompactSelectedEvent(model) : null;
            float eventListTop;
            if (compact) eventListTop = BindCompactFocusHeader(editor, selectedEvent, width);
            else
            {
                TacticalInput("inputTitle", editor.Source.Name, "封装名称", 12, 12, token.Target == CharacterTestTacticalEditTarget.SharedDefinition ? content - 124 : content, editor, editor.CanEdit,
                    (lease, text) => editor.Commit(lease, "修改封装名称", s => s.Name = text));
                float actionWidth = (content - 8) / 2;
                TacticalButton("btnOutcomeReturn", "+ 添加事件", 20 + actionWidth, 54, actionWidth, editor.CanEdit, () => {
                    var lease = editor.Token;
                    string timeline = editor.Source.EntryTimelineId;
                    ShowTacticalOperationPicker(editor, timeline, null, _root.GetChild("btnOutcomeReturn"), lease);
                });
                eventListTop = BindEventFocusHeader(editor, content);
            }
            _eventSetList.visible = !compact || selectedEvent != null;
            _eventSetList.SetXY(12, eventListTop); _eventSetList.SetSize(content, Math.Max(80, _root.height - eventListTop - 12));
            bool rebind = changedOwner || _eventSetVersion != version || _eventSetWidth != content;
            if (rebind)
            {
                float scroll = changedOwner ? (_eventSetScroll.TryGetValue(owner, out var saved) ? saved : 0)
                    : _eventSetList.scrollPane?.posY ?? 0;
                var desired = new HashSet<string>();
                int order = 0;
                foreach (var item in VisibleEventItems(editor))
                {
                    var timeline = item.timeline;
                    var ev = item.ev;
                    if (!compact && model.InspectorMode == BehaviorRecipeInspectorMode.Event
                        && editor.Preview.FindBlock(model.EventId) != null && ev.EventId != model.EventId) continue;
                    if (compact)
                    {
                        if (ev.EventId != selectedEvent?.EventId) continue;
                    }
                    string key = timeline.TimelineId + ":" + ev.EventId;
                    desired.Add(key);
                    var row = _eventSetRows.FirstOrDefault(r => r.Key == key);
                    if (row == null)
                    {
                        var component = Fgui.UIPackage.CreateObjectFromURL(_root.resourceURL)?.asCom;
                        if (component == null) continue;
                        row = new EventSetRow { Key = key, Timeline = timeline.TimelineId, Event = ev.EventId, Root = component,
                            Editor = new CharacterTestTacticalEditingSession(editor.Controller, editor.CardId),
                            Binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(_commandSink, _viewId,
                                _popupService, _locateTimeline, null, _bindTooltip),
                            Collapsed = _eventSetCollapsed.TryGetValue(owner + ":" + key, out var folded) && folded };
                        component.name = "eventSet:" + key;
                        _eventSetList.AddChild(component); _eventSetRows.Add(row);
                    }
                    _eventSetList.SetChildIndex(row.Root, order++);
                    row.Editor.BindEmbeddedEvent(editor, row.Timeline, row.Event);
                    row.Editor.Locate = editor.Locate;
                    BindEventSetRow(row, ev, content, model.CharacterActionOptions);
                }
                for (int i = _eventSetRows.Count - 1; i >= 0; i--)
                {
                    var row = _eventSetRows[i]; if (desired.Contains(row.Key)) continue;
                    row.Binder.CloseConditionDropdown(); row.Binder.CloseFormalConditionPicker();
                    row.Binder.CloseTargetStatePicker(); row.Binder.CloseCharacterActionPicker();
                    row.Binder.CloseActionLibraryPicker();
                    row.Binder.CloseCharacterActionTimelinePreview();
                    row.Binder.RestoreTacticalInputs();
                    row.TimingBinder?.CloseConditionDropdown(); row.TimingBinder?.RestoreTacticalInputs();
                    row.AdvancedBinder?.RestoreTacticalInputs();
                    if (!row.Root.isDisposed) row.Root.Dispose(); _eventSetRows.RemoveAt(i);
                }
                _eventSetVersion = version; _eventSetWidth = content;
                _eventSetList.EnsureBoundsCorrect();
                _eventSetList.scrollPane?.SetPosY(scroll, false);
            }
            string selection = model.TimelineId + ":" + model.EventId;
            if (_eventSetSelection != selection)
            {
                _eventSetSelection = selection;
                foreach (var row in _eventSetRows)
                {
                    bool selected = row.Key == selection;
                    if (!compact && row.Root.GetChild("btnGroupAll") is Fgui.GButton heading)
                        heading.titleColor = selected ? new UnityEngine.Color(.3f, .85f, .73f) : UnityEngine.Color.white;
                    if (!selected) continue;
                    if (row.Collapsed) { row.Collapsed = false; _eventSetCollapsed[owner + ":" + row.Key] = false; BindEventSetRow(row, editor.Preview.FindEvent(row.Timeline, row.Event), content, model.CharacterActionOptions); }
                    _eventSetList.EnsureBoundsCorrect();
                    // An event section can be taller than the viewport. Reveal its heading,
                    // rather than asking ScrollToView to fit the whole section and landing at its end.
                    _eventSetList.scrollPane?.SetPosY(row.Root.y, false);
                }
            }
        }

        private void BindEventSetRow(EventSetRow row, CharacterTestTacticalAuthoringEvent ev, float width,
            BehaviorRecipeCharacterActionOptionViewModel[] actions)
        {
            if (ev == null) return;
            if (row.Editor.Preview.FindBlock(row.Event) is CharacterTestTacticalEventBlock block)
            { BindEventBlockRow(row, block, width); return; }
            var b = row.Binder; var editor = row.Editor;
            var model = BehaviorRecipeTriggerEventInspectorProjection.BuildEmbeddedEventConditions(editor, actions);
            b._root = row.Root; b.ReconcileTacticalOwner(model); b._model = model;
            b.ConfigureStaticHandlers();
            foreach (var child in row.Root.GetChildren()) child.visible = false;
            row.Root.SetSize(width, 40);
            b.SetVisible("background", true);
            bool compact = editor.Preview.FocusBlockId != null;
            string type = ev.Operation.Kind switch { "CharacterAction" => "动作", "Movement" => "移动", "ApproachOptimalRange" => "接近",
                "TurnToTarget" => "转向", "WaitForCondition" => "等待", "RetreatFromTarget" => "后撤", "ReturnToPlanner" => "退出", _ => ev.Operation.Kind };
            string title = editor.Focused ? CharacterTestTacticalAuthoringPreview.LowLevelTitle(ev) : string.IsNullOrEmpty(ev.Title) ? type : ev.Title;
            if (!compact) b.TacticalButton("btnGroupAll", (row.Collapsed ? "▸ " : "▾ ") + title + " · " + type, 4, 4, width - 8, true, () => {
                row.Collapsed = !row.Collapsed; _eventSetCollapsed[_eventSetOwner + ":" + row.Key] = row.Collapsed;
                float scroll = _eventSetList.scrollPane?.posY ?? 0;
                BindEventSetRow(row, row.Editor.Preview.FindEvent(row.Timeline, row.Event), width, actions);
                _eventSetList.EnsureBoundsCorrect(); _eventSetList.scrollPane?.SetPosY(scroll, false);
            });
            if (!compact && row.Collapsed) { b.Place("background", 0, 0, width, 40); return; }
            float y = compact ? 0 : 46;
            if (!compact)
            {
                b.TacticalInput("inputTitle", CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(ev), "事件名称", 8, y, width - 16, editor, editor.CanEdit,
                    (lease, text) => editor.Commit(lease, "修改事件名称", s => CharacterTestTacticalAuthoringEdits.Event(s, row.Timeline, row.Event).Title = text));
                y += 40;
            }
            b.TacticalButton("btnOutcomeReturn", compact ? CompactOperationName(ev.Operation.Kind) + " ▾" : editor.Focused ? "替换事件类型 · " + type : "编辑编译结果", 8, y, width - 16,
                compact || editor.Focused ? editor.CanEdit : editor.Composition != null, () => {
                    if (compact || editor.Focused) b.ShowTacticalOperationPicker(editor, row.Timeline, row.Event, row.Root.GetChild("btnOutcomeReturn"), editor.Token);
                    else editor.OpenEventFocus(row.Timeline, row.Event);
                });
            y += 40;
            y = BindEventSetTiming(row, ev, model, y, width);
            bool action = ev.Operation.Kind == "CharacterAction", approach = ev.Operation.Kind == "ApproachOptimalRange";
            if (action)
            {
                bool functional = editor.Source.Slots.FirstOrDefault(s => s.SlotId == ev.Operation.SlotId)?.IsFunctional == true;
                b.TacticalButton("btnCharacterActionParameter", "动作：" + b.ActionBindingName(editor, ev) + (functional ? string.Empty : " ▾"),
                    8, y, width - 16, !functional && !editor.Preview.IsFrozen && (editor.Composition?.CanEdit ?? editor.CanEdit),
                    () => b.ShowActionBindingPicker(editor, ev, row.Root.GetChild("btnCharacterActionParameter")));
                row.Root.GetChild("btnCharacterActionParameter").tooltips = "选择此事件使用的动作；当前角色下次出牌／测试生效。";
                y += 40;
            }
            if (approach)
            {
                b.TacticalButton("btnCharacterActionParameter", "关联动作事件",
                    8, y, width - 16, editor.CanEdit, () => {
                        var lease = editor.Token;
                        var events = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, row.Timeline).Events.Where(e => e.Operation.Kind == "CharacterAction").ToArray();
                        string[] values = events.Select(e => e.EventId).ToArray();
                        b.TacticalChoices(row.Root.GetChild("btnCharacterActionParameter"), values, value => editor.Commit(lease, "修改动作来源", source => {
                            var target = CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event);
                            target.Operation.SourceActionEventId = value;
                        }), display: value => CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(events.First(e => e.EventId == value)));
                    });
                y += 40;
            }
            if (action)
            {
                var option = BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(actions,
                    new MxFramework.CharacterAction.CharacterActionId(editor.Preview.Compilation?.EventActionId(row.Event) ?? 0));
                model.CharacterActionTimelinePreview = option?.TimelinePreview;
                model.OperationCharacterActionId = option?.ActionId ?? default;
                model.CharacterActionReferenceValid = option?.Valid == true;
                model.CharacterActionEvent = true;
                if (!compact || option?.TimelinePreview?.Available == true)
                {
                    b.TacticalButton("btnViewCharacterActionTimeline", (option?.DisplayName ?? "动作未解析") + (compact ? " ↗" : " · 打开动作制作"),
                        8, y, width - 16, option?.TimelinePreview?.Available == true, b.ShowCharacterActionTimelinePreview);
                    y += 40;
                }
            }
            if (ev.Operation.Kind == "Movement") y = b.BindTacticalMovement(editor, row.Timeline, row.Event, ev.Operation.Movement, y, width);
            if (ev.Operation.Kind == "LoopRegion")
            {
                b.SetVisible("operationParameterLabel", true); b.SetText("operationParameterLabel", "循环次数"); b.Place("operationParameterLabel", 8, y, 90, 32);
                b.TacticalInput("inputOperationParameter", ev.Operation.MaxLoopRepeats.ToString(), "0 为不限次数", 102, y, width - 110, editor, editor.CanEdit,
                    (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改循环次数", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation.MaxLoopRepeats = value)));
                y += 40;
            }
            if (ev.Operation.Kind == "EnterTimeline")
            {
                b.TacticalButton("btnTargetTimeline", "目标时间轴：" + ev.Operation.TargetTimelineId, 8, y, width - 16, editor.CanEdit, () => {
                    var token = editor.Token;
                    b.TacticalChoices(row.Root.GetChild("btnTargetTimeline"), editor.Source.Timelines.Select(t => t.TimelineId).ToArray(), target =>
                        editor.Commit(token, "修改跳转目标", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation.TargetTimelineId = target));
                });
                y += 40;
            }
            if (approach || ev.Operation.Kind == "Movement" || ev.Operation.Kind == "TurnToTarget" || ev.Operation.Kind == "RetreatFromTarget")
            {
                b.SetVisible("operationParameterLabel", true);
                b.SetText("operationParameterLabel", compact ? "倍率" : ev.Operation.Kind == "TurnToTarget" ? "转向倍率" : "移动倍率");
                b.Place("operationParameterLabel", 8, y, 82, 32);
                b.TacticalInput("inputOperationParameter", ev.Operation.Speed.ToString(CultureInfo.InvariantCulture), "速度倍率", 94, y, width - 102, editor, editor.CanEdit,
                    (lease, text) => {
                        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || float.IsNaN(speed) || float.IsInfinity(speed))
                        { editor.Feedback("请输入有限数值。"); return false; }
                        return editor.Commit(lease, "修改速度倍率", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation.Speed = speed);
                    });
                y += 40;
            }
            if (ev.Operation.Kind == "TurnToTarget")
            {
                string sourceLabel = ev.Operation.FacingSource == "ActionFacing" ? "引用装配动作朝向"
                    : ev.Operation.FacingSource == "Manual" ? "手填面向容差" : "未配置";
                b.TacticalButton("btnTargetTimeline", "面向参数：" + sourceLabel, 8, y, width - 16, editor.CanEdit, () => {
                    var token = editor.Token;
                    b.TacticalChoices(row.Root.GetChild("btnTargetTimeline"), new[] { "引用装配动作朝向", "手填面向容差" }, choice => editor.Commit(token, "修改转向面向参数来源", source => {
                        var op = CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation;
                        op.FacingSource = choice == "引用装配动作朝向" ? "ActionFacing" : "Manual";
                        op.FacingActionSlotId = op.FacingSource == "ActionFacing" ? source.Slots.FirstOrDefault(s => !s.IsFunctional)?.SlotId : null;
                        op.FacingToleranceDegrees = op.FacingSource == "Manual" ? op.FacingToleranceDegrees ?? CharacterTestTacticalApproachActionSplit.DefaultFacingTolerance() : null;
                    }));
                }); y += 40;
                if (ev.Operation.FacingSource == "ActionFacing")
                {
                    string slotLabel = editor.Source.Slots.Count(s => !s.IsFunctional) == 1 ? "当前装配"
                        : CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionUseLabel(editor.Source, ev.Operation.FacingActionSlotId);
                    b.TacticalButton("btnCharacterActionParameter", "朝向参考：" + slotLabel
                        + " · " + editor.Preview.DescribeTurnFacing(ev.EventId), 8, y, width - 16, editor.CanEdit, () => {
                            var token = editor.Token;
                            b.TacticalChoices(row.Root.GetChild("btnCharacterActionParameter"), editor.Source.Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId).ToArray(), slot =>
                                editor.Commit(token, "修改转向朝向参考", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation.FacingActionSlotId = slot));
                        }); y += 40;
                }
                else if (ev.Operation.FacingSource == "Manual")
                {
                    b.SetVisible("operationSecondaryParameterLabel", true); b.SetText("operationSecondaryParameterLabel", "面向容差（°）");
                    b.Place("operationSecondaryParameterLabel", 8, y, 120, 32);
                    b.TacticalInput("inputOperationSecondaryParameter", ev.Operation.FacingToleranceDegrees?.ToString(CultureInfo.InvariantCulture) ?? "", "面向容差（度）",
                        136, y, width - 144, editor, editor.CanEdit, (lease, text) => {
                            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > 180)
                            { editor.Feedback("面向容差必须在 (0,180] 度内。"); return false; }
                            return editor.Commit(lease, "修改转向面向容差", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Operation.FacingToleranceDegrees = value);
                        }); y += 40;
                }
            }
            b.SetVisible("ruleWhenLabel", true);
            b.SetText("ruleWhenLabel", compact ? ev.ConditionRoot == null ? "条件：无" : "条件" : ev.Operation.Kind == "WaitForCondition" ? "等待放行条件" : action ? "动作起手条件"
                : ev.Operation.Kind == "ReturnToPlanner" ? "退出条件" : ev.Operation.Kind == "CompleteBlock" ? "本段结束条件" : "事件条件");
            b.Place("ruleWhenLabel", 8, y, compact ? width - 72 : width - 16, 28);
            if (compact && row.Root.GetChild("ruleWhenLabel")?.asTextField is Fgui.GTextField conditionHeading)
            { conditionHeading.textFormat.size = 16; conditionHeading.align = Fgui.AlignType.Left; }
            float conditionHeaderY = y;
            y += compact ? 36 : 30;
            var conditions = row.Root.GetChild("conditionList")?.asList;
            if (conditions != null)
            {
                conditions.visible = !compact || ev.ConditionRoot != null; conditions.SetXY(8, y); conditions.SetSize(414, 1);
                float scale = Math.Min(1, (width - 16) / 414f); conditions.SetScale(scale, scale);
                b.BindConditionList(model.Conditions); b.BindTacticalDataConditions();
                conditions.EnsureBoundsCorrect();
                float height = 0;
                for (int i = 0; i < conditions.numChildren; i++) height += conditions.GetChildAt(i).height + 2;
                conditions.height = Math.Max(1, height); conditions.scrollPane?.SetPosY(0, false);
                if (conditions.visible) y += height * scale + 6;
            }
            float half = (width - 24) / 2;
            b.SetVisible("btnAddCondition", true); b.SetButtonTitle("btnAddCondition", "+ 条件");
            b.SetEnabled("btnAddCondition", editor.CanEdit); b.Place("btnAddCondition", 8, y, half, 32);
            b.SetVisible("btnAddConditionGroup", true); b.SetButtonTitle("btnAddConditionGroup", "+ 逻辑组 / 动作数据");
            b.SetEnabled("btnAddConditionGroup", editor.CanEdit); b.Place("btnAddConditionGroup", 16 + half, y, half, 32);
            b.BindTacticalConditionLeases(row.Root); y += 40;
            if (compact)
            {
                y -= 40;
                b.SetVisible("btnAddConditionGroup", false);
                b.Place("btnAddConditionGroup", width - 56, conditionHeaderY, 48, 32);
                b.TacticalButton("btnAddCondition", "＋", width - 56, conditionHeaderY, 48, editor.CanEdit, () =>
                    b.TacticalChoices(row.Root.GetChild("btnAddCondition"), new[] { "判断条件", "逻辑组 / 动作数据" }, choice => {
                        if (choice == "判断条件") b.ShowConditionSubjectPicker(row.Root.GetChild("btnAddCondition"));
                        else row.Root.GetChild("btnAddConditionGroup").onClick.Call();
                    }));
                bool customized = ev.Priority != 0 || ev.MaxExecutionsPerPass != 1 || ev.CooldownFrames != 0;
                b.TacticalButton("btnGroupAll", (row.Advanced ? "▾ " : "▸ ") + "高级设置" + (customized ? " · 已调整" : ""), 8, y, width - 16, true, () => {
                    row.Advanced = !row.Advanced;
                    float scroll = _eventSetList.scrollPane?.posY ?? 0;
                    BindEventSetRow(row, row.Editor.Preview.FindEvent(row.Timeline, row.Event), width, actions);
                    _eventSetList.EnsureBoundsCorrect(); _eventSetList.scrollPane?.SetPosY(scroll, false);
                });
                y += 40;
                if (row.Advanced) y = BindCompactAdvanced(row, ev, model, y, width);
                row.ExpandedHeight = y; row.Root.height = y; b.Place("background", 0, 0, width, y);
                return;
            }
            b.TacticalButton("btnDeleteEvent", "删除事件", 8, y, width - 16, editor.CanEdit, () => {
                var lease = editor.Token;
                if (b._tacticalDeleteKey != row.Key || b._tacticalDeleteToken.WorkspaceRevision != lease.WorkspaceRevision)
                {
                    b._tacticalDeleteKey = row.Key; b._tacticalDeleteToken = lease;
                    b.SetButtonTitle("btnDeleteEvent", "确认删除此事件");
                    string[] impacts = CharacterTestTacticalAuthoringEdits.DeleteImpact(editor.Source, new[] { row.Event });
                    b.SetText("validationText", impacts.Length == 0 ? "无外部引用；删除可撤销。" : string.Join("\n", impacts));
                    b.SetVisible("validationText", true); b.Place("validationText", 8, row.ExpandedHeight, width - 16, Math.Max(40, impacts.Length * 24));
                    row.Root.height = row.ExpandedHeight + Math.Max(40, impacts.Length * 24) + 8;
                    _eventSetList.EnsureBoundsCorrect(); return;
                }
                editor.Commit(b._tacticalDeleteToken, "删除事件", source => CharacterTestTacticalAuthoringEdits.Delete(source, row.Timeline, new[] { row.Event }, true));
                b._tacticalDeleteKey = null;
            });
            y += 40; row.ExpandedHeight = y; row.Root.height = y;
            b.Place("background", 0, 0, width, y);
        }

        private void BindTacticalForm(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            _root.visible = true;
            foreach (var child in _root.GetChildren()) { _tacticalVisibility[child] = child.visible; child.visible = false; }
            var editor = model.TacticalEditor;
            if (editor == null) return;
            if (editor.Source.ExecutionInput != null) { BindPointMoveForm(editor); return; }
            float inspectorWidth = _root.width > 100f ? _root.width : 360f;
            _root.SetSize(inspectorWidth, editor.InspectorHeight);
            float contentW = inspectorWidth - 24f;
            float halfBtnW = (contentW - 8f) / 2f;
            string timeline = string.IsNullOrEmpty(model.TimelineId) ? editor.Source.EntryTimelineId : model.TimelineId;
            string id = model.EventId;
            var ev = editor.Preview.FindEvent(timeline, id);
            bool editable = editor.CanEdit;
            bool timelineMode = ev == null && model.InspectorMode == BehaviorRecipeInspectorMode.Timeline;
            var sourceTimeline = editor.Source.Timelines.FirstOrDefault(t => t?.TimelineId == timeline) ?? editor.Source.Timelines.FirstOrDefault();
            bool missingTimeline = sourceTimeline == null;
            if (sourceTimeline == null) sourceTimeline = new CharacterTestTacticalAuthoringTimeline { TimelineId = "single-primary", Title = "未配置时间轴" };
            timeline = sourceTimeline.TimelineId;
            SetVisible("background", true); SetVisible("bodyBackground", true); SetVisible("headerTitle", true);
            SetText("headerTitle", timelineMode ? "时间轴属性" : ev == null ? "战术卡制作" : "战术事件属性");
            Place("background", 0, 0, inspectorWidth, UnityEngine.Mathf.Max(900, _root.height));
            Place("headerTitle", 12, 8, contentW, 32);
            Place("bodyBackground", 12, 64, contentW, UnityEngine.Mathf.Max(830, _root.height - 80));
            bool shared = editor.Token.Target == CharacterTestTacticalEditTarget.SharedDefinition;
            TacticalInput("inputTitle", timelineMode ? sourceTimeline.Title : ev?.Title ?? (shared ? editor.Source.Name : editor.CardName), timelineMode ? "时间轴名称" : ev == null ? shared ? "共享封装名称" : "卡名称" : "事件名称", 12, 72, contentW, editor, editable,
                (token, text) => timelineMode ? editor.Commit(token, "修改时间轴名称", s => CharacterTestTacticalAuthoringEdits.Timeline(s, timeline).Title = text)
                    : ev == null ? editor.Commit(token, "修改卡名称", null, text)
                    : editor.Commit(token, "修改事件名称", s => CharacterTestTacticalAuthoringEdits.Event(s, timeline, id).Title = text));
            SetVisible("summary", true); SetText("summary", ev == null ? "选择事件编辑；Ctrl 点击可多选。" : ev.Operation.Kind + " · Ctrl 点击可多选");
            Place("summary", 12, 112, contentW, 36);
            if (timelineMode)
            {
                SetVisible("priorityLabel", true); SetText("priorityLabel", "时间轴顺序"); Place("priorityLabel", 12, 166, 100, 32);
                TacticalInput("inputPriority", sourceTimeline.Order.ToString(CultureInfo.InvariantCulture), "时间轴顺序", 116, 166, contentW - 104, editor, editable,
                    (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改时间轴顺序", s => CharacterTestTacticalAuthoringEdits.Timeline(s, timeline).Order = value)));
                TacticalButton("btnCharacterActionParameter", editor.Source.EntryTimelineId == timeline ? "当前入口时间轴" : "设为入口时间轴", 12, 214, contentW,
                    editable && editor.Source.EntryTimelineId != timeline, () => editor.Commit(editor.Token, "修改入口时间轴", s => s.EntryTimelineId = timeline));
            }
            if (ev != null)
            {
                BindTacticalReference(editor, timeline, id, ev.Start, false, editable, 166, contentW);
                bool hasAuthoredEnd = ev.Operation.Kind != "CharacterAction" && ev.TimingKind != "Instant";
                BindTacticalReference(editor, timeline, id, ev.End, true, editable && hasAuthoredEnd, 254, contentW);
                if (!hasAuthoredEnd)
                {
                    SetText("inputDurationFrames", ev.Operation.Kind == "CharacterAction" ? "随动作时长" : "单帧事件");
                    SetVisible("btnTargetTimeline", false); SetVisible("btnSignal", false);
                }
                if (ev.Operation.Kind == "CharacterAction")
                    TacticalButton("btnViewCharacterActionTimeline", "打开动作制作入口", 12, 298, contentW,
                        model.CharacterActionReferenceValid && model.CharacterActionTimelinePreview?.Available == true, ShowCharacterActionTimelinePreview);
                var slot = _root.GetChild("btnCharacterActionParameter")?.asButton;
                bool action = ev.Operation.Kind == "CharacterAction";
                TacticalButton("btnCharacterActionParameter", action ? "动作：" + ActionBindingName(editor, ev) + " ▾" : "关联动作事件",
                    12, 342, contentW, editable && (action || ev.Operation.Kind == "ApproachOptimalRange"), () =>
                    {
                        if (action) { ShowActionBindingPicker(editor, ev, slot); return; }
                        var token = editor.Token;
                        var values = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.Where(e => e.Operation.Kind == "CharacterAction").Select(e => e.EventId).ToArray();
                        TacticalChoices(slot, values, selected => editor.Commit(token, "修改动作关联", s => {
                            var target = CharacterTestTacticalAuthoringEdits.Event(s, timeline, id);
                            target.Operation.SourceActionEventId = selected;
                        }), display: value => CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(
                            CharacterTestTacticalAuthoringEdits.Event(editor.Source, timeline, value)));
                    });
                float opLabelW = 68f;
                float opValW = UnityEngine.Mathf.Max(60f, (contentW - 156f) / 2f);
                SetVisible("operationParameterLabel", true); SetText("operationParameterLabel", "速度倍率"); Place("operationParameterLabel", 12, 390, opLabelW, 32);
                TacticalInput("inputOperationParameter", ev.Operation.Speed.ToString(CultureInfo.InvariantCulture), "倍率", 12 + opLabelW + 4, 390, opValW, editor,
                    editable && (ev.Operation.Kind == "ApproachOptimalRange" || ev.Operation.Kind == "TurnToTarget" || ev.Operation.Kind == "RetreatFromTarget"), (token, text) => {
                        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) || float.IsNaN(speed) || float.IsInfinity(speed)) { editor.Feedback("请输入有限数值。"); return false; }
                        return editor.Commit(token, "修改速度", s => CharacterTestTacticalAuthoringEdits.Event(s, timeline, id).Operation.Speed = speed);
                    });
                float prioX = 12 + opLabelW + 4 + opValW + 8;
                float prioLabelW = 52f;
                float prioValW = contentW - (prioX - 12) - prioLabelW - 4;
                SetVisible("priorityLabel", true); SetText("priorityLabel", "优先级"); Place("priorityLabel", prioX, 390, prioLabelW, 32);
                TacticalInput("inputPriority", ev.Priority.ToString(CultureInfo.InvariantCulture), "优先级", prioX + prioLabelW + 4, 390, prioValW, editor, editable,
                    (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改优先级", s => CharacterTestTacticalAuthoringEdits.Event(s, timeline, id).Priority = value)));
            }
            TacticalButton("btnGroupAll", ev == null ? "时间轴进入条件" : "事件起手条件", 12, 438, halfBtnW, editable && !missingTimeline,
                () => editor.OpenConditions(timeline, ev == null ? "$timeline-entry" : id));
            TacticalButton("btnGroupAnyNot", "整卡启用条件", 12 + halfBtnW + 8, 438, halfBtnW, editable, editor.OpenCardActivation);
            if (ev == null)
            {
                TacticalButton("btnOutcomeReturn", "+ 添加事件", 12, 482, contentW, editable, () => {
                    var token = editor.Token;
                    var values = shared ? new[] { "CharacterAction", "Movement", "ApproachOptimalRange", "TurnToTarget", "WaitForCondition", "ReturnToPlanner", "RetreatFromTarget" }
                        : new[] { "CharacterAction", "ApproachOptimalRange", "TurnToTarget", "WaitForCondition", "ReturnToPlanner" };
                    TacticalChoices(_root.GetChild("btnOutcomeReturn"), values, value => editor.Commit(token, "添加事件", s => {
                        if (s.Timelines.Count == 0) { s.Timelines.Add(new CharacterTestTacticalAuthoringTimeline { TimelineId = timeline, Title = "主时间轴" }); s.EntryTimelineId = timeline; }
                        if (shared && value == "RetreatFromTarget") { s.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema; s.Version = 1; }
                        CharacterTestTacticalAuthoringEdits.Add(s, timeline, value);
                    }));
                });
                SetVisible("btnDeleteEvent", false);
            }
            else
            {
                TacticalButton("btnOutcomeReturn", "+ 添加事件", 12, 482, halfBtnW, editable, () => {
                    var token = editor.Token;
                    var values = shared ? new[] { "CharacterAction", "Movement", "ApproachOptimalRange", "TurnToTarget", "WaitForCondition", "ReturnToPlanner", "RetreatFromTarget" }
                        : new[] { "CharacterAction", "ApproachOptimalRange", "TurnToTarget", "WaitForCondition", "ReturnToPlanner" };
                    TacticalChoices(_root.GetChild("btnOutcomeReturn"), values, value => editor.Commit(token, "添加事件", s => {
                        if (s.Timelines.Count == 0) { s.Timelines.Add(new CharacterTestTacticalAuthoringTimeline { TimelineId = timeline, Title = "主时间轴" }); s.EntryTimelineId = timeline; }
                        if (shared && value == "RetreatFromTarget") { s.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema; s.Version = 1; }
                        CharacterTestTacticalAuthoringEdits.Add(s, timeline, value);
                    }));
                });
                TacticalButton("btnDeleteEvent", "删除选择", 12 + halfBtnW + 8, 482, halfBtnW, editable, () => {
                    string[] ids = editor.Selected(id);
                    string key = string.Join("|", ids.OrderBy(x => x));
                    bool confirm = key == _tacticalDeleteKey && _tacticalDeleteToken.WorkspaceRevision == editor.Token.WorkspaceRevision;
                    var impacts = CharacterTestTacticalAuthoringEdits.DeleteImpact(editor.Source, ids);
                    if (!confirm)
                    {
                        _tacticalDeleteKey = key; _tacticalDeleteToken = editor.Token;
                        editor.Feedback("请核对删除影响，再次点击确认。");
                        return;
                    }
                    editor.Commit(_tacticalDeleteToken, "删除事件", s => CharacterTestTacticalAuthoringEdits.Delete(s, timeline, ids, true));
                    _tacticalDeleteKey = null;
                });
                SetVisible("btnDeleteEvent", true);
            }
            string linkedEvent = !string.IsNullOrEmpty(ev?.Operation?.SourceActionEventId) ? ev.Operation.SourceActionEventId
                : ev?.End?.Kind == "Anchor" ? ev.End.SourceEventId : ev?.Start?.Kind == "Anchor" ? ev.Start.SourceEventId : string.Empty;
            if (!string.IsNullOrEmpty(linkedEvent))
                TacticalButton("btnLocateTarget", "定位关联事件：" + linkedEvent, 12, 534, contentW, true, () => editor.Locate?.Invoke(timeline, linkedEvent));
            SetVisible("validationText", true);
            SetText("validationText", editor.Message + "\n" + model.TacticalDescription);
            Place("validationText", 12, string.IsNullOrEmpty(linkedEvent) ? 534 : 578, contentW, string.IsNullOrEmpty(linkedEvent) ? 340 : 296);
            var detail = _root.GetChild("validationText")?.asTextField;
            if (detail != null) { detail.color = UnityEngine.Color.white; detail.textFormat.size = 14; detail.verticalAlign = Fgui.VertAlignType.Top; }
            var selectedIds = editor.Selected(id);
            if (editor.Controller?.CompositionEditingSession != null)
                ArrangeSequentialInspector(model, ev, timelineMode);
            if (_tacticalDeleteKey == string.Join("|", selectedIds.OrderBy(x => x))
                && _tacticalDeleteToken.WorkspaceRevision == editor.Token.WorkspaceRevision && _tacticalDeleteToken.CardId == editor.CardId)
            { SetButtonTitle("btnDeleteEvent", "确认删除"); ShowTacticalDeleteImpact(selectedIds, CharacterTestTacticalAuthoringEdits.DeleteImpact(editor.Source, selectedIds)); }
            else _tacticalDeleteKey = null;
        }

        private void BindPointMoveForm(CharacterTestTacticalEditingSession editor)
        {
            float width = _root.width > 100f ? _root.width : 360f, content = width - 24;
            _root.SetSize(width, UnityEngine.Mathf.Max(760, editor.InspectorHeight));
            SetVisible("background", true); Place("background", 0, 0, width, _root.height);
            SetVisible("headerTitle", true); SetText("headerTitle", "前往本次选定点 · 作者规则"); Place("headerTitle", 12, 8, content, 32);
            SetVisible("summary", true); SetText("summary", editor.CardName + "\n每次执行输入 WorldPoint；以执行位置检查范围，原移动接受后扣费并起算冷却。");
            Place("summary", 12, 48, content, 80);
            var rules = editor.Source.ExecutionInput;
            string[] inputs = { "inputLocalFrame", "inputDurationFrames", "inputMaxExecutions", "inputPriority", "inputOperationParameter", "inputCooldown" };
            string[] labels = { "timeLabel", "durationFramesLabel", "repeatLabel", "priorityLabel", "operationParameterLabel", "operationSecondaryParameterLabel" };
            string[] fields = { "range", "arrival", "frames", "speed", "cost", "cooldown" };
            string[] titles = { "范围（毫米）", "到达容差（毫米）", "最大帧数", "速度（千分比）", "成本", "冷却（秒）" };
            string[] values = { rules.RangeMillimeters.ToString(CultureInfo.InvariantCulture), rules.ArrivalMillimeters.ToString(CultureInfo.InvariantCulture),
                rules.MaximumFrames.ToString(CultureInfo.InvariantCulture), rules.SpeedMilli.ToString(CultureInfo.InvariantCulture),
                editor.PointCost.ToString(CultureInfo.InvariantCulture), editor.PointCooldownSeconds.ToString("R", CultureInfo.InvariantCulture) };
            for (int i = 0; i < fields.Length; i++)
            {
                string field = fields[i]; float y = 144 + i * 52;
                SetVisible(labels[i], true); SetText(labels[i], titles[i]); Place(labels[i], 12, y, 144, 32);
                TacticalInput(inputs[i], values[i], titles[i], 160, y, content - 148, editor, editor.CanEdit,
                    (token, text) => editor.EditPointField(token, field, text));
            }
            float buttonWidth = (content - 16) / 3;
            TacticalButton("btnOutcomeReturn", "保存草稿", 12, 472, buttonWidth, editor.CanEdit, editor.Save);
            TacticalButton("btnDeleteEvent", "撤销", 20 + buttonWidth, 472, buttonWidth, editor.CanEdit, () => editor.Undo(false));
            TacticalButton("btnLocateTarget", "重做", 28 + buttonWidth * 2, 472, buttonWidth, editor.CanEdit, () => editor.Undo(true));
            SetVisible("validationText", true); SetText("validationText", editor.Message + "\n" + editor.Preview.StatusText);
            Place("validationText", 12, 520, content, 200);
        }

        private void ShowTacticalDeleteImpact(string[] ids, string[] impacts)
        {
            float w = _root.width > 100f ? _root.width : 360f;
            float contentW = w - 24f;
            SetVisible("validationText", false);
            SetVisible("btnLocateTarget", false);
            SetVisible("deleteHint", true); SetText("deleteHint", "删除 " + ids.Length + " 个事件；关联将保留，可撤销。"); Place("deleteHint", 12, 532, contentW, 26);
            var hint = _root.GetChild("deleteHint")?.asTextField;
            if (hint != null) { hint.autoSize = Fgui.AutoSizeType.None; hint.align = Fgui.AlignType.Left; hint.verticalAlign = Fgui.VertAlignType.Top; }
            var list = _root.GetChild("conditionList")?.asList;
            if (list == null) return;
            list.visible = true; Place("conditionList", 12, 562, contentW, 302);
            var lines = impacts.Length == 0 ? new[] { "没有外部引用。" } : impacts;
            list.itemRenderer = (index, item) => {
                var row = item.asCom;
                if (row == null) return;
                row.SetSize(list.viewWidth, 62);
                foreach (var child in row.GetChildren()) child.visible = false;
                var label = row.GetChild("observation")?.asTextField;
                if (label != null) { label.visible = true; label.SetXY(8, 4); label.SetSize(list.viewWidth - 16, 54); label.textFormat.size = 13; label.color = UnityEngine.Color.white; label.align = Fgui.AlignType.Left; label.verticalAlign = Fgui.VertAlignType.Top; label.relations.ClearAll(); label.AddRelation(row, Fgui.RelationType.Width); label.text = lines[index]; }
            };
            list.numItems = lines.Length;
        }

        private void BindTacticalReference(CharacterTestTacticalEditingSession editor, string timeline, string id,
            CharacterTestTacticalFrameReference reference, bool end, bool enabled, float y, float contentW = 336f)
        {
            if (editor.Source.HasTimingVersion && editor.Preview.FocusBlockId != null)
            { BindTimingRelation(editor, timeline, id, reference, end, enabled, y, contentW); return; }
            string inputName = end ? "inputDurationFrames" : "inputLocalFrame";
            string kindName = end ? "btnDurationMode" : "btnFrameAbsolute";
            string sourceName = end ? "btnTargetTimeline" : "btnFrameActionEnd";
            string anchorName = end ? "btnSignal" : "btnSignalReason";
            string labelName = end ? "durationFramesLabel" : "timeLabel";
            float labelW = 76f;
            float kindW = 86f;
            float inputW = contentW - labelW - kindW - 16f;
            SetVisible(labelName, true);
            SetText(labelName, (end ? "结束" : "开始") + (reference?.Kind == "Anchor" ? "偏移" : "帧"));
            Place(labelName, 12, y, labelW, 32);
            TacticalButton(kindName, reference?.Kind == "Anchor" ? "时间关联" : reference?.Space == "Block" ? "本段帧" : "时间轴帧", 12 + labelW + 8, y, kindW, enabled, () => {
                var token = editor.Token;
                bool member = CharacterTestTacticalBlockEdits.Owner(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline), id) != null;
                TacticalChoices(_root.GetChild(kindName), member ? new[] { "Absolute", "TimelineAbsolute", "Anchor" } : new[] { "Absolute", "Anchor" },
                    kind => SetTacticalReferenceKind(editor, token, timeline, id, end, kind));
            });
            TacticalInput(inputName, (reference?.Kind == "Anchor" ? reference.OffsetFrames : reference?.Frame ?? 0).ToString(CultureInfo.InvariantCulture),
                "帧 / 偏移", 12 + labelW + 8 + kindW + 8, y, inputW, editor, enabled, (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改时间参数", s => {
                    var old = CharacterTestTacticalBlockEdits.Reference(s, timeline, id, end);
                    var next = CharacterTestTacticalBlockEdits.Copy(old) ?? CharacterTestTacticalAuthoringTemplates.Absolute(0);
                    if (next.Kind == "Anchor") next.OffsetFrames = value; else next.Frame = value;
                    CharacterTestTacticalAuthoringEdits.SetReference(s, timeline, id, end, next);
                })));
            bool isAnchor = reference?.Kind == "Anchor";
            if (isAnchor)
            {
                float srcW = contentW - 104f;
                string targetKey = reference.TargetKind + ":" + reference.SourceEventId;
                TacticalButton(sourceName, "来源：" + TacticalChoiceLabel(targetKey), 12, y + 40, srcW, enabled, () => {
                    var token = editor.Token;
                    var values = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events
                        .Where(e => end || e.EventId != id).Select(e => "Event:" + e.EventId)
                        .Concat(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).EventBlocks.Select(b => "Block:" + b.BlockId)).ToArray();
                    TacticalChoices(_root.GetChild(sourceName), values, value => editor.Commit(token, "修改关联事件", s => {
                        var target = CharacterTestTacticalBlockEdits.Reference(s, timeline, id, end);
                        int split = value.IndexOf(':'); target.TargetKind = value.Substring(0, split); target.SourceEventId = value.Substring(split + 1);
                        if (target.TargetKind == "Block") { if (target.Anchor != "Start" && target.Anchor != "End") target.Anchor = "End"; }
                        else if (target.SourceEventId == id || CharacterTestTacticalAuthoringEdits.Event(s, timeline, target.SourceEventId).Operation.Kind != "CharacterAction"
                            && (target.Anchor == "ActionStartupEnd" || target.Anchor == "ActionEnd")) target.Anchor = "Start";
                    }));
                });
                _root.GetChild(sourceName).tooltips = "右键跳转查看引用来源";
                _root.GetChild(sourceName).onRightClick.Set(() => editor.OpenEventFocus(timeline, reference.SourceEventId));
                TacticalButton(anchorName, TacticalChoiceLabel(reference?.Anchor ?? "锚点"), 12 + srcW + 8, y + 40, 96, enabled, () => {
                    var token = editor.Token;
                    var source = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.FirstOrDefault(e => e.EventId == reference.SourceEventId);
                    var anchors = reference.TargetKind == "Block" ? new[] { "Start", "End" } : reference.SourceEventId == id ? new[] { "Start" }
                        : source?.Operation.Kind == "CharacterAction" ? new[] { "Start", "End", "ActionStartupEnd", "ActionEnd" } : new[] { "Start", "End" };
                    TacticalChoices(_root.GetChild(anchorName), anchors, value => editor.Commit(token, "修改锚点", s => {
                        CharacterTestTacticalBlockEdits.Reference(s, timeline, id, end).Anchor = value;
                    }));
                });
            }
            else
            {
                SetVisible(sourceName, false);
                SetVisible(anchorName, false);
            }
        }

        private static bool IntEdit(CharacterTestTacticalEditingSession editor, string text, Func<int, bool> commit)
        { if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) { editor.Feedback("请输入有效整数。"); return false; } return commit(value); }

        private void TacticalInput(string name, string value, string prompt, float x, float y, float width,
            CharacterTestTacticalEditingSession editor, bool enabled, Func<CharacterTestTacticalEditToken, string, bool> commit)
        {
            var input = _root.GetChild(name)?.asTextInput;
            if (input == null) return;
            _tacticalInputs.Add(input);
            input.color = enabled ? UnityEngine.Color.white : new UnityEngine.Color(0.72f, 0.78f, 0.83f);
            input.textFormat.size = 16; input.alpha = 1f;
            input.visible = true; input.enabled = true; input.grayed = false; input.editable = enabled; input.touchable = enabled; input.promptText = prompt;
            // These shared controls carry legacy restrictions such as "0-9", which is a literal
            // regex in FairyGUI and discards typed digits. This editor validates the complete value on commit.
            input.restrict = null;
            input.maxLength = name == "inputTitle" ? 256 : 32;
            Place(name, x, y, width, 32);
            if (!input.focused) input.text = value ?? string.Empty;
            void Begin() { if (!_tacticalFieldLeases.ContainsKey(input)) _tacticalFieldLeases[input] = new TacticalFieldLease { Token = editor.Token, Text = value ?? string.Empty, Commit = enabled ? commit : null }; }
            void Finish()
            {
                if (!_tacticalFieldLeases.TryGetValue(input, out var lease)) return;
                _tacticalFieldLeases.Remove(input);
                if (lease.Commit != null && input.text != lease.Text) lease.Commit(lease.Token, input.text);
            }
            input.onFocusIn.Set(Begin);
            input.onChanged.Set(() => { if (input.focused) Begin(); });
            input.onSubmit.Set(Finish);
            input.onFocusOut.Set(() => {
                if (CharacterTestTacticalInputNavigation.IsContextNavigationPointer())
                {
                    if (_tacticalFieldLeases.TryGetValue(input, out var lease))
                    { _tacticalFieldLeases.Remove(input); input.text = lease.Text; }
                    return;
                }
                Finish();
            });
            input.onKeyDown.Set(context => { if (context.inputEvent?.keyCode != UnityEngine.KeyCode.Escape) return;
                if (_tacticalFieldLeases.TryGetValue(input, out var lease)) { _tacticalFieldLeases.Remove(input); input.text = lease.Text; }
                context.StopPropagation(); });
        }

        private void TacticalButton(string name, string title, float x, float y, float width, bool enabled, Action action)
        {
            SetVisible(name, true); Place(name, x, y, width, 32); SetButtonTitle(name, title); SetEnabled(name, enabled); BindButton(name, action);
            var button = _root.GetChild(name)?.asButton;
            if (button != null) { if (!_tacticalButtonIcons.ContainsKey(button)) _tacticalButtonIcons[button] = button.icon; button.icon = string.Empty; }
        }

        private string TacticalChoiceLabel(string value)
        {
            string known = value switch {
                "LoopRegion" => "循环区间", "CompleteBlock" => "结束当前意图段", "EnterTimeline" => "进入时间轴",
                "ReturnToPlanner:Instant" => "单帧退出行为卡", "EnterTimeline:Instant" => "单帧进入时间轴",
                "Builtin:SimulationEnd" => "模拟结束帧", "Builtin:Minimum" => "最小持续帧", "Ratio" => "比例",
                "Absolute" => "手填帧数", "TimelineAbsolute" => "所属时间轴帧", "Anchor" => "事件锚点", "Start" => "事件开始", "End" => "事件结束",
                "ActionStartupEnd" => "动作前摇结束", "ActionEnd" => "动作结束", "CharacterAction" => "角色动作",
                "Movement" => "移动（六种方式）", "ApproachOptimalRange" => "接近最佳位置", "TurnToTarget" => "转向目标", "WaitForCondition" => "等待条件", "RetreatFromTarget" => "背离目标后撤",
                "ReturnToPlanner" => "显式退出行为卡", "MovementArrived" => "移动到位", "TurnAligned" => "面向达标", "RootDistanceRange" => "动作距离", "TargetFacing" => "动作朝向",
                "All" => "全部满足", "Any" => "任意满足", "Not" => "取反", _ => null };
            if (known != null) return known;
            var source = _model?.TacticalEditor?.Source;
            if (value.StartsWith("Block:", StringComparison.Ordinal))
                return "事件段 · " + (source?.Timelines.SelectMany(t => t.EventBlocks).FirstOrDefault(b => b.BlockId == value.Substring(6))?.Title ?? value.Substring(6));
            if (value.StartsWith("Event:", StringComparison.Ordinal)) value = value.Substring(6);
            var ev = source?.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == value);
            if (ev != null && !string.IsNullOrWhiteSpace(ev.Title) && ev.Title != value)
            {
                string title = CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(ev);
                var peers = source.Timelines.SelectMany(t => t.Events)
                    .Where(e => CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(e) == title).ToArray();
                return peers.Length > 1 ? title + "（第 " + (Array.IndexOf(peers, ev) + 1) + " 处）" : title;
            }
            return value;
        }

        private void TacticalChoices(Fgui.GObject anchor, string[] values, Action<string> choose, int page = 0,
            Func<string, string> display = null)
        {
            if (values.Length == 0) { _model?.TacticalEditor?.Feedback("没有可选择的来源，请先添加相应事件。"); return; }
            int start = page * 15;
            var labels = values.Skip(start).Take(15).Select(value => display != null ? display(value) : TacticalChoiceLabel(value)).ToList();
            int count = labels.Count;
            if (values.Length > 15) labels.Add(start + count < values.Length ? "下一页" : "返回第一页");
            ShowConditionDropdown(anchor, labels.ToArray(), -1, index => {
                if (index == count) TacticalChoices(anchor, values, choose, start + count < values.Length ? page + 1 : 0, display);
                else choose(values[start + index]);
            });
        }
    }
}
