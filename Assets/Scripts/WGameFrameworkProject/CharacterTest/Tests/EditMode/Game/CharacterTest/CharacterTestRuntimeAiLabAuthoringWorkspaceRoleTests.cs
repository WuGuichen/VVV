using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceRoleTests
    {
        [Test]
        public void AuthoringWorkspaceRoles_DefineOnlyMainAndTwoExperiments()
        {
            var definitions = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.All;

            Assert.AreEqual(3, definitions.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, definitions[0].Role);
            Assert.IsTrue(definitions[0].CanPublish);
            Assert.IsFalse(definitions[0].CanReset);
            Assert.IsFalse(definitions[1].CanPublish);
            Assert.IsTrue(definitions[1].CanReset);
            Assert.IsFalse(definitions[2].CanPublish);
            Assert.IsTrue(definitions[2].CanReset);
            Assert.IsFalse(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(
                (CharacterTestRuntimeAiLabAuthoringWorkspaceRole)(-1)));
            Assert.IsFalse(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(
                (CharacterTestRuntimeAiLabAuthoringWorkspaceRole)3));
            Assert.IsFalse(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(
                (CharacterTestRuntimeAiLabAuthoringWorkspaceRole)int.MaxValue));
        }

        [Test]
        public void AuthoringWorkspaceRoles_FirstOpenCreatesMainAndLocalActiveRole()
        {
            using (var fixture = new RoleFixture())
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result = fixture.Open();

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual(result.Workspace.WorkspaceId, File.ReadAllText(fixture.LatestPath).Trim());
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
                Assert.IsTrue(File.Exists(fixture.RegistryPath));
                StringAssert.DoesNotContain("activeRole", File.ReadAllText(fixture.RegistryPath));
                StringAssert.StartsWith(
                    Path.GetFullPath(Path.Combine(fixture.Root, "UserSettings")),
                    Path.GetFullPath(fixture.ActiveRolePath));
                Assert.AreEqual(1, fixture.GuidWorkspaceDirectoryCount());

                string mainId = result.Workspace.WorkspaceId;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult reopened = fixture.Reopen();
                Assert.IsTrue(reopened.Success, reopened.Message);
                Assert.AreEqual(mainId, reopened.Workspace.WorkspaceId);
                Assert.AreEqual(1, fixture.GuidWorkspaceDirectoryCount());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_AdoptsLegacyLatestOnceThenRegistryWins()
        {
            using (var fixture = new RoleFixture())
            {
                var legacy = new CharacterTestRuntimeAiLabDraftPersistence(
                    fixture.DraftsRoot,
                    fixture.RecoveryRoot);
                string adoptedId = legacy.CreateFromCurrent(
                    CharacterTestArenaMatchConfig.CreateDefault()).WorkspaceId;
                Assert.IsTrue(legacy.Flush(), legacy.LastStatus);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult first = fixture.Open();
                Assert.IsTrue(first.Success, first.Message);
                Assert.AreEqual(adoptedId, first.Workspace.WorkspaceId);

                var laterLegacyWriter = new CharacterTestRuntimeAiLabDraftPersistence(
                    fixture.DraftsRoot,
                    fixture.RecoveryRoot);
                string laterId = laterLegacyWriter.CreateFromCurrent(
                    CharacterTestArenaMatchConfig.CreateDefault()).WorkspaceId;
                Assert.IsTrue(laterLegacyWriter.Flush(), laterLegacyWriter.LastStatus);
                Assert.AreEqual(laterId, File.ReadAllText(fixture.LatestPath).Trim());

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult reopened = fixture.Reopen();
                Assert.IsTrue(reopened.Success, reopened.Message);
                Assert.AreEqual(adoptedId, reopened.Workspace.WorkspaceId);
                Assert.AreNotEqual(laterId, reopened.Workspace.WorkspaceId);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_ActiveExperimentOpensWhenMainDirectoryIsBroken()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                string mainId = fixture.Persistence.Workspace.WorkspaceId;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copied =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(copied.Success, copied.Message);
                string experimentId = copied.Workspace.WorkspaceId;
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                fixture.DeleteWorkspaceAndRecovery(mainId);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult reopened = fixture.Reopen();

                Assert.IsTrue(reopened.Success, reopened.Message);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA, fixture.Roles.ActiveRole);
                Assert.AreEqual(experimentId, reopened.Workspace.WorkspaceId);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_UninitializedActiveExperimentCanRecoverToMain()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                var registry = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
                    fixture.RegistryPath,
                    fixture.ActiveRolePath);
                Assert.IsTrue(registry.WriteActiveRole(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    out string writeMessage), writeMessage);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failed = fixture.Reopen();
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized,
                    failed.Status);
                Assert.IsFalse(fixture.Roles.HasActiveRole);
                Assert.IsNull(fixture.Persistence.Workspace);
                Assert.AreEqual("ExperimentA", File.ReadAllText(fixture.ActiveRolePath).Trim());

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult recovered =
                    fixture.Roles.Switch(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main);
                Assert.IsTrue(recovered.Success, recovered.Message);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_BrokenActiveExperimentCanRecoverToMain()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                string mainId = fixture.Persistence.Workspace.WorkspaceId;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copied =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(copied.Success, copied.Message);
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                fixture.DeleteWorkspaceAndRecovery(copied.Workspace.WorkspaceId);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failed = fixture.Reopen();
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    failed.Status);
                Assert.IsNull(fixture.Persistence.Workspace);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult recovered =
                    fixture.Roles.Switch(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main);
                Assert.IsTrue(recovered.Success, recovered.Message);
                Assert.AreEqual(mainId, recovered.Workspace.WorkspaceId);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_NoActiveSessionCanSwitchToAnotherValidExperiment()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult a =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult b =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB);
                Assert.IsTrue(a.Success, a.Message);
                Assert.IsTrue(b.Success, b.Message);
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                fixture.DeleteWorkspaceAndRecovery(a.Workspace.WorkspaceId);
                Assert.IsFalse(fixture.Reopen().Success);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult recovered =
                    fixture.Roles.Switch(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB);

                Assert.IsTrue(recovered.Success, recovered.Message);
                Assert.AreEqual(b.Workspace.WorkspaceId, recovered.Workspace.WorkspaceId);
                Assert.AreEqual("ExperimentB", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_FailedReopenDeactivatesOldWorkspaceAndAutosave()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                CharacterTestRuntimeAiLabDraftWorkspace oldWorkspace = fixture.Persistence.Workspace;
                MutateFirstCard(fixture, "Unsaved old workspace");
                string directory = fixture.WorkspaceDirectory(oldWorkspace.WorkspaceId);
                fixture.DeleteWorkspaceAndRecovery(oldWorkspace.WorkspaceId);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failed =
                    fixture.Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());

                Assert.IsFalse(failed.Success);
                Assert.IsNull(failed.Workspace);
                Assert.IsFalse(fixture.Roles.HasActiveRole);
                Assert.IsNull(fixture.Roles.ActiveWorkspace);
                Assert.IsNull(fixture.Persistence.Workspace);
                fixture.Persistence.TickAutosave();
                Assert.IsFalse(fixture.Persistence.Flush());
                Assert.IsFalse(Directory.Exists(directory));
                Assert.IsTrue(oldWorkspace.Dirty);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_BrokenRegisteredMainFailsClosed()
        {
            using (var fixture = new RoleFixture())
            {
                var registry = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
                    fixture.RegistryPath,
                    fixture.ActiveRolePath);
                var state = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
                {
                    Main = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                        Guid.NewGuid().ToString("D"))
                };
                Assert.IsTrue(registry.Save(state, out string saveMessage), saveMessage);
                Assert.IsTrue(
                    registry.WriteActiveRole(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                        out saveMessage),
                    saveMessage);
                string registryBefore = File.ReadAllText(fixture.RegistryPath);
                string activeBefore = File.ReadAllText(fixture.ActiveRolePath);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result = fixture.Open();

                Assert.IsFalse(result.Success);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable,
                    result.Status);
                Assert.IsNull(fixture.Persistence.Workspace);
                Assert.AreEqual(0, fixture.GuidWorkspaceDirectoryCount());
                Assert.IsFalse(File.Exists(fixture.LatestPath));
                Assert.AreEqual(registryBefore, File.ReadAllText(fixture.RegistryPath));
                Assert.AreEqual(activeBefore, File.ReadAllText(fixture.ActiveRolePath));
            }
        }

        [Test]
        public void AuthoringWorkspaceRoleRegistry_RejectsDuplicateWorkspaceIdsOnSaveAndLoad()
        {
            using (var fixture = new RoleFixture())
            {
                string duplicateId = Guid.NewGuid().ToString("D");
                string sourceId = Guid.NewGuid().ToString("D");
                var registry = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
                    fixture.RegistryPath,
                    fixture.ActiveRolePath);
                var duplicateState = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
                {
                    Main = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(duplicateId),
                    ExperimentA = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                        duplicateId,
                        sourceId,
                        1,
                        DateTime.UtcNow)
                };

                Assert.IsFalse(registry.Save(duplicateState, out string saveMessage));
                StringAssert.Contains("distinct", saveMessage);

                Directory.CreateDirectory(fixture.DraftsRoot);
                File.WriteAllText(
                    fixture.RegistryPath,
                    "{\n"
                        + "  \"schema\": \"mx.character-test.runtime-ai-lab.authoring-workspace-roles.v1\",\n"
                        + "  \"main\": { \"workspaceId\": \"" + duplicateId + "\" },\n"
                        + "  \"experimentA\": {\n"
                        + "    \"workspaceId\": \"" + duplicateId + "\",\n"
                        + "    \"sourceMainWorkspaceId\": \"" + sourceId + "\",\n"
                        + "    \"sourceMainRevision\": 1,\n"
                        + "    \"copiedUtc\": \"" + DateTime.UtcNow.ToString("O") + "\"\n"
                        + "  }\n"
                        + "}");

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus load =
                    registry.Load(out _, out string loadMessage);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Invalid,
                    load);
                StringAssert.Contains("distinct", loadMessage);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_UninitializedExperimentAndFlushFailureKeepMain()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                CharacterTestRuntimeAiLabDraftWorkspace main = fixture.Persistence.Workspace;

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult missing =
                    fixture.Roles.Switch(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized,
                    missing.Status);
                Assert.AreSame(main, fixture.Persistence.Workspace);

                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                main.IsReadOnly = true;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult blocked =
                    fixture.Roles.Switch(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed, blocked.Status);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreSame(main, fixture.Persistence.Workspace);
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_MainAndExperimentsStayIsolatedAndResetSafely()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                string mainId = fixture.Persistence.Workspace.WorkspaceId;
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB).Success);
                fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry aEntry);
                fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry bEntry);
                Assert.AreEqual(3, new[] { mainId, aEntry.WorkspaceId, bEntry.WorkspaceId }.Distinct().Count());

                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                string cardId = MutateFirstCard(fixture, "Experiment A only");
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main).Success);
                Assert.AreNotEqual("Experiment A only", CardName(fixture.Persistence.Workspace, cardId));
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB).Success);
                Assert.AreNotEqual("Experiment A only", CardName(fixture.Persistence.Workspace, cardId));
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                Assert.AreEqual("Experiment A only", CardName(fixture.Persistence.Workspace, cardId));

                string oldAId = fixture.Persistence.Workspace.WorkspaceId;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult reset =
                    fixture.Roles.ResetExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(reset.Success, reset.Message);
                Assert.AreNotEqual(oldAId, reset.Workspace.WorkspaceId);
                Assert.AreNotEqual("Experiment A only", CardName(reset.Workspace, cardId));
                Assert.AreEqual(mainId, File.ReadAllText(fixture.LatestPath).Trim());
                Assert.IsTrue(fixture.Persistence.TryLoad(oldAId, out _, out string oldMessage), oldMessage);
                fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry bAfter);
                Assert.AreEqual(bEntry.WorkspaceId, bAfter.WorkspaceId);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_RejectsApplyWhenMainRevisionChanged()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                string mainId = fixture.Persistence.Workspace.WorkspaceId;
                MutateFirstCard(fixture, "Main changed");
                Assert.IsTrue(fixture.Persistence.Flush(), fixture.Persistence.LastStatus);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    fixture.Roles.ApplyExperimentToMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainRevisionConflict,
                    result.Status);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreEqual(mainId, fixture.Persistence.Workspace.WorkspaceId);
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_ApplyPreservesOldMainAndActivatesNewMain()
        {
            using (var fixture = new RoleFixture())
            {
                Assert.IsTrue(fixture.Open().Success);
                string oldMainId = fixture.Persistence.Workspace.WorkspaceId;
                string originalName = CardName(
                    fixture.Persistence.Workspace,
                    fixture.FixtureCardId);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copy =
                    fixture.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.IsTrue(copy.Success, copy.Message);
                string experimentId = copy.Workspace.WorkspaceId;
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                string cardId = MutateFirstCard(fixture, "Applied experiment");

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult applied =
                    fixture.Roles.ApplyExperimentToMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.IsTrue(applied.Success, applied.Message);
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, fixture.Roles.ActiveRole);
                Assert.AreNotEqual(oldMainId, applied.Workspace.WorkspaceId);
                Assert.AreEqual(
                    applied.Workspace.WorkspaceId,
                    File.ReadAllText(fixture.LatestPath).Trim());
                Assert.AreEqual("Applied experiment", CardName(applied.Workspace, cardId));
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
                Assert.IsTrue(fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry mainEntry));
                Assert.AreEqual(oldMainId, mainEntry.PreviousWorkspaceId);
                Assert.IsTrue(fixture.Persistence.TryLoad(
                    oldMainId,
                    out CharacterTestRuntimeAiLabDraftWorkspace oldMain,
                    out string oldMessage), oldMessage);
                Assert.AreEqual(originalName, CardName(oldMain, cardId));
                Assert.IsTrue(fixture.Roles.TryGetEntry(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry experimentEntry));
                Assert.AreEqual(experimentId, experimentEntry.WorkspaceId);
            }
        }

        [TestCase(CloneFailurePoint.Registry)]
        [TestCase(CloneFailurePoint.ActiveRole)]
        [TestCase(CloneFailurePoint.LatestPointer)]
        public void AuthoringWorkspaceRoles_CloneStateFailureDoesNotLeaveOrphan(
            CloneFailurePoint failurePoint)
        {
            using (var fixture = new RoleFixture())
            {
                bool failStateWrite = false;
                Func<string, bool> stateFault = path => failStateWrite
                    && PathEquals(
                        path,
                        failurePoint == CloneFailurePoint.Registry
                            ? fixture.RegistryPath
                            : fixture.ActiveRolePath);
                Assert.IsTrue(fixture.Open(stateFault).Success);

                if (failurePoint == CloneFailurePoint.Registry)
                {
                    int before = fixture.GuidWorkspaceDirectoryCount();
                    failStateWrite = true;
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failed =
                        fixture.Roles.InitializeExperimentFromMain(
                            CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                        failed.Status);
                    Assert.AreEqual(before, fixture.GuidWorkspaceDirectoryCount());
                    Assert.IsFalse(fixture.Roles.TryGetEntry(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                        out _));
                    return;
                }

                string oldMainId = fixture.Persistence.Workspace.WorkspaceId;
                Assert.IsTrue(fixture.Roles.InitializeExperimentFromMain(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                Assert.IsTrue(fixture.Roles.Switch(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).Success);
                MutateFirstCard(fixture, "Apply with injected failure");
                int directoryCount = fixture.GuidWorkspaceDirectoryCount();
                if (failurePoint == CloneFailurePoint.ActiveRole)
                {
                    failStateWrite = true;
                }
                else
                {
                    int remainingFailures = 1;
                    fixture.Persistence.AuthoringWorkspaceWriteFailureForTests = path =>
                    {
                        if (!PathEquals(path, fixture.LatestPath) || remainingFailures == 0)
                            return false;
                        remainingFailures--;
                        return true;
                    };
                }

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult apply =
                    fixture.Roles.ApplyExperimentToMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    apply.Status,
                    apply.Message);
                Assert.AreEqual(directoryCount, fixture.GuidWorkspaceDirectoryCount());
                Assert.AreEqual(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA, fixture.Roles.ActiveRole);
                Assert.AreEqual("ExperimentA", File.ReadAllText(fixture.ActiveRolePath).Trim());
                Assert.AreEqual(oldMainId, File.ReadAllText(fixture.LatestPath).Trim());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_CreateMainActiveWriteFailureRecoversSameMainOnNextOpen()
        {
            using (var fixture = new RoleFixture())
            {
                bool failActiveWrite = true;
                Func<string, bool> fault = path => failActiveWrite
                    && PathEquals(path, fixture.ActiveRolePath);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failed = fixture.Open(fault);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    failed.Status);
                Assert.IsNull(fixture.Persistence.Workspace);
                Assert.IsFalse(fixture.Roles.HasActiveRole);
                Assert.AreEqual(1, fixture.GuidWorkspaceDirectoryCount());
                string createdMainId = fixture.SingleWorkspaceId();
                Assert.IsTrue(File.Exists(fixture.RegistryPath));
                Assert.IsFalse(File.Exists(fixture.ActiveRolePath));

                failActiveWrite = false;
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult recovered =
                    fixture.Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());

                Assert.IsTrue(recovered.Success, recovered.Message);
                Assert.AreEqual(createdMainId, recovered.Workspace.WorkspaceId);
                Assert.AreEqual(1, fixture.GuidWorkspaceDirectoryCount());
                Assert.AreEqual("Main", File.ReadAllText(fixture.ActiveRolePath).Trim());
            }
        }

        [Test]
        public void AuthoringWorkspaceRoles_ExplicitRootDoesNotUseUserRoleStateWithoutInjection()
        {
            using (var fixture = new RoleFixture())
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    fixture.DraftsRoot,
                    fixture.RecoveryRoot);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoles roles =
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.Create(persistence);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot,
                    result.Status);
                Assert.IsFalse(roles.Enabled);
                Assert.IsNull(persistence.Workspace);
                Assert.IsFalse(File.Exists(fixture.RegistryPath));
                Assert.IsFalse(File.Exists(fixture.ActiveRolePath));
            }
        }

        [Test]
        public void AuthoringWorkspaceController_SeparatesResultsAndFailsClosedWhenWorkspaceUnavailable()
        {
            var gameObject = new GameObject("RuntimeAiLabWorkspaceRoleControllerTest");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    gameObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.WorkspaceAvailable);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted,
                    controller.InitialOpenResult.Status);

                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult operation =
                    controller.TrySwitchAuthoringWorkspaceRole(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot,
                    operation.Status);
                Assert.AreEqual(operation.Status, controller.LastOperationResult.Status);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted,
                    controller.InitialOpenResult.Status);
                Assert.IsFalse(controller.HasAuthoringWorkspaceRoleOpenFailure);
                Assert.IsTrue(controller.WorkspaceAvailable);

                FieldInfo workspaceField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_draftWorkspace", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(workspaceField);
                workspaceField.SetValue(controller, null);
                Assert.IsFalse(controller.TryGetAvailableDraftWorkspace(out _, out string availability));
                StringAssert.Contains("unavailable", availability.ToLowerInvariant());
                Assert.DoesNotThrow(() =>
                {
                    Assert.IsFalse(controller.TryValidateSessionStartConfiguration(out string validation));
                    StringAssert.Contains("unavailable", validation.ToLowerInvariant());
                });

                MethodInfo buildCandidate = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetMethod("BuildCandidateRevision", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(buildCandidate);
                Assert.DoesNotThrow(() => buildCandidate.Invoke(controller, null));
                StringAssert.Contains("unavailable", controller.CandidateDiagnostic.ToLowerInvariant());
                Assert.IsFalse(controller.IsProjectBehaviorCardLibraryActive);
                Assert.IsNull(controller.CreateProjectBehaviorCardLibraryView());
                Assert.DoesNotThrow(() =>
                {
                    Assert.IsFalse(controller.RestorePublishedCardAuthoringSlotBinding());
                });
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static string MutateFirstCard(RoleFixture fixture, string name)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = fixture.Persistence.Workspace;
            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                fixture.RequireFixtureCard(workspace);
            card.DisplayName = name;
            card.CurrentRevision++;
            workspace.CurrentRevision++;
            workspace.UpdatedUtc = DateTime.UtcNow;
            fixture.Persistence.MarkDirty();
            return card.CardDraftId;
        }

        private static string CardName(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string cardId)
        {
            return workspace.Cards[cardId].DisplayName;
        }

        private static bool PathEquals(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public enum CloneFailurePoint
        {
            Registry = 0,
            ActiveRole = 1,
            LatestPointer = 2
        }

        private sealed class RoleFixture : IDisposable
        {
            private string _fixtureCardId = string.Empty;

            internal RoleFixture()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "MxFrameworkTests",
                    "RuntimeAiLabWorkspaceRoles",
                    Guid.NewGuid().ToString("N"));
                DraftsRoot = Path.Combine(Root, "Authoring", "RuntimeAiLab", "Drafts");
                RecoveryRoot = Path.Combine(Root, "UserSettings", "RuntimeAiLab", "DraftRecovery");
                RegistryPath = Path.Combine(DraftsRoot, "workspace-roles.json");
                ActiveRolePath = Path.Combine(
                    Root,
                    "UserSettings",
                    "RuntimeAiLab",
                    "active-workspace-role.txt");
                LatestPath = Path.Combine(DraftsRoot, "latest-workspace.txt");
            }

            internal string Root { get; }
            internal string DraftsRoot { get; }
            internal string RecoveryRoot { get; }
            internal string RegistryPath { get; }
            internal string ActiveRolePath { get; }
            internal string LatestPath { get; }
            internal CharacterTestRuntimeAiLabDraftPersistence Persistence { get; private set; }
            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoles Roles { get; private set; }
            internal string FixtureCardId => _fixtureCardId;

            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Open(
                Func<string, bool> shouldFailStateWrite = null)
            {
                CreateSession(shouldFailStateWrite);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());
                EnsureMinimumFixtureData(result);
                return result;
            }

            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Reopen(
                Func<string, bool> shouldFailStateWrite = null)
            {
                CreateSession(shouldFailStateWrite);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    Roles.OpenInitial(CharacterTestArenaMatchConfig.CreateDefault());
                EnsureMinimumFixtureData(result);
                return result;
            }

            internal CharacterTestRuntimeAiLabBehaviorCardDraft RequireFixtureCard(
                CharacterTestRuntimeAiLabDraftWorkspace workspace)
            {
                if (workspace != null
                    && _fixtureCardId.Length > 0
                    && workspace.Cards.TryGetValue(
                        _fixtureCardId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    return card;
                }
                throw new InvalidOperationException(
                    "The UX-03 role fixture card is missing from the active workspace.");
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

            internal int GuidWorkspaceDirectoryCount()
            {
                return Directory.Exists(DraftsRoot)
                    ? Directory.GetDirectories(DraftsRoot)
                        .Count(path => Guid.TryParse(Path.GetFileName(path), out _))
                    : 0;
            }

            internal string SingleWorkspaceId()
            {
                return Directory.GetDirectories(DraftsRoot)
                    .Select(Path.GetFileName)
                    .Single(value => Guid.TryParse(value, out _));
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }

            private void CreateSession(Func<string, bool> shouldFailStateWrite)
            {
                Persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    DraftsRoot,
                    RecoveryRoot);
                Roles = CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.CreateIsolated(
                    Persistence,
                    RegistryPath,
                    ActiveRolePath,
                    shouldFailStateWrite);
            }

            private void EnsureMinimumFixtureData(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result)
            {
                if (!result.Success)
                    return;
                if (_fixtureCardId.Length > 0)
                {
                    RequireFixtureCard(result.Workspace);
                    return;
                }
                if (!result.Workspace.TryGetGroupForSlot(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
                {
                    throw new InvalidOperationException(
                        "The UX-03 role fixture requires a Team 1 slot 1 behavior group.");
                }

                string behaviorId =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                    null,
                    string.Empty,
                    "UX-03 Fixture Wait",
                    "Test",
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(behaviorId));
                var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    behaviorId,
                    enabled: true,
                    order: 10);
                var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                    null,
                    card.CardDraftId,
                    behaviorId,
                    10,
                    disabled: false,
                    runtimeEntry);
                result.Workspace.AddLoadedCard(card);
                group.Entries.Add(entry);
                group.Profile.Entries.Add(entry.RuntimeEntry);
                group.NormalizeOrders();
                group.CurrentRevision++;
                result.Workspace.CurrentRevision++;
                result.Workspace.UpdatedUtc = DateTime.UtcNow;
                Persistence.MarkDirty();
                if (!Persistence.Flush())
                {
                    throw new InvalidOperationException(
                        "The UX-03 role fixture could not persist its minimal card graph: "
                            + Persistence.LastStatus);
                }
                _fixtureCardId = card.CardDraftId;
            }
        }
    }
}
