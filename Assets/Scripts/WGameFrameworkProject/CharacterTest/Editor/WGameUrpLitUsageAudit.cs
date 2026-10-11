using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class WGameUrpLitUsageAudit
    {
        private static readonly string[] BannedShaderNames =
        {
            "Universal Render Pipeline/" + "Lit",
            "Universal Render Pipeline/" + "Simple Lit",
        };

        private static readonly string[] SerializedRoots =
        {
            "Assets",
        };

        private static readonly string[] SourceRoots =
        {
            "Assets/Scripts/WGameFrameworkProject",
            "Packages/com.vincent.wgameframework",
        };

        [MenuItem("WGame/Rendering/Audit Stock URP Lit Usage")]
        internal static void AuditMenu()
        {
            IReadOnlyList<string> violations = Scan();
            if (violations.Count == 0)
            {
                Debug.Log("WGame stock URP Lit audit passed: 0 violations.");
                return;
            }

            throw new InvalidOperationException(
                "WGame stock URP Lit audit failed (" + violations.Count + "):\n" +
                string.Join("\n", violations));
        }

        internal static IReadOnlyList<string> Scan()
        {
            var violations = new List<string>();
            ScanMaterialAssets(violations);
            ScanSerializedAssets(violations);
            ScanSourceFiles(violations);
            violations.Sort(StringComparer.Ordinal);
            return violations;
        }

        private static void ScanMaterialAssets(List<string> violations)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", SerializedRoots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsExcluded(path))
                    continue;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && IsBanned(material.shader))
                    violations.Add("material:" + path + " -> " + material.shader.name);
            }
        }

        private static void ScanSerializedAssets(List<string> violations)
        {
            var bannedGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < BannedShaderNames.Length; i++)
            {
                Shader shader = Shader.Find(BannedShaderNames[i]);
                string shaderPath = shader == null ? string.Empty : AssetDatabase.GetAssetPath(shader);
                string guid = string.IsNullOrEmpty(shaderPath) ? string.Empty : AssetDatabase.AssetPathToGUID(shaderPath);
                if (!string.IsNullOrEmpty(guid))
                    bannedGuids[guid] = BannedShaderNames[i];
            }

            string[] extensions = { "*.unity", "*.prefab", "*.asset" };
            for (int rootIndex = 0; rootIndex < SerializedRoots.Length; rootIndex++)
            {
                string root = SerializedRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                for (int extensionIndex = 0; extensionIndex < extensions.Length; extensionIndex++)
                {
                    string[] paths = Directory.GetFiles(root, extensions[extensionIndex], SearchOption.AllDirectories);
                    for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                    {
                        string path = paths[pathIndex].Replace('\\', '/');
                        if (IsExcluded(path))
                            continue;

                        string text = File.ReadAllText(path);
                        foreach (KeyValuePair<string, string> pair in bannedGuids)
                        {
                            if (text.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                violations.Add("serialized:" + path + " -> " + pair.Value);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static void ScanSourceFiles(List<string> violations)
        {
            for (int rootIndex = 0; rootIndex < SourceRoots.Length; rootIndex++)
            {
                string root = SourceRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] paths = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                {
                    string path = paths[pathIndex].Replace('\\', '/');
                    if (IsExcluded(path))
                        continue;

                    string text = File.ReadAllText(path);
                    for (int shaderIndex = 0; shaderIndex < BannedShaderNames.Length; shaderIndex++)
                    {
                        string quotedName = "\"" + BannedShaderNames[shaderIndex] + "\"";
                        if (text.IndexOf(quotedName, StringComparison.Ordinal) >= 0)
                            violations.Add("source:" + path + " -> " + BannedShaderNames[shaderIndex]);
                    }
                }
            }
        }

        private static bool IsBanned(Shader shader)
        {
            if (shader == null)
                return false;

            for (int i = 0; i < BannedShaderNames.Length; i++)
            {
                if (string.Equals(shader.name, BannedShaderNames[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsExcluded(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith("Assets/_Recovery/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/_TempImportedResources/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/Piloto Studio/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/CombatGirlsCharacterPack/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/FImpossible Creations/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/MagicaCloth2/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/InitTestScene", StringComparison.OrdinalIgnoreCase) ||
                   normalized.IndexOf("/.agent/", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
