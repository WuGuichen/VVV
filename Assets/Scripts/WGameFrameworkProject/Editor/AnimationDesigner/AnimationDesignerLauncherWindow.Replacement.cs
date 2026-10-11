using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    public sealed partial class AnimationDesignerLauncherWindow
    {
        private AnimationClip _replacementSource;
        private AnimationClip _observedExport;
        private ObjectField _replacementSourceField;
        private Button _replaceButton;
        private Button _undoReplacementButton;
        private Button _recentExportButton;
        private Label _replacementTarget;
        private Label _replacementMessage;
        private Label _replacementHistory;
        private string _historyPath;
        private bool _canUndoReplacement;
        private bool _replacementWasBusy;
        private bool _historyInitialized;
        private int _historyRequest;

        private void BuildReplacementControls()
        {
            _historyInitialized = false;
            _historyRequest++;
            var section = new Foldout { text = "替换与 Git 版本", value = true };
            section.style.marginTop = 8;
            _replacementTarget = new Label();
            section.Add(_replacementTarget);
            var sourceRow = new VisualElement(); sourceRow.style.flexDirection = FlexDirection.Row;
            _replacementSourceField = new ObjectField("已导出新版") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = _replacementSource };
            _replacementSourceField.style.flexGrow = 1;
            _replacementSourceField.RegisterValueChangedCallback(e => { _replacementSource = e.newValue as AnimationClip; RefreshReplacementStatus(); });
            _recentExportButton = new Button(UseRecentExport) { text = "带入最近导出" };
            sourceRow.Add(_replacementSourceField); sourceRow.Add(_recentExportButton); section.Add(sourceRow);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.height = 32;
            _replaceButton = new Button(() => RunReplacement(false)) { text = "快速替换并提交" };
            _replaceButton.tooltip = "自动保存新版，保留旧文件名和引用进行替换提交；无引用的导出副本会删除，被设计存档或其他资产引用的副本会保留。";
            _undoReplacementButton = new Button(() => RunReplacement(true)) { text = "撤回上次替换" };
            _replaceButton.style.flexGrow = 1; _undoReplacementButton.style.flexGrow = 1;
            row.Add(_replaceButton); row.Add(_undoReplacementButton); section.Add(row);
            _replacementMessage = new Label(); _replacementMessage.style.whiteSpace = WhiteSpace.Normal;
            _replacementHistory = new Label(); _replacementHistory.style.whiteSpace = WhiteSpace.Normal;
            section.Add(_replacementMessage); section.Add(_replacementHistory);
            rootVisualElement.Add(section);
            rootVisualElement.schedule.Execute(RefreshReplacementStatus).Every(500);
        }

        private void UseRecentExport()
        {
            AnimationClip exported = AnimationDesignerSession.LatestExportFor(_clip);
            if (exported == null || exported == _clip) return;
            _replacementSource = exported;
            _replacementSourceField.SetValueWithoutNotify(exported);
            RefreshReplacementStatus();
        }

        private void RefreshReplacementStatus()
        {
            if (_replacementMessage == null) return;
            bool busy = AnimationClipReplacementService.Busy;
            bool nativePreview = AnimationDesignerSession.IsClipPreviewActive;
            bool wasBusy = _replacementWasBusy;
            _replacementWasBusy = busy;
            rootVisualElement.SetEnabled(!busy);
            _replacementMessage.text = nativePreview ? "请先结束片段实时预览，再执行 Git 替换或撤回。" : AnimationClipReplacementService.Message;
            string path = _clip == null ? string.Empty : AssetDatabase.GetAssetPath(_clip);
            _replacementTarget.text = _clip == null ? "替换目标：先选择上方动画片段" : "替换目标：" + _clip.name + "（保留原引用）";
            _replacementTarget.tooltip = path;
            if (!busy && (_historyPath != path || wasBusy || !_historyInitialized))
            {
                _historyInitialized = true;
                bool changedTarget = _historyPath != path;
                _historyPath = path;
                if (changedTarget)
                {
                    _replacementSource = null; _observedExport = null;
                    _replacementSourceField.SetValueWithoutNotify(null);
                }
                ReloadReplacementHistory();
            }
            AnimationClip recent = AnimationDesignerSession.LatestExportFor(_clip);
            if (recent != null && recent != _observedExport)
            {
                _observedExport = recent;
                if (_replacementSource == null && recent != _clip)
                {
                    _replacementSource = recent;
                    _replacementSourceField.SetValueWithoutNotify(recent);
                }
            }
            bool standalone = _clip != null && !AssetDatabase.IsSubAsset(_clip) && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
            _recentExportButton.SetEnabled(!busy && recent != null && recent != _clip);
            _replaceButton.SetEnabled(!busy && !nativePreview && standalone && _replacementSource != null && _replacementSource != _clip);
            _undoReplacementButton.SetEnabled(!busy && !nativePreview && standalone && _canUndoReplacement);
        }

        private async void ReloadReplacementHistory()
        {
            int request = ++_historyRequest;
            _canUndoReplacement = false;
            if (_clip == null) { _replacementHistory.text = string.Empty; return; }
            _replacementHistory.text = "正在读取该动画的 Git 记录…";
            var history = await AnimationClipReplacementService.ReadHistoryAsync(_clip);
            if (this == null || request != _historyRequest) return;
            _canUndoReplacement = history.CanUndo;
            _replacementHistory.text = history.Message;
            RefreshReplacementStatus();
        }

        private void OnFocus()
        {
            if (_replacementHistory != null && !AnimationClipReplacementService.Busy) ReloadReplacementHistory();
        }

        private async void RunReplacement(bool undo)
        {
            ClipReplacementResult result = undo
                ? await AnimationClipReplacementService.UndoAsync(_clip)
                : await AnimationClipReplacementService.ReplaceAsync(_clip, _replacementSource);
            if (this == null) return;
            if (!undo && result.Success && _replacementSource == null)
            {
                _replacementSource = null; _observedExport = null;
                _replacementSourceField.SetValueWithoutNotify(null);
            }
            ReloadReplacementHistory();
            RefreshStatus();
        }
    }
}
