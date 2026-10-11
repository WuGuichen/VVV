using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestTacticalAuthoringExitTests
    {
        [UnityTest]
        public IEnumerator ReturningWithCachedCompositionRemovesFloatingControlsAndRestoresGeometry()
        {
            const string packageName = "MxCharacterTestRuntimeAiLabUi";
            var package = Fgui.UIPackage.GetByName(packageName);
            bool ownsPackage = package == null;
            Texture2D atlas = null; Fgui.GComponent panel = null; GameObject owner = null;
            try
            {
                if (ownsPackage)
                {
                    string path = Path.Combine(Application.dataPath, "Bundles/FGUI", packageName);
                    atlas = new Texture2D(2, 2);
                    Assert.IsTrue(atlas.LoadImage(File.ReadAllBytes(Path.Combine(path, packageName + "_atlas0.png"))));
                    var texture = atlas;
                    package = Fgui.UIPackage.AddPackage(File.ReadAllBytes(Path.Combine(path, packageName + "_fui.bytes")), packageName,
                        (string name, string extension, Type type, out Fgui.DestroyMethod destroy) => {
                            destroy = Fgui.DestroyMethod.None; return type.IsInstanceOfType(texture) ? texture : null;
                        });
                }
                panel = Fgui.UIPackage.CreateObject(packageName, "RuntimeAiPlannerDevPanel").asCom;
                Assert.IsNotNull(panel); panel.SetSize(1920, 1080); Fgui.GRoot.inst.AddChild(panel);
                owner = new GameObject("Tactical exit regression"); owner.SetActive(false);
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var workspace = controller.DraftWorkspace;
                var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget);
                source.Slots.Clear();
                var document = new CharacterTestTacticalDslSource { CardId = "exit-test", Name = "退出测试",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "use", Kind = "Reference", DefinitionId = "shared" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string cardId, out string issue), issue);
                var session = new CharacterTestTacticalCompositionEditingSession(controller); session.Bind(workspace, workspace.Cards[cardId]);
                var preview = CharacterTestTacticalAuthoringPreview.FromSource(source,
                    CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>()), "");
                var model = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel { WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard };
                model.BehaviorCardWorkspace.Timeline.CompositionEditor = session;
                model.BehaviorCardWorkspace.Timeline.TacticalPreview = preview;
                var layout = new CharacterTestFairyGuiTacticalAuthoringLayoutBinder();
                var composition = panel.GetChild(CharacterTestTacticalPreviewControlContract.CompositionPanel).asCom;
                var context = panel.GetChild("authoringGroupText");
                Rect Bounds(Fgui.GObject item) => new Rect(item.x, item.y, item.width, item.height);
                Rect original = Bounds(context);
                int revision = workspace.CurrentRevision;
                foreach (var destination in new[] { CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup, CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary })
                {
                    model.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                    layout.BeginBind(panel, model); layout.Bind(panel, model, null, null);
                    var confirm = panel.GetChild("btnConfirmShared");
                    Assert.IsNotNull(confirm); Assert.IsTrue(confirm.visible);
                    Assert.IsTrue(panel.GetChild(CharacterTestTacticalPreviewControlContract.ActivationInspector).visible);
                    // Keep the cached card model exactly as the live failure did.
                    model.WorkspaceMode = destination; model.ShowBehaviorLogic = false;
                    layout.BeginBind(panel, model); layout.Bind(panel, model, null, null);
                    Assert.IsFalse(composition.visible);
                    Assert.IsFalse(confirm.visible); Assert.AreSame(composition, confirm.parent);
                    Assert.IsFalse(panel.GetChild(CharacterTestTacticalPreviewControlContract.ActivationInspector).visible);
                    Assert.IsFalse(panel.GetChild(CharacterTestTacticalPreviewControlContract.BindingLabel).visible);
                    Assert.IsFalse(panel.GetChild(CharacterTestTacticalPreviewControlContract.RequiredText).visible);
                    Assert.AreEqual(original, Bounds(context));
                    panel.SetSize(1910, 1080); panel.SetSize(1920, 1080);
                    Assert.IsFalse(confirm.visible, "Resize must not invoke a stale authoring layout callback.");
                    yield return null;
                }
                Assert.AreEqual(revision, workspace.CurrentRevision);
            }
            finally
            {
                panel?.Dispose(); if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (ownsPackage && package != null) Fgui.UIPackage.RemovePackage(package.id);
                if (atlas != null) UnityEngine.Object.Destroy(atlas);
            }
        }
    }
}
