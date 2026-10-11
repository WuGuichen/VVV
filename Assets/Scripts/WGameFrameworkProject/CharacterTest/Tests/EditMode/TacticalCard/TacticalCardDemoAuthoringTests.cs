using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalCardDemoAuthoringTests
    {
        private const string ScenePath = "Assets/Scenes/TacticalCardDemoScene.unity";
        private const string LevelPath = "Assets/Config/MxFramework/Levels/TacticalCardDemo/tactical_card_demo.level.json";
        private const string LevelId = "tactical_card_demo";
        private const string SceneRootName = "Tactical Card Demo Scene Root";
        private readonly Dictionary<string, byte[]> _originalArtifacts = new Dictionary<string, byte[]>();
        private SceneSetup[] _sceneSetup;
        private byte[] _defaultLevel;

        [OneTimeSetUp]
        public void PreserveArtifacts()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _defaultLevel = File.ReadAllBytes(CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath);
            foreach (string path in new[] { LevelPath, ScenePath })
            {
                _originalArtifacts.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                _originalArtifacts.Add(path + ".meta", File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null);
            }
        }

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.ReleaseCachedFileHandles();
            foreach (string path in new[] { LevelPath, ScenePath })
            {
                byte[] original = _originalArtifacts[path];
                Assert.IsNotNull(original, "Formal Tactical Card scene asset is required: " + path);
                RestoreArtifact(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [TearDown]
        public void AssertDefaultLevelUnchanged()
        {
            CollectionAssert.AreEqual(_defaultLevel,
                File.ReadAllBytes(CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreArtifacts()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var entry in _originalArtifacts)
            {
                if (entry.Value == null)
                {
                    if (File.Exists(entry.Key))
                        File.Delete(entry.Key);
                }
                else
                    RestoreArtifact(entry.Key, entry.Value);
            }
            foreach (string path in new[] { LevelPath, ScenePath })
            {
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            if (_sceneSetup != null && _sceneSetup.Length > 0 && _sceneSetup.All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
        }

        [Test]
        public void A01_OpenReopenAndLevelEditorSave_KeepDocumentAndCacheIdentical()
        {
            MxLevelAuthoringDocument envelope = LoadEnvelope();
            Assert.AreEqual(LevelId, envelope.LevelId);
            Assert.AreEqual(ScenePath, envelope.ScenePath);
            Assert.AreEqual(SceneRootName, envelope.RootTransformRef.Path);
            Assert.IsTrue(envelope.TryGetModule(CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId, out var module));
            Assert.AreEqual(2, module.SchemaVersion);
            Assert.IsTrue(envelope.TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var stageModule));
            Assert.AreEqual(1, stageModule.SchemaVersion);
            var stage = stageModule.ReadPayload<CharacterTestTacticalStageAuthoringDocument>();
            Assert.AreEqual(50, stage.InitialMomentum);
            Assert.AreEqual(3, stage.HandCapacity);
            Assert.AreEqual(3, stage.RequiredStableTicks);
            Assert.AreEqual(1f, stage.NormalScale);
            Assert.AreEqual(0.2f, stage.DilatedScale);
            Assert.AreEqual(240, stage.PreActionBudgetTicks);
            Assert.AreEqual(300, stage.ExecutionBudgetTicks);
            Assert.AreEqual(300, stage.IngressBudgetTicks);
            Assert.AreEqual(60, stage.StoryTransferBudgetTicks);
            Assert.AreEqual("DefeatWinsSameFrame", stage.SimultaneousOutcomePolicy);
            Assert.AreEqual("ResetSessionAndDeck", stage.StageResetPolicy);
            Assert.AreEqual("tactical.player.spawn", stage.PlayerSpawnStableId);
            Assert.AreEqual("tactical.enemy.spawn", stage.OpponentSpawnStableId);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager manager = FindManager();
            AssertCacheMatches(envelope, manager);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath),
                new SerializedObject(manager).FindProperty("_profile").objectReferenceValue);
            var anchors = manager.WorldObjectSceneRoot.Find("Spawns");
            Assert.AreEqual(2, anchors.childCount);
            foreach (Transform anchor in anchors)
            {
                Assert.AreEqual(0, anchor.childCount);
                Assert.AreEqual(1, anchor.GetComponents<Component>().Length, "Anchors contain only a Transform.");
                Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(anchor.gameObject));
            }
            Assert.IsEmpty(manager.GetComponentsInChildren<Animator>(true));
            Assert.IsEmpty(manager.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            Assert.AreEqual(0, UnityEngine.Camera.main.cullingMask & (1 << LayerMask.NameToLayer("UI")));

            var editor = new CharacterTestSceneCharacterLevelEditorModule();
            editor.OnEnable();
            Assert.AreEqual(LevelPath, editor.DocumentPath);
            Assert.IsTrue(MxLevelEditorSaveCoordinator.TrySaveModules(new IMxLevelEditorModule[] { editor }, out string error), error);
            Assert.IsFalse(editor.IsDirty);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssertCacheMatches(LoadEnvelope(), FindManager());
            Assert.IsTrue(LoadEnvelope().TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var savedStage));
            Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(stageModule.Payload, savedStage.Payload));
        }

        [Test]
        public void A02_AnchorMoveAndUndoRedo_ShareDocumentTransactionAndSave()
        {
            GameManager manager = FindManager();
            var editor = new CharacterTestSceneCharacterLevelEditorModule();
            editor.SetDocumentPath(LevelPath);
            editor.OnEnable();
            Transform anchor = manager.WorldObjectSceneRoot.Find("Spawns/Player[0]");
            Vector3 before = anchor.position;
            float beforeYaw = anchor.eulerAngles.y;
            string beforeHash = CharacterTestSceneCharacterAuthoringAdapter.ComputeContentHash(ReadDocument(editor));
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(anchor, "Move tactical spawn");
            anchor.SetPositionAndRotation(new Vector3(1.25f, 0.5f, -3f), Quaternion.Euler(0f, 65f, 0f));
            Assert.IsTrue(editor.NotifySceneTransformChanged(anchor));
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            AssertPose(editor, anchor);
            Assert.IsTrue(editor.IsDocumentDirty);
            string afterHash = CharacterTestSceneCharacterAuthoringAdapter.ComputeContentHash(ReadDocument(editor));
            Assert.AreNotEqual(beforeHash, afterHash);

            Undo.PerformUndo();
            Assert.AreEqual(before, anchor.position);
            Assert.AreEqual(beforeYaw, anchor.eulerAngles.y);
            AssertPose(editor, anchor);
            Assert.AreEqual(beforeHash, CharacterTestSceneCharacterAuthoringAdapter.ComputeContentHash(ReadDocument(editor)));
            Assert.IsFalse(editor.IsDocumentDirty);

            Undo.PerformRedo();
            AssertPose(editor, anchor);
            Assert.AreEqual(afterHash, CharacterTestSceneCharacterAuthoringAdapter.ComputeContentHash(ReadDocument(editor)));
            Assert.IsTrue(editor.IsDocumentDirty);
            Assert.IsTrue(MxLevelEditorSaveCoordinator.TrySaveModules(new IMxLevelEditorModule[] { editor }, out string error), error);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssertCacheMatches(LoadEnvelope(), FindManager());
            Assert.AreEqual(new Vector3(1.25f, 0.5f, -3f),
                FindManager().CharacterSceneCache.Characters.Single(item => item.StableId == "tactical.player.spawn").SpawnPosition);
        }

        [TestCase(-1, CharacterTestSceneCharacterAuthoringAdapter.InvalidCharacterId)]
        [TestCase(int.MaxValue, CharacterTestSceneCharacterAuthoringAdapter.CharacterDefinitionNotFound)]
        public void A03_InvalidCharacterId_RejectsWithoutReplacingExistingCache(int characterId, string expectedCode)
        {
            GameManager manager = FindManager();
            string hash = manager.CharacterSceneCache.AuthoringContentHash;
            var original = manager.CharacterSceneCache.Characters.ToArray();
            var envelope = LoadEnvelope();
            var document = Extract(envelope);
            document.Characters[0].CharacterId = characterId;
            envelope = CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(envelope, document, manager.WorldObjectSceneRoot);
            Assert.IsFalse(CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                envelope, manager.CharacterSceneCache, manager.WorldObjectSceneRoot, LevelPath, out var report));
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == expectedCode));
            CollectionAssert.AreEqual(original, manager.CharacterSceneCache.Characters);
            Assert.AreEqual(hash, manager.CharacterSceneCache.AuthoringContentHash);
            var invalid = new CharacterTestSceneCharacterCache.CharacterInstance(true, "invalid", "Invalid", characterId, 1,
                CharacterTestCharacterControlOwner.LocalPlayer, null, "", "", hasSpawnPose: true, spawnPosition: new Vector3(4, 5, 6));
            Assert.IsFalse(invalid.TryCreateSpawnSpec(new CharacterTestCharacterDefinitionRegistry(), out var spec, out var diagnostic));
            Assert.AreEqual(default(CharacterTestCharacterSpawnSpec), spec);
            StringAssert.Contains("character-not-found", diagnostic);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void A03_InvalidTeam_RejectsCache(int teamId)
        {
            var document = Extract(LoadEnvelope());
            document.Characters[0].TeamId = teamId;
            var report = CharacterTestSceneCharacterAuthoringAdapter.ValidateDocumentForCache(
                document, FindManager().CharacterSceneCache, FindManager().WorldObjectSceneRoot, new CharacterTestCharacterDefinitionRegistry());
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(issue => issue.Code == CharacterTestSceneCharacterAuthoringAdapter.InvalidTeamId));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void A03_UnknownModuleOrPayloadVersion_RejectsWithoutNormalization(bool changeModule)
        {
            var envelope = LoadEnvelope();
            Assert.IsTrue(envelope.TryGetModule(CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId, out var module));
            if (changeModule)
                module.SchemaVersion = 999;
            else
                module.Payload["schemaVersion"] = 999;
            Assert.IsFalse(CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                envelope, FindManager().CharacterSceneCache, FindManager().WorldObjectSceneRoot,
                LevelPath, out var report));
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void V1_WithoutPose_StillUsesLiveAnchorAndDoesNotRewriteSource()
        {
            GameManager manager = FindManager();
            var document = Extract(LoadEnvelope());
            document.Format = CharacterTestSceneCharacterAuthoringDocument.FormatV1;
            document.SchemaVersion = CharacterTestSceneCharacterAuthoringDocument.LegacySchemaVersion;
            foreach (var character in document.Characters)
                character.SpawnPose = null;
            var envelope = CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(document, manager.WorldObjectSceneRoot);
            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            envelope = MxLevelAuthoringJson.LoadFromJson(json);
            var cache = new CharacterTestSceneCharacterCache();
            Assert.IsTrue(CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                envelope, cache, manager.WorldObjectSceneRoot, "legacy.json", out var report), FormatIssues(report));
            Assert.AreEqual(json, MxLevelAuthoringJson.SaveToJson(envelope));
            var instance = cache.Characters[0];
            Assert.IsFalse(instance.HasSpawnPose);
            instance.Transform.position = new Vector3(6f, 7f, 8f);
            Assert.IsTrue(instance.TryCreateSpawnSpec(new CharacterTestCharacterDefinitionRegistry(), out var spec, out _));
            Assert.AreEqual(new Vector3(6f, 7f, 8f), new Vector3(spec.X, spec.Y, spec.Z));
        }

        [Test]
        public void V2_RuntimeUsesCompiledPoseEvenAfterAnchorMovedOrDestroyed()
        {
            var instance = FindManager().CharacterSceneCache.Characters[0];
            Vector3 published = instance.SpawnPosition;
            instance.Transform.position = new Vector3(100f, 200f, 300f);
            Assert.IsTrue(instance.TryCreateSpawnSpec(new CharacterTestCharacterDefinitionRegistry(), out var moved, out _));
            Assert.AreEqual(published, new Vector3(moved.X, moved.Y, moved.Z));
            Object.DestroyImmediate(instance.Transform.gameObject);
            Assert.IsTrue(instance.TryCreateSpawnSpec(new CharacterTestCharacterDefinitionRegistry(), out var missing, out _));
            Assert.AreEqual(published, new Vector3(missing.X, missing.Y, missing.Z));
        }

        [Test]
        public void DocumentPath_OverrideAndSceneSwitchReloadTheCorrectDocument()
        {
            var editor = new CharacterTestSceneCharacterLevelEditorModule();
            editor.OnEnable();
            Assert.AreEqual(LevelId, ReadDocument(editor).LevelId);
            editor.SetDocumentPath(CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath.Replace('/', '\\'));
            editor.OnEnable();
            Assert.AreEqual(CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath, editor.DocumentPath);
            Assert.AreEqual(CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId, ReadDocument(editor).LevelId);
            editor.SetDocumentPath(null);
            editor.OnEnable();
            Assert.AreEqual(LevelId, ReadDocument(editor).LevelId);
            EditorSceneManager.OpenScene(CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath, OpenSceneMode.Single);
            editor.OnEnable();
            Assert.AreEqual(CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId, ReadDocument(editor).LevelId);
        }

        private static void AssertCacheMatches(MxLevelAuthoringDocument envelope, GameManager manager)
        {
            Assert.AreEqual(LevelPath, manager.CharacterSceneCache.AuthoringJsonPath);
            Assert.AreEqual(MxLevelAuthoringJson.ComputeContentHash(envelope), manager.CharacterSceneCache.AuthoringContentHash);
            var document = Extract(envelope);
            Assert.AreEqual(2, manager.CharacterSceneCache.CharacterInstanceCount);
            foreach (var character in document.Characters)
            {
                var instance = manager.CharacterSceneCache.Characters.Single(item => item.StableId == character.StableId);
                Assert.AreEqual(character.Enabled, instance.IsEnabled);
                Assert.AreEqual(character.DisplayName, instance.DisplayName);
                Assert.AreEqual(character.CharacterId, instance.CharacterId);
                Assert.AreEqual(character.TeamId, instance.TeamId);
                Assert.AreEqual(character.ControlOwner, instance.ControlOwner);
                Assert.AreEqual(character.RuntimeAiPlannerProfilePath, instance.RuntimeAiPlannerProfilePath);
                Assert.AreEqual(character.Group, instance.Group);
                Assert.AreEqual(character.Notes, instance.Notes);
                Assert.IsTrue(instance.HasSpawnPose);
                Assert.AreEqual(character.SpawnPose.Position, instance.SpawnPosition);
                Assert.AreEqual(character.SpawnPose.YawDegrees, instance.SpawnYawDegrees);
                Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(manager.WorldObjectSceneRoot, character.TransformRef, out var anchor));
                Assert.AreSame(anchor, instance.Transform);
                Assert.AreEqual(character.SpawnPose.Position, anchor.position);
                Assert.AreEqual(character.SpawnPose.YawDegrees, anchor.eulerAngles.y);
            }
            var reconstructed = CharacterTestSceneCharacterAuthoringAdapter.CreateDocumentFromCache(
                manager.CharacterSceneCache, manager.WorldObjectSceneRoot, envelope.LevelId, envelope.ScenePath);
            foreach (var character in reconstructed.Characters)
            {
                var original = document.Characters.Single(item => item.StableId == character.StableId);
                Assert.AreEqual(original.SpawnPose.Position, character.SpawnPose.Position);
                Assert.AreEqual(original.SpawnPose.YawDegrees, character.SpawnPose.YawDegrees);
            }
        }

        private static void AssertPose(CharacterTestSceneCharacterLevelEditorModule editor, Transform anchor)
        {
            var pose = ReadDocument(editor).Characters.Single(item => item.StableId == "tactical.player.spawn").SpawnPose;
            Assert.AreEqual(anchor.position, pose.Position);
            Assert.AreEqual(anchor.eulerAngles.y, pose.YawDegrees);
        }

        private static CharacterTestSceneCharacterAuthoringDocument ReadDocument(CharacterTestSceneCharacterLevelEditorModule editor) =>
            (CharacterTestSceneCharacterAuthoringDocument)typeof(CharacterTestSceneCharacterLevelEditorModule)
                .GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);

        private static GameManager FindManager() => Object.FindObjectsByType<GameManager>(FindObjectsInactive.Exclude).Single();
        private static void RestoreArtifact(string path, byte[] bytes)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { File.WriteAllBytes(path, bytes); return; }
                catch (IOException) when (attempt < 5)
                {
                    AssetDatabase.ReleaseCachedFileHandles();
                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        private static MxLevelAuthoringDocument LoadEnvelope() => MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(LevelPath));

        private static CharacterTestSceneCharacterAuthoringDocument Extract(MxLevelAuthoringDocument envelope)
        {
            Assert.IsTrue(CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(envelope, out var document, out var report), FormatIssues(report));
            return document;
        }

        private static string FormatIssues(LevelWorldObjectAuthoringValidationReport report) =>
            string.Join("; ", report.Issues.Select(issue => issue.Code + ": " + issue.Message));
    }
}
