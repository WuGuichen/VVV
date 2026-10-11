using System;
using System.Linq;
using MxFramework.Editor.Levels;
using UnityEngine.UIElements;
namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestGuidanceLevelEditorModule
    {
        internal bool RevealLevelIssue(MxLevelEditorWindow window, string path)
        {
            EnsureLoaded(); int index = CharacterTestLevelDiagnosticLocation.ItemIndex(path, "steps");
            if (window == null || index < 0 || index >= _document.Steps.Count) return false;
            _selectedStableId = _document.Steps[index].StableId;
            if (!window.TrySelectWorkspace(ModuleId)) return false;
            _stepList?.SetSelection(index); _stepList?.ScrollToItem(index); RebuildEditorAndPreview();
            string field = path.Contains("completion", StringComparison.OrdinalIgnoreCase) ? "character-test-guidance-completion-condition"
                : path.Contains("inputAction", StringComparison.OrdinalIgnoreCase) ? "character-test-guidance-input-action"
                : path.Contains("stableId", StringComparison.OrdinalIgnoreCase) ? "character-test-guidance-field-Stable ID" : null;
            var target = field == null ? null : window.rootVisualElement.Q<VisualElement>(field);
            target?.Focus();
            _status = target != null ? "已定位教学步骤与字段：" + path : "已定位教学步骤；此嵌套字段暂未支持自动焦点：" + path;
            return true;
        }
    }
    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule
    {
        internal bool RevealLevelIssue(MxLevelEditorWindow window, string path)
        {
            EnsureLoaded(); int index = CharacterTestLevelDiagnosticLocation.ItemIndex(path, "stations");
            if (window == null || index < 0 || index >= _document.Stations.Count) return false;
            var station = _document.Stations[index];
            string label = path.EndsWith("stableId", StringComparison.OrdinalIgnoreCase) ? "Stable ID"
                : path.EndsWith("displayName", StringComparison.OrdinalIgnoreCase) ? "名称"
                : path.EndsWith("intent", StringComparison.OrdinalIgnoreCase) ? "验证意图"
                : path.EndsWith("notes", StringComparison.OrdinalIgnoreCase) ? "备注" : null;
            bool located = window.RevealDocumentRow(ModuleId, "document", "station:" + station.StableId,
                label == null ? null : "mx-genesis-field-" + label);
            if (located) _status = label != null ? "已定位验证站与字段：" + path : "已定位验证站；此嵌套字段暂未支持自动焦点：" + path;
            return located;
        }
    }
}
