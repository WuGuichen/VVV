using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor
{
    internal sealed class WGameCharacterPaletteLegacyShaderGui : ShaderGUI
    {
        internal const string ShaderName = "WGame/CharacterPaletteLegacy";
        internal const string FullTypeName = "MxFramework.Project.Editor.WGameCharacterPaletteLegacyShaderGui";

        private const string PaletteKeyword = "_WG_PALETTE_ON";
        private const string FaceDetailKeyword = "_WG_FACE_DETAIL_ON";

        private static readonly string[] DetailQualityLabels =
        {
            "Low",
            "High"
        };

        private static bool _globalLookExpanded;
        private static bool _materialResponseExpanded;
        private static bool _technicalExpanded;
        private static bool _runtimeEffectsExpanded;
        private static bool _systemStateExpanded;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null || materialEditor.targets.Length == 0)
                return;

            if (!TryResolvePaletteMode(materialEditor.targets, out bool paletteMode, out bool mixedMode))
                return;

            EditorGUILayout.LabelField("WGame Character Palette — Historical Compatibility", EditorStyles.boldLabel);
            if (mixedMode)
            {
                EditorGUILayout.HelpBox(
                    "The selection mixes Palette and flat compatibility materials. Select one rendering mode at a time.",
                    MessageType.Info);
                materialEditor.PropertiesDefaultGUI(properties);
                return;
            }

            Material firstMaterial = materialEditor.target as Material;
            DrawAuthorityNotice(firstMaterial, paletteMode);

            if (paletteMode)
                DrawPaletteInspector(materialEditor, properties);
            else
                DrawFlatCompatibilityInspector(materialEditor, properties);
        }

        public override void ValidateMaterial(Material material)
        {
            if (material == null)
                return;

            SetKeyword(material, PaletteKeyword, IsEnabled(material, "_WgPaletteEnabled"));
            SetKeyword(material, FaceDetailKeyword, IsEnabled(material, "_WgFaceDetailEnabled"));
        }

        private static void DrawAuthorityNotice(Material material, bool paletteMode)
        {
            string path = material != null ? AssetDatabase.GetAssetPath(material) : string.Empty;
            bool generated = path.Replace('\\', '/').StartsWith(
                "Assets/MxFrameworkGenerated/",
                StringComparison.OrdinalIgnoreCase);

            if (paletteMode)
            {
                EditorGUILayout.HelpBox(
                    generated
                        ? "Generated Palette material. Color Atlas, dimensions, face mode and culling come from character import/runtime binding; Surface Profile is immutable PaletteData.A mesh data."
                        : "Palette material. Keep project-wide lighting consistent; use the Global Palette Look foldout only for shared LookDev.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    generated
                        ? "Generated flat compatibility material. Only Color, culling and shared runtime effects are supported."
                        : "Lightweight flat-color compatibility material.",
                    MessageType.Info);
            }
        }

        private static void DrawPaletteInspector(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Palette Appearance", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                DrawTextureSingleLine(editor, properties, "_WgColorPaletteTex", "Shared RGB Color Atlas");
                EditorGUILayout.LabelField("Color Palette", "256 x 256 RGB · all cells valid");
                EditorGUILayout.LabelField("Surface Profiles", "16 generated profiles · PaletteData.A");
                DrawProperty(editor, properties, "_Cull", "Render Face");
            }

            DrawFaceStatus(editor, properties);

            EditorGUILayout.Space(4f);
            _globalLookExpanded = EditorGUILayout.Foldout(
                _globalLookExpanded,
                "Global Palette Look (Advanced)",
                true);
            if (_globalLookExpanded)
                DrawGlobalPaletteLook(editor, properties);

            _runtimeEffectsExpanded = EditorGUILayout.Foldout(
                _runtimeEffectsExpanded,
                "Runtime Effects (Debug)",
                true);
            if (_runtimeEffectsExpanded)
                DrawRuntimeEffects(editor, properties, paletteMode: true);

            _systemStateExpanded = EditorGUILayout.Foldout(
                _systemStateExpanded,
                "Generated / System State", true);
            if (_systemStateExpanded)
                DrawPaletteSystemState(editor, properties);
        }

        private static void DrawFlatCompatibilityInspector(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Flat Compatibility", EditorStyles.boldLabel);
            DrawProperty(editor, properties, "_Color", "Color");
            DrawProperty(editor, properties, "_Cull", "Render Face");

            _runtimeEffectsExpanded = EditorGUILayout.Foldout(
                _runtimeEffectsExpanded,
                "Runtime Effects (Debug)",
                true);
            if (_runtimeEffectsExpanded)
                DrawRuntimeEffects(editor, properties, paletteMode: false);
        }

        private static void DrawFaceStatus(MaterialEditor editor, MaterialProperty[] properties)
        {
            MaterialProperty enabled = TryFindProperty("_WgFaceDetailEnabled", properties);
            bool isEnabled = enabled != null && !enabled.hasMixedValue && enabled.floatValue > 0.5f;
            EditorGUILayout.LabelField("Face Detail", isEnabled ? "Enabled by character configuration" : "Disabled / unavailable");

            if (!isEnabled)
                return;

            using (new EditorGUI.DisabledScope(true))
            {
                DrawTextureSingleLine(editor, properties, "_WgFaceDetailTex", "Character FaceDetail RGBA");
                DrawProperty(editor, properties, "_WgFaceDetailIntensity", "Face Intensity");
            }
        }

        private static void DrawGlobalPaletteLook(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "Color comes from the shared RGB565 Atlas. All 65,536 addresses are unique; the 16 Surface Profiles are generated from Catalog v3 and are not material-instance parameters.",
                MessageType.None);

            EditorGUILayout.HelpBox("Direct and indirect lighting now use Scene Lighting. Adjust the shared Look asset for lighting style; this material controls surface response and effects.", MessageType.Info);
            EditorGUILayout.LabelField("Surface Response", EditorStyles.miniBoldLabel);
            DrawVectorComponent(
                editor,
                TryFindProperty("_WgPaletteEdgeParams", properties),
                0,
                "Stylized Edge Light",
                0f,
                1f);
            MaterialProperty proceduralDetail = TryFindProperty("_WgPaletteDetailParams", properties);
            DrawVectorComponent(
                editor,
                proceduralDetail,
                0,
                "Procedural Detail Strength",
                0f,
                1f);
            DrawDetailQuality(editor, proceduralDetail);

            _materialResponseExpanded = EditorGUILayout.Foldout(
                _materialResponseExpanded,
                "Surface Profile Response",
                true);
            if (_materialResponseExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("16 immutable profiles", "Generated HLSL table");
                    EditorGUILayout.LabelField("Profile source", "PaletteData.A");
                    DrawProperty(editor, properties, "_WgPaletteRimPower", "Palette Rim Power");
                }
            }

            _technicalExpanded = EditorGUILayout.Foldout(
                _technicalExpanded,
                "Technical Parameters",
                true);
            if (_technicalExpanded)
                DrawTechnicalPaletteParameters(editor, properties);

            EditorGUILayout.Space(3f);
            if (GUILayout.Button("Reset Material Response To Project Defaults"))
                ResetPaletteLook(editor);
        }

        private static void DrawTechnicalPaletteParameters(MaterialEditor editor, MaterialProperty[] properties)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                DrawProperty(editor, properties, "_WgPaletteEdgeWarmTint", "Warm Edge Tint");
                DrawProperty(editor, properties, "_WgPaletteEdgeCoolTint", "Cool Edge Tint");

                MaterialProperty edge = TryFindProperty("_WgPaletteEdgeParams", properties);
                DrawVectorComponent(editor, edge, 1, "Edge Power", 0.25f, 8f);
                DrawVectorComponent(editor, edge, 2, "Edge Side Softness", 0.01f, 1f);
                DrawVectorComponent(editor, edge, 3, "Edge Base Color Mix", 0f, 1f);

                MaterialProperty detail = TryFindProperty("_WgPaletteDetailParams", properties);
                DrawVectorComponent(editor, detail, 1, "Detail Full Distance", 0f, 20f);
                DrawVectorComponent(editor, detail, 2, "Detail Fade End", 0.01f, 40f);
            }
        }

        private static void DrawDetailQuality(MaterialEditor editor, MaterialProperty property)
        {
            if (property == null)
                return;

            Vector4 value = property.vectorValue;
            int current = value.w > 0.5f ? 1 : 0;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int updated = EditorGUILayout.Popup("Detail Quality", current, DetailQualityLabels);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo("Detail Quality");
                value.w = updated;
                property.vectorValue = value;
            }
            EditorGUI.showMixedValue = false;
        }

        private static void DrawRuntimeEffects(
            MaterialEditor editor,
            MaterialProperty[] properties,
            bool paletteMode)
        {
            EditorGUILayout.HelpBox(
                "These properties are normally driven by MaterialBindingHub or runtime presentation systems. Manual edits are for diagnostics only.",
                MessageType.None);

            DrawProperty(editor, properties, "_WgHitFlashColor", "Hit Flash Color");
            DrawProperty(editor, properties, "_WgHitFlashAmount", "Hit Flash Amount");
            DrawProperty(editor, properties, "_WgStatusTintColor", "Status Tint Color");
            DrawProperty(editor, properties, "_WgStatusTintAmount", "Status Tint Amount");
            DrawProperty(editor, properties, "_WgDissolveProgress", "Dissolve Progress");
            DrawProperty(editor, properties, "_WgDissolveNoiseMode", "Dissolve Noise Source");
            DrawTextureSingleLine(editor, properties, "_WgDissolveNoiseTex", "Dissolve Noise");
            DrawProperty(editor, properties, "_WgDissolveEdgeColor", "Dissolve Edge Color");
            DrawProperty(editor, properties, "_WgDissolveEdgeWidth", "Dissolve Edge Width");
            DrawProperty(editor, properties, "_WgOutlineColor", paletteMode ? "Readable Rim Color" : "Outline / Rim Color");
            DrawProperty(editor, properties, "_WgOutlineAmount", paletteMode ? "Readable Rim Amount" : "Outline / Rim Amount");
        }

        private static void DrawPaletteSystemState(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "Import/runtime-owned values. Change the character palette binding configuration instead of editing these fields.",
                MessageType.None);
            using (new EditorGUI.DisabledScope(true))
            {
                DrawProperty(editor, properties, "_WgPaletteEnabled", "Palette Enabled");
                DrawTextureSingleLine(editor, properties, "_WgColorPaletteTex", "Shared RGB Color Atlas");
                DrawProperty(editor, properties, "_WgColorPaletteParams", "Color Atlas Dimensions / Inverse");
                DrawProperty(editor, properties, "_WgFaceDetailEnabled", "Face Detail Enabled");
                DrawProperty(editor, properties, "_Cull", "Render Face");
            }
        }

        private static void DrawVectorComponent(
            MaterialEditor editor,
            MaterialProperty property,
            int componentIndex,
            string label,
            float minimum,
            float maximum)
        {
            if (property == null)
                return;

            Vector4 value = property.vectorValue;
            float current = GetComponent(value, componentIndex);
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            float updated = EditorGUILayout.Slider(label, current, minimum, maximum);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo(label);
                SetComponent(ref value, componentIndex, updated);
                property.vectorValue = value;
            }
            EditorGUI.showMixedValue = false;
        }

        private static void DrawProperty(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty property = TryFindProperty(propertyName, properties);
            if (property != null)
                editor.ShaderProperty(property, new GUIContent(label));
        }

        private static void DrawTextureSingleLine(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty property = TryFindProperty(propertyName, properties);
            if (property != null)
                editor.TexturePropertySingleLine(new GUIContent(label), property);
        }

        private static MaterialProperty TryFindProperty(
            string propertyName,
            MaterialProperty[] properties)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (property != null && string.Equals(property.name, propertyName, StringComparison.Ordinal))
                    return property;
            }

            return null;
        }

        private static bool TryResolvePaletteMode(
            UnityEngine.Object[] targets,
            out bool paletteMode,
            out bool mixedMode)
        {
            paletteMode = false;
            mixedMode = false;
            bool found = false;

            for (int i = 0; i < targets.Length; i++)
            {
                if (!(targets[i] is Material material))
                    continue;

                bool current = material.IsKeywordEnabled(PaletteKeyword)
                    || IsEnabled(material, "_WgPaletteEnabled");
                if (!found)
                {
                    paletteMode = current;
                    found = true;
                }
                else if (paletteMode != current)
                {
                    mixedMode = true;
                }
            }

            return found;
        }

        private static bool IsEnabled(Material material, string propertyName)
        {
            return material != null
                && material.HasProperty(propertyName)
                && material.GetFloat(propertyName) > 0.5f;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static float GetComponent(Vector4 value, int index)
        {
            switch (index)
            {
                case 0:
                    return value.x;
                case 1:
                    return value.y;
                case 2:
                    return value.z;
                default:
                    return value.w;
            }
        }

        private static void SetComponent(ref Vector4 value, int index, float component)
        {
            switch (index)
            {
                case 0:
                    value.x = component;
                    break;
                case 1:
                    value.y = component;
                    break;
                case 2:
                    value.z = component;
                    break;
                default:
                    value.w = component;
                    break;
            }
        }

        private static void ResetPaletteLook(MaterialEditor editor)
        {
            Undo.RecordObjects(editor.targets, "Reset WGame Palette Look");
            for (int i = 0; i < editor.targets.Length; i++)
            {
                if (!(editor.targets[i] is Material material))
                    continue;

                SetColor(material, "_WgPaletteEdgeWarmTint", new Color(1.00f, 0.80f, 0.52f, 1f));
                SetColor(material, "_WgPaletteEdgeCoolTint", new Color(0.38f, 0.70f, 0.86f, 1f));
                SetVector(material, "_WgPaletteEdgeParams", new Vector4(0.085f, 4.25f, 0.24f, 0.28f));
                SetVector(material, "_WgPaletteDetailParams", new Vector4(0.65f, 3f, 12f, 1f));
                SetFloat(material, "_WgPaletteRimPower", 2.6f);
                EditorUtility.SetDirty(material);
            }

            editor.PropertiesChanged();
        }

        private static void SetVector(Material material, string propertyName, Vector4 value)
        {
            if (material.HasProperty(propertyName))
                material.SetVector(propertyName, value);
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, value);
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }
    }
}
