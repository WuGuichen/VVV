using System.Collections.Generic;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule : IMxLevelEditorDraftProvider, IMxLevelEditorSceneSaveSnapshotProvider, IMxLevelEditorDocumentUndoBoundary
    {
        public void RetireDocumentUndo() { _undoSession?.Retire(); _undoSession = null; }

        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            var owner = FindSceneCacheTarget()?.DirtyObject;
            if (owner != null) output.Add(owner);
        }

        public IReadOnlyList<MxLevelAuthoringModuleDocument> CaptureDraftModules()
        {
            EnsureLoaded(); SyncEnvelopeFromDocument();
            return _envelope.Modules.Where(m => m.ModuleId == ModuleId || m.ModuleId == "characterTest.gameplayTriggers")
                .Select(m => m.Normalize()).ToArray();
        }
    }

    public sealed partial class CharacterTestSceneCharacterLevelEditorModule : IMxLevelEditorDraftProvider, IMxLevelEditorSceneSaveSnapshotProvider, IMxLevelEditorDocumentUndoBoundary
    {
        public void RetireDocumentUndo() { _undoSession?.Retire(); _undoSession = null; }

        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            var owner = FindGameManager();
            if (owner != null) output.Add(owner);
        }

        public IReadOnlyList<MxLevelAuthoringModuleDocument> CaptureDraftModules()
        {
            EnsureLoaded();
            var envelope = CaptureReadOnlyCharacterEnvelope();
            return envelope.Modules.Where(m => m.ModuleId == ModuleId).Select(m => m.Normalize()).ToArray();
        }
    }

    internal sealed partial class CharacterTestGuidanceLevelEditorModule : IMxLevelEditorDraftProvider, IMxLevelEditorSceneSaveSnapshotProvider, IMxLevelEditorDocumentUndoBoundary
    {
        public void RetireDocumentUndo() { _undoSession?.Retire(); _undoSession = null; }

        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            var owner = FindGameManager();
            if (owner != null) output.Add(owner);
        }

        public IReadOnlyList<MxLevelAuthoringModuleDocument> CaptureDraftModules()
        {
            EnsureLoaded();
            var envelope = CharacterTestGuidanceAuthoringAdapter.Upsert(_envelope, _document);
            return envelope.Modules.Where(m => m.ModuleId == ModuleId).Select(m => m.Normalize()).ToArray();
        }
    }

    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule : IMxLevelEditorDraftProvider, IMxLevelEditorSceneSaveSnapshotProvider, IMxLevelEditorDocumentUndoBoundary
    {
        public void RetireDocumentUndo() { _undoSession?.Retire(); _undoSession = null; }

        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            var owner = FindGameManager();
            if (owner != null) output.Add(owner);
        }

        public IReadOnlyList<MxLevelAuthoringModuleDocument> CaptureDraftModules()
        {
            EnsureLoaded();
            var envelope = CharacterTestGenesisValidationAuthoringAdapter.Upsert(_envelope, _document);
            return envelope.Modules.Where(m => m.ModuleId == ModuleId).Select(m => m.Normalize()).ToArray();
        }
    }
}
