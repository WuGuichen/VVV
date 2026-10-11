using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    [InitializeOnLoad]
    internal static class TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow
    {
        private const string SessionKey =
            "MxFramework.TriggerTests.RuntimeAiLabCharacterActionAuthoring.v1";
        private const double TimeoutSeconds = 120d;
        private static Fgui.GComponent _visualEvidenceInspector;
        private static CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
            _visualEvidenceBinder;
        private static BehaviorRecipeTriggerEventInspectorViewModel _visualEvidenceModel;

        static TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        internal static TriggerTestRuntimeAiLabPlayModeSnapshot CaptureSnapshot()
        {
            State state = Load();
            double elapsed = state.StartedAt <= 0d
                ? 0d
                : Math.Max(0d, (state.CompletedAt > 0d ? state.CompletedAt : EditorApplication.timeSinceStartup) - state.StartedAt);
            return new TriggerTestRuntimeAiLabPlayModeSnapshot(
                (TriggerTestEditorWorkflowStatus)state.Status,
                state.Message,
                elapsed);
        }

        internal static bool TryBegin(out string message)
        {
            if (!TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.TryBegin(out message))
                return false;
            Save(new State
            {
                Status = (int)TriggerTestEditorWorkflowStatus.Running,
                Message = "正在打开运行中的行为逻辑，随后验证完整动作编排。",
                StartedAt = EditorApplication.timeSinceStartup,
            });
            return true;
        }

        internal static bool ValidateScheduledState(out string message)
        {
            State state = Load();
            bool valid = state.Status == (int)TriggerTestEditorWorkflowStatus.Running;
            message = valid ? "完整动作编排 Trigger 已进入跨 Domain Reload 等待状态。" : state.Message;
            return valid;
        }

        internal static void ClearForTests()
        {
            SessionState.EraseString(SessionKey);
        }

        private static void Tick()
        {
            State state = Load();
            if (state.Status != (int)TriggerTestEditorWorkflowStatus.Running)
                return;
            if (EditorApplication.timeSinceStartup - state.StartedAt > TimeoutSeconds)
            {
                Fail(state, "完整动作编排 Trigger 超时：" + state.Message);
                return;
            }
            TriggerTestRuntimeAiLabPlayModeSnapshot prerequisite =
                TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.CaptureSnapshot();
            if (prerequisite.Status == TriggerTestEditorWorkflowStatus.Failed)
            {
                Fail(state, prerequisite.Message);
                return;
            }
            if (prerequisite.Status != TriggerTestEditorWorkflowStatus.Passed || !EditorApplication.isPlaying)
                return;
            RunExternalVerification(state);
        }

        private static void RunExternalVerification(State state)
        {
            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            if (controller == null || host?.CommandSink == null)
                return;
            if (state.Phase == 1)
            {
                CompletePreviewWorkspaceVerification(state, controller, host);
                return;
            }
            if (state.Phase == 2)
            {
                CompleteStoppedWorkspaceVerification(state, host);
                return;
            }
            if (!controller.TryGetSelectedCharacterActionAuthoringContext(
                    out CharacterTestRuntimeAiLabCharacterActionAuthoringContext context,
                    out string contextDiagnostic))
            {
                Fail(state, contextDiagnostic);
                return;
            }
            if (!VerifyPublishedInspectorButton(out string buttonDiagnostic))
            {
                Fail(state, buttonDiagnostic);
                return;
            }

            var openRequest = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                Operation = CharacterTestRuntimeAiLabCharacterActionAuthoringOperation.OpenEmbedded,
                ActionId = new MxFramework.CharacterAction.CharacterActionId(301),
                Context = context,
                SelectedAgentIndex = controller.SelectedAgentIndex,
                TimelineMode = CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
            };
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot workspace =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Open,
                        OpenRequest = openRequest,
                    });
            if (!workspace.Available
                || workspace.Tracks.Length != 7
                || workspace.Actions.Length == 0
                || workspace.InspectorFields.Length == 0)
            {
                Fail(state, "共享动作编排会话未投影完整动作列表、七轨或 Inspector：" + workspace.Status);
                return;
            }

            Fgui.GComponent component = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabCharacterActionWorkspace")?.asCom;
            bool componentValid = component != null
                && component.GetChild("actionList")?.asList != null
                && component.GetChild("trackList")?.asList != null
                && component.GetChild("inspectorList")?.asList != null
                && component.GetChild("problemsList")?.asList != null
                && component.GetChild("btnPreviewPlay")?.asButton != null;
            component?.Dispose();
            if (!componentValid)
            {
                Fail(state, "正式 FairyGUI 包无法创建完整动作编排工作区。");
                return;
            }

            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField display =
                Array.Find(workspace.InspectorFields, field => field.Path == "displayName");
            if (display == null)
            {
                Fail(state, "动作 Inspector 缺少 displayName 字段。");
                return;
            }
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot changed =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Execute,
                        Command = new CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
                        {
                            Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SetField,
                            ExpectedRevision = workspace.Revision,
                            Text = "displayName",
                            SecondaryText = "Trigger Temporary Action Name",
                        },
                    });
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot undone =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Execute,
                        Command = new CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
                        {
                            Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.Undo,
                            ExpectedRevision = changed.Revision,
                        },
                    });
            string restored = Array.Find(undone.InspectorFields, field => field.Path == "displayName")?.Value;
            if (!string.Equals(restored, display.Value, StringComparison.Ordinal))
            {
                Fail(state, "共享动作编排 Undo 未恢复原动作字段。" );
                return;
            }

            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot preview =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPause,
                        PreviewFrame = 0,
                    });
            string previewDiagnostic = string.Empty;
            if (string.IsNullOrWhiteSpace(preview.PreviewSourceDirectory)
                || !Directory.Exists(preview.PreviewSourceDirectory)
                || !controller.TryStartCharacterActionWorkspacePreview(
                    new MxFramework.CharacterAction.CharacterActionId(preview.SelectedActionId),
                    preview.PreviewSourceDirectory,
                    play: false,
                    targetFrame: 0,
                    out previewDiagnostic))
            {
                Fail(state, "当前测试角色草稿预览失败：" + previewDiagnostic);
                return;
            }
            controller.TryStopCharacterActionWorkspacePreview(out _);
            CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                {
                    Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close,
                });
            controller.ResetSession();
            if (!controller.StartSessionThroughShell(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic))
            {
                Fail(state, "草稿预览结束后未能从第 0 帧重新开始测试。" );
                return;
            }
            if (!OpenPublishedVisualEvidence(controller, host, out string visualDiagnostic))
            {
                Fail(state, visualDiagnostic);
                return;
            }
            state.Phase = 1;
            state.Message = "已启动草稿动作播放；等待下一 Editor Tick 验证工作区重挂载与停止。";
            Save(state);
        }

        private static void CompletePreviewWorkspaceVerification(
            State state,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host)
        {
            Fgui.GComponent page = host?.RootForTests;
            Fgui.GComponent timelineWorkspace = page?.GetChild("runtimeFrameWorkspace")?.asCom;
            Fgui.GComponent currentInspector = timelineWorkspace?.GetChild("behaviorRecipeInspector")?.asCom;
            if (_visualEvidenceModel == null)
            {
                Fail(state, "AI Lab 页面重绑后，无法取得当前事件 Inspector 以恢复 CharacterAction 工作区。");
                return;
            }
            if (currentInspector == null)
            {
                state.Message = "测试重启后正在等待当前事件 Inspector 重新挂载。";
                Save(state);
                return;
            }
            RebindVisualEvidenceToCurrentHost(host, currentInspector);
            if (!_visualEvidenceBinder.IsCharacterActionWorkspaceMountedInCurrentPageForTests)
            {
                Fail(state, "AI Lab 当前 Inspector 重新 Bind 后，已打开的 CharacterAction 工作区没有自动恢复。");
                return;
            }
            if (!_visualEvidenceBinder.TryStopCharacterActionWorkspaceForTests())
            {
                Fail(state, "停止动作预览后，PreviewActive 没有恢复为 false。");
                return;
            }
            state.Phase = 2;
            state.Message = "草稿预览已停止；等待下一 Editor Tick 验证工作区仍留在当前页面。";
            Save(state);
        }

        private static void CompleteStoppedWorkspaceVerification(
            State state,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host)
        {
            Fgui.GComponent page = host?.RootForTests;
            Fgui.GComponent timelineWorkspace = page?.GetChild("runtimeFrameWorkspace")?.asCom;
            Fgui.GComponent currentInspector = timelineWorkspace?.GetChild("behaviorRecipeInspector")?.asCom;
            if (_visualEvidenceModel == null)
            {
                Fail(state, "停止预览后的页面重绑无法取得当前事件 Inspector。");
                return;
            }
            if (currentInspector == null)
            {
                state.Message = "停止预览后正在等待当前事件 Inspector 重新挂载。";
                Save(state);
                return;
            }
            RebindVisualEvidenceToCurrentHost(host, currentInspector);
            if (!_visualEvidenceBinder.IsCharacterActionWorkspaceMountedInCurrentPageForTests)
            {
                Fail(state, "停止动作预览并重启测试后，CharacterAction 工作区退出了当前页面。");
                return;
            }
            state.Status = (int)TriggerTestEditorWorkflowStatus.Passed;
            state.Message = "已通过 Trigger 验证播放、页面重绑和停止均不退出动作工作区，并覆盖七轨、Inspector、Undo、草稿预览及 frame-0 重置。";
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
        }

        private static bool VerifyPublishedInspectorButton(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.IsRegistered)
            {
                diagnostic = "Editor 动作编排桥接未注册，无法验证“查看动作时间轴”按钮。";
                return false;
            }

            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load();
            if (runtime?.Registry == null
                || !runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig action))
            {
                diagnostic = "运行目录中找不到 Trigger 验证动作 light_attack。";
                return false;
            }
            CharacterActionTimelinePreviewViewModel preview =
                CharacterActionTimelinePreviewProjection.Build(action);
            if (preview?.Available != true)
            {
                diagnostic = "Trigger 验证动作无法生成时间轴：" + (preview?.Diagnostic ?? string.Empty);
                return false;
            }

            Fgui.GComponent component = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabBehaviorRecipeInspector")?.asCom;
            if (component == null)
            {
                diagnostic = "正式 FairyGUI 包无法创建行为事件 Inspector。";
                return false;
            }

            try
            {
                var sink = new RecordingCommandSink();
                var binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    popupService: null,
                    locateTimeline: _ => true,
                    closeInspector: () => { });
                binder.Bind(
                    component,
                    new BehaviorRecipeTriggerEventInspectorViewModel
                    {
                        Available = true,
                        UnifiedV6 = true,
                        InspectorMode = BehaviorRecipeInspectorMode.Event,
                        TimelineId = "trigger.timeline",
                        EventId = "trigger.event",
                        DisplayTitle = "Trigger CharacterAction Event",
                        OperationKind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                        OperationCharacterActionId = action.ActionId,
                        SelectedCharacterActionDisplayName = action.DisplayName,
                        SelectedCharacterActionDurationFrames = preview.DurationFrames,
                        CharacterActionReferenceValid = true,
                        CharacterActionEvent = true,
                        CharacterActionTimelinePreview = preview,
                        DurationFrames = Math.Max(1, preview.DurationFrames),
                        ActivationDurationFrames = Math.Max(1, preview.DurationFrames),
                        CanEdit = true,
                    });
                Fgui.GButton button = component.GetChild("btnViewCharacterActionTimeline")?.asButton;
                if (button == null || !button.visible || !button.enabled)
                {
                    diagnostic = "正式 Inspector 中“查看动作时间轴”按钮不可点击。";
                    return false;
                }

                button.onClick.Call();
                CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload payload =
                    sink.LastCommand.Payload as CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload;
                if (!sink.HasCommand
                    || !string.Equals(
                        sink.LastCommand.CommandId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenCharacterActionAuthoring,
                        StringComparison.Ordinal)
                    || payload?.ActionId != action.ActionId)
                {
                    diagnostic = "点击“查看动作时间轴”后未发出当前动作的完整工作区打开命令。";
                    return false;
                }
                return true;
            }
            finally
            {
                component.Dispose();
            }
        }

        private static bool OpenPublishedVisualEvidence(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            if (runtime?.Registry == null
                || !runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig action))
            {
                diagnostic = "无法为视觉验收加载 light_attack。";
                return false;
            }
            CharacterActionTimelinePreviewViewModel preview =
                CharacterActionTimelinePreviewProjection.Build(action);
            Fgui.GComponent page = host.RootForTests;
            Fgui.GComponent timelineWorkspace =
                page?.GetChild("runtimeFrameWorkspace")?.asCom;
            Fgui.GComponent component =
                timelineWorkspace?.GetChild("behaviorRecipeInspector")?.asCom;
            if (page == null || component == null || preview?.Available != true)
            {
                diagnostic = "当前 AI Lab 页面中找不到正式行为事件 Inspector 或动作预览。";
                return false;
            }

            var model = new BehaviorRecipeTriggerEventInspectorViewModel
            {
                Available = true,
                UnifiedV6 = true,
                InspectorMode = BehaviorRecipeInspectorMode.Event,
                TimelineId = "trigger.visual.timeline",
                EventId = "trigger.visual.event",
                DisplayTitle = "Trigger CharacterAction Visual Evidence",
                OperationKind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                OperationCharacterActionId = action.ActionId,
                SelectedCharacterActionDisplayName = action.DisplayName,
                SelectedCharacterActionDurationFrames = preview.DurationFrames,
                CharacterActionReferenceValid = true,
                CharacterActionEvent = true,
                CharacterActionTimelinePreview = preview,
                DurationFrames = Math.Max(1, preview.DurationFrames),
                ActivationDurationFrames = Math.Max(1, preview.DurationFrames),
                CanEdit = true,
            };
            _visualEvidenceModel = model;
            RebindVisualEvidenceToCurrentHost(host, component);
            Fgui.GButton button = component.GetChild("btnViewCharacterActionTimeline")?.asButton;
            button?.onClick.Call();
            Fgui.GObject workspacePickerPrototype = page.GetChild(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                    .Prototype);
            if (button == null
                || host.CommandSink?.CharacterActionWorkspace?.Available != true
                || host.CommandSink.CharacterActionWorkspace.Dirty
                || !_visualEvidenceBinder.IsCharacterActionWorkspaceMountedInCurrentPageForTests
                || controller.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || page.GetChild("behaviorCardTimeline")?.visible != false
                || workspacePickerPrototype?.visible == true
                || host.PopupServiceForTests?.HasOpenPopup == true)
            {
                diagnostic = "真实按钮打开后，动作时间轴未留在当前 AI Lab 页面、Game 渲染帧被替换，或仍有 Workspace 弹层覆盖。";
                return false;
            }
            if (!_visualEvidenceBinder.TryCloseCleanCharacterActionWorkspaceForTests()
                || page.GetChild("behaviorCardTimeline")?.visible != true)
            {
                diagnostic = "动作工作区零操作打开后仍被判 Dirty，或返回事件没有恢复原时间轴。";
                return false;
            }

            _visualEvidenceBinder.Bind(component, model);
            button.onClick.Call();
            if (host.CommandSink?.CharacterActionWorkspace?.Available != true
                || host.CommandSink.CharacterActionWorkspace.Dirty
                || !_visualEvidenceBinder.IsCharacterActionWorkspaceMountedInCurrentPageForTests)
            {
                diagnostic = "零操作返回验证后无法重新打开当前页动作工作区。";
                return false;
            }
            if (!_visualEvidenceBinder.TryPlayCharacterActionWorkspaceForTests())
            {
                diagnostic = "点击动作预览播放后，草稿动作没有进入 PreviewActive 状态。";
                return false;
            }
            return true;
        }

        private static void RebindVisualEvidenceToCurrentHost(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host,
            Fgui.GComponent inspector)
        {
            // Preview play/stop intentionally rebuilds the Runtime AI Lab panel. The old host
            // binder owns and disposes its popup service during that rebuild, so this Trigger
            // must model the real UI lifecycle by rebinding against the current service instead
            // of retaining a disposed service across phases.
            _visualEvidenceInspector = inspector;
            _visualEvidenceBinder =
                new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(
                    host.CommandSink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    popupService: host.PopupServiceForTests,
                    locateTimeline: _ => true,
                    closeInspector: () => { });
            _visualEvidenceBinder.Bind(inspector, _visualEvidenceModel);
        }

        private static void Fail(State state, string message)
        {
            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            controller?.TryStopCharacterActionWorkspacePreview(out _);
            CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                {
                    Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close,
                });
            state.Status = (int)TriggerTestEditorWorkflowStatus.Failed;
            state.Message = message ?? string.Empty;
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
            Debug.LogError("[TriggerTest][RuntimeAiLabCharacterActionAuthoring][FAIL] " + state.Message);
        }

        private static State Load()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            return string.IsNullOrEmpty(json) ? new State() : JsonUtility.FromJson<State>(json) ?? new State();
        }

        private static void Save(State state)
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(state));
        }

        [Serializable]
        private sealed class State
        {
            public int Status;
            public int Phase;
            public string Message = string.Empty;
            public double StartedAt;
            public double CompletedAt;
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            internal bool HasCommand { get; private set; }
            internal MxUiCommand LastCommand { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                HasCommand = true;
                LastCommand = command;
            }
        }
    }
}
