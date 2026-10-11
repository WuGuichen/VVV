#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabAuthoringWorkspacePickerTests
    {
        [Test]
        public void TypedCommands_AlwaysBoxFixedRoleEnum()
        {
            MxUiViewId viewId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId;
            MxUiCommand[] commands =
            {
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Select(
                    viewId,
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main),
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                    viewId,
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA),
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ResetExperiment(
                    viewId,
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA),
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ApplyExperimentToMain(
                    viewId,
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB)
            };

            for (int i = 0; i < commands.Length; i++)
            {
                Assert.IsInstanceOf<CharacterTestRuntimeAiLabAuthoringWorkspaceRole>(
                    commands[i].Payload);
                Assert.IsFalse(commands[i].Payload is string);
                Assert.IsFalse(commands[i].Payload is Guid);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Select(
                    viewId,
                    (CharacterTestRuntimeAiLabAuthoringWorkspaceRole)99));
        }

        [Test]
        public void PickerViewModel_ProjectsExactlyMainExperimentAExperimentB()
        {
            using (var fixture = new PickerFixture())
            {
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel =
                    fixture.Sink.CaptureAuthoringWorkspacePickerViewModel();

                Assert.IsTrue(viewModel.Available);
                Assert.AreEqual(3, viewModel.Rows.Length);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, viewModel.Rows[0].Role);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA, viewModel.Rows[1].Role);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB, viewModel.Rows[2].Role);
                Assert.IsInstanceOf<CharacterTestRuntimeAiLabAuthoringWorkspaceRole>(
                    viewModel.Rows[1].SelectionPayload);
                Assert.AreEqual("未创建", viewModel.Rows[1].StateText);
                Assert.IsTrue(viewModel.CanCopyExperimentA);
                Assert.IsFalse(viewModel.ShowResetExperiment);
                Assert.IsFalse(viewModel.ShowApplyExperimentToMain);
            }
        }

        [Test]
        public void CopyMainToExperiment_RequiresConfirmationAndCancelIsZeroMutation()
        {
            using (var fixture = new PickerFixture())
            {
                int initialWorkspaceCount = fixture.GuidWorkspaceDirectoryCount();
                MxUiCommand copy =
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                fixture.Enqueue(copy);
                Assert.IsFalse(fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    out _));
                Assert.AreEqual(initialWorkspaceCount, fixture.GuidWorkspaceDirectoryCount());
                Assert.IsTrue(fixture.Sink.CaptureAuthoringWorkspacePickerViewModel().ShowConfirmation);

                fixture.Enqueue(CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Close(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId));
                Assert.IsFalse(fixture.Sink.CaptureAuthoringWorkspacePickerViewModel().ShowConfirmation);
                Assert.AreEqual(initialWorkspaceCount, fixture.GuidWorkspaceDirectoryCount());

                fixture.Enqueue(copy);
                fixture.Enqueue(copy);
                Assert.IsTrue(fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry));
                Assert.IsNotEmpty(entry.WorkspaceId);
                Assert.AreEqual(initialWorkspaceCount + 1, fixture.GuidWorkspaceDirectoryCount());
            }
        }

        [Test]
        public void ResetCurrentExperiment_RequiresConfirmationAndRestoresMainCopy()
        {
            using (var fixture = new PickerFixture())
            {
                fixture.CopyExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                fixture.Select(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                CharacterTestRuntimeAiLabDraftWorkspace experiment = fixture.Persistence.Workspace;
                string originalWorkspaceId = experiment.WorkspaceId;
                experiment.DisplayName = "Pending experiment edit";
                experiment.CurrentRevision++;
                fixture.Persistence.MarkDirty();
                MxUiCommand reset =
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ResetExperiment(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                fixture.Enqueue(reset);
                Assert.AreEqual(originalWorkspaceId, fixture.Persistence.Workspace.WorkspaceId);
                Assert.AreEqual("Pending experiment edit", fixture.Persistence.Workspace.DisplayName);
                Assert.IsTrue(fixture.Sink.CaptureAuthoringWorkspacePickerViewModel().ShowConfirmation);

                fixture.Enqueue(reset);
                Assert.AreNotEqual(originalWorkspaceId, fixture.Persistence.Workspace.WorkspaceId);
                Assert.AreEqual("Experiment A", fixture.Persistence.Workspace.DisplayName);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    fixture.Controller.ActiveAuthoringWorkspaceRole);
            }
        }

        [Test]
        public void ApplyExperimentToMain_ReportsDiffAndSwitchesToMainAfterConfirmation()
        {
            using (var fixture = new PickerFixture())
            {
                fixture.CopyExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                fixture.Select(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                CharacterTestRuntimeAiLabBehaviorGroupDraft group =
                    fixture.Persistence.Workspace.Groups.Values.First();
                group.DisplayName += " Experiment";
                group.CurrentRevision++;
                fixture.Persistence.Workspace.CurrentRevision++;
                fixture.Persistence.MarkDirty();
                MxUiCommand apply =
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ApplyExperimentToMain(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                fixture.Enqueue(apply);
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel confirmation =
                    fixture.Sink.CaptureAuthoringWorkspacePickerViewModel();
                Assert.IsTrue(confirmation.ShowConfirmation);
                StringAssert.Contains("1 个行为组", confirmation.ConfirmationSummary);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    fixture.Controller.ActiveAuthoringWorkspaceRole);

                fixture.Enqueue(apply);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                    fixture.Controller.ActiveAuthoringWorkspaceRole);
                Assert.IsFalse(fixture.Sink.CaptureAuthoringWorkspacePickerViewModel().ShowConfirmation);
            }
        }

        [Test]
        public void ApplyExperimentToMain_MainRevisionConflictBlocksConfirmationAndWrite()
        {
            using (var fixture = new PickerFixture())
            {
                fixture.CopyExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                fixture.Persistence.Workspace.DisplayName = "Main changed later";
                fixture.Persistence.Workspace.CurrentRevision++;
                fixture.Persistence.MarkDirty();
                fixture.Select(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                string mainWorkspaceId = fixture.MainWorkspaceId;
                int workspaceCount = fixture.GuidWorkspaceDirectoryCount();

                fixture.Enqueue(
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ApplyExperimentToMain(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA));

                CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel =
                    fixture.Sink.CaptureAuthoringWorkspacePickerViewModel();
                Assert.IsFalse(viewModel.ShowConfirmation);
                Assert.IsFalse(viewModel.CanApplyExperimentToMain);
                StringAssert.Contains("不能直接覆盖", viewModel.ApplyDisabledReason);
                Assert.AreEqual(mainWorkspaceId, fixture.MainWorkspaceId);
                Assert.AreEqual(workspaceCount, fixture.GuidWorkspaceDirectoryCount());
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    fixture.Controller.ActiveAuthoringWorkspaceRole);
            }
        }

        [Test]
        public void ComponentContract_RequiresUnifiedThreeRoleRows()
        {
            Fgui.GComponent popup = BuildContractPopup();
            try
            {
                Assert.IsTrue(
                    CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder
                        .ValidateComponentContract(popup, out string diagnostic),
                    diagnostic);
                popup.RemoveChild(
                    popup.GetChild(
                        CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                            .ExperimentBRow),
                    dispose: true);
                Assert.IsFalse(
                    CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder
                        .ValidateComponentContract(popup, out diagnostic));
                StringAssert.Contains("roleExperimentB", diagnostic);
            }
            finally
            {
                popup.Dispose();
            }
        }

        [Test]
        public void InputBoundary_StopsPointerAndWheelBubblingAndEscapeCloses()
        {
            var outer = new Fgui.GComponent { name = "outer", opaque = true };
            var popup = new Fgui.GComponent { name = "popup", opaque = true };
            var inner = new Fgui.GGraph { name = "inner" };
            outer.AddChild(popup);
            popup.AddChild(inner);
            int outerClick = 0;
            int outerTouch = 0;
            int outerWheel = 0;
            int closeCount = 0;
            outer.onClick.Add(() => outerClick++);
            outer.onTouchBegin.Add(() => outerTouch++);
            outer.displayObject.onMouseWheel.Add(() => outerWheel++);
            CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder.InstallInputBoundary(
                popup,
                () => closeCount++);
            try
            {
                inner.onClick.BubbleCall();
                inner.onTouchBegin.BubbleCall();
                inner.displayObject.onMouseWheel.BubbleCall();
                Assert.AreEqual(0, outerClick);
                Assert.AreEqual(0, outerTouch);
                Assert.AreEqual(0, outerWheel);

                var input = new Fgui.InputEvent();
                typeof(Fgui.InputEvent).GetProperty(
                        nameof(Fgui.InputEvent.keyCode),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.SetValue(input, KeyCode.Escape);
                popup.onKeyDown.Call(input);
                Assert.AreEqual(1, closeCount);
            }
            finally
            {
                outer.Dispose();
            }
        }

        [Test]
        public void Bind_DoesNotRevealComposerPrototypeBeforePickerOpens()
        {
            using (var fixture = new PickerFixture())
            {
                var root = new Fgui.GComponent();
                var anchor = new Fgui.GButton
                {
                    name = CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                        .Anchor
                };
                Fgui.GComponent prototype = BuildContractPopup();
                prototype.name =
                    CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                        .Prototype;
                prototype.visible = false;
                root.AddChild(anchor);
                root.AddChild(prototype);
                var binder = new CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder(
                    fixture.Sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    popupService: null);
                try
                {
                    long loads = CharacterTestRuntimeAiLabDraftPersistence.DirectoryLoadCount;
                    for (int frame = 0; frame < 10; frame++) binder.Bind(root);
                    Assert.AreEqual(loads, CharacterTestRuntimeAiLabDraftPersistence.DirectoryLoadCount,
                        "Normal panel refresh must not read any draft directory, even when the picker is hidden.");
                    Assert.IsNull(typeof(CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder)
                        .GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binder),
                        "The live popup should be created only when explicitly opened.");
                    fixture.Sink.CaptureAuthoringWorkspacePickerViewModel();
                    Assert.Greater(CharacterTestRuntimeAiLabDraftPersistence.DirectoryLoadCount, loads,
                        "Explicit picker inspection must still validate persisted workspaces.");

                    Assert.IsFalse(
                        prototype.visible,
                        "Binding the toolbar must not expose the Composer prototype as a live popup.");
                    Assert.AreSame(prototype, root.GetChild(
                        CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                            .Prototype));
                }
                finally
                {
                    binder.Dispose();
                    root.Dispose();
                }
            }
        }

        private static Fgui.GComponent BuildContractPopup()
        {
            var popup = new Fgui.GComponent();
            AddText(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Title);
            AddText(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Status);
            popup.AddChild(BuildRoleRow(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.MainRow));
            popup.AddChild(BuildRoleRow(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentARow));
            popup.AddChild(BuildRoleRow(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentBRow));
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentA);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentB);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ResetExperiment);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ApplyExperimentToMain);
            popup.AddChild(new Fgui.GGraph
            {
                name = CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationPanel
            });
            AddText(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationTitle);
            AddText(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationSummary);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Confirm);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Cancel);
            AddButton(popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Close);
            return popup;
        }

        private static Fgui.GComponent BuildRoleRow(string name)
        {
            var row = new Fgui.GComponent { name = name };
            row.AddChild(new Fgui.GGraph
            {
                name = CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowCurrentMark
            });
            string[] textChildren =
            {
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRoleName,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRoleKind,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowState,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRevision,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowUpdated,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowSource,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowDiagnostic
            };
            for (int i = 0; i < textChildren.Length; i++)
                AddText(row, textChildren[i]);
            return row;
        }

        private static void AddText(Fgui.GComponent parent, string name)
        {
            parent.AddChild(new Fgui.GTextField { name = name });
        }

        private static void AddButton(Fgui.GComponent parent, string name)
        {
            parent.AddChild(new Fgui.GButton { name = name });
        }

        private sealed class PickerFixture : IDisposable
        {
            private readonly GameObject _gameObject;

            internal PickerFixture()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "MxFrameworkTests",
                    "RuntimeAiLabWorkspacePicker",
                    Guid.NewGuid().ToString("N"));
                DraftsRoot = Path.Combine(Root, "Authoring", "RuntimeAiLab", "Drafts");
                RecoveryRoot = Path.Combine(Root, "UserSettings", "RuntimeAiLab", "DraftRecovery");
                RegistryPath = Path.Combine(DraftsRoot, "workspace-roles.json");
                ActiveRolePath = Path.Combine(
                    Root,
                    "UserSettings",
                    "RuntimeAiLab",
                    "active-workspace-role.txt");
                Persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    DraftsRoot,
                    RecoveryRoot);
                Roles = CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.CreateIsolated(
                    Persistence,
                    RegistryPath,
                    ActiveRolePath);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult opened =
                    Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(opened.Success, opened.Message);
                _gameObject = new GameObject("RuntimeAiLabWorkspacePickerTest");
                Controller = _gameObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                Controller.PrepareInitialSetupState(restorePersistedDraft: false);
                SetField(Controller, "_draftPersistence", Persistence);
                SetField(Controller, "_authoringWorkspaceRoles", Roles);
                SetField(Controller, "_draftWorkspace", opened.Workspace);
                SetField(Controller, "_initialOpenResult", opened);
                SetField(
                    Controller,
                    "_lastOperationResult",
                    default(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult));
                Sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                    Controller,
                    refreshPanel: () => true);
            }

            internal string Root { get; }
            internal string DraftsRoot { get; }
            internal string RecoveryRoot { get; }
            internal string RegistryPath { get; }
            internal string ActiveRolePath { get; }
            internal CharacterTestRuntimeAiLabDraftPersistence Persistence { get; }
            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoles Roles { get; }
            internal CharacterTestRuntimeAiPlannerDevSceneController Controller { get; }
            internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink Sink { get; }

            internal string MainWorkspaceId
            {
                get
                {
                    Assert.IsTrue(Roles.TryGetEntry(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                        out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry main));
                    return main.WorkspaceId;
                }
            }

            internal void Enqueue(MxUiCommand command)
            {
                ((IMxUiCommandSink)Sink).Enqueue(command);
            }

            internal void CopyExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
            {
                MxUiCommand command =
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        role);
                Enqueue(command);
                Enqueue(command);
                Assert.IsTrue(Roles.TryGetEntry(role, out _));
            }

            internal void Select(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
            {
                Enqueue(CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Select(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    role));
                Assert.AreEqual(role, Controller.ActiveAuthoringWorkspaceRole);
            }

            internal int GuidWorkspaceDirectoryCount()
            {
                return Directory.Exists(DraftsRoot)
                    ? Directory.GetDirectories(DraftsRoot)
                        .Count(path => Guid.TryParse(Path.GetFileName(path), out _))
                    : 0;
            }

            public void Dispose()
            {
                if (_gameObject != null)
                    Object.DestroyImmediate(_gameObject);
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }

            private static void SetField(object target, string name, object value)
            {
                FieldInfo field = target.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field, "Missing test fixture field: " + name);
                field.SetValue(target, value);
            }
        }
    }
}
#endif
