using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Rejects missing serialized dependencies used by eager runtime VFX creation.</summary>
    internal sealed class CharacterTestStartupShaderBuildValidation : IProcessSceneWithReport
    {
        internal const string PropShaderName = "WGame/PropStylizedLit";
        public int callbackOrder => -100;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var owner in root.GetComponentsInChildren<GameManager>(true))
                    ValidateProfile(owner.ConfiguredProfile, scene.path);
        }

        internal static void ValidateProfile(CharacterTestSceneProfile profile, string scenePath)
        {
            if (profile == null)
                throw new BuildFailedException("CharacterTest scene profile is missing: " + scenePath);
            // ConfigureCharacterTestVfxResources explicitly disables VFX when Fireball has no binding.
            if (profile.FireballVfxPrefab == null) return;
            if (profile.RuntimeVfxPropShader == null ||
                !string.Equals(profile.RuntimeVfxPropShader.name, PropShaderName, StringComparison.Ordinal))
                throw new BuildFailedException("CharacterTest runtime VFX requires an explicit " + PropShaderName +
                    " reference in the scene profile before resource bootstrap: " + scenePath);
            // isSupported is a target-Player check; headless build hosts need not have a graphics device.
        }
    }
}
