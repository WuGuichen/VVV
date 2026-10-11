using System;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorModuleDocumentIdentityTests
    {
        private static MxLevelAuthoringDocument Envelope(string level = "a", string scene = "Assets/A.unity")
            => new() { LevelId = level, ScenePath = scene };

        [Test] public void CleanDraftCanLoadOtherSceneDocument()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "source-a");
            Assert.DoesNotThrow(() => id.RequireCanLoad("b.json", true, false, "Genesis"));
            id.Bind("b.json", Envelope("b", "Assets/B.unity"), "source-b");
            Assert.DoesNotThrow(() => id.RequireSave("b.json", "Assets/B.unity", Envelope("b", "Assets/B.unity"), "source-b", "Genesis"));
        }

        [Test] public void SameSceneDifferentLevelIsDifferentDocument()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "a");
            Assert.IsFalse(id.IsFor("b.json"));
            Assert.Throws<InvalidOperationException>(() => id.RequireSave("b.json", "Assets/A.unity", Envelope("b"), "b", "Guidance"));
        }

        [TestCase("Genesis")] [TestCase("Guidance")]
        public void DirtyDraftBlocksDocumentSwitchAndRetainsSource(string module)
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "a");
            string before = id.Capture().ToString();
            var error = Assert.Throws<InvalidOperationException>(() => id.RequireCanLoad("b.json", true, true, module));
            StringAssert.Contains("a.json", error.Message); StringAssert.Contains("b.json", error.Message);
            Assert.AreEqual(before, id.Capture().ToString());
            Assert.DoesNotThrow(() => id.RequireSave("a.json", "Assets/A.unity", Envelope(), "a", module));
        }

        [Test] public void SameDocumentDirtyDraftDoesNotReload()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "a");
            Assert.DoesNotThrow(() => id.RequireCanLoad("a.json", true, true, "Genesis"));
        }

        [Test] public void EnvelopeSceneCannotBeCombinedWithAnotherOwnerScene()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "a");
            Assert.Throws<InvalidOperationException>(() => id.RequireSave("a.json", "Assets/B.unity", Envelope(), "a", "Genesis"));
            Assert.Throws<InvalidOperationException>(() => id.RequireSave("a.json", "Assets/A.unity", Envelope("b"), "a", "Genesis"));
        }

        [TestCase("changed")] [TestCase(null)]
        public void ExternalSourceChangeOrDeletionRejectsWithoutRebinding(string source)
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "old");
            string before = id.Capture().ToString();
            var error = Assert.Throws<InvalidOperationException>(() => id.RequireSave("a.json", "Assets/A.unity", Envelope(), source, "Guidance"));
            StringAssert.Contains("a.json", error.Message); Assert.AreEqual(before, id.Capture().ToString());
        }

        [Test] public void RecoveryPreservesOriginalSourceGenerationAfterExternalChange()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "old");
            var restored = new CharacterTestLevelEditorModuleDocumentIdentity();
            restored.Restore(id.Capture(), "a.json", Envelope(), "new");
            Assert.Throws<InvalidOperationException>(() => restored.RequireSave("a.json", "Assets/A.unity", Envelope(), "new", "Genesis"));
            Assert.DoesNotThrow(() => restored.RequireSave("a.json", "Assets/A.unity", Envelope(), "old", "Genesis"));
        }

        [Test] public void RecoveryForAnotherDocumentRejectsBeforeBinding()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "a");
            var restored = new CharacterTestLevelEditorModuleDocumentIdentity();
            Assert.Throws<InvalidOperationException>(() => restored.Restore(id.Capture(), "b.json", Envelope("b"), "b"));
            Assert.IsNull(restored.LoadedPath);
        }

        [Test] public void AcknowledgementAcceptsCommittedSourceOnly()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Bind("a.json", Envelope(), "old");
            id.AcknowledgeSource("committed");
            Assert.DoesNotThrow(() => id.RequireSave("a.json", "Assets/A.unity", Envelope(), "committed", "Guidance"));
            Assert.Throws<InvalidOperationException>(() => id.RequireSave("a.json", "Assets/A.unity", Envelope(), "old", "Guidance"));
        }

        [Test] public void PreviousRecoveryRecordBindsItsProvidedEnvelope()
        {
            var id = new CharacterTestLevelEditorModuleDocumentIdentity();
            id.Restore(null, "a.json", Envelope(), "a");
            Assert.DoesNotThrow(() => id.RequireSave("a.json", "Assets/A.unity", Envelope(), "a", "Genesis"));
        }
    }
}
