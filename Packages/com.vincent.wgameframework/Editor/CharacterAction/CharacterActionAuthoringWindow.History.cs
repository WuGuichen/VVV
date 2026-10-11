using System.Collections.Generic;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void UndoEdit()
        {
            if (_undoHistory.Count == 0)
                return;

            CharacterActionAuthoringHistorySnapshot snapshot = PopHistory(_undoHistory);
            PushRedoSnapshot(snapshot.RestoreAuthoringFilesOnApply);
            RestoreHistorySnapshot(snapshot);
            RecordCurrentActionRecentActivity();
        }

        private void RedoEdit()
        {
            if (_redoHistory.Count == 0)
                return;

            CharacterActionAuthoringHistorySnapshot snapshot = PopHistory(_redoHistory);
            PushUndoSnapshot(
                clearRedo: false,
                restoreAuthoringFilesOnApply: snapshot.RestoreAuthoringFilesOnApply);
            RestoreHistorySnapshot(snapshot);
            RecordCurrentActionRecentActivity();
        }

        private void RestoreHistorySnapshot(CharacterActionAuthoringHistorySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            CharacterActionAuthoringModel currentModel = _model;
            if (snapshot.RestoreAuthoringFilesOnApply)
                snapshot.RestoreMechanicTuningFiles();
            RestorePendingMechanicTuningDrafts(snapshot.CreatePendingMechanicTuningDraftClones());
            RestoreAbilityEffectTimelineHistorySnapshot(snapshot);
            CharacterActionAuthoringModel restoredModel = snapshot.CreateModelClone();
            _model = restoredModel;
            TouchWorkbenchPreviewRevision();
            _validationDirty = true;
            ScheduleWorkbenchValidation();
            if (_documentSession != null)
            {
                // Stable ids are editable. Resolve the exact live DTO slots before
                // replacing them with snapshot clones; an old-id lookup loses renamed
                // DTOs and is ambiguous for equal reaction ids in separate bundles.
                ApplyHistoryModelToDocumentSession(restoredModel, currentModel);
            }
            else
            {
                _profileDirty = true;
                _actionSetDirty = true;
                _actionLibraryDirty = true;
            }
            RefreshUi(UiRefreshScope.All);
        }

        private void PushUndoSnapshot(
            bool clearRedo = true,
            bool restoreAuthoringFilesOnApply = false)
        {
            if (_model == null)
                return;

            _undoHistory.Add(CharacterActionAuthoringHistorySnapshot.Capture(
                _model,
                pendingMechanicTuningDrafts: _pendingMechanicTuningDrafts,
                abilityAuthoringJsonPath: _abilityAuthoringJsonPath,
                abilityAuthoringRootJson: SerializeAbilityEffectTimelineRootForHistory(),
                abilityEffectTimelinesJson: SerializeAbilityEffectTimelineDraftsForSession(),
                selectedAbilityEffectTimelineId: _selectedAbilityEffectTimelineId,
                selectedAbilityEffectTimelineItemId: _pendingAbilityEffectTimelineSelectedItemId,
                abilityEffectTimelineDirty: _abilityEffectTimelineDirty,
                restoreAuthoringFilesOnApply: restoreAuthoringFilesOnApply));
            TrimHistory(_undoHistory);
            if (clearRedo)
                _redoHistory.Clear();
        }

        private void PushRedoSnapshot(bool restoreAuthoringFilesOnApply)
        {
            if (_model == null)
                return;

            _redoHistory.Add(CharacterActionAuthoringHistorySnapshot.Capture(
                _model,
                pendingMechanicTuningDrafts: _pendingMechanicTuningDrafts,
                abilityAuthoringJsonPath: _abilityAuthoringJsonPath,
                abilityAuthoringRootJson: SerializeAbilityEffectTimelineRootForHistory(),
                abilityEffectTimelinesJson: SerializeAbilityEffectTimelineDraftsForSession(),
                selectedAbilityEffectTimelineId: _selectedAbilityEffectTimelineId,
                selectedAbilityEffectTimelineItemId: _pendingAbilityEffectTimelineSelectedItemId,
                abilityEffectTimelineDirty: _abilityEffectTimelineDirty,
                restoreAuthoringFilesOnApply: restoreAuthoringFilesOnApply));
            TrimHistory(_redoHistory);
        }

        private void RemoveLastUndoSnapshot()
        {
            if (_undoHistory.Count > 0)
                _undoHistory.RemoveAt(_undoHistory.Count - 1);
        }

        private static CharacterActionAuthoringHistorySnapshot PopHistory(
            List<CharacterActionAuthoringHistorySnapshot> history)
        {
            int index = history.Count - 1;
            CharacterActionAuthoringHistorySnapshot snapshot = history[index];
            history.RemoveAt(index);
            return snapshot;
        }

        private static void TrimHistory(List<CharacterActionAuthoringHistorySnapshot> history)
        {
            while (history.Count > MaxEditHistory)
                history.RemoveAt(0);
        }
    }
}
