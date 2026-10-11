using System;
using System.Linq;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Standalone tactical library backed by the published Composer page.</summary>
    internal sealed partial class CharacterTestFairyGuiTacticalLibraryBrowser
    {
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink _sink;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder _cards;
        private Fgui.GComponent _root, _page;
        private string _renderKey;
        private string _pendingDeleteKey, _deleteWorkspaceId, _deletePrompt, _notice;
        private int _deleteRevision;
        private CharacterTestConfiguredCard _pendingTestDelete;
        private CharacterTestFairyGuiCardUiPreview _cardUiPreview;
        internal CharacterTestFairyGuiTacticalLibraryBrowser(IMxUiCommandSink sink, IMxFairyGuiPopupService popups, MxUiViewId viewId)
        {
            _sink = sink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            _configuredPopups = popups; _configuredView = viewId;
            _cards = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder(null, null, viewId)
            {
                ExternalQueryChanged = query => { _pendingDeleteKey = null; _sink.LibraryController.TacticalLibraryQuery = query; _sink.LibraryController.TacticalLibraryPage = 0; _sink.LibraryController.TacticalLibraryIssue = string.Empty; Render(); },
                ExternalPageChanged = delta => { _pendingDeleteKey = null; _sink.LibraryController.TacticalLibraryPage = Math.Max(0, _sink.LibraryController.TacticalLibraryPage + delta); Render(); },
                ExternalSelectCard = key =>
                {
                    if (_sink.LibraryController.TacticalLibrarySelectedKey != key)
                    {
                        _pendingDeleteKey = null;
                        _sink.LibraryController.TacticalLibrarySelectedKey = key;
                        _sink.LibraryController.TacticalLibraryIssue = string.Empty;
                    }
                    Render();
                },
                ExternalOpenCard = key =>
                {
                    _sink.LibraryController.TacticalLibrarySelectedKey = key;
                    if (_sink.LibraryController.TestCardTemplatePicking)
                    { ShowConfiguredDialog(key, false); return true; }
                    bool opened = _sink.OpenTacticalLibraryCard(key);
                    _sink.LibraryController.TacticalLibraryIssue = opened ? string.Empty : _sink.LastStatusMessage;
                    if (!opened) Render(); return opened;
                }
            };
        }
        internal void Bind(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel model)
        {
            if (_sink?.LibraryController == null) return;
            if (!ReferenceEquals(root, _root))
            {
                _cardUiPreview?.Dispose(); _cardUiPreview = null;
                _root = root; _faceRenderKey = _renderKey = null;
            }
            _page = root.GetChild("tacticalLibraryPage")?.asCom;
            _sink.LibraryController.ShowTestCardDialog = ShowConfiguredDialog;
            _sink.LibraryController.ShowTestCardCommand = ShowConfiguredCommand;
            bool library = model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary;
            _cardUiPreview?.Guard(library, _sink.LibraryController.TestCardRole);
            if (_page != null) _page.visible = library;
            if (library)
            {
                foreach (var child in root.GetChildren()) child.visible = ReferenceEquals(child, _page);
                Render(); return;
            }
            _pendingDeleteKey = null;
            var entry = root.GetChild("btnTacticalLibrary")?.asButton;
            if (entry != null)
            {
                entry.visible = model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup
                    || model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
                entry.enabled = true; entry.sortingOrder = 85; entry.title = "基础战术卡库";
                bool setup = model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup;
                entry.SetXY(setup ? 20 : 400, setup ? 154 : 72);
                entry.SetSize(152, 36);
                entry.onClick.Set(() => {
                    if (!_sink.LibraryController.LeaveConfiguredCard()) return;
                    _sink.LibraryController.TestCardLibraryMode = false;
                    _sink.LibraryController.TestCardTemplatePicking = false;
                    _sink.OpenTacticalLibrary();
                });
                if (setup && root.GetChild("btnWorkbenchEdit")?.asButton is Fgui.GButton test)
                {
                    // Reuse the published workbench action, unused by the Setup layout.
                    test.visible = true; test.enabled = true; test.sortingOrder = 85;
                    test.title = "战术卡测试"; test.SetXY(184, 154); test.SetSize(152, 36);
                    test.tooltips = "加载当前阵容，手动选择战术卡逐张测试。";
                    test.onClick.Set(_sink.OpenTacticalCardTestLibrary);
                }
            }
            if (model.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null
                && (model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard || model.ShowBehaviorLogic))
            {
                var button = root.GetChild("btnWorkbenchEdit")?.asButton;
                if (button != null) { button.title = "卡库"; button.onClick.Set(() => _sink.OpenTacticalLibrary()); }
            }
            if (_sink.LibraryController.HasStandaloneTacticalCard)
            {
                if (root.GetChild("authoringGroupText") is Fgui.GObject group) group.visible = false;
                if (root.GetChild("authoringCardText") is Fgui.GObject card) card.y = 18;
                foreach (string name in new[] { "btnPublishCurrentChanges", "btnPublishDraft", "btnTacticalBlank", "btnTacticalTemplate", "btnCreateBehaviorCard" })
                    if (root.GetChild(name) is Fgui.GObject item) item.visible = false;
            }
            BindCardTestControls(root);
            if (_sink.LibraryController.IsConfiguredTestCardEditing && root.GetChild("authoringCardText") is Fgui.GObject title)
                title.text = _sink.LibraryController.TestCardOwnerLabel + " · " + _sink.LibraryController.ConfiguredCardName;
        }

        private void BindCardTestControls(Fgui.GComponent root)
        {
            var controller = _sink.LibraryController;
            if (!controller.TacticalCardTestMode) return;
            var picker = root.GetChild("btnWorkbenchEdit")?.asButton;
            if (picker != null)
            {
                picker.title = "选择下一张";
                picker.enabled = controller.CanChooseTacticalTestCard;
                picker.tooltips = "选择本次要执行的战术卡，也可以再次选择同一张。";
                picker.onClick.Set(() => _sink.OpenTacticalLibrary());
            }
            bool canPlay = controller.TacticalCardTestInFlight || !string.IsNullOrEmpty(controller.PendingTacticalTestCardId);
            string playIssue = "";
            if (!controller.TacticalCardTestInFlight) canPlay &= controller.ConfiguredCardCanPlay(out playIssue);
            canPlay &= !controller.TestCurrentChangesQueued && !controller.TestCurrentChangesInProgress
                && !controller.SessionCompleted && !controller.IsHistoricalFrameSelected;
            void Playback()
            {
                controller.ToggleTacticalCardTestPlayback(out _);
            }
            var play = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges)?.asButton;
            if (play != null)
            {
                play.title = controller.TacticalCardTestPlaybackLabel; play.enabled = canPlay;
                play.tooltips = string.IsNullOrEmpty(playIssue) ? controller.TacticalCardTestPlaybackLabel : playIssue;
                play.tooltips = controller.TacticalCardTestStatus;
                play.onClick.Set(Playback);
            }
            var status = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText)?.asTextField;
            if (status != null) { status.text = controller.TacticalCardTestStatus; status.tooltips = status.text; }
            var header = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader)?.asCom;
            var previewPlay = header?.GetChild("btnRuntimePlayPause")?.asButton;
            if (previewPlay != null)
            {
                previewPlay.enabled = canPlay; previewPlay.tooltips = controller.TacticalCardTestPlaybackLabel;
                previewPlay.onClick.Set(Playback);
            }
            var restart = header?.GetChild("btnRuntimeBack10")?.asButton;
            if (restart != null)
            {
                restart.enabled = !controller.TestCurrentChangesQueued && !controller.TestCurrentChangesInProgress;
                restart.tooltips = "重置整个战场并暂停，保留当前选择的战术卡。";
                restart.onClick.Set(_sink.ResetTacticalCardTest);
            }
        }
        private void Render()
        {
            if (_page == null || !_page.visible) return;
            var controller = _sink.LibraryController;
            if (controller.TestCardLibraryMode && !controller.TestCardTemplatePicking) { RenderConfiguredFaces(); return; }
            _faceRenderKey = null;
            if (_page.GetChild("configuredCards") is Fgui.GObject faces) faces.visible = false;
            if (_page.GetChild("faceMenu") is Fgui.GObject faceMenu) faceMenu.visible = false;
            var all = controller.GetTacticalLibraryEntries();
            string query = controller.TacticalLibraryQuery;
            string key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(all) + ":" + query + ":" + controller.TacticalLibraryPage
                + ":" + controller.TacticalLibrarySelectedKey + ":" + _root.width + ":" + _root.height + ":" + controller.TacticalLibraryIssue
                + ":" + _pendingDeleteKey + ":" + _notice + ":" + controller.TacticalCardTestMode;
            if (_renderKey == key) return;
            _renderKey = key;
            var filtered = all.Where(c => string.IsNullOrWhiteSpace(query) || c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (c.Description ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            var grid = LibraryGrid(_root.width, _root.height);
            int capacity = grid.x * grid.y;
            int first = Mathf.Clamp(controller.TacticalLibraryPage / capacity * capacity, 0,
                Math.Max(0, (filtered.Length - 1) / capacity * capacity));
            controller.TacticalLibraryPage = first;
            var model = new CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel
            {
                HeaderText = controller.TestCardTemplatePicking ? "选择基础原材料" : controller.TestCardLibraryMode ? "测试战术卡库" : "基础战术卡库",
                SummaryText = (controller.TestCardLibraryMode ? controller.TacticalPreviewRoleLabel + " · " : "") + filtered.Length + " 张" + (string.IsNullOrWhiteSpace(query) ? "" : " / 共 " + all.Length + " 张")
                    + "  ·  第 " + (first / capacity + 1) + " / " + Math.Max(1, (filtered.Length + capacity - 1) / capacity) + " 页"
                    + (string.IsNullOrEmpty(_notice) ? "" : " · " + _notice),
                EmptyText = all.Length == 0 ? controller.TestCardLibraryMode ? "测试卡库为空，点击“从基础卡创建”。" : "还没有基础战术卡。" : "没有匹配的战术卡。",
                Query = query, Available = true, ActiveCount = filtered.Length, FirstVisibleIndex = first,
                FocusedIndex = Array.FindIndex(filtered, x => x.Key == controller.TacticalLibrarySelectedKey),
                Cards = filtered.Select(c => new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
                { StableCardId = c.Key, Title = c.Name, Description = c.Description, StatusText = c.Revision,
                    HasValidationProblems = c.Key == controller.TacticalLibrarySelectedKey && !string.IsNullOrEmpty(controller.TacticalLibraryIssue),
                    ValidationStatusText = c.Key == controller.TacticalLibrarySelectedKey ? controller.TacticalLibraryIssue : string.Empty,
                    BlockReasonText = c.Key == controller.TacticalLibrarySelectedKey ? controller.TacticalLibraryIssue : string.Empty,
                    Draft = c.Draft, Available = true, Unlocked = true }).ToArray()
            };
            _page.SetXY(0, 0); _page.SetSize(_root.width, _root.height);
            _cards.SetVisibleCardCount(_page, capacity);
            _cards.Bind(_page, model, true);
            if (_page.GetChild("libraryEmpty") is Fgui.GObject empty) empty.visible = filtered.Length == 0;
            float width = _page.width, height = _page.height;
            Place(_page,"background",0,0,width,height);
            Place(_page,"libraryTitle",24,18,210,40);
            Place(_page,"librarySummary",244,24,Mathf.Max(200,width-950),30);
            Place(_page,"libraryQuery",width-538,20,350,38);
            Place(_page,"btnLibraryFilter",width-152,18,126,40);
            BindConfiguredLibraryTools(width);
            if (_page.GetChild("btnLibraryFilter") is Fgui.GButton back)
            { back.visible=true; back.title="返回"; back.enabled=true; back.onClick.Set(_sink.CloseTacticalLibrary); }
            if (_page.GetChild("libraryQuery") is Fgui.GTextInput search) search.promptText="搜索战术卡";
            Place(_page,"libraryEmpty",28,108,width-56,60);
            Place(_page,"btnLibraryPrevious",width-216,height-58,88,38);
            Place(_page,"btnLibraryNext",width-112,height-58,88,38);
            var previous = _page.GetChild("btnLibraryPrevious")?.asButton;
            var next = _page.GetChild("btnLibraryNext")?.asButton;
            if (previous != null) { previous.title = "上一页"; previous.visible = filtered.Length > capacity; }
            if (next != null) { next.title = "下一页"; next.visible = filtered.Length > capacity; }
            const float gap = 14;
            float cardWidth = (width - 48 - gap * (grid.x - 1)) / grid.x;
            float cardHeight = Math.Min(224, (height - 198 - gap * (grid.y - 1)) / grid.y);
            for(int i=0;i<capacity;i++)
            {
                var card=_page.GetChild("libraryCard"+(i+1))?.asCom; if(card==null)continue;
                Place(_page,card.name,24+(i%grid.x)*(cardWidth+gap),130+(i/grid.x)*(cardHeight+gap),cardWidth,cardHeight);
                Place(card,"background",0,0,cardWidth,cardHeight);
                Place(card,"title",56,16,cardWidth-100,32);
                Place(card,"state",16,54,cardWidth-32,24);
                Place(card,"description",16,90,cardWidth-32,cardHeight-150);
                Place(card,"blockReason",16,90,cardWidth-32,cardHeight-150);
                Place(card,"btnAction",cardWidth-120,cardHeight-50,104,34);
                foreach(string name in new[]{"cost","tags","slot","inlineFeedback"})
                    if(card.GetChild(name) is Fgui.GObject extra)extra.visible=false;
                var row = first + i < filtered.Length ? filtered[first + i] : null;
                bool selected = row != null && row.Key == controller.TacticalLibrarySelectedKey;
                bool confirming = row != null && row.Key == _pendingDeleteKey;
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(card, null, 16, 16, 30,
                    selected ? new Color32(83, 224, 186, 255) : new Color32(83, 151, 179, 255), true);
                if (card.GetChild("background") is Fgui.GGraph surface)
                    surface.DrawRect(cardWidth, cardHeight, 1,
                        selected ? new Color32(74, 211, 177, 255) : new Color32(37, 68, 82, 255),
                        selected ? new Color32(12, 49, 57, 255) : new Color32(10, 29, 39, 255));
                if (row != null)
                {
                    card.tooltips = row.Name + "\n" + row.Revision + "\n" + row.Description;
                    if (row.Description == row.Name && card.GetChild("description") is Fgui.GObject duplicate)
                        duplicate.visible = false;
                }
                var publish = card.GetChild("btnSecondaryAction")?.asButton;
                if (publish != null)
                {
                    string publishReason = string.Empty;
                    bool canPublish = row?.Draft == true
                        && controller.CanPublishTacticalLibraryCard(row.Key, out publishReason);
                    bool configured = row?.Key.StartsWith("test:", StringComparison.Ordinal) == true;
                    publish.visible = configured || !controller.TacticalCardTestMode && (confirming || row?.Draft == true);
                    publish.enabled = confirming || canPublish || configured;
                    publish.grayed = !publish.enabled;
                    publish.title = confirming
                        ? "确认删除"
                        : configured ? "重命名" : canPublish
                            ? "发布"
                            : publishReason.IndexOf("同步", StringComparison.Ordinal) >= 0
                                ? "已同步"
                                : "不可发布";
                    publish.titleColor = confirming
                        ? new Color32(255, 102, 112, 255)
                        : new Color32(55, 226, 166, 255);
                    Place(card, "btnSecondaryAction", cardWidth - 226, cardHeight - 50, 96, 34);
                    publish.tooltips = canPublish ? "发布这张战术卡的当前修改" : publishReason;
                    publish.onClick.Set(context =>
                    {
                        context.StopPropagation();
                        if (row == null) return;
                        if (confirming) DeleteCard(row.Key);
                        else if (configured) ShowConfiguredRename(row.Key);
                        else PublishCard(row.Key);
                    });
                }
                var delete = card.GetChild("btnDeleteCard")?.asButton;
                if (delete != null)
                {
                    delete.visible = (!controller.TacticalCardTestMode || row?.Key.StartsWith("test:", StringComparison.Ordinal) == true) && selected && !confirming;
                    delete.enabled = delete.visible;
                    delete.grayed = false;
                    delete.SetXY(cardWidth - 42, 16);
                    delete.SetSize(28, 28);
                    delete.onClick.Set(context => { context.StopPropagation(); if (row != null) DeleteCard(row.Key); });
                }
                var edit = card.GetChild("btnAction")?.asButton;
                if (edit != null && row != null)
                {
                    edit.title = confirming ? "取消" : controller.TestCardTemplatePicking ? "用此卡创建" : controller.TacticalCardTestMode ? "选择并编辑" : "编辑";
                    edit.onClick.Set(context => {
                        context.StopPropagation();
                        if (confirming) { _pendingDeleteKey = null; Render(); }
                        else _cards.ExternalOpenCard?.Invoke(row.Key);
                    });
                }
                if (confirming)
                {
                    if (card.GetChild("description") is Fgui.GTextField prompt) { prompt.visible = true; prompt.text = _deletePrompt; prompt.tooltips = _deletePrompt; }
                    if (card.GetChild("blockReason") is Fgui.GObject block) block.visible = false;
                }
                foreach(string name in new[]{"title","state","description","blockReason"})
                    if(card.GetChild(name) is Fgui.GTextField text)
                    {
                        text.textFormat.size = name == "title" ? 20 : name == "state" ? 13 : 15;
                        text.singleLine = name == "title" || name == "state";
                        text.autoSize = Fgui.AutoSizeType.None;
                        text.verticalAlign = Fgui.VertAlignType.Top;
                        if (name == "description") text.color = new Color32(176, 199, 208, 255);
                    }
            }
        }
        internal static Vector2Int LibraryGrid(float width, float height)
            => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt((width - 34) / 334), 2, 6),
                Mathf.Clamp(Mathf.FloorToInt((height - 142) / 204), 1, 6));
        private void DeleteCard(string key)
        {
            var controller = _sink.LibraryController;
            if (key.StartsWith("test:", StringComparison.Ordinal))
            {
                if (_pendingDeleteKey != key) { _pendingDeleteKey = key; _pendingTestDelete = controller.ConfiguredCards().FirstOrDefault(c => c.Id == key.Substring(5)); _deletePrompt = "删除这张测试单卡？基础卡和动作数据保留。"; }
                else { if (_pendingTestDelete != null) controller.DeleteConfiguredCard(key.Substring(5), out _notice, _pendingTestDelete.Revision, _pendingTestDelete.FileHash); _pendingDeleteKey = null; }
                _renderKey = null; Render(); return;
            }
            if (_pendingDeleteKey != key)
            {
                if (!controller.CanDeleteTacticalLibraryCard(key, out _deletePrompt))
                { controller.TacticalLibraryIssue = _deletePrompt; Render(); return; }
                _pendingDeleteKey = key;
                _deleteWorkspaceId = controller.DraftWorkspace.WorkspaceId;
                _deleteRevision = controller.DraftWorkspace.CurrentRevision;
                controller.TacticalLibraryIssue = string.Empty;
            }
            else
            {
                bool deleted = controller.TryDeleteTacticalLibraryCard(key, _deleteWorkspaceId, _deleteRevision, out string message);
                _pendingDeleteKey = null;
                if (deleted) _notice = message;
                else controller.TacticalLibraryIssue = message;
            }
            Render();
        }
        private void PublishCard(string key)
        {
            _pendingDeleteKey = null;
            bool published = _sink.LibraryController.TryPublishTacticalLibraryCard(key, out string message);
            if (published) _notice = message;
            else _sink.LibraryController.TacticalLibraryIssue = message;
            Render();
        }
        private static void Place(Fgui.GComponent parent,string name,float x,float y,float width,float height)
        { var child=parent.GetChild(name);if(child==null)return;child.relations?.ClearAll();child.SetXY(x,y);child.SetSize(width,height); }
    }
}
