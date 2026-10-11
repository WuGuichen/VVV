using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestRuntimeAiLabCharacterActionAuthoringTests
    {
        [Test]
        public void TimelinePreview_ProjectsRuntimeWorkstationRowsDeterministically()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.IsReady);
            Assert.IsTrue(runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig action));

            CharacterActionTimelinePreviewViewModel first =
                CharacterActionTimelinePreviewProjection.Build(action);
            CharacterActionTimelinePreviewViewModel second =
                CharacterActionTimelinePreviewProjection.Build(action);

            Assert.IsTrue(first.Available, first.Diagnostic);
            Assert.AreEqual(action.DurationFrames, first.DurationFrames);
            Assert.AreEqual(9, first.Rows.Length);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Phase, first.Rows[0].Kind);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Cancel, first.Rows[7].Kind);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Interrupt, first.Rows[8].Kind);
            Assert.AreEqual(first.Rows.Length, second.Rows.Length);
            Assert.AreEqual(first.Rows[0].Entries.Length, second.Rows[0].Entries.Length);
        }

        [Test]
        public void TimelinePreview_MissingActionReturnsBlockingDiagnostic()
        {
            CharacterActionTimelinePreviewViewModel preview =
                CharacterActionTimelinePreviewProjection.Build(null);

            Assert.IsFalse(preview.Available);
            StringAssert.Contains("不存在", preview.Diagnostic);
            Assert.AreEqual(0, preview.Rows.Length);
        }

        [Test]
        public void EditorResolver_CombatGirlLightAttackTargetsCommonFormalSource()
        {
            var request = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                ActionId = new MxFramework.CharacterAction.CharacterActionId(301),
                Context = new CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
                    1,
                    "char.combat_girl_sword",
                    "equip_state.combat_girl_sword.sword_empty",
                    "combat_girl_sword.sword_empty"),
            };

            Assert.IsTrue(
                CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge.TryResolve(
                    request,
                    out CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge.ResolvedRequest resolved,
                    out string error),
                error);
            Assert.AreEqual("Common", resolved.ActionLibraryId);
            Assert.AreEqual(
                "CharacterActionSource/Libraries/Common",
                resolved.ActionLibrarySourceDirectory);
            Assert.AreEqual("combat_girl_sword.default", resolved.ProfileId);
        }

        [Test]
        public void OpenRequest_StatusCallbackIsNotSerialized()
        {
            var request = new CharacterActionAuthoringOpenRequest
            {
                ActionId = 301,
                StatusCallback = _ => { },
            };

            string json = JsonConvert.SerializeObject(request);

            StringAssert.DoesNotContain("StatusCallback", json);
            StringAssert.Contains("\"ActionId\":301", json);
        }

        [Test]
        public void WorkspaceSession_ProjectsAllTracksAndSupportsUndoRedoWithoutSaving()
        {
            CharacterActionAuthoringOpenRequest open = CreateCombatGirlOpenRequest();
            using CharacterActionAuthoringWorkspaceSession session =
                CharacterActionAuthoringWorkspaceSession.Open(open);
            CharacterActionAuthoringWorkspaceSnapshot initial = session.BuildSnapshot();
            Assert.AreEqual(301, initial.SelectedActionId);
            Assert.IsFalse(
                initial.Dirty,
                "Fresh workspace must be clean. Dirty paths: " + string.Join(", ", initial.DirtyPaths));
            Assert.AreEqual(7, initial.Tracks.Length);
            Assert.Greater(initial.Actions.Length, 0);
            Assert.IsTrue(Array.Exists(
                initial.Tracks,
                track => Array.Exists(track.Items, item => item.CanResize)));
            Assert.IsFalse(Array.Exists(
                initial.Tracks,
                track => Array.Exists(
                    track.Items,
                    item => item.TrackKind == "Motion"
                        && item.Title.StartsWith("RootMotion", StringComparison.OrdinalIgnoreCase)
                        && item.CanResize)));
            CharacterActionAuthoringInspectorFieldDescriptor display = Array.Find(
                initial.InspectorFields,
                field => field.Path == "displayName");
            Assert.NotNull(display);

            string firstItemId = string.Empty;
            for (int trackIndex = 0;
                 trackIndex < initial.Tracks.Length && firstItemId.Length == 0;
                 trackIndex++)
            {
                if (initial.Tracks[trackIndex]?.Items?.Length > 0)
                    firstItemId = initial.Tracks[trackIndex].Items[0].ItemId;
            }
            Assert.IsNotEmpty(firstItemId);
            var selectionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CharacterActionAuthoringWorkspaceSnapshot selected = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.SelectTimelineItem,
                    ExpectedRevision = initial.Revision,
                    Text = firstItemId,
                });
            selectionStopwatch.Stop();
            TestContext.WriteLine(
                "CharacterAction selection-only workspace update: "
                + selectionStopwatch.Elapsed.TotalMilliseconds.ToString("0.###")
                + " ms");
            Assert.AreSame(initial.Actions, selected.Actions);
            Assert.AreSame(initial.Tracks, selected.Tracks);
            Assert.AreEqual(firstItemId, selected.SelectedTimelineItemId);
            Assert.IsTrue(Array.Exists(
                selected.Tracks,
                track => Array.Exists(track.Items, item => item.Selected)));
            CharacterActionAuthoringWorkspaceSnapshot deselected = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.SelectTimelineItem,
                    ExpectedRevision = selected.Revision,
                    Text = string.Empty,
                });
            Assert.AreSame(initial.Actions, deselected.Actions);
            Assert.AreSame(initial.Tracks, deselected.Tracks);

            CharacterActionAuthoringWorkspaceSnapshot changed = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.SetField,
                    ExpectedRevision = deselected.Revision,
                    Text = "displayName",
                    SecondaryText = "AI Lab Temporary Name",
                });
            Assert.IsTrue(changed.Dirty);
            Assert.IsTrue(changed.CanUndo);
            Assert.AreEqual(
                "AI Lab Temporary Name",
                Array.Find(changed.InspectorFields, field => field.Path == "displayName")?.Value);

            CharacterActionAuthoringWorkspaceSnapshot undone = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.Undo,
                    ExpectedRevision = changed.Revision,
                });
            Assert.AreEqual(
                display.Value,
                Array.Find(undone.InspectorFields, field => field.Path == "displayName")?.Value);
            Assert.IsTrue(undone.CanRedo);

            CharacterActionAuthoringWorkspaceSnapshot arrayAdded = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.AddArrayElement,
                    ExpectedRevision = undone.Revision,
                    Text = "requirements",
                });
            StringAssert.Contains(
                "items",
                Array.Find(arrayAdded.InspectorFields, field => field.Path == "requirements")?.Value);
            Assert.IsTrue(arrayAdded.Dirty);
        }

        [Test]
        public void WorkspaceBridge_PreviewSourceLivesUnderTempAndCloseCleansIt()
        {
            CharacterActionAuthoringOpenRequest open = CreateCombatGirlOpenRequest();
            var request = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                ActionId = new MxFramework.CharacterAction.CharacterActionId(301),
                Context = new CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
                    1,
                    "char.combat_girl_sword",
                    "equip_state.combat_girl_sword.sword_empty",
                    "combat_girl_sword.sword_empty"),
            };
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot workspace =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Open,
                        OpenRequest = request,
                    });
            Assert.IsTrue(workspace.Available, workspace.Status);
            string firstItemId = string.Empty;
            for (int trackIndex = 0;
                 trackIndex < workspace.Tracks.Length && firstItemId.Length == 0;
                 trackIndex++)
            {
                if (workspace.Tracks[trackIndex]?.Items?.Length > 0)
                    firstItemId = workspace.Tracks[trackIndex].Items[0].ItemId;
            }
            Assert.IsNotEmpty(firstItemId);
            var bridgeSelectionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot selected =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Execute,
                        Command = new CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
                        {
                            Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SelectTimelineItem,
                            ExpectedRevision = workspace.Revision,
                            Text = firstItemId,
                        },
                    });
            bridgeSelectionStopwatch.Stop();
            TestContext.WriteLine(
                "CharacterAction selection-only bridge update: "
                + bridgeSelectionStopwatch.Elapsed.TotalMilliseconds.ToString("0.###")
                + " ms");
            Assert.AreSame(workspace.Actions, selected.Actions);
            Assert.AreSame(workspace.Tracks, selected.Tracks);
            Assert.AreEqual(firstItemId, selected.SelectedTimelineItemId);
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot preview =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPlay,
                        PreviewFrame = 0,
                    });
            StringAssert.Contains("Temp/RuntimeAiLab/CharacterActionPreview", preview.PreviewSourceDirectory);
            Assert.IsTrue(System.IO.Directory.Exists(preview.PreviewSourceDirectory));
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(
                preview.PreviewSourceDirectory,
                "character_actions.json")));

            CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                {
                    Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close,
                });
            Assert.IsFalse(System.IO.Directory.Exists(preview.PreviewSourceDirectory));
        }

        [Test]
        public void WorkspaceBridge_CapturePreservesOpenContextAndPreviewAcrossUiRebind()
        {
            var request = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                ActionId = new CharacterActionId(301),
                Context = new CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
                    1,
                    "char.combat_girl_sword",
                    "equip_state.combat_girl_sword.sword_empty",
                    "combat_girl_sword.sword_empty"),
                SelectedAgentIndex = 2,
                StableCardId = "card.return",
                TimelineId = "timeline.return",
                EventId = "event.return",
                TimelineMode = CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
            };
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot opened =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Open,
                        OpenRequest = request,
                    });
            try
            {
                Assert.IsTrue(opened.Available, opened.Status);
                CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot preview =
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                        new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                        {
                            Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
                                .PreviewPlay,
                            PreviewFrame = 3,
                        });
                CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot captured =
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                        new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                        {
                            Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
                                .Capture,
                        });

                Assert.IsTrue(captured.Available, captured.Status);
                Assert.AreEqual(opened.SessionId, captured.SessionId);
                Assert.IsTrue(captured.PreviewActive);
                Assert.IsTrue(captured.PreviewPlaying);
                Assert.AreEqual(preview.PreviewFrame, captured.PreviewFrame);
                Assert.AreEqual(2, captured.ReturnSelectedAgentIndex);
                Assert.AreEqual("card.return", captured.ReturnStableCardId);
                Assert.AreEqual("timeline.return", captured.ReturnTimelineId);
                Assert.AreEqual("event.return", captured.ReturnEventId);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                    captured.ReturnTimelineMode);
            }
            finally
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close,
                    });
            }
        }

        [Test]
        public void WorkspaceClose_RestartsRuntimeOnlyWhilePreviewIsActive()
        {
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .CharacterActionWorkspaceCloseRequiresRuntimeRestart(null));
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .CharacterActionWorkspaceCloseRequiresRuntimeRestart(
                        new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot()));
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .CharacterActionWorkspaceCloseRequiresRuntimeRestart(
                        new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
                        {
                            PreviewActive = true,
                        }));
        }

        [Test]
        public void WorkspaceSession_ResizableTimelineItemUsesOneUndoTransaction()
        {
            using CharacterActionAuthoringWorkspaceSession session =
                CharacterActionAuthoringWorkspaceSession.Open(CreateCombatGirlOpenRequest());
            CharacterActionAuthoringWorkspaceSnapshot initial = session.BuildSnapshot();
            CharacterActionAuthoringTimelineItemSnapshot resizable = null;
            for (int trackIndex = 0;
                 trackIndex < initial.Tracks.Length && resizable == null;
                 trackIndex++)
            {
                resizable = Array.Find(
                    initial.Tracks[trackIndex].Items,
                    item => item.CanResize);
            }
            Assert.NotNull(resizable);
            int nextStart = Math.Min(resizable.StartFrame + 1, resizable.EndFrame);
            int nextEnd = Math.Max(nextStart, resizable.EndFrame - 1);
            if (nextStart == resizable.StartFrame && nextEnd == resizable.EndFrame)
                nextEnd = resizable.EndFrame + 1;

            CharacterActionAuthoringWorkspaceSnapshot resized = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.MoveResizeTimelineItem,
                    ExpectedRevision = initial.Revision,
                    Text = resizable.ItemId,
                    IntValue = nextStart,
                    SecondaryIntValue = nextEnd,
                });
            CharacterActionAuthoringTimelineItemSnapshot resizedItem = FindTimelineItem(
                resized,
                resizable.ItemId);
            Assert.NotNull(
                resizedItem,
                resized.Status + " originalItemId=" + resizable.ItemId
                + " selectedItemId=" + resized.SelectedTimelineItemId);
            Assert.IsTrue(
                resizedItem.StartFrame != resizable.StartFrame
                || resizedItem.EndFrame != resizable.EndFrame,
                "The range command must change at least one edge after track-specific constraints are applied.");
            Assert.IsTrue(resized.CanUndo);

            var undoStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CharacterActionAuthoringWorkspaceSnapshot undone = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.Undo,
                    ExpectedRevision = resized.Revision,
                });
            undoStopwatch.Stop();
            TestContext.WriteLine(
                "CharacterAction undo workspace update: "
                + undoStopwatch.Elapsed.TotalMilliseconds.ToString("0.###")
                + " ms");
            CharacterActionAuthoringTimelineItemSnapshot restored = FindTimelineItem(
                undone,
                resizable.ItemId);
            restored ??= FindFirstResizableTimelineItem(undone);
            Assert.NotNull(restored);
            Assert.AreEqual(resizable.StartFrame, restored.StartFrame);
            Assert.AreEqual(resizable.EndFrame, restored.EndFrame);

            var redoStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CharacterActionAuthoringWorkspaceSnapshot redone = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.Redo,
                    ExpectedRevision = undone.Revision,
                });
            redoStopwatch.Stop();
            TestContext.WriteLine(
                "CharacterAction redo workspace update: "
                + redoStopwatch.Elapsed.TotalMilliseconds.ToString("0.###")
                + " ms");
            CharacterActionAuthoringTimelineItemSnapshot redoneItem = FindTimelineItem(
                redone,
                resizedItem.ItemId);
            redoneItem ??= FindFirstResizableTimelineItem(redone);
            Assert.NotNull(redoneItem);
            Assert.AreEqual(resizedItem.StartFrame, redoneItem.StartFrame);
            Assert.AreEqual(resizedItem.EndFrame, redoneItem.EndFrame);
            Assert.IsTrue(redone.CanUndo);
        }

        [Test]
        public void WorkspaceCommand_DoesNotRequestFullAiLabPanelRefresh()
        {
            Func<CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest,
                CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot> previous =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler;
            int refreshCount = 0;
            try
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler = _ =>
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
                    {
                        Available = true,
                        CanRedo = true,
                        Status = "updated",
                    };
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                    controller: null,
                    refreshPanel: () =>
                    {
                        refreshCount++;
                        return true;
                    });
                sink.Enqueue(new MxFramework.UI.MxUiCommand(
                    default,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .CharacterActionWorkspaceCommand,
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.Undo,
                    }));

                Assert.AreEqual(0, refreshCount);
                Assert.IsTrue(sink.CharacterActionWorkspace.CanRedo);
            }
            finally
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler = previous;
            }
        }

        [Test]
        public void WorkspacePreviewBaselineReset_RemountsPanelAfterFinalSnapshot()
        {
            Func<CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest,
                CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot> previous =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler;
            int refreshCount = 0;
            int remountCount = 0;
            try
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler = request =>
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
                    {
                        Available = true,
                        PreviewActive = request?.Kind
                            == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
                                .PreviewPlay,
                        PreviewPlaying = request?.Kind
                            == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
                                .PreviewPlay,
                        Status = "preview",
                    };
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                    controller: null,
                    refreshPanel: () =>
                    {
                        refreshCount++;
                        return true;
                    },
                    remountPanel: () =>
                    {
                        remountCount++;
                        return true;
                    });

                sink.Enqueue(new MxFramework.UI.MxUiCommand(
                    default,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .CharacterActionWorkspacePreview,
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
                            .PreviewPlay,
                        PreviewFrame = 0,
                    }));

                Assert.AreEqual(0, refreshCount);
                Assert.AreEqual(1, remountCount);
                Assert.IsTrue(sink.CharacterActionWorkspace.PreviewActive);
            }
            finally
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler = previous;
            }
        }

        [Test]
        public void WorkspaceLayout_InspectorAlignsWithFormalEventSidebar()
        {
            Assert.AreEqual(
                1400f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveWorkspaceInspectorX(1920f, 1400f, 520f));
            Assert.AreEqual(
                2020f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveWorkspaceInspectorX(2560f, 2020f, 540f));
            Assert.AreEqual(
                1400f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .ResolveWorkspaceInspectorX(1920f, -1f, 0f));
        }

        [Test]
        public void WorkspaceLifecycle_DisposedComponentCannotBeReusedAfterPanelRemount()
        {
            var root = new global::FairyGUI.GComponent();
            var workspace = new global::FairyGUI.GComponent();
            root.AddChild(workspace);
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .IsCharacterActionWorkspaceReusableForRoot(workspace, root));

            workspace.Dispose();

            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
                    .IsCharacterActionWorkspaceReusableForRoot(workspace, root));
            root.Dispose();
        }

        [Test]
        public void TrackedLatestWorkspace_LoadsRichBehaviorCardAfterNumericActionIdMigration()
        {
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence();
            Assert.IsTrue(
                persistence.TryLoadLatest(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out string message),
                message);
            Assert.AreEqual(
                "e56af3d4-374b-4ae4-b005-620a5cc8b6dc",
                workspace.WorkspaceId);
            Assert.IsTrue(
                workspace.Cards.TryGetValue(
                    "6146593a-c00a-440d-972e-b8acb5cac574",
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card));
            Assert.AreEqual("靠近并普攻", card.DisplayName);
            Assert.AreEqual(87, card.CurrentRevision);
            int eventCount = 0;
            for (int timelineIndex = 0;
                 timelineIndex < card.LogicPayload.Timelines.Count;
                 timelineIndex++)
            {
                eventCount += card.LogicPayload.Timelines[timelineIndex].Events.Count;
            }
            Assert.AreEqual(10, eventCount);
        }

        [Test]
        public void WorkspaceInspector_UsesSharedTypedSchemaForCombatFields()
        {
            using CharacterActionAuthoringWorkspaceSession session =
                CharacterActionAuthoringWorkspaceSession.Open(CreateCombatGirlOpenRequest());
            CharacterActionAuthoringWorkspaceSnapshot initial = session.BuildSnapshot();
            CharacterActionAuthoringTimelineTrackSnapshot combat = Array.Find(
                initial.Tracks,
                track => track.Kind == "Combat");
            Assert.NotNull(combat);
            Assert.IsNotEmpty(combat.Items);
            CharacterActionAuthoringWorkspaceSnapshot selected = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.SelectTimelineItem,
                    ExpectedRevision = initial.Revision,
                    Text = combat.Items[0].ItemId,
                });

            CharacterActionAuthoringInspectorFieldDescriptor eventKind = FindInspectorField(
                selected,
                ".kind");
            CharacterActionAuthoringInspectorFieldDescriptor equipmentSlot = FindInspectorField(
                selected,
                ".equipmentSlotId");
            CharacterActionAuthoringInspectorFieldDescriptor traceProfile = FindInspectorField(
                selected,
                ".traceProfileId");
            CharacterActionAuthoringInspectorFieldDescriptor targetType = FindInspectorField(
                selected,
                ".targetTypes[0]");
            CharacterActionAuthoringInspectorFieldDescriptor environmentPolicy = FindInspectorField(
                selected,
                ".environmentHitPolicy");
            CharacterActionAuthoringInspectorFieldDescriptor damageType = FindInspectorField(
                selected,
                ".damageTypeId");
            CharacterActionAuthoringInspectorFieldDescriptor formula = FindInspectorField(
                selected,
                ".formulaId");

            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Enum, eventKind.Kind);
            CollectionAssert.Contains(eventKind.Options, "HitTraceWindow");
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Reference, equipmentSlot.Kind);
            CollectionAssert.Contains(equipmentSlot.Options, "mainHand");
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.ReadOnly, traceProfile.Kind);
            Assert.IsFalse(traceProfile.Editable);
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Enum, targetType.Kind);
            CollectionAssert.Contains(targetType.Options, "HostileCharacter");
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Enum, environmentPolicy.Kind);
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Reference, damageType.Kind);
            CollectionAssert.Contains(damageType.Options, "slash");
            Assert.AreEqual(CharacterActionAuthoringInspectorFieldKind.Reference, formula.Kind);
            CollectionAssert.Contains(formula.Options, "ct.soft_defense_v1");
            Assert.AreEqual(formula.Options.Length, formula.OptionLabels.Length);

            CharacterActionAuthoringWorkspaceSnapshot legacyValue = session.Execute(
                new CharacterActionAuthoringWorkspaceCommand
                {
                    Kind = CharacterActionAuthoringWorkspaceCommandKind.SetField,
                    ExpectedRevision = selected.Revision,
                    Text = damageType.Path,
                    SecondaryText = "legacy.custom_damage",
                });
            CharacterActionAuthoringInspectorFieldDescriptor preservedDamageType =
                FindInspectorField(legacyValue, ".damageTypeId");
            int preservedIndex = Array.IndexOf(
                preservedDamageType.Options,
                "legacy.custom_damage");
            Assert.GreaterOrEqual(preservedIndex, 0);
            StringAssert.Contains(
                "保留旧值",
                preservedDamageType.OptionLabels[preservedIndex]);
        }

        private static CharacterActionAuthoringInspectorFieldDescriptor FindInspectorField(
            CharacterActionAuthoringWorkspaceSnapshot snapshot,
            string pathSuffix)
        {
            CharacterActionAuthoringInspectorFieldDescriptor field = Array.Find(
                snapshot.InspectorFields,
                candidate => candidate.Path.EndsWith(pathSuffix, StringComparison.Ordinal));
            Assert.NotNull(field, "Inspector field was not projected: " + pathSuffix);
            return field;
        }

        private static CharacterActionAuthoringTimelineItemSnapshot FindTimelineItem(
            CharacterActionAuthoringWorkspaceSnapshot snapshot,
            string itemId)
        {
            for (int trackIndex = 0; trackIndex < snapshot.Tracks.Length; trackIndex++)
            {
                CharacterActionAuthoringTimelineItemSnapshot found = Array.Find(
                    snapshot.Tracks[trackIndex].Items,
                    item => string.Equals(item.ItemId, itemId, StringComparison.Ordinal));
                if (found != null)
                    return found;
            }
            return null;
        }

        private static CharacterActionAuthoringTimelineItemSnapshot FindFirstResizableTimelineItem(
            CharacterActionAuthoringWorkspaceSnapshot snapshot)
        {
            for (int trackIndex = 0; trackIndex < snapshot.Tracks.Length; trackIndex++)
            {
                CharacterActionAuthoringTimelineItemSnapshot found = Array.Find(
                    snapshot.Tracks[trackIndex].Items,
                    item => item.CanResize);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static CharacterActionAuthoringOpenRequest CreateCombatGirlOpenRequest()
        {
            var request = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                ActionId = new MxFramework.CharacterAction.CharacterActionId(301),
                Context = new CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
                    1,
                    "char.combat_girl_sword",
                    "equip_state.combat_girl_sword.sword_empty",
                    "combat_girl_sword.sword_empty"),
            };
            Assert.IsTrue(
                CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge.TryResolve(
                    request,
                    out CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge.ResolvedRequest resolved,
                    out string error),
                error);
            return new CharacterActionAuthoringOpenRequest
            {
                ProfileSourceDirectory = resolved.ProfileSourceDirectory,
                ActionSetSourceDirectory = resolved.ActionSetSourceDirectory,
                ActionLibrarySourceDirectory = resolved.ActionLibrarySourceDirectory,
                ProfileId = resolved.ProfileId,
                ActionSetStableId = request.Context.ActionSetStableId,
                ActionLibraryId = resolved.ActionLibraryId,
                ActionId = request.ActionId.Value,
                Mode = "focusOnly",
            };
        }
    }
}
