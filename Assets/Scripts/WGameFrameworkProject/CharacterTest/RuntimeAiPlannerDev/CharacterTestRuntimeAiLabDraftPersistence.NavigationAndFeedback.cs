namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftPersistence
    {
        /// <summary>
        /// Reloads the last successfully persisted snapshot for the active workspace. It never
        /// deletes the saved draft and never writes a new version.
        /// </summary>
        internal bool TryDiscardUnsavedChanges(
            out CharacterTestRuntimeAiLabDraftWorkspace restored,
            out string diagnostic)
        {
            restored = null;
            CharacterTestRuntimeAiLabDraftWorkspace current = _workspace;
            if (current == null)
            {
                diagnostic = "Runtime AI Lab workspace is unavailable.";
                return false;
            }
            if (!current.Dirty)
            {
                restored = current;
                diagnostic = "No unsaved Runtime AI Lab changes were present.";
                return true;
            }
            if (current.LastSavedRevision <= 0)
            {
                diagnostic = "The active workspace has no saved snapshot to restore.";
                return false;
            }
            if (!TryLoad(current.WorkspaceId, out restored, out diagnostic)
                || restored == null)
            {
                return false;
            }

            _workspace = restored;
            _saveDueAt = double.MaxValue;
            LastStatus = restored.RecoveredFromPrevious
                ? "Unsaved changes were discarded; the previous recovery snapshot was restored."
                : "Unsaved Runtime AI Lab changes were discarded.";
            diagnostic = LastStatus;
            return true;
        }
    }
}
