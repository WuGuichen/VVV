using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>One published, scrollable action browser shared by binding and event creation.</summary>
    internal sealed partial class CharacterTestFairyGuiTacticalActionPicker
    {
        private readonly MxUiViewId _view;
        private readonly IMxFairyGuiPopupService _popups;
        private Fgui.GComponent _root;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private CharacterTestActionBrowserData _catalog;
        private CharacterTestActionBrowserEntry[] _filtered = Array.Empty<CharacterTestActionBrowserEntry>();
        private string _weapon = "", _type = "", _context;
        private int _selected;
        private Action<string> _select;
        private Func<bool> _accept;
        private long _summaryOwner, _summaryRevision;
        private bool _pollingSummary;
        private string _summaryUi;
        private bool _usingPopupService;

        internal CharacterTestFairyGuiTacticalActionPicker(MxUiViewId view, IMxFairyGuiPopupService popups)
        { _view = view; _popups = popups; }

        internal void Show(CharacterTestRuntimeAiPlannerDevSceneController controller, string current, Fgui.GObject anchor,
            bool allowDefaults, Func<bool> accept, Action<string> select)
        {
            Close();
            Ensure();
            if (_root == null) throw new InvalidOperationException("Published tactical action browser is missing.");
            _controller = controller; _context = controller.TacticalPreviewBindingContextKey;
            _accept = accept; _select = select;
            _summaryOwner = 0; _summaryUi = null; _summaryRevision = CharacterTestTacticalPreparationCatalog.Revision;
            _selected = CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(current);
            _weapon = _type = "";
            _catalog = CharacterTestActionBrowserCatalog.Get();
            _root.GetChild("query").asTextInput.text = "";
            _root.GetChild("btnDefault").visible = allowDefaults;
            _root.GetChild("btnClear").visible = allowDefaults;
            Layout();
            Rebuild();
            _root.visible = true;
            _usingPopupService = _popups?.Show(_view, _root, anchor,
                new MxFairyGuiPopupOptions(MxFairyGuiPopupPlacement.Auto, safeMargin: 12f, anchorGap: 4f)) == true;
            if (!_usingPopupService) Fgui.GRoot.inst.ShowPopup(_root, anchor);
            _root.focusable = true; _root.RequestFocus();
            _pollingSummary = true; Fgui.Timers.inst.Add(0.1f, 0, PollSummary);
            PollSummary(null);
        }

        internal void Close()
        {
            StopSummaryPolling();
            if (_root != null && !_root.isDisposed)
            {
                CancelClassification();
                if (_usingPopupService) _popups?.Hide(_root, MxFairyGuiPopupCloseReason.Toggle);
                else if (_root.parent != null) Fgui.GRoot.inst.HidePopup(_root);
                _root.visible = false;
            }
            _select = null; _accept = null; _usingPopupService = false;
        }

        private void Ensure()
        {
            if (_root != null && !_root.isDisposed) return;
            _root = Fgui.UIPackage.CreateObject(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabTacticalActionPicker")?.asCom;
            if (_root == null) return;
            _root.GetChild("details").asTextField.verticalAlign = Fgui.VertAlignType.Top;
            _root.GetChild("btnClose").asButton.title = "×";
            BindClassification();
            var list = _root.GetChild("actionList").asList;
            list.itemRenderer = RenderRow; list.SetVirtual();
            var query = _root.GetChild("query").asTextInput;
            query.promptText = "搜索名称、动作 ID、来源";
            query.onChanged.Set(() => { if (_controller != null && _catalog != null) Rebuild(); });
            _root.GetChild("btnWeapon").onClick.Set(() => ShowFilter(true));
            _root.GetChild("btnType").onClick.Set(() => ShowFilter(false));
            _root.GetChild("btnClose").onClick.Set(Close);
            _root.GetChild("btnCancel").onClick.Set(Close);
            _root.GetChild("btnDefault").onClick.Set(() => Confirm("$default"));
            _root.GetChild("btnClear").onClick.Set(() => Confirm(""));
            _root.GetChild("btnConfirm").onClick.Set(() => Confirm(CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(_selected)));
            _root.GetChild("btnRefresh").onClick.Set(() => {
                _controller.RefreshActionBrowser(); _catalog = CharacterTestActionBrowserCatalog.Get(); Rebuild();
            });
            _root.GetChild("btnSummary").onClick.Set(() => StartSummary(false));
            _root.GetChild("btnBatchSummary").onClick.Set(() => {
                if (_controller.RuntimeSummaryRunning) { _controller.CancelRuntimeSummary(); PollSummary(null); }
                else StartSummary(true);
            });
            _root.GetChild("btnSummary").tooltips = "在当前界面分帧重建所选动作摘要，使用独立模拟，不退出播放。";
            _root.GetChild("btnBatchSummary").tooltips = "为当前角色与装备重建全部支持的动作摘要；运行中点击可取消。";
            _root.onRemovedFromStage.Add(StopSummaryPolling);
            _root.onKeyDown.Set(context => {
                if (context.inputEvent?.keyCode != KeyCode.Escape) return;
                if (_classifyingId > 0) CancelClassification();
                else if (_root.GetChild("filterList").visible) _root.GetChild("filterList").visible = false;
                else Close();
                context.StopPropagation();
            });
        }

        private void StartSummary(bool batch)
        {
            if (!CanClassify() || !batch && _selected <= 0) return;
            long before = _controller.RuntimeSummarySerial;
            _controller.RequestActionBrowserSummary(_selected, batch);
            if (_controller.RuntimeSummarySerial != before) { _summaryOwner = _controller.RuntimeSummarySerial; _summaryUi = null; }
            _root.GetChild("notice").text = _controller.StatusMessage;
            PollSummary(null);
        }

        private void PollSummary(object unused)
        {
            if (_root == null || _root.isDisposed || !_root.visible || _root.parent == null) { StopSummaryPolling(); return; }
            if (_controller == null) return;
            bool changed = _summaryRevision != CharacterTestTacticalPreparationCatalog.Revision;
            if (changed)
            {
                _summaryRevision = CharacterTestTacticalPreparationCatalog.Revision;
                _root.GetChild("actionList").asList.RefreshVirtualList();
            }
            string status = _controller.RuntimeSummaryStatus;
            if (_summaryUi != status || changed)
            {
                _summaryUi = status;
                if (_classifyingId <= 0) ShowDetails();
                if (!string.IsNullOrEmpty(status))
                {
                    _root.GetChild("notice").text = status;
                    _root.GetChild("notice").tooltips = _controller.RuntimeSummaryDetails;
                }
            }
        }

        private void StopSummaryPolling()
        {
            if (_pollingSummary) { Fgui.Timers.inst.Remove(PollSummary); _pollingSummary = false; }
            if (_controller != null && _summaryOwner != 0 && _summaryOwner == _controller.RuntimeSummarySerial)
                _controller.CancelRuntimeSummary();
            _summaryOwner = 0;
        }

        private void Layout()
        {
            float width = Mathf.Min(1040, Fgui.GRoot.inst.width - 32), height = Mathf.Min(720, Fgui.GRoot.inst.height - 32);
            _root.SetSize(width, height);
            Place("background", 0, 0, width, height);
            Place("btnClose", width - 56, 16, 32, 32);
            Place("resultCount", width - 400, 18, 330, 28);
            float filterWidth = 196, queryWidth = width - 72 - filterWidth * 2;
            Place("querySurface", 24, 66, queryWidth, 38); Place("query", 34, 66, queryWidth - 20, 38);
            Place("btnWeapon", 40 + queryWidth, 66, filterWidth, 38);
            Place("btnType", 48 + queryWidth + filterWidth, 66, filterWidth, 38);
            Place("notice", 24, 110, width - 48, 32);
            float detailsWidth = Mathf.Min(336, width * .34f), listWidth = width - detailsWidth - 70;
            Place("actionList", 24, 150, listWidth, height - 230);
            Place("emptyText", 34, 250, listWidth - 20, 60);
            Place("details", 46 + listWidth, 196, detailsWidth, height - 266);
            LayoutClassification(46 + listWidth, detailsWidth, height);
            Place("btnDefault", 24, height - 54, 126, 36);
            Place("btnClear", 160, height - 54, 110, 36);
            Place("btnRefresh", 280, height - 54, 110, 36);
            Place("btnSummary", 400, height - 54, 142, 36);
            Place("btnBatchSummary", 552, height - 54, 180, 36);
            Place("btnCancel", width - 278, height - 54, 110, 36);
            Place("btnConfirm", width - 158, height - 54, 134, 36);
            _root.GetChild("filterList").visible = false;
        }

        private void Rebuild()
        {
            CancelClassification();
            if (_weapon.Length > 0 && _weapon != "unclassified" && !_catalog.WeaponTypes.Any(w => w.Id == _weapon)) _weapon = "";
            _filtered = CharacterTestActionBrowserCatalog.Filter(_catalog, _root.GetChild("query").text, _weapon, _type);
            _root.GetChild("resultCount").text = _filtered.Length + " / " + _catalog.Actions.Length + " 个动作";
            _root.GetChild("btnWeapon").asButton.title = "武器 / 用途：" + (_weapon.Length == 0 ? "全部"
                : _weapon == "unclassified" ? "未分类" : _catalog.WeaponTypes.First(w => w.Id == _weapon).Label) + " ▾";
            _root.GetChild("btnType").asButton.title = "类型：" + (_type.Length == 0 ? "全部"
                : CharacterTestActionBrowserCatalog.TypeLabel((CharacterActionCategory)Enum.Parse(typeof(CharacterActionCategory), _type))) + " ▾";
            var notice = _root.GetChild("notice");
            notice.text = _catalog.Issues.Length == 0 ? "不可执行的动作可查看原因；可选范围按当前角色和执行器校验。" : "目录问题：" + _catalog.Issues[0];
            notice.tooltips = string.Join("\n", _catalog.Issues);
            var list = _root.GetChild("actionList").asList;
            list.numItems = _filtered.Length; list.RefreshVirtualList();
            _root.GetChild("emptyText").visible = _filtered.Length == 0;
            if (!_filtered.Any(a => a.ActionId == _selected)) _selected = 0;
            ShowDetails();
        }

        private void RenderRow(int index, Fgui.GObject item)
        {
            var row = item.asCom; var entry = _filtered[index];
            var availability = _controller.EvaluateBrowserAction(entry.ActionId);
            row.SetSize(_root.GetChild("actionList").asList.viewWidth, 70); row.opaque = true;
            row.GetChild("title").text = entry.Name;
            row.GetChild("title").SetSize(row.width - 200, 26);
            row.GetChild("title").asTextField.color = availability.CanSelect ? new Color32(228, 239, 244, 255) : new Color32(139, 155, 164, 255);
            row.GetChild("stableId").text = entry.TypeLabel + " · " + CharacterTestActionBrowserCatalog.WeaponLabels(_catalog, entry)
                + " · " + entry.LibraryId + " / " + entry.ActionId;
            row.GetChild("stableId").SetSize(row.width - 24, 23);
            row.GetChild("meta").SetXY(row.width - 182, 10);
            row.GetChild("meta").SetSize(168, 24);
            row.GetChild("meta").text = availability.CanSelect ? "可选" : availability.Reason;
            row.GetChild("meta").asTextField.color = availability.CanSelect ? new Color32(99, 219, 170, 255) : new Color32(202, 153, 129, 255);
            row.GetChild("selected").visible = entry.ActionId == _selected;
            row.tooltips = entry.Name + "\n" + availability.Reason
                + (availability.Guidance.Length == 0 ? "" : "\n" + availability.Guidance);
            row.onClick.Set(context => {
                context.StopPropagation(); _selected = entry.ActionId; ShowDetails();
                _root.GetChild("actionList").asList.RefreshVirtualList();
                _root.GetChild("filterList").visible = false;
            });
        }

        private void ShowDetails()
        {
            var entry = _filtered.FirstOrDefault(a => a.ActionId == _selected);
            var confirm = _root.GetChild("btnConfirm").asButton;
            confirm.enabled = false;
            _root.GetChild("btnClassify").enabled = entry != null && CanClassify();
            _root.GetChild("btnSummary").enabled = entry != null && CanClassify() && !_controller.RuntimeSummaryRunning;
            _root.GetChild("btnBatchSummary").enabled = _controller.RuntimeSummaryRunning || CanClassify();
            _root.GetChild("btnBatchSummary").asButton.title = _controller.RuntimeSummaryRunning ? "取消重建" : "全量重建摘要";
            if (entry == null) { _root.GetChild("details").text = "选择动作查看详情"; return; }
            var availability = _controller.EvaluateBrowserAction(entry.ActionId);
            confirm.enabled = availability.CanSelect;
            _root.GetChild("details").text = entry.Name + "\n\n" + AvailabilityDetails(availability)
                + "\n\n动作 ID：" + entry.ActionId + "\n来源库：" + entry.LibraryId
                + "\n类型：" + entry.TypeLabel + "\n武器：" + CharacterTestActionBrowserCatalog.WeaponLabels(_catalog, entry)
                + "\n时长：" + (entry.DurationFrames?.ToString() ?? "未定义") + " 帧"
                + "\n\n作者源：\n" + entry.SourcePath;
            _root.GetChild("details").tooltips = _root.GetChild("details").text
                + (string.IsNullOrEmpty(availability.Diagnostic) ? "" : "\n\n原始诊断：\n" + availability.Diagnostic);
        }

        private static string AvailabilityDetails(CharacterTestActionCompatibility availability)
            => availability.CanSelect ? availability.Reason : "不可选原因：" + availability.Reason
                + (availability.Guidance.Length == 0 ? "" : "\n\n处理办法：\n" + availability.Guidance);

        private void ShowFilter(bool weapon)
        {
            var list = _root.GetChild("filterList").asList;
            var values = weapon ? new[] { "" }.Concat(_catalog.WeaponTypes.Select(w => w.Id)).Concat(new[] { "unclassified" }).ToArray()
                : new[] { "" }.Concat(Enum.GetNames(typeof(CharacterActionCategory))).ToArray();
            string Label(string value) => value.Length == 0 ? "全部" : weapon
                ? value == "unclassified" ? "未分类" : _catalog.WeaponTypes.First(w => w.Id == value).GroupLabel
                : CharacterTestActionBrowserCatalog.TypeLabel((CharacterActionCategory)Enum.Parse(typeof(CharacterActionCategory), value));
            list.itemRenderer = (index, item) => {
                string value = values[index]; var button = item.asButton;
                button.SetSize(list.viewWidth, 32); button.title = Label(value);
                button.onClick.Set(context => {
                    context.StopPropagation(); if (weapon) _weapon = value; else _type = value;
                    list.visible = false; Rebuild();
                });
            };
            var anchor = _root.GetChild(weapon ? "btnWeapon" : "btnType");
            list.SetXY(anchor.x, anchor.y + anchor.height + 4); list.SetSize(anchor.width, Mathf.Min(340, values.Length * 36));
            list.sortingOrder = 100; list.visible = true; list.numItems = values.Length;
        }

        private void Confirm(string key)
        {
            if (_accept?.Invoke() != true || _context != _controller.TacticalPreviewBindingContextKey)
            { _root.GetChild("details").text = "编辑对象或角色已变化，请关闭后重新选择。"; _root.GetChild("btnConfirm").asButton.enabled = false; return; }
            if (key.Length > 0 && key != "$default")
            {
                var valid = _controller.EvaluateBrowserAction(CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(key), force: true);
                if (!valid.CanSelect) {
                    _root.GetChild("details").text = AvailabilityDetails(valid);
                    _root.GetChild("details").tooltips = valid.Diagnostic;
                    _root.GetChild("btnConfirm").asButton.enabled = false;
                    _root.GetChild("actionList").asList.RefreshVirtualList();
                    return;
                }
            }
            var select = _select; Close(); select?.Invoke(key);
        }

        private void Place(string name, float x, float y, float width, float height)
        { var child = _root.GetChild(name); child.relations.ClearAll(); child.SetXY(x, y); child.SetSize(width, height); }
    }
}
