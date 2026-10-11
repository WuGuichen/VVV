using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Tracks content history separately from transient window state.</summary>
    public sealed class ProjectileAuthoringDocumentSession
    {
        private const int Capacity = 64;
        private readonly List<Snapshot> history = new List<Snapshot>();
        private int cursor = -1;

        public string PackagePath { get; set; } = string.Empty;
        public string WorkspaceHash { get; set; } = string.Empty;
        public string SelectedStableId { get; set; } = string.Empty;
        public string Filter { get; set; } = string.Empty;
        public string MotionFilter { get; set; } = "All Motion";
        public string ImpactFilter { get; set; } = "All Impact";
        public string StatusFilter { get; set; } = "All Status";
        public bool UsedOnly { get; set; }
        public bool SharedOnly { get; set; }
        public bool DirtyOnly { get; set; }
        public bool InvalidOnly { get; set; }
        public float NavigationPaneSize { get; set; } = 330f;
        public float InspectorPaneSize { get; set; } = 360f;
        public string[] ExpandedStableIds { get; set; } = Array.Empty<string>();
        public string DraftPath { get; private set; } = string.Empty;
        public ProjectileAuthoringMutationRequestView CurrentMutation { get; private set; }
        public IReadOnlyList<ProjectileAuthoringMutationRequestView> PendingCommands => cursor >= 0 ? history[cursor].Commands : Array.Empty<ProjectileAuthoringMutationRequestView>();
        public bool HasExternalChanges { get; private set; }
        public IReadOnlyCollection<string> DirtySources => cursor >= 0 ? history[cursor].DirtySources : Array.Empty<string>();
        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor >= 0 && cursor < history.Count - 1;

        public void Reset(string workspaceHash)
        {
            WorkspaceHash = workspaceHash ?? string.Empty;
            history.Clear(); cursor = -1; HasExternalChanges = false;
            PushComposite(string.Empty, Array.Empty<string>(), Array.Empty<ProjectileAuthoringMutationRequestView>());
        }

        public void Push(string draftPath, IEnumerable<string> dirtySources, ProjectileAuthoringMutationRequestView mutation = null)
        {
            var commands = cursor >= 0 ? history[cursor].Commands.Select(Clone).ToList() : new List<ProjectileAuthoringMutationRequestView>();
            if (mutation != null) commands.Add(Clone(mutation));
            PushComposite(draftPath, dirtySources, commands);
        }

        public void PushComposite(string draftPath, IEnumerable<string> dirtySources, IEnumerable<ProjectileAuthoringMutationRequestView> commands)
        {
            if (cursor < history.Count - 1) history.RemoveRange(cursor + 1, history.Count - cursor - 1);
            history.Add(new Snapshot(draftPath, dirtySources, commands));
            if (history.Count > Capacity) history.RemoveAt(0); else cursor++;
            cursor = history.Count - 1;
            DraftPath = draftPath ?? string.Empty;
            CurrentMutation = history[cursor].Commands.LastOrDefault();
        }

        public void UpdateCurrentDraft(string draftPath, IEnumerable<string> dirtySources)
        {
            if (cursor < 0) return;
            history[cursor].DraftPath = draftPath ?? string.Empty;
            history[cursor].DirtySources = new HashSet<string>(dirtySources ?? Array.Empty<string>(), StringComparer.Ordinal);
            Restore();
        }

        public void Undo() { if (CanUndo) { cursor--; Restore(); } }
        public void Redo() { if (CanRedo) { cursor++; Restore(); } }
        public void MarkExternalChanged(bool changed) { HasExternalChanges = changed; }
        public bool CanSave => DirtySources.Count > 0 && !HasExternalChanges && !string.IsNullOrEmpty(DraftPath);

        public SessionState CaptureState()
        {
            return new SessionState
            {
                WorkspaceHash = WorkspaceHash,
                Cursor = cursor,
                SelectedStableId = SelectedStableId,
                Filter = Filter,
                MotionFilter = MotionFilter,
                ImpactFilter = ImpactFilter,
                StatusFilter = StatusFilter,
                UsedOnly = UsedOnly,
                SharedOnly = SharedOnly,
                DirtyOnly = DirtyOnly,
                InvalidOnly = InvalidOnly,
                NavigationPaneSize = NavigationPaneSize,
                InspectorPaneSize = InspectorPaneSize,
                ExpandedStableIds = ExpandedStableIds ?? Array.Empty<string>(),
                History = history.Select(value => value.ToState()).ToArray()
            };
        }

        public void RestoreState(SessionState state)
        {
            if (state == null) return;
            WorkspaceHash = state.WorkspaceHash ?? string.Empty;
            SelectedStableId = state.SelectedStableId ?? string.Empty;
            Filter = state.Filter ?? string.Empty;
            MotionFilter = string.IsNullOrEmpty(state.MotionFilter) ? "All Motion" : state.MotionFilter;
            ImpactFilter = string.IsNullOrEmpty(state.ImpactFilter) ? "All Impact" : state.ImpactFilter;
            StatusFilter = string.IsNullOrEmpty(state.StatusFilter) ? "All Status" : state.StatusFilter;
            UsedOnly = state.UsedOnly; SharedOnly = state.SharedOnly; DirtyOnly = state.DirtyOnly; InvalidOnly = state.InvalidOnly;
            NavigationPaneSize = state.NavigationPaneSize > 0 ? state.NavigationPaneSize : 330f;
            InspectorPaneSize = state.InspectorPaneSize > 0 ? state.InspectorPaneSize : 360f;
            ExpandedStableIds = state.ExpandedStableIds ?? Array.Empty<string>();
            history.Clear();
            foreach (SnapshotState item in state.History ?? Array.Empty<SnapshotState>()) history.Add(Snapshot.FromState(item));
            cursor = history.Count == 0 ? -1 : Math.Max(0, Math.Min(state.Cursor, history.Count - 1));
            if (cursor < 0) PushComposite(string.Empty, Array.Empty<string>(), Array.Empty<ProjectileAuthoringMutationRequestView>()); else Restore();
        }

        private void Restore() { DraftPath = history[cursor].DraftPath; CurrentMutation = history[cursor].Commands.LastOrDefault(); }
        private static ProjectileAuthoringMutationRequestView Clone(ProjectileAuthoringMutationRequestView value) => value == null ? null : UnityEngine.JsonUtility.FromJson<ProjectileAuthoringMutationRequestView>(UnityEngine.JsonUtility.ToJson(value));

        private sealed class Snapshot
        {
            public Snapshot(string draftPath, IEnumerable<string> dirty, IEnumerable<ProjectileAuthoringMutationRequestView> commands)
            {
                DraftPath = draftPath ?? string.Empty;
                DirtySources = new HashSet<string>(dirty ?? Array.Empty<string>(), StringComparer.Ordinal);
                Commands = (commands ?? Array.Empty<ProjectileAuthoringMutationRequestView>()).Select(Clone).ToArray();
            }
            public string DraftPath { get; set; }
            public HashSet<string> DirtySources { get; set; }
            public ProjectileAuthoringMutationRequestView[] Commands { get; }
            public SnapshotState ToState() => new SnapshotState { DirtySources = DirtySources.ToArray(), Commands = Commands.Select(Clone).ToArray() };
            public static Snapshot FromState(SnapshotState value) => new Snapshot(string.Empty, value?.DirtySources, value?.Commands);
        }

        [Serializable]
        public sealed class SessionState
        {
            public string WorkspaceHash;
            public int Cursor;
            public string SelectedStableId;
            public string Filter;
            public string MotionFilter;
            public string ImpactFilter;
            public string StatusFilter;
            public bool UsedOnly;
            public bool SharedOnly;
            public bool DirtyOnly;
            public bool InvalidOnly;
            public float NavigationPaneSize;
            public float InspectorPaneSize;
            public string[] ExpandedStableIds = Array.Empty<string>();
            public SnapshotState[] History = Array.Empty<SnapshotState>();
        }

        [Serializable]
        public sealed class SnapshotState
        {
            public string[] DirtySources = Array.Empty<string>();
            public ProjectileAuthoringMutationRequestView[] Commands = Array.Empty<ProjectileAuthoringMutationRequestView>();
        }
    }
}
