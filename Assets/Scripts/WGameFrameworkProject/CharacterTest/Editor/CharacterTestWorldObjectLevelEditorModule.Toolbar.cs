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
        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar)
        {
            EnsureLoaded();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.NoWrap;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.justifyContent = Justify.FlexEnd;
            toolbar.style.minWidth = 0;

            if (_unregisteredSceneObjects.Count > 0)
            {
                var unmanaged = new Button(() =>
                {
                    SelectFirstUnregisteredSceneObject();
                    window?.RepaintRequested();
                })
                {
                    name = "character-test-unregistered-scene-objects",
                    text = "未登记 " + _unregisteredSceneObjects.Count,
                    tooltip = "选择第一个未登记的场景对象，然后为它指定关卡类型。"
                };
                unmanaged.AddToClassList("mx-level-editor__warning-action");
                toolbar.Add(unmanaged);
            }

            var addMenu = new ToolbarMenu
            {
                name = "character-test-world-object-add-menu",
                text = "添加当前选中"
            };
            addMenu.AddToClassList("mx-level-editor__add-menu");
            addMenu.menu.AppendAction("添加可移动物体", _ =>
            {
                AddSelected(LevelWorldObjectRole.Movable, _selectedMovableDefinitionId);
                window.RepaintRequested();
            });
            addMenu.menu.AppendAction("添加静态碰撞体", _ =>
            {
                AddSelected(LevelWorldObjectRole.StaticCollider, _selectedStaticDefinitionId);
                window.RepaintRequested();
            });
            addMenu.menu.AppendAction("从选中对象创建静态批次", _ =>
            {
                AddStaticBatchFromSelection();
                window.RepaintRequested();
            });
            addMenu.menu.AppendSeparator();
            addMenu.menu.AppendAction("添加关卡点", _ =>
            {
                AddPointFromSelection();
                window.RepaintRequested();
            });
            addMenu.menu.AppendAction("添加区域", _ =>
            {
                AddRegionFromSelection();
                window.RepaintRequested();
            });
            toolbar.Add(addMenu);
        }

        public void BuildToolsMenu(DropdownMenu menu, MxLevelEditorWindow window)
        {
            menu.AppendAction("导出场景源到默认 JSON", _ =>
            {
                ExportCurrentSceneSourceToDefaultJson();
                Reload();
                window?.RepaintRequested();
            });
            menu.AppendAction("修复 Transform 引用", _ =>
            {
                RepairTransformRefs();
                window?.RepaintRequested();
            });
        }

        private VisualElement CreateSourceToolbar(MxLevelEditorWindow window)
        {
            VisualElement actions = CreateToolbarRow("character-test-world-object-source-actions");
            MxLevelEditorWindow.AddToolbarButton(actions, "导出场景源", () =>
            {
                ExportCurrentSceneSourceToDefaultJson();
                Reload();
                window.RepaintRequested();
            });
            MxLevelEditorWindow.AddToolbarButton(actions, "修复引用", () =>
            {
                RepairTransformRefs();
                window.RepaintRequested();
            });
            return actions;
        }

        private VisualElement CreateAddOperationToolbar(MxLevelEditorWindow window)
        {
            VisualElement root = CreateToolbarRow("character-test-world-object-add-area");
            root.style.marginTop = 2;

            root.Add(CreateAddGroup(
                "character-test-add-movable",
                CreateDefinitionPopup(
                    "可移动物体",
                    _selectedMovableDefinitionId,
                    definition => definition.CanSpawnMovableNaturalObject,
                    value => _selectedMovableDefinitionId = value,
                    "character-test-toolbar-movable-definition"),
                "添加可移动物体",
                () =>
                {
                    AddSelected(LevelWorldObjectRole.Movable, _selectedMovableDefinitionId);
                    window.RepaintRequested();
                }));
            root.Add(CreateAddGroup(
                "character-test-add-static",
                CreateDefinitionPopup(
                    "静态碰撞体",
                    _selectedStaticDefinitionId,
                    definition => definition.CanSpawnStaticCollider,
                    value => _selectedStaticDefinitionId = value,
                    "character-test-toolbar-static-definition"),
                "添加静态碰撞体",
                () =>
                {
                    AddSelected(LevelWorldObjectRole.StaticCollider, _selectedStaticDefinitionId);
                    window.RepaintRequested();
                }));
            root.Add(CreateSmallButton("创建静态批次", () =>
            {
                AddStaticBatchFromSelection();
                window.RepaintRequested();
            }));
            root.Add(CreateAddGroup(
                "character-test-add-point",
                CreateDefinitionPopup(
                    "关卡点",
                    _selectedPointDefinitionId,
                    definition => definition.CanSpawnPoint,
                    value => _selectedPointDefinitionId = value,
                    "character-test-toolbar-point-definition"),
                "添加关卡点",
                () =>
                {
                    AddPointFromSelection();
                    window.RepaintRequested();
                }));
            root.Add(CreateAddGroup(
                "character-test-add-region",
                CreateDefinitionPopup(
                    "区域",
                    _selectedRegionDefinitionId,
                    definition => definition.CanSpawnRegion,
                    value => _selectedRegionDefinitionId = value,
                    "character-test-toolbar-region-definition"),
                "添加区域",
                () =>
                {
                    AddRegionFromSelection();
                    window.RepaintRequested();
                }));
            return root;
        }

        private static VisualElement CreateAddGroup(
            string name,
            VisualElement selector,
            string buttonText,
            Action action)
        {
            var group = new VisualElement { name = name };
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginRight = 10;
            group.style.marginBottom = 3;
            group.Add(selector);
            group.Add(CreateSmallButton(buttonText, action));
            return group;
        }

        private VisualElement CreateDefinitionToolbar()
        {
            var root = CreateToolbarRow("character-test-world-object-definitions");
            root.style.marginTop = 3;

            root.Add(CreateDefinitionPopup(
                "可移动物体",
                _selectedMovableDefinitionId,
                definition => definition.CanSpawnMovableNaturalObject,
                value => _selectedMovableDefinitionId = value,
                "character-test-toolbar-movable-definition"));
            root.Add(CreateDefinitionPopup(
                "静态碰撞体",
                _selectedStaticDefinitionId,
                definition => definition.CanSpawnStaticCollider,
                value => _selectedStaticDefinitionId = value,
                "character-test-toolbar-static-definition"));
            root.Add(CreateDefinitionPopup(
                "关卡点",
                _selectedPointDefinitionId,
                definition => definition.CanSpawnPoint,
                value => _selectedPointDefinitionId = value,
                "character-test-toolbar-point-definition"));
            root.Add(CreateDefinitionPopup(
                "区域",
                _selectedRegionDefinitionId,
                definition => definition.CanSpawnRegion,
                value => _selectedRegionDefinitionId = value,
                "character-test-toolbar-region-definition"));
            return root;
        }

        private static VisualElement CreateToolbarRow(string name)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.flexShrink = 0;
            row.style.minWidth = 0;
            row.style.marginBottom = 2;
            return row;
        }

        private PopupField<string> CreateDefinitionPopup(
            string label,
            int selectedId,
            Func<CharacterTestWorldObjectDefinition, bool> predicate,
            Action<int> setter,
            string fieldName = null)
        {
            CharacterTestWorldObjectDefinitionCatalog catalog = ActiveDefinitionCatalog;
            var ids = new List<int>();
            var labels = new List<string>();
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                CharacterTestWorldObjectDefinition definition = catalog.Definitions[i];
                if (!predicate(definition))
                    continue;

                ids.Add(definition.DefinitionId);
                labels.Add(CharacterTestLevelEditorChineseText.DefinitionLabel(definition));
            }

            int index = Mathf.Clamp(ids.IndexOf(selectedId), 0, Math.Max(0, ids.Count - 1));
            var popup = new PopupField<string>(label, labels, index);
            if (!string.IsNullOrWhiteSpace(fieldName))
                popup.name = fieldName;
            popup.style.width = 220;
            popup.style.marginRight = 6;
            popup.RegisterValueChangedCallback(_ =>
            {
                int selected = Mathf.Clamp(labels.IndexOf(popup.value), 0, Math.Max(0, ids.Count - 1));
                if (ids.Count > 0)
                    setter(ids[selected]);
            });
            return popup;
        }

        private PopupField<string> CreateInspectorDefinitionPopup(
            int selectedId,
            Func<CharacterTestWorldObjectDefinition, bool> predicate,
            Action<int> setter)
        {
            PopupField<string> popup = CreateDefinitionPopup(
                "对象定义",
                selectedId,
                predicate,
                value =>
                {
                    setter(value);
                    MarkDirty();
                    if (_listView != null)
                        RefreshInspector();
                },
                "character-test-inspector-definition-popup");
            popup.style.width = Length.Percent(100);
            return popup;
        }

    }
}
