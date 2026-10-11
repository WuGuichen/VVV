using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class
        CharacterTestGameplayTriggerWorkspacePersistenceTests
    {
        private const string ScenePath =
            CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;
        private const string DocumentPath =
            CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath;

        private byte[] _documentBackup;
        private byte[] _sceneBackup;
        private IReadOnlyList<IMxLevelEditorModule> _modules;
        private CharacterTestWorldObjectLevelEditorModule _worldModule;
        private MxLevelEditorWindow _window;
        private bool _hadOpenWindow;

        [SetUp]
        public void SetUp()
        {
            _hadOpenWindow =
                UnityEngine.Resources
                    .FindObjectsOfTypeAll<MxLevelEditorWindow>()
                    .Length > 0;
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            Assert.IsFalse(
                EditorSceneManager.GetActiveScene().isDirty,
                "Persistence tests require the saved scene baseline.");

            _documentBackup =
                File.ReadAllBytes(ToFullPath(DocumentPath));
            _sceneBackup =
                File.ReadAllBytes(ToFullPath(ScenePath));
            _modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < _modules.Count; i++)
                _modules[i].Reload();
            _worldModule = FindWorldModule(_modules);
            Assert.IsNotNull(_worldModule);
            Assert.IsFalse(_worldModule.IsDirty);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                _window.Close();

            Scene activeScene =
                EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid()
                && activeScene.isDirty
                && string.Equals(
                    activeScene.path,
                    ScenePath,
                    StringComparison.Ordinal))
            {
                // Clear Unity's in-memory dirty prompt before restoring the byte-for-byte
                // fixture backup. The temporary test state is overwritten immediately below.
                Assert.IsTrue(EditorSceneManager.SaveScene(activeScene));
            }

            AssetDatabase.ReleaseCachedFileHandles();
            if (_documentBackup != null)
            {
                File.WriteAllBytes(
                    ToFullPath(DocumentPath),
                    _documentBackup);
            }
            if (_sceneBackup != null)
            {
                File.WriteAllBytes(
                    ToFullPath(ScenePath),
                    _sceneBackup);
            }
            AssetDatabase.ImportAsset(
                DocumentPath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                ScenePath,
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            if (_modules != null)
            {
                for (int i = 0; i < _modules.Count; i++)
                    _modules[i].Reload();
            }

            if (_hadOpenWindow)
            {
                _window = MxLevelEditorWindow.OpenWindow();
                _window.CreateGUI();
            }
        }

        [UnityTest]
        public IEnumerator SaveUndoReopenAndDiscard_RestoreExactBaseline()
        {
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                ExtractTriggerDocument(
                    _worldModule.CaptureDocumentSnapshotForTests());
            CharacterTestLevelGameplayTriggerEntry entry =
                GetFirstEntry(trigger);
            string stableId = entry.StableId;
            string originalName = entry.DisplayName;
            Assert.IsTrue(
                _worldModule.TryGetTriggerPayloadsForTests(
                    stableId,
                    out long originalPayload0,
                    out long originalPayload1,
                    out long originalPayload2));

            Transform region = FindRegionTransform();
            Vector3 originalPosition = region.localPosition;
            Vector3 savedPosition =
                originalPosition + new Vector3(0.125f, 0f, 0f);
            MoveManagedRegion(region, savedPosition);
            Assert.IsTrue(
                _worldModule.UpdateTriggerDisplayNameForTests(
                    stableId,
                    "P3.3b 保存恢复规则"));
            Assert.IsTrue(
                _worldModule.UpdateTriggerPayloadsForTests(
                    stableId,
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    CharacterTestAreaConfinementIds.MechanicSourceId,
                    originalPayload2));
            string savedEffect =
                _worldModule.GetTriggerEffectSentenceForTests(
                    stableId);

            _window = MxLevelEditorWindow.OpenWindow();
            _window.CreateGUI();
            Assert.IsTrue(
                _window.TrySelectWorkspace(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue));
            _window.RepaintRequested();
            Assert.IsTrue(_window.hasUnsavedChanges);

            _window.SaveChanges();
            yield return null;
            Assert.IsFalse(_worldModule.IsDirty);
            Assert.IsFalse(
                EditorSceneManager.GetActiveScene().isDirty);
            if (_window != null)
                Assert.IsFalse(_window.hasUnsavedChanges);
            AssertSavedTrigger(
                stableId,
                "P3.3b 保存恢复规则",
                CharacterTestGuardAuraIds
                    .OnHitDefenseBreakBuffId,
                CharacterTestAreaConfinementIds
                    .MechanicSourceId);
            Assert.AreEqual(
                savedPosition,
                FindRegionTransform().localPosition);
            GameManager savedGameManager =
                Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(savedGameManager);
            CharacterTestGameplayTriggerSceneCacheData expectedCache =
                CompileSnapshot(
                    _worldModule,
                    _worldModule.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(
                expectedCache.SensorRows.Length,
                savedGameManager.GameplayTriggerSceneCache.SensorRows.Count);
            Assert.AreEqual(
                expectedCache.SensorRows[0].Min,
                savedGameManager.GameplayTriggerSceneCache.SensorRows[0].Min,
                "Save must publish the moved Region bounds to the Runtime cache.");
            Assert.AreEqual(
                expectedCache.SensorRows[0].Max,
                savedGameManager.GameplayTriggerSceneCache.SensorRows[0].Max,
                "Save must publish the moved Region bounds to the Runtime cache.");

            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(
                _worldModule.TryGetTriggerPayloadsForTests(
                    stableId,
                    out long undonePayload0,
                    out long undonePayload1,
                    out long undonePayload2));
            Assert.AreEqual(originalPayload0, undonePayload0);
            Assert.AreEqual(originalPayload1, undonePayload1);
            Assert.AreEqual(originalPayload2, undonePayload2);
            Assert.IsTrue(
                _worldModule.IsDirty,
                "Undoing away from the saved baseline must be dirty.");
            if (_window != null)
            {
                _window.RepaintRequested();
                Assert.IsTrue(_window.hasUnsavedChanges);
            }

            region = FindRegionTransform();
            MoveManagedRegion(
                region,
                savedPosition + new Vector3(0.25f, 0f, 0f));
            EnsureWindow();
            _window.RepaintRequested();
            Assert.IsTrue(_window.hasUnsavedChanges);
            _window.DiscardChanges();
            yield return null;

            Assert.IsFalse(_worldModule.IsDirty);
            Assert.IsFalse(
                EditorSceneManager.GetActiveScene().isDirty);
            Assert.AreEqual(
                savedPosition,
                FindRegionTransform().localPosition);
            AssertSavedTrigger(
                stableId,
                "P3.3b 保存恢复规则",
                CharacterTestGuardAuraIds
                    .OnHitDefenseBreakBuffId,
                CharacterTestAreaConfinementIds
                    .MechanicSourceId);
            Assert.AreEqual(
                savedEffect,
                _worldModule.GetTriggerEffectSentenceForTests(
                    stableId));

            if (_window != null)
                _window.Close();
            _worldModule.Reload();
            _window = MxLevelEditorWindow.OpenWindow();
            _window.CreateGUI();
            Assert.IsTrue(
                _window.TrySelectWorkspace(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue));
            Assert.AreEqual(
                "P3.3b 保存恢复规则",
                _worldModule.GetTriggerDisplayNameForTests(
                    stableId));
            Assert.AreEqual(
                savedEffect,
                _worldModule.GetTriggerEffectSentenceForTests(
                    stableId));
            Assert.IsFalse(_window.hasUnsavedChanges);

            Assert.AreNotEqual(
                originalName,
                _worldModule.GetTriggerDisplayNameForTests(
                    stableId));
        }

        [UnityTest]
        public IEnumerator
            PlayerHpThreshold_SaveReopenAndDiscardPreserveAuthoringAndCache()
        {
            _worldModule.SetTriggerDocumentForTests(
                new CharacterTestLevelGameplayTriggerAuthoringDocument(),
                markDirty: false);
            CharacterTestLevelGameplayTriggerEntry entry =
                _worldModule.AddPlayerHpLowStoryTriggerForTests();
            Assert.IsNotNull(entry);
            Assert.IsTrue(
                _worldModule.UpdatePlayerHpThresholdsForTests(
                    entry.StableId,
                    enterThreshold: 22,
                    exitThreshold: 29));
            Assert.IsTrue(
                _worldModule.UpdateTriggerConditionRootForTests(
                    entry.StableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.Any,
                        children: new[]
                        {
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation
                                    .CurrentScalar,
                                operand: 22000000L,
                                comparison:
                                    GameplayTriggerComparison.LessOrEqual),
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation.Not,
                                children: new[]
                                {
                                    new GameplayTriggerConditionNodeAuthoring(
                                        GameplayTriggerConditionOperation
                                            .HasSubject,
                                        operand: 1L)
                                })
                        })));
            StringAssert.Contains(
                "任一满足",
                _worldModule.GetTriggerEffectSentenceForTests(
                    entry.StableId));

            GameManager gameManager =
                Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            CharacterTestGameplayTriggerSceneCache cache =
                gameManager.GameplayTriggerSceneCache;
            string staleCanonical =
                cache.CanonicalTriggerEnvelopeJson;
            long staleCombinedHash =
                cache.CombinedCatalogHash;

            _window = MxLevelEditorWindow.OpenWindow();
            _window.CreateGUI();
            Assert.IsTrue(
                _window.TrySelectWorkspace(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue));
            _window.SaveChanges();
            yield return null;

            Assert.IsFalse(_worldModule.IsDirty);
            Assert.AreNotEqual(
                staleCanonical,
                cache.CanonicalTriggerEnvelopeJson);
            Assert.AreNotEqual(
                staleCombinedHash,
                cache.CombinedCatalogHash);
            CharacterTestLevelGameplayTriggerAuthoringDocument saved =
                ExtractTriggerDocument(
                    File.ReadAllText(ToFullPath(DocumentPath)));
            Assert.IsTrue(
                saved.TryFindEntry(
                    entry.StableId,
                    out CharacterTestLevelGameplayTriggerEntry savedEntry));
            Assert.IsTrue(
                saved.TryGetPlayerHpThresholds(
                    savedEntry,
                    out int savedEnter,
                    out int savedExit));
            Assert.AreEqual(22, savedEnter);
            Assert.AreEqual(29, savedExit);
            Assert.IsTrue(
                saved.TryGetConditionRoot(
                    savedEntry,
                    out GameplayTriggerConditionNodeAuthoring
                        savedCondition));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Any,
                savedCondition.Operation);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.CurrentScalar,
                savedCondition.Children[0].Operation);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Not,
                savedCondition.Children[1].Operation);
            Assert.AreEqual(
                CharacterTestGameplayTriggerSceneCache
                    .CurrentCacheVersion,
                cache.CacheVersion);
            Assert.AreEqual(0, cache.SensorRowCount);
            Assert.AreEqual(1, cache.AttributeThresholdRowCount);
            Assert.AreNotEqual(
                0L,
                cache.AttributeThresholdCatalogHash);

            _worldModule.Reload();
            Assert.IsFalse(_worldModule.IsDirty);
            Assert.IsTrue(
                _worldModule.UpdatePlayerHpThresholdsForTests(
                    entry.StableId,
                    enterThreshold: 18,
                    exitThreshold: 25));
            Assert.IsTrue(
                _worldModule.UpdateTriggerConditionRootForTests(
                    entry.StableId,
                    new GameplayTriggerConditionNodeAuthoring(
                        GameplayTriggerConditionOperation.All,
                        children: new[]
                        {
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation
                                    .HasSubject,
                                operand: 1L),
                            new GameplayTriggerConditionNodeAuthoring(
                                GameplayTriggerConditionOperation
                                    .CurrentScalar,
                                operand: 18000000L,
                                comparison:
                                    GameplayTriggerComparison.LessOrEqual)
                        })));
            EnsureWindow();
            _window.DiscardChanges();
            yield return null;

            CharacterTestLevelGameplayTriggerAuthoringDocument restored =
                ExtractTriggerDocument(
                    _worldModule.CaptureDocumentSnapshotForTests());
            Assert.IsTrue(
                restored.TryFindEntry(
                    entry.StableId,
                    out CharacterTestLevelGameplayTriggerEntry restoredEntry));
            Assert.IsTrue(
                restored.TryGetPlayerHpThresholds(
                    restoredEntry,
                    out int restoredEnter,
                    out int restoredExit));
            Assert.AreEqual(22, restoredEnter);
            Assert.AreEqual(29, restoredExit);
            Assert.IsTrue(
                restored.TryGetConditionRoot(
                    restoredEntry,
                    out GameplayTriggerConditionNodeAuthoring
                        restoredCondition));
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Any,
                restoredCondition.Operation);
            Assert.AreEqual(
                22000000L,
                restoredCondition.Children[0].Operand);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.Not,
                restoredCondition.Children[1].Operation);
            Assert.IsFalse(_worldModule.IsDirty);
        }

        [Test]
        public void InvalidCleanAuthoring_DoesNotOverwriteRuntimeCache()
        {
            CharacterTestLevelGameplayTriggerAuthoringDocument current =
                ExtractTriggerDocument(
                    _worldModule.CaptureDocumentSnapshotForTests());
            Assert.IsNotNull(current.RegionBindings);
            Assert.IsNotEmpty(current.RegionBindings);
            string regionStableId =
                current.RegionBindings[0].RegionStableId;
            GameManager gameManager =
                Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            CharacterTestGameplayTriggerSceneCache liveCache =
                gameManager.GameplayTriggerSceneCache;
            string beforeCanonical =
                liveCache.CanonicalTriggerEnvelopeJson;
            long beforeTriggerHash = liveCache.TriggerCatalogHash;
            long beforeSensorHash = liveCache.SensorCatalogHash;
            long beforeCombinedHash = liveCache.CombinedCatalogHash;
            var invalid =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            invalid.AddTemplate(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                regionStableId,
                payload0: 0L);
            _worldModule.SetTriggerDocumentForTests(
                invalid,
                markDirty: false);

            _worldModule.ApplyToScene();

            Assert.IsFalse(_worldModule.IsDirty);
            Assert.IsTrue(
                _worldModule.HasTriggerValidationIssueForTests(
                    "CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID"));
            StringAssert.Contains("错误", _worldModule.Status);
            Assert.AreEqual(
                beforeCanonical,
                liveCache.CanonicalTriggerEnvelopeJson);
            Assert.AreEqual(
                beforeTriggerHash,
                liveCache.TriggerCatalogHash);
            Assert.AreEqual(
                beforeSensorHash,
                liveCache.SensorCatalogHash);
            Assert.AreEqual(
                beforeCombinedHash,
                liveCache.CombinedCatalogHash);
        }

        [Test]
        public void Save_RefreshesRuntimeHashesAndActionBootstrap()
        {
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                ExtractTriggerDocument(
                    _worldModule.CaptureDocumentSnapshotForTests());
            CharacterTestLevelGameplayTriggerEntry entry =
                GetFirstEntry(trigger);
            GameManager gameManager =
                Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            CharacterTestGameplayTriggerSceneCache liveCache =
                gameManager.GameplayTriggerSceneCache;
            string beforeCanonical =
                liveCache.CanonicalTriggerEnvelopeJson;
            long beforeTriggerHash = liveCache.TriggerCatalogHash;
            long beforeSensorHash = liveCache.SensorCatalogHash;
            long beforeCombinedHash = liveCache.CombinedCatalogHash;

            Assert.IsTrue(
                _worldModule.UpdateTriggerPayloadsForTests(
                    entry.StableId,
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    CharacterTestAreaConfinementIds.MechanicSourceId));
            _worldModule.Save();
            Assert.IsFalse(_worldModule.IsDirty);
            CharacterTestGameplayTriggerSceneCacheData expected =
                CompileSnapshot(
                    _worldModule,
                    _worldModule.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(
                EditorSceneManager.GetActiveScene().isDirty);
            Assert.AreEqual(
                expected.CanonicalTriggerEnvelopeJson,
                liveCache.CanonicalTriggerEnvelopeJson);
            Assert.AreEqual(
                expected.TriggerCatalogHash,
                liveCache.TriggerCatalogHash);
            Assert.AreEqual(
                expected.SensorCatalogHash,
                liveCache.SensorCatalogHash);
            Assert.AreEqual(
                expected.CombinedCatalogHash,
                liveCache.CombinedCatalogHash);
            Assert.AreNotEqual(0L, liveCache.TriggerCatalogHash);
            Assert.AreNotEqual(0L, liveCache.SensorCatalogHash);
            Assert.AreNotEqual(0L, liveCache.CombinedCatalogHash);
            Assert.AreNotEqual(
                beforeCanonical,
                liveCache.CanonicalTriggerEnvelopeJson);
            Assert.AreNotEqual(
                beforeTriggerHash,
                liveCache.TriggerCatalogHash);
            Assert.AreNotEqual(
                beforeSensorHash,
                liveCache.SensorCatalogHash);
            Assert.AreNotEqual(
                beforeCombinedHash,
                liveCache.CombinedCatalogHash);

            CharacterTestLevelGameplayTriggerAuthoringDocument
                publishedTrigger =
                    ExtractTriggerDocument(
                        liveCache.CanonicalTriggerEnvelopeJson);
            GameplayTriggerCompileResult compile =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .CompileRuntimeCache(
                        publishedTrigger,
                        liveCache.TriggerCatalogHash,
                        out LevelWorldObjectAuthoringValidationReport
                            report);
            Assert.IsTrue(
                compile?.Succeeded == true,
                JoinIssues(report));
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    .TryCreateActionBootstrap(
                        compile.CompiledCatalog,
                        new GameplayBuffRequestBuffer(64, 64),
                        includeStory: true,
                        out GameplayTriggerActionBootstrapBinding[]
                            bindings,
                        out string bootstrapMessage),
                bootstrapMessage);
            Assert.IsNotEmpty(bindings);
        }

        private void EnsureWindow()
        {
            if (_window != null)
                return;
            _window = MxLevelEditorWindow.OpenWindow();
            _window.CreateGUI();
        }

        private void MoveManagedRegion(
            Transform region,
            Vector3 position)
        {
            Undo.RecordObject(
                region,
                "P3.3b persistence region move");
            region.localPosition = position;
            EditorSceneManager.MarkSceneDirty(
                region.gameObject.scene);
            Assert.IsTrue(
                ((IMxLevelEditorSceneChangeAwareModule)_worldModule)
                .NotifySceneTransformChanged(region));
        }

        private static CharacterTestWorldObjectLevelEditorModule
            FindWorldModule(
                IReadOnlyList<IMxLevelEditorModule> modules)
        {
            for (int i = 0;
                 modules != null && i < modules.Count;
                 i++)
            {
                if (modules[i]
                    is CharacterTestWorldObjectLevelEditorModule
                        worldModule)
                {
                    return worldModule;
                }
            }
            return null;
        }

        private static Transform FindRegionTransform()
        {
            Transform[] transforms =
                Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include);
            Scene activeScene =
                EditorSceneManager.GetActiveScene();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null
                    && transform.gameObject.scene == activeScene
                    && string.Equals(
                        transform.name,
                        "LevelRegion_StartArea",
                        StringComparison.Ordinal))
                {
                    return transform;
                }
            }

            Assert.Fail(
                "LevelRegion_StartArea was not found in the active scene.");
            return null;
        }

        private static CharacterTestLevelGameplayTriggerEntry
            GetFirstEntry(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    trigger)
        {
            Assert.IsNotNull(trigger);
            Assert.IsNotNull(trigger.Entries);
            Assert.IsNotEmpty(trigger.Entries);
            return trigger.Entries[0];
        }

        private static void AssertSavedTrigger(
            string stableId,
            string expectedName,
            long expectedPayload0,
            long expectedPayload1)
        {
            CharacterTestLevelGameplayTriggerAuthoringDocument trigger =
                ExtractTriggerDocument(
                    File.ReadAllText(ToFullPath(DocumentPath)));
            Assert.IsTrue(
                trigger.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry));
            Assert.AreEqual(expectedName, entry.DisplayName);
            Assert.IsTrue(
                trigger.TryGetTemplatePayloads(
                    entry,
                    out long payload0,
                    out long payload1,
                    out _));
            Assert.AreEqual(expectedPayload0, payload0);
            Assert.AreEqual(expectedPayload1, payload1);
        }

        private static CharacterTestGameplayTriggerSceneCacheData
            CompileSnapshot(
                CharacterTestWorldObjectLevelEditorModule module,
                string snapshot)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(snapshot);
            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter
                    .TryExtractWorldObjectsDocument(
                        envelope,
                        out LevelWorldObjectAuthoringDocument world,
                        out LevelWorldObjectAuthoringValidationReport
                            worldReport),
                JoinIssues(worldReport));
            Assert.IsTrue(
                CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                    envelope,
                    world,
                    module.ManagedSceneRoot,
                    CharacterTestWorldObjectDefinitionCatalog
                        .CreateCharacterTestSceneCatalog(),
                    out CharacterTestGameplayTriggerSceneCacheData data,
                    out LevelWorldObjectAuthoringValidationReport
                        report),
                JoinIssues(report));
            return data;
        }

        private static CharacterTestLevelGameplayTriggerAuthoringDocument
            ExtractTriggerDocument(string json)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(json);
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        trigger,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsTrue(modulePresent);
            return trigger;
        }

        private static string JoinIssues(
            LevelWorldObjectAuthoringValidationReport report)
        {
            if (report?.Issues == null
                || report.Issues.Count == 0)
            {
                return string.Empty;
            }

            var rows = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                rows[i] = issue == null
                    ? "[null]"
                    : issue.Code + ": " + issue.Message;
            }
            return string.Join(Environment.NewLine, rows);
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    assetPath));
        }
    }
}
