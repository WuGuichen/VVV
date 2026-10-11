using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalLibraryBrowser
    {
        private readonly HashSet<string> _expandedFaces = new(), _faceDetails = new();
        private CharacterTestFairyGuiTacticalActionPicker _faceActionPicker;
        private string _faceRenderKey, _renamingFace, _renameText, _renameToken, _renameRole, _scrollToFace;
        private string _faceFilterKey;

        private void RenderConfiguredFaces()
        {
            var c = _sink.LibraryController;
            var all = c.ConfiguredCardFaces();
            string query = c.TacticalLibraryQuery ?? "";
            string filterKey = c.TestCardRole + ":" + c.TestCardLibraryFilter + ":" + query;
            string key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(all) + ":" + filterKey
                + ":" + _root.width + ":" + _root.height + ":" + c.TacticalLibrarySelectedKey + ":" + _notice
                + ":" + c.TacticalCardTestMode + ":" + c.CanChooseTacticalTestCard + ":" + c.IsHistoricalFrameSelected;
            if (_faceRenderKey == key) return;
            _faceRenderKey = key;
            var rows = all.Where(f => c.TestCardLibraryFilter == "all" || (c.TestCardLibraryFilter == "shared" ? f.Owner == "共用" : f.Owner != "共用"))
                .Where(f => query.Length == 0 || (f.Name + " " + f.Description + " " + string.Join(" ", f.Actions.Select(a => a.ActionName)))
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            var list = _page.GetChild("configuredCards")?.asList;
            if (list == null) throw new InvalidOperationException("测试卡面发布资源缺失，请重新发布 Runtime AI Lab UI。");
            foreach (var child in _page.GetChildren()) child.visible = false;
            float width = _root.width, height = _root.height;
            _page.SetXY(0, 0); _page.SetSize(width, height);
            void Text(string name, string value, float x, float y, float w, float h)
            { var item = _page.GetChild(name); item.visible = true; item.text = value; Place(_page, name, x, y, w, h); }
            _page.GetChild("background").visible = true; Place(_page, "background", 0, 0, width, height);
            Text("libraryTitle", "测试战术卡库", 24, 18, 220, 38);
            Text("librarySummary", c.TacticalPreviewRoleLabel + " · " + rows.Length + " 张" + (string.IsNullOrEmpty(_notice) ? "" : " · " + _notice),
                24, 108, width - 48, 24);
            var search = _page.GetChild("libraryQuery").asTextInput;
            search.visible = true; search.promptText = "搜索卡名、行为或动作"; if (search.text != query) search.text = query;
            Place(_page, "libraryQuery", Mathf.Max(260, width - 510), 20, Mathf.Min(350, width - 430), 36);
            search.onChanged.Set(() => { c.TacticalLibraryQuery = search.text; Render(); });
            var back = _page.GetChild("btnLibraryFilter").asButton;
            back.visible = true; back.enabled = true; back.title = "返回";
            Place(_page, "btnLibraryFilter", width - 140, 18, 116, 38);
            back.onClick.Set(_sink.CloseTacticalLibrary);
            BindConfiguredLibraryTools(width);
            if (rows.Length == 0) Text("libraryEmpty", all.Length == 0 ? "从基础卡创建第一张测试卡，默认配置会自动填入。" : "没有匹配的测试卡。", 24, 156, width - 48, 50);

            float scroll = filterKey == _faceFilterKey ? list.scrollPane.posY : 0;
            _faceFilterKey = filterKey;
            list.visible = true; Place(_page, "configuredCards", 24, 144, width - 48, Mathf.Max(160, height - 164));
            list.layout = Fgui.ListLayoutType.FlowHorizontal; list.autoResizeItem = false;
            list.columnGap = 16; list.lineGap = 16;
            int columns = Mathf.Max(1, Mathf.FloorToInt((list.width - 16) / 286));
            float cardWidth = Mathf.Floor((list.width - 16 - (columns - 1) * 16) / columns);
            list.itemRenderer = (index, item) => BindConfiguredFace(item.asCom, rows[index], cardWidth);
            list.numItems = rows.Length;
            list.EnsureBoundsCorrect(); list.scrollPane.SetPosY(scroll, false);
            list.scrollPane.onScroll.Set(() => { var menu = _page.GetChild("faceMenu"); if (menu != null) menu.visible = false; });
            if (_scrollToFace != null)
            {
                int index = Array.FindIndex(rows, r => r.Id == _scrollToFace);
                if (index >= 0) list.ScrollToView(index, false);
                _scrollToFace = null;
            }
        }

        private void BindConfiguredFace(Fgui.GComponent card, CharacterTestConfiguredCardFace face, float width)
        {
            var c = _sink.LibraryController;
            string role = c.TestCardRole;
            bool editable = !c.IsHistoricalFrameSelected && face.Token != null;
            bool selected = c.TacticalLibrarySelectedKey == "test:" + face.Id;
            bool expanded = _expandedFaces.Contains(face.Id);
            int actionCount = expanded ? face.Actions.Length : Math.Min(2, face.Actions.Length);
            bool details = _faceDetails.Contains(face.Id);
            float actionsHeight = actionCount * 64;
            float readinessY = 207 + actionsHeight + (face.Actions.Length > 2 ? 34 : 0) + 4;
            float readinessHeight = string.IsNullOrEmpty(face.Issue) ? 22 : 48;
            float bottom = readinessY + readinessHeight + 8;
            float detailsHeight = 0;
            var detail = card.GetChild("details").asTextField;
            detail.textFormat.size = 13;
            detail.text = face.Details; detail.singleLine = false; detail.autoSize = Fgui.AutoSizeType.Height;
            Place(card, "details", 16, bottom, width - 32, 24); detail.visible = details;
            if (details) detailsHeight = detail.height + 12;
            bottom += detailsHeight;
            float previewY = bottom;
            bottom += 38;
            card.opaque = true;
            card.SetSize(width, bottom + 50);
            Place(card, "background", 0, 0, width, card.height);
            var background = card.GetChild("background").asGraph;
            background.DrawRoundRect(width, card.height,
                selected ? new Color32(17, 49, 54, 255) : new Color32(12, 27, 38, 255), new[] { 12f, 12f, 12f, 12f });
            background.touchable = false;
            Place(card, "accent", 16, 12, width - 32, 2);
            Place(card, "owner", 16, 22, 95, 22); card.GetChild("owner").text = face.Owner;
            Place(card, "state", 114, 22, width - 168, 22); card.GetChild("state").text = face.Dirty ? "未保存" : "已保存";
            Place(card, "icon", (width - 52) / 2, 49, 52, 52);
            Place(card, "title", 16, 111, width - 90, 32); card.GetChild("title").text = face.Name;
            card.GetChild("title").tooltips = face.Name;
            var title = card.GetChild("title").asTextField; title.autoSize = Fgui.AutoSizeType.Shrink;
            Place(card, "description", 16, 151, width - 32, 46);
            var description = card.GetChild("description").asTextField;
            description.text = face.Description ?? ""; description.singleLine = false;
            description.autoSize = Fgui.AutoSizeType.Shrink; description.tooltips = face.Description;
            Place(card, "actions", 16, 207, width - 32, actionsHeight);
            var actions = card.GetChild("actions").asList;
            actions.visible = actionCount > 0; actions.autoResizeItem = false; actions.lineGap = 4;
            if (actions.scrollPane != null) { actions.scrollPane.mouseWheelEnabled = false; actions.scrollPane.touchEffect = false; }
            actions.itemRenderer = (index, item) => {
                var location = face.Actions[index]; var row = item.asCom;
                row.SetSize(width - 32, 60);
                Place(row, "eventLabel", 0, 0, width - 32, 20);
                row.GetChild("eventLabel").text = location.Label; row.GetChild("eventLabel").tooltips = location.Label;
                Place(row, "btnAction", 0, 23, width - 32, 34);
                var button = row.GetChild("btnAction").asButton;
                button.title = location.ActionName + "  ▾"; button.enabled = editable; button.tooltips = location.ActionName;
                var caption = button.GetTextField();
                if (caption != null) { caption.textFormat.size = 14; caption.autoSize = Fgui.AutoSizeType.Shrink; }
                button.onClick.Set(context => { context.StopPropagation(); OpenFaceAction(face, location, role, button); });
            };
            actions.numItems = actionCount;
            float y = 207 + actionsHeight;
            FaceButton(card, "btnExpand", expanded ? "收起动作" : "展开全部 " + face.Actions.Length + " 项动作", 16, y, width - 32, 28,
                face.Actions.Length > 2, true, () => { if (!_expandedFaces.Add(face.Id)) _expandedFaces.Remove(face.Id); RefreshFaces(); });
            if (face.Actions.Length > 2) y += 34;
            Place(card, "readiness", 16, readinessY, width - 32, readinessHeight);
            var readiness = card.GetChild("readiness").asTextField;
            readiness.textFormat.size = 14;
            readiness.singleLine = false;
            readiness.autoSize = Fgui.AutoSizeType.None;
            readiness.text = string.IsNullOrEmpty(face.Issue) ? "" : "不可用：" + FaceIssueHeadline(face.Issue) + "\n点此查看处理方式 ›";
            readiness.tooltips = face.Details;
            // FairyGUI text fields do not receive clicks. The opaque card handles this row below.
            readiness.touchable = false;
            FaceButton(card, "btnSave", "保存", 16, bottom, 64, 32, true, editable && face.Dirty,
                () => { c.SaveConfiguredCardFace(face.Id, role, out _notice); RefreshFaces(); });
            if (card.GetChild("btnPreviewUi") != null)
                FaceButton(card, "btnPreviewUi", "预览卡面", 16, previewY, width - 32, 30, true, face.Token != null,
                    () => ShowCardUiPreview(face.Id));
            FaceButton(card, "btnEdit", "编辑行为", 88, bottom, 92, 32, true, editable && face.BehaviorWorkspaceAvailable && c.CanChooseTacticalTestCard,
                () => _sink.OpenTacticalLibraryCard("test:" + face.Id, selectForTest: false));
            FaceButton(card, "btnUse", "选用测试", 188, bottom, width - 204, 32, c.TacticalCardTestMode,
                editable && face.BehaviorWorkspaceAvailable && c.CanChooseTacticalTestCard, () => _sink.OpenTacticalLibraryCard("test:" + face.Id, editBehavior: false));
            if (!face.BehaviorWorkspaceAvailable)
            {
                card.GetChild("btnEdit").tooltips = face.Issue;
                card.GetChild("btnUse").tooltips = face.Issue;
            }
            FaceButton(card, "btnMore", "更多", width - 68, 18, 52, 26, true, true,
                () => ShowFaceMenu(face, role, card.GetChild("btnMore")));
            FaceButton(card, "btnRename", "改名", width - 66, 112, 50, 28, true, editable,
                () => BeginFaceRename(card, face, role));
            bool renaming = _renamingFace == face.Id;
            var input = card.GetChild("nameInput").asTextInput;
            Place(card, "nameInput", 16, 111, width - 90, 32); input.visible = renaming; title.visible = !renaming;
            if (renaming && input.text != _renameText) input.text = _renameText;
            input.onChanged.Set(() => { if (_renamingFace == face.Id) _renameText = input.text; });
            input.onKeyDown.Set(context => {
                var key = context.inputEvent?.keyCode;
                if (key != KeyCode.Return && key != KeyCode.KeypadEnter && key != KeyCode.Escape) return;
                context.StopPropagation();
                if (key == KeyCode.Escape) { _renamingFace = null; RefreshFaces(); return; }
                if (c.EditConfiguredCardFace(face.Id, _renameRole, _renameToken, input.text, null, null, out _notice)) _renamingFace = null;
                RefreshFaces();
            });
            card.onClick.Set(context => {
                Vector2 point = card.GlobalToLocal(context.inputEvent.position);
                if (!string.IsNullOrEmpty(face.Issue)
                    && point.x >= readiness.x && point.x <= readiness.x + readiness.width
                    && point.y >= readiness.y - 4 && point.y <= readiness.y + readiness.height + 4)
                {
                    ToggleFaceDetails(face.Id);
                    context.StopPropagation();
                    return;
                }
                c.TacticalLibrarySelectedKey = "test:" + face.Id;
                RefreshFaces();
            });
            card.onRightClick.Clear();
        }

        private static void FaceButton(Fgui.GComponent parent, string name, string title, float x, float y, float width, float height,
            bool visible, bool enabled, Action click)
        {
            var button = parent.GetChild(name).asButton;
            Place(parent, name, x, y, width, height); button.visible = visible; button.enabled = enabled;
            button.title = title;
            var caption = button.GetTextField();
            if (caption != null) { caption.textFormat.size = 13; caption.autoSize = Fgui.AutoSizeType.Shrink; }
            button.onClick.Set(context => { context.StopPropagation(); click(); });
        }

        private void BeginFaceRename(Fgui.GComponent card, CharacterTestConfiguredCardFace face, string role)
        {
            _renamingFace = face.Id; _renameText = face.Name; _renameToken = face.Token; _renameRole = role;
            RefreshFaces();
            var input = card.GetChild("nameInput").asTextInput;
            input.RequestFocus(); input.SetSelection(0, input.text.Length);
        }

        private void OpenFaceAction(CharacterTestConfiguredCardFace face, CharacterTestTacticalActionLocation location, string role, Fgui.GObject anchor)
        {
            var c = _sink.LibraryController;
            _faceActionPicker ??= new CharacterTestFairyGuiTacticalActionPicker(_configuredView, _configuredPopups);
            _faceActionPicker.Show(c, location.ActionId, anchor, false,
                () => _page?.visible == true && c.TestCardRole == role && !c.IsHistoricalFrameSelected,
                action => { c.EditConfiguredCardFace(face.Id, role, face.Token, null, location.Path, action, out _notice); RefreshFaces(); });
        }

        private void ShowFaceMenu(CharacterTestConfiguredCardFace face, string role, Fgui.GObject anchor)
        {
            var menu = _page.GetChild("faceMenu").asList;
            menu.RemoveChildrenToPool(); menu.visible = true; menu.sortingOrder = 100;
            void Add(string label, Action click)
            {
                var b = menu.AddItemFromPool().asButton; b.title = label; b.SetSize(180, 32);
                b.onClick.Set(context => { context.StopPropagation(); menu.visible = false; click(); });
            }
            Add(_faceDetails.Contains(face.Id) ? "收起详情" : "查看详情", () => ToggleFaceDetails(face.Id));
            if (!_sink.LibraryController.IsHistoricalFrameSelected)
            {
                Add("另存为…", () => ShowConfiguredDialog("test:" + face.Id, true));
                Add("删除…", () => ShowFaceDelete(face, role));
            }
            menu.ResizeToFit();
            Vector2 position = _page.GlobalToLocal(anchor.LocalToGlobal(new Vector2(0, anchor.height)));
            menu.SetXY(Mathf.Clamp(position.x, 12, _page.width - menu.width - 12), Mathf.Clamp(position.y, 12, _page.height - menu.height - 12));
        }

        private void ShowFaceDelete(CharacterTestConfiguredCardFace face, string role)
        {
            var c = _sink.LibraryController;
            var expected = c.ConfiguredCards().FirstOrDefault(card => card.Id == face.Id);
            ShowConfiguredForm("test:" + face.Id, true, false);
            _configuredDialog.GetChild("title").text = "删除测试卡";
            _configuredDialog.GetChild("nameInput").visible = false; _configuredDialog.GetChild("nameLabel").visible = false;
            _configuredDialog.GetChild("btnDestination").visible = false;
            _configuredDialog.GetChild("notice").text = "删除“" + face.Name + "”？未保存修改也会移除。";
            var confirm = _configuredDialog.GetChild("btnConfirm").asButton; confirm.title = "删除";
            confirm.onClick.Set(() => {
                if (c.TestCardRole != role || expected == null) { _configuredDialog.GetChild("notice").text = "角色或卡片已变化。"; return; }
                if (!c.DeleteConfiguredCard(face.Id, out _notice, expected.Revision, expected.FileHash)) { _configuredDialog.GetChild("notice").text = _notice; return; }
                CloseConfiguredDialog(); RefreshFaces();
            });
        }

        private void RefreshFaces() { _faceRenderKey = _renderKey = null; Render(); }

        private void ShowCardUiPreview(string id)
        {
            var controller = _sink.LibraryController;
            if (!controller.TryGetConfiguredCardUiPreview(id, out var data, out _notice)) { RefreshFaces(); return; }
            try
            {
                if (_cardUiPreview?.IsDisposed == true) _cardUiPreview = null;
                _cardUiPreview ??= new CharacterTestFairyGuiCardUiPreview(_root, _configuredPopups, _configuredView);
                _cardUiPreview.Show(data, controller.TestCardRole, controller);
            }
            catch (InvalidOperationException error) { _notice = error.Message; RefreshFaces(); }
        }

        private void ToggleFaceDetails(string id)
        {
            if (!_faceDetails.Add(id)) _faceDetails.Remove(id);
            RefreshFaces();
        }

        private static string FaceIssueHeadline(string issue)
        {
            if (issue.StartsWith("独立机制试用卡", StringComparison.Ordinal)) return "独立机制卡，未接入意图编排";
            const string full = "武器轨迹数据缺失或过期";
            return issue.StartsWith(full, StringComparison.Ordinal)
                ? "武器轨迹缺失/过期" + issue.Substring(full.Length)
                : issue;
        }
    }
}
