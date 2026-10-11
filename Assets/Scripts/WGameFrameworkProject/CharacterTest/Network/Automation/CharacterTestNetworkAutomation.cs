using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using MxFramework.Network;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestNetworkAutomationRole
    {
        None = 0,
        Host = 1,
        Client = 2
    }

    public enum CharacterTestNetworkAutomationResult
    {
        None = 0,
        Running = 1,
        Pass = 2,
        Fail = 3
    }

    public sealed class CharacterTestNetworkAutomationRequest
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public bool Enabled { get; set; } = true;
        public string RunId { get; set; } = string.Empty;
        public int ExpectedPlayers { get; set; } = 2;
        public CharacterTestNetworkLaunchMode LaunchMode { get; set; } = CharacterTestNetworkLaunchMode.Room;
        public string BindAddress { get; set; } = "0.0.0.0";
        public string RemoteAddress { get; set; } = "127.0.0.1";
        public ushort Port { get; set; } = 7777;
        public string ServerPeerId { get; set; } = "server";
        public string RoomId { get; set; } = "character-test-automation-room";
        public string RoomName { get; set; } = "CharacterTest Automation Room";
        public int TimeoutFrames { get; set; } = 2400;
        public int ClientJoinDelayFrames { get; set; } = 30;
        public bool RequireProbePass { get; set; } = true;
        public long CreatedUtcTicks { get; set; }
        public long ExpiresUtcTicks { get; set; }

        [JsonIgnore]
        public bool IsExpired => ExpiresUtcTicks > 0L && DateTime.UtcNow.Ticks > ExpiresUtcTicks;

        public static CharacterTestNetworkAutomationRequest CreateDefault(string runId)
        {
            string normalizedRunId = string.IsNullOrWhiteSpace(runId)
                ? Guid.NewGuid().ToString("N")
                : runId.Trim();
            DateTime now = DateTime.UtcNow;
            return new CharacterTestNetworkAutomationRequest
            {
                RunId = normalizedRunId,
                RoomId = "character-test-automation-" + normalizedRunId,
                RoomName = "CharacterTest Automation " + normalizedRunId,
                CreatedUtcTicks = now.Ticks,
                ExpiresUtcTicks = now.AddMinutes(30d).Ticks
            }.Normalize();
        }

        public CharacterTestNetworkAutomationRequest Normalize()
        {
            if (SchemaVersion <= 0)
                SchemaVersion = CurrentSchemaVersion;
            RunId = string.IsNullOrWhiteSpace(RunId) ? Guid.NewGuid().ToString("N") : RunId.Trim();
            ExpectedPlayers = Mathf.Max(2, ExpectedPlayers);
            LaunchMode = CharacterTestNetworkLaunchMode.Room;
            BindAddress = string.IsNullOrWhiteSpace(BindAddress) ? "0.0.0.0" : BindAddress.Trim();
            RemoteAddress = string.IsNullOrWhiteSpace(RemoteAddress) ? "127.0.0.1" : RemoteAddress.Trim();
            Port = Port == 0 ? (ushort)7777 : Port;
            ServerPeerId = string.IsNullOrWhiteSpace(ServerPeerId) ? "server" : ServerPeerId.Trim();
            RoomId = string.IsNullOrWhiteSpace(RoomId) ? "character-test-automation-" + RunId : RoomId.Trim();
            RoomName = string.IsNullOrWhiteSpace(RoomName) ? "CharacterTest Automation " + RunId : RoomName.Trim();
            TimeoutFrames = Mathf.Max(60, TimeoutFrames);
            ClientJoinDelayFrames = Mathf.Max(0, ClientJoinDelayFrames);
            if (CreatedUtcTicks <= 0L)
                CreatedUtcTicks = DateTime.UtcNow.Ticks;
            if (ExpiresUtcTicks <= 0L)
                ExpiresUtcTicks = new DateTime(CreatedUtcTicks, DateTimeKind.Utc).AddMinutes(30d).Ticks;
            return this;
        }
    }

    public sealed class CharacterTestNetworkAutomationReport
    {
        public int SchemaVersion { get; set; } = CharacterTestNetworkAutomationRequest.CurrentSchemaVersion;
        public string RunId { get; set; } = string.Empty;
        public CharacterTestNetworkAutomationRole Role { get; set; }
        public string PlayerLabel { get; set; } = string.Empty;
        public string[] PlayerTags { get; set; } = Array.Empty<string>();
        public string PeerId { get; set; } = string.Empty;
        public CharacterTestNetworkAutomationResult Result { get; set; }
        public bool Success { get; set; }
        public string ResultCode { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public int ElapsedFrames { get; set; }
        public int OwnedEntityId { get; set; }
        public CharacterTestNetworkSessionKind SessionKind { get; set; }
        public CharacterTestNetworkMode NetworkMode { get; set; }
        public NetworkSessionStatus ServerStatus { get; set; }
        public NetworkSessionStatus ClientStatus { get; set; }
        public NetworkRoomStatus RoomStatus { get; set; }
        public int RoomReadyCount { get; set; }
        public int RoomConnectedCount { get; set; }
        public int RttEstimateFrames { get; set; }
        public int FrameDriftFrames { get; set; }
        public int MaxFrameDriftFrames { get; set; }
        public int SnapshotRequestCount { get; set; }
        public NetworkStateSyncPolicy StateSyncPolicy { get; set; }
        public int HeartbeatIntervalFrames { get; set; }
        public int AuthoritativeCommandSentCount { get; set; }
        public int AuthoritativeCommandReceivedCount { get; set; }
        public int AuthoritativeCommandEchoCount { get; set; }
        public int AuthoritativeCommandEnqueuedCount { get; set; }
        public int AuthoritativeCommandLateCount { get; set; }
        public int AuthoritativeCommandDroppedCount { get; set; }
        public int FullSnapshotSentCount { get; set; }
        public int FullSnapshotReceivedCount { get; set; }
        public int DigestSnapshotSentCount { get; set; }
        public int DigestSnapshotReceivedCount { get; set; }
        public int PendingTransportMessages { get; set; }
        public int MaxSentPayloadBytes { get; set; }
        public long SentBytes { get; set; }
        public long RuntimeCommandSentBytes { get; set; }
        public long SnapshotSentBytes { get; set; }
        public string SyncProbeResultCode { get; set; } = string.Empty;
        public CharacterTestNetworkSyncProbeResult SyncProbeResult { get; set; }
        public string SyncProbeSummary { get; set; } = string.Empty;
        public string SyncProbeDetail { get; set; } = string.Empty;
        public string RecentDiagnostics { get; set; } = string.Empty;
        public int SyncProbeSubmittedCommandCount { get; set; }
        public string SyncProbeInputPattern { get; set; } = string.Empty;
        public int SyncProbeZeroIntentCommandCount { get; set; }
        public int SyncProbeNonZeroIntentCommandCount { get; set; }
        public long SyncProbeLastZeroIntentCommandFrame { get; set; }
        public long SyncProbeLastNonZeroIntentCommandFrame { get; set; }
        public string SyncProbeLogPath { get; set; } = string.Empty;
        public bool SyncProbeLogWriteFailed { get; set; }
        public int SyncProbeLateCommandDelta { get; set; }
        public int SyncProbeDroppedCommandDelta { get; set; }
        public int SyncProbeFullSnapshotDelta { get; set; }
        public int SyncProbeDigestSnapshotDelta { get; set; }
        public int SyncProbeSnapshotRequestDelta { get; set; }
        public string ReplayResultCode { get; set; } = string.Empty;
        public bool ReplaySuccess { get; set; }
        public string ReplaySummary { get; set; } = string.Empty;
        public int ReplayFrameCount { get; set; }
        public int ReplayAuthoritativeCommandCount { get; set; }
        public long ReplayFailureFrame { get; set; }
        public long CompletedUtcTicks { get; set; }
    }

    public interface ICharacterTestNetworkAutomationPlayerContext
    {
        bool IsAvailable { get; }

        bool IsMainEditor { get; }

        string PlayerLabel { get; }

        IReadOnlyList<string> Tags { get; }

        void ReportResult(bool condition, string message);
    }

    public static class CharacterTestNetworkAutomationReportStore
    {
        public const string RequestPlayerPrefsKey =
            "MxFramework.CharacterTest.NetworkAutomation.RequestJson";

        private const string RequestFileName = "automation-request.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        public static string RootDirectory =>
            Path.Combine(Application.persistentDataPath, "CharacterTest", "NetworkAutomation");

        public static void SaveRequest(CharacterTestNetworkAutomationRequest request)
        {
            request = (request ?? CharacterTestNetworkAutomationRequest.CreateDefault(string.Empty)).Normalize();
            string json = JsonConvert.SerializeObject(request, JsonSettings);
            Directory.CreateDirectory(RootDirectory);
            WriteAllTextAtomic(Path.Combine(RootDirectory, RequestFileName), json);
            PlayerPrefs.SetString(RequestPlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        public static bool TryLoadRequest(out CharacterTestNetworkAutomationRequest request)
        {
            request = null;
            string json = TryReadRequestFile();
            if (string.IsNullOrWhiteSpace(json))
                json = PlayerPrefs.GetString(RequestPlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                request = JsonConvert.DeserializeObject<CharacterTestNetworkAutomationRequest>(json);
                request = request?.Normalize();
                return request != null && request.Enabled && !request.IsExpired;
            }
            catch (Exception)
            {
                request = null;
                return false;
            }
        }

        public static void ClearRequest()
        {
            PlayerPrefs.DeleteKey(RequestPlayerPrefsKey);
            PlayerPrefs.Save();
            string path = Path.Combine(RootDirectory, RequestFileName);
            if (File.Exists(path))
                File.Delete(path);
        }

        public static void ClearReports(string runId)
        {
            if (!Directory.Exists(RootDirectory))
                return;

            string prefix = SanitizeFilePart(runId);
            foreach (string path in Directory.GetFiles(RootDirectory, prefix + "-*.json"))
                File.Delete(path);
        }

        public static void WriteReport(CharacterTestNetworkAutomationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            Directory.CreateDirectory(RootDirectory);
            string path = GetReportPath(report.RunId, report.Role, report.PeerId);
            string json = JsonConvert.SerializeObject(report, JsonSettings);
            WriteAllTextAtomic(path, json);
        }

        public static IReadOnlyList<CharacterTestNetworkAutomationReport> ReadReports(string runId)
        {
            if (!Directory.Exists(RootDirectory))
                return Array.Empty<CharacterTestNetworkAutomationReport>();

            string prefix = SanitizeFilePart(runId);
            var reports = new List<CharacterTestNetworkAutomationReport>();
            foreach (string path in Directory.GetFiles(RootDirectory, prefix + "-*.json"))
            {
                if (Path.GetFileName(path).Equals(RequestFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    string json = File.ReadAllText(path);
                    CharacterTestNetworkAutomationReport report =
                        JsonConvert.DeserializeObject<CharacterTestNetworkAutomationReport>(json);
                    if (report != null && string.Equals(report.RunId, runId, StringComparison.Ordinal))
                        reports.Add(report);
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }
            }

            reports.Sort(CompareReports);
            return reports.Count == 0
                ? Array.Empty<CharacterTestNetworkAutomationReport>()
                : reports.ToArray();
        }

        private static string TryReadRequestFile()
        {
            string path = Path.Combine(RootDirectory, RequestFileName);
            if (!File.Exists(path))
                return string.Empty;

            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private static string GetReportPath(
            string runId,
            CharacterTestNetworkAutomationRole role,
            string peerId)
        {
            string fileName = SanitizeFilePart(runId)
                + "-"
                + role.ToString().ToLowerInvariant()
                + "-"
                + SanitizeFilePart(peerId)
                + ".json";
            return Path.Combine(RootDirectory, fileName);
        }

        private static void WriteAllTextAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tempPath = path
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            File.WriteAllText(tempPath, text ?? string.Empty);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }

        private static string SanitizeFilePart(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = new char[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                char ch = source[i];
                chars[i] = Array.IndexOf(invalid, ch) >= 0 ? '_' : ch;
            }

            string sanitized = new string(chars);
            return sanitized.Length > 80 ? sanitized.Substring(0, 80) : sanitized;
        }

        private static int CompareReports(
            CharacterTestNetworkAutomationReport left,
            CharacterTestNetworkAutomationReport right)
        {
            int role = left.Role.CompareTo(right.Role);
            return role != 0
                ? role
                : string.Compare(left.PeerId, right.PeerId, StringComparison.Ordinal);
        }
    }

    public sealed class CharacterTestNetworkAutomationPlayerContext :
        ICharacterTestNetworkAutomationPlayerContext
    {
        private readonly Type _currentPlayerType;
        private readonly PropertyInfo _isMainEditorProperty;
        private readonly MethodInfo _readOnlyTagsMethod;
        private readonly MethodInfo _reportResultMethod;

        public CharacterTestNetworkAutomationPlayerContext()
        {
            _currentPlayerType = ResolveCurrentPlayerType();
            if (_currentPlayerType == null)
                return;

            _isMainEditorProperty = _currentPlayerType.GetProperty(
                "IsMainEditor",
                BindingFlags.Public | BindingFlags.Static);
            _readOnlyTagsMethod = _currentPlayerType.GetMethod(
                "ReadOnlyTags",
                BindingFlags.Public | BindingFlags.Static);
            _reportResultMethod = _currentPlayerType.GetMethod(
                "ReportResult",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(bool), typeof(string), typeof(string), typeof(int) },
                modifiers: null);
            IsAvailable = _isMainEditorProperty != null;
            IsMainEditor = IsAvailable && Convert.ToBoolean(_isMainEditorProperty.GetValue(null));
            Tags = ReadTags();
            PlayerLabel = IsMainEditor ? "MainEditor" : CreateVirtualPlayerLabel(Tags);
        }

        public bool IsAvailable { get; }

        public bool IsMainEditor { get; }

        public string PlayerLabel { get; } = "Unavailable";

        public IReadOnlyList<string> Tags { get; } = Array.Empty<string>();

        public void ReportResult(bool condition, string message)
        {
            if (_reportResultMethod == null)
                return;

            try
            {
                _reportResultMethod.Invoke(
                    null,
                    new object[] { condition, message ?? string.Empty, string.Empty, 0 });
            }
            catch (TargetInvocationException)
            {
            }
        }

        private static Type ResolveCurrentPlayerType()
        {
            Type type = Type.GetType(
                "Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode",
                throwOnError: false);
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("Unity.Multiplayer.Playmode.CurrentPlayer", throwOnError: false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private IReadOnlyList<string> ReadTags()
        {
            if (_readOnlyTagsMethod == null)
                return Array.Empty<string>();

            try
            {
                object value = _readOnlyTagsMethod.Invoke(null, Array.Empty<object>());
                if (value is IEnumerable<string> tags)
                    return CopyNonEmptyTags(tags);
            }
            catch (TargetInvocationException)
            {
            }

            return Array.Empty<string>();
        }

        private static string CreateVirtualPlayerLabel(IReadOnlyList<string> tags)
        {
            if (tags != null && tags.Count > 0)
            {
                var labels = new string[tags.Count];
                for (int i = 0; i < tags.Count; i++)
                    labels[i] = SanitizeLabel(tags[i]);
                return "VirtualPlayer-" + string.Join("-", labels);
            }

            return "VirtualPlayer";
        }

        private static string SanitizeLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "tag";

            string trimmed = value.Trim();
            var chars = new char[trimmed.Length];
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                chars[i] = char.IsLetterOrDigit(ch) ? ch : '-';
            }

            return new string(chars);
        }

        private static string[] CopyNonEmptyTags(IEnumerable<string> tags)
        {
            var copied = new List<string>();
            foreach (string tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    copied.Add(tag);
            }

            return copied.Count == 0 ? Array.Empty<string>() : copied.ToArray();
        }
    }

    public interface ICharacterTestNetworkAutomationController :
        ICharacterTestNetworkSessionController,
        ICharacterTestNetworkLobbyController,
        ICharacterTestNetworkDiagnosticController
    {
    }

    internal sealed class CharacterTestNetworkAutomationRunner
    {
        private enum Stage
        {
            Created = 0,
            WaitingClientDelay = 1,
            StartingSession = 2,
            WaitingRoom = 3,
            Ready = 4,
            WaitingRoomStart = 5,
            EnteringGameplay = 6,
            WaitingGameplay = 7,
            StartingProbe = 8,
            WaitingProbe = 9,
            WaitingPeerReports = 10,
            Complete = 11
        }

        private readonly CharacterTestNetworkAutomationRequest _request;
        private readonly ICharacterTestNetworkAutomationController _controller;
        private readonly ICharacterTestNetworkAutomationPlayerContext _playerContext;
        private readonly IRuntimeLogger _logger;
        private readonly CharacterTestNetworkAutomationRole _role;
        private readonly string _peerId;
        private readonly string _displayName;

        private Stage _stage;
        private int _elapsedFrames;
        private bool _sessionStarted;
        private bool _readySent;
        private bool _enterRequested;
        private bool _reportWritten;
        private string _lastFailure = string.Empty;

        public CharacterTestNetworkAutomationRunner(
            CharacterTestNetworkAutomationRequest request,
            ICharacterTestNetworkAutomationController controller,
            ICharacterTestNetworkAutomationPlayerContext playerContext,
            IRuntimeLogger logger)
        {
            _request = (request ?? CharacterTestNetworkAutomationRequest.CreateDefault(string.Empty)).Normalize();
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _playerContext = playerContext ?? new CharacterTestNetworkAutomationPlayerContext();
            _logger = logger;
            _role = _playerContext.IsMainEditor
                ? CharacterTestNetworkAutomationRole.Host
                : CharacterTestNetworkAutomationRole.Client;
            _peerId = BuildPeerId(_request.RunId, _role, _playerContext);
            _displayName = _role == CharacterTestNetworkAutomationRole.Host
                ? "Host-" + ShortRunId(_request.RunId)
                : "Client-" + ShortRunId(_peerId);
            _stage = _role == CharacterTestNetworkAutomationRole.Client
                ? Stage.WaitingClientDelay
                : Stage.StartingSession;
        }

        public bool IsActive => _stage != Stage.Complete;

        public string RunId => _request.RunId;

        /// <remarks>LowFreqAlloc: automation Tick may create report strings and files outside normal gameplay.</remarks>
        public void Tick()
        {
            if (_stage == Stage.Complete)
                return;

            _elapsedFrames++;
            if (!_playerContext.IsAvailable)
            {
                Complete(
                    false,
                    "MPPM_UNAVAILABLE",
                    "CharacterTest network automation requires Unity Multiplayer Play Mode CurrentPlayer.");
                return;
            }

            if (_elapsedFrames > _request.TimeoutFrames)
            {
                if (_reportWritten)
                {
                    StopSessionAndMarkComplete();
                    return;
                }

                Complete(
                    false,
                    "TIMEOUT",
                    "CharacterTest network automation timed out at stage " + _stage + ".");
                return;
            }

            switch (_stage)
            {
                case Stage.WaitingClientDelay:
                    if (_elapsedFrames >= _request.ClientJoinDelayFrames)
                        _stage = Stage.StartingSession;
                    break;
                case Stage.StartingSession:
                    StartSession();
                    break;
                case Stage.WaitingRoom:
                    WaitForRoom();
                    break;
                case Stage.Ready:
                    SendReady();
                    break;
                case Stage.WaitingRoomStart:
                    WaitForRoomStart();
                    break;
                case Stage.EnteringGameplay:
                    RequestGameplayEnterIfReady();
                    break;
                case Stage.WaitingGameplay:
                    WaitForGameplay();
                    break;
                case Stage.StartingProbe:
                    StartProbe();
                    break;
                case Stage.WaitingProbe:
                    WaitForProbe();
                    break;
                case Stage.WaitingPeerReports:
                    WaitForPeerReports();
                    break;
            }
        }

        private void StartSession()
        {
            CharacterTestNetworkSessionOptions options = _controller.CreateDefaultNetworkSessionOptions(
                _role == CharacterTestNetworkAutomationRole.Host
                    ? CharacterTestNetworkSessionKind.Host
                    : CharacterTestNetworkSessionKind.Join);
            options.Kind = _role == CharacterTestNetworkAutomationRole.Host
                ? CharacterTestNetworkSessionKind.Host
                : CharacterTestNetworkSessionKind.Join;
            options.LaunchMode = CharacterTestNetworkLaunchMode.Room;
            options.BindAddress = _request.BindAddress;
            options.RemoteAddress = _request.RemoteAddress;
            options.Port = _request.Port;
            options.ServerPeerId = _request.ServerPeerId;
            options.ClientPeerId = _peerId;
            options.RoomId = _request.RoomId;
            options.RoomName = _request.RoomName;
            options.RoomMaxMembers = Math.Max(_request.ExpectedPlayers, 2);
            options.RoomMinReadyMembers = Math.Max(_request.ExpectedPlayers, 2);
            options.RoomClientAutoReady = false;
            options.RoomServerAutoStartWhenReady = false;
            options.DisplayName = _displayName;

            _controller.SetNetworkSyncProbeAutoRun(false);
            CharacterTestNetworkSessionResult result = _controller.StartNetworkSession(options);
            if (!result.Success)
            {
                Complete(false, "START_FAILED", result.Message);
                return;
            }

            _sessionStarted = true;
            _stage = Stage.WaitingRoom;
            _logger?.Info(
                "CharacterTest.NetworkAutomation",
                _role + " started room session for run " + _request.RunId + ".");
        }

        private void WaitForRoom()
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus();
            if (HasNetworkFailure(status))
            {
                Complete(false, "NETWORK_FAILED", status.ReadinessMessage);
                return;
            }

            if (status.IsNetworkActive && status.RoomStatus == NetworkRoomStatus.Open)
                _stage = Stage.Ready;
        }

        private void SendReady()
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus();
            if (HasNetworkFailure(status))
            {
                Complete(false, "NETWORK_FAILED", status.ReadinessMessage);
                return;
            }

            if (status.RoomStatus != NetworkRoomStatus.Open || !status.CanSetRoomReady)
                return;

            if (!_readySent)
            {
                CharacterTestNetworkSessionResult result = _controller.SetLocalReady(true);
                if (!result.Success)
                {
                    Complete(false, "READY_FAILED", result.Message);
                    return;
                }

                _readySent = true;
            }

            _stage = _role == CharacterTestNetworkAutomationRole.Host
                ? Stage.WaitingRoomStart
                : Stage.EnteringGameplay;
        }

        private void WaitForRoomStart()
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus();
            if (HasNetworkFailure(status))
            {
                Complete(false, "NETWORK_FAILED", status.ReadinessMessage);
                return;
            }

            if (status.RoomStatus == NetworkRoomStatus.InGame)
            {
                _stage = Stage.EnteringGameplay;
                return;
            }

            if (status.RoomStatus != NetworkRoomStatus.Open)
                return;

            bool enoughMembers = status.RoomConnectedCount >= _request.ExpectedPlayers;
            bool allConnectedMembersReady = status.RoomConnectedCount > 0
                && status.RoomReadyCount >= status.RoomConnectedCount;
            if (!enoughMembers || !allConnectedMembersReady || !status.CanStartRoomGame)
                return;

            CharacterTestNetworkSessionResult result = _controller.EnterNetworkGameplay();
            _enterRequested = result.Success;
            if (!result.Success)
            {
                Complete(false, "ENTER_REQUEST_FAILED", result.Message);
                return;
            }

            _stage = Stage.EnteringGameplay;
        }

        private void RequestGameplayEnterIfReady()
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus();
            if (HasNetworkFailure(status))
            {
                Complete(false, "NETWORK_FAILED", status.ReadinessMessage);
                return;
            }

            if (status.RoomStatus != NetworkRoomStatus.InGame)
                return;
            if (!status.CanEnterGameplay)
                return;

            CharacterTestNetworkSessionResult result = _controller.EnterNetworkGameplay();
            if (!result.Success)
            {
                Complete(false, "ENTER_FAILED", result.Message);
                return;
            }

            _enterRequested = true;
            _stage = Stage.WaitingGameplay;
        }

        private void WaitForGameplay()
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus();
            if (HasNetworkFailure(status))
            {
                Complete(false, "NETWORK_FAILED", status.ReadinessMessage);
                return;
            }

            if (status.CanEnterGameplay && _enterRequested)
                _stage = Stage.StartingProbe;
        }

        private void StartProbe()
        {
            CharacterTestNetworkSyncProbeStatus probeStatus = _controller.CaptureNetworkSyncProbeStatus();
            if (probeStatus != null && probeStatus.IsRunning)
            {
                _stage = Stage.WaitingProbe;
                return;
            }

            probeStatus = _controller.StartNetworkSyncProbe();
            if (probeStatus == null || probeStatus.Result != CharacterTestNetworkSyncProbeResult.Running)
            {
                Complete(
                    false,
                    "PROBE_START_FAILED",
                    probeStatus != null ? probeStatus.Summary : "Sync Probe did not start.");
                return;
            }

            _stage = Stage.WaitingProbe;
        }

        private void WaitForProbe()
        {
            CharacterTestNetworkSyncProbeStatus probeStatus = _controller.CaptureNetworkSyncProbeStatus();
            if (probeStatus == null || probeStatus.IsRunning)
                return;
            if (probeStatus.Result == CharacterTestNetworkSyncProbeResult.Idle
                || probeStatus.Result == CharacterTestNetworkSyncProbeResult.Rejected)
            {
                if (probeStatus.Result == CharacterTestNetworkSyncProbeResult.Rejected)
                    Complete(false, "PROBE_REJECTED", probeStatus.Summary);
                return;
            }

            bool success = probeStatus.Result == CharacterTestNetworkSyncProbeResult.Pass
                || (!_request.RequireProbePass
                    && probeStatus.Result == CharacterTestNetworkSyncProbeResult.Warning);
            CharacterTestReplayPlaybackStatus replayStatus = ValidateReplayIfRequired();
            if (success
                && _role == CharacterTestNetworkAutomationRole.Host
                && (!replayStatus.Available || !replayStatus.Success))
            {
                success = false;
            }
            Complete(
                success,
                success ? "PASS" : ResolveFailureCode(probeStatus, replayStatus, _role),
                success
                    ? probeStatus.Summary
                    : probeStatus.Summary + " " + replayStatus.Summary);
        }

        private void WaitForPeerReports()
        {
            IReadOnlyList<CharacterTestNetworkAutomationReport> reports =
                CharacterTestNetworkAutomationReportStore.ReadReports(_request.RunId);
            int successfulReports = 0;
            for (int i = 0; i < reports.Count; i++)
            {
                CharacterTestNetworkAutomationReport report = reports[i];
                if (report != null && report.Success)
                    successfulReports++;
            }

            if (successfulReports < _request.ExpectedPlayers)
                return;

            StopSessionAndMarkComplete();
        }

        private void Complete(bool success, string resultCode, string summary)
        {
            if (_reportWritten)
                return;

            _lastFailure = success ? string.Empty : (summary ?? string.Empty);
            CharacterTestNetworkAutomationReport report = BuildReport(
                success,
                resultCode,
                summary);
            CharacterTestNetworkAutomationReportStore.WriteReport(report);
            _playerContext.ReportResult(success, report.Summary);
            _logger?.Log(
                success ? RuntimeLogLevel.Info : RuntimeLogLevel.Warning,
                "CharacterTest.NetworkAutomation",
                report.Summary + " " + report.SyncProbeDetail);
            _reportWritten = true;
            if (success && _role == CharacterTestNetworkAutomationRole.Host)
            {
                _stage = Stage.WaitingPeerReports;
                return;
            }

            StopSessionAndMarkComplete();
        }

        private void StopSessionAndMarkComplete()
        {
            if (_sessionStarted)
                _controller.StopNetworkSession();
            _stage = Stage.Complete;
        }

        private CharacterTestNetworkAutomationReport BuildReport(
            bool success,
            string resultCode,
            string summary)
        {
            CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus()
                ?? CharacterTestNetworkSessionStatus.Offline(summary);
            CharacterTestNetworkSyncProbeStatus probeStatus =
                _controller.CaptureNetworkSyncProbeStatus()
                ?? CharacterTestNetworkSyncProbeStatus.Idle(false);
            CharacterTestReplayPlaybackStatus replayStatus = CaptureReplayStatus();
            string reportSummary = "CharacterTest MPPM automation "
                + (success ? "PASS" : "FAIL")
                + " "
                + resultCode
                + " role="
                + _role
                + " peer="
                + _peerId
                + " owned="
                + status.OwnedEntityId
                + " room="
                + status.RoomReadyCount
                + "/"
                + status.RoomConnectedCount
                + " probe="
                + probeStatus.ResultCode;
            if (replayStatus.Available)
                reportSummary += " replay=" + replayStatus.ResultCode;
            if (!string.IsNullOrWhiteSpace(summary))
                reportSummary += " " + summary.Trim();

            return new CharacterTestNetworkAutomationReport
            {
                RunId = _request.RunId,
                Role = _role,
                PlayerLabel = _playerContext.PlayerLabel,
                PlayerTags = CopyTags(_playerContext.Tags),
                PeerId = _peerId,
                Result = success
                    ? CharacterTestNetworkAutomationResult.Pass
                    : CharacterTestNetworkAutomationResult.Fail,
                Success = success,
                ResultCode = resultCode ?? string.Empty,
                Summary = reportSummary,
                FailureReason = _lastFailure,
                ElapsedFrames = _elapsedFrames,
                OwnedEntityId = status.OwnedEntityId,
                SessionKind = status.Kind,
                NetworkMode = status.NetworkMode,
                ServerStatus = status.ServerStatus,
                ClientStatus = status.ClientStatus,
                RoomStatus = status.RoomStatus,
                RoomReadyCount = status.RoomReadyCount,
                RoomConnectedCount = status.RoomConnectedCount,
                RttEstimateFrames = status.RttEstimateFrames,
                FrameDriftFrames = status.FrameDriftFrames,
                MaxFrameDriftFrames = status.MaxFrameDriftFrames,
                SnapshotRequestCount = status.SnapshotRequestCount,
                StateSyncPolicy = status.StateSyncPolicy,
                HeartbeatIntervalFrames = status.HeartbeatIntervalFrames,
                AuthoritativeCommandSentCount = status.AuthoritativeCommandSentCount,
                AuthoritativeCommandReceivedCount = status.AuthoritativeCommandReceivedCount,
                AuthoritativeCommandEchoCount = status.AuthoritativeCommandEchoCount,
                AuthoritativeCommandEnqueuedCount = status.AuthoritativeCommandEnqueuedCount,
                AuthoritativeCommandLateCount = status.AuthoritativeCommandLateCount,
                AuthoritativeCommandDroppedCount = status.AuthoritativeCommandDroppedCount,
                FullSnapshotSentCount = status.FullSnapshotSentCount,
                FullSnapshotReceivedCount = status.FullSnapshotReceivedCount,
                DigestSnapshotSentCount = status.DigestSnapshotSentCount,
                DigestSnapshotReceivedCount = status.DigestSnapshotReceivedCount,
                PendingTransportMessages = status.PendingTransportMessages,
                MaxSentPayloadBytes = status.MaxSentPayloadBytes,
                SentBytes = status.SentBytes,
                RuntimeCommandSentBytes = status.RuntimeCommandSentBytes,
                SnapshotSentBytes = status.SnapshotSentBytes,
                SyncProbeResultCode = probeStatus.ResultCode,
                SyncProbeResult = probeStatus.Result,
                SyncProbeSummary = probeStatus.Summary,
                SyncProbeDetail = probeStatus.Detail,
                RecentDiagnostics = status.RecentDiagnostics,
                SyncProbeSubmittedCommandCount = probeStatus.SubmittedCommandCount,
                SyncProbeInputPattern = probeStatus.InputPattern.ToString(),
                SyncProbeZeroIntentCommandCount = probeStatus.ZeroIntentCommandCount,
                SyncProbeNonZeroIntentCommandCount = probeStatus.NonZeroIntentCommandCount,
                SyncProbeLastZeroIntentCommandFrame = probeStatus.LastZeroIntentCommandFrame,
                SyncProbeLastNonZeroIntentCommandFrame = probeStatus.LastNonZeroIntentCommandFrame,
                SyncProbeLogPath = probeStatus.LogPath,
                SyncProbeLogWriteFailed = probeStatus.LogWriteFailed,
                SyncProbeLateCommandDelta = probeStatus.DeltaAuthoritativeCommandLate,
                SyncProbeDroppedCommandDelta = probeStatus.DeltaAuthoritativeCommandDropped,
                SyncProbeFullSnapshotDelta = probeStatus.DeltaFullSnapshot,
                SyncProbeDigestSnapshotDelta = probeStatus.DeltaDigestSnapshot,
                SyncProbeSnapshotRequestDelta = probeStatus.DeltaSnapshotRequest,
                ReplayResultCode = replayStatus.ResultCode,
                ReplaySuccess = replayStatus.Success,
                ReplaySummary = replayStatus.Summary,
                ReplayFrameCount = replayStatus.FrameCount,
                ReplayAuthoritativeCommandCount = replayStatus.AuthoritativeCommandCount,
                ReplayFailureFrame = replayStatus.FailureFrame.Value,
                CompletedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        private CharacterTestReplayPlaybackStatus ValidateReplayIfRequired()
        {
            var replayController = _controller as ICharacterTestNetworkReplayController;
            if (replayController == null)
                return CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay controller is unavailable.");

            CharacterTestReplayPlaybackStatus status = replayController.CaptureNetworkReplayStatus()
                ?? CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay status is unavailable.");
            if (_role == CharacterTestNetworkAutomationRole.Host
                && status.Available
                && !status.PlaybackRan)
            {
                status = replayController.ValidateNetworkReplay();
            }

            return status;
        }

        private CharacterTestReplayPlaybackStatus CaptureReplayStatus()
        {
            var replayController = _controller as ICharacterTestNetworkReplayController;
            return replayController != null
                ? replayController.CaptureNetworkReplayStatus()
                : CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay controller is unavailable.");
        }

        private static string ResolveFailureCode(
            CharacterTestNetworkSyncProbeStatus probeStatus,
            CharacterTestReplayPlaybackStatus replayStatus,
            CharacterTestNetworkAutomationRole role)
        {
            if (probeStatus != null && probeStatus.Result != CharacterTestNetworkSyncProbeResult.Pass)
                return "PROBE_FAILED";
            if (role == CharacterTestNetworkAutomationRole.Host
                && replayStatus != null
                && !replayStatus.Available)
            {
                return "REPLAY_UNAVAILABLE";
            }
            if (replayStatus != null && replayStatus.Available && !replayStatus.Success)
                return "REPLAY_FAILED";
            return "FAILED";
        }

        private static bool HasNetworkFailure(CharacterTestNetworkSessionStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.DisconnectReason)
                    || status.ServerStatus == NetworkSessionStatus.Rejected
                    || status.ClientStatus == NetworkSessionStatus.Rejected);
        }

        private static string BuildPeerId(
            string runId,
            CharacterTestNetworkAutomationRole role,
            ICharacterTestNetworkAutomationPlayerContext context)
        {
            string label = context != null ? context.PlayerLabel : role.ToString();
            return "mppm-"
                + role.ToString().ToLowerInvariant()
                + "-"
                + ShortRunId(runId)
                + "-"
                + ShortRunId(label + "-" + Guid.NewGuid().ToString("N"));
        }

        private static string ShortRunId(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
            var chars = new char[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                char ch = source[i];
                chars[i] = char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-';
            }

            string normalized = new string(chars);
            return normalized.Length <= 8 ? normalized : normalized.Substring(0, 8);
        }

        private static string[] CopyTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return Array.Empty<string>();

            var copied = new string[tags.Count];
            for (int i = 0; i < tags.Count; i++)
                copied[i] = tags[i];
            return copied;
        }
    }
}
