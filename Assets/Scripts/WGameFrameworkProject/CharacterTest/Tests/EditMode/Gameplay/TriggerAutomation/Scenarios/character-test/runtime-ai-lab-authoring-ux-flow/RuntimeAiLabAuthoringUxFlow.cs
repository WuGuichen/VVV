using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Game.CharacterTest;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class RuntimeAiLabAuthoringUxFlowArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabAuthoringUxFlowArguments Instance =
            new RuntimeAiLabAuthoringUxFlowArguments();

        private RuntimeAiLabAuthoringUxFlowArguments()
        {
        }
    }

    /// <summary>
    /// Runs the UX-08 core authoring chain against scenario-owned Temp roots. The live scene is
    /// read only here; asynchronous Game View interaction remains explicit external evidence.
    /// </summary>
    /// <remarks>LowFreqAlloc: one explicit manual Trigger invocation.</remarks>
    internal sealed class RuntimeAiLabAuthoringUxFlowFunction :
        TriggerTestFunction<RuntimeAiLabAuthoringUxFlowArguments>,
        ITriggerTestFunctionPreviewProvider
    {
        internal static readonly TriggerTestFunctionId Id = new TriggerTestFunctionId(
            "mx.trigger.test.function.character-test.runtime-ai-lab-authoring-ux-flow");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        public TriggerTestFunctionPreview DescribePreview(
            ITriggerTestFunctionArguments arguments)
        {
            return new TriggerTestFunctionPreview(
                "验证 Runtime AI Lab 创作、测试、发布与实验工作区组合闭环",
                "Play Mode · 全部可写内容重定向到 Temp · 生产范围前后 SHA-256 门禁",
                "Runtime AI Lab 正式场景已经打开",
                "Unity 已进入 Play Mode 且只有一个活动 Controller",
                "正式 FairyGUI Panel 已配置并打开");
        }

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabAuthoringUxFlowArguments arguments)
        {
            if (!context.Fixture.Services.TryGet(out RuntimeAiLabAuthoringUxFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab authoring UX flow probe is unavailable.");
            }

            probe.InvocationCount++;
            if (!probe.TryCaptureLivePreconditions(out string diagnostic))
                return Fail(probe, "live-precondition", diagnostic);
            if (!probe.TryRunIsolatedCore(out diagnostic))
                return Fail(probe, probe.LastStage, diagnostic);

            probe.FinalStage = "core-complete";
            return TriggerTestOperationResult.Succeeded(
                "Temp Main/Experiment、TestCurrentChanges、发布取消/确认、" +
                "继续当前测试、Published 重测、无修改返回和生产哈希门禁已完成；" +
                "真实延迟队列与 Game View 输入验收保持 PendingExternalEvidence。");
        }

        private static TriggerTestOperationResult Fail(
            RuntimeAiLabAuthoringUxFlowProbe probe,
            string stage,
            string diagnostic)
        {
            probe.FinalStage = string.IsNullOrWhiteSpace(stage) ? "failed" : stage;
            return TriggerTestOperationResult.Failed(
                probe.FinalStage + ": " +
                (string.IsNullOrWhiteSpace(diagnostic)
                    ? "No diagnostic was provided."
                    : diagnostic));
        }
    }

    internal sealed class RuntimeAiLabAuthoringUxFlowFixture : ITriggerTestFixture
    {
        private readonly ITriggerTestFixture _inner;
        private readonly RuntimeAiLabAuthoringUxFlowProbe _probe;

        internal RuntimeAiLabAuthoringUxFlowFixture(ITriggerTestFixture inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _probe = new RuntimeAiLabAuthoringUxFlowProbe();
            _inner.Services.Register(_probe);
        }

        public MxFramework.Gameplay.Triggers.GameplayTriggerRuntime TriggerRuntime =>
            _inner.TriggerRuntime;

        public TriggerTestServiceRegistry Services => _inner.Services;

        public void Initialize()
        {
            _inner.Initialize();
        }

        public void Tick(long frame, double deltaTime)
        {
            _inner.Tick(frame, deltaTime);
        }

        public void Dispose()
        {
            try
            {
                _inner.Dispose();
            }
            finally
            {
                _probe.Dispose();
            }
        }
    }

    /// <summary>
    /// Independent oracle for UX-08. Function success is never used as the only assertion.
    /// </summary>
    internal sealed class RuntimeAiLabAuthoringUxFlowProbe : IDisposable
    {
        private readonly RuntimeAiLabAuthoringUxFlowSandbox _sandbox;
        private readonly RuntimeAiLabProductionFootprint _productionBefore;
        private RuntimeAiLabProductionFootprint _productionAfterCore;

        internal RuntimeAiLabAuthoringUxFlowProbe()
        {
            _productionBefore = RuntimeAiLabProductionFootprint.Capture();
            _sandbox = new RuntimeAiLabAuthoringUxFlowSandbox();
        }

        internal int InvocationCount { get; set; }
        internal int CoreRunCount { get; private set; }
        internal string LastStage { get; private set; } = "not-started";
        internal string FinalStage { get; set; } = string.Empty;
        internal bool LiveSceneReady { get; private set; }
        internal bool LivePanelReady { get; private set; }
        internal bool MainOpened { get; private set; }
        internal bool FixtureSelected { get; private set; }
        internal bool DraftModified { get; private set; }
        internal bool DraftSaved { get; private set; }
        internal int DraftFlushCount { get; private set; }
        internal int TestCurrentChangesFlushCount { get; private set; }
        internal bool TestCurrentChangesSucceeded { get; private set; }
        internal bool TestRestartedFromFrameZero { get; private set; }
        internal bool TestContextRestored { get; private set; }
        internal bool NoModificationReturnSucceeded { get; private set; }
        internal bool CharacterActionNoModificationReturnSucceeded { get; private set; }
        internal bool BusyNavigationFailedClosed { get; private set; }
        internal bool PublishPreviewCancelledWithoutWrite { get; private set; }
        internal bool PublishConfirmedOnce { get; private set; }
        internal bool PublishCheckpointSaved { get; private set; }
        internal bool ContinuePreservedRuntimeIdentity { get; private set; }
        internal bool PublishedRetestSucceeded { get; private set; }
        internal bool ExperimentCopied { get; private set; }
        internal bool ExperimentModifiedAndTested { get; private set; }
        internal bool ExperimentPublishUnavailable { get; private set; }
        internal bool MainUnchangedByExperiment { get; private set; }
        internal bool PublishedUnchangedByExperiment { get; private set; }
        internal bool ReturnedToMain { get; private set; }
        internal bool ReopenedOnMain { get; private set; }
        internal string GroupDraftId { get; private set; } = string.Empty;
        internal string CardDraftId { get; private set; } = string.Empty;
        internal string TimelineId { get; private set; } = string.Empty;
        internal string EventId { get; private set; } = string.Empty;
        internal int SourceCardRevision { get; private set; }
        internal int PublishedCardRevision { get; private set; }
        internal long DraftSemanticHash { get; private set; }
        internal long PublishedRetestHash { get; private set; }
        internal int TempPublishedWriteCount { get; private set; }
        internal bool ProductionFootprintValid =>
            _productionBefore.IsValid && _productionAfterCore.IsValid;
        internal bool ProductionUnchangedAfterCore =>
            _productionBefore.Equals(_productionAfterCore);
        internal string TempRoot => _sandbox.RootPath;
        internal bool AllWritableAuthoritiesUseTemp =>
            _sandbox.AllWritableAuthoritiesUseTemp;

        internal bool TryCaptureLivePreconditions(out string diagnostic)
        {
            if (!Application.isPlaying)
            {
                diagnostic = "Unity is not in Play Mode.";
                return false;
            }
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out diagnostic))
            {
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            LiveSceneReady = controller != null
                && string.Equals(
                    active.path,
                    TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                    StringComparison.Ordinal);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panel =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            LivePanelReady = panel != null
                && panel.IsConfigured
                && panel.IsOpen
                && panel.RootForTests != null
                && panel.CommandSink != null;
            if (!LiveSceneReady || !LivePanelReady)
            {
                diagnostic = !LiveSceneReady
                    ? "The active Play Mode scene is not the Runtime AI Lab target."
                    : "The published Runtime AI Lab FairyGUI panel is not fully open.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        internal bool TryRunIsolatedCore(out string diagnostic)
        {
            CoreRunCount++;
            if (CoreRunCount != 1)
            {
                diagnostic = "The isolated UX flow may run only once per fresh fixture.";
                return false;
            }

            try
            {
                LastStage = "initialize-temp-authority";
                if (!_productionBefore.IsValid)
                {
                    diagnostic = _productionBefore.Diagnostic;
                    return false;
                }
                if (!_sandbox.TryInitialize(out diagnostic))
                    return false;

                LastStage = "open-main";
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult mainOpen =
                    _sandbox.Roles.OpenInitial(_sandbox.MatchConfig);
                MainOpened = mainOpen.Success
                    && _sandbox.Roles.ActiveRole ==
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                    && ReferenceEquals(mainOpen.Workspace, _sandbox.Persistence.Workspace);
                if (!MainOpened)
                {
                    diagnostic = mainOpen.Message;
                    return false;
                }

                LastStage = "select-fixture";
                if (!RuntimeAiLabAuthoringUxFlowSelection.TrySelect(
                        _sandbox.Persistence.Workspace,
                        preferredGroupDraftId: string.Empty,
                        preferredEntryId: string.Empty,
                        out RuntimeAiLabAuthoringUxFlowSelection selection,
                        out diagnostic))
                {
                    return false;
                }
                FixtureSelected = true;
                GroupDraftId = selection.GroupDraftId;
                CardDraftId = selection.CardDraftId;
                TimelineId = selection.TimelineId;
                EventId = selection.EventId;
                SourceCardRevision = selection.SourcePublishedRevision;

                LastStage = "modify-main";
                if (!selection.TryToggleFixtureField(
                        _sandbox.Persistence.Workspace,
                        out RuntimeAiLabAuthoringUxFlowSelection modified,
                        out diagnostic))
                {
                    return false;
                }
                selection = modified;
                DraftSemanticHash = selection.SemanticHash;
                DraftModified = DraftSemanticHash != 0L
                    && DraftSemanticHash != selection.SourcePublishedSemanticHash;
                if (!DraftModified)
                {
                    diagnostic = "The fixture edit did not change the card semantic hash.";
                    return false;
                }

                LastStage = "save-main";
                _sandbox.Persistence.MarkDirty();
                DraftFlushCount++;
                DraftSaved = _sandbox.Persistence.Flush()
                    && !_sandbox.Persistence.Workspace.Dirty
                    && _sandbox.WorkspaceFileExists(
                        _sandbox.Persistence.Workspace.WorkspaceId);
                if (!DraftSaved)
                {
                    diagnostic = _sandbox.Persistence.LastStatus;
                    return false;
                }

                LastStage = "test-current-changes";
                CharacterTestRuntimeAiLabTestCurrentChangesResult currentTest =
                    RunTestCurrentChanges(
                        selection,
                        publishedContentPristine: false,
                        selectedEntityId: 7001,
                        operationId: "ux08-test-current-main",
                        out int testFlushCount,
                        out diagnostic);
                TestCurrentChangesFlushCount = testFlushCount;
                TestCurrentChangesSucceeded = currentTest.Success;
                TestRestartedFromFrameZero = currentTest.RestartedFromFrameZero
                    && currentTest.FirstVisibleFrame == 0;
                TestContextRestored = currentTest.ContextRestored
                    && string.Equals(currentTest.GroupDraftId, GroupDraftId, StringComparison.Ordinal)
                    && string.Equals(currentTest.CardDraftId, CardDraftId, StringComparison.Ordinal);
                if (!TestCurrentChangesSucceeded
                    || !TestRestartedFromFrameZero
                    || !TestContextRestored
                    || TestCurrentChangesFlushCount != 1)
                {
                    diagnostic = "TestCurrentChanges fixture mismatch: success="
                        + TestCurrentChangesSucceeded
                        + ", frame0=" + TestRestartedFromFrameZero
                        + ", context=" + TestContextRestored
                        + ", flushCount=" + TestCurrentChangesFlushCount
                        + ". " + currentTest.Message;
                    return false;
                }

                LastStage = "navigation-no-modification";
                if (!TryValidateNavigationContracts(out diagnostic))
                    return false;

                LastStage = "publish-cancel";
                var authoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
                    _sandbox.Published.State,
                    _sandbox.Published.Reload,
                    _sandbox.Published.Write);
                string publishedHashBeforeCancel = _sandbox.Published.CaptureContentHash();
                CharacterTestRuntimeAiLabPublishCurrentChangesResult cancelledPreview =
                    authoring.PreparePublishCurrentChanges(
                        _sandbox.Persistence.Workspace.WorkspaceId,
                        _sandbox.Persistence.Workspace.CurrentRevision,
                        selection.Group,
                        _sandbox.Persistence.Workspace.Cards,
                        _sandbox.References);
                if (!cancelledPreview.PreviewReady
                    || !authoring.CancelPublishCurrentChanges(cancelledPreview.Preview.Token))
                {
                    diagnostic = cancelledPreview.Message;
                    return false;
                }
                PublishPreviewCancelledWithoutWrite = _sandbox.Published.WriteCount == 0
                    && string.Equals(
                        publishedHashBeforeCancel,
                        _sandbox.Published.CaptureContentHash(),
                        StringComparison.Ordinal);
                if (!PublishPreviewCancelledWithoutWrite)
                {
                    diagnostic = "Cancelling the publish preview changed the Temp Published override.";
                    return false;
                }

                LastStage = "publish-confirm";
                CharacterTestRuntimeAiLabPublishCurrentChangesResult preview =
                    authoring.PreparePublishCurrentChanges(
                        _sandbox.Persistence.Workspace.WorkspaceId,
                        _sandbox.Persistence.Workspace.CurrentRevision,
                        selection.Group,
                        _sandbox.Persistence.Workspace.Cards,
                        _sandbox.References);
                if (!preview.PreviewReady)
                {
                    diagnostic = preview.Message;
                    return false;
                }
                CharacterTestRuntimeAiLabPublishCurrentChangesResult committed =
                    authoring.CommitPublishCurrentChanges(
                        preview.Preview.Token,
                        _sandbox.Persistence.Workspace.WorkspaceId,
                        _sandbox.Persistence.Workspace.CurrentRevision,
                        selection.Group,
                        _sandbox.Persistence.Workspace.Cards,
                        _sandbox.References);
                TempPublishedWriteCount = _sandbox.Published.WriteCount;
                PublishConfirmedOnce = committed.CatalogCommitSucceeded
                    && TempPublishedWriteCount == 1
                    && committed.PublishedCards.Count > 0;
                if (!PublishConfirmedOnce)
                {
                    diagnostic = committed.Message;
                    return false;
                }

                LastStage = "publish-checkpoint";
                if (!_sandbox.Persistence.Workspace.TryStagePublishCurrentChangesCheckpoint(
                        committed,
                        committed.State.GroupFileHash,
                        out CharacterTestRuntimeAiLabDraftWorkspace.PublishCurrentChangesCheckpoint checkpoint,
                        out diagnostic))
                {
                    return false;
                }
                _sandbox.Persistence.MarkDirty();
                if (!_sandbox.Persistence.Flush())
                {
                    checkpoint.TryRollback(out string rollbackDiagnostic);
                    diagnostic = _sandbox.Persistence.LastStatus + " " + rollbackDiagnostic;
                    return false;
                }
                checkpoint.Commit();
                CharacterTestRuntimeAiLabPublishCurrentChangesResult completed =
                    committed.CompleteAfterDraftCheckpoint(
                        "Temp Published Card/Group and draft checkpoints updated.");
                PublishCheckpointSaved = completed.Success;
                PublishedCardRevision = completed.PublishedCards[0].Card.Revision;
                if (!PublishCheckpointSaved || PublishedCardRevision <= SourceCardRevision)
                {
                    diagnostic = "The Temp Published checkpoint did not advance the card revision.";
                    return false;
                }

                LastStage = "continue-current-test";
                var postPublish = new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator();
                const string retainedRuntimeIdentity = "ux08-runtime-session-before-publish";
                CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult continued =
                    postPublish.Choose(
                        completed,
                        completed.Preview.Token,
                        CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                            .ContinueCurrentTest,
                        () => retainedRuntimeIdentity,
                        restartPublished: null);
                ContinuePreservedRuntimeIdentity = continued.Success
                    && continued.RuntimeIdentityPreserved;
                if (!ContinuePreservedRuntimeIdentity)
                {
                    diagnostic = continued.Message;
                    return false;
                }

                LastStage = "retest-published";
                if (!RuntimeAiLabAuthoringUxFlowSelection.TrySelect(
                        _sandbox.Persistence.Workspace,
                        GroupDraftId,
                        selection.EntryId,
                        out selection,
                        out diagnostic))
                {
                    return false;
                }
                CharacterTestRuntimeAiLabTestCurrentChangesResult publishedRetest =
                    RunTestCurrentChanges(
                        selection,
                        publishedContentPristine: true,
                        selectedEntityId: 7001,
                        operationId: "ux08-retest-published",
                        out int publishedRetestFlushCount,
                        out diagnostic);
                PublishedRetestHash = publishedRetest.InstalledAuthorityHash;
                PublishedRetestSucceeded = publishedRetest.Success
                    && publishedRetest.PublishedContentPristine
                    && publishedRetest.RestartedFromFrameZero
                    && publishedRetest.ContextRestored
                    && publishedRetestFlushCount == 1;
                if (!PublishedRetestSucceeded)
                {
                    diagnostic = "Published retest mismatch: success="
                        + publishedRetest.Success
                        + ", pristine=" + publishedRetest.PublishedContentPristine
                        + ", frame0=" + publishedRetest.RestartedFromFrameZero
                        + ", context=" + publishedRetest.ContextRestored
                        + ", flushCount=" + publishedRetestFlushCount
                        + ". " + publishedRetest.Message;
                    return false;
                }

                LastStage = "copy-experiment-a";
                string mainWorkspaceId = _sandbox.Persistence.Workspace.WorkspaceId;
                string mainWorkspaceHash = string.Empty;
                string publishedHashBeforeExperiment = _sandbox.Published.CaptureContentHash();
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult copied =
                    _sandbox.Roles.InitializeExperimentFromMain(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult switched =
                    copied.Success
                        ? _sandbox.Roles.Switch(
                            CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA)
                        : copied;
                ExperimentCopied = copied.Success
                    && switched.Success
                    && _sandbox.Roles.ActiveRole ==
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA;
                if (!ExperimentCopied)
                {
                    diagnostic = switched.Message;
                    return false;
                }
                // Creating an Experiment is allowed to flush Main and refresh save metadata.
                // Freeze the Main file footprint only after the copy/switch transaction, then
                // prove subsequent Experiment editing and testing cannot mutate it.
                mainWorkspaceHash = _sandbox.HashWorkspace(mainWorkspaceId);

                LastStage = "modify-test-experiment-a";
                if (!RuntimeAiLabAuthoringUxFlowSelection.TrySelect(
                        _sandbox.Persistence.Workspace,
                        GroupDraftId,
                        selection.EntryId,
                        out RuntimeAiLabAuthoringUxFlowSelection experimentSelection,
                        out diagnostic)
                    || !experimentSelection.TryToggleFixtureField(
                        _sandbox.Persistence.Workspace,
                        out experimentSelection,
                        out diagnostic))
                {
                    return false;
                }
                _sandbox.Persistence.MarkDirty();
                if (!_sandbox.Persistence.Flush())
                {
                    diagnostic = _sandbox.Persistence.LastStatus;
                    return false;
                }
                CharacterTestRuntimeAiLabTestCurrentChangesResult experimentTest =
                    RunTestCurrentChanges(
                        experimentSelection,
                        publishedContentPristine: false,
                        selectedEntityId: 7001,
                        operationId: "ux08-test-experiment-a",
                        out int experimentFlushCount,
                        out diagnostic);
                ExperimentModifiedAndTested = experimentTest.Success
                    && experimentFlushCount == 1;
                ExperimentPublishUnavailable =
                    !CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA).CanPublish
                    && _sandbox.Published.WriteCount == 1;
                MainUnchangedByExperiment = string.Equals(
                    mainWorkspaceHash,
                    _sandbox.HashWorkspace(mainWorkspaceId),
                    StringComparison.Ordinal);
                PublishedUnchangedByExperiment = string.Equals(
                    publishedHashBeforeExperiment,
                    _sandbox.Published.CaptureContentHash(),
                    StringComparison.Ordinal);
                if (!ExperimentModifiedAndTested
                    || !ExperimentPublishUnavailable
                    || !MainUnchangedByExperiment
                    || !PublishedUnchangedByExperiment)
                {
                    diagnostic = "Experiment mismatch: testSuccess="
                        + experimentTest.Success
                        + ", flushCount=" + experimentFlushCount
                        + ", publishUnavailable=" + ExperimentPublishUnavailable
                        + ", mainUnchanged=" + MainUnchangedByExperiment
                        + ", publishedUnchanged=" + PublishedUnchangedByExperiment
                        + ". " + experimentTest.Message;
                    return false;
                }

                LastStage = "return-main-reopen";
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult returnMain =
                    _sandbox.Roles.Switch(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main);
                ReturnedToMain = returnMain.Success
                    && string.Equals(
                        returnMain.Workspace.WorkspaceId,
                        mainWorkspaceId,
                        StringComparison.Ordinal);
                if (!ReturnedToMain)
                {
                    diagnostic = returnMain.Message;
                    return false;
                }
                if (!_sandbox.TryReopenRoles(out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult reopened))
                {
                    diagnostic = reopened.Message;
                    return false;
                }
                ReopenedOnMain = reopened.Success
                    && _sandbox.Roles.ActiveRole ==
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                    && string.Equals(
                        reopened.Workspace.WorkspaceId,
                        mainWorkspaceId,
                        StringComparison.Ordinal);
                if (!ReopenedOnMain)
                {
                    diagnostic = reopened.Message;
                    return false;
                }

                LastStage = "production-footprint";
                _productionAfterCore = RuntimeAiLabProductionFootprint.Capture();
                if (!_productionAfterCore.IsValid
                    || !_productionBefore.Equals(_productionAfterCore))
                {
                    diagnostic = _productionAfterCore.IsValid
                        ? "Production Draft, Published Catalog, or active role changed."
                        : _productionAfterCore.Diagnostic;
                    return false;
                }

                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal bool TryValidateCore(out string diagnostic)
        {
            RuntimeAiLabProductionFootprint current = RuntimeAiLabProductionFootprint.Capture();
            bool valid = CoreRunCount == 1
                && MainOpened
                && FixtureSelected
                && DraftModified
                && DraftSaved
                && DraftFlushCount == 1
                && TestCurrentChangesFlushCount == 1
                && TestCurrentChangesSucceeded
                && TestRestartedFromFrameZero
                && TestContextRestored
                && NoModificationReturnSucceeded
                && CharacterActionNoModificationReturnSucceeded
                && BusyNavigationFailedClosed
                && PublishPreviewCancelledWithoutWrite
                && PublishConfirmedOnce
                && PublishCheckpointSaved
                && ContinuePreservedRuntimeIdentity
                && PublishedRetestSucceeded
                && ExperimentCopied
                && ExperimentModifiedAndTested
                && ExperimentPublishUnavailable
                && MainUnchangedByExperiment
                && PublishedUnchangedByExperiment
                && ReturnedToMain
                && ReopenedOnMain
                && SourceCardRevision > 0
                && PublishedCardRevision > SourceCardRevision
                && DraftSemanticHash != 0L
                && PublishedRetestHash != 0L
                && TempPublishedWriteCount == 1
                && AllWritableAuthoritiesUseTemp
                && _productionBefore.IsValid
                && _productionAfterCore.IsValid
                && current.IsValid
                && _productionBefore.Equals(_productionAfterCore)
                && _productionBefore.Equals(current);
            if (valid)
            {
                diagnostic =
                    "Verified one Temp publication, Main/Experiment isolation, F0 transaction " +
                    "contracts, stable Group/Card/Timeline/Event identity, and unchanged production hashes.";
                return true;
            }

            diagnostic = "The independent UX-08 core probe did not observe every required invariant.";
            return false;
        }

        internal bool TryValidateScenario(out string diagnostic)
        {
            if (InvocationCount != 1
                || !LiveSceneReady
                || !LivePanelReady
                || !string.Equals(FinalStage, "core-complete", StringComparison.Ordinal))
            {
                diagnostic =
                    "The live Trigger function did not execute exactly once against the expected scene/panel.";
                return false;
            }
            return TryValidateCore(out diagnostic);
        }

        public void Dispose()
        {
            _sandbox.Dispose();
        }

        private CharacterTestRuntimeAiLabTestCurrentChangesResult RunTestCurrentChanges(
            RuntimeAiLabAuthoringUxFlowSelection selection,
            bool publishedContentPristine,
            int selectedEntityId,
            string operationId,
            out int flushCount,
            out string diagnostic)
        {
            int localFlushCount = 0;
            flushCount = 0;
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                _sandbox.Persistence.Workspace;
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request =
                selection.CreateTestRequest(workspace, operationId);
            var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
            if (!tracker.TryBegin(request, out diagnostic))
                return default;

            bool Flush(out string message)
            {
                localFlushCount++;
                bool saved = _sandbox.Persistence.Flush();
                message = _sandbox.Persistence.LastStatus;
                return saved;
            }

            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action markRestarting)
            {
                markRestarting?.Invoke();
                long hash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(
                    selection.Card.LogicPayload);
                return new CharacterTestRuntimeAiLabTestCurrentChangesApplyResult(
                    success: hash != 0L,
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.None,
                    string.Empty,
                    "Scenario-owned authority baseline installed from frame 0.",
                    blockingDiagnosticCount: 0,
                    candidateAuthorityHash: hash,
                    installedAuthorityHash: hash,
                    selectedEntityId,
                    runtimeProfileSlot: 0,
                    firstVisibleFrame: 0,
                    restartedFromFrameZero: true,
                    contextRestored: true,
                    previousSessionRecovered: true,
                    publishedContentPristine);
            }

            CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                    tracker,
                    Flush,
                    Apply);
            flushCount = localFlushCount;
            diagnostic = result.Message;
            return result;
        }

        private bool TryValidateNavigationContracts(out string diagnostic)
        {
            int flushCount = 0;
            CharacterTestRuntimeAiLabNavigationExitResult clean =
                CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                    CharacterTestRuntimeAiLabNavigationExitIntent.WorkspaceBack,
                    new CharacterTestRuntimeAiLabNavigationExitState(
                        historicalReadOnly: false,
                        operationBusy: false,
                        busyStage: string.Empty,
                        draftDirty: false,
                        canWriteDraft: true,
                        characterActionOpen: false,
                        characterActionDirty: false),
                    () =>
                    {
                        flushCount++;
                        return new CharacterTestRuntimeAiLabNavigationFlushResult(true, string.Empty);
                    });
            NoModificationReturnSucceeded = clean.CanProceed
                && !clean.FlushAttempted
                && flushCount == 0;

            CharacterTestRuntimeAiLabNavigationExitResult actionReturn =
                CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                    CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn,
                    new CharacterTestRuntimeAiLabNavigationExitState(
                        historicalReadOnly: false,
                        operationBusy: false,
                        busyStage: string.Empty,
                        draftDirty: false,
                        canWriteDraft: true,
                        characterActionOpen: true,
                        characterActionDirty: false),
                    null);
            CharacterActionNoModificationReturnSucceeded = actionReturn.CanProceed
                && !actionReturn.FlushAttempted
                && !actionReturn.Feedback.IsVisible;

            CharacterTestRuntimeAiLabNavigationExitResult busy =
                CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                    CharacterTestRuntimeAiLabNavigationExitIntent.TerminalClose,
                    new CharacterTestRuntimeAiLabNavigationExitState(
                        historicalReadOnly: false,
                        operationBusy: true,
                        busyStage: "测试当前修改",
                        draftDirty: false,
                        canWriteDraft: true,
                        characterActionOpen: false,
                        characterActionDirty: false),
                    null);
            BusyNavigationFailedClosed =
                busy.Disposition == CharacterTestRuntimeAiLabNavigationExitDisposition.Busy
                && !busy.CanProceed
                && busy.Feedback.IsVisible;

            bool valid = NoModificationReturnSucceeded
                && CharacterActionNoModificationReturnSucceeded
                && BusyNavigationFailedClosed;
            diagnostic = valid
                ? string.Empty
                : "Navigation policy did not preserve clean return or busy fail-closed semantics.";
            return valid;
        }
    }

    internal sealed class RuntimeAiLabAuthoringUxFlowAssertion : ITriggerTestAssertion
    {
        internal static readonly RuntimeAiLabAuthoringUxFlowAssertion Instance =
            new RuntimeAiLabAuthoringUxFlowAssertion();

        private RuntimeAiLabAuthoringUxFlowAssertion()
        {
        }

        public string AssertionId => "runtime-ai-lab-authoring-ux-flow-probe";

        public TriggerTestOperationResult Evaluate(in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(out RuntimeAiLabAuthoringUxFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab authoring UX flow probe is unavailable.");
            }

            return probe.TryValidateScenario(out string diagnostic)
                ? TriggerTestOperationResult.Succeeded(diagnostic)
                : TriggerTestOperationResult.Failed(diagnostic);
        }
    }
}
