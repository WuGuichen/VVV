using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Network
{
    public enum NetworkCodecErrorCode
    {
        None = 0,
        InvalidBytes = 1,
        InvalidJson = 2,
        InvalidEnvelope = 3,
        UnsupportedSchema = 4,
        UnsupportedMessageKind = 5,
        PayloadTypeMismatch = 6,
        InvalidPayload = 7
    }

    public readonly struct NetworkCodecError
    {
        public NetworkCodecError(NetworkCodecErrorCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public NetworkCodecErrorCode Code { get; }
        public string Message { get; }
        public bool IsNone => Code == NetworkCodecErrorCode.None;

        public static NetworkCodecError None => new NetworkCodecError(NetworkCodecErrorCode.None, string.Empty);

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    public readonly struct NetworkCodecEncodeResult
    {
        private NetworkCodecEncodeResult(bool success, byte[] bytes, NetworkCodecError error)
        {
            Success = success;
            Bytes = bytes ?? Array.Empty<byte>();
            Error = error;
        }

        public bool Success { get; }
        public byte[] Bytes { get; }
        public NetworkCodecError Error { get; }

        public static NetworkCodecEncodeResult Succeeded(byte[] bytes)
        {
            return new NetworkCodecEncodeResult(true, bytes, NetworkCodecError.None);
        }

        public static NetworkCodecEncodeResult Failed(NetworkCodecError error)
        {
            return new NetworkCodecEncodeResult(false, Array.Empty<byte>(), error);
        }
    }

    public readonly struct NetworkCodecDecodeResult
    {
        private NetworkCodecDecodeResult(bool success, NetworkMessageEnvelope message, NetworkCodecError error)
        {
            Success = success;
            Message = message;
            Error = error;
        }

        public bool Success { get; }
        public NetworkMessageEnvelope Message { get; }
        public NetworkCodecError Error { get; }

        public static NetworkCodecDecodeResult Succeeded(NetworkMessageEnvelope message)
        {
            return new NetworkCodecDecodeResult(true, message, NetworkCodecError.None);
        }

        public static NetworkCodecDecodeResult Failed(NetworkCodecError error)
        {
            return new NetworkCodecDecodeResult(false, null, error);
        }
    }

    public interface INetworkMessageCodec
    {
        string CodecId { get; }
        NetworkCodecEncodeResult Encode(NetworkMessageEnvelope message);
        NetworkCodecDecodeResult Decode(byte[] bytes);
    }

    public sealed class JsonNetworkMessageCodec : INetworkMessageCodec
    {
        public const int SchemaVersion = 1;

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public string CodecId => "json-v0";

        public NetworkCodecEncodeResult Encode(NetworkMessageEnvelope message)
        {
            if (message == null)
                return NetworkCodecEncodeResult.Failed(Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message is null."));
            if (!Enum.IsDefined(typeof(NetworkMessageKind), message.Kind))
                return NetworkCodecEncodeResult.Failed(Error(NetworkCodecErrorCode.UnsupportedMessageKind, "Unsupported message kind: " + (int)message.Kind));

            if (!TryCreatePayloadDto(message, out object payload, out NetworkCodecError error))
                return NetworkCodecEncodeResult.Failed(error);

            try
            {
                var wire = new WireMessageDto
                {
                    SchemaVersion = SchemaVersion,
                    SessionId = message.SessionId,
                    SenderPeerId = message.SenderPeerId,
                    ReceiverPeerId = message.ReceiverPeerId,
                    Sequence = message.Sequence,
                    Channel = (int)message.Channel,
                    Kind = (int)message.Kind,
                    Frame = message.Frame.Value,
                    Payload = payload
                };
                string json = JsonConvert.SerializeObject(wire, Settings);
                return NetworkCodecEncodeResult.Succeeded(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                return NetworkCodecEncodeResult.Failed(Error(NetworkCodecErrorCode.InvalidPayload, "Network message encode failed: " + ex.Message));
            }
        }

        public NetworkCodecDecodeResult Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidBytes, "Network message bytes are null or empty."));

            JObject root;
            try
            {
                root = JObject.Parse(Encoding.UTF8.GetString(bytes));
            }
            catch (Exception ex)
            {
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidJson, "Network message json is invalid: " + ex.Message));
            }

            if (!TryReadInt(root, "schemaVersion", out int schemaVersion, out NetworkCodecError error))
                return NetworkCodecDecodeResult.Failed(error);
            if (schemaVersion != SchemaVersion)
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.UnsupportedSchema, "Unsupported network wire schema: " + schemaVersion));
            if (!TryReadLong(root, "sequence", out long sequence, out error))
                return NetworkCodecDecodeResult.Failed(error);
            if (sequence < 0L)
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message sequence cannot be negative."));
            if (!TryReadInt(root, "channel", out int channelValue, out error))
                return NetworkCodecDecodeResult.Failed(error);
            if (!Enum.IsDefined(typeof(NetworkChannel), channelValue))
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidEnvelope, "Unsupported network channel: " + channelValue));
            if (!TryReadInt(root, "kind", out int kindValue, out error))
                return NetworkCodecDecodeResult.Failed(error);
            if (!Enum.IsDefined(typeof(NetworkMessageKind), kindValue))
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.UnsupportedMessageKind, "Unsupported message kind: " + kindValue));
            if (!TryReadLong(root, "frame", out long frameValue, out error))
                return NetworkCodecDecodeResult.Failed(error);
            if (frameValue < 0L)
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message frame cannot be negative."));

            var kind = (NetworkMessageKind)kindValue;
            if (!TryReadPayload(root, kind, out object payload, out error))
                return NetworkCodecDecodeResult.Failed(error);

            try
            {
                return NetworkCodecDecodeResult.Succeeded(new NetworkMessageEnvelope(
                    ReadString(root, "sessionId"),
                    ReadString(root, "senderPeerId"),
                    ReadString(root, "receiverPeerId"),
                    sequence,
                    (NetworkChannel)channelValue,
                    kind,
                    new RuntimeFrame(frameValue),
                    payload));
            }
            catch (Exception ex)
            {
                return NetworkCodecDecodeResult.Failed(Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message envelope could not be created: " + ex.Message));
            }
        }

        private static bool TryCreatePayloadDto(
            NetworkMessageEnvelope message,
            out object payload,
            out NetworkCodecError error)
        {
            switch (message.Kind)
            {
                case NetworkMessageKind.HandshakeRequest:
                    var request = message.Payload as NetworkHandshakeRequest;
                    if (request == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = new HandshakeRequestDto
                    {
                        PeerId = request.PeerId,
                        Role = (int)request.Role,
                        Config = ToDto(request.Config)
                    };
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.HandshakeResponse:
                    var response = message.Payload as NetworkHandshakeResponse;
                    if (response == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = new HandshakeResponseDto
                    {
                        Accepted = response.Accepted,
                        Reason = response.Reason,
                        ServerConfig = ToDto(response.ServerConfig)
                    };
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.RuntimeCommands:
                    var commands = message.Payload as NetworkRuntimeCommandPacket;
                    if (commands == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(commands);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.AuthoritativeCommands:
                    var authoritativeCommands = message.Payload as NetworkAuthoritativeCommandPacket;
                    if (authoritativeCommands == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(authoritativeCommands);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.Snapshot:
                    var snapshot = message.Payload as NetworkSnapshotPacket;
                    if (snapshot == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(snapshot);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.SnapshotRequest:
                    var snapshotRequest = message.Payload as NetworkSnapshotRequestPacket;
                    if (snapshotRequest == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(snapshotRequest);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.RoomCommand:
                    var roomCommand = message.Payload as NetworkRoomCommandPacket;
                    if (roomCommand == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(roomCommand);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.RoomState:
                    var roomState = message.Payload as NetworkRoomStatePacket;
                    if (roomState == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(roomState);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.Ping:
                case NetworkMessageKind.Pong:
                    var heartbeat = message.Payload as NetworkHeartbeatPacket;
                    if (heartbeat == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = ToDto(heartbeat);
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.ApplicationControlRequest:
                    var applicationRequest = message.Payload as NetworkApplicationControlRequestPacket;
                    if (applicationRequest == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = new ApplicationControlRequestDto
                    {
                        TypeId = applicationRequest.TypeId,
                        SchemaVersion = applicationRequest.SchemaVersion,
                        RequestId = applicationRequest.RequestId,
                        PayloadJson = applicationRequest.PayloadJson
                    };
                    error = NetworkCodecError.None;
                    return true;
                case NetworkMessageKind.ApplicationControlResponse:
                    var applicationResponse = message.Payload as NetworkApplicationControlResponsePacket;
                    if (applicationResponse == null)
                        return FailPayload(message.Kind, out payload, out error);
                    payload = new ApplicationControlResponseDto
                    {
                        TypeId = applicationResponse.TypeId,
                        SchemaVersion = applicationResponse.SchemaVersion,
                        RequestId = applicationResponse.RequestId,
                        Accepted = applicationResponse.Accepted,
                        Reason = applicationResponse.Reason,
                        PayloadJson = applicationResponse.PayloadJson
                    };
                    error = NetworkCodecError.None;
                    return true;
                default:
                    payload = null;
                    error = Error(NetworkCodecErrorCode.UnsupportedMessageKind, "Unsupported message kind: " + (int)message.Kind);
                    return false;
            }
        }

        private static bool TryReadPayload(
            JObject root,
            NetworkMessageKind kind,
            out object payload,
            out NetworkCodecError error)
        {
            JToken token = root["payload"];
            if (token == null || token.Type != JTokenType.Object)
            {
                payload = null;
                error = Error(NetworkCodecErrorCode.PayloadTypeMismatch, "Network message payload must be an object for kind " + kind + ".");
                return false;
            }

            try
            {
                switch (kind)
                {
                    case NetworkMessageKind.HandshakeRequest:
                        HandshakeRequestDto request = token.ToObject<HandshakeRequestDto>(Serializer);
                        if (request == null || request.Config == null)
                            return FailInvalidPayload(kind, "Handshake request payload is incomplete.", out payload, out error);
                        if (!Enum.IsDefined(typeof(NetworkSessionRole), request.Role))
                            return FailInvalidPayload(kind, "Handshake request role is invalid: " + request.Role, out payload, out error);
                        payload = new NetworkHandshakeRequest(
                            request.PeerId,
                            (NetworkSessionRole)request.Role,
                            FromDto(request.Config));
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.HandshakeResponse:
                        HandshakeResponseDto response = token.ToObject<HandshakeResponseDto>(Serializer);
                        if (response == null || response.ServerConfig == null)
                            return FailInvalidPayload(kind, "Handshake response payload is incomplete.", out payload, out error);
                        payload = new NetworkHandshakeResponse(
                            response.Accepted,
                            response.Reason,
                            FromDto(response.ServerConfig));
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.RuntimeCommands:
                        RuntimeCommandPacketDto commands = token.ToObject<RuntimeCommandPacketDto>(Serializer);
                        if (commands == null)
                            return FailInvalidPayload(kind, "Runtime command payload is missing.", out payload, out error);
                        payload = FromDto(commands);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.AuthoritativeCommands:
                        AuthoritativeCommandPacketDto authoritativeCommands = token.ToObject<AuthoritativeCommandPacketDto>(Serializer);
                        if (authoritativeCommands == null)
                            return FailInvalidPayload(kind, "Authoritative command payload is missing.", out payload, out error);
                        payload = FromDto(authoritativeCommands);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.Snapshot:
                        SnapshotPacketDto snapshot = token.ToObject<SnapshotPacketDto>(Serializer);
                        if (snapshot == null)
                            return FailInvalidPayload(kind, "Snapshot payload is missing.", out payload, out error);
                        payload = FromDto(snapshot);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.SnapshotRequest:
                        SnapshotRequestPacketDto snapshotRequest = token.ToObject<SnapshotRequestPacketDto>(Serializer);
                        if (snapshotRequest == null)
                            return FailInvalidPayload(kind, "Snapshot request payload is missing.", out payload, out error);
                        payload = FromDto(snapshotRequest);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.RoomCommand:
                        RoomCommandPacketDto roomCommand = token.ToObject<RoomCommandPacketDto>(Serializer);
                        if (roomCommand == null)
                            return FailInvalidPayload(kind, "Room command payload is missing.", out payload, out error);
                        if (!Enum.IsDefined(typeof(NetworkRoomCommandKind), roomCommand.CommandKind))
                            return FailInvalidPayload(kind, "Room command kind is invalid: " + roomCommand.CommandKind, out payload, out error);
                        payload = FromDto(roomCommand);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.RoomState:
                        RoomStatePacketDto roomState = token.ToObject<RoomStatePacketDto>(Serializer);
                        if (roomState == null || roomState.State == null || roomState.State.Config == null)
                            return FailInvalidPayload(kind, "Room state payload is incomplete.", out payload, out error);
                        if (!Enum.IsDefined(typeof(NetworkRoomStatus), roomState.State.Status))
                            return FailInvalidPayload(kind, "Room status is invalid: " + roomState.State.Status, out payload, out error);
                        payload = FromDto(roomState);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.Ping:
                    case NetworkMessageKind.Pong:
                        HeartbeatPacketDto heartbeat = token.ToObject<HeartbeatPacketDto>(Serializer);
                        if (heartbeat == null)
                            return FailInvalidPayload(kind, "Heartbeat payload is missing.", out payload, out error);
                        payload = FromDto(heartbeat);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.ApplicationControlRequest:
                        ApplicationControlRequestDto applicationRequest =
                            token.ToObject<ApplicationControlRequestDto>(Serializer);
                        if (applicationRequest == null
                            || string.IsNullOrWhiteSpace(applicationRequest.TypeId)
                            || applicationRequest.SchemaVersion <= 0
                            || applicationRequest.RequestId < 0L)
                        {
                            return FailInvalidPayload(
                                kind,
                                "Application control request payload is invalid.",
                                out payload,
                                out error);
                        }
                        payload = new NetworkApplicationControlRequestPacket(
                            applicationRequest.TypeId,
                            applicationRequest.SchemaVersion,
                            applicationRequest.RequestId,
                            applicationRequest.PayloadJson);
                        error = NetworkCodecError.None;
                        return true;
                    case NetworkMessageKind.ApplicationControlResponse:
                        ApplicationControlResponseDto applicationResponse =
                            token.ToObject<ApplicationControlResponseDto>(Serializer);
                        if (applicationResponse == null
                            || string.IsNullOrWhiteSpace(applicationResponse.TypeId)
                            || applicationResponse.SchemaVersion <= 0
                            || applicationResponse.RequestId < 0L)
                        {
                            return FailInvalidPayload(
                                kind,
                                "Application control response payload is invalid.",
                                out payload,
                                out error);
                        }
                        payload = new NetworkApplicationControlResponsePacket(
                            applicationResponse.TypeId,
                            applicationResponse.SchemaVersion,
                            applicationResponse.RequestId,
                            applicationResponse.Accepted,
                            applicationResponse.Reason,
                            applicationResponse.PayloadJson);
                        error = NetworkCodecError.None;
                        return true;
                    default:
                        payload = null;
                        error = Error(NetworkCodecErrorCode.UnsupportedMessageKind, "Unsupported message kind: " + (int)kind);
                        return false;
                }
            }
            catch (Exception ex)
            {
                payload = null;
                error = Error(NetworkCodecErrorCode.InvalidPayload, "Network payload decode failed for kind " + kind + ": " + ex.Message);
                return false;
            }
        }

        private static bool TryReadInt(JObject root, string property, out int value, out NetworkCodecError error)
        {
            if (!TryReadLong(root, property, out long longValue, out error))
            {
                value = 0;
                return false;
            }

            if (longValue < int.MinValue || longValue > int.MaxValue)
            {
                value = 0;
                error = Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message property is outside int range: " + property);
                return false;
            }

            value = (int)longValue;
            return true;
        }

        private static bool TryReadLong(JObject root, string property, out long value, out NetworkCodecError error)
        {
            JToken token = root[property];
            if (token == null || token.Type != JTokenType.Integer)
            {
                value = 0L;
                error = Error(NetworkCodecErrorCode.InvalidEnvelope, "Network message property must be an integer: " + property);
                return false;
            }

            value = token.Value<long>();
            error = NetworkCodecError.None;
            return true;
        }

        private static string ReadString(JObject root, string property)
        {
            JToken token = root[property];
            return token != null && token.Type == JTokenType.String ? token.Value<string>() : string.Empty;
        }

        private static bool FailPayload(NetworkMessageKind kind, out object payload, out NetworkCodecError error)
        {
            payload = null;
            error = Error(NetworkCodecErrorCode.PayloadTypeMismatch, "Network payload does not match message kind " + kind + ".");
            return false;
        }

        private static bool FailInvalidPayload(
            NetworkMessageKind kind,
            string message,
            out object payload,
            out NetworkCodecError error)
        {
            payload = null;
            error = Error(NetworkCodecErrorCode.InvalidPayload, "Network payload is invalid for kind " + kind + ": " + message);
            return false;
        }

        private static NetworkCodecError Error(NetworkCodecErrorCode code, string message)
        {
            return new NetworkCodecError(code, message);
        }

        private static SessionConfigDto ToDto(NetworkSessionConfig config)
        {
            return new SessionConfigDto
            {
                ProtocolVersion = config.ProtocolVersion,
                TickRate = config.TickRate,
                InputDelayFrames = config.InputDelayFrames,
                SnapshotIntervalFrames = config.SnapshotIntervalFrames,
                ConfigHash = config.ConfigHash,
                ResourceCatalogHash = config.ResourceCatalogHash,
                StateSyncPolicy = (int)config.StateSyncPolicy,
                HeartbeatIntervalFrames = config.HeartbeatIntervalFrames
            };
        }

        private static NetworkSessionConfig FromDto(SessionConfigDto dto)
        {
            return new NetworkSessionConfig(
                dto.ProtocolVersion,
                dto.TickRate,
                dto.InputDelayFrames,
                dto.SnapshotIntervalFrames,
                dto.ConfigHash,
                dto.ResourceCatalogHash,
                (NetworkStateSyncPolicy)dto.StateSyncPolicy,
                dto.HeartbeatIntervalFrames > 0
                    ? dto.HeartbeatIntervalFrames
                    : NetworkSessionConfig.DefaultHeartbeatIntervalFrames);
        }

        private static RuntimeCommandPacketDto ToDto(NetworkRuntimeCommandPacket packet)
        {
            var commands = new List<RuntimeCommandDto>(packet.Commands.Count);
            for (int i = 0; i < packet.Commands.Count; i++)
                commands.Add(ToDto(packet.Commands[i]));

            return new RuntimeCommandPacketDto
            {
                InputPeerId = packet.InputPeerId,
                ClientFrame = packet.ClientFrame.Value,
                Commands = commands
            };
        }

        private static NetworkRuntimeCommandPacket FromDto(RuntimeCommandPacketDto dto)
        {
            var commands = new List<RuntimeCommand>();
            if (dto.Commands != null)
            {
                for (int i = 0; i < dto.Commands.Count; i++)
                    commands.Add(FromDto(dto.Commands[i]));
            }

            return new NetworkRuntimeCommandPacket(
                dto.InputPeerId,
                new RuntimeFrame(dto.ClientFrame),
                commands);
        }

        private static AuthoritativeCommandPacketDto ToDto(NetworkAuthoritativeCommandPacket packet)
        {
            var commands = new List<RuntimeCommandDto>(packet.Commands.Count);
            for (int i = 0; i < packet.Commands.Count; i++)
                commands.Add(ToDto(packet.Commands[i]));

            return new AuthoritativeCommandPacketDto
            {
                InputPeerId = packet.InputPeerId,
                ServerFrame = packet.ServerFrame.Value,
                AckedInputFrame = packet.AckedInputFrame.Value,
                Commands = commands
            };
        }

        private static NetworkAuthoritativeCommandPacket FromDto(AuthoritativeCommandPacketDto dto)
        {
            var commands = new List<RuntimeCommand>();
            if (dto.Commands != null)
            {
                for (int i = 0; i < dto.Commands.Count; i++)
                    commands.Add(FromDto(dto.Commands[i]));
            }

            return new NetworkAuthoritativeCommandPacket(
                dto.InputPeerId,
                new RuntimeFrame(dto.ServerFrame),
                new RuntimeFrame(dto.AckedInputFrame),
                commands);
        }

        private static RuntimeCommandDto ToDto(RuntimeCommand command)
        {
            return new RuntimeCommandDto
            {
                Frame = command.Frame.Value,
                SourceId = command.SourceId,
                CommandId = command.CommandId,
                TargetId = command.TargetId,
                Payload0 = command.Payload0,
                Payload1 = command.Payload1,
                Payload2 = command.Payload2,
                TraceId = command.TraceId,
                Sequence = command.Sequence,
                Envelope = command.Envelope.IsPresent ? ToDto(command.Envelope) : null
            };
        }

        private static RuntimeCommand FromDto(RuntimeCommandDto dto)
        {
            return new RuntimeCommand(
                new RuntimeFrame(dto.Frame),
                dto.SourceId,
                dto.CommandId,
                dto.TargetId,
                dto.Payload0,
                dto.Payload1,
                dto.Payload2,
                dto.TraceId,
                dto.Sequence,
                envelope: FromDto(dto.Envelope));
        }

        private static RuntimeCommandEnvelopeDto ToDto(RuntimeCommandEnvelope envelope)
        {
            return new RuntimeCommandEnvelopeDto
            {
                TypeId = envelope.TypeId,
                SchemaVersion = envelope.SchemaVersion,
                Provenance = (int)envelope.Provenance,
                Data0 = envelope.Data0,
                Data1 = envelope.Data1,
                Data2 = envelope.Data2,
                Data3 = envelope.Data3,
                Data4 = envelope.Data4,
                Data5 = envelope.Data5,
                Data6 = envelope.Data6,
                Data7 = envelope.Data7
            };
        }

        private static RuntimeCommandEnvelope FromDto(RuntimeCommandEnvelopeDto dto)
        {
            // A missing envelope is the legacy wire shape and remains
            // compatible. Once an envelope property is present, malformed
            // values must not silently downgrade to an untyped command.
            if (dto == null)
                return default;

            if (dto.TypeId <= 0 || dto.SchemaVersion <= 0)
                throw new ArgumentException("Runtime command envelope has an invalid type or schema.");

            if (!Enum.IsDefined(typeof(RuntimeCommandProvenanceKind), dto.Provenance))
                throw new ArgumentException("Runtime command envelope provenance is invalid.");

            return new RuntimeCommandEnvelope(
                dto.TypeId,
                dto.SchemaVersion,
                (RuntimeCommandProvenanceKind)dto.Provenance,
                dto.Data0,
                dto.Data1,
                dto.Data2,
                dto.Data3,
                dto.Data4,
                dto.Data5,
                dto.Data6,
                dto.Data7);
        }

        private static SnapshotPacketDto ToDto(NetworkSnapshotPacket packet)
        {
            var snapshots = new List<RuntimeCustomStateDto>(packet.ModuleSnapshots.Count);
            for (int i = 0; i < packet.ModuleSnapshots.Count; i++)
                snapshots.Add(ToDto(packet.ModuleSnapshots[i]));

            return new SnapshotPacketDto
            {
                AuthoritativeFrame = packet.AuthoritativeFrame.Value,
                ResultHash = packet.ResultHash,
                AckedInputFrame = packet.AckedInputFrame.Value,
                FullSnapshot = packet.FullSnapshot,
                ModuleSnapshots = snapshots
            };
        }

        private static NetworkSnapshotPacket FromDto(SnapshotPacketDto dto)
        {
            var snapshots = new List<RuntimeCustomState>();
            if (dto.ModuleSnapshots != null)
            {
                for (int i = 0; i < dto.ModuleSnapshots.Count; i++)
                    snapshots.Add(FromDto(dto.ModuleSnapshots[i]));
            }

            return new NetworkSnapshotPacket(
                new RuntimeFrame(dto.AuthoritativeFrame),
                dto.ResultHash,
                new RuntimeFrame(dto.AckedInputFrame),
                snapshots,
                dto.FullSnapshot);
        }

        private static RuntimeCustomStateDto ToDto(RuntimeCustomState state)
        {
            return new RuntimeCustomStateDto
            {
                NumericTypeId = state != null ? state.NumericTypeId : 0,
                TypeId = state != null ? state.TypeId : string.Empty,
                SchemaVersion = state != null ? state.SchemaVersion : 0,
                PayloadJson = state != null ? state.PayloadJson : string.Empty
            };
        }

        private static RuntimeCustomState FromDto(RuntimeCustomStateDto dto)
        {
            return new RuntimeCustomState(
                dto != null ? dto.NumericTypeId : 0,
                dto != null ? dto.TypeId : string.Empty,
                dto != null ? dto.SchemaVersion : 0,
                dto != null ? dto.PayloadJson : string.Empty);
        }

        private static SnapshotRequestPacketDto ToDto(NetworkSnapshotRequestPacket packet)
        {
            return new SnapshotRequestPacketDto
            {
                PeerId = packet.PeerId,
                AuthoritativeFrame = packet.AuthoritativeFrame.Value,
                LocalHash = packet.LocalHash,
                RemoteHash = packet.RemoteHash,
                ConsecutiveMismatchCount = packet.ConsecutiveMismatchCount,
                Reason = packet.Reason
            };
        }

        private static NetworkSnapshotRequestPacket FromDto(SnapshotRequestPacketDto dto)
        {
            return new NetworkSnapshotRequestPacket(
                dto.PeerId,
                new RuntimeFrame(dto.AuthoritativeFrame),
                dto.LocalHash,
                dto.RemoteHash,
                dto.ConsecutiveMismatchCount,
                dto.Reason);
        }

        private static HeartbeatPacketDto ToDto(NetworkHeartbeatPacket packet)
        {
            return new HeartbeatPacketDto
            {
                PeerId = packet.PeerId,
                SentFrame = packet.SentFrame.Value,
                PingSequence = packet.PingSequence
            };
        }

        private static NetworkHeartbeatPacket FromDto(HeartbeatPacketDto dto)
        {
            return new NetworkHeartbeatPacket(
                dto.PeerId,
                new RuntimeFrame(dto.SentFrame),
                dto.PingSequence);
        }

        private static RoomCommandPacketDto ToDto(NetworkRoomCommandPacket packet)
        {
            return new RoomCommandPacketDto
            {
                CommandKind = (int)packet.CommandKind,
                PeerId = packet.PeerId,
                DisplayName = packet.DisplayName,
                Ready = packet.Ready,
                RequestId = packet.RequestId
            };
        }

        private static NetworkRoomCommandPacket FromDto(RoomCommandPacketDto dto)
        {
            return new NetworkRoomCommandPacket(
                (NetworkRoomCommandKind)dto.CommandKind,
                dto.PeerId,
                dto.DisplayName,
                dto.Ready,
                dto.RequestId);
        }

        private static RoomStatePacketDto ToDto(NetworkRoomStatePacket packet)
        {
            return new RoomStatePacketDto
            {
                Accepted = packet.Accepted,
                Reason = packet.Reason,
                RequestId = packet.RequestId,
                State = ToDto(packet.State)
            };
        }

        private static NetworkRoomStatePacket FromDto(RoomStatePacketDto dto)
        {
            return new NetworkRoomStatePacket(
                FromDto(dto.State),
                dto.Accepted,
                dto.Reason,
                dto.RequestId);
        }

        private static RoomStateDto ToDto(NetworkRoomState state)
        {
            var members = new List<RoomMemberDto>(state.Members.Count);
            for (int i = 0; i < state.Members.Count; i++)
                members.Add(ToDto(state.Members[i]));

            return new RoomStateDto
            {
                Status = (int)state.Status,
                Config = ToDto(state.Config),
                ServerPeerId = state.ServerPeerId,
                GameplaySessionId = state.GameplaySessionId,
                StateVersion = state.StateVersion,
                LastReason = state.LastReason,
                Members = members
            };
        }

        private static NetworkRoomState FromDto(RoomStateDto dto)
        {
            var members = new List<NetworkRoomMember>();
            if (dto.Members != null)
            {
                for (int i = 0; i < dto.Members.Count; i++)
                    members.Add(FromDto(dto.Members[i]));
            }

            return new NetworkRoomState(
                (NetworkRoomStatus)dto.Status,
                FromDto(dto.Config),
                dto.ServerPeerId,
                dto.GameplaySessionId,
                members,
                dto.StateVersion,
                dto.LastReason);
        }

        private static RoomConfigDto ToDto(NetworkRoomConfig config)
        {
            return new RoomConfigDto
            {
                RoomId = config.RoomId,
                RoomName = config.RoomName,
                MaxMembers = config.MaxMembers,
                MinReadyMembers = config.MinReadyMembers,
                RequireAllReady = config.RequireAllReady,
                AllowLateJoin = config.AllowLateJoin,
                GameplaySessionConfig = ToDto(config.GameplaySessionConfig)
            };
        }

        private static NetworkRoomConfig FromDto(RoomConfigDto dto)
        {
            NetworkSessionConfig gameplaySessionConfig = dto.GameplaySessionConfig != null
                ? FromDto(dto.GameplaySessionConfig)
                : NetworkSessionConfig.Default;
            return new NetworkRoomConfig(
                dto.RoomId,
                dto.RoomName,
                dto.MaxMembers,
                dto.MinReadyMembers,
                dto.RequireAllReady,
                dto.AllowLateJoin,
                gameplaySessionConfig);
        }

        private static RoomMemberDto ToDto(NetworkRoomMember member)
        {
            return new RoomMemberDto
            {
                PeerId = member.PeerId,
                DisplayName = member.DisplayName,
                SlotIndex = member.SlotIndex,
                Ready = member.Ready,
                Connected = member.Connected,
                JoinedFrame = member.JoinedFrame.Value
            };
        }

        private static NetworkRoomMember FromDto(RoomMemberDto dto)
        {
            return new NetworkRoomMember(
                dto.PeerId,
                dto.DisplayName,
                dto.SlotIndex,
                dto.Ready,
                dto.Connected,
                new RuntimeFrame(dto.JoinedFrame));
        }

        private sealed class WireMessageDto
        {
            public int SchemaVersion { get; set; }
            public string SessionId { get; set; }
            public string SenderPeerId { get; set; }
            public string ReceiverPeerId { get; set; }
            public long Sequence { get; set; }
            public int Channel { get; set; }
            public int Kind { get; set; }
            public long Frame { get; set; }
            public object Payload { get; set; }
        }

        private sealed class SessionConfigDto
        {
            public int ProtocolVersion { get; set; }
            public int TickRate { get; set; }
            public int InputDelayFrames { get; set; }
            public int SnapshotIntervalFrames { get; set; }
            public string ConfigHash { get; set; }
            public string ResourceCatalogHash { get; set; }
            public int StateSyncPolicy { get; set; }
            public int HeartbeatIntervalFrames { get; set; }
        }

        private sealed class HandshakeRequestDto
        {
            public string PeerId { get; set; }
            public int Role { get; set; }
            public SessionConfigDto Config { get; set; }
        }

        private sealed class HandshakeResponseDto
        {
            public bool Accepted { get; set; }
            public string Reason { get; set; }
            public SessionConfigDto ServerConfig { get; set; }
        }

        private sealed class RuntimeCommandPacketDto
        {
            public string InputPeerId { get; set; }
            public long ClientFrame { get; set; }
            public List<RuntimeCommandDto> Commands { get; set; }
        }

        private sealed class AuthoritativeCommandPacketDto
        {
            public string InputPeerId { get; set; }
            public long ServerFrame { get; set; }
            public long AckedInputFrame { get; set; }
            public List<RuntimeCommandDto> Commands { get; set; }
        }

        private sealed class RuntimeCommandDto
        {
            public long Frame { get; set; }
            public int SourceId { get; set; }
            public int CommandId { get; set; }
            public int TargetId { get; set; }
            public int Payload0 { get; set; }
            public int Payload1 { get; set; }
            public int Payload2 { get; set; }
            public RuntimeTraceIdentity TraceId { get; set; }
            public long Sequence { get; set; }
            // Keep legacy command JSON byte-compatible when no typed envelope
            // is present. Older/non-CharacterTest peers therefore see no new
            // field, while newer peers can opt into the envelope explicitly.
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public RuntimeCommandEnvelopeDto Envelope { get; set; }
        }

        private sealed class RuntimeCommandEnvelopeDto
        {
            public int TypeId { get; set; }
            public int SchemaVersion { get; set; }
            public int Provenance { get; set; }
            public long Data0 { get; set; }
            public long Data1 { get; set; }
            public long Data2 { get; set; }
            public long Data3 { get; set; }
            public long Data4 { get; set; }
            public long Data5 { get; set; }
            public long Data6 { get; set; }
            public long Data7 { get; set; }
        }

        private sealed class SnapshotPacketDto
        {
            public long AuthoritativeFrame { get; set; }
            public long ResultHash { get; set; }
            public long AckedInputFrame { get; set; }
            public bool FullSnapshot { get; set; }
            public List<RuntimeCustomStateDto> ModuleSnapshots { get; set; }
        }

        private sealed class RuntimeCustomStateDto
        {
            public int NumericTypeId { get; set; }
            public string TypeId { get; set; }
            public int SchemaVersion { get; set; }
            public string PayloadJson { get; set; }
        }

        private sealed class SnapshotRequestPacketDto
        {
            public string PeerId { get; set; }
            public long AuthoritativeFrame { get; set; }
            public long LocalHash { get; set; }
            public long RemoteHash { get; set; }
            public int ConsecutiveMismatchCount { get; set; }
            public string Reason { get; set; }
        }

        private sealed class HeartbeatPacketDto
        {
            public string PeerId { get; set; }
            public long SentFrame { get; set; }
            public long PingSequence { get; set; }
        }

        private sealed class ApplicationControlRequestDto
        {
            public string TypeId { get; set; }
            public int SchemaVersion { get; set; }
            public long RequestId { get; set; }
            public string PayloadJson { get; set; }
        }

        private sealed class ApplicationControlResponseDto
        {
            public string TypeId { get; set; }
            public int SchemaVersion { get; set; }
            public long RequestId { get; set; }
            public bool Accepted { get; set; }
            public string Reason { get; set; }
            public string PayloadJson { get; set; }
        }

        private sealed class RoomCommandPacketDto
        {
            public int CommandKind { get; set; }
            public string PeerId { get; set; }
            public string DisplayName { get; set; }
            public bool Ready { get; set; }
            public long RequestId { get; set; }
        }

        private sealed class RoomStatePacketDto
        {
            public bool Accepted { get; set; }
            public string Reason { get; set; }
            public long RequestId { get; set; }
            public RoomStateDto State { get; set; }
        }

        private sealed class RoomStateDto
        {
            public int Status { get; set; }
            public RoomConfigDto Config { get; set; }
            public string ServerPeerId { get; set; }
            public string GameplaySessionId { get; set; }
            public long StateVersion { get; set; }
            public string LastReason { get; set; }
            public List<RoomMemberDto> Members { get; set; }
        }

        private sealed class RoomConfigDto
        {
            public string RoomId { get; set; }
            public string RoomName { get; set; }
            public int MaxMembers { get; set; }
            public int MinReadyMembers { get; set; }
            public bool RequireAllReady { get; set; }
            public bool AllowLateJoin { get; set; }
            public SessionConfigDto GameplaySessionConfig { get; set; }
        }

        private sealed class RoomMemberDto
        {
            public string PeerId { get; set; }
            public string DisplayName { get; set; }
            public int SlotIndex { get; set; }
            public bool Ready { get; set; }
            public bool Connected { get; set; }
            public long JoinedFrame { get; set; }
        }
    }
}
