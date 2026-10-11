using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestReplayTests
    {
        [Test]
        public void ReplayRecorder_RecordsCommandCopiesAndEmptyPlaybackPasses()
        {
            var recorder = new CharacterTestReplayRecorder(CreateConfig());
            recorder.Begin(RuntimeFrame.Zero, null);
            var commands = new List<RuntimeCommand>
            {
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    RuntimeFrame.Zero,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    targetEntityId: 7,
                    Vector2.right,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(986647))
            };

            recorder.RecordFrame(RuntimeFrame.Zero, commands, resultHash: 123L, diagnosticsSummary: "copy");
            commands[0] = CharacterTestRuntimeCommandFactory.SetControlIntent(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 99,
                Vector2.left,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                traceId: new RuntimeTraceIdentity(916807));

            CharacterTestReplayCapture capture = recorder.CreateCapture();

            Assert.AreEqual(1, capture.FrameCount);
            Assert.AreEqual(CharacterTestReplayRecorder.SchemaVersion, capture.RuntimeReplaySnapshot.Header.SchemaVersion);
            StringAssert.StartsWith(
                CharacterTestReplayRecorder.FrameworkVersion,
                capture.RuntimeReplaySnapshot.Header.FrameworkVersion);
            Assert.AreEqual(7, capture.RuntimeReplaySnapshot.Records[0].Commands[0].TargetId);

            var emptyRecorder = new CharacterTestReplayRecorder(CreateConfig());
            CharacterTestReplayCapture empty = emptyRecorder.Begin(RuntimeFrame.Zero, null);
            RuntimeReplayPlaybackResult result = CharacterTestReplayPlayback.Play(empty);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(0, result.FramesPlayed);
        }

        [Test]
        public void ReplayCapture_AuthoritativeCommandsExposeImmutableView()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();

            Assert.IsFalse(capture.AuthoritativeCommands is RuntimeCommand[]);
            var mutableView = capture.AuthoritativeCommands as IList<RuntimeCommand>;
            Assert.IsNotNull(mutableView);
            Assert.Throws<NotSupportedException>(() => mutableView[0] = default);
        }

        [Test]
        public void ReplayRecordIdentity_DiagnosticsTextDoesNotChangeAuthorityHash()
        {
            CharacterTestReplayCapture source = CreateMoveStopReplay();
            var records = new List<RuntimeReplayFrameRecord>(source.RuntimeReplaySnapshot.Records);
            RuntimeReplayFrameRecord first = records[0];
            records[0] = new RuntimeReplayFrameRecord(
                first.Frame,
                first.Commands,
                first.ResultHash,
                first.DiagnosticsSummary + " display-only-change");
            CharacterTestReplayCapture changedDiagnostics = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(
                    source.RuntimeReplaySnapshot.Header,
                    records),
                source.BootstrapSnapshot,
                source.TickRate,
                source.StateSyncPolicy,
                source.AuthoritativeCommands,
                source.ReplaySliceFactory,
                source.SessionAuthorityBootstrapSnapshot,
                source.SessionAuthorityHash,
                source.SessionAuthorityParticipantFactory,
                source.SessionAuthorityIdentity,
                source.RecordedAtUtc);

            Assert.AreEqual(
                CharacterTestReplayRecordIdentity.Inspect(source).AuthorityRecordHash,
                CharacterTestReplayRecordIdentity.Inspect(changedDiagnostics).AuthorityRecordHash);
        }

        [Test]
        public void ReplayValidation_RejectsPlayablePackageResourceIdentityMismatchBeforeBootstrap()
        {
            CharacterTestReplayCapture source = CreateMoveStopReplay();
            RuntimeReplayHeader header = source.RuntimeReplaySnapshot.Header;
            var mismatchedHeader = new RuntimeReplayHeader(
                header.SchemaVersion,
                header.FrameworkVersion,
                header.ConfigHash,
                "mismatch:" + header.ResourceCatalogHash,
                header.StartFrame);
            var mutated = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(
                    mismatchedHeader,
                    source.RuntimeReplaySnapshot.Records),
                source.BootstrapSnapshot,
                source.TickRate,
                source.StateSyncPolicy,
                source.AuthoritativeCommands,
                source.ReplaySliceFactory,
                source.SessionAuthorityBootstrapSnapshot,
                source.SessionAuthorityHash,
                source.SessionAuthorityParticipantFactory,
                source.SessionAuthorityIdentity,
                source.RecordedAtUtc);

            CharacterTestReplayRecordIdentity identity =
                CharacterTestReplayRecordIdentity.Inspect(mutated);
            CharacterTestReplayPlaybackStatus validation =
                CharacterTestReplayPlayback.Validate(mutated);

            Assert.IsFalse(identity.IsValid);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.PlayableResourceCatalogMismatch,
                identity.Code);
            Assert.IsFalse(validation.Success);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.PlayableResourceCatalogMismatch,
                validation.ResultCode);
            StringAssert.Contains("playable package resource identity", validation.Summary);
        }

        [Test]
        public void ReplayPlayback_ReplaysMoveThenStopWithMatchingHashes()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();

            RuntimeReplayPlaybackResult result = CharacterTestReplayPlayback.Play(capture);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(capture.FrameCount, result.FramesPlayed);
            Assert.Greater(capture.FrameCount, 0);
        }

        [Test]
        public void ReplayV2_IsRejectedBeforePlaybackBecauseItStoresStringCharacterActionIds()
        {
            CharacterTestReplayCapture current = CreateMoveStopReplay();
            RuntimeReplayHeader sourceHeader = current.RuntimeReplaySnapshot.Header;
            var legacyRuntime = new RuntimeReplaySnapshot(
                new RuntimeReplayHeader(
                    CharacterTestReplayRecorder.LegacyCharacterActionSchemaVersion,
                    "character-test-replay-v0.1",
                    sourceHeader.ConfigHash,
                    sourceHeader.ResourceCatalogHash,
                    sourceHeader.StartFrame),
                current.RuntimeReplaySnapshot.Records);
            var legacy = new CharacterTestReplayCapture(
                legacyRuntime,
                current.BootstrapSnapshot,
                current.TickRate,
                current.StateSyncPolicy,
                current.AuthoritativeCommands,
                current.ReplaySliceFactory,
                current.SessionAuthorityBootstrapSnapshot,
                current.SessionAuthorityHash,
                current.SessionAuthorityParticipantFactory,
                current.SessionAuthorityIdentity,
                current.RecordedAtUtc);

            RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(legacy);
            CharacterTestReplayPlaybackStatus validation = CharacterTestReplayPlayback.Validate(legacy);

            Assert.IsFalse(playback.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.DriverException, playback.FailureCode);
            Assert.AreEqual(0, playback.FramesPlayed);
            Assert.IsFalse(playback.HasFailureFrame);
            Assert.IsInstanceOf<NotSupportedException>(playback.Exception);
            StringAssert.Contains("legacy string CharacterAction IDs", playback.FailureMessage);
            Assert.IsFalse(validation.Success);
            Assert.AreEqual(CharacterTestReplayValidationCodes.ReplaySchemaUnsupported, validation.ResultCode);
            StringAssert.Contains("legacy string CharacterAction IDs", validation.Summary);
        }

        [Test]
        public void AuthorityPoseSchema33_SeveredBootstrapRollbackPlusOneCommandHashesAndRestoresSilently()
        {
            CharacterTestReplayCapture capture = CreateSeveredRollbackPlusOneReplay(
                out int tailDefinitionIndex,
                out long tailRevision);

            Assert.AreEqual(
                CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                capture.BootstrapSnapshot.SchemaVersion);
            JObject payload = JObject.Parse(capture.BootstrapSnapshot.PayloadJson);
            var bodyPartState = (JObject)((JArray)payload["bodyPartStates"])[0];
            var tail = (JObject)((JArray)bodyPartState["entries"])[tailDefinitionIndex];
            Assert.AreEqual(
                (int)CharacterTestBodyPartStructureState.Severed,
                (int)tail["structureState"]);
            Assert.AreEqual(tailRevision, (long)tail["revision"]);
            Assert.IsNull(bodyPartState["transitions"],
                "One-shot transition events are not replay/bootstrap authority fields.");
            var poseState = (JObject)((JArray)payload["authorityPoseTerrainStates"])[0];
            Assert.IsTrue((bool)poseState["isConfigured"]);
            Assert.AreEqual(4, ((JArray)poseState["limbs"]).Count);
            Assert.IsNull(poseState["resolvedFrame"],
                "recomputable resolved mid/tip samples are not replay/bootstrap authority fields");
            Assert.AreEqual(1, capture.FrameCount);
            Assert.AreEqual(1, capture.AuthoritativeCommandCount);

            RuntimeReplayFrameRecord record = capture.RuntimeReplaySnapshot.Records[0];
            using (var driver = new CharacterTestReplayFrameDriver(capture))
            {
                driver.Reset(capture.RuntimeReplaySnapshot.Header);
                Assert.AreEqual(1, driver.Slice.CharacterEntities.Count);
                CharacterTestCharacterEntity restored = driver.Slice.CharacterEntities[0];
                Assert.IsTrue(restored.BodyPartState.TryGetCurrentEntry(
                    tailDefinitionIndex,
                    out CharacterTestBodyPartStateEntry restoredTail));
                Assert.AreEqual(
                    CharacterTestBodyPartStructureState.Severed,
                    restoredTail.StructureState);
                Assert.AreEqual(tailRevision, restoredTail.Revision);
                Assert.AreEqual(0, restored.BodyPartState.TransitionCount,
                    "Rollback/bootstrap restore must not replay the transient sever event.");
                Assert.IsTrue(restored.TerrainIkState.IsConfigured);
                Assert.AreEqual(4, restored.TerrainIkState.LimbCount);
                Assert.IsFalse(restored.ResolvedAuthorityPoseFrame.IsPublished,
                    "resolved frame is derived and must start cleared after replay restore");

                RuntimeReplayPlaybackFrameResult plusOne = driver.RunFrame(record);
                Assert.AreEqual(0, plusOne.CommandErrors.Count);
                Assert.AreEqual(record.ResultHash, plusOne.ActualResultHash);
                Assert.AreEqual(0, restored.BodyPartState.TransitionCount);
            }

            CharacterTestReplayPlaybackStatus validation =
                CharacterTestReplayPlayback.Validate(capture);
            Assert.IsTrue(validation.Success, validation.Summary);
            Assert.AreEqual(CharacterTestReplayValidationCodes.Passed, validation.ResultCode);
            Assert.AreEqual(1, validation.FrameCount);
            Assert.AreEqual(validation.ExpectedResultHash, validation.ActualResultHash);
        }

        [TestCase("future")]
        [TestCase("envelope")]
        public void AuthorityPoseSchema33_GameplayBootstrapSchemaMismatchFailsClosed(string mutation)
        {
            CharacterTestReplayCapture source = CreateMoveStopReplay();
            RuntimeCustomState sourceBootstrap = source.BootstrapSnapshot;
            int mutatedSchema = mutation == "future"
                ? CharacterTestNetworkSnapshotParticipant.SchemaVersionValue + 1
                : CharacterTestNetworkSnapshotParticipant.BodyPartStateSchemaVersionValue;
            var mutatedBootstrap = new RuntimeCustomState(
                sourceBootstrap.NumericTypeId,
                sourceBootstrap.TypeId,
                mutatedSchema,
                sourceBootstrap.PayloadJson);
            CharacterTestReplayCapture mutated = CloneGameplayEnvelope(source, mutatedBootstrap);

            CharacterTestReplayPlaybackStatus validation =
                CharacterTestReplayPlayback.Validate(mutated);

            Assert.IsFalse(validation.Success);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed,
                validation.ResultCode);
            StringAssert.Contains("schema", validation.Summary.ToLowerInvariant());
        }

        [Test]
        public void ReplayPlayback_HashMismatchReportsCharacterTestFrame()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();
            var records = new List<RuntimeReplayFrameRecord>(capture.RuntimeReplaySnapshot.Records);
            RuntimeReplayFrameRecord last = records[records.Count - 1];
            records[records.Count - 1] = new RuntimeReplayFrameRecord(
                last.Frame,
                last.Commands,
                last.ResultHash + 1L,
                last.DiagnosticsSummary);
            var mutatedRuntime = new RuntimeReplaySnapshot(capture.RuntimeReplaySnapshot.Header, records);
            var mutated = new CharacterTestReplayCapture(
                mutatedRuntime,
                capture.BootstrapSnapshot,
                capture.TickRate,
                capture.StateSyncPolicy,
                capture.AuthoritativeCommands,
                capture.ReplaySliceFactory,
                capture.SessionAuthorityBootstrapSnapshot,
                capture.SessionAuthorityHash,
                capture.SessionAuthorityParticipantFactory,
                capture.SessionAuthorityIdentity,
                capture.RecordedAtUtc);

            RuntimeReplayPlaybackResult result = CharacterTestReplayPlayback.Play(mutated);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, result.FailureCode);
            Assert.AreEqual(last.Frame, result.FailureFrame);
        }

        [Test]
        public void ReplayValidationKernel_SynchronousAndIncrementalResultsMatch()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();
            CharacterTestReplayPlaybackStatus synchronous = CharacterTestReplayPlayback.Validate(capture);

            CharacterTestReplayValidationResult incremental;
            using (var kernel = new CharacterTestReplayValidationComparisonKernel(capture))
            {
                Assert.AreEqual(CharacterTestReplayValidationState.Ready, kernel.Inspect().State);
                Assert.IsTrue(kernel.RestoreSessionAuthorityBootstrap().Success);
                Assert.IsTrue(kernel.RestoreGameplayBootstrap().Success);
                while (!kernel.IsTerminal)
                    kernel.CompareNextFrame();
                incremental = kernel.TerminalResult;
            }

            Assert.IsTrue(synchronous.Success, synchronous.Summary);
            Assert.IsTrue(incremental.Success, incremental.Summary);
            Assert.AreEqual(synchronous.ResultCode, incremental.Code);
            Assert.AreEqual(capture.FrameCount, incremental.FramesCompared);
            RuntimeReplayFrameRecord finalRecord =
                capture.RuntimeReplaySnapshot.Records[capture.FrameCount - 1];
            Assert.AreEqual(finalRecord.ResultHash, incremental.ExpectedHash);
            Assert.AreEqual(finalRecord.ResultHash, incremental.ActualHash);
        }

        [Test]
        public void ReplayValidationKernel_AggregateMismatchReportsFirstFrame()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();
            var records = new List<RuntimeReplayFrameRecord>(capture.RuntimeReplaySnapshot.Records);
            RuntimeReplayFrameRecord first = records[0];
            records[0] = new RuntimeReplayFrameRecord(
                first.Frame,
                first.Commands,
                first.ResultHash + 1L,
                first.DiagnosticsSummary);
            var mutated = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(capture.RuntimeReplaySnapshot.Header, records),
                capture.BootstrapSnapshot,
                capture.TickRate,
                capture.StateSyncPolicy,
                capture.AuthoritativeCommands,
                capture.ReplaySliceFactory,
                capture.SessionAuthorityBootstrapSnapshot,
                capture.SessionAuthorityHash,
                capture.SessionAuthorityParticipantFactory,
                capture.SessionAuthorityIdentity,
                capture.RecordedAtUtc);

            using (var kernel = new CharacterTestReplayValidationComparisonKernel(mutated))
            {
                CharacterTestReplayValidationResult result = kernel.RunToCompletion();

                Assert.AreEqual(CharacterTestReplayValidationState.Diverged, result.State);
                Assert.AreEqual(CharacterTestReplayValidationCodes.FrameHashDiverged, result.Code);
                Assert.AreEqual(first.Frame, result.FailureFrame);
                Assert.AreEqual(first.ResultHash + 1L, result.ExpectedHash);
            }
        }

        [Test]
        public void ReplayValidationKernel_CancelKeepsCaptureRetryable()
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();
            using (var cancelledKernel = new CharacterTestReplayValidationComparisonKernel(capture))
            {
                Assert.AreEqual(CharacterTestReplayValidationState.Ready, cancelledKernel.Inspect().State);
                Assert.IsTrue(cancelledKernel.RestoreSessionAuthorityBootstrap().Success);
                Assert.IsTrue(cancelledKernel.RestoreGameplayBootstrap().Success);

                CharacterTestReplayValidationResult cancelled = cancelledKernel.Cancel();

                Assert.AreEqual(CharacterTestReplayValidationState.Cancelled, cancelled.State);
                Assert.AreEqual(CharacterTestReplayValidationCodes.OperationCancelled, cancelled.Code);
                Assert.IsFalse(cancelled.Success);
            }

            using (var retryKernel = new CharacterTestReplayValidationComparisonKernel(capture))
            {
                CharacterTestReplayValidationResult retry = retryKernel.RunToCompletion();
                Assert.IsTrue(retry.Success, retry.Summary);
            }
        }

        [Test]
        public void ReplayDivergenceProjection_AddsWarningOverlayWithoutMutatingSource()
        {
            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[5];
            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i] = new CharacterTestRuntimeAiLabTimelineTrackProjection(
                    (CharacterTestRuntimeAiLabTimelineTrackKind)i,
                    ((CharacterTestRuntimeAiLabTimelineTrackKind)i).ToString(),
                    Array.Empty<CharacterTestRuntimeAiLabTimelineItem>());
            }
            var source = new CharacterTestRuntimeAiLabTimelineProjection(
                7L,
                new CharacterTestRuntimeAiLabTimelineDomain(10, 10, 12, 12, 3, 3),
                new CharacterTestRuntimeAiLabTimelineViewport(10, 12, 0, 1200f, false),
                tracks);
            var result = new CharacterTestReplayValidationResult
            {
                State = CharacterTestReplayValidationState.Diverged,
                Code = CharacterTestReplayValidationCodes.FrameHashDiverged,
                Summary = "diverged",
                HasFailureFrame = true,
                FailureFrame = new RuntimeFrame(11),
                ExpectedHash = 10L,
                ActualHash = 20L
            };

            CharacterTestRuntimeAiLabTimelineProjection overlay =
                CharacterTestRuntimeAiPlannerDevSceneController.BuildReplayDivergenceProjection(
                    source,
                    result,
                    operationId: 3L);

            Assert.AreEqual(0, source.Tracks[4].Items.Count);
            Assert.AreEqual(1, overlay.Tracks[4].Items.Count);
            Assert.AreEqual(11, overlay.Domain.SelectedFrame);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineSeverity.Error, overlay.Tracks[4].Items[0].Severity);
            StringAssert.Contains("FRAME_HASH_DIVERGED", overlay.Tracks[4].Items[0].Tooltip);
        }

        [TestCase("generation")]
        [TestCase("authority")]
        public void ReplayTimelineSidecar_RejectsStaleGenerationAndPerFrameAuthorityMismatch(
            string mismatch)
        {
            CharacterTestReplayCapture capture = CreateMoveStopReplay();
            CharacterTestReplayRecordIdentity identity = CharacterTestReplayRecordIdentity.Inspect(capture);
            Assert.IsTrue(identity.IsValid, identity.Message);
            CharacterTestRuntimeAiPlannerDevTimeline timeline =
                BuildReplayEvidenceTimeline(capture, mismatch == "authority");
            CharacterTestRuntimeAiLabTimelineProjection source =
                BuildReplayEvidenceProjection(identity, mismatch == "generation");

            CharacterTestRuntimeAiLabTimelineProjection result =
                CharacterTestRuntimeAiPlannerDevSceneController.BuildReplayTimelineSidecarProjection(
                    source,
                    timeline,
                    capture,
                    identity,
                    out string diagnostic);

            Assert.IsNull(result);
            StringAssert.Contains(
                mismatch == "authority" ? "authority hash mismatch" : "provenance is stale",
                diagnostic);
            using (var kernel = new CharacterTestReplayValidationComparisonKernel(capture))
            {
                Assert.AreEqual(
                    CharacterTestReplayValidationState.Passed,
                    kernel.RunToCompletion().State,
                    "Presentation-sidecar rejection must not corrupt the independent authority record.");
            }
        }

        [Test]
        public void ReplayCausalEvidenceHash_TracksFactCandidateCommitAndOverflowContent()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                BuildEmptyEvidenceProjection();
            CharacterTestRuntimeAiLabReplayCausalSnapshot baseline =
                CreateCausalEvidenceSnapshot(1, 10, 100, 5);
            long baselineHash =
                CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineEvidenceHash(
                    projection,
                    new[] { baseline });

            Assert.AreNotEqual(baselineHash,
                CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineEvidenceHash(
                    projection,
                    new[] { CreateCausalEvidenceSnapshot(2, 10, 100, 5) }));
            Assert.AreNotEqual(baselineHash,
                CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineEvidenceHash(
                    projection,
                    new[] { CreateCausalEvidenceSnapshot(1, 11, 100, 5) }));
            Assert.AreNotEqual(baselineHash,
                CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineEvidenceHash(
                    projection,
                    new[] { CreateCausalEvidenceSnapshot(1, 10, 101, 5) }));
            Assert.AreNotEqual(baselineHash,
                CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineEvidenceHash(
                    projection,
                    new[] { CreateCausalEvidenceSnapshot(1, 10, 100, 6) }));
        }

        [Test]
        public void ReplayValidationInspection_RejectsEmptyAndDuplicateFramesBeforeBootstrap()
        {
            var empty = new CharacterTestReplayRecorder(CreateConfig()).Begin(RuntimeFrame.Zero, null);
            CharacterTestReplayRecordIdentity emptyIdentity = CharacterTestReplayRecordIdentity.Inspect(empty);
            Assert.AreEqual(CharacterTestReplayValidationCodes.ReplayEmpty, emptyIdentity.Code);

            CharacterTestReplayCapture valid = CreateMoveStopReplay();
            RuntimeReplayFrameRecord first = valid.RuntimeReplaySnapshot.Records[0];
            var duplicated = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(
                    valid.RuntimeReplaySnapshot.Header,
                    new[] { first, first }),
                valid.BootstrapSnapshot,
                valid.TickRate,
                valid.StateSyncPolicy,
                Array.Empty<RuntimeCommand>(),
                valid.ReplaySliceFactory,
                valid.SessionAuthorityBootstrapSnapshot,
                valid.SessionAuthorityHash,
                valid.SessionAuthorityParticipantFactory,
                valid.SessionAuthorityIdentity,
                valid.RecordedAtUtc);
            CharacterTestReplayRecordIdentity duplicateIdentity =
                CharacterTestReplayRecordIdentity.Inspect(duplicated);

            Assert.AreEqual(
                CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                duplicateIdentity.Code);

            var overflow = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(
                    new RuntimeReplayHeader(
                        valid.RuntimeReplaySnapshot.Header.SchemaVersion,
                        valid.RuntimeReplaySnapshot.Header.FrameworkVersion,
                        valid.RuntimeReplaySnapshot.Header.ConfigHash,
                        valid.RuntimeReplaySnapshot.Header.ResourceCatalogHash,
                        new RuntimeFrame((long)int.MaxValue + 1L)),
                    new[]
                    {
                        new RuntimeReplayFrameRecord(
                            new RuntimeFrame((long)int.MaxValue + 1L),
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)
                    }),
                valid.BootstrapSnapshot,
                valid.TickRate,
                valid.StateSyncPolicy,
                Array.Empty<RuntimeCommand>(),
                valid.ReplaySliceFactory,
                valid.SessionAuthorityBootstrapSnapshot,
                valid.SessionAuthorityHash,
                valid.SessionAuthorityParticipantFactory,
                valid.SessionAuthorityIdentity,
                valid.RecordedAtUtc);
            CharacterTestReplayRecordIdentity overflowIdentity =
                CharacterTestReplayRecordIdentity.Inspect(overflow);

            Assert.AreEqual(
                CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                overflowIdentity.Code);
        }

        [Test]
        public void ReplayValidationInspection_RequiresAuthorityBootstrapFactoryAndIdentity()
        {
            CharacterTestReplayCapture valid = CreateMoveStopReplay();
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.None,
                CharacterTestReplayRecordIdentity.Inspect(valid).Code,
                "An empty authority roster is valid only when its full identity envelope is present.");

            CharacterTestReplayCapture missingBootstrap = CloneAuthorityEnvelope(
                valid,
                authorityBootstrap: null,
                factory: valid.SessionAuthorityParticipantFactory,
                identity: valid.SessionAuthorityIdentity,
                authorityHash: valid.SessionAuthorityHash);
            CharacterTestReplayCapture missingFactory = CloneAuthorityEnvelope(
                valid,
                valid.SessionAuthorityBootstrapSnapshot,
                factory: null,
                identity: valid.SessionAuthorityIdentity,
                authorityHash: valid.SessionAuthorityHash);
            CharacterTestReplayCapture missingIdentity = CloneAuthorityEnvelope(
                valid,
                valid.SessionAuthorityBootstrapSnapshot,
                valid.SessionAuthorityParticipantFactory,
                identity: null,
                authorityHash: valid.SessionAuthorityHash);
            var mismatchedIdentity = new CharacterTestReplaySessionAuthorityIdentity(
                valid.SessionAuthorityIdentity.CatalogId,
                valid.SessionAuthorityIdentity.CatalogRevision,
                valid.SessionAuthorityIdentity.CatalogHash,
                valid.SessionAuthorityIdentity.RuntimeContractVersion,
                valid.SessionAuthorityHash + 1L,
                Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>());
            CharacterTestReplayCapture hashMismatch = CloneAuthorityEnvelope(
                valid,
                valid.SessionAuthorityBootstrapSnapshot,
                valid.SessionAuthorityParticipantFactory,
                mismatchedIdentity,
                valid.SessionAuthorityHash);

            Assert.AreEqual(
                CharacterTestReplayValidationCodes.SessionAuthorityBootstrapMissing,
                CharacterTestReplayRecordIdentity.Inspect(missingBootstrap).Code);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                CharacterTestReplayRecordIdentity.Inspect(missingFactory).Code);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.CatalogIdentityMissing,
                CharacterTestReplayRecordIdentity.Inspect(missingIdentity).Code);
            Assert.AreEqual(
                CharacterTestReplayValidationCodes.SessionAuthorityHashMismatch,
                CharacterTestReplayRecordIdentity.Inspect(hashMismatch).Code);
        }

        [Test]
        public void ReplayValidationBootstrap_RejectsTamperedCatalogIdentity()
        {
            CharacterTestReplayCapture valid = CreateMoveStopReplay();
            CharacterTestReplaySessionAuthorityIdentity expected = valid.SessionAuthorityIdentity;
            var tamperedIdentity = new CharacterTestReplaySessionAuthorityIdentity(
                expected.CatalogId,
                expected.CatalogRevision,
                expected.CatalogHash + 1L,
                expected.RuntimeContractVersion,
                expected.AuthorityHash,
                Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>());
            CharacterTestReplayCapture tampered = CloneAuthorityEnvelope(
                valid,
                valid.SessionAuthorityBootstrapSnapshot,
                valid.SessionAuthorityParticipantFactory,
                tamperedIdentity,
                valid.SessionAuthorityHash);

            using (var kernel = new CharacterTestReplayValidationComparisonKernel(tampered))
            {
                Assert.AreEqual(CharacterTestReplayValidationState.Ready, kernel.Inspect().State);
                CharacterTestReplayBootstrapPhaseResult result =
                    kernel.RestoreSessionAuthorityBootstrap();

                Assert.IsFalse(result.Success);
                Assert.AreEqual(CharacterTestReplayValidationCodes.CatalogHashMismatch, result.Code);
            }
        }

        [TestCase("revision")]
        [TestCase("content")]
        [TestCase("deck")]
        public void ReplayValidationBootstrap_RejectsTamperedPinnedCardAndDeckIdentity(string field)
        {
            CharacterTestReplayCapture valid = CreateMoveStopReplayWithPinnedCard();
            var payload = JObject.Parse(valid.SessionAuthorityBootstrapSnapshot.PayloadJson);
            var locks = (JArray)payload.GetValue("Locks", StringComparison.OrdinalIgnoreCase);
            var deck = (JObject)locks?[0];
            var cards = (JArray)deck?.GetValue("Cards", StringComparison.OrdinalIgnoreCase);
            var card = (JObject)cards?[0];
            Assert.IsNotNull(deck);
            Assert.IsNotNull(card);
            if (field == "revision")
                SetJsonNumber(card, "Revision", 1L);
            else if (field == "content")
                SetJsonNumber(card, "ContentHash", 1L);
            else
                SetJsonNumber(deck, "DeckHash", 1L);

            var tamperedBootstrap = new RuntimeCustomState(
                valid.SessionAuthorityBootstrapSnapshot.NumericTypeId,
                valid.SessionAuthorityBootstrapSnapshot.TypeId,
                valid.SessionAuthorityBootstrapSnapshot.SchemaVersion,
                payload.ToString(Formatting.None));
            CharacterTestReplayCapture tampered = CloneAuthorityEnvelope(
                valid,
                tamperedBootstrap,
                valid.SessionAuthorityParticipantFactory,
                valid.SessionAuthorityIdentity,
                valid.SessionAuthorityHash);

            using (var kernel = new CharacterTestReplayValidationComparisonKernel(tampered))
            {
                Assert.AreEqual(CharacterTestReplayValidationState.Ready, kernel.Inspect().State);
                CharacterTestReplayBootstrapPhaseResult result =
                    kernel.RestoreSessionAuthorityBootstrap();
                Assert.IsFalse(result.Success, field + " tamper must fail closed.");
                Assert.AreEqual(
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    result.Code);
            }
        }

        [Test]
        public void ReplayBootstrap_FirstRecordedFrameSnapshotMatchesSource()
        {
            using (var source = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            {
                Tick(source, 8);
                CharacterTestCharacterEntity entity = FindPlayer(source);
                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                var recorder = new CharacterTestReplayRecorder(CreateConfig());
                recorder.Begin(source.CurrentFrame, sourceParticipant.CaptureSnapshot(source.CurrentFrame));
                TickRecorded(
                    source,
                    sourceParticipant,
                    recorder,
                    entity.Id.Value,
                    Vector2.right,
                    new RuntimeTraceIdentity(1201811));

                var frame = new RuntimeFrame(source.CurrentFrame.Value - 1L);
                RuntimeCustomState expected = sourceParticipant.CaptureSnapshot(frame);
                CharacterTestReplayCapture capture = recorder.CreateCapture();
                RuntimeReplayFrameRecord record = capture.RuntimeReplaySnapshot.Records[0];
                using (var driver = new CharacterTestReplayFrameDriver(capture))
                {
                    driver.Reset(capture.RuntimeReplaySnapshot.Header);
                    driver.RunFrame(record);
                    CharacterTestNetworkSnapshotParticipant actualParticipant =
                        driver.Slice.Services.Network.CreateSnapshotParticipant();
                    RuntimeCustomState actual = actualParticipant.CaptureSnapshot(frame);

                    Assert.AreEqual(expected.PayloadJson, actual.PayloadJson);
                }
            }
        }

        [Test]
        public void LoopbackReplay_RecordsServerAuthoritativeStopIntentAndPlaysBack()
        {
            using (var runner = new CharacterTestNetworkLoopbackRunner(
                NullRuntimeLogger.Instance,
                config: CreateConfig(),
                transportOptions: LoopbackNetworkTransportOptions.ReliableImmediate,
                createServerViews: false))
            {
                Tick(runner, 30);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = runner.CaptureDiagnostics();
                Assert.AreEqual(NetworkSessionStatus.Connected, diagnostics.ClientStatus);
                Assert.Greater(diagnostics.OwnedEntityId, 0);

                CharacterTestReplayPlaybackStatus recording = runner.BeginReplayRecording();
                Assert.AreEqual("RECORDING", recording.ResultCode);

                RuntimeFrame moveFrame = runner.ServerSlice.CurrentFrame;
                runner.SubmitClientCommand(CharacterTestRuntimeCommandFactory.SetControlIntent(
                    moveFrame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    diagnostics.OwnedEntityId,
                    Vector2.right,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(929698)));
                Tick(runner, 8);

                RuntimeFrame stopFrame = runner.ServerSlice.CurrentFrame;
                runner.SubmitClientCommand(CharacterTestRuntimeCommandFactory.SetControlIntent(
                    stopFrame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    diagnostics.OwnedEntityId,
                    Vector2.zero,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(910429)));
                Tick(runner, 16);

                CharacterTestReplayCapture capture = runner.CreateReplayCapture();
                Assert.Greater(capture.AuthoritativeCommandCount, 0);
                Assert.IsTrue(
                    capture.HasZeroControlIntentAfter(stopFrame, diagnostics.OwnedEntityId),
                    "Expected a server-authoritative zero movement command in replay.");

                RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);
                Assert.IsTrue(playback.Success, playback.FailureMessage);
            }
        }

        [Test]
        public void NetworkSessionReplay_FreezesPhysicsAtBeginRecordingAndReusesItForRetry()
        {
            var world = new CombatPhysicsWorld();
            var frozenBodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart);
            var laterBodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 1);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(100),
                frozenBodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                frozenBodyId,
                new CombatColliderId(1),
                0,
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                CombatSurfaceMaterial.Default));
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(0),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "replay-freeze-test");
            var transport = new LoopbackNetworkTransport();
            using (var runner = new CharacterTestNetworkSessionRunner(
                transport,
                transport,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                config: CreateConfig(),
                createServerViews: false))
            {
                Assert.AreEqual("RECORDING", runner.BeginReplayRecording().ResultCode);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(101),
                    laterBodyId,
                    new FixVector3(Fix64.One, Fix64.One, Fix64.One)));
                runner.Tick(1d / 60d);
                runner.Tick(1d / 60d);
                CharacterTestReplayCapture capture = runner.CreateReplayCapture();

                using (GameSlice firstAttempt = capture.ReplaySliceFactory())
                using (GameSlice retry = capture.ReplaySliceFactory())
                {
                    Assert.IsTrue(firstAttempt.PhysicsRuntime.World.TryGetBody(frozenBodyId, out _));
                    Assert.IsFalse(firstAttempt.PhysicsRuntime.World.TryGetBody(laterBodyId, out _));
                    Assert.IsTrue(retry.PhysicsRuntime.World.TryGetBody(frozenBodyId, out _));
                    Assert.IsFalse(retry.PhysicsRuntime.World.TryGetBody(laterBodyId, out _));
                }
            }
        }

        [Test]
        public void LockstepSeed_DualHeadlessSlicesProduceMatchingIntegerSnapshotsAndHashes()
        {
            using (var left = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            using (var right = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            {
                Tick(left, 8);
                Tick(right, 8);

                CharacterTestCharacterEntity leftEntity = FindPlayer(left);
                CharacterTestCharacterEntity rightEntity = FindPlayer(right);
                Assert.AreEqual(
                    leftEntity.Id.Value,
                    rightEntity.Id.Value,
                    "Headless lockstep slices must allocate matching deterministic entity ids.");

                var leftParticipant = new CharacterTestNetworkSnapshotParticipant(
                    left.CharacterRuntime,
                    left.RuntimeCommands);
                var rightParticipant = new CharacterTestNetworkSnapshotParticipant(
                    right.CharacterRuntime,
                    right.RuntimeCommands);

                for (int step = 0; step < 90; step++)
                {
                    RuntimeFrame frame = left.CurrentFrame;
                    Assert.AreEqual(frame, right.CurrentFrame, "Lockstep slices must stay on the same frame.");

                    SubmitDeterministicLockstepIntent(left, frame, leftEntity.Id.Value, step);
                    SubmitDeterministicLockstepIntent(right, frame, rightEntity.Id.Value, step);
                    left.Tick(1d / 60d);
                    right.Tick(1d / 60d);

                    long leftHash = leftParticipant.ComputeStateHash(frame);
                    long rightHash = rightParticipant.ComputeStateHash(frame);
                    Assert.AreEqual(leftHash, rightHash, "State hash diverged at step " + step + ".");

                    RuntimeCustomState leftSnapshot = leftParticipant.CaptureSnapshot(frame);
                    RuntimeCustomState rightSnapshot = rightParticipant.CaptureSnapshot(frame);
                    Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue, leftSnapshot.SchemaVersion);
                    Assert.AreEqual(leftSnapshot.PayloadJson, rightSnapshot.PayloadJson);
                    AssertIntegerSnapshotMatchesEntity(leftSnapshot, leftEntity);
                }
            }
        }

        private static CharacterTestReplayCapture CreateMoveStopReplay()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreateCatalog(
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
                        out _,
                        out string catalogError),
                    catalogError);
                var authority = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
                    catalog,
                    runtimeContractVersion: 1);
                var recorder = new CharacterTestReplayRecorder(CreateConfig());
                recorder.Begin(
                    slice.CurrentFrame,
                    participant.CaptureSnapshot(slice.CurrentFrame),
                    authority.CaptureSnapshot(slice.CurrentFrame),
                    authority.AuthorityHash,
                    authority.CreateReplayReplica,
                    authority.CaptureReplayIdentity());

                for (int i = 0; i < 30; i++)
                    TickRecorded(
                        slice,
                        participant,
                        recorder,
                        entity.Id.Value,
                        Vector2.right,
                        new RuntimeTraceIdentity(1201812),
                        authority.AuthorityHash);
                RuntimeFrame stopSearchFrame = slice.CurrentFrame;
                for (int i = 0; i < 30; i++)
                    TickRecorded(
                        slice,
                        participant,
                        recorder,
                        entity.Id.Value,
                        Vector2.zero,
                        new RuntimeTraceIdentity(1201813),
                        authority.AuthorityHash);

                CharacterTestReplayCapture capture = recorder.CreateCapture();
                Assert.IsTrue(capture.HasZeroControlIntentAfter(stopSearchFrame, entity.Id.Value));
                return capture;
            }
        }

        private static CharacterTestReplayCapture CreateSeveredRollbackPlusOneReplay(
            out int tailDefinitionIndex,
            out long tailRevision)
        {
            using (GameSlice slice = CreateDragonReplaySlice(spawnDragon: true))
            {
                Tick(slice, 8);
                Assert.AreEqual(1, slice.CharacterEntities.Count);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                tailDefinitionIndex = FindBodyPartDefinitionIndex(
                    entity.BodyPartState,
                    CharacterTestAuthoredContentIds.D400_Id43);
                long breakFrame = slice.CurrentFrame.Value;
                CommitBodyPartDamage(entity, breakFrame, tailDefinitionIndex, eventKey: 801);
                CommitBodyPartSever(entity, breakFrame + 1L, tailDefinitionIndex, correlationId: 802L);
                while (slice.CurrentFrame.Value < breakFrame + 1L)
                    slice.Tick(1d / 60d);

                Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(
                    tailDefinitionIndex,
                    out CharacterTestBodyPartStateEntry severedTail));
                Assert.AreEqual(
                    CharacterTestBodyPartStructureState.Severed,
                    severedTail.StructureState);
                tailRevision = severedTail.Revision;

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreateCatalog(
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
                        out _,
                        out string catalogError),
                    catalogError);
                var authority = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
                    catalog,
                    runtimeContractVersion: 1);
                var recorder = new CharacterTestReplayRecorder(CreateConfig());
                recorder.Begin(
                    slice.CurrentFrame,
                    participant.CaptureSnapshot(slice.CurrentFrame),
                    authority.CaptureSnapshot(slice.CurrentFrame),
                    authority.AuthorityHash,
                    authority.CreateReplayReplica,
                    authority.CaptureReplayIdentity());
                TickRecorded(
                    slice,
                    participant,
                    recorder,
                    entity.Id.Value,
                    Vector2.zero,
                    new RuntimeTraceIdentity(1201814),
                    authority.AuthorityHash);
                return recorder.CreateCapture(() => CreateDragonReplaySlice(spawnDragon: false));
            }
        }

        private static CharacterTestReplayCapture CreateMoveStopReplayWithPinnedCard()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreateCatalog(
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
                        out _,
                        out string catalogError),
                    catalogError);
                Assert.Greater(catalog.Cards.Count, 0);
                string stableCardId = catalog.Cards[0].StableCardId;
                CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlockedResult =
                    CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                        new[] { stableCardId });
                Assert.IsTrue(unlockedResult.Success);
                var authority = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
                    catalog,
                    runtimeContractVersion: 1);
                var context = new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                    "peer.local",
                    new[] { "character.hero" },
                    unlockedResult.Collection,
                    catalog,
                    1);
                var request = new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                    "peer.local",
                    "character.hero",
                    1,
                    catalog.CatalogId,
                    catalog.CatalogNumericId,
                    catalog.Revision,
                    catalog.CatalogHash,
                    catalog.NumericIdentityHash,
                    new[] { catalog.Cards[0].NumericId },
                    new[] { stableCardId });
                CharacterTestRuntimeAiPlannerSessionAuthorityResult lockResult =
                    authority.TryLock(request, context);
                Assert.IsTrue(lockResult.Success);
                Assert.AreEqual(1, authority.SessionLocks.Count);

                var recorder = new CharacterTestReplayRecorder(CreateConfig());
                recorder.Begin(
                    slice.CurrentFrame,
                    participant.CaptureSnapshot(slice.CurrentFrame),
                    authority.CaptureSnapshot(slice.CurrentFrame),
                    authority.AuthorityHash,
                    authority.CreateReplayReplica,
                    authority.CaptureReplayIdentity());
                for (int i = 0; i < 4; i++)
                {
                    TickRecorded(
                        slice,
                        participant,
                        recorder,
                        entity.Id.Value,
                        Vector2.right,
                        new RuntimeTraceIdentity(1201815),
                        authority.AuthorityHash);
                }
                return recorder.CreateCapture();
            }
        }

        private static CharacterTestRuntimeAiPlannerDevTimeline BuildReplayEvidenceTimeline(
            CharacterTestReplayCapture capture,
            bool tamperFirstAuthorityHash)
        {
            IReadOnlyList<RuntimeReplayFrameRecord> replayRecords =
                capture.RuntimeReplaySnapshot.Records;
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(
                Math.Max(4, replayRecords.Count + 2));
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(
                1,
                (int)replayRecords[0].Frame.Value + 1,
                Math.Max(4, replayRecords.Count + 2)));
            for (int i = 0; i < replayRecords.Count; i++)
            {
                RuntimeReplayFrameRecord replay = replayRecords[i];
                int replayFrame = (int)replay.Frame.Value;
                long replayHash = replay.ResultHash;
                if (tamperFirstAuthorityHash && i == 0)
                    replayHash++;
                timeline.AddRecord(
                    replayFrame + 1,
                    0f,
                    0d,
                    null,
                    Array.Empty<RuntimeCommand>(),
                    null,
                    replay.ResultHash,
                    hasReplayAuthorityEvidence: true,
                    replayAuthorityFrame: replayFrame,
                    replayAuthorityHash: replayHash);
            }
            return timeline;
        }

        private static CharacterTestRuntimeAiLabTimelineProjection BuildReplayEvidenceProjection(
            CharacterTestReplayRecordIdentity identity,
            bool staleSessionGeneration)
        {
            int liveFrame = identity.StartFrame + 1;
            var item = new CharacterTestRuntimeAiLabTimelineItem(
                "test.replay.provenance",
                CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                CharacterTestRuntimeAiLabTimelineItemProvenance.TypedEvent,
                liveFrame,
                liveFrame,
                false,
                1,
                0,
                "test",
                CharacterTestRuntimeAiLabTimelineSeverity.Info,
                "test",
                "test",
                1,
                "trace.test",
                0,
                sessionGeneration: staleSessionGeneration ? 2 : 1);
            var track = new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                "Decision",
                new[] { item });
            int latest = identity.EndFrame + 1;
            return new CharacterTestRuntimeAiLabTimelineProjection(
                1L,
                new CharacterTestRuntimeAiLabTimelineDomain(
                    identity.StartFrame,
                    identity.StartFrame,
                    latest,
                    latest,
                    identity.FrameCount + 1,
                    identity.FrameCount),
                new CharacterTestRuntimeAiLabTimelineViewport(
                    identity.StartFrame,
                    latest,
                    0,
                    1200f,
                    false),
                new[] { track });
        }

        private static CharacterTestRuntimeAiLabTimelineProjection BuildEmptyEvidenceProjection()
        {
            return new CharacterTestRuntimeAiLabTimelineProjection(
                1L,
                new CharacterTestRuntimeAiLabTimelineDomain(1, 1, 2, 2, 2, 2),
                new CharacterTestRuntimeAiLabTimelineViewport(1, 2, 0, 1200f, false),
                new[]
                {
                    new CharacterTestRuntimeAiLabTimelineTrackProjection(
                        CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                        "Decision",
                        Array.Empty<CharacterTestRuntimeAiLabTimelineItem>())
                });
        }

        private static CharacterTestRuntimeAiLabReplayCausalSnapshot CreateCausalEvidenceSnapshot(
            int factValue,
            int candidatePriority,
            int commitActionId,
            int overflowActual)
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1, 1, 2, 2);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(
                1L,
                2,
                out CharacterTestRuntimeAiLabDecisionTraceHandle handle,
                false,
                2);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(7);
            var commit = new CharacterTestRuntimeAiLabCommitTrace
            {
                PlannerLogicKind = CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed,
                CommandEnqueueKind = CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued,
                CardStableId = "card.test",
                StageStableId = "stage.test",
                CommandActionId = commitActionId
            };
            agent.SetCommit(commit);
            var fact = new CharacterTestRuntimeAiLabFactObservationTrace
            {
                StableId = "fact.test",
                Domain = CharacterTestRuntimeAiLabDecisionTraceFactDomain.RuntimeAiPlanner,
                Value = CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(factValue),
                ObservedFrame = 2
            };
            Assert.IsTrue(agent.AddFact(fact));
            var candidate = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "candidate.test",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected,
                PriorityAvailable = true,
                Priority = candidatePriority
            };
            Assert.IsTrue(agent.AddCandidate(candidate));
            agent.RecordOverflow(
                CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.CandidateFactLinks,
                overflowActual,
                4);
            writer.Seal();
            return new CharacterTestRuntimeAiLabReplayCausalSnapshot(
                1,
                11L,
                0L,
                1,
                2,
                2,
                1,
                7,
                1L,
                22L,
                33L,
                store,
                handle);
        }

        private static GameSlice CreateDragonReplaySlice(bool spawnDragon)
        {
            CharacterTestCharacterSpawnSpec[] startup = spawnDragon
                ? new[]
                {
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 1,
                        x: 0f,
                        y: 1f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.LocalPlayer)
                }
                : Array.Empty<CharacterTestCharacterSpawnSpec>();
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startup,
                SuppressMissingStartupSpawnWarning = true
            });
        }

        private static int FindBodyPartDefinitionIndex(
            CharacterTestBodyPartStateComponent component,
            int partStateId)
        {
            for (int i = 0; i < component.DefinitionCount; i++)
            {
                Assert.IsTrue(component.TryGetDefinition(
                    i,
                    out CharacterTestBodyPartStateDefinition definition));
                if (definition.PartStateId == partStateId)
                    return i;
            }
            Assert.Fail("Missing body-part state definition: " + partStateId);
            return -1;
        }

        private static void CommitBodyPartDamage(
            CharacterTestCharacterEntity entity,
            long frame,
            int definitionIndex,
            int eventKey)
        {
            Assert.IsTrue(entity.BodyPartState.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                entity.BodyPartState.StageAcceptedDamage(
                    new CharacterTestBodyPartAcceptedDamageFact(
                        authorityFrame: frame,
                        sourceEntityId: 99,
                        targetEntityId: entity.Id.Value,
                        actionInstanceId: eventKey,
                        policyEventId: eventKey,
                        traceId: eventKey,
                        localFrame: eventKey,
                        definitionIndex,
                        finalDamage: int.MaxValue)));
            CharacterTestBodyPartStateCommitResult result = entity.BodyPartState.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, result.Status);
            Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(
                definitionIndex,
                out CharacterTestBodyPartStateEntry broken));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, broken.StructureState);
        }

        private static void CommitBodyPartSever(
            CharacterTestCharacterEntity entity,
            long frame,
            int definitionIndex,
            long correlationId)
        {
            Assert.IsTrue(entity.BodyPartState.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                entity.BodyPartState.StageSever(new CharacterTestBodyPartSeverFact(
                    authorityFrame: frame,
                    sourceEntityId: 99,
                    targetEntityId: entity.Id.Value,
                    definitionIndex,
                    severTag: "action.000003.tail.sever",
                    correlationId)));
            CharacterTestBodyPartStateCommitResult result = entity.BodyPartState.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, result.Status);
            Assert.AreEqual(1, result.TransitionCount);
            Assert.AreEqual(
                CharacterTestBodyPartTransitionReason.Severed,
                entity.BodyPartState.GetTransitionAt(0).Reason);
        }

        private static CharacterTestReplayCapture CloneGameplayEnvelope(
            CharacterTestReplayCapture source,
            RuntimeCustomState gameplayBootstrap)
        {
            return new CharacterTestReplayCapture(
                source.RuntimeReplaySnapshot,
                gameplayBootstrap,
                source.TickRate,
                source.StateSyncPolicy,
                source.AuthoritativeCommands,
                source.ReplaySliceFactory,
                source.SessionAuthorityBootstrapSnapshot,
                source.SessionAuthorityHash,
                source.SessionAuthorityParticipantFactory,
                source.SessionAuthorityIdentity,
                source.RecordedAtUtc);
        }

        private static CharacterTestReplayCapture CloneAuthorityEnvelope(
            CharacterTestReplayCapture source,
            RuntimeCustomState authorityBootstrap,
            Func<INetworkSnapshotParticipant> factory,
            CharacterTestReplaySessionAuthorityIdentity identity,
            long authorityHash)
        {
            return new CharacterTestReplayCapture(
                source.RuntimeReplaySnapshot,
                source.BootstrapSnapshot,
                source.TickRate,
                source.StateSyncPolicy,
                source.AuthoritativeCommands,
                source.ReplaySliceFactory,
                authorityBootstrap,
                authorityHash,
                factory,
                identity,
                source.RecordedAtUtc);
        }

        private static void SetJsonNumber(JObject value, string propertyName, long delta)
        {
            JProperty property = value.Property(
                propertyName,
                StringComparison.OrdinalIgnoreCase);
            Assert.IsNotNull(property, propertyName);
            property.Value = property.Value.Value<long>() + delta;
        }

        private static void SubmitDeterministicLockstepIntent(
            GameSlice slice,
            RuntimeFrame frame,
            int entityId,
            int step)
        {
            CharacterTestControlIntent intent = CreateDeterministicIntent(entityId, step);
            RuntimeCommandValidationResult result = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    intent,
                    traceId: new RuntimeTraceIdentity(996651, 0, 0, step)));
            Assert.IsTrue(result.Success, result.Error.Message);
        }

        private static CharacterTestControlIntent CreateDeterministicIntent(int entityId, int step)
        {
            short moveX;
            short moveY;
            switch ((step / 10) % 6)
            {
                case 0:
                    moveX = 1000;
                    moveY = 0;
                    break;
                case 1:
                    moveX = 500;
                    moveY = 500;
                    break;
                case 2:
                    moveX = 0;
                    moveY = 1000;
                    break;
                case 3:
                    moveX = -750;
                    moveY = 250;
                    break;
                case 4:
                    moveX = 250;
                    moveY = -750;
                    break;
                default:
                    moveX = 0;
                    moveY = 0;
                    break;
            }

            bool jumpPressed = step == 18;
            bool jumpHeld = step >= 18 && step <= 28;
            bool jumpReleased = step == 29;
            return CharacterTestControlIntent.FromDeterministic(
                entityId,
                moveX,
                moveY,
                moveX,
                moveY,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed: false);
        }

        private static void AssertIntegerSnapshotMatchesEntity(
            RuntimeCustomState snapshot,
            CharacterTestCharacterEntity entity)
        {
            CharacterTestNetworkSnapshotDocument document =
                JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(snapshot.PayloadJson);
            Assert.NotNull(document);
            Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue, document.SchemaVersion);
            Assert.NotNull(document.Characters);
            CharacterTestNetworkCharacterSnapshot character = null;
            for (int i = 0; i < document.Characters.Length; i++)
            {
                if (document.Characters[i].EntityId == entity.Id.Value)
                {
                    character = document.Characters[i];
                    break;
                }
            }

            Assert.NotNull(character);
            Assert.AreEqual(entity.Transform.XMillimeters, character.XMillimeters);
            Assert.AreEqual(entity.Transform.YMillimeters, character.YMillimeters);
            Assert.AreEqual(entity.Transform.ZMillimeters, character.ZMillimeters);
            Assert.AreEqual(entity.Transform.YawMilliDegrees, character.YawMilliDegrees);
            Assert.AreEqual(entity.Input.MoveXMilli, character.InputMoveXMilli);
            Assert.AreEqual(entity.Input.MoveYMilli, character.InputMoveYMilli);
        }

        private static void TickRecorded(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            CharacterTestReplayRecorder recorder,
            int entityId,
            Vector2 move,
            RuntimeTraceIdentity traceId,
            long? sessionAuthorityHash = null)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult result = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    entityId,
                    move,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    traceId: traceId));
            Assert.IsTrue(result.Success, result.Error.Message);

            slice.Tick(1d / 60d);
            long gameplayHash = participant.ComputeStateHash(frame);
            long hash = sessionAuthorityHash.HasValue
                ? CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(
                    gameplayHash,
                    sessionAuthorityHash.Value)
                : gameplayHash;
            recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                "local",
                frame,
                frame,
                slice.RuntimeCommands.LastDrainedCommands));
            recorder.RecordFrame(
                frame,
                slice.RuntimeCommands.LastDrainedCommands,
                hash,
                CharacterTestReplayDiagnostics.CreateFrameSummary(
                    frame,
                    slice.RuntimeCommands.LastDrainedCommands.Count,
                    slice.CharacterEntities.Count,
                    hash));
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / 60d);
        }

        private static void Tick(CharacterTestNetworkLoopbackRunner runner, int count)
        {
            for (int i = 0; i < count; i++)
                runner.Tick(1d / 60d);
        }

        private static CharacterTestCharacterEntity FindPlayer(GameSlice slice)
        {
            foreach (CharacterTestCharacterEntity entity in slice.CharacterEntities)
            {
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Expected CharacterTest player entity. Runtime errors: " + string.Join(" | ", System.Linq.Enumerable.Select(slice.CaptureDiagnostics().Errors, error => error.ModuleId + ": " + error.Message + " " + error.Exception.StackTrace)) + " Events: " + string.Join(" | ", slice.RecentRuntimeEvents) + " Story: " + JsonConvert.SerializeObject(slice.StorySnapshot) + " Resource: " + slice.Services.Modules.Resources.BootstrapErrorMessage);
            return null;
        }

        private static NetworkSessionConfig CreateConfig()
        {
            return new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 2,
                configHash: "character-test-replay",
                resourceCatalogHash:
                    CharacterTestNetworkSessionFactory.ComputeCurrentPlayableCharacterResourceCatalogHash(),
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
        }
    }
}
