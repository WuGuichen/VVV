using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNetworkAutomationTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";

        [Test]
        public void Request_RoundTripsThroughSharedStore()
        {
            string runId = "unit-request-" + Guid.NewGuid().ToString("N");
            CharacterTestNetworkAutomationReportStore.ClearRequest();
            CharacterTestNetworkAutomationReportStore.ClearReports(runId);

            var request = CharacterTestNetworkAutomationRequest.CreateDefault(runId);
            request.ExpectedPlayers = 1;
            request.Port = 0;
            request.Normalize();

            try
            {
                CharacterTestNetworkAutomationReportStore.SaveRequest(request);

                bool loaded = CharacterTestNetworkAutomationReportStore.TryLoadRequest(
                    out CharacterTestNetworkAutomationRequest loadedRequest);

                Assert.IsTrue(loaded);
                Assert.IsNotNull(loadedRequest);
                Assert.AreEqual(runId, loadedRequest.RunId);
                Assert.AreEqual(2, loadedRequest.ExpectedPlayers);
                Assert.AreEqual(7777, loadedRequest.Port);
                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, loadedRequest.LaunchMode);
            }
            finally
            {
                CharacterTestNetworkAutomationReportStore.ClearRequest();
                CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            }
        }

        [Test]
        public void ReportStore_WritesReadsAndOrdersReports()
        {
            string runId = "unit-report-" + Guid.NewGuid().ToString("N");
            CharacterTestNetworkAutomationReportStore.ClearReports(runId);

            try
            {
                CharacterTestNetworkAutomationReportStore.WriteReport(CreateReport(
                    runId,
                    CharacterTestNetworkAutomationRole.Client,
                    "client-b",
                    9));
                CharacterTestNetworkAutomationReportStore.WriteReport(CreateReport(
                    runId,
                    CharacterTestNetworkAutomationRole.Host,
                    "host-peer",
                    7));
                CharacterTestNetworkAutomationReportStore.WriteReport(CreateReport(
                    runId,
                    CharacterTestNetworkAutomationRole.Client,
                    "client-a",
                    8));

                IReadOnlyList<CharacterTestNetworkAutomationReport> reports =
                    CharacterTestNetworkAutomationReportStore.ReadReports(runId);

                Assert.AreEqual(3, reports.Count);
                Assert.AreEqual(CharacterTestNetworkAutomationRole.Host, reports[0].Role);
                Assert.AreEqual("host-peer", reports[0].PeerId);
                Assert.AreEqual(7, reports[0].OwnedEntityId);
                Assert.AreEqual(CharacterTestNetworkAutomationRole.Client, reports[1].Role);
                Assert.AreEqual("client-a", reports[1].PeerId);
                Assert.AreEqual(8, reports[1].OwnedEntityId);
                Assert.AreEqual(CharacterTestNetworkAutomationRole.Client, reports[2].Role);
                Assert.AreEqual("client-b", reports[2].PeerId);
                Assert.AreEqual(9, reports[2].OwnedEntityId);
            }
            finally
            {
                CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            }
        }

        [Test]
        public void Runner_MainEditorStartsHostReadiesEntersAndReportsPass()
        {
            string runId = "unit-host-" + Guid.NewGuid().ToString("N");
            CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            var request = CharacterTestNetworkAutomationRequest.CreateDefault(runId);
            request.TimeoutFrames = 120;
            var controller = new RecordingAutomationController();
            var context = new FakePlayerContext(isMainEditor: true);
            var runner = new CharacterTestNetworkAutomationRunner(
                request,
                controller,
                context,
                NullRuntimeLogger.Instance);

            try
            {
                for (int i = 0; i < 40 && runner.IsActive; i++)
                    runner.Tick();

                IReadOnlyList<CharacterTestNetworkAutomationReport> reports =
                    CharacterTestNetworkAutomationReportStore.ReadReports(runId);

                Assert.AreEqual(1, controller.StartCount);
                Assert.AreEqual(CharacterTestNetworkSessionKind.Host, controller.LastStartedOptions.Kind);
                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, controller.LastStartedOptions.LaunchMode);
                Assert.AreEqual(2, controller.LastStartedOptions.RoomMinReadyMembers);
                Assert.AreEqual(1, controller.ReadyCount);
                Assert.GreaterOrEqual(controller.EnterCount, 2);
                Assert.AreEqual(1, controller.StartProbeCount);
                Assert.AreEqual(1, controller.BeginReplayCount);
                Assert.AreEqual(1, controller.ValidateReplayCount);
                Assert.AreEqual(0, controller.StopCount, "Host should keep the room alive until peer reports arrive.");
                Assert.AreEqual(1, reports.Count);
                Assert.IsTrue(reports[0].Success);
                Assert.AreEqual(CharacterTestNetworkAutomationRole.Host, reports[0].Role);
                Assert.AreEqual(42, reports[0].OwnedEntityId);
                Assert.AreEqual("PASS", reports[0].ReplayResultCode);
                Assert.IsTrue(reports[0].ReplaySuccess);
                Assert.AreEqual(12, reports[0].ReplayFrameCount);
                Assert.AreEqual(3, reports[0].ReplayAuthoritativeCommandCount);
                Assert.IsTrue(context.ReportResultCalled);
                Assert.IsTrue(context.LastReportedCondition);
            }
            finally
            {
                CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            }
        }

        [Test]
        public void Runner_VirtualPlayerWaitsJoinDelayJoinsAndStopsAfterReport()
        {
            string runId = "unit-client-" + Guid.NewGuid().ToString("N");
            CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            var request = CharacterTestNetworkAutomationRequest.CreateDefault(runId);
            request.TimeoutFrames = 120;
            request.ClientJoinDelayFrames = 3;
            var controller = new RecordingAutomationController();
            var context = new FakePlayerContext(isMainEditor: false);
            var runner = new CharacterTestNetworkAutomationRunner(
                request,
                controller,
                context,
                NullRuntimeLogger.Instance);

            try
            {
                runner.Tick();
                runner.Tick();
                Assert.AreEqual(0, controller.StartCount);

                for (int i = 0; i < 40 && runner.IsActive; i++)
                    runner.Tick();

                IReadOnlyList<CharacterTestNetworkAutomationReport> reports =
                    CharacterTestNetworkAutomationReportStore.ReadReports(runId);

                Assert.AreEqual(1, controller.StartCount);
                Assert.AreEqual(CharacterTestNetworkSessionKind.Join, controller.LastStartedOptions.Kind);
                Assert.AreEqual("127.0.0.1", controller.LastStartedOptions.RemoteAddress);
                Assert.AreEqual(1, controller.ReadyCount);
                Assert.GreaterOrEqual(controller.EnterCount, 1);
                Assert.AreEqual(1, controller.StartProbeCount);
                Assert.AreEqual(1, controller.StopCount);
                Assert.AreEqual(1, reports.Count);
                Assert.IsTrue(reports[0].Success);
                Assert.AreEqual(CharacterTestNetworkAutomationRole.Client, reports[0].Role);
            }
            finally
            {
                CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            }
        }

        [UnityTest]
        public IEnumerator RoomHostAndOneVirtualPlayer_AutoSyncProbeAndExit()
        {
            string runId = "mppm-" + Guid.NewGuid().ToString("N");
            var request = CharacterTestNetworkAutomationRequest.CreateDefault(runId);
            CharacterTestNetworkAutomationReportStore.ClearReports(runId);
            CharacterTestNetworkAutomationReportStore.SaveRequest(request);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            IReadOnlyList<CharacterTestNetworkAutomationReport> reports =
                Array.Empty<CharacterTestNetworkAutomationReport>();
            bool hasHostAndClientReports = false;

            yield return new EnterPlayMode();

            for (int i = 0; i < request.TimeoutFrames + 600; i++)
            {
                reports = CharacterTestNetworkAutomationReportStore.ReadReports(runId);
                bool hasHost = reports.Any(report => report.Role == CharacterTestNetworkAutomationRole.Host);
                bool hasClient = reports.Any(report => report.Role == CharacterTestNetworkAutomationRole.Client);
                bool hasFailure = reports.Any(report => !report.Success);
                hasHostAndClientReports = hasHost && hasClient;
                if (hasHostAndClientReports || hasFailure)
                    break;
                yield return null;
            }

            yield return new ExitPlayMode();
            CharacterTestNetworkAutomationReportStore.ClearRequest();

            Assert.IsTrue(
                hasHostAndClientReports,
                "CharacterTest MPPM automation did not receive both Host and Client reports. "
                + "Select an MPPM Scenario with Main Editor Player + at least 1 Virtual Player before running this PlayMode test. "
                + FormatAutomationReports(reports));
            Assert.AreEqual(
                2,
                reports.Count(report => report.Success),
                FormatAutomationReports(reports));
            CharacterTestNetworkAutomationReport host =
                reports.First(report => report.Role == CharacterTestNetworkAutomationRole.Host);
            CharacterTestNetworkAutomationReport client =
                reports.First(report => report.Role == CharacterTestNetworkAutomationRole.Client);
            Assert.Greater(host.OwnedEntityId, 0);
            Assert.Greater(client.OwnedEntityId, 0);
            Assert.AreNotEqual(host.OwnedEntityId, client.OwnedEntityId);
            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Pass, host.SyncProbeResult);
            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Pass, client.SyncProbeResult);
            Assert.Greater(host.AuthoritativeCommandReceivedCount + host.AuthoritativeCommandEnqueuedCount, 0);
            Assert.Greater(client.AuthoritativeCommandReceivedCount + client.AuthoritativeCommandEchoCount, 0);
            Assert.AreEqual(0, host.SyncProbeLateCommandDelta);
            Assert.AreEqual(0, client.SyncProbeLateCommandDelta);
            Assert.AreEqual(0, host.SyncProbeDroppedCommandDelta);
            Assert.AreEqual(0, client.SyncProbeDroppedCommandDelta);
            Assert.AreEqual(0, host.SyncProbeFullSnapshotDelta);
            Assert.AreEqual(0, client.SyncProbeFullSnapshotDelta);
            Assert.AreEqual(0, host.SyncProbeDigestSnapshotDelta);
            Assert.AreEqual(0, client.SyncProbeDigestSnapshotDelta);
            Assert.AreEqual(0, host.SyncProbeSnapshotRequestDelta);
            Assert.AreEqual(0, client.SyncProbeSnapshotRequestDelta);
            Assert.AreEqual("PASS", host.ReplayResultCode);
            Assert.IsTrue(host.ReplaySuccess);
            Assert.Greater(host.ReplayFrameCount, 0);
            Assert.Greater(host.ReplayAuthoritativeCommandCount, 0);
        }

        private static string FormatAutomationReports(
            IReadOnlyList<CharacterTestNetworkAutomationReport> reports)
        {
            if (reports == null || reports.Count == 0)
                return "No automation reports were written.";

            return string.Join(
                " | ",
                reports.Select(report =>
                    report.Role
                    + " success="
                    + report.Success
                    + " code="
                    + report.ResultCode
                    + " probe="
                    + report.SyncProbeResultCode
                    + " owned="
                    + report.OwnedEntityId
                    + " room="
                    + report.RoomReadyCount
                    + "/"
                    + report.RoomConnectedCount
                    + " late="
                    + report.AuthoritativeCommandLateCount
                    + " drop="
                    + report.AuthoritativeCommandDroppedCount
                    + " full="
                    + (report.FullSnapshotSentCount + report.FullSnapshotReceivedCount)
                    + " probeFull="
                    + report.SyncProbeFullSnapshotDelta
                    + " probeDigest="
                    + report.SyncProbeDigestSnapshotDelta
                    + " snapReq="
                    + report.SnapshotRequestCount
                    + " probeSnapReq="
                    + report.SyncProbeSnapshotRequestDelta
                    + " replay="
                    + report.ReplayResultCode
                    + " summary="
                    + report.Summary));
        }

        private static CharacterTestNetworkAutomationReport CreateReport(
            string runId,
            CharacterTestNetworkAutomationRole role,
            string peerId,
            int ownedEntityId)
        {
            return new CharacterTestNetworkAutomationReport
            {
                RunId = runId,
                Role = role,
                PeerId = peerId,
                Success = true,
                Result = CharacterTestNetworkAutomationResult.Pass,
                ResultCode = "PASS",
                OwnedEntityId = ownedEntityId,
                SyncProbeResult = CharacterTestNetworkSyncProbeResult.Pass,
                SyncProbeResultCode = "PASS"
            };
        }

        private sealed class FakePlayerContext : ICharacterTestNetworkAutomationPlayerContext
        {
            public FakePlayerContext(bool isMainEditor)
            {
                IsMainEditor = isMainEditor;
                PlayerLabel = isMainEditor ? "MainEditor" : "VirtualPlayer-Test";
            }

            public bool IsAvailable => true;
            public bool IsMainEditor { get; }
            public string PlayerLabel { get; }
            public IReadOnlyList<string> Tags { get; } = Array.Empty<string>();
            public bool ReportResultCalled { get; private set; }
            public bool LastReportedCondition { get; private set; }

            public void ReportResult(bool condition, string message)
            {
                ReportResultCalled = true;
                LastReportedCondition = condition;
            }
        }

        private sealed class RecordingAutomationController :
            ICharacterTestNetworkAutomationController,
            ICharacterTestNetworkReplayController
        {
            private int _probeCaptureCount;

            public CharacterTestNetworkSessionOptions LastStartedOptions { get; private set; }
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int ReadyCount { get; private set; }
            public int EnterCount { get; private set; }
            public int StartProbeCount { get; private set; }
            public int BeginReplayCount { get; private set; }
            public int ValidateReplayCount { get; private set; }

            public CharacterTestNetworkSessionStatus Status { get; } =
                CharacterTestNetworkSessionStatus.Offline();

            public CharacterTestNetworkSyncProbeStatus ProbeStatus { get; private set; } =
                CharacterTestNetworkSyncProbeStatus.Idle(false);

            public CharacterTestReplayPlaybackStatus ReplayStatus { get; private set; } =
                CharacterTestReplayPlaybackStatus.Unavailable("Replay has not started.");

            public CharacterTestNetworkSessionOptions CreateDefaultNetworkSessionOptions(
                CharacterTestNetworkSessionKind kind)
            {
                return kind == CharacterTestNetworkSessionKind.Host
                    ? CharacterTestNetworkSessionOptions.HostDefaults()
                    : CharacterTestNetworkSessionOptions.JoinDefaults();
            }

            public CharacterTestNetworkSessionResult StartNetworkSession(
                CharacterTestNetworkSessionOptions options)
            {
                StartCount++;
                LastStartedOptions = options.Clone();
                Status.Kind = options.Kind;
                Status.LaunchMode = options.LaunchMode;
                Status.NetworkMode = CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    options.Kind,
                    options.LaunchMode);
                Status.ClientStatus = NetworkSessionStatus.Connected;
                Status.ServerStatus = options.Kind == CharacterTestNetworkSessionKind.Host
                    ? NetworkSessionStatus.Connected
                    : NetworkSessionStatus.Disconnected;
                Status.RoomStatus = NetworkRoomStatus.Open;
                Status.RoomConnectedCount = options.Kind == CharacterTestNetworkSessionKind.Host ? 2 : 1;
                Status.RoomReadyCount = 0;
                Status.CanSetRoomReady = true;
                Status.IsRoomHost = options.Kind == CharacterTestNetworkSessionKind.Host;
                Status.OwnedEntityId = 42;
                Status.FullSnapshotReceivedCount = 1;
                return CharacterTestNetworkSessionResult.Succeeded(
                    options.Kind,
                    Status.NetworkMode,
                    "Started.",
                    Status);
            }

            public CharacterTestNetworkSessionResult StopNetworkSession()
            {
                StopCount++;
                Status.Kind = CharacterTestNetworkSessionKind.Offline;
                Status.NetworkMode = CharacterTestNetworkMode.Disabled;
                return CharacterTestNetworkSessionResult.Succeeded(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    "Stopped.",
                    Status);
            }

            public CharacterTestNetworkSessionStatus CaptureNetworkSessionStatus()
            {
                Status.CanEnterGameplay = Status.RoomStatus == NetworkRoomStatus.InGame
                    && Status.ClientStatus == NetworkSessionStatus.Connected
                    && Status.OwnedEntityId > 0
                    && Status.FullSnapshotReceivedCount > 0;
                return Status;
            }

            public CharacterTestNetworkSessionResult SetLocalReady(bool ready)
            {
                ReadyCount++;
                Status.LocalRoomReady = ready;
                Status.RoomReadyCount = ready ? Math.Max(Status.RoomConnectedCount, 1) : 0;
                Status.CanStartRoomGame = Status.IsRoomHost && Status.RoomReadyCount >= Status.RoomConnectedCount;
                if (!Status.IsRoomHost)
                    Status.RoomStatus = NetworkRoomStatus.InGame;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    "Ready.",
                    Status);
            }

            public CharacterTestNetworkSessionResult StartRoomGame()
            {
                Status.RoomStatus = NetworkRoomStatus.InGame;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    "Started room.",
                    Status);
            }

            public CharacterTestNetworkSessionResult EnterNetworkGameplay()
            {
                EnterCount++;
                Status.RoomStatus = NetworkRoomStatus.InGame;
                Status.CanEnterGameplay = true;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    "Entered.",
                    Status);
            }

            public void SetNetworkSyncProbeAutoRun(bool enabled)
            {
            }

            public CharacterTestNetworkSyncProbeStatus StartNetworkSyncProbe()
            {
                StartProbeCount++;
                _probeCaptureCount = 0;
                if (Status.Kind == CharacterTestNetworkSessionKind.Host)
                    BeginNetworkReplayRecording();
                ProbeStatus = new CharacterTestNetworkSyncProbeStatus
                {
                    Result = CharacterTestNetworkSyncProbeResult.Running,
                    IsRunning = true,
                    CanStart = true,
                    ResultCode = "RUNNING",
                    Summary = "Sync Probe RUNNING: test"
                };
                return ProbeStatus;
            }

            public CharacterTestNetworkSyncProbeStatus StopNetworkSyncProbe()
            {
                ProbeStatus = new CharacterTestNetworkSyncProbeStatus
                {
                    Result = CharacterTestNetworkSyncProbeResult.Cancelled,
                    ResultCode = "CANCELLED",
                    Summary = "Sync Probe CANCELLED: test"
                };
                return ProbeStatus;
            }

            public CharacterTestNetworkSyncProbeStatus CaptureNetworkSyncProbeStatus()
            {
                if (ProbeStatus.IsRunning)
                {
                    _probeCaptureCount++;
                    if (_probeCaptureCount > 2)
                    {
                        ProbeStatus = new CharacterTestNetworkSyncProbeStatus
                        {
                            Result = CharacterTestNetworkSyncProbeResult.Pass,
                            IsRunning = false,
                            CanStart = true,
                            ResultCode = "PASS",
                            Summary = "Sync Probe PASS PASS: submitted=3 auth recv=3 echo=1 enq=2 late=0 drop=0 full=0 snapReq=0",
                            Detail = "cmdBytes=128 snapshotBytes=0",
                            SubmittedCommandCount = 3,
                            DeltaAuthoritativeCommandReceived = 3,
                            DeltaAuthoritativeCommandEcho = 1,
                            DeltaAuthoritativeCommandEnqueued = 2,
                            DeltaFullSnapshot = 0,
                            DeltaDigestSnapshot = 0,
                            DeltaSnapshotRequest = 0
                        };
                    }
                }

                return ProbeStatus;
            }

            public CharacterTestReplayPlaybackStatus BeginNetworkReplayRecording()
            {
                BeginReplayCount++;
                ReplayStatus = new CharacterTestReplayPlaybackStatus
                {
                    Available = true,
                    Recording = true,
                    ResultCode = "RECORDING",
                    Summary = "CharacterTest replay recording started. startFrame=0"
                };
                return ReplayStatus;
            }

            public CharacterTestReplayPlaybackStatus ValidateNetworkReplay()
            {
                ValidateReplayCount++;
                ReplayStatus = new CharacterTestReplayPlaybackStatus
                {
                    Available = true,
                    PlaybackRan = true,
                    Success = true,
                    ResultCode = "PASS",
                    Summary = "CharacterTest replay PASS: frames=12 authCommands=3",
                    FrameCount = 12,
                    AuthoritativeCommandCount = 3
                };
                return ReplayStatus;
            }

            public CharacterTestReplayPlaybackStatus CaptureNetworkReplayStatus()
            {
                return ReplayStatus;
            }
        }
    }
}
