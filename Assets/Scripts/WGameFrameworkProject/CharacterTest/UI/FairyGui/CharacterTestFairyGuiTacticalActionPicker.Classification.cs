using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalActionPicker
    {
        private readonly HashSet<string> _classificationWeapons = new(StringComparer.Ordinal);
        private int _classifyingId;
        private string _classificationFingerprint;
        private static readonly string[] ClassificationControls = {
            "classificationTitle", "classificationHint", "classificationWeapons", "btnClassUnclassified", "btnClassSave", "btnClassCancel"
        };

        private bool CanClassify() => CharacterTestActionClassificationEditor.CanEdit && _controller != null
            && _accept?.Invoke() == true && _context == _controller.TacticalPreviewBindingContextKey;

        private void BindClassification()
        {
            _root.GetChild("btnClassify").onClick.Set(BeginClassification);
            _root.GetChild("btnClassSave").onClick.Set(SaveClassification);
            _root.GetChild("btnClassCancel").onClick.Set(CancelClassification);
            _root.GetChild("btnClassUnclassified").onClick.Set(() => {
                _classificationWeapons.Clear(); RefreshClassificationChoices();
            });
            _root.GetChild("classificationWeapons").asList.itemRenderer = (index, item) => {
                var type = _catalog.WeaponTypes[index];
                var button = item.asButton;
                button.SetSize(_root.GetChild("classificationWeapons").asList.viewWidth, 34);
                button.title = (_classificationWeapons.Contains(type.Id) ? "✓  " : "     ") + type.GroupLabel;
                button.onClick.Set(context => {
                    context.StopPropagation();
                    if (!_classificationWeapons.Remove(type.Id)) _classificationWeapons.Add(type.Id);
                    RefreshClassificationChoices();
                });
            };
        }

        private void LayoutClassification(float x, float width, float height)
        {
            Place("btnClassify", x, 150, width, 34);
            Place("classificationTitle", x, 150, width, 32);
            Place("classificationHint", x, 190, width, 54);
            Place("classificationWeapons", x, 250, width, Mathf.Max(68, height - 416));
            Place("btnClassUnclassified", x, height - 154, width, 32);
            float half = (width - 10) / 2;
            Place("btnClassCancel", x, height - 112, half, 34);
            Place("btnClassSave", x + half + 10, height - 112, half, 34);
            SetClassificationEditing(false);
        }

        private void SetClassificationEditing(bool editing)
        {
            foreach (string name in ClassificationControls) _root.GetChild(name).visible = editing;
            _root.GetChild("details").visible = !editing;
            _root.GetChild("btnClassify").visible = !editing;
            foreach (string name in new[] { "query", "btnWeapon", "btnType", "btnRefresh", "btnDefault", "btnClear", "btnSummary", "btnBatchSummary" })
                _root.GetChild(name).enabled = !editing;
            _root.GetChild("actionList").touchable = !editing;
            if (editing) _root.GetChild("btnConfirm").enabled = false;
        }

        private void BeginClassification()
        {
            if (!CanClassify()) return;
            var entry = _catalog.Actions.FirstOrDefault(a => a.ActionId == _selected);
            if (entry == null) return;
            _classifyingId = entry.ActionId;
            _classificationFingerprint = _catalog.Fingerprint;
            _classificationWeapons.Clear();
            _classificationWeapons.UnionWith(entry.WeaponTypes);
            _root.GetChild("filterList").visible = false;
            SetClassificationEditing(true);
            _root.GetChild("classificationTitle").text = "武器分类 · " + entry.Name;
            _root.GetChild("classificationTitle").tooltips = entry.Name + " / " + entry.ActionId;
            RefreshClassificationChoices();
        }

        private void RefreshClassificationChoices()
        {
            _root.GetChild("classificationWeapons").asList.numItems = _catalog.WeaponTypes.Length;
            _root.GetChild("classificationHint").text = "可多选，保存后对整个动作库生效。\n"
                + (_classificationWeapons.Count == 0 ? "当前：未分类" : "已选：" + string.Join("、", _catalog.WeaponTypes.Where(w => _classificationWeapons.Contains(w.Id)).Select(w => w.Label)));
        }

        private void CancelClassification()
        {
            bool wasEditing = _classifyingId > 0;
            _classifyingId = 0;
            _classificationWeapons.Clear();
            if (_root == null || _root.isDisposed) return;
            SetClassificationEditing(false);
            if (wasEditing) ShowDetails();
        }

        private void SaveClassification()
        {
            if (_classifyingId <= 0) return;
            if (!CanClassify())
            { _root.GetChild("classificationHint").text = "编辑对象已变化，请取消后重新打开动作库。"; return; }
            if (!CharacterTestActionClassificationEditor.TrySave(_classifyingId, _classificationWeapons.ToArray(), _classificationFingerprint, out string issue))
            {
                _root.GetChild("classificationHint").text = issue;
                _root.GetChild("classificationHint").tooltips = issue;
                return;
            }
            int edited = _classifyingId;
            CancelClassification();
            _controller.RefreshActionBrowser();
            _catalog = CharacterTestActionBrowserCatalog.Get();
            Rebuild();
            string notice = issue.Length > 0 ? issue : _filtered.Any(a => a.ActionId == edited)
                ? "武器分类已保存。动作可用性仍按角色和执行配置校验。"
                : "武器分类已保存；该动作不再符合当前筛选条件。";
            _root.GetChild("notice").text = notice;
            _root.GetChild("notice").tooltips = notice;
        }
    }
}
