using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class RuntimeAiLabBehaviorCardConnectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator PublishedRecipeTimelineCreatesBranchEventAndUndo()
        {
            RemovePackageIfLoaded();
            Fgui.FontManager.Clear();
            Texture2D atlas = null;
            IMxFairyGuiComponentHandle panelHandle = null;
            GameObject controllerHost = null;
            try
            {
                Fgui.UIPackage package = LoadPublishedPackage(out atlas);
                Assert.That(package, Is.Not.Null);
                Fgui.GComponent component = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName)?.asCom;
                Assert.That(component, Is.Not.Null);
                panelHandle = new MxFairyGuiComponentHandle(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName,
                    component);
                var concrete = panelHandle as MxFairyGuiComponentHandle;
                Assert.That(concrete, Is.Not.Null);
                concrete.Component.SetSize(1920f, 1080f);
                Fgui.GRoot.inst.AddChild(concrete.Component);

                controllerHost = new GameObject("Runtime AI Lab PlayMode Connection Test");
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    controllerHost.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.That(controller.UsesTransientDraftPersistence, Is.True);
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();

                const int entryIndex = 2;
                CharacterTestRuntimeAiLabBehaviorCardDraft card = ResolveCard(controller, entryIndex);
                Assert.That(card.LogicPayload.Timelines.Count, Is.EqualTo(1));
                Assert.That(card.LogicPayload.Timelines[0].EntryEvent, Is.Not.Null);
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timelineBefore =
                    ResolveUnifiedTimeline(card);
                int triggerCountBefore = timelineBefore.Events.Count;

                var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
                var commandHandler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                var commandSink = new ForwardingCommandSink
                {
                    Handler = commandHandler
                };
                var binder = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    null,
                    null,
                    commandHandler);
                Action rebuild = () => BuildAndBind(
                    controller,
                    entryIndex,
                    viewModel,
                    binder,
                    panelHandle);
                commandHandler.Configure(
                    controller,
                    null,
                    null,
                    rebuild,
                    () => entryIndex,
                    workspaceView: () => viewModel.BehaviorCardWorkspace);
                rebuild();
                yield return null;

                Fgui.GComponent timeline = concrete.Component.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)?.asCom;
                Assert.That(timeline, Is.Not.Null);
                Fgui.GComponent recipeHost = timeline.GetChild("timelineGroupHost")?.asCom;
                Assert.That(recipeHost, Is.Not.Null);
                Fgui.GComponent viewport = recipeHost.GetChild("recipeViewport")?.asCom;
                Fgui.GComponent rows = viewport?.GetChild("recipeRows")?.asCom;
                Assert.That(rows, Is.Not.Null);
                int trackCountBefore = CountVisibleRecipeTracks(rows);
                Assert.That(trackCountBefore, Is.GreaterThanOrEqualTo(2),
                    "The unified timeline must render its primary action and existing events.");
                int visibleEntryTrackCount =
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].EntryConditionCount > 0
                        ? 1
                        : 0;

                Fgui.GComponent primaryTrack = rows.GetChild(
                    "recipeTrack" + visibleEntryTrackCount)?.asCom;
                Fgui.GObject primaryTrackLabel = primaryTrack?.GetChild("trackLabel");
                Fgui.GComponent primaryEventRow = primaryTrack?.GetChild("trackContent")?.asCom;
                Assert.That(primaryTrackLabel, Is.Not.Null);
                Assert.That(primaryTrackLabel.visible, Is.False,
                    "Track labels are an internal layout artifact and must not consume editor width.");
                Assert.That(primaryTrackLabel.touchable, Is.False);
                Assert.That(primaryEventRow, Is.Not.Null);
                Assert.That(primaryEventRow.x, Is.LessThan(20f));
                Fgui.GObject primaryEvent = primaryEventRow.GetChild("eventSpan");
                Assert.That(primaryEvent, Is.Not.Null);
                Vector2 menuPoint = primaryEvent.LocalToGlobal(
                    new Vector2(primaryEvent.width * 0.5f, primaryEvent.height * 0.5f));
                primaryEventRow.DispatchEvent(
                    CreatePointerEvent("onRightClick", primaryEventRow, menuPoint));

                Fgui.GComponent contextMenu = recipeHost.GetChild("recipeContextMenu")?.asCom;
                Assert.That(contextMenu, Is.Not.Null);
                Assert.That(contextMenu.visible, Is.True);
                Fgui.GComponent addInstantEventItem = contextMenu.GetChild("menuItem0")?.asCom;
                Assert.That(addInstantEventItem, Is.Not.Null);
                StringAssert.Contains(
                    "单帧事件",
                    addInstantEventItem.GetChild("label")?.asTextField?.text);
                addInstantEventItem.DispatchEvent(
                    CreatePointerEvent("onClick", addInstantEventItem, menuPoint));
                yield return null;

                CharacterTestRuntimeAiLabBehaviorCardDraft mutated = ResolveCard(controller, entryIndex);
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline mutatedTimeline =
                    ResolveUnifiedTimeline(mutated);
                Assert.That(mutatedTimeline.Events.Count,
                    Is.EqualTo(triggerCountBefore + 1),
                    commandHandler.LastMessage);
                CharacterTestRuntimeAiLabBehaviorRecipeEvent createdEvent =
                    mutatedTimeline.Events.Find(candidate =>
                        candidate.TimingKind
                            == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                        && candidate.StartFrame == 0);
                Assert.That(createdEvent, Is.Not.Null,
                    "Right-click creation at 0f must create a native unified-timeline instant event.");

                recipeHost = timeline.GetChild("timelineGroupHost")?.asCom;
                rows = recipeHost?.GetChild("recipeViewport")?.asCom?.GetChild("recipeRows")?.asCom;
                Assert.That(CountVisibleRecipeTracks(rows), Is.EqualTo(trackCountBefore + 1));
                Fgui.GComponent createdTrack = rows.GetChild(
                    "recipeTrack" + trackCountBefore)?.asCom;
                Assert.That(createdTrack, Is.Not.Null);
                Fgui.GComponent createdContent = createdTrack.GetChild("trackContent")?.asCom;
                bool createdVisualVisible = createdContent?.GetChild("eventSpan")?.visible == true
                    || createdContent?.GetChild("eventMarker")?.visible == true;
                Assert.That(createdVisualVisible, Is.True,
                    "The new unified-timeline event must render as either a span or instant marker.");

                Fgui.GObject undo = timeline.GetChild("btnTimelineUndo");
                Assert.That(undo, Is.Not.Null);
                Assert.That(undo.enabled, Is.True);
                undo.onClick.Call();
                Assert.That(ResolveUnifiedTimeline(
                        ResolveCard(controller, entryIndex)).Events.Count,
                    Is.EqualTo(triggerCountBefore));
                yield return null;
                recipeHost = timeline.GetChild("timelineGroupHost")?.asCom;
                rows = recipeHost?.GetChild("recipeViewport")?.asCom?.GetChild("recipeRows")?.asCom;
                Assert.That(CountVisibleRecipeTracks(rows), Is.EqualTo(trackCountBefore));
            }
            finally
            {
                if (panelHandle is MxFairyGuiComponentHandle concrete
                    && concrete.Component.parent != null)
                {
                    concrete.Component.RemoveFromParent();
                }
                panelHandle?.Dispose();
                RemovePackageIfLoaded();
                if (atlas != null)
                    UnityEngine.Object.Destroy(atlas);
                if (controllerHost != null)
                    UnityEngine.Object.Destroy(controllerHost);
            }
        }

        private static void BuildAndBind(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int entryIndex,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder binder,
            IMxFairyGuiComponentHandle panelHandle)
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft card = ResolveCard(controller, entryIndex);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                card.DisplayName,
                compile,
                card.LogicPayload,
                selectedStageIndex: -1);
            viewModel.BehaviorCardWorkspace.Timeline.CanEdit = true;
            viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            viewModel.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            viewModel.Workspace.ShowBehaviorCard = true;
            binder.Bind(panelHandle, viewModel);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft ResolveCard(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int entryIndex)
        {
            Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True);
            return controller.DraftWorkspace.Cards[group.Entries[entryIndex].CardDraftId];
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline ResolveUnifiedTimeline(
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload.Clone();
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(payload);
            Assert.That(payload.Timelines.Count, Is.EqualTo(1));
            return payload.Timelines[0];
        }

        private static int CountVisibleRecipeTracks(Fgui.GComponent rows)
        {
            int count = 0;
            for (int i = 0; rows != null && i < rows.numChildren; i++)
            {
                Fgui.GObject child = rows.GetChildAt(i);
                if (child != null
                    && child.visible
                    && child.name.StartsWith("recipeTrack", StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static Fgui.EventContext CreatePointerEvent(
            string type,
            Fgui.GObject sender,
            Vector2 point)
        {
            var input = new Fgui.InputEvent();
            SetInternalProperty(input, nameof(Fgui.InputEvent.x), point.x);
            SetInternalProperty(input, nameof(Fgui.InputEvent.y), point.y);
            var context = new Fgui.EventContext { type = type };
            SetInternalProperty(context, nameof(Fgui.EventContext.sender), sender);
            SetInternalProperty(context, nameof(Fgui.EventContext.inputEvent), input);
            return context;
        }

        private static void SetInternalProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }

        private static Fgui.UIPackage LoadPublishedPackage(out Texture2D atlas)
        {
            string packageId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId;
            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI",
                packageId,
                packageId + "_fui.bytes"));
            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(atlas.LoadImage(File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI",
                packageId,
                packageId + "_atlas0.png"))), Is.True);
            Texture2D packageAtlas = atlas;
            return Fgui.UIPackage.AddPackage(
                packageBytes,
                packageId,
                (string name,
                    string extension,
                    Type type,
                    out Fgui.DestroyMethod destroyMethod) =>
                {
                    destroyMethod = Fgui.DestroyMethod.None;
                    return type != null && type.IsInstanceOfType(packageAtlas)
                        ? packageAtlas
                        : null;
                });
        }

        private static void RemovePackageIfLoaded()
        {
            Fgui.UIPackage package = Fgui.UIPackage.GetById(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId)
                ?? Fgui.UIPackage.GetByName(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
            if (package != null)
                Fgui.UIPackage.RemovePackage(package.id);
        }

        private sealed class ForwardingCommandSink : IMxUiCommandSink
        {
            internal CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler Handler { get; set; }

            public void Enqueue(MxUiCommand command)
            {
                Handler?.TryHandle(command, out _);
            }
        }
    }
}
