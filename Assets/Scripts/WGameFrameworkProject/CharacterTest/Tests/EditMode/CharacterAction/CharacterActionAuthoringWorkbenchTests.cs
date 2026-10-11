using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Core;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringWorkbenchTests
    {
        private const string LeftWidthKey = "MxFramework.CharacterAction.Workbench.LeftWidth";
        private const string InspectorWidthKey = "MxFramework.CharacterAction.Workbench.InspectorWidth";
        private const string PreviewHeightKey = "MxFramework.CharacterAction.Workbench.PreviewHeight";
        private const string ProblemsHeightKey = "MxFramework.CharacterAction.Workbench.ProblemsHeight";

        [Test]
        public void AttachmentTransformField_UsesUnifiedHumanFriendlyEditor()
        {
            Assert.AreEqual(MxAttachmentMode.FollowSubject, MxAttachmentTransform.Default.Mode);
            Assert.AreEqual(MxTransformOffset.Identity, MxAttachmentTransform.Default.Transform);

            MxAttachmentTransform changed = default;
            var initial = new MxAttachmentTransform(
                MxAttachmentMode.FollowSocket,
                "mainHand",
                MxTransformSpace.Local,
                new MxTransformOffset(positionX: 0.25f));
            var field = new MxAttachmentTransformField(
                initial,
                value => changed = value,
                new[] { "mainHand", "headVfx" },
                socketId => "角色骨架 / RightHand / " + socketId);

            Assert.NotNull(field.Q<Foldout>("mx-attachment-foldout"));
            Assert.NotNull(field.Q<Button>("mx-attachment-reset"));
            Assert.NotNull(field.Q<Button>("mx-attachment-mode-socket"));
            Assert.NotNull(field.Q<Button>("mx-attachment-mode-subject"));
            Assert.NotNull(field.Q<Button>("mx-attachment-mode-world"));
            Assert.NotNull(field.Q<TextField>("mx-attachment-socket"));
            Assert.NotNull(field.Q<Button>("mx-attachment-pick-socket"));
            Assert.NotNull(field.Q<Button>("mx-attachment-reset-transform"));
            Assert.NotNull(field.Q<Button>("mx-attachment-uniform-scale"));
            Assert.NotNull(field.Q<Button>("mx-attachment-locate-preview"));
            Assert.AreEqual(9, field.Query<FloatField>(className: "mx-attachment__axis").ToList().Count);
            StringAssert.Contains(
                "角色骨架 / RightHand / mainHand",
                field.Q<Label>("mx-attachment-socket-status").text);

            PressButton(field.Q<Button>("mx-attachment-mode-world"));
            Assert.AreEqual(MxAttachmentMode.FixedWorld, changed.Mode);
            StringAssert.Contains("固定世界", field.Q<Label>("mx-attachment-preview-status").text);

            PressButton(field.Q<Button>("mx-attachment-mode-socket"));
            FloatField scaleX = field.Q<FloatField>("mx-attachment-缩放-X");
            Assert.NotNull(scaleX);
            MethodInfo setScale = typeof(MxAttachmentTransformField)
                .GetMethod("SetScaleComponent", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(setScale);
            setScale.Invoke(field, new object[] { 0, 2f });
            Assert.AreEqual(2f, changed.Transform.ScaleX);
            Assert.AreEqual(2f, changed.Transform.ScaleY);
            Assert.AreEqual(2f, changed.Transform.ScaleZ);
        }

        [Test]
        public void AttachmentTransformJson_RoundTripsAsNestedFormalData()
        {
            var source = new PresentationTrackEventJsonDto
            {
                Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                VfxAttachment = new MxAttachmentTransform(
                    MxAttachmentMode.FollowSocket,
                    "mainHand",
                    MxTransformSpace.Local,
                    new MxTransformOffset(
                        positionX: 1f,
                        rotationEulerY: 30f,
                        scaleX: 1.5f,
                        scaleY: 1.5f,
                        scaleZ: 1.5f)),
            };

            string json = JsonConvert.SerializeObject(
                source,
                CharacterActionAuthoringJsonService.JsonSettings);
            StringAssert.Contains("\"vfxAttachment\"", json);
            StringAssert.Contains("\"mode\": \"FollowSocket\"", json);
            StringAssert.Contains("\"transform\"", json);
            StringAssert.DoesNotContain("vfxSocketId", json);

            PresentationTrackEventJsonDto restored =
                JsonConvert.DeserializeObject<PresentationTrackEventJsonDto>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
            Assert.AreEqual(source.VfxAttachment, restored.VfxAttachment);

            string audioJson = JsonConvert.SerializeObject(
                new PresentationTrackEventJsonDto
                {
                    Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                    CueId = CharacterTestBulkIds38.Id790031,
                },
                CharacterActionAuthoringJsonService.JsonSettings);
            StringAssert.Contains("\"cueId\": \"sfx.test\"", audioJson);
            StringAssert.DoesNotContain("resourceKey", audioJson);
            StringAssert.DoesNotContain("vfxAttachment", audioJson);
        }

        [Test]
        public void Shell_RepeatedCreateGuiKeepsHostsAndClampsPanePreferences()
        {
            EditorPreferenceSnapshot left = EditorPreferenceSnapshot.Capture(LeftWidthKey);
            EditorPreferenceSnapshot inspector = EditorPreferenceSnapshot.Capture(InspectorWidthKey);
            EditorPreferenceSnapshot preview = EditorPreferenceSnapshot.Capture(PreviewHeightKey);
            EditorPreferenceSnapshot problems = EditorPreferenceSnapshot.Capture(ProblemsHeightKey);
            CharacterActionAuthoringWindow window = null;
            try
            {
                EditorPrefs.SetFloat(LeftWidthKey, -100f);
                EditorPrefs.SetFloat(InspectorWidthKey, 10000f);
                EditorPrefs.SetFloat(PreviewHeightKey, 10000f);
                EditorPrefs.SetFloat(ProblemsHeightKey, 10000f);

                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();

                Assert.AreEqual(new Vector2(1180f, 700f), window.minSize);
                VisualElement workbench = window.rootVisualElement.Q("character-action-workbench");
                VisualElement problemsHost = window.rootVisualElement.Q("character-action-problems");
                Assert.NotNull(workbench);
                Assert.NotNull(problemsHost);
                Assert.NotNull(window.rootVisualElement.Q("character-action-navigation"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-preview"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-timeline-host"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-context-inspector"));
                Button actionMode = window.rootVisualElement.Q<Button>("character-action-mode-action");
                Button abilityMode = window.rootVisualElement.Q<Button>("character-action-mode-ability");
                Assert.NotNull(actionMode);
                Assert.NotNull(abilityMode);
                Button hotReload = window.rootVisualElement.Q<Button>("character-action-hot-reload");
                Assert.NotNull(hotReload);
                Assert.AreEqual("热重载", hotReload.text);
                Assert.IsEmpty(window.rootVisualElement.Query<Label>(className: "ca-validation-badge").ToList());
                Assert.IsFalse(actionMode.enabledSelf);
                Assert.IsTrue(abilityMode.enabledSelf);
                Assert.AreEqual(
                    0,
                    window.rootVisualElement.Query<Label>().ToList().FindAll(
                        label => string.Equals(label.text, "当前选中项", StringComparison.Ordinal)).Count);
                VisualElement timelineViewport = window.rootVisualElement.Q(className: "ca-tl-viewport");
                VisualElement timelineBody = window.rootVisualElement.Q(className: "ca-tl-body-scroll");
                Assert.NotNull(timelineViewport);
                Assert.NotNull(timelineBody);
                Assert.Less(timelineViewport.parent.IndexOf(timelineViewport), timelineBody.parent.IndexOf(timelineBody));

                TwoPaneSplitView leftSplit = window.rootVisualElement.Q<TwoPaneSplitView>(
                    "character-action-left-split");
                TwoPaneSplitView inspectorSplit = window.rootVisualElement.Q<TwoPaneSplitView>(
                    "character-action-inspector-split");
                TwoPaneSplitView previewSplit = window.rootVisualElement.Q<TwoPaneSplitView>(
                    "character-action-preview-timeline-split");
                Assert.AreEqual(220f, leftSplit.fixedPaneInitialDimension, 0.01f);
                Assert.AreEqual(380f, inspectorSplit.fixedPaneInitialDimension, 0.01f);
                Assert.AreEqual(340f, previewSplit.fixedPaneInitialDimension, 0.01f);

                MethodInfo clampProblems = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "ClampProblemsDrawerHeight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(clampProblems);
                Assert.AreEqual(190f, (float)clampProblems.Invoke(window, new object[] { 10000f }), 0.01f);

                window.CreateGUI();

                Assert.AreSame(workbench, window.rootVisualElement.Q("character-action-workbench"));
                Assert.AreSame(problemsHost, window.rootVisualElement.Q("character-action-problems"));
                Assert.AreEqual(
                    1,
                    window.rootVisualElement.Query<VisualElement>(name: "character-action-workbench").ToList().Count);
                Assert.AreEqual(
                    1,
                    window.rootVisualElement.Query<VisualElement>(name: "character-action-problems").ToList().Count);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                left.Restore();
                inspector.Restore();
                preview.Restore();
                problems.Restore();
            }
        }

        [Test]
        public void PrimaryModeSwitch_ChangesBetweenActionAndAbilityTimelineEditing()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();

                Button abilityMode = window.rootVisualElement.Q<Button>("character-action-mode-ability");
                Assert.NotNull(abilityMode);
                Invoke(window, "OpenAbilityEffectTimelineWorkspace");

                Assert.AreEqual(
                    DisplayStyle.Flex,
                    window.rootVisualElement.Q("character-action-ability-workspace").style.display.value);
                Assert.AreEqual(
                    DisplayStyle.None,
                    window.rootVisualElement.Q("character-action-shared-workspace").style.display.value);
                Assert.NotNull(window.rootVisualElement.Q("character-action-ability-navigation"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-ability-preview-viewport"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-ability-timeline-host"));
                Assert.NotNull(window.rootVisualElement.Q("character-action-ability-inspector"));
                Assert.NotNull(GetField<CharacterActionTimelineView>(window, "_abilityEffectTimelineView"));
                Assert.IsFalse(
                    window.rootVisualElement.Q<Button>("character-action-mode-ability").enabledSelf);
                Assert.IsTrue(
                    window.rootVisualElement.Q<Button>("character-action-mode-action").enabledSelf);

                CharacterActionPreviewViewport abilityPreview =
                    GetField<CharacterActionPreviewViewport>(window, "_abilityPreviewViewport");
                Assert.AreEqual(CharacterActionPreviewSurfaceMode.Embedded, abilityPreview.SurfaceMode);
                Invoke(window, "EnterSceneEditMode");
                Assert.AreEqual(CharacterActionPreviewSurfaceMode.SceneView, abilityPreview.SurfaceMode);
                Invoke(window, "ExitSceneEditMode", true);
                Assert.AreEqual(CharacterActionPreviewSurfaceMode.Embedded, abilityPreview.SurfaceMode);

                Button actionMode = window.rootVisualElement.Q<Button>("character-action-mode-action");
                Assert.NotNull(actionMode);
                Invoke(window, "OpenActionCompositionWorkspace");

                Assert.AreEqual(
                    DisplayStyle.Flex,
                    window.rootVisualElement.Q("character-action-composition-workspace").style.display.value);
                Assert.IsFalse(
                    window.rootVisualElement.Q<Button>("character-action-mode-action").enabledSelf);
                Assert.IsTrue(
                    window.rootVisualElement.Q<Button>("character-action-mode-ability").enabledSelf);
            }
            finally
            {
                if (window != null)
                {
                    SetField(window, "_savingSession", true);
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [Test]
        public void ActionComposition_PreviewAndReentryNormalizeStaleAbilityMode()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = CreateWindowWithSingleAction(out CharacterActionAuthoringModel model);
                SetEnumField(window, "_editorMode", "AbilityEffectTimeline");

                Invoke(window, "UpdateTimelineRuntimePreview", 0);

                Assert.AreEqual("CharacterAction", GetField<object>(window, "_editorMode").ToString());
                Assert.NotNull(model.SelectedAction);
                CharacterActionPreviewViewport viewport =
                    GetField<CharacterActionPreviewViewport>(window, "_previewViewport");
                Assert.NotNull(viewport);
                Assert.AreNotEqual("选择一个动作以开始预览。", viewport.DiagnosticsText);

                SetEnumField(window, "_editorMode", "AbilityEffectTimeline");
                Invoke(window, "OpenActionCompositionWorkspace");
                Assert.AreEqual("CharacterAction", GetField<object>(window, "_editorMode").ToString());
            }
            finally
            {
                if (window != null)
                {
                    SetField(window, "_savingSession", true);
                    UnityEngine.Object.DestroyImmediate(window);
                }
                CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
            }
        }

        [Test]
        public void Shell_PlayModeBoundaryRebuildsDisposedEmbeddedPreview()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                VisualElement initialPreview = window.rootVisualElement.Q(
                    "character-action-preview-viewport");
                Assert.NotNull(initialPreview);

                MethodInfo playModeChanged = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "OnPlayModeStateChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo shellBuilt = typeof(CharacterActionAuthoringWindow).GetField(
                    "_shellBuilt",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(playModeChanged);
                Assert.NotNull(shellBuilt);

                playModeChanged.Invoke(window, new object[] { PlayModeStateChange.ExitingEditMode });
                Assert.IsFalse((bool)shellBuilt.GetValue(window));

                window.CreateGUI();
                VisualElement restoredPreview = window.rootVisualElement.Q(
                    "character-action-preview-viewport");
                Assert.NotNull(restoredPreview);
                Assert.AreNotSame(initialPreview, restoredPreview);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ActionEdits_RefreshProjectedRegionsWithoutRebuildingShell()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = CreateWindowWithSingleAction(out CharacterActionAuthoringModel model);
                VisualElement shell = window.rootVisualElement.Q("character-action-workbench");
                CharacterActionTimelineView timeline = GetField<CharacterActionTimelineView>(window, "_timelineView");
                Assert.NotNull(shell);
                Assert.NotNull(timeline);
                Assert.AreEqual(8, timeline.Snapshot.DurationFrames);

                Invoke(
                    window,
                    "ApplyFieldChange",
                    new Action(() => model.SelectedAction.DurationFrames = 17),
                    true);

                Assert.AreEqual(17, timeline.Snapshot.DurationFrames);
                Assert.AreSame(shell, window.rootVisualElement.Q("character-action-workbench"));
                Assert.AreEqual(1, GetCollectionCount(window, "_actionNavigationEntries"));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RecentEdits_FilterTracksMutationsOrdersFlatListAndHandlesRenameDeleteAndSearch()
        {
            string preferenceKey =
                "MxFramework.Tests.CharacterAction.RecentEdits." + Guid.NewGuid();
            CharacterActionAuthoringWindow window = null;
            try
            {
                var history = new CharacterActionRecentEditHistory(preferenceKey);
                window = CreateWindowWithSingleAction(
                    out CharacterActionAuthoringModel model,
                    history);
                Button recentButton = window.rootVisualElement.Q<Button>(
                    "character-action-filter-recent");
                Assert.NotNull(recentButton);
                Assert.AreEqual("最近编辑", recentButton.text);

                Assert.AreEqual(0, history.Entries.Count, "Opening an action must not count as an edit.");

                Invoke(
                    window,
                    "ApplyFieldChange",
                    new Action(() => model.SelectedAction.DurationFrames = 17),
                    true);
                int originalActionId = model.SelectedAction.ActionId;
                Assert.AreEqual(1, history.Entries.Count);
                Assert.AreEqual(originalActionId, history.Entries[0].ActionId);

                Invoke(window, "DuplicateSelectedActionFromWorkbench");
                int duplicateActionId = model.SelectedAction.ActionId;
                Assert.AreEqual(2, history.Entries.Count);
                Assert.AreEqual(duplicateActionId, history.Entries[0].ActionId);

                SetField(window, "_actionCategoryFilter", "最近编辑");
                Invoke(window, "RebuildWorkbenchActionNavigation");
                IList navigation = GetField<IList>(window, "_actionNavigationEntries");
                Assert.AreEqual(2, navigation.Count);
                Assert.AreEqual(
                    duplicateActionId,
                    GetNavigationAction(navigation[0]).ActionId,
                    "The newest edited action must be the first flat recent row.");
                Assert.AreEqual(
                    originalActionId,
                    GetNavigationAction(navigation[1]).ActionId,
                    "The earlier edited action must remain as the second flat recent row.");

                SetField(window, "_actionListFilter", "does-not-match");
                Invoke(window, "RebuildWorkbenchActionNavigation");
                Assert.AreEqual(0, navigation.Count);
                Assert.AreEqual(
                    DisplayStyle.Flex,
                    window.rootVisualElement.Q<Label>(
                        "character-action-recent-empty").style.display.value);
                Assert.AreEqual(2, history.Entries.Count);

                SetField(window, "_actionListFilter", string.Empty);
                int deletedActionId = model.SelectedAction.ActionId;
                int expectedRemainingActionId = deletedActionId == duplicateActionId
                    ? originalActionId
                    : duplicateActionId;
                Invoke(window, "DeleteSelectedActionFromWorkbench", false);
                Assert.AreEqual(1, history.Entries.Count);
                Assert.AreEqual(
                    expectedRemainingActionId,
                    history.Entries[0].ActionId,
                    "Deleting an action must remove it without touching the fallback selection.");

                Invoke(
                    window,
                    "ApplyFieldChange",
                    new Action(() => model.SelectedAction.DisplayName = "Renamed Action"),
                    true);
                Assert.AreEqual(1, history.Entries.Count);
                Assert.AreEqual(expectedRemainingActionId, history.Entries[0].ActionId);

                SetField(window, "_actionCategoryFilter", "全部");
                Invoke(window, "RebuildWorkbenchActionNavigation");
                Assert.AreEqual(1, history.Entries.Count);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                EditorPrefs.DeleteKey(preferenceKey);
            }
        }

        [Test]
        public void RecentEdits_BrowsingFromNavigationAndTimelinePickerRecordsAndReordersActions()
        {
            string preferenceKey =
                "MxFramework.Tests.CharacterAction.RecentBrowsing." + Guid.NewGuid();
            CharacterActionAuthoringWindow window = null;
            try
            {
                var history = new CharacterActionRecentEditHistory(preferenceKey);
                window = CreateWindowWithSingleAction(
                    out CharacterActionAuthoringModel model,
                    history);
                int firstActionId = model.SelectedAction.ActionId;
                model.Actions.Add(CharacterActionAuthoringModel.CreateBlankAction(
                    2,
                    "test.browsed"));
                InvokeEnum(window, "RefreshUi", "All");

                Invoke(window, "SelectActionWithoutDocumentEdit", 1);

                Assert.AreEqual(1, history.Entries.Count);
                Assert.AreEqual(2, history.Entries[0].ActionId);

                Invoke(window, "SelectCharacterActionTimelinePickerItem", firstActionId.ToString());

                Assert.AreEqual(2, history.Entries.Count);
                Assert.AreEqual(firstActionId, history.Entries[0].ActionId);
                Assert.AreEqual(2, history.Entries[1].ActionId);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                EditorPrefs.DeleteKey(preferenceKey);
            }
        }

        [Test]
        public void ContextInspector_SwitchesBetweenActionAndEditableTimelineItem()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = CreateWindowWithSingleAction(out CharacterActionAuthoringModel model);
                VisualElement actionInspector = GetField<VisualElement>(window, "_actionInspectorHeaderHost");
                VisualElement timelineInspector = GetField<VisualElement>(window, "_timelineInspectorHost");
                CharacterActionTimelineView timeline = GetField<CharacterActionTimelineView>(window, "_timelineView");

                Assert.AreEqual(DisplayStyle.Flex, actionInspector.style.display.value);
                Assert.AreEqual(DisplayStyle.None, timelineInspector.style.display.value);
                Assert.Greater(actionInspector.Query<TextField>().ToList().Count, 0);

                // UI Toolkit elements can survive an assembly reload while their delegate host does not.
                timeline.SetHost(null);
                SetField(window, "_timelineHostConfigured", true);
                Invoke(window, "EnsureTimelineView");
                Assert.IsTrue(timeline.HasHost);

                ITimelineItemAdapter phase = timeline.Snapshot.ItemsByTrack[TimelineTrackKind.Phases][0];
                timeline.SelectItem(phase.Id);

                Assert.AreEqual(DisplayStyle.None, actionInspector.style.display.value);
                Assert.AreEqual(DisplayStyle.Flex, timelineInspector.style.display.value);
                List<IntegerField> itemFields = timelineInspector.Query<IntegerField>().ToList();
                Assert.GreaterOrEqual(itemFields.Count, 2);
                Assert.IsTrue(itemFields[1].enabledSelf);
                List<PopupField<string>> itemPopups = timelineInspector.Query<PopupField<string>>().ToList();
                Assert.Greater(itemPopups.Count, 0);

                itemPopups[0].value = itemPopups[0].choices[1];

                Assert.AreEqual(
                    Enum.GetNames(typeof(CharacterActionPhaseKind))[1],
                    model.SelectedAction.Phases[0].Kind);

                timeline.SelectItem(string.Empty);

                Assert.AreEqual(DisplayStyle.Flex, actionInspector.style.display.value);
                Assert.AreEqual(DisplayStyle.None, timelineInspector.style.display.value);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HitWindowInspector_UsesCompactTargetsDamagePreviewAndConditionalEnvironmentFields()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = CreateWindowWithSingleAction(out CharacterActionAuthoringModel model);
                CharacterActionJsonDto action = model.SelectedAction;
                action.DurationFrames = 40;
                action.CombatTrack = new CombatTrackJsonDto
                {
                    Events = new[]
                    {
                        new CombatTrackEventJsonDto
                        {
                            Frame = 24,
                            EndFrame = 30,
                            Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                            EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                            StableEventId = CharacterTestBulkIds38.Id790032,
                            HitEvent = new CharacterActionHitEventJsonDto
                            {
                                TargetTypes = new[] { CombatHitTargetTypes.HostileCharacter.ToString() },
                                EnvironmentHitPolicy = CombatEnvironmentHitPolicy.None.ToString(),
                                MaxAcceptedHits = 1,
                                HitSameTargetPolicy = CombatHitSameTargetPolicy.OncePerEvent.ToString(),
                                Damage = new CharacterActionHitDamageJsonDto
                                {
                                    DamageTypeId = CharacterTestBulkIds38.Id790006,
                                    BaseDamage = 6,
                                    SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(0.65f),
                                    ImpactPower = 34,
                                },
                            },
                        },
                    },
                };
                InvokeEnum(window, "RefreshUi", "All");

                CharacterActionTimelineView timeline = GetField<CharacterActionTimelineView>(window, "_timelineView");
                ITimelineItemAdapter adapter = timeline.Snapshot.ItemsByTrack[TimelineTrackKind.Combat][0];
                Assert.IsInstanceOf<HitTraceWindowTimelineAdapter>(adapter);
                timeline.SelectItem(adapter.Id);

                VisualElement inspector = GetField<VisualElement>(window, "_timelineInspectorHost");
                Assert.NotNull(inspector.Q("character-action-hit-window"));
                Assert.NotNull(inspector.Q<IntegerField>("hit-window-start-frame"));
                Assert.NotNull(inspector.Q<IntegerField>("hit-window-end-frame"));
                Assert.NotNull(inspector.Q("character-action-hit-volume"));
                Assert.NotNull(inspector.Q("character-action-hit-targets"));
                VisualElement damageResult = inspector.Q("character-action-damage-result");
                Assert.NotNull(damageResult);
                Assert.AreEqual("19 HP", inspector.Q<Label>("character-action-estimated-damage").text);
                Assert.AreEqual(0, damageResult.Query<HelpBox>().ToList().Count);
                Assert.IsFalse(inspector.Q<Foldout>("character-action-damage-formula").value);
                Assert.IsFalse(inspector.Q<Foldout>("character-action-hit-advanced").value);

                List<Label> allLabels = inspector.Query<Label>().ToList();
                Assert.IsTrue(allLabels.Exists(label => label.text.Contains("主手 · 斩击 · 24–30 帧 · 仅敌方 · 单次命中")));

                Button environmentPreset = inspector.Query<Button>().ToList().Find(button => button.text == "环境交互");
                Assert.NotNull(environmentPreset);
                PressButton(environmentPreset);

                CombatHitTargetTypes targets = CharacterActionHitEventAuthoringModel.ParseTargetTypes(
                    action.CombatTrack.Events[0].HitEvent.TargetTypes);
                Assert.AreEqual(
                    CombatHitTargetTypes.DamageableObject
                        | CombatHitTargetTypes.WorldWall
                        | CombatHitTargetTypes.WorldGround
                        | CombatHitTargetTypes.WorldStatic,
                    targets);
                inspector = GetField<VisualElement>(window, "_timelineInspectorHost");
                Assert.IsTrue(inspector.Query<PopupField<string>>().ToList().Exists(field => field.label == "环境命中行为"));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ActionCollectionChanges_KeepNavigationTimelineAndEmptySelectionInSync()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = CreateWindowWithSingleAction(out CharacterActionAuthoringModel model);
                CharacterActionTimelineView timeline = GetField<CharacterActionTimelineView>(window, "_timelineView");

                Invoke(window, "DuplicateSelectedActionFromWorkbench");

                Assert.AreEqual(2, model.Actions.Count);
                Assert.AreEqual(model.SelectedAction.ActionId, timeline.Snapshot.Action.ActionId);
                Assert.AreEqual(2, GetCollectionCount(window, "_actionNavigationEntries"));

                Invoke(window, "DeleteSelectedActionFromWorkbench", false);

                Assert.AreEqual(1, model.Actions.Count);
                Assert.AreEqual(model.SelectedAction.ActionId, timeline.Snapshot.Action.ActionId);
                Assert.AreEqual(1, GetCollectionCount(window, "_actionNavigationEntries"));

                Invoke(window, "DeleteSelectedActionFromWorkbench", false);

                Assert.AreEqual(0, model.Actions.Count);
                Assert.IsNull(model.SelectedAction);
                Assert.LessOrEqual(timeline.Snapshot.Action.ActionId, 0);
                Assert.AreEqual(0, GetCollectionCount(window, "_actionNavigationEntries"));
                Assert.IsFalse(GetField<CharacterActionTimelinePlaybackController>(window, "_timelinePlayback").IsPlaying);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ProfileComposition_UsesSchemeAndLibraryCardsFromDocumentGraph()
        {
            CharacterActionAuthoringDocumentSession session = CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                    "profile-display-name-does-not-matter"),
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                        "action-set-display-name-does-not-matter"),
                },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test",
                        "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common",
                        "Common"),
                });
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                SetField(window, "_documentSession", session);
                SetField(window, "_actionLibrarySources", Array.Empty<CharacterActionAuthoringSourceEntry>());
                SetField(window, "_model", CreateModelFromSession(session));
                var schemes = new VisualElement();
                var details = new VisualElement();

                Invoke(window, "BuildProfileTab", schemes, details);

                Assert.NotNull(schemes.Q<VisualElement>(
                    "character-action-profile-action-set-codex_flow_test.empty_empty"), "scheme card");
                Assert.NotNull(schemes.Q<Button>("character-action-profile-add-action-set"), "add scheme button");
                Assert.NotNull(schemes.Q<Label>("character-action-profile-current-scheme"), "current scheme badge");
                Assert.AreEqual(
                    "方案基础设置",
                    schemes.Q<Button>("character-action-profile-edit-action-set")?.text);
                StringAssert.StartsWith(
                    "文档会话 · ",
                    schemes.Q<Label>("character-action-profile-health")?.text);
                Assert.NotNull(details.Q<VisualElement>(
                    "character-action-profile-library-codex_flow_test"), "role library card");
                Assert.NotNull(details.Q<VisualElement>(
                    "character-action-profile-library-Common"), "common library card");
                Assert.AreEqual(2, details.Query<Button>(
                    name: "character-action-profile-browse-library").ToList().Count);
                Assert.NotNull(details.Q<Foldout>("profile-technical-information"), "technical foldout");
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ProfileComposition_SelectingSchemeAndNavigationDoNotDirtyDocuments()
        {
            CharacterActionAuthoringDocumentSession session = CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                    "codex_flow_test"),
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                        "codex_flow_test"),
                },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test",
                        "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common",
                        "Common"),
                });
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                SetField(window, "_documentSession", session);
                SetField(window, "_model", CreateModelFromSession(session));
                SetEnumField(window, "_workspace", "Mappings");
                SetEnumField(window, "_mappingSection", "ProfileComposition");
                InvokeEnum(window, "RefreshUi", "All");

                VisualElement schemes = GetField<VisualElement>(window, "_mappingMainHost");
                Assert.NotNull(window.rootVisualElement.Q<Label>(
                    "character-action-toolbar-current-action-set"));
                Assert.IsNull(window.rootVisualElement.Q<ToolbarMenu>(
                    "character-action-toolbar-action-set-selector"));
                Assert.NotNull(window.rootVisualElement.Q<Label>(
                    "character-action-toolbar-binding-scope"));
                ToolbarMenu characterBinding = window.rootVisualElement.Q<ToolbarMenu>(
                    "character-action-toolbar-binding-Character");
                Assert.NotNull(characterBinding);
                Assert.AreEqual("角色  char.codex_flow_test", characterBinding.text);
                ToolbarMenu equipmentBinding = window.rootVisualElement.Q<ToolbarMenu>(
                    "character-action-toolbar-binding-EquipmentState");
                Assert.NotNull(equipmentBinding);
                Assert.AreEqual(
                    "装备  equip_state.codex_flow_test.empty_empty",
                    equipmentBinding.text);
                StringAssert.DoesNotContain("equip_loadout", equipmentBinding.text);

                VisualElement swordCard = schemes.Q<VisualElement>(
                    "character-action-profile-action-set-codex_flow_test.sword_empty");
                Assert.NotNull(swordCard);
                PressButton(swordCard.Q<Button>("character-action-profile-select-action-set"));

                Assert.AreEqual("codex_flow_test.sword_empty", session.Selection.ActionSetStableId);
                for (int i = 0; i < session.DocumentStates.Count; i++)
                    Assert.IsFalse(session.DocumentStates[i].IsDirty, "document index " + i);

                Invoke(window, "OpenProfileCommandBindings");
                Assert.AreEqual("CommandBindings", GetField<object>(window, "_mappingSection").ToString());
                Invoke(window, "OpenProfileAbilityBindings");
                Assert.AreEqual("AbilityBindings", GetField<object>(window, "_mappingSection").ToString());
                Invoke(window, "OpenProfileReactions");
                Assert.AreEqual("Reactions", GetField<object>(window, "_mappingSection").ToString());
                Invoke(window, "OpenProfileProblems");
                Assert.IsTrue(GetField<bool>(window, "_problemsExpanded"));
                Assert.AreEqual(string.Empty, GetField<string>(window, "_problemsQuery"));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RuntimePreview_CommonLibraryActionUsesCompleteProfileDocumentGraph()
        {
            CharacterActionAuthoringDocumentSession session = CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                    "codex_flow_test"),
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                        "codex_flow_test"),
                },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test",
                        "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(
                        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common",
                        "Common"),
                });
            Assert.IsTrue(session.SelectActionSet("codex_flow_test.empty_empty"));
            Assert.IsTrue(session.SelectAction(300));
            CharacterActionAuthoringModel model = CreateModelFromSession(session);
            CharacterActionAuthoringExportResult singleLibrary = CharacterActionAuthoringJsonService.Validate(
                model,
                model.ReferenceContext,
                buildWorkstationSnapshots: false);
            Assert.IsFalse(singleLibrary.ImportResult.Success);
            StringAssert.Contains("ACT_MISSING_ACTION_CONFIG", singleLibrary.DiagnosticsText);

            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                SetField(window, "_documentSession", session);
                SetField(window, "_model", model);
                SetField(window, "_previewVisible", true);
                SetField(window, "_workbenchPreviewRevision", 41L);

                Invoke(window, "UpdateTimelineRuntimePreview", 0);

                CharacterActionPreviewViewport viewport =
                    GetField<CharacterActionPreviewViewport>(window, "_previewViewport");
                CharacterActionPreviewRuntimeSession runtime = viewport.Session;
                Assert.NotNull(runtime.PreviewInstance, runtime.DiagnosticsText);
                StringAssert.DoesNotContain("ACT_MISSING_ACTION_CONFIG", runtime.DiagnosticsText);
                int validationCount = runtime.ContextValidationCount;

                Invoke(window, "UpdateTimelineRuntimePreview", 1);

                Assert.AreEqual(
                    validationCount,
                    runtime.ContextValidationCount,
                    "Unchanged preview frames must reuse the complete document-graph validation context.");
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
            }
        }

        private static CharacterActionAuthoringWindow CreateWindowWithSingleAction(
            out CharacterActionAuthoringModel model,
            CharacterActionRecentEditHistory recentActionHistory = null)
        {
            var window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            window.position = new Rect(0f, 0f, 1180f, 700f);
            if (recentActionHistory != null)
                SetField(window, "_recentActionHistory", recentActionHistory);
            window.CreateGUI();
            model = new CharacterActionAuthoringModel
            {
                Profile = new CharacterActionProfileJsonDto
                {
                    StableId = "test.profile",
                    ActionSetIds = new[] { "test.set" },
                    ActionLibraryIds = new[] { "test.library" },
                },
                ActionSet = new CharacterActionSetJsonDto { StableId = "test.set" },
            };
            model.Actions.Add(CharacterActionAuthoringModel.CreateBlankAction(1, "test.action"));
            model.Actions[0].DurationFrames = 8;
            model.SelectedActionIndex = 0;
            SetField(window, "_documentSession", null);
            SetField(window, "_model", model);
            SetEnumField(window, "_workspace", "ActionComposition");
            SetEnumField(window, "_tab", "Tracks");
            SetField(window, "_actionCategoryFilter", "全部");
            InvokeEnum(window, "RefreshUi", "All");
            return window;
        }

        private static CharacterActionAuthoringModel CreateModelFromSession(
            CharacterActionAuthoringDocumentSession session)
        {
            var model = new CharacterActionAuthoringModel
            {
                Profile = session.CurrentProfile,
                ActionSet = session.CurrentActionSet,
            };
            model.Actions.AddRange(session.CurrentActionLibrary.Document.Actions);
            model.SelectedActionIndex = 0;
            return model;
        }

        private static bool ContainsText(IList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if ((values[i] ?? string.Empty).IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static int GetCollectionCount(object instance, string fieldName)
        {
            return ((ICollection)GetField<object>(instance, fieldName)).Count;
        }

        private static CharacterActionJsonDto GetNavigationAction(object entry)
        {
            FieldInfo field = entry.GetType().GetField(
                "Action",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (CharacterActionJsonDto)field.GetValue(entry);
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private static void SetEnumField(object instance, string fieldName, string enumValue)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, Enum.Parse(field.FieldType, enumValue));
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object InvokeEnum(object instance, string methodName, string enumValue)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            Type enumType = method.GetParameters()[0].ParameterType;
            return method.Invoke(instance, new[] { Enum.Parse(enumType, enumValue) });
        }

        private static void PressButton(Button button)
        {
            Assert.NotNull(button);
            object clickable = typeof(Button)
                .GetProperty("clickable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.NotNull(clickable);

            MethodInfo[] methods = clickable.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo invoke = null;
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, "Invoke", StringComparison.Ordinal))
                    continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 0
                    || (parameters.Length == 1 && typeof(EventBase).IsAssignableFrom(parameters[0].ParameterType)))
                {
                    invoke = methods[i];
                    break;
                }
            }

            Assert.NotNull(invoke);
            if (invoke.GetParameters().Length == 0)
            {
                invoke.Invoke(clickable, Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
                invoke.Invoke(clickable, new object[] { click });
        }

        private readonly struct EditorPreferenceSnapshot
        {
            private readonly string _key;
            private readonly bool _exists;
            private readonly float _value;

            private EditorPreferenceSnapshot(string key, bool exists, float value)
            {
                _key = key;
                _exists = exists;
                _value = value;
            }

            public static EditorPreferenceSnapshot Capture(string key)
            {
                bool exists = EditorPrefs.HasKey(key);
                return new EditorPreferenceSnapshot(key, exists, exists ? EditorPrefs.GetFloat(key) : 0f);
            }

            public void Restore()
            {
                if (_exists)
                    EditorPrefs.SetFloat(_key, _value);
                else
                    EditorPrefs.DeleteKey(_key);
            }
        }
    }
}
