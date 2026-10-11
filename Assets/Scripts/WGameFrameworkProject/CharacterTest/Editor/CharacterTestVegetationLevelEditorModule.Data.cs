using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestVegetationLevelEditorModule
    {
        private void BuildDataBindingControls(VisualElement body)
        {
            var data = new ObjectField("植被数据资产") { objectType = typeof(CharacterTestVegetationData),
                allowSceneObjects = false, value = _owner?.Data, name = "vegetation-data" };
            data.RegisterValueChangedCallback(e =>
            {
                if (e.newValue == null) { data.SetValueWithoutNotify(_owner?.Data); return; }
                FinishGesture(false);
                try
                {
                    _owner = CharacterTestVegetationAssetAuthoring.Bind(SceneManager.GetActiveScene(), (CharacterTestVegetationData)e.newValue);
                    _selected.Clear(); EnableTypeInstancing();
                    _status = "已绑定植被数据资产；点击顶部保存场景关联。";
                    Changed(); _context?.RequestRefresh?.Invoke();
                }
                catch (Exception error) { data.SetValueWithoutNotify(_owner?.Data); _status = error.Message; _window?.RefreshWorkspaceState(); }
            });
            body.Add(data);
            if (_owner == null)
            {
                body.Add(new HelpBox("创建新的独立数据资产，或在上方选择已有资产。选择同一资产会共享实例与类型；不会导入场景里的草对象。", HelpBoxMessageType.Info));
                body.Add(new Button(Initialize) { text = "创建植被数据并绑定", name = "vegetation-create-data" });
            }
            else
            {
                body.Add(new Button(() => Selection.activeObject = _owner.Data) { text = "定位数据资产" });
                var enabled = new Toggle("启用植被") { value = _owner.Binding.Enabled, name = "vegetation-enabled" };
                enabled.RegisterValueChangedCallback(e =>
                {
                    FinishGesture(false); Undo.RecordObject(_owner.BindingOwner, "启停植被绑定"); _owner.Binding.Enabled = e.newValue; Changed();
                }); body.Add(enabled);
            }
        }

        private void EnableTypeInstancing()
        {
            foreach (var type in _owner.Types)
            {
                var material = type.Prefab != null ? type.Prefab.GetComponent<MeshRenderer>()?.sharedMaterial : null;
                if (material == null || material.enableInstancing) continue;
                Undo.RecordObject(material, "启用植被实例化"); material.enableInstancing = true;
                EditorUtility.SetDirty(material); _editedMaterials.Add(material);
            }
        }

        private void BuildTypeControls(VisualElement body)
        {
            var types = new Foldout { text = "植被类型与几何 LOD", value = _owner.Types.Count == 0, name = "vegetation-types" };
            types.Add(new Button(() =>
            {
                FinishGesture(false); Undo.RegisterCompleteObjectUndo(_owner.Data, "添加植被类型");
                int id = 1; foreach (var item in _owner.Types) id = Math.Max(id, item.Id + 1);
                _owner.EditableTypes.Add(new CharacterTestVegetationType { Id = id,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestVegetationAuthoring.GrassPrefabPath) });
                EnableTypeInstancing(); Changed(); _context?.RequestRefresh?.Invoke();
            }) { text = "添加类型", name = "vegetation-add-type" });
            foreach (var type in _owner.Types)
            {
                var row = new Foldout { text = type.Id + ": " + type.Name };
                row.Add(new Label("类型 ID " + type.Id + "（实例引用，保持不变）"));
                var title = new TextField("名称") { value = type.Name };
                title.RegisterValueChangedCallback(e => EditType(() => type.Name = e.newValue)); row.Add(title);
                var prefab = new ObjectField("草 Prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = type.Prefab };
                prefab.RegisterValueChangedCallback(e => EditType(() => { type.Prefab = (GameObject)e.newValue; EnableTypeInstancing(); })); row.Add(prefab);
                AddMeshField(row, "LOD1 网格", type.Lod1Mesh, mesh => type.Lod1Mesh = mesh);
                AddMeshField(row, "LOD2 网格", type.Lod2Mesh, mesh => type.Lod2Mesh = mesh);
                AddTypeFloat(row, "LOD0 屏幕高度阈值", type.Lod0ScreenHeight, x => type.Lod0ScreenHeight = x);
                AddTypeFloat(row, "LOD1 屏幕高度阈值", type.Lod1ScreenHeight, x => type.Lod1ScreenHeight = x);
                AddTypeFloat(row, "LOD 滞回", type.LodHysteresis, x => type.LodHysteresis = x);
                AddTypeFloat(row, "显示距离（米）", type.DrawDistance, x => type.DrawDistance = x);
                var cast = new Toggle("投射固定方向阴影") { value = type.CastFixedDirectionalShadow };
                cast.RegisterValueChangedCallback(e => EditType(() => type.CastFixedDirectionalShadow = e.newValue)); row.Add(cast);
                bool used = false; foreach (var item in _owner.Instances) if (item.TypeId == type.Id) { used = true; break; }
                var remove = new Button(() =>
                {
                    FinishGesture(false);
                    try { CharacterTestVegetationAuthoring.RemoveType(_owner, type.Id); Changed(); _context?.RequestRefresh?.Invoke(); }
                    catch (Exception error) { _status = error.Message; _window?.RefreshWorkspaceState(); }
                }) { text = used ? "已有实例引用，不能删除类型" : "删除类型" };
                remove.SetEnabled(!used); row.Add(remove); types.Add(row);
            }
            types.Add(new Label("未配置或无效的低档网格保持原始几何。修改固定阴影输入后需显式重新烘焙。"));
            body.Add(types);
            var settings = new Foldout { text = "分类与批处理设置", value = false };
            var serialized = new SerializedObject(_owner.Data);
            foreach (string field in new[] { "_chunkSize", "_drawBatchSize", "_classificationMode" })
            {
                var property = new PropertyField(serialized.FindProperty(field));
                property.RegisterCallback<SerializedPropertyChangeEvent>(_ => Changed()); settings.Add(property);
            }
            settings.Bind(serialized); body.Add(settings);
        }

        private void AddMeshField(VisualElement row, string label, Mesh value, Action<Mesh> apply)
        {
            var field = new ObjectField(label) { objectType = typeof(Mesh), allowSceneObjects = false, value = value };
            field.RegisterValueChangedCallback(e => EditType(() => apply((Mesh)e.newValue))); row.Add(field);
        }
        private void AddTypeFloat(VisualElement row, string label, float value, Action<float> apply)
        {
            var field = new FloatField(label) { value = value };
            field.RegisterValueChangedCallback(e => EditType(() => apply(e.newValue))); row.Add(field);
        }
        private void EditType(Action edit)
        {
            FinishGesture(false); Undo.RegisterCompleteObjectUndo(_owner.Data, "修改植被类型"); edit(); Changed();
        }
    }
}
