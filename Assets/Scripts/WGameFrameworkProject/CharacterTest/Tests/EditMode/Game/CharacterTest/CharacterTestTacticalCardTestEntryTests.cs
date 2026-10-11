using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardTestEntryTests
    {
        [Test]
        public void ActionLocationsUseEventLabelsAndKeepRepeatedReferencesDistinct()
        {
            var owner = new GameObject("Action location labels"); owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var workspace = controller.DraftWorkspace;
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack("接近后出手");
                var document = new CharacterTestTacticalDslSource { CardId = "action-locations", Name = "动作位置",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "internal-reference-a", Kind = "Reference", DefinitionId = "shared" },
                        new CharacterTestTacticalDslNode { NodeId = "internal-reference-b", Kind = "Reference", DefinitionId = "shared" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                int revision = workspace.CurrentRevision;
                var locations = controller.TacticalActionLocations(id);
                Assert.AreEqual(2, locations.Length);
                Assert.AreNotEqual(locations[0].Path, locations[1].Path);
                Assert.AreNotEqual(locations[0].Label, locations[1].Label);
                foreach (var location in locations)
                {
                    StringAssert.Contains("执行动作", location.Label);
                    StringAssert.DoesNotContain("primary", location.Label);
                    StringAssert.DoesNotContain("internal-reference", location.Label);
                }
                Assert.AreEqual(revision, workspace.CurrentRevision, "Displaying action bindings must not rewrite shared source.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void CompletionInvalidatesRetainedEditorButtons()
        {
            var owner = new GameObject("Manual card completion presentation");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var type = typeof(CharacterTestRuntimeAiPlannerDevSceneController);
                type.GetField("<TacticalCardTestMode>k__BackingField", flags).SetValue(controller, true);
                type.GetField("<TacticalCardTestInFlight>k__BackingField", flags).SetValue(controller, true);
                type.GetField("_phase", flags).SetValue(controller, CharacterTestRuntimeAiPlannerDevScenePhase.Completed);
                var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel {
                    ShowBehaviorLogic = true, TacticalPresentationRevision = controller.TacticalPresentationRevision };
                long before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost.ComputeBehaviorCardPresentationRevision(view);
                type.GetMethod("ObserveTacticalCardTestFrame", flags).Invoke(controller, null);
                view.TacticalPresentationRevision = controller.TacticalPresentationRevision;
                Assert.IsFalse(controller.TacticalCardTestInFlight);
                Assert.IsTrue(controller.CanChooseTacticalTestCard);
                Assert.AreNotEqual(before, CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost.ComputeBehaviorCardPresentationRevision(view),
                    "The retained editor must rebind the disabled card picker and playback label when execution finishes.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void EntrySelectsLastCardWithoutStartingOrChangingItsSource()
        {
            var owner = new GameObject("Manual tactical card test entry");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var workspace = controller.DraftWorkspace;
                var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance);
                source.Slots.Clear();
                var document = new CharacterTestTacticalDslSource {
                    CardId = "manual-entry", Name = "手动出牌入口测试",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "use", Kind = "Reference", DefinitionId = "shared" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> {
                        new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } }
                };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                controller.TacticalLibrarySelectedKey = "draft:" + id;
                int revision = workspace.CurrentRevision;
                Assert.IsTrue(controller.BeginTacticalCardTest(out string key, out issue), issue);
                Assert.AreEqual("draft:" + id, key);
                Assert.IsTrue(controller.BeginStandaloneTacticalEditing(key, out issue), issue);
                controller.SelectTacticalTestCard();
                Assert.IsTrue(controller.TacticalCardTestMode);
                Assert.IsFalse(controller.TacticalCardTestInFlight);
                Assert.AreEqual(id, controller.PendingTacticalTestCardId);
                Assert.AreEqual(CharacterTestRuntimeAiPlannerDevScenePhase.Setup, controller.Phase);
                Assert.IsTrue(controller.CanChooseTacticalTestCard);
                Assert.AreEqual("出牌并播放", controller.TacticalCardTestPlaybackLabel);
                Assert.AreEqual(revision, workspace.CurrentRevision);
                controller.EndStandaloneTacticalEditing();
                Assert.IsFalse(controller.TacticalCardTestMode);
                Assert.IsNull(controller.PendingTacticalTestCardId);
                Assert.AreEqual(revision, workspace.CurrentRevision);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
