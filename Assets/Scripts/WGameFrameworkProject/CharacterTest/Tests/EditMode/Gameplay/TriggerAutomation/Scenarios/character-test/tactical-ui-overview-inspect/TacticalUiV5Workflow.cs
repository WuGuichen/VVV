using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Fgui = global::FairyGUI;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    /// <summary>Editor-only repeatable pointer validation of the actual published workbench.</summary>
    /// <remarks>LowFreqAlloc: explicit test operation; no authoring writes or simulation steps.</remarks>
    public static class TacticalUiV5Workflow
    {
        private sealed class Step
        {
            internal string Name;
            internal Func<Fgui.GObject> Target;
            internal Action Verify;
            internal bool Escape;
        }
        private static readonly List<Step> Steps = new List<Step>();
        private static readonly List<object> Evidence = new List<object>();
        private static CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private static CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost _host;
        private static Fgui.GComponent _root;
        private static int _step, _phase;
        private static double _next, _started;
        private static Vector2 _pointer;
        private static string _beforeSource, _directory, _failure;
        private static float _previewWidth;
        private static string _eventId;
        public static bool Running { get; private set; }
        public static bool HasLiveRoot => _root != null && !_root.isDisposed && _root.visible;
        public static string Status { get; private set; } = "NotStarted";
        public static string EvidenceDirectory => _directory;

        public static bool TryBegin(out string message)
        {
            message = string.Empty;
            if (Running) { message = "An overview input run is already active."; return false; }
            _controller = Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            _host = Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            _root = _host?.RootForTests;
            var session = _controller?.CompositionEditingSession;
            if (!Application.isPlaying || !HasLiveRoot || session?.SelectedDefinition == null || Mouse.current == null || Keyboard.current == null)
            { message = "请在Play中打开战术卡制作页并选择一个封装。"; return false; }
            if (_controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running && !_controller.CaptureSnapshot().Paused)
            { message = "请先暂停当前测试，视图验收不推进运行世界。"; return false; }
            _directory = Path.GetFullPath("Library/Task04UiReworkEvidence/overview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(_directory);
            _beforeSource = JsonConvert.SerializeObject(session.Document);
            _previewWidth = Header.width;
            var action = session.SelectedDefinition.Source.Timelines.SelectMany(t => t.Events)
                .FirstOrDefault(e => e.Operation.Kind == "CharacterAction");
            if (action == null) { message = "当前样例需要一个角色动作事件。"; return false; }
            _eventId = action.EventId;
            string actionTitle = string.IsNullOrEmpty(action.Title) ? action.EventId : action.Title;
            try { VerifyGeometry(); }
            catch (Exception error) { message = error.Message; return false; }
            Steps.Clear(); Evidence.Clear();
            var alternate = session.SelectedDefinition.Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId != action.EventId);
            if (alternate != null)
            {
                string alternateTitle = string.IsNullOrEmpty(alternate.Title) ? alternate.EventId : alternate.Title;
                Steps.Add(new Step { Name = "select-other-event", Target = () => FindEvent(Timeline, alternate.EventId), Verify = () => {
                    Require(Inspector.GetChild("inputTitle")?.text == alternateTitle, "Event selection did not change the Inspector."); } });
            }
            Steps.Add(new Step { Name = "select-action", Target = () => FindEvent(Timeline, action.EventId), Verify = () => {
                var name = Inspector.GetChild("inputTitle")?.text;
                Require(name == actionTitle, "The clicked action did not become the Inspector object."); } });
            Steps.Add(new Step { Name = "expand-preview", Target = () => Header.GetChild("btnRuntimeExpand"), Verify = () => {
                Require(Header.width > _previewWidth + 100, "Preview did not expand."); } });
            Steps.Add(new Step { Name = "restore-editor", Target = () => Header.GetChild("btnRuntimeExpand"), Verify = VerifyGeometry });
            Steps.Add(new Step { Name = "open-insert-menu", Target = () => Composition.GetChild("btnInsert"), Verify = () => {
                Require(FindVisible(Fgui.GRoot.inst, "choiceList") != null, "Insert menu is not visible."); } });
            Steps.Add(new Step { Name = "cancel-menu", Escape = true, Verify = () => {
                Require(FindVisible(Fgui.GRoot.inst, "choiceList") == null, "Escape did not close the menu."); } });
            _step = 0; _phase = 0; _failure = null;
            _started = EditorApplication.timeSinceStartup; _next = _started;
            Status = "Running"; Running = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            message = "真实输入已排队；外部结果与截图写入 " + _directory;
            return true;
        }

        public static string CaptureStatusJson() => JsonConvert.SerializeObject(new {
            status = Status, step = _step < Steps.Count ? Steps[_step].Name : "complete", error = _failure,
            directory = _directory, count = Evidence.Count, running = Running });

        private static Fgui.GComponent Header => _root.GetChild("behaviorCardRuntimeHeader").asCom;
        private static Fgui.GComponent Timeline => _root.GetChild("behaviorCardTimeline").asCom;
        private static Fgui.GComponent Composition => _root.GetChild("tacticalCompositionPanel").asCom;
        private static Fgui.GComponent Inspector => _root.GetChild("runtimeFrameWorkspace").asCom.GetChild("behaviorRecipeInspector").asCom;

        private static void Tick()
        {
            if (!Running || EditorApplication.timeSinceStartup < _next) return;
            try
            {
                Require(Application.isPlaying && HasLiveRoot, "Play mode or the workbench closed during validation.");
                Require(EditorApplication.timeSinceStartup - _started < 30, "UI input workflow timed out.");
                if (_step >= Steps.Count) { Finish(null); return; }
                var step = Steps[_step];
                if (_phase == 0)
                {
                    if (step.Escape)
                    {
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
                    }
                    else
                    {
                        var target = step.Target();
                        Require(target != null, "Input target missing: " + step.Name);
                        _pointer = ClickPoint(target);
                        SendMouse(true);
                    }
                    _phase = 1; _next = EditorApplication.timeSinceStartup + 0.08;
                }
                else if (_phase == 1)
                {
                    if (step.Escape)
                    {
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    }
                    else SendMouse(false);
                    _phase = 2; _next = EditorApplication.timeSinceStartup + 0.18;
                }
                else if (_phase == 2)
                {
                    step.Verify();
                    Require(JsonConvert.SerializeObject(_controller.CompositionEditingSession.Document) == _beforeSource,
                        "A view-only input changed the authoring source.");
                    string screenshot = Path.Combine(_directory, _step.ToString("00") + "-" + step.Name + ".png");
                    ScreenCapture.CaptureScreenshot(screenshot);
                    Evidence.Add(new { step = step.Name, screenshot, width = Screen.width, height = Screen.height, sourceUnchanged = true });
                    _phase = 3; _next = EditorApplication.timeSinceStartup + 0.15;
                }
                else { _step++; _phase = 0; }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            if (Mouse.current != null) SendMouse(false);
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            _failure = failure; Running = false; Status = failure == null ? "PassedInputAndGeometry" : "Failed";
            File.WriteAllText(Path.Combine(_directory, "result.json"), JsonConvert.SerializeObject(new {
                status = Status, error = failure, inputPath = "InputSystem pointer -> FairyGUI; InputSystem keyboard -> AI Lab shortcut map -> popup service", steps = Evidence,
                visualReview = "PendingExternalEvidence", selectedEvent = _eventId }, Formatting.Indented));
        }

        private static void SendMouse(bool down)
        {
            var state = new MouseState { position = new Vector2(_pointer.x, Screen.height - _pointer.y) };
            if (down) state = state.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(Mouse.current, state);
        }

        private static Vector2 ClickPoint(Fgui.GObject target)
        {
            Require(target.visible && target.width > 0 && target.height > 0, "Input target is not visible.");
            Vector2 point = target.LocalToGlobal(new Vector2(target.width * 0.5f, target.height * 0.5f));
            Require(point.x >= 0 && point.y >= 0 && point.x < Screen.width && point.y < Screen.height, "Input target is clipped off-screen.");
            var hit = Fgui.Stage.inst.HitTest(point, true)?.gOwner;
            bool related = false;
            var expected = target is Fgui.GTextField ? target.parent : target;
            for (var current = hit; current != null; current = current.parent)
                if (ReferenceEquals(current, expected)) { related = true; break; }
            Require(related, "Visible control is covered: " + target.name);
            return point;
        }

        private static void VerifyGeometry()
        {
            Require(Timeline.visible && Header.visible && Inspector.visible, "The linked timeline, preview and Inspector must be visible.");
            Require(Timeline.y < Header.y && Timeline.y + Timeline.height <= Header.y + 1, "Combat preview must be below the editing workspace.");
            Require(Header.x + Header.width <= Inspector.x && Header.y + Header.height <= _root.height, "Preview overlaps the full-height Inspector or window edge.");
            Require(Timeline.height > 180, "Internal events are still squeezed into the old footer.");
            Require(Composition.GetChild("compositionTitle")?.visible == false, "Redundant composition heading is visible.");
            ClickPoint(_root.GetChild("btnCompositionSave"));
            ClickPoint(_root.GetChild("btnCompositionCopy"));
        }

        private static Fgui.GObject FindEvent(Fgui.GComponent parent, string eventId)
        {
            foreach (var child in parent.GetChildren())
            {
                if (!child.visible) continue;
                if (child.data is BehaviorRecipeEventViewModel model && (model.EventId == eventId
                    || model.TacticalSourceEventId == eventId && model.TacticalSourceNodeId == _controller.CompositionEditingSession?.SelectedNodeId)) return child;
                if (child is Fgui.GComponent component && FindEvent(component, eventId) is Fgui.GObject found) return found;
            }
            return null;
        }

        private static Fgui.GObject FindVisible(Fgui.GComponent parent, string name)
        {
            foreach (var child in parent.GetChildren())
            {
                if (!child.visible) continue;
                if (child.name == name) return child;
                if (child is Fgui.GComponent component && FindVisible(component, name) is Fgui.GObject found) return found;
            }
            return null;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
