using System.Linq;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalActivationOverviewTests
    {
        [Test]
        public void CardConditionsDoNotRetargetOrModifyTheOpenSharedDefinition()
        {
            var host = new GameObject("Tactical activation isolation fixture");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision,
                    CharacterTestTacticalDslTrialTemplates.Create(), out string cardId, out string diagnostic), diagnostic);
                var session = new CharacterTestTacticalCompositionEditingSession(controller);
                session.Bind(workspace, workspace.Cards[cardId]);
                string selected = session.SelectedNodeId;
                string before = JsonConvert.SerializeObject(workspace.CloneTacticalDefinitions());
                var editor = session.BuildActivationOverview().Editor;
                var condition = CharacterTestTacticalDslTrialTemplates.Create().Root.Children
                    .Select(n => n.EndCondition ?? n.Condition).FirstOrDefault(c => c != null);
                Assert.IsNotNull(condition, "Fixture must contain a real condition to copy.");
                Assert.IsTrue(session.EditActivation(editor.Token, source => source.ActivationConditionRoot = condition, out diagnostic), diagnostic);
                Assert.AreEqual(selected, session.SelectedNodeId);
                Assert.AreEqual(CharacterTestTacticalEditTarget.SharedDefinition, session.Target);
                Assert.AreEqual(before, JsonConvert.SerializeObject(workspace.CloneTacticalDefinitions()));
                Assert.IsNotNull(workspace.Cards[cardId].CloneCompositionSource().ActivationConditionRoot);
                Assert.IsFalse(session.EditActivation(editor.Token, source => source.ActivationConditionRoot = null, out _),
                    "A stale condition lease must not write after the workspace revision changed.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void NavigationInvalidatesTheIndependentCardConditionLease()
        {
            var host = new GameObject("Tactical activation navigation fixture");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision,
                    CharacterTestTacticalDslTrialTemplates.Create(), out string cardId, out string diagnostic), diagnostic);
                var session = new CharacterTestTacticalCompositionEditingSession(controller);
                session.Bind(workspace, workspace.Cards[cardId]);
                var token = session.BuildActivationOverview().Editor.Token;
                int revision = workspace.CurrentRevision;
                session.Select(session.Document.Root.NodeId);
                Assert.IsFalse(session.EditActivation(token, source => source.ActivationConditionRoot = null, out _));
                Assert.AreEqual(revision, workspace.CurrentRevision);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
