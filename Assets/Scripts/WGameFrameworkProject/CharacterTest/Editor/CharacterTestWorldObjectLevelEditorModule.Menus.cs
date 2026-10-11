using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        [MenuItem("MxFramework/Level Editor/CharacterTest/Export Active Scene WorldObjects To Default JSON")]
        public static void ExportCurrentSceneSourceToDefaultJson()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                Debug.LogWarning("CharacterTest Level Editor: no CharacterTest GameManager scene cache target found.");
                return;
            }

            LevelWorldObjectAuthoringDocument document = target.IsGameManager
                ? CharacterTestWorldObjectAuthoringAdapter.CreateDocumentFromCache(target.Cache, target.Root)
                : CharacterTestWorldObjectAuthoringAdapter.CreateDocumentFromSource(target.LegacySource);
            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(document, target.Root);
            WriteEnvelope(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath, envelope);
            AssetDatabase.ImportAsset(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            Debug.Log(
                "CharacterTest Level Editor: exported world object JSON to "
                + CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath
                + ".");
        }

        [MenuItem("MxFramework/Level Editor/CharacterTest/Apply Default JSON To Active Scene Source")]
        public static void ApplyDefaultJsonToActiveSceneSource()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                Debug.LogWarning("CharacterTest Level Editor: no CharacterTest GameManager scene cache target found.");
                return;
            }

            string path = CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath;
            string fullPath = ToFullPath(path);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning("CharacterTest Level Editor: JSON does not exist: " + path);
                return;
            }

            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            CharacterTestGameplayTriggerSceneCacheData triggerCacheData = null;
            if (target.IsGameManager)
            {
                if (!CharacterTestWorldObjectAuthoringAdapter
                        .TryExtractWorldObjectsDocument(
                            envelope,
                            out LevelWorldObjectAuthoringDocument worldObjects,
                            out LevelWorldObjectAuthoringValidationReport
                                worldObjectReport)
                    || worldObjectReport.HasErrors)
                {
                    Debug.LogWarning(
                        "CharacterTest Level Editor: runtime cache preflight failed with errors="
                        + (worldObjectReport?.ErrorCount ?? 0)
                        + ".");
                    return;
                }
                if (!CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                        envelope,
                        worldObjects,
                        target.Root,
                        CharacterTestWorldObjectDefinitionCatalogLoader
                            .LoadDefaultOrBuiltIn()
                            .Catalog,
                        out triggerCacheData,
                        out LevelWorldObjectAuthoringValidationReport
                            triggerCacheReport))
                {
                    Debug.LogWarning(
                        "CharacterTest Level Editor: runtime cache preflight failed with errors="
                        + (triggerCacheReport?.ErrorCount ?? 0)
                        + ".");
                    return;
                }
            }
            Undo.RecordObject(target.DirtyObject, "Apply CharacterTest WorldObject JSON");
            LevelWorldObjectAuthoringValidationReport report;
            bool applied;
            if (target.IsGameManager)
            {
                applied = CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToCache(
                    envelope,
                    target.Cache,
                    target.Root,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog,
                    path,
                    out report);
            }
            else
            {
                applied = CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToSource(
                    envelope,
                    target.LegacySource,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog,
                    path,
                    out report);
            }
            if (!applied)
            {
                Debug.LogWarning(
                    "CharacterTest Level Editor: apply blocked by validation errors="
                    + report.ErrorCount
                    + ".");
                return;
            }

            if (target.IsGameManager)
            {
                target.GameManager.GameplayTriggerSceneCache.Configure(
                    triggerCacheData);
            }
            EditorUtility.SetDirty(target.DirtyObject);
            EditorSceneManager.MarkSceneDirty(target.GameObject.scene);
            EditorSceneManager.SaveScene(target.GameObject.scene);
            Debug.Log("CharacterTest Level Editor: applied JSON to " + target.DisplayName + " and saved scene.");
        }

        [MenuItem("MxFramework/Level Editor/CharacterTest/Repair Default JSON TransformRefs")]
        public static void RepairDefaultJsonTransformRefs()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                Debug.LogWarning("CharacterTest Level Editor: no CharacterTest GameManager scene cache target found.");
                return;
            }

            string fullPath = ToFullPath(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning("CharacterTest Level Editor: default JSON does not exist: " + fullPath);
                return;
            }

            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            if (!CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument document,
                    out LevelWorldObjectAuthoringValidationReport report)
                || report.HasErrors)
            {
                Debug.LogWarning(
                    "CharacterTest Level Editor: default JSON does not contain a valid world objects module.");
                return;
            }

            int repaired = CharacterTestWorldObjectLevelEditorModel.RepairTransformRefs(document, target.Root);
            envelope.UpsertModulePayload(
                CharacterTestWorldObjectAuthoringAdapter.WorldObjectsModuleId,
                CharacterTestWorldObjectAuthoringAdapter.WorldObjectsModuleSchemaVersion,
                document.Normalize(),
                SerializedModuleName);
            WriteEnvelope(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath, envelope);
            AssetDatabase.ImportAsset(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            Debug.Log("CharacterTest Level Editor: repaired " + repaired + " JSON transform reference(s).");
        }

    }
}
