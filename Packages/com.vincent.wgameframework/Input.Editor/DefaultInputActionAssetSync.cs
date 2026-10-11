using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input.Editor
{
    public static class DefaultInputActionAssetSync
    {
        public const string DefaultAssetPath =
            "Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions";

        private const string SyncMenuPath = "MxFramework/Input/Sync Default Input Actions";
        private const string ValidateMenuPath = "MxFramework/Input/Validate Default Input Actions";

        [MenuItem(SyncMenuPath)]
        public static void SyncDefaultInputActions()
        {
            IReadOnlyList<DefaultInputBindingConflict> conflicts =
                DefaultInputBindingCatalog.FindConflicts();
            if (conflicts.Count > 0)
            {
                Debug.LogError("Default input bindings contain conflicts:\n" + FormatConflicts(conflicts));
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultAssetPath) ?? "Assets");
            InputActionAsset generated = DefaultInputActionAssetFactory.Create();
            try
            {
                File.WriteAllText(DefaultAssetPath, generated.ToJson());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(generated);
            }

            AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Synced default input actions: " + DefaultAssetPath);
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateDefaultInputActions()
        {
            if (ValidateDefaultAsset(out string message))
                Debug.Log(message);
            else
                Debug.LogError(message);
        }

        public static bool ValidateDefaultAsset(out string message)
        {
            IReadOnlyList<DefaultInputBindingConflict> conflicts =
                DefaultInputBindingCatalog.FindConflicts();
            if (conflicts.Count > 0)
            {
                message = "Default input bindings contain conflicts:\n" + FormatConflicts(conflicts);
                return false;
            }

            InputActionAsset existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultAssetPath);
            if (existing == null)
            {
                message = "Default input action asset not found: " + DefaultAssetPath;
                return false;
            }

            InputActionAsset generated = DefaultInputActionAssetFactory.Create();
            try
            {
                string existingSignature = CreateSignature(existing);
                string generatedSignature = CreateSignature(generated);
                if (!string.Equals(existingSignature, generatedSignature, StringComparison.Ordinal))
                {
                    message = "Default input action asset differs from DefaultInputBindingCatalog.";
                    return false;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(generated);
            }

            message = "Default input action asset matches DefaultInputBindingCatalog.";
            return true;
        }

        private static string CreateSignature(InputActionAsset asset)
        {
            var rows = new List<string>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    rows.Add("A|" + map.name + "|" + action.name + "|" + action.type + "|"
                        + (action.expectedControlType ?? string.Empty));
                }

                foreach (InputBinding binding in map.bindings)
                {
                    rows.Add("B|" + map.name + "|" + binding.action + "|"
                        + (binding.name ?? string.Empty) + "|"
                        + (binding.path ?? string.Empty) + "|"
                        + binding.isComposite + "|"
                        + binding.isPartOfComposite);
                }
            }

            rows.Sort(StringComparer.Ordinal);
            return string.Join("\n", rows);
        }

        private static string FormatConflicts(IReadOnlyList<DefaultInputBindingConflict> conflicts)
        {
            var lines = new string[conflicts.Count];
            for (int i = 0; i < conflicts.Count; i++)
                lines[i] = conflicts[i].ToString();
            return string.Join("\n", lines);
        }
    }
}
