using System;
using System.IO;
using System.Linq;
using MxFramework.Tests.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabPublishCurrentChangesTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void TacticalRecoverySurvivesNewSessionWithoutPublishingAgain(bool failReload, bool uncertainWrite, bool noCommit)
        {
            var fixture = CreateFixture();
            string journal = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M3/recovery-" + Guid.NewGuid().ToString("N") + "/pending.json"));
            fixture.Authoring.SetPublishRecoveryJournalForTests(journal);
            using (var runtime = new CharacterTestTacticalCardRuntimeFixture())
            {
                var workspace = fixture.Workspace;
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision,
                    CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
                Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out var group));
                fixture.Authoring.CompileTacticalForPublish = draft => {
                    var document = workspace.ResolveComposition(draft);
                    Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, runtime.Inputs, out var compiled, out var error), error);
                    return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(
                        CharacterTestTacticalFrozenSource.Capture(workspace, draft, document, runtime.Inputs, compiled)), Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
                };
                var preview = fixture.Authoring.PreparePublishCurrentChanges(workspace.WorkspaceId, workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                Assert.IsTrue(preview.PreviewReady, preview.Message);
                fixture.FailReload = failReload; fixture.ReturnUnconfirmedWriteFailure = uncertainWrite; fixture.FailWrite = noCommit;
                var result = fixture.Authoring.CommitPublishCurrentChanges(preview.Preview.Token, workspace.WorkspaceId,
                    workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                Assert.AreEqual(failReload, result.RequiresRecovery, result.Message);
                Assert.IsTrue(File.Exists(journal)); Assert.AreEqual(1, fixture.WriteCount);
                Assert.IsTrue(fixture.Authoring.PreparePublishCurrentChanges(workspace.WorkspaceId, workspace.CurrentRevision,
                    group, workspace.Cards, fixture.References).RequiresRecovery);
                Assert.AreEqual(1, fixture.WriteCount);
                fixture.FailReload = false;
                var restarted = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(fixture.Reload(), fixture.Reload, fixture.Write);
                restarted.SetPublishRecoveryJournalForTests(journal);
                Assert.IsTrue(restarted.HasPendingPublishRecovery);
                Assert.AreEqual(preview.Preview.Token, restarted.GetPublishRecoveryPreview().Token);
                string localName = "recovery keeps later author edits";
                Assert.IsTrue(workspace.TryRenameCompositionCard(new CharacterTestTacticalEditToken(workspace, workspace.Cards[id]), localName, out issue), issue);
                var recovered = restarted.RecoverPublishCurrentChanges(preview.Preview.Token, fixture.References);
                Assert.IsFalse(recovered.RequiresRecovery, recovered.Message);
                Assert.AreEqual(!noCommit, recovered.CatalogCommitSucceeded, recovered.Message);
                if (!noCommit)
                {
                    Assert.IsTrue(workspace.TryStagePublishCurrentChangesCheckpoint(recovered, recovered.State.GroupFileHash,
                        out var checkpoint, out issue, recovering: true), issue);
                    checkpoint.Commit();
                    Assert.AreEqual(localName, workspace.Cards[id].DisplayName);
                    Assert.AreEqual(1, workspace.Cards[id].SourcePublishedRevision);
                    Assert.IsTrue(workspace.Cards[id].HasCompositionSource);
                    Assert.IsTrue(restarted.CompletePublishRecoveryJournal(out issue), issue);
                    var replay = restarted.CommitPublishCurrentChanges(preview.Preview.Token, workspace.WorkspaceId,
                        workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                    Assert.IsTrue(replay.DuplicateConfirmation);
                }
                else
                {
                    Assert.AreEqual(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Cancelled, recovered.Status);
                    Assert.IsFalse(workspace.Cards[id].HasPublishedSource);
                    Assert.IsFalse(restarted.CommitPublishCurrentChanges(preview.Preview.Token, workspace.WorkspaceId,
                        workspace.CurrentRevision, group, workspace.Cards, fixture.References).CatalogChanged);
                }
                Assert.AreEqual(1, fixture.WriteCount); Assert.IsFalse(File.Exists(journal));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(journal), "verified.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    failReload, uncertainWrite, noCommit, writes = fixture.WriteCount, restartRecovered = true,
                    sourceRevision = workspace.Cards[id].SourcePublishedRevision, localEditsPreserved = workspace.Cards[id].DisplayName == localName }));
            }
        }
    }
}
