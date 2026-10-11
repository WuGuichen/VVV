using System;
using UnityEditor.SceneManagement;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        private IMxLevelEditorDocumentOwner DocumentOwner
            => MxLevelEditorDocumentOwnerRegistry.Owner?.AppliesToActiveScene == true
                ? MxLevelEditorDocumentOwnerRegistry.Owner : null;

        /// <summary>Allocation behavior: NoAlloc. Includes scene edits before changing document identity.</summary>
        public bool HasPendingDocumentChanges
            => DocumentOwner?.IsDirty == true || HasUnsavedChanges(MxLevelEditorModuleRegistry.Modules) || EditorSceneManager.GetActiveScene().isDirty;

        /// <summary>Allocation behavior: LowFreqAlloc. Resolves unsaved work before a document switch.</summary>
        public bool TryPrepareDocumentSwitch(MxLevelEditorUnsavedDecision decision, out string message)
        {
            message = string.Empty;
            if (!HasPendingDocumentChanges) return true;
            if (decision == MxLevelEditorUnsavedDecision.Cancel) { message = "已取消切换，当前编辑内容保留。"; return false; }
            if (decision == MxLevelEditorUnsavedDecision.Save)
            {
                bool saved = TrySaveAllModules(showFailureDialog: false);
                if (!saved) message = "保存失败，未切换关卡，当前编辑内容保留。";
                return saved;
            }
            if (DocumentOwner != null)
            {
                if (DocumentOwner is IMxLevelEditorScopedDocumentOwner scoped)
                {
                    if (!scoped.TryDiscardDraft(out message)) return false;
                    ReloadDocumentModules(); return true;
                }
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    message = "Scene 含有未保存编辑，当前不能安全地单独放弃关卡草稿；请保存 Scene 或取消切换。当前关卡和 Scene 编辑全部保留。";
                    return false;
                }
                DocumentOwner.ReloadSavedDocument(); ReloadDocumentModules(); return true;
            }
            bool restored = RestoreSavedState(showFailureDialog: false, refreshWindow: true);
            if (!restored) message = "未能恢复已保存状态，当前关卡保持不变。";
            return restored;
        }

        /// <summary>Allocation behavior: LowFreqAlloc. Reloads every module after the shared document changes.</summary>
        public void ReloadDocumentModules()
        {
            foreach (IMxLevelEditorModule module in MxLevelEditorModuleRegistry.Modules)
                if (module is IMxLevelEditorDocumentUndoBoundary boundary) boundary.RetireDocumentUndo();
            foreach (IMxLevelEditorModule module in MxLevelEditorModuleRegistry.Modules) module.Reload();
            _selectedTreeNodeId = "document";
            _selectedListRowKey = string.Empty;
            _selectedRowKeys.Clear();
            _selectedListRow = null;
            _rowSearchText = string.Empty;
            RefreshChrome();
        }
    }
}
