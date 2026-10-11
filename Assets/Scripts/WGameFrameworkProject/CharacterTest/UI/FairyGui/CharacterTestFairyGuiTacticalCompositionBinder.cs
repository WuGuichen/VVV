using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Binds only Composer-published components. Width expresses readable structure, never frames.</summary>
    internal sealed partial class CharacterTestFairyGuiTacticalCompositionBinder
    {
        private Fgui.GComponent _panel;
        private CharacterTestTacticalCompositionEditingSession _session;
        private CharacterTestTacticalEditToken _choiceToken;
        private readonly List<Action> _choices = new List<Action>();
        private string _detailsKey;
        private bool _choicesRequireEdit = true;
        private CharacterTestTacticalEditToken? _nameLease;
        private string _nameBefore;
        private readonly List<(CharacterTestTacticalDslNode node, bool close)> _items = new List<(CharacterTestTacticalDslNode, bool)>();
        private readonly Dictionary<string, Fgui.GObject> _controls = new Dictionary<string, Fgui.GObject>();
        private Fgui.GList _choiceList;
        private CharacterTestTacticalDslSource _itemsDocument;
        private Fgui.GList _boundCompositionList;
        private IMxFairyGuiPopupService _popupService;
        private MxUiViewId _viewId;
        private CharacterTestFairyGuiTacticalActionPicker _actionLibraryPicker;
        internal void ConfigureServices(MxUiViewId viewId, IMxFairyGuiPopupService popupService)
        { _viewId = viewId; _popupService = popupService; }
        private Fgui.GObject Control(string name) => _controls.TryGetValue(name, out var value) ? value : _panel?.GetChild(name);
        internal void Deactivate()
        {
            _actionLibraryPicker?.Close();
            _session = null; _nameLease = null; _choices.Clear();
            if (_panel != null && !_panel.isDisposed) _panel.visible = false;
            foreach (var control in _controls.Values)
            {
                if (control == null || control.isDisposed) continue;
                control.visible = false;
                if (_panel != null && !_panel.isDisposed && control.parent != _panel)
                { control.sortingOrder = 0; _panel.AddChild(control); }
            }
            if (_choiceList != null && !_choiceList.isDisposed) Fgui.GRoot.inst.HidePopup(_choiceList);
        }
        internal void Bind(Fgui.GComponent panel, CharacterTestTacticalCompositionEditingSession session)
        {
            if (!ReferenceEquals(_panel, panel))
            {
                Deactivate();
                _controls.Clear();
                _boundCompositionList = null;
                if (panel != null) foreach (string name in CharacterTestTacticalPreviewControlContract.CompositionButtons)
                    if (panel.GetChild(name) is Fgui.GObject child) _controls[name] = child;
                _choiceList = panel?.GetChild("choiceList")?.asList;
            }
            _panel = panel; _session = session;
            if (panel == null) return;
            panel.visible = session != null;
            if (session == null)
            {
                Deactivate();
                return;
            }
            BindCardName();
            var list = panel.GetChild("compositionList")?.asList;
            if (list == null) return;
            var choiceList = _choiceList;
            if (choiceList != null && (!(_choicesRequireEdit ? _session.Accepts(_choiceToken) : _session.AcceptsView(_choiceToken)) || _choices.Count == 0)) { choiceList.visible = false; _choices.Clear(); }
            RefreshFlattenedItems(session.Document);
            bool rowsNeedRebuild = !ReferenceEquals(_boundCompositionList, list)
                || list.numChildren != _items.Count;
            if (!rowsNeedRebuild)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (list.GetChildAt(i)?.asCom != null)
                        continue;
                    rowsNeedRebuild = true;
                    break;
                }
            }
            if (rowsNeedRebuild)
                list.RemoveChildrenToPool();
            _boundCompositionList = list;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i]; var node = item.node;
                bool rowCreated = rowsNeedRebuild;
                var row = rowsNeedRebuild
                    ? list.AddItemFromPool().asCom
                    : list.GetChildAt(i)?.asCom;
                if (row == null)
                    continue;
                row.opaque = true;
                row.data = i;
                bool group = node.Children != null;
                var definition = session.Document.Definitions.FirstOrDefault(d => d.DefinitionId == node.DefinitionId);
                row.GetChild("title").text = item.close ? "〕" : (group ? "〔 " : "") + KindTitle(node.Kind, definition?.Source.Name);
                row.GetChild("detail").text = string.Empty;
                row.GetChild("detail").visible = false;
                float bodyWidth = item.close ? 26 : group ? 86 : 186;
                row.width = bodyWidth;
                row.height = 86;
                var title = row.GetChild("title").asTextField;
                title.SetXY(group ? 6 : 14, 26);
                title.SetSize(bodyWidth - 20, 36);
                title.textFormat.size = group ? 14 : 17;
                title.textFormat.bold = !group;
                title.touchable = false;
                row.GetChild("background").visible = !group;
                row.GetChild("background").SetXY(6, 18);
                row.GetChild("background").SetSize(row.width - 12, 60);
                bool selected = session.Selection.Contains(node.NodeId);
                row.GetChild("background").asGraph.DrawRect(row.width - 12, 60, selected ? 2 : 1,
                    selected ? new Color32(68, 196, 168, 255) : new Color32(48, 78, 95, 255), new Color32(17, 37, 49, 255));
                if (rowCreated)
                {
                    row.onClick.Set(HandleCompositionRowClick);
                    row.onRightClick.Set(HandleCompositionRowRightClick);
                }
                BindSelectedSharedControls(row, node, !item.close && node.Kind == "Reference"
                    && selected && node.NodeId == session.SelectedNodeId);
            }
            panel.GetChild("compositionTitle").text = (session.Frozen != null ? "运行帧 " + session.FrozenSourceFrame + " 的冻结源 · 只读" : "战术卡编排 · 从左向右") + " · " + session.Document.Name;
            string impact = session.Target == CharacterTestTacticalEditTarget.SharedDefinition ? session.ImpactText : "所选：" + KindTitle(session.SelectedNode?.Kind, session.SelectedDefinition?.Source.Name);
            panel.GetChild("sharedImpactText").text = impact + (string.IsNullOrEmpty(session.Message) ? "" : "\n" + session.Message);
            panel.GetChild("sharedImpactText").tooltips = impact;
            foreach (string name in CharacterTestTacticalPreviewControlContract.CompositionButtons) if (Control(name) != null) Control(name).visible = false;
            Button("btnInsert", "封装", session.CanEdit, ShowInsert);
            Button("btnGroup", "组", session.CanEdit, ShowGroupOperations);
            Button("btnEntrance", "条件", session.CanEdit, ShowConditionOperations);
            Button("btnCompositionUndo", "撤销", session.CanEdit, () => session.Undo(false));
            Button("btnCompositionRedo", "重做", session.CanEdit, () => session.Undo(true));
            Button("btnDelete", "删除", session.CanEdit, () => session.Edit(session.Token, "删除编排项",
                source => CharacterTestTacticalCompositionEdits.Delete(source, session.SelectedNodeId)));
            Button("btnExpandShared", session.Target == CharacterTestTacticalEditTarget.Composition ? "展开内部" : "收起内部", true,
                () => session.Select(session.SelectedNodeId, session.Target != CharacterTestTacticalEditTarget.Composition ? CharacterTestTacticalEditTarget.Composition
                    : session.SelectedNode?.Kind == "Reference" ? CharacterTestTacticalEditTarget.SharedDefinition : CharacterTestTacticalEditTarget.Composition));
            Button("btnCompositionSave", "保存", session.CanEdit, session.Save);
            Button("btnCompositionCopy", "另存卡", session.CanEdit, session.CopyCard);
            Button("btnCopyShared", "另存独立封装", session.CanEdit && session.SelectedNode?.Kind == "Reference", () => session.DuplicateDefinition(session.Token));
            Button("btnDescriptionDetails", session.DetailsOpen ? "收起说明" : "说明详情", true, () => { session.DetailsOpen = !session.DetailsOpen; session.Feedback(""); });
            Button("btnActivation", "整卡启用", session.CanEdit,
                () => session.Select(session.Document.Root.NodeId, CharacterTestTacticalEditTarget.CardActivation));
            if (session.Target == CharacterTestTacticalEditTarget.SharedDefinition)
            {
                Button("btnConfirmShared", session.SharedConfirmed ? "完成编辑" : "编辑内容", session.CanEdit,
                    () => { if (session.SharedConfirmed) session.FinishSharedEditing(); else BeginSharedEditing(); });
                if (Control("btnConfirmShared") != null) Control("btnConfirmShared").tooltips = session.ImpactText;
            }
            if (session.DetailsOpen) ShowDescriptionDetails();
        }

        private void BindSelectedSharedControls(Fgui.GComponent row, CharacterTestTacticalDslNode node, bool active)
        {
            var parent = active ? CharacterTestTacticalCompositionEdits.Nodes(_session.Document.Root)
                .FirstOrDefault(p => p.Children?.Any(c => c.NodeId == node.NodeId) == true) : null;
            int index = parent?.Children.FindIndex(c => c.NodeId == node.NodeId) ?? -1;
            for (int side = 0; side < 2; side++)
            {
                string name = side == 0 ? "moveSharedLeft" : "moveSharedRight";
                var button = row.GetChild(name)?.asButton;
                if (!active) { if (button != null) button.visible = false; continue; }
                if (button == null)
                {
                    button = Fgui.UIPackage.CreateObjectFromURL(Control("btnGroup").resourceURL)?.asButton;
                    if (button == null) continue;
                    button.name = name; row.AddChild(button);
                }
                int direction = side == 0 ? -1 : 1;
                bool canMove = _session.CanEdit && index >= 0 && index + direction >= 0
                    && index + direction < parent.Children.Count;
                button.visible = true; button.enabled = canMove; button.icon = string.Empty;
                button.title = side == 0 ? "←" : "→"; button.titleFontSize = 16;
                button.SetXY(row.width * .5f - 76 + side * 52, 52); button.SetSize(48, 24);
                button.tooltips = !_session.CanEdit ? "当前只读" : canMove
                    ? side == 0 ? "向左移动一项" : "向右移动一项"
                    : side == 0 ? "已在当前组最左侧" : "已在当前组最右侧";
                var token = _session.Token; string id = node.NodeId;
                button.onClick.Set(context => {
                    context.StopPropagation();
                    if (!canMove) return;
                    if (_session.Edit(token, "移动封装", source => CharacterTestTacticalCompositionEdits.Move(source, id, direction)))
                        _session.Select(id, CharacterTestTacticalEditTarget.SharedDefinition);
                });
            }
            var add = row.GetChild("appendShared")?.asButton;
            if (active)
            {
                if (add == null)
                {
                    add = Fgui.UIPackage.CreateObjectFromURL(Control("btnInsert").resourceURL)?.asButton;
                    if (add != null) { add.name = "appendShared"; row.AddChild(add); }
                }
                if (add != null)
                {
                    add.visible = true; add.enabled = _session.CanEdit;
                    add.icon = string.Empty; add.title = "+"; add.titleFontSize = 18;
                    add.SetXY(row.width * .5f + 28, 52); add.SetSize(48, 24);
                    add.tooltips = _session.CanEdit ? "在此封装右侧添加" : "当前只读";
                    var anchor = add; string target = node.NodeId;
                    add.onClick.Set(context => {
                        context.StopPropagation();
                        if (_session.CanEdit) ShowInsertAt(anchor, target);
                    });
                }
            }
            else if (add != null) add.visible = false;
            if (active && row.GetChild("title") is Fgui.GTextField title)
            { title.y = 19; title.height = 30; }
        }

        private void RefreshFlattenedItems(CharacterTestTacticalDslSource document)
        {
            if (ReferenceEquals(_itemsDocument, document))
                return;
            _itemsDocument = document;
            _items.Clear();
            WriteFlattenedItems(document?.Root);
        }

        private void WriteFlattenedItems(CharacterTestTacticalDslNode node)
        {
            if (node == null)
                return;
            _items.Add((node, false));
            if (node.Children != null)
                foreach (var child in node.Children)
                    WriteFlattenedItems(child);
            if (node.Children != null)
                _items.Add((node, true));
        }

        private void HandleCompositionRowClick(Fgui.EventContext context)
        {
            if (!(context?.sender is Fgui.GComponent row) || !(row.data is int index)
                || index < 0 || index >= _items.Count)
                return;
            var item = _items[index];
            _session.Select(
                item.node.NodeId,
                item.node.Kind == "Reference" && context.inputEvent?.ctrlOrCmd != true
                    ? CharacterTestTacticalEditTarget.SharedDefinition
                    : CharacterTestTacticalEditTarget.Composition,
                additive: context.inputEvent?.ctrlOrCmd == true);
        }

        private void HandleCompositionRowRightClick(Fgui.EventContext context)
        {
            if (!(context?.sender is Fgui.GComponent row) || !(row.data is int index)
                || index < 0 || index >= _items.Count)
                return;
            var node = _items[index].node;
            _session.Select(
                node.NodeId,
                node.Kind == "Reference"
                    ? CharacterTestTacticalEditTarget.SharedDefinition
                    : CharacterTestTacticalEditTarget.Composition);
        }
        private void BindCardName()
        {
            var input = _panel.parent?.GetChild("inputProjectCardDisplayName")?.asTextInput;
            if (input == null) return;
            if (_nameLease.HasValue && !_session.Accepts(_nameLease.Value)) { _nameLease = null; input.text = _session.Document.Name; }
            input.enabled = input.editable = input.touchable = _session.CanEdit;
            input.grayed = !_session.CanEdit; input.restrict = null; input.maxLength = 128;
            input.promptText = "战术卡名称";
            input.tooltips = _session.Frozen != null ? "冻结版本的卡名称，只读" : "修改当前卡名称，不改变共享封装名称";
            if (!input.focused) input.text = _session.Document.Name;
            input.onFocusIn.Set(() => { if (!_nameLease.HasValue) { _nameLease = _session.Token; _nameBefore = input.text; } });
            void Commit()
            {
                if (!_nameLease.HasValue) return;
                var token = _nameLease.Value; _nameLease = null;
                if (_nameBefore != input.text) _session.RenameCard(token, input.text);
            }
            input.onFocusOut.Set(() => {
                if (CharacterTestTacticalInputNavigation.IsContextNavigationPointer())
                { _nameLease = null; input.text = _session.Document.Name; return; }
                Commit();
            });
            input.onSubmit.Set(Commit);
        }
        private static string KindTitle(string kind, string name = null)
            => kind == "Reference" ? name ?? "缺失封装" : kind == "Sequence" ? "顺序组" : kind == "If" ? "若条件成立"
                : kind == "Wait" ? "等待" : kind == "Exit" ? "退出整卡" : "未选择";
        private void Button(string name, string title, bool enabled, Action action)
        {
            var button = Control(name)?.asButton;
            if (button == null) return;
            button.visible = true; button.enabled = enabled; button.title = title; button.titleFontSize = 12;
            button.onClick.Set(() => action());
        }
        private void ShowInsert()
        {
            ShowInsertAt(Control("btnInsert"), _session.SelectedNodeId);
        }

        private void ShowInsertAt(Fgui.GObject anchor, string targetNodeId)
        {
            var token = _session.Token; var options = new List<(string, Action)>();
            foreach (var definition in _session.Document.Definitions)
            {
                string id = definition.DefinitionId;
                options.Add((definition.Source.Name, () => Insert(token, "Reference", id, targetNodeId)));
            }
            options.Add(("＋ 新建空白封装", () => _session.CreateDefinition(token, targetNodeId)));
            ShowChoices(options, anchor: anchor, menuWidth: 300f);
        }
        private void Insert(CharacterTestTacticalEditToken token, string kind, string definition = null, string targetNodeId = null)
        {
            string insertedId = null;
            bool changed = _session.Edit(token, "添加编排项", source => {
                var selected = CharacterTestTacticalCompositionEdits.Find(source, targetNodeId ?? token.NodeId);
                var parent = selected.Children != null ? selected : CharacterTestTacticalCompositionEdits.Parent(source, selected.NodeId);
                int index = ReferenceEquals(selected, parent) ? parent.Children.Count : parent.Children.IndexOf(selected) + 1;
                var node = CharacterTestTacticalCompositionEdits.NewNode(kind, definition);
                insertedId = node.NodeId;
                CharacterTestTacticalCompositionEdits.Insert(source, parent.NodeId, index, node);
            });
            if (changed && insertedId != null) _session.Select(insertedId, kind == "Reference"
                ? CharacterTestTacticalEditTarget.SharedDefinition : CharacterTestTacticalEditTarget.Composition);
        }
        private void ShowOperations()
        {
            if (!_session.CanEdit) { ShowRuntimeSources(); return; }
            var token = _session.Token; string[] selection = _session.Selection.ToArray();
            var operations = new List<(string, Action)> {
                ("【移动】← 在当前组内移动", () => _session.Edit(token, "移动", s => CharacterTestTacticalCompositionEdits.Move(s, token.NodeId, -1))),
                ("【移动】在当前组内移动 →", () => _session.Edit(token, "移动", s => CharacterTestTacticalCompositionEdits.Move(s, token.NodeId, 1))),
                ("【成组】选中内容成组（Ctrl 多选）", () => _session.Edit(token, "成组", s => CharacterTestTacticalCompositionEdits.Group(s, selection, "Sequence"))),
                ("【成组】为选中内容添加入口条件组", () => _session.Edit(token, "条件成组", s => CharacterTestTacticalCompositionEdits.Group(s, selection, "If"))),
                ("【成组】解组（先移除组条件）", () => _session.Edit(token, "解组", s => CharacterTestTacticalCompositionEdits.Ungroup(s, token.NodeId))),
                ("【条件】编辑入口／等待条件", () => _session.Select(token.NodeId, CharacterTestTacticalEditTarget.NodeCondition)),
                ("【条件】编辑持续局部结束条件", () => _session.Select(token.NodeId, CharacterTestTacticalEditTarget.NodeEnd)),
                ("【条件】移动到位时结束此范围…", () => ShowMovementEnd(token, false)),
                ("【条件】移动目标失效时结束此范围…", () => ShowMovementEnd(token, true)),
                ("【条件】动作提交失败时结束此范围…", () => ShowCommitFailureEnd(token)),
                ("【条件】移除当前入口／等待条件", () => _session.Edit(token, "移除条件", s => CharacterTestTacticalCompositionEdits.SetCondition(s, CharacterTestTacticalEditTarget.NodeCondition, token.NodeId, null))),
                ("【条件】移除当前局部结束条件", () => _session.Edit(token, "移除局部结束", s => CharacterTestTacticalCompositionEdits.SetCondition(s, CharacterTestTacticalEditTarget.NodeEnd, token.NodeId, null))),
                ("【封装】另存封装并替换当前引用", () => _session.DuplicateDefinition(token)),
                ("【封装】替换这次使用的封装／修复引用", () => ShowReplacement(token)),
                ("【整卡】另存为测试单卡", _session.CopyCard),
                ("【整卡】删除所选项及其内容", () => _session.Edit(token, "删除编排项", s => CharacterTestTacticalCompositionEdits.Delete(s, token.NodeId))),
                ("【历史】撤销", () => _session.Undo(false)),
                ("【历史】重做", () => _session.Undo(true)),
                ("【历史】定位当前运行来源／查看冻结版本", ShowRuntimeSources) };
            if (_session.Controller.IsConfiguredTestCardEditing)
            {
                operations.Add(("【测试卡】重命名", () => _session.Controller.ShowTestCardCommand?.Invoke("rename")));
                operations.Add(("【测试卡】重新载入库版本…", () => _session.Controller.ShowTestCardCommand?.Invoke("reload")));
                operations.Add(("【测试卡】删除当前测试卡…", () => _session.Controller.ShowTestCardCommand?.Invoke("delete")));
            }
            ShowChoices(operations);
        }
        private void ShowRuntimeSources()
        {
            var choices = _session.RuntimeTargets.Select(t => (t.Label, (Action)(() => _session.InspectRuntimeSource(t)))).ToList();
            choices.Add(("返回当前制作（恢复最新帧）", _session.ReturnToCurrentSource));
            ShowChoices(choices, requireEdit: false);
        }
        private void ShowCommitFailureEnd(CharacterTestTacticalEditToken token)
        {
            var scope = _session.SelectedNode;
            if (scope == null || (scope.Kind != "Reference" && scope.Kind != "Sequence"))
            { _session.Feedback("请选择共享封装或顺序组，明确失败后要结束的范围。"); return; }
            var choices = new List<(string, Action)>();
            foreach (var use in CharacterTestTacticalCompositionEdits.Nodes(scope).Where(n => n.Kind == "Reference"))
            {
                var definition = _session.Document.Definitions.FirstOrDefault(d => d.DefinitionId == use.DefinitionId);
                if (definition == null) continue;
                foreach (var evt in definition.Source.Timelines.SelectMany(t => t.Events).Where(e => e.Operation.Kind == "CharacterAction"))
                {
                    string reference = use.NodeId + "/" + evt.EventId;
                    choices.Add((definition.Source.Name + " / " + evt.EventId + " 提交失败 → 结束所选范围", () =>
                        _session.Edit(token, "配置动作提交失败后继", source => CharacterTestTacticalCompositionEdits.SetCondition(source,
                            CharacterTestTacticalEditTarget.NodeEnd, token.NodeId, new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {
                                NodeId = "commit-failed-" + Guid.NewGuid().ToString("N"), Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                                SourceEventId = reference, Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed }))));
                }
            }
            if (choices.Count == 0) { _session.Feedback("所选范围中没有实际出招事件。"); return; }
            ShowChoices(choices);
        }
        private void ShowReplacement(CharacterTestTacticalEditToken token)
        {
            if (!_session.Accepts(token) || _session.SelectedNode?.Kind != "Reference")
            { _session.Feedback("请先选择需要替换的封装引用。"); return; }
            ShowChoices(_session.Document.Definitions.Select(d => (d.Source.Name + " · 版本 " + d.Revision, (Action)(() =>
                _session.Edit(token, "替换封装引用", source => CharacterTestTacticalCompositionEdits.Find(source, token.NodeId).DefinitionId = d.DefinitionId)))).ToList());
        }
        internal void ShowTestActionPicker(Fgui.GObject anchor)
        {
            if (_session?.CanEdit != true) return;
            var controller = _session.Controller;
            var choices = new List<(string, Action)>();
            var locations = controller.TacticalActionLocations(_session.CardId);
            void ChooseAction(CharacterTestTacticalActionLocation location)
            {
                var token = _session.Token;
                _actionLibraryPicker ??= new CharacterTestFairyGuiTacticalActionPicker(_viewId, _popupService);
                _actionLibraryPicker.Show(controller, location.ActionId, anchor, true,
                    () => _session?.CanEdit == true && _session.AcceptsView(token), selected => {
                        if (selected == "$default") controller.ResetTacticalPreviewAction(_session.CardId, location.Path);
                        else controller.SetTacticalPreviewSlot(_session.CardId, location.Path, selected);
                        _session.Feedback(controller.StatusMessage);
                    });
            }
            if (locations.Length == 1) { ChooseAction(locations[0]); return; }
            foreach (var location in locations)
                choices.Add((location.Label + " → " + location.ActionName, () => ChooseAction(location)));
            if (choices.Count == 0) choices.Add(("本卡没有需要配置的动作", () => { }));
            ShowChoices(choices, requireEdit: false, anchor: anchor);
        }

        private void ShowChoices(List<(string label, Action action)> options, bool requireEdit = true, Fgui.GObject anchor = null,
            float menuWidth = 440f)
        {
            _session.DetailsOpen = false; _detailsKey = null;
            _choicesRequireEdit = requireEdit;
            var list = _choiceList; _choiceToken = _session.Token;
            list.SetSize(menuWidth, 300);
            list.RemoveChildrenToPool(); _choices.Clear();
            foreach (var option in options)
            {
                var row = list.AddItemFromPool().asCom; int index = _choices.Count; _choices.Add(option.action);
                row.GetChild("title").text = option.label; row.GetChild("detail").text = ""; row.data = index;
                row.SetSize(list.width - 18, 36);
                row.opaque = true; row.tooltips = option.label;
                row.GetChild("title").SetSize(row.width - 24, 36);
                row.GetChild("title").touchable = false;
                row.GetChild("background").SetXY(0, 0); row.GetChild("background").SetSize(row.width, 36);
                row.GetChild("title").SetXY(8, 4); row.GetChild("title").SetSize(row.width - 16, 28);
                foreach (string decoration in new[] { "detail", "groupTop", "groupBottom", "groupEdge" })
                    if (row.GetChild(decoration) is Fgui.GObject child) child.visible = false;
                row.onClick.Set(() => {
                    var token = _choiceToken;
                    bool requiresWrite = _choicesRequireEdit;
                    Action action = index >= 0 && index < _choices.Count ? _choices[index] : null;
                    if (_popupService != null) _popupService.Hide(list, MxFairyGuiPopupCloseReason.Toggle);
                    else Fgui.GRoot.inst.HidePopup(list);
                    list.visible = false; _choices.Clear();
                    if (action != null && (requiresWrite ? _session.Accepts(token) : _session.AcceptsView(token))) action();
                    else _session.Feedback("选项上下文已变化。");
                });
            }
            float desiredHeight = Mathf.Clamp(options.Count * 40f + 8f, 80f, 400f);
            list.height = desiredHeight;
            list.visible = true; list.sortingOrder = 10;
            list.onKeyDown.Set(context => {
                if (context.inputEvent?.keyCode != KeyCode.Escape) return;
                if (_popupService != null) _popupService.Hide(list, MxFairyGuiPopupCloseReason.Cancel);
                else Fgui.GRoot.inst.HidePopup(list);
                list.visible = false; _choices.Clear(); context.StopPropagation();
            });
            if (_popupService == null || !_popupService.Show(_viewId, list, anchor ?? Control("btnGroup"),
                new MxFairyGuiPopupOptions(MxFairyGuiPopupPlacement.Auto, safeMargin: 12f, anchorGap: 4f)))
                Fgui.GRoot.inst.ShowPopup(list, anchor ?? Control("btnGroup"));
            list.focusable = true;
            list.RequestFocus();
        }
        private void ShowDescriptionDetails()
        {
            var list = _panel.GetChild("choiceList").asList;
            string text = _session.DescriptionText + "\n\n" + _session.DescriptionDetails + "\n\n" + _session.DescriptionWarnings;
            string key = _session.CardId + "|" + list.width + "|" + text;
            if (_detailsKey != key)
            {
                _detailsKey = key; _choices.Clear(); list.RemoveChildrenToPool();
                foreach (string paragraph in text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var row = list.AddItemFromPool().asCom; row.width = list.width - 18;
                    var title = row.GetChild("title").asTextField; title.SetSize(row.width - 16, 24); title.text = paragraph;
                    float height = Mathf.Max(38, title.textHeight + 16); title.height = height - 12; row.height = height;
                    row.GetChild("background").SetSize(row.width, height); row.GetChild("detail").text = "";
                    row.onClick.Clear(); row.onRightClick.Clear();
                }
            }
            list.visible = true; list.sortingOrder = 10;
            _panel.GetChild("compositionTitle").text = "自动说明详情 · 滚动阅读 · 点击“收起说明”返回编排";
        }
        internal float Arrange(float width)
        {
            if (_panel == null || _session == null) return 0;
            int columns = Math.Max(2, Math.Min(6, (int)((width - 16) / 102)));
            var buttons = CharacterTestTacticalPreviewControlContract.CompositionButtons.Select(n => _panel.GetChild(n)).Where(b => b.visible).ToArray();
            for (int i = 0; i < buttons.Length; i++) { buttons[i].SetXY(8 + (i % columns) * (width - 16) / columns, 32 + (i / columns) * 34); buttons[i].SetSize((width - 16) / columns - 4, 30); }
            float listY = 32 + Mathf.CeilToInt(buttons.Length / (float)columns) * 34;
            var list = _panel.GetChild("compositionList"); list.SetXY(8, listY); list.SetSize(width - 16, 74);
            var impact = _panel.GetChild("sharedImpactText"); impact.SetXY(8, listY + 80); impact.SetSize(width - 16, 44);
            var chooser = _panel.GetChild("choiceList"); chooser.SetXY(8, listY);
            float chooserHeight = Mathf.Clamp(chooser.height > 120f ? chooser.height : 280f, 120f, 380f);
            chooser.SetSize(width - 16, chooserHeight);
            _panel.GetChild("compositionTitle").SetSize(width - 16, 26);
            if (_session.DetailsOpen) ShowDescriptionDetails();
            float height = listY + 132; _panel.SetSize(width, height); _panel.GetChild("background").SetSize(width, height);
            return height;
        }
    }
}
