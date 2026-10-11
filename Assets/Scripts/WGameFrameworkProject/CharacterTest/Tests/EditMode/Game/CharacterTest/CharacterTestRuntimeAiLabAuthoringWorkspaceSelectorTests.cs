using System;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorTests
    {
        [Test]
        public void Selector_ProjectsOnlyMainAndTwoExperiments()
        {
            using (var fixture = new SelectorFixture())
            {
                Assert.IsTrue(fixture.Open().Success);

                CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot snapshot =
                    fixture.Selector.Capture(default);

                Assert.IsTrue(snapshot.Available);
                Assert.IsTrue(snapshot.HasActiveRole);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, snapshot.ActiveRole);
                Assert.AreEqual(3, snapshot.Rows.Count);
                Assert.AreEqual("主工作区", snapshot.Rows[0].DisplayName);
                Assert.AreEqual("主工作区", snapshot.Rows[0].RoleKindText);
                Assert.IsTrue(snapshot.Rows[0].IsCurrent);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.Saved,
                    snapshot.Rows[0].State);
                Assert.AreEqual("实验 A", snapshot.Rows[1].DisplayName);
                Assert.AreEqual("测试工作区", snapshot.Rows[1].RoleKindText);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.NotCreated,
                    snapshot.Rows[1].State);
                Assert.IsTrue(snapshot.Rows[1].CanCopyFromMain);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.NotCreated,
                    snapshot.Rows[2].State);
            }
        }

        [Test]
        public void Selector_FlushesThenPersistsActiveRoleBeforeSwitching()
        {
            using (var fixture = new SelectorFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                CharacterTestRuntimeAiLabDraftWorkspace main = fixture.Persistence.Workspace;
                main.DisplayName = "Pending Main edit";
                main.CurrentRevision++;
                fixture.Persistence.MarkDirty();

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult switched =
                    fixture.Selector.Select(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.IsTrue(switched.Success, switched.Message);
                Assert.IsFalse(main.Dirty, "The pending Main draft must be flushed before switching.");
                Assert.AreEqual("ExperimentA", File.ReadAllText(fixture.ActiveRolePath).Trim());
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    fixture.Roles.ActiveRole);
                Assert.AreEqual(switched.Workspace.WorkspaceId, fixture.Persistence.Workspace.WorkspaceId);
            }
        }

        [Test]
        public void Selector_RejectsMissingWorkspaceWithoutChangingCurrentRole()
        {
            using (var fixture = new SelectorFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                CharacterTestRuntimeAiLabDraftWorkspace main = fixture.Persistence.Workspace;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copied =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(copied.Success, copied.Message);
                fixture.DeleteWorkspaceAndRecovery(copied.Workspace.WorkspaceId);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    fixture.Selector.Select(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    result.Status);
                Assert.AreSame(main, fixture.Persistence.Workspace);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void Selector_RejectsDamagedCurrentSnapshotEvenWhenPreviousRecoveryExists()
        {
            using (var fixture = new SelectorFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copied =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(copied.Success, copied.Message);
                Assert.IsTrue(fixture.Selector.Select(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                CharacterTestRuntimeAiLabDraftWorkspace experiment = fixture.Persistence.Workspace;
                experiment.DisplayName = "Create a previous recovery snapshot";
                experiment.CurrentRevision++;
                fixture.Persistence.MarkDirty();
                Assert.IsTrue(fixture.Persistence.Flush(), fixture.Persistence.LastStatus);
                Assert.IsTrue(fixture.Selector.Select(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main).Success);

                File.WriteAllText(
                    Path.Combine(fixture.WorkspaceDirectory(experiment.WorkspaceId), "workspace.json"),
                    "{");
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    fixture.Selector.Select(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    result.Status);
                StringAssert.Contains("damaged", result.Message.ToLowerInvariant());
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void Selector_SaveFailureKeepsRoleSelectionAndProjectsFailure()
        {
            using (var fixture = new SelectorFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                fixture.Persistence.Workspace.IsReadOnly = true;

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    fixture.Selector.Select(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot snapshot =
                    fixture.Selector.Capture(result);

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed,
                    result.Status);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.SaveFailed,
                    snapshot.Rows[0].State);
            }
        }

        [Test]
        public void SelectorPayload_AcceptsRoleEnumOnly()
        {
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .TryParseAuthoringWorkspaceRolePayload(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB,
                        out CharacterTestRuntimeAiLabAuthoringWorkspaceRole role));
            Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB, role);
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .TryParseAuthoringWorkspaceRolePayload("ExperimentB", out _));
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .TryParseAuthoringWorkspaceRolePayload(Guid.NewGuid(), out _));
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
                    .TryParseAuthoringWorkspaceRolePayload(
                        (CharacterTestRuntimeAiLabAuthoringWorkspaceRole)99,
                        out _));
        }

        [Test]
        public void SelectorCommand_SwitchesOnceAndNoOpDoesNotRefresh()
        {
            using (var fixture = new SelectorFixture())
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult opened = fixture.Open();
                Assert.IsTrue(opened.Success, opened.Message);
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                var gameObject = new GameObject("RuntimeAiLabWorkspaceSelectorCommandTest");
                try
                {
                    CharacterTestRuntimeAiPlannerDevSceneController controller =
                        gameObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                    controller.PrepareInitialSetupState(restorePersistedDraft: false);
                    fixture.Attach(controller, opened);
                    int refreshCount = 0;
                    var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                        controller,
                        refreshPanel: () =>
                        {
                            refreshCount++;
                            return true;
                        });
                    IMxUiCommandSink commandSink = sink;
                    var command = new MxUiCommand(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .SelectAuthoringWorkspaceRole,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                    commandSink.Enqueue(command);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                        controller.ActiveAuthoringWorkspaceRole);
                    Assert.AreEqual("ExperimentA", File.ReadAllText(fixture.ActiveRolePath).Trim());
                    Assert.AreEqual(1, refreshCount);

                    commandSink.Enqueue(command);
                    Assert.AreEqual(1, refreshCount, "Selecting the active role must not rebuild the UI.");
                }
                finally
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        private sealed class SelectorFixture : IDisposable
        {
            internal SelectorFixture()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "MxFrameworkTests",
                    "RuntimeAiLabWorkspaceSelector",
                    Guid.NewGuid().ToString("N"));
                DraftsRoot = Path.Combine(Root, "Authoring", "RuntimeAiLab", "Drafts");
                RecoveryRoot = Path.Combine(Root, "UserSettings", "RuntimeAiLab", "DraftRecovery");
                RegistryPath = Path.Combine(DraftsRoot, "workspace-roles.json");
                ActiveRolePath = Path.Combine(
                    Root,
                    "UserSettings",
                    "RuntimeAiLab",
                    "active-workspace-role.txt");
            }

            internal string Root { get; }
            internal string DraftsRoot { get; }
            internal string RecoveryRoot { get; }
            internal string RegistryPath { get; }
            internal string ActiveRolePath { get; }
            internal CharacterTestRuntimeAiLabDraftPersistence Persistence { get; private set; }
            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoles Roles { get; private set; }
            internal CharacterTestRuntimeAiLabAuthoringWorkspaceSelector Selector =>
                new CharacterTestRuntimeAiLabAuthoringWorkspaceSelector(Roles, Persistence);

            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Open()
            {
                Persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    DraftsRoot,
                    RecoveryRoot);
                Roles = CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.CreateIsolated(
                    Persistence,
                    RegistryPath,
                    ActiveRolePath);
                return Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());
            }

            internal string WorkspaceDirectory(string workspaceId)
            {
                return Path.Combine(DraftsRoot, workspaceId);
            }

            internal void DeleteWorkspaceAndRecovery(string workspaceId)
            {
                string current = WorkspaceDirectory(workspaceId);
                if (Directory.Exists(current))
                    Directory.Delete(current, recursive: true);
                string recovery = Path.Combine(RecoveryRoot, workspaceId);
                if (Directory.Exists(recovery))
                    Directory.Delete(recovery, recursive: true);
            }

            internal void Attach(
                CharacterTestRuntimeAiPlannerDevSceneController controller,
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult opened)
            {
                SetField(controller, "_draftPersistence", Persistence);
                SetField(controller, "_authoringWorkspaceRoles", Roles);
                SetField(controller, "_draftWorkspace", opened.Workspace);
                SetField(controller, "_initialOpenResult", opened);
                SetField(
                    controller,
                    "_lastOperationResult",
                    default(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult));
            }

            public void Dispose()
            {
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
