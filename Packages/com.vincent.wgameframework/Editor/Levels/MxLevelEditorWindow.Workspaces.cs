using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow
    {
        private readonly List<WorkspaceNavigationItem>
            _workspaceNavigationItems =
                new List<WorkspaceNavigationItem>();

        /// <summary>
        /// Gets the currently displayed workspace key.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        public string CurrentWorkspaceId => _selectedWorkspaceId;

        /// <summary>
        /// Refreshes dirty and status labels after an in-place workspace edit.
        /// Allocation behavior: LowFreqAlloc; updates UI Toolkit text after
        /// explicit editor mutations.
        /// </summary>
        public void RefreshWorkspaceState()
        {
            SyncUnsavedChangesState();
            Label state = _header?.Q<Label>(
                "mx-level-editor-document-state");
            if (state != null)
            {
                bool dirty = HasUnsavedChanges(
                    MxLevelEditorModuleRegistry.Modules);
                state.text = dirty
                    ? "● 有未保存更改"
                    : "● 已保存";
                state.EnableInClassList(
                    "mx-level-editor__state--dirty",
                    dirty);
                state.EnableInClassList(
                    "mx-level-editor__state--saved",
                    !dirty);
            }

            Label status = _header?.Q<Label>(
                "mx-level-editor-status-summary");
            if (status != null && _context?.Module != null)
            {
                status.text = _context.Module.Status;
                status.tooltip =
                    string.IsNullOrWhiteSpace(
                        _context.Module.Status)
                        ? _context.Module.DocumentPath
                        : _context.Module.Status
                            + "\n"
                            + _context.Module.DocumentPath;
            }
            Repaint();
        }

        /// <summary>
        /// Switches to a registered workspace without creating another document
        /// or save participant.
        /// Allocation behavior: LowFreqAlloc; intended for explicit editor
        /// navigation gestures.
        /// </summary>
        public bool TrySelectWorkspace(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return false;

            IReadOnlyList<WorkspaceNavigationItem> workspaces =
                RebuildWorkspaceNavigation(
                    MxLevelEditorModuleRegistry.Modules);
            int index = FindWorkspaceIndex(workspaces, workspaceId);
            if (index < 0)
                return false;

            WorkspaceNavigationItem selected = workspaces[index];
            _selectedModuleIndex = index;
            _selectedWorkspaceId = selected.WorkspaceId;
            _selectedTreeNodeId = "document";
            _selectedListRowKey = string.Empty;
            _selectedListRow = null;
            _rowSearchText = string.Empty;
            selected.OwnerModule.OnEnable();
            RefreshChrome();
            Show();
            Focus();
            return true;
        }

        /// <summary>
        /// Selects the owner module row used by Scene overlays while an
        /// explicit workspace is active.
        /// Allocation behavior: LowFreqAlloc; rebuilds editor projection rows.
        /// </summary>
        public bool SetWorkspaceSceneContext(
            string treeNodeId,
            string rowKey)
        {
            if (_activeWorkspace == null
                || _structuredModule == null
                || _context == null
                || string.IsNullOrWhiteSpace(rowKey))
            {
                return false;
            }

            string normalizedTreeNodeId =
                string.IsNullOrWhiteSpace(treeNodeId)
                    ? "document"
                    : treeNodeId.Trim();
            IReadOnlyList<MxLevelEditorListRow> rows =
                _structuredModule.BuildRows(
                    _context,
                    normalizedTreeNodeId);
            _allListRows.Clear();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    MxLevelEditorListRow row = rows[i];
                    if (row != null)
                        _allListRows.Add(row);
                }
            }

            _selectedTreeNodeId = normalizedTreeNodeId;
            _selectedListRowKey = rowKey.Trim();
            _selectedListRow = null;
            for (int i = 0; i < _allListRows.Count; i++)
            {
                if (string.Equals(
                        _allListRows[i].Key,
                        _selectedListRowKey,
                        StringComparison.Ordinal))
                {
                    _selectedListRow = _allListRows[i];
                    break;
                }
            }

            SceneView.RepaintAll();
            return _selectedListRow != null;
        }

        private IReadOnlyList<WorkspaceNavigationItem>
            RebuildWorkspaceNavigation(
                IReadOnlyList<IMxLevelEditorModule> modules)
        {
            _workspaceNavigationItems.Clear();
            if (modules == null)
                return _workspaceNavigationItems;

            IReadOnlyList<IMxLevelEditorWorkspace> explicitWorkspaces =
                MxLevelEditorWorkspaceRegistry.Workspaces;
            for (int moduleIndex = 0;
                 moduleIndex < modules.Count;
                 moduleIndex++)
            {
                IMxLevelEditorModule module = modules[moduleIndex];
                if (module == null)
                    continue;

                _workspaceNavigationItems.Add(
                    new WorkspaceNavigationItem(
                        module.ModuleId,
                        ResolveModuleDisplayName(module),
                        module,
                        null));
                for (int workspaceIndex = 0;
                     workspaceIndex < explicitWorkspaces.Count;
                     workspaceIndex++)
                {
                    IMxLevelEditorWorkspace workspace =
                        explicitWorkspaces[workspaceIndex];
                    if (workspace == null
                        || !string.Equals(
                            workspace.OwnerModuleId,
                            module.ModuleId,
                            StringComparison.Ordinal)
                        || string.Equals(
                            workspace.WorkspaceId,
                            module.ModuleId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    _workspaceNavigationItems.Add(
                        new WorkspaceNavigationItem(
                            workspace.WorkspaceId,
                            workspace.DisplayName,
                            module,
                            workspace));
                }
            }

            int selectedIndex = FindWorkspaceIndex(
                _workspaceNavigationItems,
                _selectedWorkspaceId);
            if (selectedIndex >= 0)
                _selectedModuleIndex = selectedIndex;
            else
                _selectedModuleIndex = Mathf.Clamp(
                    _selectedModuleIndex,
                    0,
                    Math.Max(0, _workspaceNavigationItems.Count - 1));
            if (_workspaceNavigationItems.Count > 0)
            {
                _selectedWorkspaceId =
                    _workspaceNavigationItems[_selectedModuleIndex]
                        .WorkspaceId;
            }

            return _workspaceNavigationItems;
        }

        private static int FindWorkspaceIndex(
            IReadOnlyList<WorkspaceNavigationItem> workspaces,
            string workspaceId)
        {
            if (workspaces == null
                || string.IsNullOrWhiteSpace(workspaceId))
            {
                return -1;
            }

            for (int i = 0; i < workspaces.Count; i++)
            {
                if (string.Equals(
                        workspaces[i].WorkspaceId,
                        workspaceId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void PrepareWorkspaceSceneContext()
        {
            _allListRows.Clear();
            _selectedListRow = null;
            if (_structuredModule == null || _context == null)
                return;

            IReadOnlyList<MxLevelEditorListRow> rows =
                _structuredModule.BuildRows(_context, "document");
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                    _allListRows.Add(rows[i]);
            }
        }

        private void BuildWorkspaceContent(
            IMxLevelEditorWorkspace workspace)
        {
            var root = new VisualElement
            {
                name = "mx-level-editor-workspace-content"
            };
            root.style.flexGrow = 1;
            root.style.minWidth = 0;
            root.style.minHeight = 0;
            VisualElement content = workspace.CreateContent(this);
            if (content != null)
                root.Add(content);
            _contentHost.Add(root);
        }

        private sealed class WorkspaceNavigationItem
        {
            internal WorkspaceNavigationItem(
                string workspaceId,
                string displayName,
                IMxLevelEditorModule ownerModule,
                IMxLevelEditorWorkspace workspace)
            {
                WorkspaceId = workspaceId ?? string.Empty;
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? WorkspaceId
                    : displayName.Trim();
                OwnerModule = ownerModule;
                Workspace = workspace;
            }

            internal string WorkspaceId { get; }

            internal string DisplayName { get; }

            internal IMxLevelEditorModule OwnerModule { get; }

            internal IMxLevelEditorWorkspace Workspace { get; }
        }
    }
}
