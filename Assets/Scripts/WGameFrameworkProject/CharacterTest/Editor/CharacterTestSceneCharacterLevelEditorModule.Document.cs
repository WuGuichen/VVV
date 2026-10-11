using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        private void EnsureLoaded()
        {
            string documentPath = DocumentPath;
            if (_document != null && string.Equals(_loadedDocumentPath, documentPath, StringComparison.Ordinal))
                return;

            if (_document != null && _dirty)
                throw new InvalidOperationException("场景角色草稿仍属于 " + _loadedDocumentPath + "；当前文档为 "
                    + documentPath + "。未保存草稿保留，未自动加载其他关卡。");
            _undoSession?.Retire(); _undoSession = null;
            _envelope = LoadOrCreateEnvelope();
            _document = ExtractDocument(_envelope);
            _loadedDocumentPath = documentPath;
            _report = ValidateLoadedDocument();
            _dirty = false;
            _sceneDirty = false;
        }

        private MxLevelAuthoringDocument LoadOrCreateEnvelope()
        {
            string fullPath = ToFullPath(DocumentPath);
            if (File.Exists(fullPath))
                return MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));

            GameManager gameManager = FindGameManager();
            if (gameManager != null && gameManager.CharacterSceneCache.CharacterInstanceCount > 0)
            {
                CharacterTestSceneCharacterAuthoringDocument document =
                    CharacterTestSceneCharacterAuthoringAdapter.CreateDocumentFromCache(
                        gameManager.CharacterSceneCache,
                        gameManager.WorldObjectSceneRoot);
                return CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    gameManager.WorldObjectSceneRoot);
            }

            var fallback = new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot"
            };
            return CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(fallback);
        }

        private static CharacterTestSceneCharacterAuthoringDocument CreateEmptyCharacterDocument()
        {
            return new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot",
                Characters = new List<CharacterTestSceneCharacterDefinition>()
            }.Normalize();
        }

        private static CharacterTestSceneCharacterAuthoringDocument ExtractDocument(
            MxLevelAuthoringDocument envelope)
        {
            if (CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    envelope,
                    out CharacterTestSceneCharacterAuthoringDocument document,
                    out _))
            {
                return document;
            }

            return new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot"
            };
        }

        private bool IsDocumentEmpty()
        {
            return _document?.Characters == null || _document.Characters.Count == 0;
        }

        private static bool ContainsOnlyEmptyDocumentWarnings(LevelWorldObjectAuthoringValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return false;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                if (issue == null
                    || issue.Severity != LevelWorldObjectAuthoringSeverity.Warning
                    || !IsEmptyDocumentWarning(issue.Code))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEmptyDocumentWarning(string code)
        {
            return string.Equals(
                    code,
                    CharacterTestSceneCharacterAuthoringAdapter.MissingLocalPlayer,
                    StringComparison.Ordinal)
                || string.Equals(
                    code,
                    CharacterTestSceneCharacterAuthoringAdapter.MissingAiControlled,
                    StringComparison.Ordinal);
        }

        private void SyncEnvelopeFromDocument()
        {
            GameManager gameManager = FindGameManager();
            _envelope = CharacterTestLevelEditorEnvelopeMerge.MergeCharacters(
                _envelope,
                _document,
                gameManager == null ? null : gameManager.WorldObjectSceneRoot);
        }

        private void RefreshEnvelopeFromDiskPreservingCurrentModule()
        {
            string fullPath = ToFullPath(DocumentPath);
            if (CharacterTestJoinedLevelSaveContext.TryGet(DocumentPath, out var joined)) _envelope = joined;
            else if (File.Exists(fullPath))
                _envelope = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            SyncEnvelopeFromDocument();
        }

        // Read-only checks retain saved header/reference and wire-number identities.
        // Live Transform references are refreshed only by the existing save preparation.
        private MxLevelAuthoringDocument CaptureReadOnlyCharacterEnvelope()
        {
            var envelope = _envelope == null ? new MxLevelAuthoringDocument()
                : MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(_envelope));
            var current = _document?.Normalize();
            if (current == null) return envelope;
            if (CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(envelope, out var saved, out _)
                && JsonConvert.SerializeObject(saved.Normalize()) == JsonConvert.SerializeObject(current))
                return envelope;
            envelope.TryGetModule(CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId, out var previous);
            envelope.UpsertModulePayload(CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId,
                current.SchemaVersion, current, previous?.DisplayName ?? "CharacterTest Characters");
            return envelope.Normalize();
        }

        private CharacterTestSceneCharacterCacheSyncStatus BuildCacheSyncStatus()
        {
            return BuildCacheSyncStatus(FindGameManager());
        }

        private CharacterTestSceneCharacterCacheSyncStatus BuildCacheSyncStatus(GameManager gameManager)
        {
            return CharacterTestSceneCharacterCacheSyncStatus.Evaluate(
                _document,
                CaptureReadOnlyCharacterEnvelope(),
                gameManager == null ? null : gameManager.CharacterSceneCache,
                DocumentPath);
        }

        private LevelWorldObjectAuthoringValidationReport ValidateLoadedDocument()
        {
            GameManager gameManager = FindGameManager();
            return CharacterTestSceneCharacterAuthoringAdapter.ValidateEnvelopeForCache(
                CaptureReadOnlyCharacterEnvelope(),
                gameManager == null ? null : gameManager.CharacterSceneCache,
                gameManager == null ? null : gameManager.WorldObjectSceneRoot);
        }

        private void ForceValidate()
        {
            EnsureLoaded();
            _report = ValidateLoadedDocument();
            _validationQueued = false;
        }

        private void MarkDirty()
        {
            _dirty = true;
            _undoSession?.SynchronizeCurrentDocument();
            QueueValidation();
            RebuildRows();
            _activeContext?.RequestRefresh?.Invoke();
        }

        private void EnsureUndoSession()
        {
            if (_undoSession != null || _document == null)
                return;

            _undoSession = new CharacterTestLevelEditorDocumentUndoSession(
                "CharacterTest 场景角色",
                CaptureDocumentSnapshot,
                RestoreDocumentSnapshot,
                RefreshAfterDocumentUndo);
            _undoSession.ResetBaseline();
        }

        private string CaptureDocumentSnapshot()
        {
            GameManager gameManager = FindGameManager();
            MxLevelAuthoringDocument snapshot =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    _document,
                    gameManager == null ? null : gameManager.WorldObjectSceneRoot);
            return MxLevelAuthoringJson.SaveToJson(snapshot);
        }

        private void RestoreDocumentSnapshot(string snapshot)
        {
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(snapshot);
            _document = ExtractDocument(envelope);
        }

        private void RefreshAfterDocumentUndo()
        {
            _dirty = !_undoSession.IsAtSavedBaseline;
            _report = ValidateLoadedDocument();
            RebuildRows();
            SceneView.RepaintAll();
            _activeContext?.RequestRefresh?.Invoke();
        }

        private void QueueValidation()
        {
            if (_validationQueued)
                return;

            _validationQueued = true;
            EditorApplication.delayCall += RunDeferredValidation;
        }

        private void RunDeferredValidation()
        {
            if (!_validationQueued)
                return;

            _validationQueued = false;
            _report = ValidateLoadedDocument();
            _activeContext?.RequestRefresh?.Invoke();
        }

    }
}
