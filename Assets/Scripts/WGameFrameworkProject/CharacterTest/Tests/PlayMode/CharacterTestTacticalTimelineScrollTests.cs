using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestTacticalTimelineScrollTests
    {
        [UnityTest]
        public IEnumerator RebindAndResizePreserveManualScrollWhileNewSelectionStillLocates()
        {
            const string packageName = "MxCharacterTestRuntimeAiLabUi";
            var package = Fgui.UIPackage.GetByName(packageName);
            bool ownsPackage = package == null;
            Texture2D atlas = null;
            Fgui.GComponent panel = null;
            GameObject owner = null;
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
                Assert.IsNotNull(panel); Fgui.GRoot.inst.AddChild(panel);
                owner = new GameObject("Timeline scroll regression"); owner.SetActive(false);
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance);
                source.Slots.Clear();
                for (int i = 0; i < 6; i++) CharacterTestTacticalAuthoringEdits.Add(source, source.EntryTimelineId, "Movement");
                var document = new CharacterTestTacticalDslSource { CardId = "scroll-test", Name = "滚动测试",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" },
                        new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string cardId, out string issue), issue);
                int revision = workspace.CurrentRevision;
                document = workspace.ResolveComposition(workspace.Cards[cardId]);
                var inputs = CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>());
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out var compiled, out issue), issue);
                var session = new CharacterTestTacticalCompositionEditingSession(controller); session.Bind(workspace, workspace.Cards[cardId]);
                var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, inputs, "");
                preview.WholeCard = new CharacterTestTacticalWholeCardTimeline(compiled.Program, document, compiled.SourceLocations, session);
                var model = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
                model.Timeline.Available = true; model.Timeline.CompositionEditor = session;
                model.Timeline.TacticalPreview = preview; model.Timeline.RecipePayload = preview.WholeCard.DisplayPayload;
                var binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder(null, default, null);
                var timeline = panel.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline).asCom;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var implementation = typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder).GetField("_timeline", flags).GetValue(binder);
                var scroll = implementation.GetType().GetMethod("ScrollRecipeVertically", flags);
                float Offset() => (float)implementation.GetType().GetField("_recipeVerticalOffset", flags).GetValue(implementation);
                void Rebind(float height)
                {
                    // The panel's ordinary layout can run before the tactical layout sets final bounds.
                    timeline.SetSize(1400, 1100);
                    binder.Bind(panel, model, CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Logic);
                    timeline.SetSize(1000, height);
                    binder.RefreshRecipeGeometry();
                }
                Rebind(180);
                yield return null;
                scroll.Invoke(implementation, new object[] { 80f });
                Assert.AreEqual(80, Offset(), 0.1f);
                Assert.IsFalse(model.Timeline.FollowCurrent);
                foreach (float height in new[] { 180f, 220f, 160f, 180f })
                {
                    Rebind(height); yield return null;
                    Assert.AreEqual(80, Offset(), 0.1f, "Refresh or resize reset manual scroll.");
                }
                scroll.Invoke(implementation, new object[] { -40f });
                Rebind(180); Assert.AreEqual(40, Offset(), 0.1f);
                session.Select("second", CharacterTestTacticalEditTarget.SharedDefinition);
                preview.WholeCard = new CharacterTestTacticalWholeCardTimeline(compiled.Program, document, compiled.SourceLocations, session);
                model.Timeline.RecipePayload = preview.WholeCard.DisplayPayload;
                Rebind(180);
                Assert.Greater(Offset(), 80, "Explicitly selecting another reference must still locate it.");
                Assert.AreEqual(revision, workspace.CurrentRevision, "Viewport changes must not edit authoring data.");
            }
            finally
            {
                panel?.Dispose();
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (ownsPackage && package != null) Fgui.UIPackage.RemovePackage(package.id);
                if (atlas != null) UnityEngine.Object.Destroy(atlas);
            }
        }
    }
}
