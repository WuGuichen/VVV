using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor
{
    internal sealed class WGameCharacterTraditionalLitShaderGui : ShaderGUI
    {
        internal const string ShaderName = "WGame/CharacterTraditionalLit";
        internal const string FullTypeName = "MxFramework.Project.Editor.WGameCharacterTraditionalLitShaderGui";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null)
                return;

            EditorGUILayout.LabelField("WGame Character Traditional Lit", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Traditional Surface", EditorStyles.boldLabel);
            MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
            MaterialProperty color = FindProperty("_Color", properties, false);
            if (baseMap != null)
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, color);
            DrawProperty(materialEditor, properties, "_AlphaClip", "Alpha Clip");
            MaterialProperty alphaClip = FindProperty("_AlphaClip", properties, false);
            if (alphaClip != null && (alphaClip.hasMixedValue || alphaClip.floatValue > 0.5f))
            {
                using (new EditorGUI.IndentLevelScope())
                    DrawProperty(materialEditor, properties, "_Cutoff", "Cutoff");
            }
            DrawTexture(materialEditor, properties, "_WgNormalTex", "Normal Texture");
            DrawTexture(materialEditor, properties, "_WgMaskTex", "Material Mask");
            DrawTexture(materialEditor, properties, "_WgEmissionMaskTex", "Emission Mask");
            DrawProperty(materialEditor, properties, "_WgMagicCoreColor", "Emission Color");
            DrawProperty(materialEditor, properties, "_Cull", "Render Face");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Runtime Effects (Debug)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These properties are normally driven by MaterialBindingHub or runtime presentation systems.",
                MessageType.None);
            DrawProperty(materialEditor, properties, "_WgHitFlashColor", "Hit Flash Color");
            DrawProperty(materialEditor, properties, "_WgHitFlashAmount", "Hit Flash Amount");
            DrawProperty(materialEditor, properties, "_WgStatusTintColor", "Status Tint Color");
            DrawProperty(materialEditor, properties, "_WgStatusTintAmount", "Status Tint Amount");
            DrawProperty(materialEditor, properties, "_WgDissolveProgress", "Dissolve Progress");
            DrawTexture(materialEditor, properties, "_WgDissolveNoiseTex", "Dissolve Noise");
            DrawProperty(materialEditor, properties, "_WgDissolveEdgeColor", "Dissolve Edge Color");
            DrawProperty(materialEditor, properties, "_WgDissolveEdgeWidth", "Dissolve Edge Width");
            DrawProperty(materialEditor, properties, "_WgOutlineColor", "Outline / Rim Color");
            DrawProperty(materialEditor, properties, "_WgOutlineAmount", "Outline / Rim Amount");
        }

        public override void ValidateMaterial(Material material)
        {
            if (material == null)
                return;
            SetKeyword(material, "_ALPHATEST_ON", IsEnabled(material, "_AlphaClip"));
            SetKeyword(material, "_WG_NORMALMAP_ON", HasTexture(material, "_WgNormalTex"));
            SetKeyword(material, "_WG_MATERIAL_MASK_ON", HasTexture(material, "_WgMaskTex"));
            SetKeyword(material, "_WG_EMISSION_ON", HasTexture(material, "_WgEmissionMaskTex"));
        }

        private static bool IsEnabled(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
        }

        private static bool HasTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetTexture(propertyName) != null;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static void DrawTexture(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty property = FindProperty(propertyName, properties, false);
            if (property != null)
                editor.TexturePropertySingleLine(new GUIContent(label), property);
        }

        private static void DrawProperty(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty property = FindProperty(propertyName, properties, false);
            if (property != null)
                editor.ShaderProperty(property, label);
        }
    }
}
