using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        internal JObject CaptureReloadState()
            => new JObject { ["dirty"] = _dirty, ["sceneDirty"] = _sceneDirty, ["undoBaseline"] = _undoSession?.SavedBaseline };
        internal void RestoreReloadState(MxLevelAuthoringDocument envelope, JObject state)
        {
            EnsureLoaded(); _undoSession?.Retire(); _undoSession = null;
            _envelope = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(envelope));
            _document = ExtractDocument(_envelope); _loadedDocumentPath = DocumentPath;
            _dirty = (bool)state["dirty"]; _sceneDirty = (bool)state["sceneDirty"];
            EnsureUndoSession();
            if (state["undoBaseline"]?.Type == JTokenType.String) _undoSession.SetSavedBaseline((string)state["undoBaseline"]);
            _report = ValidateLoadedDocument(); RebuildRows();
        }
    }
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        internal JObject CaptureReloadState()
            => new JObject { ["dirty"] = _dirty, ["sceneDirty"] = _sceneDirty, ["triggerDirty"] = _triggerDirty,
                ["undoBaseline"] = _undoSession?.SavedBaseline, ["definitions"] = CaptureDefinitionReloadState() };
        internal void RestoreReloadState(MxLevelAuthoringDocument envelope, JObject state)
        {
            EnsureLoaded(); _undoSession?.Retire(); _undoSession = null;
            RestoreDocumentSnapshot(MxLevelAuthoringJson.SaveToJson(envelope)); _loadedDocumentPath = DocumentPath;
            _dirty = (bool)state["dirty"]; _sceneDirty = (bool)state["sceneDirty"]; _triggerDirty = (bool)state["triggerDirty"];
            EnsureUndoSession();
            if (state["undoBaseline"]?.Type == JTokenType.String) _undoSession.SetSavedBaseline((string)state["undoBaseline"]);
            RestoreDefinitionReloadState(state["definitions"] as JObject);
            _report = ValidateLoadedDocument(); RebuildRows();
        }
    }
    internal sealed partial class CharacterTestGuidanceLevelEditorModule
    {
        internal JObject CaptureReloadState() => new JObject { ["dirty"] = _dirty, ["undoBaseline"] = _undoSession?.SavedBaseline,
            ["identity"] = _documentIdentity.Capture() };
        internal void RestoreReloadState(MxLevelAuthoringDocument envelope, JObject state)
        {
            string sourcePath = ToFullPath(DocumentPath);
            var restoredIdentity = new CharacterTestLevelEditorModuleDocumentIdentity();
            restoredIdentity.Restore(state["identity"] as JObject, DocumentPath, envelope,
                System.IO.File.Exists(sourcePath) ? System.IO.File.ReadAllText(sourcePath) : null);
            EnsureLoaded(); _undoSession?.Retire(); _undoSession = null;
            _documentIdentity.Restore(restoredIdentity.Capture(), DocumentPath, envelope, null);
            _envelope = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(envelope));
            if (!CharacterTestGuidanceAuthoringAdapter.TryExtract(_envelope, out var document, out _loadDiagnostic))
                document = CharacterTestGuidanceAuthoringAdapter.CreateEmptyDocument();
            _document = document.Normalize(); _dirty = (bool)state["dirty"];
            EnsureUndoSession();
            if (state["undoBaseline"]?.Type == JTokenType.String) _undoSession.SetSavedBaseline((string)state["undoBaseline"]);
            EnsureSelectedStep(); _report = CharacterTestGuidanceAuthoringValidator.Validate(_document); RefreshEditabilityProjection();
        }
    }
    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule
    {
        internal JObject CaptureReloadState() => new JObject { ["dirty"] = _dirty, ["undoBaseline"] = _undoSession?.SavedBaseline,
            ["identity"] = _documentIdentity.Capture() };
        internal void RestoreReloadState(MxLevelAuthoringDocument envelope, JObject state)
        {
            string sourcePath = ToFullPath(DocumentPath);
            var restoredIdentity = new CharacterTestLevelEditorModuleDocumentIdentity();
            restoredIdentity.Restore(state["identity"] as JObject, DocumentPath, envelope,
                System.IO.File.Exists(sourcePath) ? System.IO.File.ReadAllText(sourcePath) : null);
            EnsureLoaded(); _undoSession?.Retire(); _undoSession = null;
            _documentIdentity.Restore(restoredIdentity.Capture(), DocumentPath, envelope, null);
            _envelope = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(envelope));
            _document = CharacterTestGenesisValidationAuthoringAdapter.Extract(_envelope); _dirty = (bool)state["dirty"];
            RefreshOwnerDocuments(); EnsureUndoSession();
            if (state["undoBaseline"]?.Type == JTokenType.String) _undoSession.SetSavedBaseline((string)state["undoBaseline"]);
            _report = ValidateDocument();
        }
    }
}
