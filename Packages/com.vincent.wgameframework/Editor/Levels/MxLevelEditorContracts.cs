using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public interface IMxLevelEditorModule
    {
        string ModuleId { get; }

        string DisplayName { get; }

        string DocumentPath { get; }

        bool IsDirty { get; }

        string Status { get; }

        void OnEnable();

        void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar);

        VisualElement CreateContent(MxLevelEditorWindow window);

        void OpenJson();

        void Reload();

        void Save();

        void Validate();

        void ApplyToScene();

        void SelectRootSource();
    }

    /// <summary>
    /// Optionally releases a module's window callbacks when the host is disabled.
    /// Detachment is idempotent and must retain authoring owners and dirty state
    /// until the window's save or discard transaction has completed.
    /// Allocation behavior: LowFreqAlloc; Editor window lifecycle only.
    /// </summary>
    public interface IMxLevelEditorWindowLifecycleModule
    {
        void DetachWindow(MxLevelEditorWindow window);
    }

    /// <summary>
    /// Limits a module to scenes that own its authoring data. Modules without this
    /// interface remain available in every scene.
    /// </summary>
    public interface IMxLevelEditorSceneScope
    {
        bool AppliesToActiveScene { get; }
    }

    /// <summary>
    /// Projects an editor-only authoring workspace owned by an existing level
    /// module. Workspaces never own documents or participate in save
    /// coordination.
    /// Allocation behavior: AllocByDesign; invoked only while opening or
    /// rebuilding Level Editor UI.
    /// </summary>
    public interface IMxLevelEditorWorkspace
    {
        /// <summary>
        /// Gets the stable navigation key.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        string WorkspaceId { get; }

        /// <summary>
        /// Gets the user-facing navigation label.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the module ID that owns document and save state.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        string OwnerModuleId { get; }

        /// <summary>
        /// Prepares low-frequency Editor state before content is shown.
        /// Allocation behavior: LowFreqAlloc.
        /// </summary>
        void OnEnable();

        /// <summary>
        /// Builds the UI Toolkit projection for this workspace.
        /// Allocation behavior: AllocByDesign.
        /// </summary>
        VisualElement CreateContent(MxLevelEditorWindow window);
    }

    /// <summary>
    /// Supplies low-frequency, editor-only dirty-state details for save diagnostics.
    /// Allocation behavior: NoAlloc.
    /// </summary>
    public interface IMxLevelEditorDirtyDiagnostics
    {
        bool IsDocumentDirty { get; }

        bool IsManagedSceneDirty { get; }
    }

    /// <summary>
    /// Adds module-specific, low-frequency commands to the Level Editor tools menu.
    /// Allocation behavior: AllocByDesign; invoked only while rebuilding editor UI.
    /// </summary>
    public interface IMxLevelEditorToolsModule
    {
        void BuildToolsMenu(DropdownMenu menu, MxLevelEditorWindow window);
    }

    public interface IMxLevelEditorStructuredModule
    {
        IReadOnlyList<MxLevelEditorTreeNode> BuildTree(MxLevelEditorContext context);

        IReadOnlyList<MxLevelEditorListRow> BuildRows(
            MxLevelEditorContext context,
            string selectedTreeNodeId);

        void BuildInspector(
            MxLevelEditorContext context,
            MxLevelEditorListRow selectedRow,
            VisualElement inspector);

        void BuildDiagnostics(MxLevelEditorContext context, VisualElement diagnostics);
    }

    public interface IMxLevelEditorSelectionSyncModule
    {
        bool TryResolveRowObject(
            MxLevelEditorContext context,
            MxLevelEditorListRow row,
            out UnityEngine.Object target);

        bool TryResolveSelection(
            MxLevelEditorContext context,
            UnityEngine.Object selectedObject,
            out MxLevelEditorSelectionTarget target);
    }

    public interface IMxLevelEditorSceneViewModule
    {
        bool SupportsSceneViewEditing { get; }

        void DrawSceneView(MxLevelEditorSceneViewContext context, SceneView sceneView);
    }

    /// <summary>
    /// Adds object-specific commands to the Scene view context menu.
    /// Allocation behavior: AllocByDesign; invoked only after an explicit context click.
    /// </summary>
    public interface IMxLevelEditorSceneContextMenuModule
    {
        void BuildSceneContextMenu(
            MxLevelEditorSceneViewContext context,
            GenericMenu menu);
    }

    public interface IMxLevelEditorPlacementModule
    {
        bool SupportsScenePlacement { get; }

        bool IsScenePlacementActive { get; }

        void BuildPlacementPanel(
            MxLevelEditorPlacementContext context,
            MxLevelEditorPlacementPanel panel);

        void DrawScenePlacement(MxLevelEditorPlacementContext context, SceneView sceneView);

        void HandleScenePlacement(MxLevelEditorPlacementContext context, SceneView sceneView);

        void CancelScenePlacement(MxLevelEditorContext context);
    }

    /// <summary>
    /// Lets a module participate in editor-only scene dirty tracking and unmanaged-object coverage.
    /// Allocation behavior: LowFreqAlloc; invoked after explicit editor object or hierarchy changes.
    /// </summary>
    public interface IMxLevelEditorSceneChangeAwareModule
    {
        Transform ManagedSceneRoot { get; }

        void CollectManagedSceneTransforms(List<Transform> output);

        bool NotifySceneTransformChanged(Transform transform);

        void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> allManagedTransforms);

        /// <summary>
        /// Clears only the module's managed-scene dirty state after Unity confirms that scene was
        /// saved. Document dirty state must remain unchanged.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        void AcknowledgeManagedSceneSaved();
    }

    /// <summary>
    /// Participates in the Level Editor's coordinated all-module save transaction.
    /// Implementations must not save a Unity scene from <see cref="SavePreparedChanges"/>.
    /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
    /// </summary>
    public interface IMxLevelEditorSaveParticipant
    {
        /// <summary>
        /// Validates the current document and scene target without writing files or saving scenes.
        /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
        /// </summary>
        bool TryPrepareSave(out string failureMessage);

        /// <summary>
        /// Writes the prepared document and applies any required in-memory scene cache changes.
        /// The coordinated save service persists the scene after every participant succeeds.
        /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
        /// </summary>
        void SavePreparedChanges();

        /// <summary>
        /// Commits or rejects the module's saved baseline after the coordinated transaction ends.
        /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
        /// </summary>
        void CompleteCoordinatedSave(bool succeeded, string message);
    }

    /// <summary>Optional assets written by the same Level Editor save transaction.</summary>
    public interface IMxLevelEditorAssetSavePaths
    {
        void CollectAssetSavePaths(List<string> paths);
    }

    public static class MxLevelEditorSceneCoverage
    {
        /// <summary>
        /// Finds top-level unmanaged subtrees while excluding registered objects, their children,
        /// and structural ancestors that lead to registered objects.
        /// Allocation behavior: LowFreqAlloc; editor hierarchy validation only.
        /// </summary>
        public static void CollectUnregisteredRoots(
            Transform root,
            IReadOnlyList<Transform> managedTransforms,
            List<Transform> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            if (root == null)
                return;

            var covered = new HashSet<Transform>();
            var structural = new HashSet<Transform> { root };
            if (managedTransforms != null)
            {
                for (int i = 0; i < managedTransforms.Count; i++)
                {
                    Transform managed = managedTransforms[i];
                    if (managed == null || (managed != root && !managed.IsChildOf(root)))
                        continue;

                    AddDescendants(managed, covered);
                    Transform ancestor = managed.parent;
                    while (ancestor != null && ancestor != root)
                    {
                        structural.Add(ancestor);
                        ancestor = ancestor.parent;
                    }
                }
            }

            for (int i = 0; i < root.childCount; i++)
                CollectUnregisteredRootsRecursive(root.GetChild(i), covered, structural, output);
        }

        private static void CollectUnregisteredRootsRecursive(
            Transform current,
            HashSet<Transform> covered,
            HashSet<Transform> structural,
            List<Transform> output)
        {
            if (current == null || covered.Contains(current))
                return;

            if (!structural.Contains(current))
            {
                output.Add(current);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
                CollectUnregisteredRootsRecursive(current.GetChild(i), covered, structural, output);
        }

        private static void AddDescendants(Transform root, HashSet<Transform> output)
        {
            if (root == null || !output.Add(root))
                return;

            for (int i = 0; i < root.childCount; i++)
                AddDescendants(root.GetChild(i), output);
        }
    }

    public sealed class MxLevelEditorSceneViewContext
    {
        public MxLevelEditorSceneViewContext(
            MxLevelEditorContext editorContext,
            string selectedTreeNodeId,
            MxLevelEditorListRow selectedRow,
            IReadOnlyList<MxLevelEditorListRow> scopeRows)
        {
            EditorContext = editorContext;
            SelectedTreeNodeId = string.IsNullOrWhiteSpace(selectedTreeNodeId)
                ? "document"
                : selectedTreeNodeId.Trim();
            SelectedRow = selectedRow;
            ScopeRows = scopeRows ?? Array.Empty<MxLevelEditorListRow>();
        }

        public MxLevelEditorContext EditorContext { get; }

        public string SelectedTreeNodeId { get; }

        public MxLevelEditorListRow SelectedRow { get; }

        public IReadOnlyList<MxLevelEditorListRow> ScopeRows { get; }
    }

    public sealed class MxLevelEditorPlacementContext
    {
        public MxLevelEditorPlacementContext(
            MxLevelEditorContext editorContext,
            string selectedTreeNodeId,
            MxLevelEditorListRow selectedRow,
            IReadOnlyList<MxLevelEditorListRow> scopeRows,
            Action closePlacementSurface = null)
        {
            EditorContext = editorContext;
            SelectedTreeNodeId = string.IsNullOrWhiteSpace(selectedTreeNodeId)
                ? "document"
                : selectedTreeNodeId.Trim();
            SelectedRow = selectedRow;
            ScopeRows = scopeRows ?? Array.Empty<MxLevelEditorListRow>();
            _closePlacementSurface = closePlacementSurface;
        }

        private readonly Action _closePlacementSurface;

        public MxLevelEditorContext EditorContext { get; }

        public string SelectedTreeNodeId { get; }

        public MxLevelEditorListRow SelectedRow { get; }

        public IReadOnlyList<MxLevelEditorListRow> ScopeRows { get; }

        /// <summary>
        /// Closes the transient placement configuration surface, when one is open.
        /// </summary>
        /// <remarks>LowFreqAlloc; Editor interaction only.</remarks>
        public void ClosePlacementSurface()
        {
            _closePlacementSurface?.Invoke();
        }
    }

    /// <summary>
    /// Provides the stable layout slots used by a level placement popover.
    /// </summary>
    /// <remarks>AllocByDesign; created only when the user opens the Editor popover.</remarks>
    public sealed class MxLevelEditorPlacementPanel
    {
        public MxLevelEditorPlacementPanel(
            VisualElement fieldsHost,
            VisualElement optionsHost,
            VisualElement actionsHost)
        {
            FieldsHost = fieldsHost ?? throw new ArgumentNullException(nameof(fieldsHost));
            OptionsHost = optionsHost ?? throw new ArgumentNullException(nameof(optionsHost));
            ActionsHost = actionsHost ?? throw new ArgumentNullException(nameof(actionsHost));
        }

        public VisualElement FieldsHost { get; }

        public VisualElement OptionsHost { get; }

        public VisualElement ActionsHost { get; }

        public ToolbarToggle AddIconToggle(
            string name,
            string tooltip,
            string fallbackText,
            Texture2D icon,
            bool value,
            Action<bool> changed)
        {
            var toggle = new ToolbarToggle
            {
                name = name,
                text = icon == null ? fallbackText : string.Empty,
                tooltip = tooltip,
                value = value
            };
            toggle.AddToClassList("mx-level-editor__placement-icon-toggle");
            if (icon != null)
            {
                toggle.style.backgroundImage = new StyleBackground(
                    Background.FromTexture2D(icon));
            }
            if (changed != null)
                toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            OptionsHost.Add(toggle);
            return toggle;
        }
    }

    public sealed class MxLevelEditorSelectionTarget
    {
        public MxLevelEditorSelectionTarget(
            string treeNodeId,
            string rowKey)
        {
            TreeNodeId = string.IsNullOrWhiteSpace(treeNodeId) ? "document" : treeNodeId.Trim();
            RowKey = string.IsNullOrWhiteSpace(rowKey) ? string.Empty : rowKey.Trim();
        }

        public string TreeNodeId { get; }

        public string RowKey { get; }
    }

    public sealed class MxLevelEditorContext
    {
        public MxLevelEditorContext(
            MxLevelEditorWindow window,
            IMxLevelEditorModule module,
            Action requestRefresh,
            Action<string, string> requestRevealRow = null)
        {
            Window = window;
            Module = module;
            RequestRefresh = requestRefresh;
            _requestRevealRow = requestRevealRow;
        }

        private readonly Action<string, string> _requestRevealRow;

        public MxLevelEditorWindow Window { get; }

        public IMxLevelEditorModule Module { get; }

        public Action RequestRefresh { get; }

        public string ActiveScenePath => EditorSceneManager.GetActiveScene().path;

        public void RevealRow(string treeNodeId, string rowKey)
        {
            _requestRevealRow?.Invoke(treeNodeId, rowKey);
        }
    }

    public sealed class MxLevelEditorTreeNode
    {
        public MxLevelEditorTreeNode(
            string id,
            string label,
            int depth = 0,
            int count = -1,
            string status = "")
        {
            Id = string.IsNullOrWhiteSpace(id) ? "root" : id.Trim();
            Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
            Depth = Mathf.Max(0, depth);
            Count = count;
            Status = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim();
        }

        public string Id { get; }

        public string Label { get; }

        public int Depth { get; }

        public int Count { get; }

        public string Status { get; }

        public string DisplayText
        {
            get
            {
                string prefix = new string(' ', Depth * 2);
                string count = Count >= 0 ? " (" + Count + ")" : string.Empty;
                string status = string.IsNullOrWhiteSpace(Status) ? string.Empty : " - " + Status;
                return prefix + Label + count + status;
            }
        }
    }

    public sealed class MxLevelEditorListRow
    {
        public MxLevelEditorListRow(
            string kind,
            string group,
            string definition,
            string id,
            string transform,
            string status,
            object payload)
            : this(
                CreateDefaultKey(kind, group, definition, id, transform, status),
                kind,
                group,
                definition,
                id,
                transform,
                status,
                string.Empty,
                payload)
        {
        }

        public MxLevelEditorListRow(
            string key,
            string kind,
            string group,
            string definition,
            string id,
            string transform,
            string status,
            string searchText,
            object payload)
        {
            Key = string.IsNullOrWhiteSpace(key)
                ? CreateDefaultKey(kind, group, definition, id, transform, status)
                : key.Trim();
            Kind = Normalize(kind);
            Group = Normalize(group);
            Definition = Normalize(definition);
            Id = Normalize(id);
            Transform = Normalize(transform);
            Status = Normalize(status);
            SearchText = Normalize(searchText);
            Payload = payload;
        }

        public string Key { get; }

        public string Kind { get; }

        public string Group { get; }

        public string Definition { get; }

        public string Id { get; }

        public string Transform { get; }

        public string Status { get; }

        public string SearchText { get; }

        public object Payload { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string CreateDefaultKey(
            string kind,
            string group,
            string definition,
            string id,
            string transform,
            string status)
        {
            return Normalize(kind)
                + "|"
                + Normalize(group)
                + "|"
                + Normalize(definition)
                + "|"
                + Normalize(id)
                + "|"
                + Normalize(transform)
                + "|"
                + Normalize(status);
        }
    }

    public static class MxLevelEditorModuleRegistry
    {
        private static readonly List<IMxLevelEditorModule> ModulesInternal =
            new List<IMxLevelEditorModule>();

        public static IReadOnlyList<IMxLevelEditorModule> Modules => ModulesInternal;

        public static void Register(IMxLevelEditorModule module)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.ModuleId))
                return;

            for (int i = 0; i < ModulesInternal.Count; i++)
            {
                if (string.Equals(ModulesInternal[i].ModuleId, module.ModuleId, StringComparison.Ordinal))
                {
                    ModulesInternal[i] = module;
                    return;
                }
            }

            ModulesInternal.Add(module);
            ModulesInternal.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
        }
    }

    /// <summary>
    /// Stores low-frequency, editor-only workspace projections.
    /// Registration does not add save participants or document owners.
    /// Allocation behavior: LowFreqAlloc during editor domain initialization.
    /// </summary>
    public static class MxLevelEditorWorkspaceRegistry
    {
        private static readonly List<IMxLevelEditorWorkspace>
            WorkspacesInternal = new List<IMxLevelEditorWorkspace>();

        /// <summary>
        /// Gets registered workspace projections in registration order.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        public static IReadOnlyList<IMxLevelEditorWorkspace> Workspaces =>
            WorkspacesInternal;

        /// <summary>
        /// Registers or replaces a workspace by stable ID.
        /// Allocation behavior: LowFreqAlloc during editor initialization.
        /// </summary>
        public static void Register(IMxLevelEditorWorkspace workspace)
        {
            if (workspace == null
                || string.IsNullOrWhiteSpace(workspace.WorkspaceId)
                || string.IsNullOrWhiteSpace(workspace.OwnerModuleId))
            {
                return;
            }

            for (int i = 0; i < WorkspacesInternal.Count; i++)
            {
                if (string.Equals(
                        WorkspacesInternal[i].WorkspaceId,
                        workspace.WorkspaceId,
                        StringComparison.Ordinal))
                {
                    WorkspacesInternal[i] = workspace;
                    return;
                }
            }

            WorkspacesInternal.Add(workspace);
        }

        /// <summary>
        /// Finds a workspace by stable ID.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        public static IMxLevelEditorWorkspace Find(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return null;

            for (int i = 0; i < WorkspacesInternal.Count; i++)
            {
                IMxLevelEditorWorkspace workspace = WorkspacesInternal[i];
                if (string.Equals(
                        workspace.WorkspaceId,
                        workspaceId,
                        StringComparison.Ordinal))
                {
                    return workspace;
                }
            }

            return null;
        }
    }
}
