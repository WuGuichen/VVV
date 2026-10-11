using System;
using System.Collections.Generic;
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
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        private enum CharacterPlacementOperation
        {
            Create,
            Duplicate,
            Reposition
        }

        private CharacterPlacementOperation _characterPlacementOperation;
        private Row _characterPlacementSourceRow;
        private GameObject _characterPlacementSourceObject;

        private bool TryResolveCharacterPlacementPosition(
            Vector2 mousePosition,
            out Vector3 position)
        {
            if (!CharacterTestLevelEditorPlacementUtility.TryResolveSurfacePosition(
                    mousePosition,
                    out Vector3 surfacePosition,
                    out _))
            {
                position = default;
                return false;
            }

            position = CharacterTestLevelEditorPlacementUtility.ResolvePivotPosition(
                _characterPlacementSourceObject,
                surfacePosition,
                fallbackBottomOffset: 0f);
            return true;
        }

        private CharacterTestCharacterControlOwner ResolvePlacementPreviewOwner()
        {
            if (_characterPlacementSourceRow != null
                && _characterPlacementSourceRow.Index >= 0
                && _characterPlacementSourceRow.Index < _document.Characters.Count)
            {
                return _document.Characters[_characterPlacementSourceRow.Index].ControlOwner;
            }

            return _placementOwner;
        }

        private string BuildPlacementPreviewLabel(CharacterTestCharacterControlOwner owner)
        {
            switch (_characterPlacementOperation)
            {
                case CharacterPlacementOperation.Duplicate:
                    return "复制并放置 " + GetControlOwnerLabel(owner);
                case CharacterPlacementOperation.Reposition:
                    return "重新放置 " + GetControlOwnerLabel(owner);
                default:
                    return "放置 " + GetControlOwnerLabel(owner);
            }
        }

        private CharacterPlacementResult CommitCharacterPlacement(Vector3 position)
        {
            switch (_characterPlacementOperation)
            {
                case CharacterPlacementOperation.Duplicate:
                    return DuplicateCharacterSceneObject(position);
                case CharacterPlacementOperation.Reposition:
                    return RepositionCharacterSceneObject(position);
                default:
                    return CreateCharacterAtPosition(position);
            }
        }

        private CharacterPlacementResult CreateCharacterAtPosition(Vector3 position)
        {
            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("放置 CharacterTest 场景角色"))
            {
                GameObject created = CreateCharacterMarker(_placementOwner, position);
                if (created == null)
                    return CharacterPlacementResult.Failed("无法创建场景角色。");

                AddTransform(created.transform, _placementOwner, GuessTeamId(_placementOwner));
                transaction.Commit();
                Selection.activeGameObject = created;
                return CharacterPlacementResult.Succeeded(
                    created,
                    "已放置角色：" + created.name + "。");
            }
        }

        private CharacterPlacementResult DuplicateCharacterSceneObject(Vector3 position)
        {
            if (!TryGetPlacementSourceCharacter(
                    out CharacterTestSceneCharacterDefinition source))
            {
                return CharacterPlacementResult.Failed("没有可复制的场景角色。");
            }

            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? null : gameManager.WorldObjectSceneRoot;
            if (root == null)
                return CharacterPlacementResult.Failed("当前场景中未找到 CharacterTest 场景根节点。");

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("复制并放置 CharacterTest 场景角色"))
            {
                GameObject duplicate = Object.Instantiate(_characterPlacementSourceObject);
                Undo.RegisterCreatedObjectUndo(duplicate, "复制并放置 CharacterTest 场景角色");
                duplicate.name = CreateUniqueCharacterSiblingName(
                    _characterPlacementSourceObject.transform.parent,
                    _characterPlacementSourceObject.name + " Copy");
                duplicate.transform.SetParent(
                    _characterPlacementSourceObject.transform.parent,
                    worldPositionStays: true);
                duplicate.transform.position = position;
                duplicate.transform.rotation = _characterPlacementSourceObject.transform.rotation;
                duplicate.transform.localScale = _characterPlacementSourceObject.transform.localScale;

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, duplicate.transform);
                string stableId = CreateUniqueCharacterStableId(source.StableId);
                var copied = new CharacterTestSceneCharacterDefinition
                {
                    Enabled = source.Enabled,
                    StableId = stableId,
                    DisplayName = source.DisplayName + " 副本",
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    SpawnPose = CaptureSpawnPose(duplicate.transform),
                    CharacterId = source.CharacterId,
                    TeamId = source.TeamId,
                    ControlOwner = source.ControlOwner,
                    RuntimeAiPlannerProfilePath = source.RuntimeAiPlannerProfilePath,
                    Group = source.Group,
                    Notes = source.Notes
                };
                _document.Characters.Add(copied);
                _document = _document.Normalize();
                MarkDirty();
                transaction.Commit();
                EditorUtility.SetDirty(duplicate);
                EditorSceneManager.MarkSceneDirty(duplicate.scene);
                Selection.activeGameObject = duplicate;
                _activeContext?.RequestRefresh?.Invoke();
                return CharacterPlacementResult.Succeeded(
                    duplicate,
                    "已复制并登记角色：" + duplicate.name + "。");
            }
        }

        private CharacterPlacementResult RepositionCharacterSceneObject(Vector3 position)
        {
            if (!TryGetPlacementSourceCharacter(out CharacterTestSceneCharacterDefinition character))
                return CharacterPlacementResult.Failed("没有可重新放置的场景角色。");

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("重新放置 CharacterTest 场景角色"))
            {
                Undo.RecordObject(
                    _characterPlacementSourceObject.transform,
                    "重新放置 CharacterTest 场景角色");
                _characterPlacementSourceObject.transform.position = position;
                character.SpawnPose = CaptureSpawnPose(_characterPlacementSourceObject.transform);
                MarkDirty();
                EditorUtility.SetDirty(_characterPlacementSourceObject.transform);
                EditorSceneManager.MarkSceneDirty(_characterPlacementSourceObject.scene);
                transaction.Commit();
                Selection.activeGameObject = _characterPlacementSourceObject;
                return CharacterPlacementResult.Succeeded(
                    _characterPlacementSourceObject,
                    "已重新放置角色：" + _characterPlacementSourceObject.name + "。");
            }
        }

        private bool TryGetPlacementSourceCharacter(
            out CharacterTestSceneCharacterDefinition character)
        {
            character = null;
            if (_characterPlacementSourceRow == null
                || _characterPlacementSourceObject == null
                || _characterPlacementSourceRow.Index < 0
                || _characterPlacementSourceRow.Index >= _document.Characters.Count)
            {
                return false;
            }

            character = _document.Characters[_characterPlacementSourceRow.Index];
            return character != null;
        }

        private void BeginCharacterDuplicatePlacement(Row row)
        {
            if (!TryBeginCharacterObjectPlacement(
                    row,
                    CharacterPlacementOperation.Duplicate,
                    "复制并放置"))
            {
                return;
            }

            _status = "复制并放置角色：左键确认副本位置，Esc 取消。";
        }

        private void BeginCharacterRepositionPlacement(Row row)
        {
            if (!TryBeginCharacterObjectPlacement(
                    row,
                    CharacterPlacementOperation.Reposition,
                    "重新放置"))
            {
                return;
            }

            _status = "重新放置角色：左键确认新位置，Esc 取消。";
        }

        private bool TryBeginCharacterObjectPlacement(
            Row row,
            CharacterPlacementOperation operation,
            string operationLabel)
        {
            if (row == null || !TryResolveRowTransform(row, out Transform transform))
            {
                _status = "无法解析当前角色 Transform，不能" + operationLabel + "。";
                return false;
            }

            _characterPlacementOperation = operation;
            _characterPlacementSourceRow = row;
            _characterPlacementSourceObject = transform.gameObject;
            _placementActive = true;
            _activeContext?.RequestRefresh?.Invoke();
            SceneView.RepaintAll();
            return true;
        }

        private void ResetCharacterPlacementOperation()
        {
            _characterPlacementOperation = CharacterPlacementOperation.Create;
            _characterPlacementSourceRow = null;
            _characterPlacementSourceObject = null;
        }

        internal bool DuplicateRowForTests(
            MxLevelEditorListRow row,
            Vector3 position,
            out GameObject duplicate)
        {
            duplicate = null;
            if (row?.Payload is not Row payload
                || !TryBeginCharacterObjectPlacement(
                    payload,
                    CharacterPlacementOperation.Duplicate,
                    "复制并放置"))
            {
                return false;
            }

            CharacterPlacementResult result = DuplicateCharacterSceneObject(position);
            _placementActive = false;
            ResetCharacterPlacementOperation();
            duplicate = result.GameObject;
            return result.Success;
        }

        internal bool RepositionRowForTests(
            MxLevelEditorListRow row,
            Vector3 position)
        {
            if (row?.Payload is not Row payload
                || !TryBeginCharacterObjectPlacement(
                    payload,
                    CharacterPlacementOperation.Reposition,
                    "重新放置"))
            {
                return false;
            }

            CharacterPlacementResult result = RepositionCharacterSceneObject(position);
            _placementActive = false;
            ResetCharacterPlacementOperation();
            return result.Success;
        }

        private VisualElement CreateCharacterQuickPlacementActions(
            Row row,
            CharacterTestSceneCharacterDefinition character)
        {
            var actions = new VisualElement
            {
                name = "character-test-character-quick-actions"
            };
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(CreateButton(
                "重新放置",
                () => BeginCharacterRepositionPlacement(row)));
            actions.Add(CreateButton(
                "复制并放置",
                () => BeginCharacterDuplicatePlacement(row)));

            var more = new ToolbarMenu { text = "更多" };
            more.menu.AppendAction("对齐网格", _ =>
            {
                int changed = CharacterTestLevelEditorPlacementUtility.AlignSelectionToGrid(
                    Selection.transforms,
                    "对齐 CharacterTest 场景角色到网格");
                _status = changed > 0
                    ? "已将 " + changed + " 个对象对齐到网格。"
                    : "当前选择无需对齐。";
                SceneView.RepaintAll();
            });
            more.menu.AppendSeparator();
            more.menu.AppendAction(
                "仅移除登记",
                _ => RemoveCharacterRegistration(character));
            more.menu.AppendAction(
                "删除场景对象...",
                _ => DeleteCharacterSceneObject(row, character));
            actions.Add(more);
            return actions;
        }

        public void BuildSceneContextMenu(
            MxLevelEditorSceneViewContext context,
            GenericMenu menu)
        {
            if (menu == null)
                return;
            if (context?.SelectedRow?.Payload is not Row row
                || row.Index < 0
                || _document?.Characters == null
                || row.Index >= _document.Characters.Count)
            {
                menu.AddDisabledItem(new GUIContent("当前角色没有可用的关卡操作"));
                return;
            }

            _activeContext = context.EditorContext;
            CharacterTestSceneCharacterDefinition character = _document.Characters[row.Index];
            bool hasTransform = TryResolveRowTransform(row, out Transform transform)
                && transform != null;
            if (hasTransform)
            {
                menu.AddItem(
                    new GUIContent("复制并放置"),
                    false,
                    () => BeginCharacterDuplicatePlacement(row));
                menu.AddItem(
                    new GUIContent("重新放置"),
                    false,
                    () => BeginCharacterRepositionPlacement(row));
                menu.AddItem(
                    new GUIContent("对齐网格"),
                    false,
                    () =>
                    {
                        int changed = CharacterTestLevelEditorPlacementUtility.AlignSelectionToGrid(
                            new[] { transform },
                            "对齐 CharacterTest 场景角色到网格");
                        _status = changed > 0
                            ? "已将角色对齐到网格。"
                            : "当前角色无需对齐。";
                        context.EditorContext?.RequestRefresh?.Invoke();
                        SceneView.RepaintAll();
                    });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("复制并放置（无法解析场景对象）"));
                menu.AddDisabledItem(new GUIContent("重新放置（无法解析场景对象）"));
                menu.AddDisabledItem(new GUIContent("对齐网格（无法解析场景对象）"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("仅移除登记"),
                false,
                () =>
                {
                    RemoveCharacterRegistration(character);
                    context.EditorContext?.RequestRefresh?.Invoke();
                });

            if (!hasTransform)
            {
                menu.AddDisabledItem(new GUIContent("删除场景对象（无法解析场景对象）"));
            }
            else if (IsReferencedByWorldObjectModule(transform))
            {
                menu.AddDisabledItem(new GUIContent(
                    "删除场景对象（仍被世界对象模块引用）"));
            }
            else
            {
                menu.AddItem(
                    new GUIContent("删除场景对象..."),
                    false,
                    () =>
                    {
                        DeleteCharacterSceneObject(row, character);
                        context.EditorContext?.RequestRefresh?.Invoke();
                    });
            }
        }

        private void RemoveCharacterRegistration(
            CharacterTestSceneCharacterDefinition character)
        {
            if (character == null)
                return;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("移除 CharacterTest 场景角色登记"))
            {
                _document.Characters.Remove(character);
                _selectedRow = null;
                MarkDirty();
                transaction.Commit();
            }
            _status = "已移除角色登记，场景对象仍然保留。";
        }

        private void DeleteCharacterSceneObject(
            Row row,
            CharacterTestSceneCharacterDefinition character)
        {
            if (row == null
                || character == null
                || !TryResolveRowTransform(row, out Transform transform)
                || transform == null)
            {
                _status = "无法解析当前角色 Transform。";
                return;
            }

            if (IsReferencedByWorldObjectModule(transform))
            {
                _status = "该对象仍被世界对象模块引用，已阻止删除。";
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除场景角色对象",
                    "将删除场景对象“" + transform.name + "”并移除角色登记。此操作可以撤销。",
                    "删除",
                    "取消"))
            {
                return;
            }

            int removedRegistrations = DeleteCharacterObjectAndRegistrations(transform);
            _selectedRow = null;
            _status = "已删除场景角色对象并移除 " + removedRegistrations + " 条登记。";
        }

        private int DeleteCharacterObjectAndRegistrations(Transform transform)
        {
            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("删除 CharacterTest 场景角色"))
            {
                int removedRegistrations = RemoveAllCharacterReferencesToTransform(transform);
                Undo.DestroyObjectImmediate(transform.gameObject);
                MarkDirty();
                transaction.Commit();
                return removedRegistrations;
            }
        }

        private int RemoveAllCharacterReferencesToTransform(Transform transform)
        {
            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? null : gameManager.WorldObjectSceneRoot;
            if (root == null || transform == null)
                return 0;

            int removed = 0;
            for (int i = _document.Characters.Count - 1; i >= 0; i--)
            {
                CharacterTestSceneCharacterDefinition candidate = _document.Characters[i];
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        candidate?.TransformRef,
                        out Transform resolved)
                    || resolved != transform)
                {
                    continue;
                }

                _document.Characters.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        internal int DeleteRowForTests(
            MxLevelEditorListRow row,
            out GameObject deletedObject)
        {
            deletedObject = null;
            if (row?.Payload is not Row payload
                || !TryResolveRowTransform(payload, out Transform transform)
                || transform == null
                || IsReferencedByWorldObjectModule(transform))
            {
                return 0;
            }

            deletedObject = transform.gameObject;
            return DeleteCharacterObjectAndRegistrations(transform);
        }

        private bool IsReferencedByWorldObjectModule(Transform transform)
        {
            if (!CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    _envelope,
                    out LevelWorldObjectAuthoringDocument worldObjects,
                    out _))
            {
                return false;
            }

            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? null : gameManager.WorldObjectSceneRoot;
            if (root == null)
                return false;

            return ContainsTransform(worldObjects.MovableInstances, root, transform)
                || ContainsTransform(worldObjects.SingleStaticColliders, root, transform)
                || ContainsBatchTransform(worldObjects.StaticColliderBatches, root, transform)
                || ContainsPointTransform(worldObjects.Points, root, transform)
                || ContainsRegionTransform(worldObjects.Regions, root, transform);
        }

        private static bool ContainsTransform(
            List<LevelWorldObjectInstanceDefinition> instances,
            Transform root,
            Transform target)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        instances[i]?.TransformRef,
                        out Transform resolved)
                    && resolved == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBatchTransform(
            List<LevelWorldObjectStaticColliderBatchDefinition> batches,
            Transform root,
            Transform target)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        batches[i]?.ContainerTransformRef,
                        out Transform container)
                    || container == null)
                {
                    continue;
                }

                if (container == target || target.parent == container)
                    return true;
            }

            return false;
        }

        private static bool ContainsPointTransform(
            List<LevelWorldObjectPointDefinition> points,
            Transform root,
            Transform target)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        points[i]?.TransformRef,
                        out Transform resolved)
                    && resolved == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsRegionTransform(
            List<LevelWorldObjectRegionDefinition> regions,
            Transform root,
            Transform target)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        regions[i]?.TransformRef,
                        out Transform resolved)
                    && resolved == target)
                {
                    return true;
                }
            }

            return false;
        }

        private string CreateUniqueCharacterStableId(string sourceStableId)
        {
            string basis = string.IsNullOrWhiteSpace(sourceStableId)
                ? "character.copy"
                : sourceStableId.Trim() + ".copy";
            string candidate = basis;
            int suffix = 2;
            while (ContainsStableId(candidate))
                candidate = basis + suffix++;
            return candidate;
        }

        private static string CreateUniqueCharacterSiblingName(
            Transform parent,
            string baseName)
        {
            string normalized = string.IsNullOrWhiteSpace(baseName)
                ? "Character Copy"
                : baseName.Trim();
            if (parent == null || parent.Find(normalized) == null)
                return normalized;

            int suffix = 2;
            while (parent.Find(normalized + " " + suffix) != null)
                suffix++;
            return normalized + " " + suffix;
        }

        private sealed class CharacterPlacementResult
        {
            private CharacterPlacementResult(bool success, GameObject gameObject, string status)
            {
                Success = success;
                GameObject = gameObject;
                Status = status ?? string.Empty;
            }

            public bool Success { get; }
            public GameObject GameObject { get; }
            public string Status { get; }

            public static CharacterPlacementResult Succeeded(
                GameObject gameObject,
                string status)
            {
                return new CharacterPlacementResult(true, gameObject, status);
            }

            public static CharacterPlacementResult Failed(string status)
            {
                return new CharacterPlacementResult(false, null, status);
            }
        }
    }
}
