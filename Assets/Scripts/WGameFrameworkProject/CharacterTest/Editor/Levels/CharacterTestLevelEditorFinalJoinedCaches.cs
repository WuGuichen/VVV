using System;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        internal void ApplyFinalJoinedCache(MxLevelAuthoringDocument final)
        {
            _envelope = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(final));
            _document = ExtractDocument(_envelope);
            LoadTriggerDocumentFromEnvelope();
            var target = FindSceneCacheTarget();
            // The persisted envelope is the hash authority. Draft synchronization would
            // regenerate Scene/root references here and silently change its content hash.
            if (!TryCompilePreparedSceneCaches(target, out var triggers, out var failure, synchronizeEnvelope: false)) throw new InvalidOperationException(failure);
            Undo.RecordObject(target.DirtyObject, "同步最终关卡世界缓存");
            bool applied = target.IsGameManager
                ? CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToCache(_envelope, target.Cache, target.Root, ActiveDefinitionCatalog, DocumentPath, out _report)
                : CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToSource(_envelope, target.LegacySource, ActiveDefinitionCatalog, DocumentPath, out _report);
            if (!applied) throw new InvalidOperationException(BuildValidationBlockedStatus("最终缓存"));
            if (target.IsGameManager) target.GameManager.GameplayTriggerSceneCache.Configure(triggers);
            EditorUtility.SetDirty(target.DirtyObject); EditorSceneManager.MarkSceneDirty(target.GameObject.scene);
        }
    }
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        internal void ApplyFinalJoinedCache(MxLevelAuthoringDocument final)
        {
            _envelope = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(final));
            var manager = FindGameManager() ?? throw new InvalidOperationException("Final character cache target missing.");
            Undo.RecordObject(manager, "同步最终关卡角色缓存");
            if (!CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(_envelope, manager.CharacterSceneCache,
                    manager.WorldObjectSceneRoot, DocumentPath, out _report)) throw new InvalidOperationException(BuildValidationBlockedStatus("最终缓存"));
            EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }
}
