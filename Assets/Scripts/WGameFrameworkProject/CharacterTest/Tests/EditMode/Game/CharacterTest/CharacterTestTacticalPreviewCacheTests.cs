using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalPreviewCacheTests
    {
        [Test]
        public void UnchangedAuthoredPreviewIsReusedWhileRuntimeFrameLocationsAdvance()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture();
            var go = new GameObject("Preview cache test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_draftWorkspace", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, workspace);
                var card = workspace.Cards[id]; var document = workspace.ResolveComposition(card);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
                var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, card, document, fixture.Inputs, compiled);
                var runtime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution {
                    Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved, Frame = 10,
                    CommittedProgram = compiled.Program.WithTacticalSource(frozen),
                    LogicAgent = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot { ExecutionState = new CharacterTestBehaviorExecutionState() } };
                var first = controller.BuildTacticalPreview(card, frozen, runtime);
                runtime.Frame = 11;
                var second = controller.BuildTacticalPreview(card, frozen, runtime);
                Assert.AreSame(first, second); Assert.AreEqual(1, controller.TacticalPreviewBuildCount);
                Assert.AreEqual(11, controller.CompositionEditingSession.RuntimeSourceFrame);
                controller.RefreshTacticalPreview();
                Assert.AreNotSame(first, controller.BuildTacticalPreview(card, frozen, runtime));
                Assert.AreEqual(2, controller.TacticalPreviewBuildCount);
                var replacement = CharacterTestTacticalFrozenSource.Capture(workspace, card, document, fixture.Inputs, compiled);
                Assert.AreNotSame(second, controller.BuildTacticalPreview(card, replacement, runtime));
                Assert.AreEqual(3, controller.TacticalPreviewBuildCount);
                Assert.AreSame(compiled.Program.Execution, first.WholeCard.Program.Execution);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WorkspaceRevisionAndSelectionInvalidateWithoutMutatingAFrozenSource()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture();
            var go = new GameObject("Preview cache invalidation test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_draftWorkspace", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, workspace);
                var card = workspace.Cards[id]; var source = workspace.ResolveComposition(card);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, fixture.Inputs, out var compiled, out issue), issue);
                var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, card, source, fixture.Inputs, compiled);
                var first = controller.BuildTacticalPreview(card, frozen);
                controller.CompositionEditingSession.Select(source.Root.NodeId);
                Assert.AreNotSame(first, controller.BuildTacticalPreview(card, frozen));
                long before = controller.TacticalPreviewBuildCount;
                Assert.IsTrue(workspace.TryRenameCompositionCard(new CharacterTestTacticalEditToken(workspace, card), "Renamed", out issue), issue);
                var changed = controller.BuildTacticalPreview(card, frozen);
                Assert.AreEqual(before + 1, controller.TacticalPreviewBuildCount);
                Assert.AreSame(compiled.Program.Execution, changed.WholeCard.Program.Execution);
                Assert.AreEqual(source.Name, frozen.CloneDocument().Name);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
