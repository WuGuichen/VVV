using System;
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
        public void BuildSceneContextMenu(
            MxLevelEditorSceneViewContext context,
            GenericMenu menu)
        {
            if (menu == null)
                return;
            if (context?.SelectedRow?.Payload is not Row row)
            {
                menu.AddDisabledItem(new GUIContent("当前对象没有可用的关卡操作"));
                return;
            }

            _activeContext = context.EditorContext;
            if (BuildGameplayTriggerSceneContextMenu(row, menu))
                menu.AddSeparator(string.Empty);
            bool supportsObjectPlacement = row.Kind != RowKind.StaticBatch;
            bool hasTransform = TryResolveRowTransform(row, out Transform transform)
                && transform != null;
            if (supportsObjectPlacement && hasTransform)
            {
                menu.AddItem(
                    new GUIContent("复制并放置"),
                    false,
                    () => BeginDuplicatePlacement(row));
                menu.AddItem(
                    new GUIContent("重新放置"),
                    false,
                    () => BeginRepositionPlacement(row));
                menu.AddItem(
                    new GUIContent("对齐网格"),
                    false,
                    () =>
                    {
                        int changed = CharacterTestLevelEditorPlacementUtility.AlignSelectionToGrid(
                            new[] { transform },
                            "对齐 CharacterTest 世界对象到网格");
                        _status = changed > 0
                            ? "已将对象对齐到网格。"
                            : "当前对象无需对齐。";
                        context.EditorContext?.RequestRefresh?.Invoke();
                        SceneView.RepaintAll();
                    });
            }
            else
            {
                string reason = row.Kind == RowKind.StaticBatch
                    ? "（静态批次暂不支持）"
                    : "（无法解析场景对象）";
                menu.AddDisabledItem(new GUIContent("复制并放置" + reason));
                menu.AddDisabledItem(new GUIContent("重新放置" + reason));
                menu.AddDisabledItem(new GUIContent("对齐网格" + reason));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("仅移除登记"),
                false,
                () =>
                {
                    RemoveSelectedRegistration(row);
                    context.EditorContext?.RequestRefresh?.Invoke();
                });

            if (!supportsObjectPlacement || !hasTransform)
            {
                menu.AddDisabledItem(new GUIContent(
                    row.Kind == RowKind.StaticBatch
                        ? "删除场景对象（静态批次暂不支持）"
                        : "删除场景对象（无法解析场景对象）"));
            }
            else if (IsReferencedByStaticBatch(transform))
            {
                menu.AddDisabledItem(new GUIContent(
                    "删除场景对象（仍被静态碰撞体批次引用）"));
            }
            else if (TryGetExternalReferenceSource(transform, out string referenceSource))
            {
                menu.AddDisabledItem(new GUIContent(
                    "删除场景对象（仍被" + referenceSource + "引用）"));
            }
            else
            {
                menu.AddItem(
                    new GUIContent("删除场景对象..."),
                    false,
                    () =>
                    {
                        DeleteSelectedSceneObject(row);
                        context.EditorContext?.RequestRefresh?.Invoke();
                    });
            }
        }

        private VisualElement CreateQuickPlacementActions(Row row)
        {
            var actions = new VisualElement
            {
                name = "character-test-world-object-quick-actions"
            };
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(CreateSmallButton("重新放置", () => BeginRepositionPlacement(row)));
            actions.Add(CreateSmallButton("复制并放置", () => BeginDuplicatePlacement(row)));

            var more = new ToolbarMenu { text = "更多" };
            more.menu.AppendAction("对齐网格", _ =>
            {
                int changed = CharacterTestLevelEditorPlacementUtility.AlignSelectionToGrid(
                    Selection.transforms,
                    "对齐 CharacterTest 世界对象到网格");
                _status = changed > 0
                    ? "已将 " + changed + " 个对象对齐到网格。"
                    : "当前选择无需对齐。";
                SceneView.RepaintAll();
            });
            more.menu.AppendSeparator();
            more.menu.AppendAction("仅移除登记", _ => RemoveSelectedRegistration(row));
            more.menu.AppendAction("删除场景对象...", _ => DeleteSelectedSceneObject(row));
            actions.Add(more);
            return actions;
        }

        private VisualElement CreateRegionInspectorActions(
            LevelTransformReference transformRef,
            Row row)
        {
            var actions = new VisualElement
            {
                name = "character-test-region-actions"
            };
            actions.AddToClassList(
                "mx-level-editor__inspector-actions");
            actions.AddToClassList(
                "mx-level-editor__region-actions");
            actions.Add(CreateSmallButton(
                "选择",
                () => SelectTransform(transformRef)));
            actions.Add(CreateSmallButton(
                "定位",
                () => PingTransform(transformRef)));
            actions.Add(CreateSmallButton(
                "重新放置",
                () => BeginRepositionPlacement(row)));

            var more = new ToolbarMenu
            {
                name = "character-test-region-more",
                text = "更多"
            };
            more.AddToClassList(
                "mx-level-editor__region-more-action");
            more.menu.AppendAction(
                "聚焦场景对象",
                _ => FrameTransform(transformRef));
            more.menu.AppendAction(
                "复制并放置",
                _ => BeginDuplicatePlacement(row));
            more.menu.AppendAction(
                "对齐网格",
                _ =>
                {
                    if (!TryResolveRowTransform(
                            row,
                            out Transform transform)
                        || transform == null)
                    {
                        _status = "无法解析当前区域的场景对象。";
                        return;
                    }

                    int changed =
                        CharacterTestLevelEditorPlacementUtility
                            .AlignSelectionToGrid(
                                new[] { transform },
                                "对齐 CharacterTest 区域到网格");
                    _status = changed > 0
                        ? "已将区域对齐到网格。"
                        : "当前区域无需对齐。";
                    SceneView.RepaintAll();
                });
            more.menu.AppendSeparator();
            more.menu.AppendAction(
                "仅移除登记",
                _ => RemoveSelectedRegistration(row));
            more.menu.AppendAction(
                "删除场景对象...",
                _ => DeleteSelectedSceneObject(row));
            actions.Add(more);
            return actions;
        }

        private void RemoveSelectedRegistration(Row row)
        {
            if (row == null)
            {
                return;
            }

            if (TryGetGameplayTriggerReferenceSource(
                    row,
                    out string triggerReferenceSource))
            {
                _status = "该区域仍被"
                    + triggerReferenceSource
                    + "引用，已阻止移除登记。请先删除 Trigger。";
                return;
            }

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("移除 CharacterTest 世界对象登记"))
            {
                RemoveRowFromDocument(row);
                MarkDirty();
                transaction.Commit();
            }
            _selectedRow = null;
            RefreshInspector();
            _status = "已移除登记，场景对象仍然保留。";
        }

        private void DeleteSelectedSceneObject(Row row)
        {
            if (row == null
                || row.Kind == RowKind.StaticBatch
                || !TryResolveRowTransform(row, out Transform transform)
                || transform == null)
            {
                _status = "当前条目不能删除场景对象。";
                return;
            }

            if (IsReferencedByStaticBatch(transform))
            {
                _status = "该对象仍被静态碰撞体批次引用，已阻止删除。";
                return;
            }

            if (TryGetExternalReferenceSource(transform, out string referenceSource))
            {
                _status = "该对象仍被" + referenceSource + "引用，已阻止删除。";
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除场景对象",
                    "将删除场景对象“" + transform.name + "”并移除对应登记。此操作可以撤销。",
                    "删除",
                    "取消"))
            {
                return;
            }

            int removedRegistrations = DeleteSceneObjectAndRegistrations(transform);
            _selectedRow = null;
            RefreshInspector();
            _status = "已删除场景对象并移除 " + removedRegistrations + " 条登记。";
        }

        private int DeleteSceneObjectAndRegistrations(Transform transform)
        {
            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("删除 CharacterTest 世界对象"))
            {
                GameObject gameObject = transform.gameObject;
                int removedRegistrations = RemoveAllDirectReferencesToTransform(transform);
                Undo.DestroyObjectImmediate(gameObject);
                MarkDirty();
                transaction.Commit();
                return removedRegistrations;
            }
        }

        private void RemoveRowFromDocument(Row row)
        {
            switch (row.Kind)
            {
                case RowKind.Movable:
                    _document.MovableInstances.RemoveAt(row.Index);
                    break;
                case RowKind.SingleStatic:
                    _document.SingleStaticColliders.RemoveAt(row.Index);
                    break;
                case RowKind.StaticBatch:
                    _document.StaticColliderBatches.RemoveAt(row.Index);
                    break;
                case RowKind.Point:
                    _document.Points.RemoveAt(row.Index);
                    break;
                case RowKind.Region:
                    _document.Regions.RemoveAt(row.Index);
                    break;
            }
        }

        private bool IsReferencedByStaticBatch(Transform transform)
        {
            for (int i = 0; i < _document.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch =
                    _document.StaticColliderBatches[i];
                if (batch == null)
                    continue;
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        FindSceneCacheTarget()?.Root,
                        batch.ContainerTransformRef,
                        out Transform container)
                    || container == null)
                {
                    continue;
                }

                if (container == transform || transform.parent == container)
                    return true;
            }

            return false;
        }

        private int RemoveAllDirectReferencesToTransform(Transform transform)
        {
            Transform root = FindSceneCacheTarget()?.Root;
            if (root == null || transform == null)
                return 0;

            int removed = 0;
            for (int i = _document.MovableInstances.Count - 1; i >= 0; i--)
            {
                if (ResolvesToTransform(_document.MovableInstances[i]?.TransformRef, root, transform))
                {
                    _document.MovableInstances.RemoveAt(i);
                    removed++;
                }
            }

            for (int i = _document.SingleStaticColliders.Count - 1; i >= 0; i--)
            {
                if (ResolvesToTransform(_document.SingleStaticColliders[i]?.TransformRef, root, transform))
                {
                    _document.SingleStaticColliders.RemoveAt(i);
                    removed++;
                }
            }

            for (int i = _document.Points.Count - 1; i >= 0; i--)
            {
                if (ResolvesToTransform(_document.Points[i]?.TransformRef, root, transform))
                {
                    _document.Points.RemoveAt(i);
                    removed++;
                }
            }

            for (int i = _document.Regions.Count - 1; i >= 0; i--)
            {
                if (ResolvesToTransform(_document.Regions[i]?.TransformRef, root, transform))
                {
                    _document.Regions.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private static bool ResolvesToTransform(
            LevelTransformReference transformRef,
            Transform root,
            Transform target)
        {
            return CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    root,
                    transformRef,
                    out Transform resolved)
                && resolved == target;
        }

        internal int DeleteRowForTests(
            MxLevelEditorListRow row,
            out GameObject deletedObject)
        {
            deletedObject = null;
            if (row?.Payload is not Row payload
                || payload.Kind == RowKind.StaticBatch
                || !TryResolveRowTransform(payload, out Transform transform)
                || transform == null
                || IsReferencedByStaticBatch(transform)
                || TryGetExternalReferenceSource(transform, out _))
            {
                return 0;
            }

            deletedObject = transform.gameObject;
            return DeleteSceneObjectAndRegistrations(transform);
        }

        private bool TryGetExternalReferenceSource(Transform transform, out string source)
        {
            source = string.Empty;
            if (TryGetGameplayTriggerReferenceSource(transform, out source))
                return true;

            if (_envelope == null
                || !CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    _envelope,
                    out CharacterTestSceneCharacterAuthoringDocument characters,
                    out _)
                || characters == null)
            {
                return false;
            }

            Transform root = FindSceneCacheTarget()?.Root;
            for (int i = 0; i < characters.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character = characters.Characters[i];
                if (character == null
                    || !CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        character.TransformRef,
                        out Transform referenced)
                    || referenced != transform)
                {
                    continue;
                }

                source = "场景角色“" + character.DisplayName + "”";
                return true;
            }

            return false;
        }
    }
}
