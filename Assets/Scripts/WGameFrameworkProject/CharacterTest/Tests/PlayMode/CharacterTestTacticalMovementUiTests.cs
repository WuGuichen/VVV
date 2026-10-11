using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestTacticalMovementUiTests
    {
        [UnityTest]
        public IEnumerator SharedEventTimeFieldsWriteSourceWithoutOverwritingMovementCoordinates()
        {
            const string packageName = "MxCharacterTestRuntimeAiLabUi";
            var package = Fgui.UIPackage.GetByName(packageName);
            bool ownsPackage = package == null;
            Texture2D atlas = null;
            Fgui.GComponent panel = null;
            CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder binder = null;
            GameObject controllerObject = null;
            try
            {
                if (ownsPackage)
                {
                    string directory = Path.Combine(Application.dataPath, "Bundles/FGUI", packageName);
                    atlas = new Texture2D(2, 2);
                    Assert.IsTrue(atlas.LoadImage(File.ReadAllBytes(Path.Combine(directory, packageName + "_atlas0.png"))));
                    var texture = atlas;
                    package = Fgui.UIPackage.AddPackage(File.ReadAllBytes(Path.Combine(directory, packageName + "_fui.bytes")), packageName,
                        (string name, string extension, Type type, out Fgui.DestroyMethod destroy) => { destroy = Fgui.DestroyMethod.None; return type.IsInstanceOfType(texture) ? texture : null; });
                }
                panel = Fgui.UIPackage.CreateObject(packageName, "RuntimeAiLabBehaviorRecipeInspector").asCom;
                Fgui.GRoot.inst.AddChild(panel);
                controllerObject = new GameObject("Shared event time UI test"); controllerObject.SetActive(false);
                var controller = controllerObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.UsesTransientDraftPersistence);
                var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget);
                source.Slots.Clear();
                source.Timelines[0].Events[0].Operation.Movement.Target = CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition;
                source.Timelines[0].Events[0].Operation.Movement.X = 2;
                source.Timelines[0].Events[0].Operation.Movement.Z = 3;
                var document = new CharacterTestTacticalDslSource { CardId = "source-time-ui", Name = "源时间编辑",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new System.Collections.Generic.List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "use", Kind = "Reference", DefinitionId = "source-time-ui" } } },
                    Definitions = new System.Collections.Generic.List<CharacterTestTacticalSharedDefinition> {
                        new CharacterTestTacticalSharedDefinition { DefinitionId = "source-time-ui", Source = source } } };
                var workspace = controller.DraftWorkspace;
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                var card = workspace.Cards[id];
                controller.BuildTacticalPreview(card);
                var session = controller.CompositionEditingSession;
                Assert.IsTrue(session.ConfirmShared(session.Token));
                binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(null, default, null, null, null);
                Fgui.GComponent Bind(float width)
                {
                    card = workspace.Cards[id];
                    var preview = controller.BuildTacticalPreview(card);
                    Assert.IsTrue(preview.Ready, preview.Diagnostic);
                    var model = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel { TacticalPreview = preview };
                    model.Presentation.SelectEvent(source.EntryTimelineId, "move");
                    BehaviorRecipeTriggerEventInspectorProjection.Rebuild(model, preview.DisplayPayload, null);
                    panel.SetSize(width, 680);
                    binder.Bind(panel, model.RecipeInspector);
                    return panel.GetChild("conditionList").asList.GetChildAt(0).asCom;
                }
                var row = Bind(400);
                foreach (float width in new[] { 400f, 300f })
                {
                    row = Bind(width);
                    var timing = row.GetChild("eventTiming").asCom;
                    Assert.IsTrue(timing.GetChild("inputLocalFrame").asTextInput.editable);
                    Assert.GreaterOrEqual(timing.GetChild("summary").asTextField.textFormat.size, 14);
                    Assert.Greater(timing.GetChild("summary").asTextField.color.a, 0.9f);
                    StringAssert.Contains("0–120", timing.GetChild("summary").text);
                    Assert.AreNotSame(timing.GetChild("inputLocalFrame"), row.GetChild("inputLocalFrame"));
                    foreach (var child in timing.GetChildren().Where(c => c.visible))
                    { Assert.GreaterOrEqual(child.x, 0); Assert.LessOrEqual(child.x + child.width, timing.width + 0.1f, child.name); }
                }
                var startInput = row.GetChild("eventTiming").asCom.GetChild("inputLocalFrame").asTextInput;
                startInput.onFocusIn.Call(); startInput.text = "8"; startInput.onSubmit.Call();
                row = Bind(400);
                var endInput = row.GetChild("eventTiming").asCom.GetChild("inputDurationFrames").asTextInput;
                endInput.onFocusIn.Call(); endInput.text = "32"; endInput.onSubmit.Call();
                var changed = controller.BuildTacticalPreview(card);
                Assert.IsTrue(changed.TryGetFrames("move", out int start, out int end));
                Assert.AreEqual(8, start); Assert.AreEqual(32, end);
                Assert.AreEqual(2, changed.Source.Timelines[0].Events[0].Operation.Movement.X);
                Assert.AreEqual(3, changed.Source.Timelines[0].Events[0].Operation.Movement.Z);
                row = Bind(400);
                row.GetChild("eventTiming").asCom.GetChild("btnDurationMode").asButton.onClick.Call();
                var popup = Fgui.GRoot.inst.GetChild("behaviorConditionDropdown")?.asCom;
                Assert.IsNotNull(popup);
                popup.GetChildren().Single(c => c.visible && c.asButton != null && c.text.Contains("事件锚点")).asButton.onClick.Call();
                changed = controller.BuildTacticalPreview(card);
                Assert.AreEqual("Anchor", changed.Source.Timelines[0].Events[0].End.Kind);
                Assert.AreEqual("move", changed.Source.Timelines[0].Events[0].End.SourceEventId);
                Assert.AreEqual(24, changed.Source.Timelines[0].Events[0].End.OffsetFrames);
                Assert.IsTrue(changed.TryGetFrames("move", out start, out end));
                Assert.AreEqual(8, start); Assert.AreEqual(32, end);
                session.Undo(false);
                row = Bind(400);
                session.Undo(false);
                row = Bind(400);
                Assert.AreEqual("120", row.GetChild("eventTiming").asCom.GetChild("inputDurationFrames").asTextInput.text);
                int revisionBeforeFocus = workspace.CurrentRevision;
                row.GetChild("btnOutcomeReturn").asButton.onClick.Call();
                row = Bind(400);
                Assert.AreEqual(revisionBeforeFocus, workspace.CurrentRevision, "Opening focus must not write a block.");
                Assert.IsNotNull(controller.BuildTacticalPreview(card).FocusBlockId);
                Assert.AreEqual(8, controller.BuildTacticalPreview(card).DisplayOrigin);
                Assert.AreEqual(1, controller.BuildTacticalPreview(card).DisplayPayload.Timelines[0].Events.Count);
                panel.GetChild("btnOutcomeReturn").asButton.onClick.Call();
                popup = Fgui.GRoot.inst.GetChild("behaviorConditionDropdown")?.asCom;
                Assert.IsNotNull(popup);
                popup.GetChildren().Single(c => c.visible && c.asButton != null && c.text.Contains("移动（六种方式）")).asButton.onClick.Call();
                row = Bind(400);
                var focused = controller.BuildTacticalPreview(card);
                Assert.AreEqual(3, focused.Source.Version);
                Assert.AreEqual(2, focused.FocusBlock.EventIds.Count);
                Assert.AreEqual(2, focused.DisplayPayload.Timelines[0].Events.Count);
                row.GetChild("btnOutcomeReturn").asButton.onClick.Call();
                popup = Fgui.GRoot.inst.GetChild("behaviorConditionDropdown")?.asCom;
                popup.GetChildren().Single(c => c.visible && c.asButton != null && c.text.Contains("转向目标")).asButton.onClick.Call();
                row = Bind(400);
                Assert.AreEqual("TurnToTarget", controller.BuildTacticalPreview(card).Source.Timelines[0].Events.First(e => e.EventId == "move").Operation.Kind);
                panel.GetChild("btnCloseInspector").asButton.onClick.Call();
                row = Bind(400);
                Assert.IsNull(controller.BuildTacticalPreview(card).FocusBlockId);
                Assert.AreEqual(2, controller.BuildTacticalPreview(card).DisplayPayload.Timelines[0].Events.Count,
                    "Outer view must show one block plus the unrelated facing event.");
                session.Undo(false); Bind(400);
                session.Undo(false); row = Bind(400);
                session.FinishSharedEditing();
                row = Bind(400);
                Assert.IsFalse(row.GetChild("eventTiming").asCom.GetChild("inputLocalFrame").asTextInput.editable);
                yield return null;
            }
            finally
            {
                if (panel != null && !panel.isDisposed) binder?.Bind(panel, null);
                panel?.Dispose();
                if (controllerObject != null) UnityEngine.Object.DestroyImmediate(controllerObject);
                if (ownsPackage && package != null) Fgui.UIPackage.RemovePackage(package.id);
                if (atlas != null) UnityEngine.Object.Destroy(atlas);
            }
        }

        [UnityTest]
        public IEnumerator PublishedInspectorDisplaysSixStrategiesAndConditionalFields()
        {
            const string packageName = "MxCharacterTestRuntimeAiLabUi";
            var package = Fgui.UIPackage.GetByName(packageName);
            bool ownsPackage = package == null;
            Texture2D atlas = null;
            Fgui.GComponent panel = null;
            CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder binder = null;
            GameObject controllerObject = null;
            try
            {
                if (ownsPackage)
                {
                    string directory = Path.Combine(Application.dataPath, "Bundles/FGUI", packageName);
                    atlas = new Texture2D(2, 2);
                    Assert.IsTrue(atlas.LoadImage(File.ReadAllBytes(Path.Combine(directory, packageName + "_atlas0.png"))));
                    var texture = atlas;
                    package = Fgui.UIPackage.AddPackage(File.ReadAllBytes(Path.Combine(directory, packageName + "_fui.bytes")), packageName,
                        (string name, string extension, Type type, out Fgui.DestroyMethod destroy) => { destroy = Fgui.DestroyMethod.None; return type.IsInstanceOfType(texture) ? texture : null; });
                }
                panel = Fgui.UIPackage.CreateObject(packageName, "RuntimeAiLabBehaviorRecipeInspector").asCom;
                Assert.IsNotNull(panel);
                Fgui.GRoot.inst.AddChild(panel);
                controllerObject = new GameObject("Tactical movement UI test"); controllerObject.SetActive(false);
                var controller = controllerObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.UsesTransientDraftPersistence);
                var workspace = controller.DraftWorkspace;
                var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
                source.Schema = CharacterTestTacticalAuthoringSource.CurrentSchema; source.Version = 2;
                Assert.IsTrue(workspace.TryImportTacticalSource(1, 0, workspace.CurrentRevision, source, out string id, out string issue), issue);
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_activeTacticalCardId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, id);
                var editor = new CharacterTestTacticalEditingSession(controller, id);
                var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, null, "UI fixture");
                editor.Bind(workspace, workspace.Cards[id], preview);
                Assert.IsTrue(editor.CanEdit);
                binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(null, default, null, null, null);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder).GetField("_root", flags).SetValue(binder, panel);
                var bind = typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder).GetMethod("BindTacticalMovement", flags);
                var movement = source.Timelines[0].Events[0].Operation.Movement;
                foreach (float width in new[] { 400f, 300f })
                foreach (CharacterTestRuntimeAiLabMovementStrategy strategy in Enum.GetValues(typeof(CharacterTestRuntimeAiLabMovementStrategy)))
                {
                    foreach (var child in panel.GetChildren()) child.visible = false;
                    movement.Strategy = strategy;
                    float bottom = (float)bind.Invoke(binder, new object[] { editor, source.EntryTimelineId, "move", movement, 12f, width });
                    panel.SetSize(width, bottom + 12);
                    Assert.AreEqual("移动方式：" + CharacterTestTacticalMovement.Label(strategy), panel.GetChild("btnDurationMode").asButton.title);
                    Assert.AreEqual(movement.UsesSide, panel.GetChild("btnSignalReason").visible);
                    Assert.AreEqual(movement.UsesBand, panel.GetChild("inputCooldown").visible);
                    Assert.IsInstanceOf<Fgui.GTextInput>(panel.GetChild("inputMaxExecutions"));
                    foreach (var child in panel.GetChildren().Where(c => c.visible))
                    { Assert.GreaterOrEqual(child.x, 0, child.name); Assert.LessOrEqual(child.x + child.width, width + 0.1f, child.name); Assert.LessOrEqual(child.y + child.height, bottom + 0.1f, child.name); }
                }
                movement.Strategy = CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget;
                foreach (var target in new[] { CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, CharacterTestRuntimeAiLabMovementTargetKind.Entity, CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget })
                {
                    foreach (var child in panel.GetChildren()) child.visible = false;
                    movement.Target = target;
                    float bottom = (float)bind.Invoke(binder, new object[] { editor, source.EntryTimelineId, "move", movement, 50f, 400f });
                    panel.SetSize(400, bottom + 12);
                    Assert.AreEqual(target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, panel.GetChild("inputLocalFrame").visible);
                    Assert.AreEqual(target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, panel.GetChild("inputDurationFrames").visible);
                }
                panel.GetChild("background").visible = true; panel.GetChild("background").SetSize(panel.width, panel.height);
                panel.GetChild("headerTitle").visible = true; panel.GetChild("headerTitle").text = "环绕目标 · 移动参数";
                panel.SetXY(12, 12);
                var minimum = panel.GetChild("inputMaxExecutions").asTextInput;
                minimum.onFocusIn.Call(); minimum.text = "2.25"; minimum.onSubmit.Call();
                Assert.AreEqual(2.25f, workspace.Cards[id].CloneTacticalSource().Timelines[0].Events[0].Operation.Movement.Minimum);
                Assert.IsTrue(workspace.TryUndo(out issue), issue);
                Assert.AreEqual(1.5f, workspace.Cards[id].CloneTacticalSource().Timelines[0].Events[0].Operation.Movement.Minimum);
                minimum.text = "1.5";
                yield return null;
                string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TacticalMovementTests/movement-inspector.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(evidence));
                ScreenCapture.CaptureScreenshot(evidence);
                yield return null;
            }
            finally
            {
                if (binder != null) typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder)
                    .GetMethod("RestoreTacticalInputs", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(binder, null);
                panel?.Dispose();
                if (controllerObject != null) UnityEngine.Object.DestroyImmediate(controllerObject);
                if (ownsPackage && package != null) Fgui.UIPackage.RemovePackage(package.id);
                if (atlas != null) UnityEngine.Object.Destroy(atlas);
            }
        }
    }
}
