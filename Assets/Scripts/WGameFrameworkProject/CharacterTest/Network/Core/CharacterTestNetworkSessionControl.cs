using System;
using System.Globalization;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestNetworkSessionKind
    {
        Offline = 0,
        Host = 1,
        Join = 2
    }

    public enum CharacterTestNetworkLaunchMode
    {
        DirectLan = 0,
        Room = 1
    }

    public sealed class CharacterTestNetworkSessionOptions
    {
        public const string DefaultBindAddress = "0.0.0.0";
        public const string DefaultRemoteAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;
        public const string DefaultServerPeerId = "server";
        public const string DefaultClientPeerId = "client-0";
        public const int DefaultInputDelayFrames = 2;
        public const int DefaultSnapshotIntervalFrames = 2;
        public const NetworkStateSyncPolicy DefaultStateSyncPolicy = NetworkStateSyncPolicy.BootstrapOnly;
        public const string DefaultRoomId = "character-test-room";
        public const string DefaultRoomName = "CharacterTest LAN Room";
        public const int DefaultRoomMaxMembers = 4;
        public const int DefaultRoomMinReadyMembers = 1;
        public const bool DefaultRoomClientAutoReady = true;
        public const bool DefaultRoomServerAutoStartWhenReady = true;
        public const string DefaultDisplayName = "Player";

        public CharacterTestNetworkSessionKind Kind { get; set; } = CharacterTestNetworkSessionKind.Offline;
        public CharacterTestNetworkLaunchMode LaunchMode { get; set; } = CharacterTestNetworkLaunchMode.DirectLan;
        public string BindAddress { get; set; } = DefaultBindAddress;
        public string RemoteAddress { get; set; } = DefaultRemoteAddress;
        public ushort Port { get; set; } = DefaultPort;
        public string ServerPeerId { get; set; } = DefaultServerPeerId;
        public string ClientPeerId { get; set; } = DefaultClientPeerId;
        public int InputDelayFrames { get; set; } = DefaultInputDelayFrames;
        public int SnapshotIntervalFrames { get; set; } = DefaultSnapshotIntervalFrames;
        public NetworkStateSyncPolicy StateSyncPolicy { get; set; } = DefaultStateSyncPolicy;
        public int HeartbeatIntervalFrames { get; set; } = NetworkSessionConfig.DefaultHeartbeatIntervalFrames;
        public string RoomId { get; set; } = DefaultRoomId;
        public string RoomName { get; set; } = DefaultRoomName;
        public int RoomMaxMembers { get; set; } = DefaultRoomMaxMembers;
        public int RoomMinReadyMembers { get; set; } = DefaultRoomMinReadyMembers;
        public bool RoomClientAutoReady { get; set; } = DefaultRoomClientAutoReady;
        public bool RoomServerAutoStartWhenReady { get; set; } = DefaultRoomServerAutoStartWhenReady;
        public string DisplayName { get; set; } = DefaultDisplayName;

        public static CharacterTestNetworkSessionOptions HostDefaults(
            ushort port = DefaultPort,
            string bindAddress = DefaultBindAddress,
            string serverPeerId = DefaultServerPeerId,
            string clientPeerId = DefaultClientPeerId)
        {
            return new CharacterTestNetworkSessionOptions
            {
                Kind = CharacterTestNetworkSessionKind.Host,
                BindAddress = string.IsNullOrWhiteSpace(bindAddress) ? DefaultBindAddress : bindAddress,
                Port = port,
                ServerPeerId = string.IsNullOrWhiteSpace(serverPeerId) ? DefaultServerPeerId : serverPeerId,
                ClientPeerId = string.IsNullOrWhiteSpace(clientPeerId) ? DefaultClientPeerId : clientPeerId
            };
        }

        public static CharacterTestNetworkSessionOptions JoinDefaults(
            string remoteAddress = DefaultRemoteAddress,
            ushort port = DefaultPort,
            string serverPeerId = DefaultServerPeerId,
            string clientPeerId = DefaultClientPeerId)
        {
            return new CharacterTestNetworkSessionOptions
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                RemoteAddress = string.IsNullOrWhiteSpace(remoteAddress) ? DefaultRemoteAddress : remoteAddress,
                Port = port,
                ServerPeerId = string.IsNullOrWhiteSpace(serverPeerId) ? DefaultServerPeerId : serverPeerId,
                ClientPeerId = string.IsNullOrWhiteSpace(clientPeerId) ? DefaultClientPeerId : clientPeerId
            };
        }

        public CharacterTestNetworkSessionOptions Clone()
        {
            return new CharacterTestNetworkSessionOptions
            {
                Kind = Kind,
                LaunchMode = LaunchMode,
                BindAddress = BindAddress,
                RemoteAddress = RemoteAddress,
                Port = Port,
                ServerPeerId = ServerPeerId,
                ClientPeerId = ClientPeerId,
                InputDelayFrames = InputDelayFrames,
                SnapshotIntervalFrames = SnapshotIntervalFrames,
                StateSyncPolicy = StateSyncPolicy,
                HeartbeatIntervalFrames = HeartbeatIntervalFrames,
                RoomId = RoomId,
                RoomName = RoomName,
                RoomMaxMembers = RoomMaxMembers,
                RoomMinReadyMembers = RoomMinReadyMembers,
                RoomClientAutoReady = RoomClientAutoReady,
                RoomServerAutoStartWhenReady = RoomServerAutoStartWhenReady,
                DisplayName = DisplayName
            };
        }
    }

    public static class CharacterTestNetworkEndpoint
    {
        public static bool TrySplitHostPort(string endpoint, out string host, out ushort port)
        {
            host = string.Empty;
            port = 0;
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            string value = endpoint.Trim();
            int separator = value.LastIndexOf(':');
            if (separator <= 0 || separator >= value.Length - 1)
                return false;
            if (value.IndexOf(':') != separator)
                return false;

            string hostPart = value.Substring(0, separator).Trim();
            string portPart = value.Substring(separator + 1).Trim();
            if (hostPart.Length == 0)
                return false;
            if (!ushort.TryParse(portPart, NumberStyles.None, CultureInfo.InvariantCulture, out ushort parsed)
                || parsed == 0)
            {
                return false;
            }

            host = hostPart;
            port = parsed;
            return true;
        }

        public static void ApplyEndpointOverrides(CharacterTestNetworkSessionOptions options)
        {
            if (options == null)
                return;

            if (TrySplitHostPort(options.RemoteAddress, out string remoteHost, out ushort remotePort))
            {
                options.RemoteAddress = remoteHost;
                options.Port = remotePort;
            }

            if (TrySplitHostPort(options.BindAddress, out string bindHost, out ushort bindPort))
            {
                options.BindAddress = bindHost;
                options.Port = bindPort;
            }
        }
    }

    public sealed class CharacterTestNetworkSessionStatus
    {
        public static CharacterTestNetworkSessionStatus Offline(string message = "Offline")
        {
            return new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Offline,
                LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                NetworkMode = CharacterTestNetworkMode.Disabled,
                NetworkTickRate = 60,
                Message = string.IsNullOrWhiteSpace(message) ? "Offline" : message
            };
        }

        public CharacterTestNetworkSessionKind Kind { get; set; }
        public CharacterTestNetworkLaunchMode LaunchMode { get; set; } = CharacterTestNetworkLaunchMode.DirectLan;
        public CharacterTestNetworkMode NetworkMode { get; set; }
        public bool IsNetworkActive => NetworkMode != CharacterTestNetworkMode.Disabled;
        public string Message { get; set; } = string.Empty;
        public string BindAddress { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public ushort Port { get; set; }
        public string ServerPeerId { get; set; } = string.Empty;
        public string ClientPeerId { get; set; } = string.Empty;
        public int NetworkTickRate { get; set; } = 60;
        public int OwnedEntityId { get; set; }
        public string TransportMode { get; set; } = string.Empty;
        public string TransportId { get; set; } = string.Empty;
        public NetworkSessionStatus ServerStatus { get; set; } = NetworkSessionStatus.Disconnected;
        public NetworkSessionStatus ClientStatus { get; set; } = NetworkSessionStatus.Disconnected;
        public RuntimeFrame ServerFrame { get; set; }
        public RuntimeFrame ClientFrame { get; set; }
        public int RttEstimateFrames { get; set; }
        public string DisconnectReason { get; set; } = string.Empty;
        public NetworkRoomStatus RoomStatus { get; set; } = NetworkRoomStatus.Closed;
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomMembers { get; set; } = string.Empty;
        public int RoomReadyCount { get; set; }
        public int RoomConnectedCount { get; set; }
        public long RoomStateVersion { get; set; }
        public string RoomReason { get; set; } = string.Empty;
        public int AverageRttFrames { get; set; }
        public int FrameDriftFrames { get; set; }
        public int AverageFrameDriftFrames { get; set; }
        public int MaxFrameDriftFrames { get; set; }
        public int ResyncCount { get; set; }
        public int SnapshotMismatchCount { get; set; }
        public int SnapshotRequestCount { get; set; }
        public int AuthoritativeCommandSentCount { get; set; }
        public int AuthoritativeCommandReceivedCount { get; set; }
        public int AuthoritativeCommandEchoCount { get; set; }
        public int AuthoritativeCommandEnqueuedCount { get; set; }
        public int AuthoritativeCommandLateCount { get; set; }
        public int AuthoritativeCommandDroppedCount { get; set; }
        public int FullSnapshotSentCount { get; set; }
        public int DigestSnapshotSentCount { get; set; }
        public int FullSnapshotReceivedCount { get; set; }
        public int DigestSnapshotReceivedCount { get; set; }
        public NetworkStateSyncPolicy StateSyncPolicy { get; set; } = NetworkStateSyncPolicy.SnapshotVerification;
        public int HeartbeatIntervalFrames { get; set; } = NetworkSessionConfig.DefaultHeartbeatIntervalFrames;
        public int PendingRuntimeCommands { get; set; }
        public int PendingTransportMessages { get; set; }
        public long SentBytes { get; set; }
        public long DeliveredBytes { get; set; }
        public int LastSentPayloadBytes { get; set; }
        public int MaxSentPayloadBytes { get; set; }
        public long RuntimeCommandSentBytes { get; set; }
        public long SnapshotSentBytes { get; set; }
        public long SnapshotRequestSentBytes { get; set; }
        public long HeartbeatSentBytes { get; set; }
        public long RoomSentBytes { get; set; }
        public bool IsRoomHost { get; set; }
        public bool LocalRoomReady { get; set; }
        public bool CanSetRoomReady { get; set; }
        public bool CanStartRoomGame { get; set; }
        public bool CanRequestEnterGameplay { get; set; }
        public bool NetworkGameplayEnterPending { get; set; }
        public bool CanEnterGameplay { get; set; }
        public string ReadinessMessage { get; set; } = string.Empty;
        public string RecentDiagnostics { get; set; } = string.Empty;
    }

    public readonly struct CharacterTestNetworkSessionResult
    {
        private CharacterTestNetworkSessionResult(
            bool success,
            CharacterTestNetworkSessionKind requestedKind,
            CharacterTestNetworkMode networkMode,
            string message,
            CharacterTestNetworkSessionStatus status)
        {
            Success = success;
            RequestedKind = requestedKind;
            NetworkMode = networkMode;
            Message = message ?? string.Empty;
            Status = status ?? CharacterTestNetworkSessionStatus.Offline(Message);
        }

        public bool Success { get; }
        public CharacterTestNetworkSessionKind RequestedKind { get; }
        public CharacterTestNetworkMode NetworkMode { get; }
        public string Message { get; }
        public CharacterTestNetworkSessionStatus Status { get; }

        public static CharacterTestNetworkSessionResult Succeeded(
            CharacterTestNetworkSessionKind requestedKind,
            CharacterTestNetworkMode networkMode,
            string message,
            CharacterTestNetworkSessionStatus status)
        {
            return new CharacterTestNetworkSessionResult(true, requestedKind, networkMode, message, status);
        }

        public static CharacterTestNetworkSessionResult Failed(
            CharacterTestNetworkSessionKind requestedKind,
            CharacterTestNetworkMode networkMode,
            string message,
            CharacterTestNetworkSessionStatus status = null)
        {
            return new CharacterTestNetworkSessionResult(false, requestedKind, networkMode, message, status);
        }
    }

    public interface ICharacterTestNetworkSessionController
    {
        CharacterTestNetworkSessionOptions CreateDefaultNetworkSessionOptions(
            CharacterTestNetworkSessionKind kind);

        CharacterTestNetworkSessionResult StartNetworkSession(
            CharacterTestNetworkSessionOptions options);

        CharacterTestNetworkSessionResult StopNetworkSession();

        CharacterTestNetworkSessionStatus CaptureNetworkSessionStatus();
    }

    public interface ICharacterTestNetworkLobbyController
    {
        CharacterTestNetworkSessionResult SetLocalReady(bool ready);

        CharacterTestNetworkSessionResult StartRoomGame();

        CharacterTestNetworkSessionResult EnterNetworkGameplay();
    }

    public interface ICharacterTestNetworkDiagnosticController
    {
        void SetNetworkSyncProbeAutoRun(bool enabled);

        CharacterTestNetworkSyncProbeStatus StartNetworkSyncProbe();

        CharacterTestNetworkSyncProbeStatus StopNetworkSyncProbe();

        CharacterTestNetworkSyncProbeStatus CaptureNetworkSyncProbeStatus();
    }

    public static class CharacterTestNetworkSessionModeMapper
    {
        public static CharacterTestNetworkMode ToNetworkMode(CharacterTestNetworkSessionKind kind)
        {
            return ToNetworkMode(kind, CharacterTestNetworkLaunchMode.DirectLan);
        }

        public static CharacterTestNetworkMode ToNetworkMode(
            CharacterTestNetworkSessionKind kind,
            CharacterTestNetworkLaunchMode launchMode)
        {
            switch (kind)
            {
                case CharacterTestNetworkSessionKind.Offline:
                    return CharacterTestNetworkMode.Disabled;
                case CharacterTestNetworkSessionKind.Host:
                    return launchMode == CharacterTestNetworkLaunchMode.Room
                        ? CharacterTestNetworkMode.UnityTransportRoomHost
                        : CharacterTestNetworkMode.UnityTransportHost;
                case CharacterTestNetworkSessionKind.Join:
                    return launchMode == CharacterTestNetworkLaunchMode.Room
                        ? CharacterTestNetworkMode.UnityTransportRoomClient
                        : CharacterTestNetworkMode.UnityTransportClient;
                default:
                    return CharacterTestNetworkMode.Disabled;
            }
        }

        public static CharacterTestNetworkSessionKind ToSessionKind(CharacterTestNetworkMode mode)
        {
            switch (mode)
            {
                case CharacterTestNetworkMode.UnityTransportServer:
                case CharacterTestNetworkMode.UnityTransportHost:
                case CharacterTestNetworkMode.UnityTransportRoomServer:
                case CharacterTestNetworkMode.UnityTransportRoomHost:
                    return CharacterTestNetworkSessionKind.Host;
                case CharacterTestNetworkMode.UnityTransportClient:
                case CharacterTestNetworkMode.UnityTransportRoomClient:
                    return CharacterTestNetworkSessionKind.Join;
                default:
                    return CharacterTestNetworkSessionKind.Offline;
            }
        }

        public static CharacterTestNetworkLaunchMode ToLaunchMode(CharacterTestNetworkMode mode)
        {
            switch (mode)
            {
                case CharacterTestNetworkMode.UnityTransportRoomServer:
                case CharacterTestNetworkMode.UnityTransportRoomClient:
                case CharacterTestNetworkMode.UnityTransportRoomHost:
                    return CharacterTestNetworkLaunchMode.Room;
                default:
                    return CharacterTestNetworkLaunchMode.DirectLan;
            }
        }
    }
}
