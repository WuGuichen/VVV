using System;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Category("RequiresUnityEditorNative")]
    public sealed class CharacterTestLevelPreflightProfileNativeTests
    {
        private static readonly Type Scope = typeof(CharacterTestLevelAcceptance).Assembly.GetType(
            "MxFramework.Game.CharacterTest.Editor.CharacterTestLevelAuthoringOperations", true)
            .GetNestedType("PreflightProfileScope", BindingFlags.NonPublic);
        private static IDisposable Acquire(CharacterTestSceneProfile configured)
            => (IDisposable)Activator.CreateInstance(Scope, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { configured }, null);
        private static CharacterTestSceneProfile Profile(IDisposable scope)
            => (CharacterTestSceneProfile)Scope.GetProperty("Profile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope);

        [Test] public void ConfiguredProfileIsBorrowedAndSurvivesDispose()
        {
            var configured = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            try
            {
                var scope = Acquire(configured);
                Assert.That(Profile(scope), Is.SameAs(configured));
                scope.Dispose(); scope.Dispose();
                Assert.That(configured != null, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(configured); }
        }
        [Test] public void RuntimeDefaultProfileIsReleasedAfterSuccess()
        {
            var scope = Acquire(null); var profile = Profile(scope);
            try { Assert.That(profile.name, Is.EqualTo("CharacterTestSceneProfileRuntimeDefault")); }
            finally { scope.Dispose(); }
            Assert.That(profile == null, Is.True); scope.Dispose();
        }
        [Test] public void RuntimeDefaultProfileIsReleasedWhenPreparationThrows()
        {
            CharacterTestSceneProfile profile = null;
            Assert.Throws<InvalidOperationException>(() => {
                using var scope = Acquire(null); profile = Profile(scope);
                throw new InvalidOperationException("Fixture preparation failure");
            });
            Assert.That(profile == null, Is.True);
        }
    }
}
