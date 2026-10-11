using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class CharacterTestStartupShaderBuildValidationTests
    {
        [Test]
        public void PublishedProfile_DeclaresPropShaderAsNativeDependency()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath);
            Assert.IsNotNull(profile);
            Assert.IsNotNull(profile.FireballVfxPrefab);
            CharacterTestStartupShaderBuildValidation.ValidateProfile(profile, "TacticalCardDemoScene");
            Assert.AreEqual(CharacterTestStartupShaderBuildValidation.PropShaderName, profile.RuntimeVfxPropShader.name);
            CollectionAssert.Contains(AssetDatabase.GetDependencies(CharacterTestSceneProfile.DefaultAssetPath, true),
                AssetDatabase.GetAssetPath(profile.RuntimeVfxPropShader));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrWrongStartupShader_BlocksBuild(bool wrongShader)
        {
            var profile = CloneProfile();
            try
            {
                SetReference(profile, "_runtimeVfxPropShader", wrongShader ? Shader.Find("WGame/MagicVfxUnlit") : null);
                Assert.Throws<BuildFailedException>(() =>
                    CharacterTestStartupShaderBuildValidation.ValidateProfile(profile, "TacticalCardDemoScene"));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ExplicitlyDisabledVfx_DoesNotRequireStartupShader()
        {
            var profile = CloneProfile();
            try
            {
                SetReference(profile, "_fireballVfxPrefab", null);
                SetReference(profile, "_runtimeVfxPropShader", null);
                Assert.DoesNotThrow(() => CharacterTestStartupShaderBuildValidation.ValidateProfile(profile, "NoVfxScene"));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static CharacterTestSceneProfile CloneProfile() => Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath));

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
