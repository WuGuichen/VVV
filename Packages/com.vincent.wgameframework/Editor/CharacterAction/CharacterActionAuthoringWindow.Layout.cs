using System.Globalization;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private int _uiRefreshDepth;

        private void Rebuild()
        {
            RefreshUi(UiRefreshScope.All);
        }

        private void RefreshUi(UiRefreshScope scope)
        {
            CharacterActionAuthoringHistorySnapshot[] undoHistory = _undoHistory.ToArray();
            CharacterActionAuthoringHistorySnapshot[] redoHistory = _redoHistory.ToArray();
            _uiRefreshDepth++;
            try
            {
                RefreshUiCore(scope);
            }
            finally
            {
                _uiRefreshDepth--;
                // UI projection can rebuild selectors and replay their current values.
                // Those callbacks may navigate the document graph, but rendering the
                // current state must never create or reset an edit-history boundary.
                _undoHistory.Clear();
                _undoHistory.AddRange(undoHistory);
                _redoHistory.Clear();
                _redoHistory.AddRange(redoHistory);
            }
        }

        private void RefreshUiCore(UiRefreshScope scope)
        {
            if (_model == null)
                _model = CharacterActionAuthoringModel.CreateDefault();

            if (!_shellBuilt || _contentHost == null)
            {
                if (!_shellBuilt)
                    BuildShell();
                scope = UiRefreshScope.All;
            }

            if (scope == UiRefreshScope.All)
            {
                EnsureValidationForCurrentTab();
                RefreshChrome();
                RefreshWorkbenchAll();
                ClearActionTimelineWhenSelectionEmpty();
                RefreshFooter();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.Chrome)
            {
                RefreshChrome();
                RefreshFooter();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.ActionList)
            {
                RebuildWorkbenchActionNavigation();
                RefreshActionTimelineForCurrentSelection();
                RebuildWorkbenchActionInspector();
                RefreshChrome();
                RefreshFooter();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.ActionDetail)
            {
                RebuildWorkbenchActionNavigation();
                RefreshActionTimelineForCurrentSelection();
                RebuildWorkbenchActionInspector();
                RefreshChrome();
                RefreshFooter();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.Bindings)
            {
                RebuildMappingWorkspaceContent();
                RebuildWorkbenchActionNavigation();
                RefreshChrome();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.Tracks)
            {
                RefreshActionTimelineForCurrentSelection();
                RebuildWorkbenchActionNavigation();
                RebuildWorkbenchActionInspector();
                RefreshChrome();
                RefreshFooter();
                SaveSessionState();
                return;
            }

            if (scope == UiRefreshScope.Content)
            {
                EnsureValidationForCurrentTab();
                RefreshCurrentWorkbenchWorkspace();
                if (_workspace == AuthoringWorkspace.ActionComposition)
                    ClearActionTimelineWhenSelectionEmpty();
                RefreshChrome();
                RefreshFooter();
                SaveSessionState();
            }
        }

        private void RefreshActionTimelineForCurrentSelection()
        {
            if (_timelineView == null)
                return;
            if (_model?.SelectedAction != null)
            {
                RebuildTracks(rebuildInspector: false);
                return;
            }

            _timelinePlayback?.Pause();
            DisposeTimelinePreviewSession();
            _pendingTimelineSelectedItemId = string.Empty;
            _documentSession?.SelectTimelineItem(string.Empty);
            _timelineView.Bind(null);
            if (_timelinePlayback != null && _timelineView.Snapshot != null)
            {
                _timelinePlayback.SetDuration(_timelineView.Snapshot.DurationFrames);
                _timelinePlayback.Seek(0);
            }
            _previewViewport?.ClearPreview();
            UpdateWorkbenchPreviewFrameLabel();
            RefreshWorkbenchTransportState();
        }

        private void ClearActionTimelineWhenSelectionEmpty()
        {
            if (_model?.SelectedAction == null)
                RefreshActionTimelineForCurrentSelection();
        }

        private void SetTab(AuthoringTab tab)
        {
            _tab = tab;
            switch (tab)
            {
                case AuthoringTab.Actions:
                case AuthoringTab.Tracks:
                    SetWorkspace(AuthoringWorkspace.ActionComposition);
                    break;
                case AuthoringTab.Projectiles:
                    _sharedResourceSection = SharedResourceSection.AbilityEffectTimeline;
                    SetWorkspace(AuthoringWorkspace.SharedResources);
                    break;
                case AuthoringTab.Profile:
                    _mappingSection = MappingWorkspaceSection.ProfileComposition;
                    SetWorkspace(AuthoringWorkspace.Mappings);
                    break;
                case AuthoringTab.References:
                    _mappingSection = MappingWorkspaceSection.ReferenceSources;
                    SetWorkspace(AuthoringWorkspace.Mappings);
                    break;
                case AuthoringTab.Validation:
                    _mappingSection = MappingWorkspaceSection.ActionSet;
                    SetWorkspace(AuthoringWorkspace.Mappings);
                    _problemsExpanded = true;
                    EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
                    RefreshProblemsWorkbench();
                    break;
                default:
                    _mappingSection = MappingWorkspaceSection.ActionSet;
                    SetWorkspace(AuthoringWorkspace.Mappings);
                    break;
            }
        }

        private bool IsFoldoutExpanded(string sectionKey, bool defaultExpanded)
        {
            if (_foldoutExpanded.TryGetValue(sectionKey, out bool expanded))
                return expanded;
            return defaultExpanded;
        }

        private void SetFoldoutExpanded(string sectionKey, bool expanded)
        {
            _foldoutExpanded[sectionKey] = expanded;
            SaveSessionState();
        }

        private Foldout CreateSectionFoldout(VisualElement parent, string sectionKey, string title, bool defaultExpanded)
        {
            var foldout = new Foldout
            {
                text = title,
                value = IsFoldoutExpanded(sectionKey, defaultExpanded),
            };
            foldout.AddToClassList("ca-foldout");
            foldout.RegisterValueChangedCallback(evt => SetFoldoutExpanded(sectionKey, evt.newValue));
            parent.Add(foldout);
            return foldout;
        }

        private string GetTracksFoldoutKey(string sectionName)
        {
            string actionId = _model?.SelectedAction?.ActionId.ToString(CultureInfo.InvariantCulture) ?? "_none";
            return actionId + ":" + sectionName;
        }
    }
}
