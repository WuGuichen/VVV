using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor
{
    /// <summary>Explicit migration of the former mixed character shader. Never runs on import.</summary>
    internal static class WGameCharacterShaderMigration
    {
        internal const string BakedShader = "WGame/CharacterStylizedLit";
        internal const string LegacyShader = "WGame/CharacterPaletteLegacy";

        [MenuItem("MxFramework/Rendering/Migrate Character Shaders")]
        private static void RunMenu() => Debug.Log(MigrateProject());

        internal static string MigrateProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Character shader migration requires Edit Mode.");
            Shader legacy = Shader.Find(LegacyShader);
            if (legacy == null) throw new InvalidOperationException("Legacy character shader is missing.");
            var report = new List<string>();
            var pending = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != BakedShader) continue;
                string mode = Classify(material, out string issue);
                report.Add(path + ": " + mode + (issue == null ? "" : " — " + issue));
                if (mode == "Conflict") continue;
                pending.Add(material);
            }
            foreach (Material material in pending)
            {
                if (!Migrate(material, legacy)) continue;
                AssetDatabase.SaveAssetIfDirty(material);
            }
            string result = string.Join("\n", report);
            Directory.CreateDirectory("Temp/CharacterShaderMigration");
            File.WriteAllText("Temp/CharacterShaderMigration/latest.txt", result);
            return result;
        }

        internal static string Classify(Material material, out string issue)
        {
            issue = null;
            if (material == null || material.shader == null) return "Unsupported";
            if (material.shader.name == LegacyShader) return "Legacy";
            if (material.shader.name != BakedShader) return "Unsupported";
            var serialized = new SerializedObject(material);
            bool baked = Flag(serialized, "_WgBakedSurfaceEnabled");
            bool palette = Flag(serialized, "_WgPaletteEnabled");
            bool face = Flag(serialized, "_WgFaceDetailEnabled");
            bool bakedKeyword = Keyword(serialized, "_WG_BAKED_SURFACE_ON");
            bool paletteKeyword = Keyword(serialized, "_WG_PALETTE_ON");
            bool faceKeyword = Keyword(serialized, "_WG_FACE_DETAIL_ON");
            bool baseMap = Texture(serialized, "_WgBakedBaseColor");
            bool surfaceMap = Texture(serialized, "_WgSurfaceDataTex");
            if ((baked || bakedKeyword) && (palette || paletteKeyword || face || faceKeyword)
                || palette != paletteKeyword || face != faceKeyword || baked != bakedKeyword
                || baseMap != surfaceMap || ((baked || bakedKeyword) && !baseMap)
                || (baseMap && (palette || face)) || (face && !palette)
                || (!baseMap && !palette && Entry(serialized, "m_Colors", "_Color") == null))
            {
                issue = "Conflicting mode flags or incomplete baked textures; material was not changed.";
                return "Conflict";
            }
            return baseMap ? "Baked" : palette ? "Palette" : "Flat";
        }

        internal static bool Migrate(Material material, Shader legacy)
        {
            if (material == null || material.shader == null || material.shader.name != BakedShader) return false;
            string mode = Classify(material, out _);
            if (mode == "Conflict") return false;
            var serialized = new SerializedObject(material);
            bool oldBaked = Keyword(serialized, "_WG_BAKED_SURFACE_ON") || Entry(serialized, "m_Floats", "_WgBakedSurfaceEnabled") != null;
            if (mode == "Baked" && !oldBaked) return false;
            Undo.RecordObject(material, "Migrate Character Shader");
            if (mode != "Baked") material.shader = legacy;
            material.DisableKeyword("_WG_BAKED_SURFACE_ON");
            serialized = new SerializedObject(material);
            Remove(serialized, "m_Floats", "_WgBakedSurfaceEnabled");
            if (mode == "Baked")
            {
                material.DisableKeyword("_WG_PALETTE_ON");
                material.DisableKeyword("_WG_FACE_DETAIL_ON");
                foreach (string name in new[] { "_WgPaletteEnabled", "_WgFaceDetailEnabled", "_WgFaceDetailIntensity" }) Remove(serialized, "m_Floats", name);
                foreach (string name in new[] { "_WgColorPaletteTex", "_WgFaceDetailTex" }) Remove(serialized, "m_TexEnvs", name);
                Remove(serialized, "m_Colors", "_WgColorPaletteParams");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(material);
            return true;
        }

        private static SerializedProperty Entry(SerializedObject obj, string array, string name)
        {
            SerializedProperty values = obj.FindProperty("m_SavedProperties." + array);
            for (int i = 0; values != null && i < values.arraySize; i++)
            {
                SerializedProperty entry = values.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name) return entry.FindPropertyRelative("second");
            }
            return null;
        }
        private static bool Flag(SerializedObject obj, string name) => Entry(obj, "m_Floats", name)?.floatValue > 0.5f;
        private static bool Texture(SerializedObject obj, string name) => Entry(obj, "m_TexEnvs", name)?.FindPropertyRelative("m_Texture").objectReferenceValue != null;
        private static bool Keyword(SerializedObject obj, string name)
        {
            foreach (string field in new[] { "m_ValidKeywords", "m_InvalidKeywords" })
            {
                SerializedProperty values = obj.FindProperty(field);
                for (int i = 0; values != null && i < values.arraySize; i++)
                    if (values.GetArrayElementAtIndex(i).stringValue == name) return true;
            }
            return false;
        }
        private static void Remove(SerializedObject obj, string array, string name)
        {
            SerializedProperty values = obj.FindProperty("m_SavedProperties." + array);
            for (int i = values.arraySize - 1; i >= 0; i--)
                if (values.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name) values.DeleteArrayElementAtIndex(i);
        }
    }
}
