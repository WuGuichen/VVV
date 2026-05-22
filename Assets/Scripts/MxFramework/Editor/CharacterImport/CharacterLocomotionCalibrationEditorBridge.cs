using MxFramework.CharacterRuntimeSpawn.Unity;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    [InitializeOnLoad]
    internal static class CharacterLocomotionCalibrationEditorBridge
    {
        static CharacterLocomotionCalibrationEditorBridge()
        {
            CharacterLocomotionCalibrationRunner.LocateProjectObjectRequested -= LocateProjectObject;
            CharacterLocomotionCalibrationRunner.LocateProjectObjectRequested += LocateProjectObject;
        }

        private static void LocateProjectObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("MxFramework Locomotion Calibration: selected clip is not a persistent Project asset. Stop Play Mode and select a Project animation clip before editing import/bake settings.");
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                return;
            }

            UnityEngine.Object selection = ResolveInspectorSelection(target, path);
            Selection.activeObject = selection;
            EditorGUIUtility.PingObject(selection);

            string extension = Path.GetExtension(path);
            string saveTarget = string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".meta";
            Debug.Log("MxFramework Locomotion Calibration: selected animation source '" + selection.name + "' at " + path + ". Inspector changes should persist to " + saveTarget + ". Edit outside Play Mode, then use Apply when Unity shows importer changes or Assets > Save Project for standalone .anim clips.");
        }

        private static UnityEngine.Object ResolveInspectorSelection(UnityEngine.Object target, string path)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase))
                return target;

            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            return mainAsset != null ? mainAsset : target;
        }
    }
}
