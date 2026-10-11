using System;
using UnityEngine.UIElements;
namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        /// <summary>Reveals a document issue in its existing module/list/Scene. Allocation behavior: LowFreqAlloc.</summary>
        public bool RevealDocumentRow(string moduleId, string treeNodeId, string rowKey, string fieldName = null)
        {
            var workspaces = RebuildWorkspaceNavigation(MxLevelEditorModuleRegistry.Modules);
            for (int i = 0; i < workspaces.Count; i++)
            {
                if (workspaces[i].OwnerModule.ModuleId != moduleId || workspaces[i].Workspace != null) continue;
                _selectedModuleIndex = i; _selectedWorkspaceId = workspaces[i].WorkspaceId;
                _selectedRowKeys.Clear(); _selectedTreeNodeId = treeNodeId; _selectedListRowKey = rowKey;
                RefreshChrome(); RevealStructuredRow(treeNodeId, rowKey); SyncUnitySelectionFromRow(_selectedListRow);
                if (!string.IsNullOrEmpty(fieldName))
                {
                    var field = _inspectorHost?.Q<VisualElement>(fieldName);
                    (field?.Q<VisualElement>("unity-text-input") ?? field)?.Focus();
                }
                return _selectedListRow != null;
            }
            return false;
        }
    }
}
