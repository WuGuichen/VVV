using System;
using System.Linq;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalLibraryBrowser
    {
        private IMxFairyGuiPopupService _configuredPopups;
        private MxUiViewId _configuredView;
        private Fgui.GComponent _configuredDialog;
        private string _dialogKey, _dialogRole;
        private string _dialogWorkspace;
        private int _dialogRevision;
        private bool _dialogCopy, _dialogRename, _dialogShared;

        private void BindConfiguredLibraryTools(float width)
        {
            var c = _sink.LibraryController;
            void Button(string name, string title, float x, float w, Action clicked, bool visible = true)
            {
                var b = _page.GetChild(name)?.asButton; if (b == null) return;
                b.visible = visible; b.title = title; b.onClick.Set(() => clicked()); Place(_page, name, x, 72, w, 34);
            }
            void Filter(string value) { c.TestCardLibraryFilter = value; c.TacticalLibraryPage = 0; _renderKey = null; Render(); }
            bool tests = c.TestCardLibraryMode && !c.TestCardTemplatePicking;
            Button("btnTestAll", "全部", 24, 88, () => Filter("all"), tests);
            Button("btnTestRole", "当前角色", 122, 126, () => Filter("role"), tests);
            Button("btnTestShared", "共用", 258, 88, () => Filter("shared"), tests);
            Button("btnTestCreate", c.TestCardTemplatePicking ? "返回测试库" : "从基础卡创建", 362, 174, () => {
                c.TestCardTemplatePicking = !c.TestCardTemplatePicking; c.TestCardLibraryMode = true;
                c.TacticalLibraryQuery = ""; c.TacticalLibraryPage = 0; _renderKey = null; Render();
            }, c.TestCardLibraryMode);
            Button("btnBaseCreate", "添加战术卡", 24, 150, () => {
                if (!c.TryCreateTacticalLibraryCard(out string key, out string issue))
                { c.TacticalLibraryIssue = issue; _renderKey = null; Render(); return; }
                _notice = issue;
                if (!_sink.OpenTacticalLibraryCard(key))
                { c.TacticalLibraryIssue = _sink.LastStatusMessage; _renderKey = null; Render(); }
            }, !c.TestCardLibraryMode);
            Button("btnTestLibrary", "测试卡库", 186, 150, () => {
                c.TestCardLibraryMode = true; c.TestCardTemplatePicking = false; c.TacticalLibraryQuery = ""; _renderKey = null; Render();
            }, !c.TestCardLibraryMode);
        }

        private void ShowConfiguredDialog(string key, bool copy)
        {
            ShowConfiguredForm(key, copy, false);
        }
        private void ShowConfiguredRename(string key)
        {
            if (!_sink.OpenTacticalLibraryCard(key)) { _notice = _sink.LastStatusMessage; Render(); return; }
            ShowConfiguredForm(key, true, true);
        }
        private void ShowConfiguredCommand(string command)
        {
            var c = _sink.LibraryController;
            if (!c.IsConfiguredTestCardEditing || c.IsHistoricalFrameSelected) return;
            string key = "test:" + c.ConfiguredTestCardId;
            ShowConfiguredForm(key, true, true);
            if (command == "rename") return;
            string id = c.ConfiguredTestCardId;
            var expected = c.ConfiguredCards().FirstOrDefault(card => card.Id == id);
            _configuredDialog.GetChild("title").text = command == "reload" ? "重新载入测试卡" : "删除测试卡";
            _configuredDialog.GetChild("nameInput").visible = false; _configuredDialog.GetChild("nameLabel").visible = false;
            _configuredDialog.GetChild("notice").text = command == "reload" ? "放弃当前未保存修改，重新读取库中的版本。" : "删除当前测试单卡。基础卡与动作数据不会删除。";
            _configuredDialog.GetChild("btnConfirm").asButton.title = "确认";
            _configuredDialog.GetChild("btnConfirm").onClick.Set(() => {
                if (c.ConfiguredTestCardId != id || c.TestCardRole != _dialogRole) { _configuredDialog.GetChild("notice").text = "编辑上下文已变化。"; return; }
                string issue;
                if (command == "reload")
                {
                    if (!c.DiscardConfiguredEdits(out string reload, out issue)) { _configuredDialog.GetChild("notice").text = issue; return; }
                    CloseConfiguredDialog(); _sink.OpenTacticalLibraryCard(reload);
                }
                else
                {
                    if (expected == null) { _configuredDialog.GetChild("notice").text = "测试卡已不存在。"; return; }
                    if (!c.DeleteConfiguredCard(id, out issue, expected.Revision, expected.FileHash))
                    { _configuredDialog.GetChild("notice").text = issue; return; }
                    CloseConfiguredDialog(); _sink.OpenTacticalLibrary();
                }
            });
        }
        private void ShowConfiguredForm(string key, bool copy, bool rename)
        {
            var c = _sink.LibraryController;
            if (c.IsHistoricalFrameSelected) return;
            _configuredDialog ??= Fgui.UIPackage.CreateObject(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId, "RuntimeAiLabConfiguredCardDialog")?.asCom;
            if (_configuredDialog == null) throw new InvalidOperationException("测试卡对话框发布资源缺失。");
            _dialogKey = key; _dialogCopy = copy; _dialogRename = rename; _dialogShared = false; _dialogRole = c.TestCardRole;
            _dialogWorkspace = c.DraftWorkspace?.WorkspaceId; _dialogRevision = c.DraftWorkspace?.CurrentRevision ?? -1;
            var title = _configuredDialog.GetChild("title"); title.text = rename ? "重命名测试单卡" : copy ? "另存测试单卡" : "从基础卡创建测试单卡";
            bool faceCopy = copy && !rename && key?.StartsWith("test:", StringComparison.Ordinal) == true && _page?.visible == true;
            string name = faceCopy ? c.ConfiguredCardFaces().FirstOrDefault(f => "test:" + f.Id == key)?.Name ?? "测试卡" : copy ? c.ConfiguredCardName : c.GetTacticalLibraryEntries().FirstOrDefault(e => e.Key == key)?.Name ?? "测试卡";
            var input = _configuredDialog.GetChild("nameInput").asTextInput;
            input.visible = true; _configuredDialog.GetChild("nameLabel").visible = true;
            input.maxLength = 128; input.text = name + (rename ? "" : " · 测试");
            var scope = _configuredDialog.GetChild("btnDestination").asButton;
            scope.visible = !rename; scope.title = "保存到：当前角色";
            scope.onClick.Set(() => { _dialogShared = !_dialogShared; scope.title = "保存到：" + (_dialogShared ? "所有角色共用" : "当前角色"); });
            _configuredDialog.GetChild("notice").text = rename ? "先修改工作副本名称，点击编辑页“保存”后更新卡库。"
                : "继承基础配置并补齐默认动作，创建后留在测试卡库；可在卡面直接改名、配置动作和保存。";
            _configuredDialog.GetChild("btnConfirm").asButton.title = rename ? "应用名称" : "创建";
            _configuredDialog.GetChild("btnConfirm").onClick.Set(() => {
                if (c.TestCardRole != _dialogRole || c.DraftWorkspace?.WorkspaceId != _dialogWorkspace || c.DraftWorkspace.CurrentRevision != _dialogRevision)
                { _configuredDialog.GetChild("notice").text = "编辑上下文已变化，请重新打开。"; return; }
                if (string.IsNullOrWhiteSpace(input.text)) { _configuredDialog.GetChild("notice").text = "请输入名称。"; return; }
                if (_dialogRename) { c.RenameConfiguredCard(input.text); CloseConfiguredDialog(); return; }
                string newKey, issue;
                bool created = faceCopy
                    ? c.CopyConfiguredCardFace(_dialogKey.Substring(5), input.text, _dialogShared, out newKey, out issue)
                    : c.CreateConfiguredCard(_dialogKey, input.text, _dialogShared, _dialogCopy, out newKey, out issue);
                if (!created) { _configuredDialog.GetChild("notice").text = issue; return; }
                CloseConfiguredDialog();
                c.TestCardLibraryFilter = "all"; c.TacticalLibraryQuery = ""; c.TacticalLibrarySelectedKey = newKey;
                if (_page?.visible != true) _sink.OpenTacticalLibrary(preserveOrigin: true);
                var entries = c.GetTacticalLibraryEntries();
                int index = Array.FindIndex(entries, entry => entry.Key == newKey);
                var grid = LibraryGrid(_root.width, _root.height);
                int capacity = grid.x * grid.y;
                c.TacticalLibraryPage = Math.Max(0, index / capacity * capacity);
                _notice = "已创建，可直接配置动作；点击卡面保存后存入库。";
                _scrollToFace = newKey.Substring(5);
                RefreshFaces();
            });
            _configuredDialog.GetChild("btnCancel").onClick.Set(CloseConfiguredDialog);
            _configuredDialog.onKeyDown.Set(context => { if (context.inputEvent?.keyCode == UnityEngine.KeyCode.Escape) { CloseConfiguredDialog(); context.StopPropagation(); } });
            _configuredDialog.visible = true;
            if (_configuredPopups?.Show(_configuredView, _configuredDialog, null,
                new MxFairyGuiPopupOptions(MxFairyGuiPopupPlacement.Auto, safeMargin: 12f)) != true) Fgui.GRoot.inst.ShowPopup(_configuredDialog);
            _configuredDialog.SetXY((Fgui.GRoot.inst.width - _configuredDialog.width) / 2, (Fgui.GRoot.inst.height - _configuredDialog.height) / 2);
        }
        private void CloseConfiguredDialog()
        {
            if (_configuredDialog == null) return;
            _configuredPopups?.Hide(_configuredDialog, MxFairyGuiPopupCloseReason.Toggle);
            if (_configuredDialog.parent != null) Fgui.GRoot.inst.HidePopup(_configuredDialog);
            _configuredDialog.visible = false;
        }
    }
}
