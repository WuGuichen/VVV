using MxFramework.CharacterRuntimeSpawn.Unity;
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

        private static void LocateProjectObject(Object target)
        {
            if (target == null)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
