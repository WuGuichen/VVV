using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor
{
    internal sealed class WGameCharacterStylizedLitShaderGui : ShaderGUI
    {
        internal const string ShaderName = "WGame/CharacterStylizedLit";
        internal const string FullTypeName = "MxFramework.Project.Editor.WGameCharacterStylizedLitShaderGui";
        private static bool _lightingExpanded;
        private static bool _effectsExpanded;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            if (editor == null || properties == null) return;
            bool generated = false;
            foreach (UnityEngine.Object target in editor.targets)
                generated |= AssetDatabase.GetAssetPath(target).Replace('\\', '/').StartsWith(
                    "Assets/MxFrameworkGenerated/", StringComparison.OrdinalIgnoreCase);

            EditorGUILayout.LabelField("角色烘焙材质", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(generated
                ? "底色、材质控制图、头发参数和渲染面由角色烘焙描述导入。请在 Blender 修改并重新发布。"
                : "使用烘焙底色和材质控制图；UV0 为贴图，UV1 / UV2 为发流与根梢数据。", MessageType.Info);
            EditorGUILayout.LabelField("烘焙贴图", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(generated))
            {
                Draw(editor, properties, "_WgBakedBaseColor", "底色（sRGB）");
                Draw(editor, properties, "_WgSurfaceDataTex", "材质控制（AO / 边缘光 / 发光 / 类型）");
                Draw(editor, properties, "_Cull", "渲染面");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("头发", EditorStyles.boldLabel);
                Draw(editor, properties, "_WgHairLobes", "主宽度 / 强度、副宽度 / 强度");
                Draw(editor, properties, "_WgHairShape", "主偏移 / 副偏移 / 根部暗化 / 逆光");
            }
            foreach (UnityEngine.Object target in editor.targets)
                if (target is Material material && (material.GetTexture("_WgBakedBaseColor") == null
                    || material.GetTexture("_WgSurfaceDataTex") == null))
                {
                    EditorGUILayout.HelpBox("烘焙贴图未完整绑定。旧色板或纯色材质请使用 MxFramework → Rendering → Migrate Character Shaders。", MessageType.Warning);
                    break;
                }
            EditorGUILayout.HelpBox("直接光与间接光统一由 Scene Lighting 响应；冷暖和明暗色阶在共享 Look 中调整。", MessageType.Info);
            _lightingExpanded = EditorGUILayout.Foldout(_lightingExpanded, "材质表面响应（高级）", true);
            if (_lightingExpanded)
                foreach (MaterialProperty property in properties)
                    if (property.name.StartsWith("_WgPaletteEdge", StringComparison.Ordinal)
                        || property.name == "_WgPaletteDetailParams" || property.name == "_WgPaletteRimPower")
                        editor.ShaderProperty(property, property.displayName);
            _effectsExpanded = EditorGUILayout.Foldout(_effectsExpanded, "运行时效果（调试）", true);
            if (_effectsExpanded)
            {
                EditorGUILayout.HelpBox("由运行时材质绑定驱动；手动调节仅用于预览。", MessageType.None);
                foreach (MaterialProperty property in properties)
                    if (property.name.StartsWith("_WgHit", StringComparison.Ordinal)
                        || property.name.StartsWith("_WgStatus", StringComparison.Ordinal)
                        || property.name.StartsWith("_WgDissolve", StringComparison.Ordinal)
                        || property.name.StartsWith("_WgOutline", StringComparison.Ordinal))
                        Draw(editor, properties, property.name, property.displayName);
            }
        }

        private static void Draw(MaterialEditor editor, MaterialProperty[] properties, string name, string label)
        {
            MaterialProperty property = FindProperty(name, properties, false);
            if (property == null) return;
            if (property.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                editor.TexturePropertySingleLine(new GUIContent(label), property);
            else editor.ShaderProperty(property, label);
        }
    }
}
