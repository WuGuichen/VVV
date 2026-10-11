using System.Collections.Generic;
using MxFramework.Config.Levels;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    /// <summary>Optional project-owned document workflow inside the existing Level Editor.</summary>
    public interface IMxLevelEditorDocumentOwner
    {
        /// <summary>Allocation behavior: NoAlloc. Indicates that this owner supplies the current document workflow.</summary>
        bool AppliesToActiveScene { get; }
        /// <summary>Allocation behavior: NoAlloc. Includes document settings owned outside individual modules.</summary>
        bool IsDirty { get; }
        /// <summary>Allocation behavior: AllocByDesign. Builds the fixed document toolbar.</summary>
        void BuildToolbar(MxLevelEditorWindow window, VisualElement host);
        /// <summary>Allocation behavior: LowFreqAlloc. Saves the exact current document or retains its draft on failure.</summary>
        bool TrySave(IReadOnlyList<IMxLevelEditorModule> modules, out string message);
        /// <summary>Allocation behavior: LowFreqAlloc. Restores document settings after explicit discard.</summary>
        void ReloadSavedDocument();
    }

    /// <summary>Validates the whole selected document. Allocation behavior: LowFreqAlloc.</summary>
    public interface IMxLevelEditorDocumentValidationOwner
    {
        bool TryValidateDocument(out string message);
    }

    /// <summary>Optional scoped discard that preserves unrelated Scene drafts. Allocation behavior: LowFreqAlloc.</summary>
    public interface IMxLevelEditorScopedDocumentOwner
    {
        bool TryDiscardDraft(out string message);
    }

    /// <summary>Retires document-specific Undo callbacks before rebinding a reused module. Allocation behavior: NoAlloc.</summary>
    public interface IMxLevelEditorDocumentUndoBoundary
    {
        void RetireDocumentUndo();
    }

    /// <summary>Exports the current module draft without saving a file or scene.</summary>
    public interface IMxLevelEditorDraftProvider
    {
        /// <summary>Allocation behavior: LowFreqAlloc. Returns an isolated module snapshot.</summary>
        IReadOnlyList<MxLevelAuthoringModuleDocument> CaptureDraftModules();
    }

    /// <summary>Explicit decision used by the document switch transaction.</summary>
    public enum MxLevelEditorUnsavedDecision { Save, Discard, Cancel }

    /// <summary>Editor extension registration; the package never depends on a project implementation.</summary>
    public static class MxLevelEditorDocumentOwnerRegistry
    {
        public static IMxLevelEditorDocumentOwner Owner { get; private set; }
        /// <summary>Allocation behavior: NoAlloc. Registers the project's existing document owner.</summary>
        public static void Register(IMxLevelEditorDocumentOwner owner) => Owner = owner;
    }
}
