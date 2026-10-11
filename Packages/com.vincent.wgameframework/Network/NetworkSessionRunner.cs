using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public sealed class NetworkSessionRunner
    {
        private const NetworkChannel RuntimeCommandChannel = NetworkChannel.ReliableOrdered;
        private const NetworkChannel HeartbeatChannel = NetworkChannel.Unreliable;

        private readonly INetworkTransport _transport;
        private readonly RuntimeCommandBuffer _runtimeCommandBuffer;
        private readonly Func<RuntimeFrame, long> _resultHashProvider;
        private readonly Func<RuntimeFrame, NetworkSnapshotHashLookupResult> _snapshotHashLookup;
        private readonly Func<string, RuntimeCommand, NetworkCommandAuthorizationResult> _commandAuthorizer;
        private readonly Func<RuntimeFrame, string, long> _peerResultHashProvider;
        private readonly Action<NetworkAuthoritativeCommandPacket> _authoritativeCommandObserver;
        private readonly Func<NetworkAuthoritativeCommandPacket, RuntimeFrame, bool> _authoritativeCommandReceiveHandler;
        private readonly Func<NetworkSnapshotPacket, RuntimeFrame, RuntimeFrame, NetworkSnapshotCorrectionResult> _snapshotCorrectionHandler;
        private readonly bool _applyOwnAuthoritativeCommands;
        private Func<string, NetworkApplicationControlRequestPacket, NetworkApplicationControlResponsePacket>
            _applicationControlHandler;
        private Action<NetworkApplicationControlResponsePacket> _applicationControlResponseObserver;
        private readonly List<NetworkMessageEnvelope> _inbox = new List<NetworkMessageEnvelope>();
        private readonly List<NetworkMessageEnvelope> _deferredSnapshotMessages = new List<NetworkMessageEnvelope>();
        private readonly List<NetworkTransportEvent> _events = new List<NetworkTransportEvent>();
        private readonly List<RuntimeCommandError> _commandErrors = new List<RuntimeCommandError>();
        private readonly NetworkSessionDiagnostics _diagnostics = new NetworkSessionDiagnostics(32);
        private readonly IRuntimeLogger _logger;
        private readonly NetworkReliableSequenceTracker _sequenceTracker = new NetworkReliableSequenceTracker();
        private readonly NetworkSnapshotRegistry _snapshotRegistry = new NetworkSnapshotRegistry();
        private readonly HashSet<string> _connectedPeers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _peersNeedingFullSnapshot = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, NetworkInputScheduleState> _inputScheduleStates =
            new Dictionary<string, NetworkInputScheduleState>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeFrame> _lastAckedInputFrameByPeer =
            new Dictionary<string, RuntimeFrame>(StringComparer.Ordinal);
        private readonly NetworkFrameSampleWindow _rttFrames = new NetworkFrameSampleWindow(32);
        private readonly NetworkFrameSampleWindow _snapshotIntervalFrames = new NetworkFrameSampleWindow(32);
        private readonly NetworkFrameSampleWindow _frameDriftFrames = new NetworkFrameSampleWindow(32);
        private readonly int _snapshotRequestMismatchThreshold;
        private readonly int _snapshotRequestCooldownFrames;
        private long _nextSequence;
        private long _nextHeartbeatSequence;
        private long _lastSnapshotRequestFrame = long.MinValue;
        private long _lastLateAuthoritativeCommandWarningFrame = long.MinValue;
        private int _suppressedLateAuthoritativeCommandWarningCount;
        private int _consecutiveComparableSnapshotMismatches;
        private bool _hasLastSnapshotIntervalFrame;
        private RuntimeFrame _lastSnapshotIntervalFrame = RuntimeFrame.Zero;
        private INetworkBoundedPacketTransport _boundedTransport;
        private INetworkBoundedPacketIngress _boundedIngress;
        private byte[] _boundedReceiveBuffer;
        private ulong _boundedSessionHandle;
        private uint _boundedLocalPeerHandle;
        private long _boundedProfileHash;
        private RuntimeFrame _recoveryMinimumFrame = RuntimeFrame.Zero;
        private RuntimeFrame _lastAppliedFullSnapshotFrame = RuntimeFrame.Zero;
        private bool _hasAppliedFullSnapshot;
        private bool _requiresReconnectSnapshot;
        private NetworkSnapshotPacket _lastAppliedFullSnapshot;

        public NetworkSessionRunner(
            NetworkSessionRole role,
            string sessionId,
            string peerId,
            string serverPeerId,
            NetworkSessionConfig config,
            INetworkTransport transport,
            RuntimeCommandBuffer serverCommandBuffer = null,
            IEnumerable<INetworkSnapshotParticipant> participants = null,
            Func<RuntimeFrame, long> resultHashProvider = null,
            IRuntimeLogger logger = null,
            NetworkSnapshotApplyPolicy snapshotApplyPolicy = NetworkSnapshotApplyPolicy.ApplyOnMismatch,
            Func<RuntimeFrame, NetworkSnapshotHashLookupResult> snapshotHashLookup = null,
            int snapshotRequestMismatchThreshold = 3,
            int snapshotRequestCooldownFrames = 60,
            Func<string, RuntimeCommand, NetworkCommandAuthorizationResult> commandAuthorizer = null,
            Func<RuntimeFrame, string, long> peerResultHashProvider = null,
            Action<NetworkAuthoritativeCommandPacket> authoritativeCommandObserver = null,
            bool applyOwnAuthoritativeCommands = false,
            Func<NetworkAuthoritativeCommandPacket, RuntimeFrame, bool> authoritativeCommandReceiveHandler = null,
            Func<NetworkSnapshotPacket, RuntimeFrame, RuntimeFrame, NetworkSnapshotCorrectionResult> snapshotCorrectionHandler = null)
        {
            Role = role;
            SessionId = string.IsNullOrEmpty(sessionId) ? "default" : sessionId;
            PeerId = string.IsNullOrEmpty(peerId) ? role.ToString().ToLowerInvariant() : peerId;
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            Config = config;
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _runtimeCommandBuffer = serverCommandBuffer;
            _resultHashProvider = resultHashProvider;
            _snapshotHashLookup = snapshotHashLookup;
            _logger = logger ?? NullRuntimeLogger.Instance;
            SnapshotApplyPolicy = snapshotApplyPolicy;
            _commandAuthorizer = commandAuthorizer;
            _peerResultHashProvider = peerResultHashProvider;
            _authoritativeCommandObserver = authoritativeCommandObserver;
            _authoritativeCommandReceiveHandler = authoritativeCommandReceiveHandler;
            _snapshotCorrectionHandler = snapshotCorrectionHandler;
            _applyOwnAuthoritativeCommands = applyOwnAuthoritativeCommands;
            _snapshotRequestMismatchThreshold = Math.Max(1, snapshotRequestMismatchThreshold);
            _snapshotRequestCooldownFrames = Math.Max(0, snapshotRequestCooldownFrames);
            Status = role == NetworkSessionRole.Server ? NetworkSessionStatus.Connected : NetworkSessionStatus.Disconnected;
            RegisterParticipants(participants);
        }

        public NetworkSessionRole Role { get; }
        public string SessionId { get; }
        public string PeerId { get; }
        public string ServerPeerId { get; }
        public NetworkSessionConfig Config { get; }
        public NetworkSessionStatus Status { get; private set; }
        public string RecoveryReason { get; private set; } = string.Empty;
        public bool IsAuthorityReady => Status == NetworkSessionStatus.Connected;
        public NetworkSnapshotApplyPolicy SnapshotApplyPolicy { get; }
        public string RejectionReason { get; private set; } = string.Empty;
        public string DisconnectReason { get; private set; } = string.Empty;
        public RuntimeFrame LastServerFrame { get; private set; } = RuntimeFrame.Zero;
        public RuntimeFrame LastAckedInputFrame { get; private set; } = RuntimeFrame.Zero;
        public long LastSnapshotHash { get; private set; }
        private static readonly Comparison<NetworkMessageEnvelope> MessageComparison = CompareMessages;

        /// <summary>Appends connected peer ids without boxing the set enumerator.</summary>
        public void CopyConnectedPeerIdsTo(List<string> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            foreach (string peerId in _connectedPeers) destination.Add(peerId);
        }

        public int ConnectedPeerCount => _connectedPeers.Count;
        public long LastReceivedSequence => _sequenceTracker.LastReceivedSequence;
        public int DuplicateMessageCount => _sequenceTracker.DuplicateMessageCount;
        public int OutOfOrderMessageCount => _sequenceTracker.OutOfOrderMessageCount;
        public int RttEstimateFrames { get; private set; }
        public int AverageRttFrames => _rttFrames.AverageRounded;
        public int MaxRttFrames => _rttFrames.Max;
        public int AverageSnapshotIntervalFrames => _snapshotIntervalFrames.AverageRounded;
        public int MaxSnapshotIntervalFrames => _snapshotIntervalFrames.Max;
        public int LastFrameDriftFrames { get; private set; }
        public int AverageFrameDriftFrames => _frameDriftFrames.AverageRounded;
        public int MaxFrameDriftFrames => _frameDriftFrames.MaxAbsolute;
        public int ResyncCount { get; private set; }
        public int SnapshotMismatchCount { get; private set; }
        public int SuppressedSnapshotMismatchCount { get; private set; }
        public int ComparableSnapshotHashCount { get; private set; }
        public int MissingSnapshotHashCount { get; private set; }
        public int SnapshotRequestSentCount { get; private set; }
        public int SnapshotRequestReceivedCount { get; private set; }
        public int SnapshotRequestCount => SnapshotRequestSentCount + SnapshotRequestReceivedCount;
        public int AuthoritativeCommandSentCount { get; private set; }
        public int AuthoritativeCommandReceivedCount { get; private set; }
        public int AuthoritativeCommandEchoCount { get; private set; }
        public int AuthoritativeCommandEnqueuedCount { get; private set; }
        public int AuthoritativeCommandLateCount { get; private set; }
        public int AuthoritativeCommandDroppedCount { get; private set; }
        public int FullSnapshotSentCount { get; private set; }
        public int DigestSnapshotSentCount { get; private set; }
        public int FullSnapshotReceivedCount { get; private set; }
        public int DigestSnapshotReceivedCount { get; private set; }
        public int DeferredSnapshotCount => _deferredSnapshotMessages.Count;
        public int SnapshotApplyFailureCount => _snapshotRegistry.ApplyFailureCount;
        public bool HasPendingFullSnapshotReceivers => _peersNeedingFullSnapshot.Count > 0;
        public IReadOnlyCollection<string> ConnectedPeerIds => _connectedPeers;
        public IReadOnlyList<RuntimeCommandError> LastCommandErrors => _commandErrors;
        public IReadOnlyList<string> RecentDiagnostics => _diagnostics.RecentMessages;
        public int BoundedPacketReceivedCount { get; private set; }
        public int BoundedPacketRejectedCount { get; private set; }

        /// <summary>Enables the additive protocol-2 bounded packet path on this existing session runner.</summary>
        /// <remarks>AllocByDesign initialization operation. Protocol-1 sessions cannot enable this path.</remarks>
        public void ConfigureBoundedProtocol(
            ulong sessionHandle,
            uint localPeerHandle,
            long profileHash,
            INetworkBoundedPacketIngress ingress,
            int maxPacketBytes = NetworkBoundedWire.SnapshotTotalLimit)
        {
            if (Config.ProtocolVersion != NetworkBoundedSessionNegotiation.RequiredProtocolVersion)
                throw new InvalidOperationException("Bounded packets require negotiated protocol 2.");
            if (sessionHandle == 0UL) throw new ArgumentOutOfRangeException(nameof(sessionHandle));
            if (localPeerHandle == 0U) throw new ArgumentOutOfRangeException(nameof(localPeerHandle));
            if (ingress == null) throw new ArgumentNullException(nameof(ingress));
            if (maxPacketBytes < NetworkBoundedWire.HeaderBytes
                || maxPacketBytes > NetworkBoundedWire.SnapshotTotalLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }
            _boundedTransport = _transport as INetworkBoundedPacketTransport;
            if (_boundedTransport == null)
                throw new InvalidOperationException("Configured transport does not expose the bounded packet seam.");
            _boundedIngress = ingress;
            _boundedReceiveBuffer = new byte[maxPacketBytes];
            _boundedSessionHandle = sessionHandle;
            _boundedLocalPeerHandle = localPeerHandle;
            _boundedProfileHash = profileHash;
        }

        public void Connect(RuntimeFrame frame)
        {
            if (Role == NetworkSessionRole.Server)
            {
                Status = NetworkSessionStatus.Connected;
                DisconnectReason = string.Empty;
                return;
            }

            _requiresReconnectSnapshot |= Status == NetworkSessionStatus.Resynchronizing
                || Status == NetworkSessionStatus.RecoveryFailed;
            if (_requiresReconnectSnapshot)
            {
                _sequenceTracker.ResetSender(ServerPeerId);
                _hasLastSnapshotIntervalFrame = false;
            }
            _deferredSnapshotMessages.Clear();
            _lastSnapshotRequestFrame = long.MinValue;
            Status = NetworkSessionStatus.Connecting;
            DisconnectReason = string.Empty;
            Send(
                ServerPeerId,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.HandshakeRequest,
                frame,
                new NetworkHandshakeRequest(PeerId, Role, Config));
        }

        public void RegisterParticipant(INetworkSnapshotParticipant participant)
        {
            _snapshotRegistry.RegisterParticipant(participant);
        }

        /// <remarks>LowFreqAlloc: session-selection control setup only.</remarks>
        public void SetApplicationControlHandler(
            Func<string, NetworkApplicationControlRequestPacket, NetworkApplicationControlResponsePacket> handler)
        {
            if (Role != NetworkSessionRole.Server)
                throw new InvalidOperationException("Only a server session can handle application control requests.");
            _applicationControlHandler = handler;
        }

        /// <remarks>LowFreqAlloc: session-selection control setup only.</remarks>
        public void SetApplicationControlResponseObserver(
            Action<NetworkApplicationControlResponsePacket> observer)
        {
            if (Role == NetworkSessionRole.Server)
                throw new InvalidOperationException("A server session does not observe application control responses.");
            _applicationControlResponseObserver = observer;
        }

        /// <remarks>LowFreqAlloc: reliable pre-session application control only.</remarks>
        public void SubmitApplicationControl(
            RuntimeFrame frame,
            NetworkApplicationControlRequestPacket request)
        {
            if (Role == NetworkSessionRole.Server)
                throw new InvalidOperationException("Server sessions do not submit application control requests.");
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (Status != NetworkSessionStatus.Connected)
                return;
            Send(
                ServerPeerId,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.ApplicationControlRequest,
                frame,
                request);
        }

        public void SubmitCommands(RuntimeFrame clientFrame, IReadOnlyList<RuntimeCommand> commands)
        {
            if (Role == NetworkSessionRole.Server)
                throw new InvalidOperationException("Server sessions do not submit client command packets.");
            if (Status != NetworkSessionStatus.Connected)
                return;

            Send(
                ServerPeerId,
                RuntimeCommandChannel,
                NetworkMessageKind.RuntimeCommands,
                clientFrame,
                new NetworkRuntimeCommandPacket(PeerId, clientFrame, commands));
        }

        public void RequestFullSnapshot(RuntimeFrame frame, string reason = "")
        {
            if (Role == NetworkSessionRole.Server || Status != NetworkSessionStatus.Connected)
                return;
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
                return;

            SendSnapshotRequest(
                frame,
                LastServerFrame,
                localHash: 0L,
                remoteHash: LastSnapshotHash,
                consecutiveMismatchCount: _consecutiveComparableSnapshotMismatches,
                reason: reason);
        }

        public void ProcessIncoming(RuntimeFrame frame)
        {
            ProcessIncoming(frame, frame);
        }

        public void ProcessIncoming(RuntimeFrame frame, RuntimeFrame localSimulationFrame)
        {
            ProcessTransportEvents(frame);

            ProcessBoundedIncoming(frame);

            _inbox.Clear();
            if (_deferredSnapshotMessages.Count > 0)
            {
                _inbox.AddRange(_deferredSnapshotMessages);
                _deferredSnapshotMessages.Clear();
            }

            _transport.Poll(PeerId, frame, _inbox);
            _inbox.Sort(MessageComparison);
            for (int i = 0; i < _inbox.Count; i++)
                ProcessMessage(_inbox[i], frame, localSimulationFrame);
            RetryRecoveryRequest(frame);
        }

        /// <summary>Stops authority admission after a correction cannot be completed.</summary>
        /// <remarks>LowFreqAlloc. BootstrapOnly fails closed; verification sessions request existing full snapshots.</remarks>
        public void RequireRecovery(RuntimeFrame frame, string reason)
        {
            if (Role == NetworkSessionRole.Server
                || (Status != NetworkSessionStatus.Connected && Status != NetworkSessionStatus.Resynchronizing))
                return;
            RecoveryReason = string.IsNullOrEmpty(reason) ? "authority correction failed" : reason;
            if (LastServerFrame > _recoveryMinimumFrame) _recoveryMinimumFrame = LastServerFrame;
            Status = Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly
                ? NetworkSessionStatus.RecoveryFailed : NetworkSessionStatus.Resynchronizing;
            AddDiagnostic("authority recovery required: " + RecoveryReason);
            RetryRecoveryRequest(frame);
        }

        private void RetryRecoveryRequest(RuntimeFrame frame)
        {
            if (Status != NetworkSessionStatus.Resynchronizing
                || Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
                return;
            if (_lastSnapshotRequestFrame != long.MinValue
                && frame.Value - _lastSnapshotRequestFrame < Math.Max(1, _snapshotRequestCooldownFrames))
                return;
            SendSnapshotRequest(frame, _recoveryMinimumFrame, 0L, LastSnapshotHash, 0, RecoveryReason);
        }

        private void ProcessBoundedIncoming(RuntimeFrame frame)
        {
            if (_boundedTransport == null || _boundedIngress == null)
                return;
            for (int i = 0; i < 128; i++)
            {
                NetworkBoundedResult poll = _boundedTransport.TryPollBounded(
                    _boundedLocalPeerHandle,
                    frame,
                    _boundedReceiveBuffer,
                    out int packetBytes,
                    out NetworkBoundedAuthenticatedMetadata authenticatedMetadata);
                if (!poll.Success)
                {
                    BoundedPacketRejectedCount++;
                    return;
                }
                if (packetBytes == 0)
                    return;
                ReadOnlySpan<byte> packet = _boundedReceiveBuffer.AsSpan(0, packetBytes);
                if (!TryValidateAuthenticatedBoundedEnvelope(packet, authenticatedMetadata))
                {
                    BoundedPacketRejectedCount++;
                    continue;
                }
                var context = new NetworkBoundedIngressContext(
                    authenticatedMetadata,
                    Role,
                    Status,
                    frame,
                    _boundedProfileHash);
                NetworkBoundedResult admitted = _boundedIngress.TryAdmit(
                    packet,
                    context);
                if (admitted.Success)
                    BoundedPacketReceivedCount++;
                else
                    BoundedPacketRejectedCount++;
            }
        }

        private bool TryValidateAuthenticatedBoundedEnvelope(
            ReadOnlySpan<byte> packet,
            in NetworkBoundedAuthenticatedMetadata authenticatedMetadata)
        {
            if (Status != NetworkSessionStatus.Connected
                || !authenticatedMetadata.IsValid
                || authenticatedMetadata.SessionHandle != _boundedSessionHandle
                || authenticatedMetadata.LocalPeerHandle != _boundedLocalPeerHandle)
            {
                return false;
            }

            NetworkBoundedResult wire = NetworkBoundedWire.TryDecode(packet, out NetworkBoundedPacketHeader header);
            if (!wire.Success
                || header.SessionHandle != authenticatedMetadata.SessionHandle
                || header.SenderPeerHandle != authenticatedMetadata.RemotePeerHandle
                || header.ReceiverPeerHandle != authenticatedMetadata.LocalPeerHandle)
            {
                return false;
            }

            switch (header.Kind)
            {
                case NetworkMessageKind.BoundedRuntimeCommands:
                case NetworkMessageKind.BoundedStateRequest:
                    return authenticatedMetadata.Direction == NetworkBoundedPacketDirection.ClientToServer
                        && Role == NetworkSessionRole.Server;
                case NetworkMessageKind.BoundedAuthoritativeCommands:
                case NetworkMessageKind.BoundedStateSnapshot:
                    return authenticatedMetadata.Direction == NetworkBoundedPacketDirection.ServerToClient
                        && Role != NetworkSessionRole.Server;
                default:
                    return false;
            }
        }

        public void SendPing(RuntimeFrame frame)
        {
            if (Status != NetworkSessionStatus.Connected)
                return;

            if (Role == NetworkSessionRole.Server)
            {
                foreach (string peerId in _connectedPeers)
                {
                    Send(
                        peerId,
                        HeartbeatChannel,
                        NetworkMessageKind.Ping,
                        frame,
                        new NetworkHeartbeatPacket(PeerId, frame, _nextHeartbeatSequence++));
                }

                return;
            }

            Send(
                ServerPeerId,
                HeartbeatChannel,
                NetworkMessageKind.Ping,
                frame,
                new NetworkHeartbeatPacket(PeerId, frame, _nextHeartbeatSequence++));
        }

        public void BroadcastSnapshot(RuntimeFrame authoritativeFrame, bool fullSnapshot = true)
        {
            if (Role != NetworkSessionRole.Server)
                return;
            if (_connectedPeers.Count == 0)
                return;
            bool bootstrapOnly = Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly;
            if (bootstrapOnly && !fullSnapshot && _peersNeedingFullSnapshot.Count == 0)
                return;
            if (!bootstrapOnly
                && authoritativeFrame.Value % Config.SnapshotIntervalFrames != 0L
                && !fullSnapshot
                && _peersNeedingFullSnapshot.Count == 0)
            {
                return;
            }

            foreach (string peerId in _connectedPeers)
            {
                bool peerNeedsFullSnapshot = (fullSnapshot && !bootstrapOnly)
                    || _peersNeedingFullSnapshot.Contains(peerId);
                if (bootstrapOnly && !peerNeedsFullSnapshot)
                    continue;

                NetworkSnapshotPacket packet = CaptureSnapshot(
                    authoritativeFrame,
                    peerNeedsFullSnapshot,
                    GetLastAckedInputFrame(peerId),
                    peerId);

                Send(
                    peerId,
                    peerNeedsFullSnapshot ? NetworkChannel.ReliableOrdered : NetworkChannel.Unreliable,
                    NetworkMessageKind.Snapshot,
                    authoritativeFrame,
                    packet);
                if (peerNeedsFullSnapshot)
                    FullSnapshotSentCount++;
                else
                    DigestSnapshotSentCount++;
                if (peerNeedsFullSnapshot)
                    _peersNeedingFullSnapshot.Remove(peerId);
            }
        }

        public NetworkSnapshotPacket CaptureSnapshot(RuntimeFrame authoritativeFrame, bool fullSnapshot)
        {
            return CaptureSnapshot(authoritativeFrame, fullSnapshot, LastAckedInputFrame, string.Empty);
        }

        public void RequestFullSnapshotForPeer(string peerId)
        {
            if (Role != NetworkSessionRole.Server)
                return;

            string key = ResolvePeerKey(peerId, string.Empty);
            if (!string.Equals(key, "unknown", StringComparison.Ordinal))
                _peersNeedingFullSnapshot.Add(key);
        }

        public bool IsPeerConnected(string peerId)
        {
            string key = ResolvePeerKey(peerId, string.Empty);
            return _connectedPeers.Contains(key);
        }

        public RuntimeFrame GetLastAckedInputFrame(string peerId)
        {
            string key = ResolvePeerKey(peerId, string.Empty);
            return _lastAckedInputFrameByPeer.TryGetValue(key, out RuntimeFrame frame)
                ? frame
                : RuntimeFrame.Zero;
        }

        private NetworkSnapshotPacket CaptureSnapshot(
            RuntimeFrame authoritativeFrame,
            bool fullSnapshot,
            RuntimeFrame ackedInputFrame,
            string receiverPeerId)
        {
            long resultHash = ResolveResultHash(authoritativeFrame, receiverPeerId);
            return _snapshotRegistry.Capture(
                authoritativeFrame,
                ackedInputFrame,
                fullSnapshot,
                resultHash,
                receiverPeerId);
        }

        public NetworkTransportSnapshot CreateTransportSnapshot()
        {
            return _transport.CreateSnapshot(PeerId);
        }

        private void RegisterParticipants(IEnumerable<INetworkSnapshotParticipant> participants)
        {
            if (participants == null)
                return;

            foreach (INetworkSnapshotParticipant participant in participants)
                RegisterParticipant(participant);
        }

        private void ProcessTransportEvents(RuntimeFrame frame)
        {
            _events.Clear();
            _transport.PollEvents(PeerId, frame, _events);
            for (int i = 0; i < _events.Count; i++)
                ProcessTransportEvent(_events[i]);
        }

        private void ProcessTransportEvent(NetworkTransportEvent evt)
        {
            string peerLabel = string.IsNullOrEmpty(evt.PeerId) ? "unknown" : evt.PeerId;
            switch (evt.Kind)
            {
                case NetworkTransportEventKind.Connected:
                    AddDiagnostic("transport connected: " + peerLabel, RuntimeLogLevel.Info);
                    if (Role != NetworkSessionRole.Server && Status == NetworkSessionStatus.Disconnected)
                        Status = NetworkSessionStatus.Connecting;
                    break;
                case NetworkTransportEventKind.Disconnected:
                    HandleDisconnectedEvent(evt, "transport disconnected", RuntimeLogLevel.Info);
                    break;
                case NetworkTransportEventKind.Timeout:
                    HandleDisconnectedEvent(evt, "transport timeout", RuntimeLogLevel.Warning);
                    break;
                case NetworkTransportEventKind.Error:
                    DisconnectReason = evt.Message;
                    AddDiagnostic("transport error: " + peerLabel + " " + evt.Message);
                    break;
            }
        }

        private void HandleDisconnectedEvent(NetworkTransportEvent evt, string reason, RuntimeLogLevel serverLogLevel)
        {
            string message = string.IsNullOrEmpty(evt.Message) ? reason : reason + ": " + evt.Message;
            if (Role == NetworkSessionRole.Server)
            {
                if (!string.IsNullOrEmpty(evt.PeerId))
                {
                    _sequenceTracker.ResetSender(evt.PeerId);
                    _connectedPeers.Remove(evt.PeerId);
                    _peersNeedingFullSnapshot.Remove(evt.PeerId);
                    _inputScheduleStates.Remove(evt.PeerId);
                    _lastAckedInputFrameByPeer.Remove(evt.PeerId);
                }

                AddDiagnostic(
                    message + " peer=" + (string.IsNullOrEmpty(evt.PeerId) ? "unknown" : evt.PeerId),
                    serverLogLevel);
                return;
            }

            _sequenceTracker.ResetSender(string.IsNullOrEmpty(evt.PeerId) ? ServerPeerId : evt.PeerId);
            _requiresReconnectSnapshot = true;
            _deferredSnapshotMessages.Clear();
            _hasLastSnapshotIntervalFrame = false;
            Status = NetworkSessionStatus.Disconnected;
            DisconnectReason = message;
            AddDiagnostic(message);
        }

        private void ProcessMessage(
            NetworkMessageEnvelope message,
            RuntimeFrame frame,
            RuntimeFrame localSimulationFrame)
        {
            if (!string.Equals(message.SessionId, SessionId, StringComparison.Ordinal))
                return;
            if (Role != NetworkSessionRole.Server
                && (message.Kind == NetworkMessageKind.Snapshot || message.Kind == NetworkMessageKind.AuthoritativeCommands)
                && (!string.Equals(message.SenderPeerId, ServerPeerId, StringComparison.Ordinal)
                    || (_requiresReconnectSnapshot && (Status == NetworkSessionStatus.Disconnected || Status == NetworkSessionStatus.Connecting))
                    || Status == NetworkSessionStatus.Rejected || Status == NetworkSessionStatus.RecoveryFailed))
                return;
            if (ShouldDeferSnapshotUntilPredictionFrame(message, localSimulationFrame))
                return;
            if (!_sequenceTracker.ShouldProcess(message, _diagnostics))
                return;
            ObserveServerFrame(message, localSimulationFrame);

            switch (message.Kind)
            {
                case NetworkMessageKind.HandshakeRequest:
                    ProcessHandshakeRequest(message, frame);
                    break;
                case NetworkMessageKind.HandshakeResponse:
                    ProcessHandshakeResponse(message);
                    break;
                case NetworkMessageKind.RuntimeCommands:
                    ProcessCommandPacket(message, frame);
                    break;
                case NetworkMessageKind.AuthoritativeCommands:
                    ProcessAuthoritativeCommandPacket(message, frame);
                    break;
                case NetworkMessageKind.Snapshot:
                    ProcessSnapshot(message, frame, localSimulationFrame);
                    break;
                case NetworkMessageKind.SnapshotRequest:
                    ProcessSnapshotRequest(message);
                    break;
                case NetworkMessageKind.Ping:
                    ProcessPing(message, frame);
                    break;
                case NetworkMessageKind.Pong:
                    ProcessPong(message, frame);
                    break;
                case NetworkMessageKind.ApplicationControlRequest:
                    ProcessApplicationControlRequest(message, frame);
                    break;
                case NetworkMessageKind.ApplicationControlResponse:
                    ProcessApplicationControlResponse(message);
                    break;
                default:
                AddDiagnostic("unsupported message kind: " + (int)message.Kind);
                break;
            }
        }

        private void ProcessApplicationControlRequest(
            NetworkMessageEnvelope message,
            RuntimeFrame frame)
        {
            if (Role != NetworkSessionRole.Server)
                return;
            if (!message.TryGetPayload(out NetworkApplicationControlRequestPacket request))
            {
                AddDiagnostic("application control request payload mismatch");
                return;
            }
            if (string.IsNullOrEmpty(message.SenderPeerId)
                || !_connectedPeers.Contains(message.SenderPeerId))
            {
                AddDiagnostic(
                    "application control request rejected for unconnected peer: "
                    + (message.SenderPeerId ?? string.Empty));
                return;
            }

            NetworkApplicationControlResponsePacket response;
            if (_applicationControlHandler == null)
            {
                response = new NetworkApplicationControlResponsePacket(
                    request.TypeId,
                    request.SchemaVersion,
                    request.RequestId,
                    accepted: false,
                    reason: "application control handler is not configured");
            }
            else
            {
                try
                {
                    response = _applicationControlHandler(message.SenderPeerId, request);
                }
                catch (Exception exception)
                {
                    response = new NetworkApplicationControlResponsePacket(
                        request.TypeId,
                        request.SchemaVersion,
                        request.RequestId,
                        accepted: false,
                        reason: "application control handler failed: " + exception.Message);
                }
                if (response == null
                    || response.RequestId != request.RequestId
                    || response.SchemaVersion != request.SchemaVersion
                    || !string.Equals(response.TypeId, request.TypeId, StringComparison.Ordinal))
                {
                    response = new NetworkApplicationControlResponsePacket(
                        request.TypeId,
                        request.SchemaVersion,
                        request.RequestId,
                        accepted: false,
                        reason: "application control handler returned a mismatched response");
                }
            }

            Send(
                message.SenderPeerId,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.ApplicationControlResponse,
                frame,
                response);
        }

        private void ProcessApplicationControlResponse(NetworkMessageEnvelope message)
        {
            if (Role == NetworkSessionRole.Server)
                return;
            if (!message.TryGetPayload(out NetworkApplicationControlResponsePacket response))
            {
                AddDiagnostic("application control response payload mismatch");
                return;
            }
            _applicationControlResponseObserver?.Invoke(response);
        }

        private bool ShouldDeferSnapshotUntilPredictionFrame(
            NetworkMessageEnvelope message,
            RuntimeFrame localSimulationFrame)
        {
            if (Role == NetworkSessionRole.Server || message.Kind != NetworkMessageKind.Snapshot)
                return false;
            if (!message.TryGetPayload(out NetworkSnapshotPacket packet))
                return false;
            if (packet.FullSnapshot || packet.ModuleSnapshots.Count > 0)
                return false;
            if (packet.AuthoritativeFrame < localSimulationFrame)
                return false;
            if (TryResolveLocalSnapshotHash(packet.AuthoritativeFrame, out _, out bool missingHistory))
                return false;
            if (!missingHistory)
                return false;

            _deferredSnapshotMessages.Add(message);
            return true;
        }

        private void ProcessHandshakeRequest(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (Role != NetworkSessionRole.Server)
                return;

            if (!message.TryGetPayload(out NetworkHandshakeRequest request))
            {
                AddDiagnostic("handshake request payload mismatch");
                return;
            }

            if (string.IsNullOrEmpty(message.SenderPeerId)
                || !string.Equals(request.PeerId, message.SenderPeerId, StringComparison.Ordinal))
            {
                AddDiagnostic("handshake request sender does not match requested peer id");
                return;
            }

            bool accepted = request.Config.IsCompatibleWith(Config, out string reason);
            if (accepted)
            {
                _connectedPeers.Add(request.PeerId);
                _peersNeedingFullSnapshot.Add(request.PeerId);
                _inputScheduleStates.Remove(request.PeerId);
                _lastAckedInputFrameByPeer.Remove(request.PeerId);
            }

            Send(
                request.PeerId,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.HandshakeResponse,
                frame,
                new NetworkHandshakeResponse(accepted, reason, Config));
        }

        private void ProcessHandshakeResponse(NetworkMessageEnvelope message)
        {
            if (Role == NetworkSessionRole.Server || Status != NetworkSessionStatus.Connecting)
                return;

            if (!message.TryGetPayload(out NetworkHandshakeResponse response))
            {
                AddDiagnostic("handshake response payload mismatch");
                return;
            }

            if (response.Accepted)
            {
                Status = _requiresReconnectSnapshot
                    ? NetworkSessionStatus.Resynchronizing : NetworkSessionStatus.Connected;
                if (_requiresReconnectSnapshot && LastServerFrame > _recoveryMinimumFrame)
                    _recoveryMinimumFrame = LastServerFrame;
                RejectionReason = string.Empty;
                DisconnectReason = string.Empty;
                return;
            }

            Status = NetworkSessionStatus.Rejected;
            RejectionReason = response.Reason;
        }

        private void ProcessCommandPacket(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (Role != NetworkSessionRole.Server || _runtimeCommandBuffer == null)
                return;

            if (!message.TryGetPayload(out NetworkRuntimeCommandPacket packet))
            {
                AddDiagnostic("runtime command payload mismatch");
                return;
            }

            string inputPeerId = ResolvePeerKey(packet.InputPeerId, message.SenderPeerId);
            NetworkInputScheduleState schedule = GetOrCreateInputScheduleState(inputPeerId);
            var acceptedCommands = new List<RuntimeCommand>(packet.Commands.Count);
            for (int i = 0; i < packet.Commands.Count; i++)
            {
                RuntimeCommand command = packet.Commands[i];
                if (!AuthorizeCommand(inputPeerId, command, frame))
                    continue;

                RuntimeCommand scheduled = schedule.Schedule(
                    command,
                    frame,
                    ResolveAuthoritativeCommandLeadFrames());
                RuntimeCommandValidationResult result = _runtimeCommandBuffer.Enqueue(scheduled);
                if (!result.Success)
                {
                    _commandErrors.Add(result.Error);
                    AddDiagnostic(
                        "runtime command rejected: command="
                        + command.CommandId
                        + " clientFrame="
                        + command.Frame.Value
                        + " serverFrame="
                        + frame.Value
                        + " scheduledFrame="
                        + scheduled.Frame.Value
                        + " reason="
                        + result.Error.Message);
                }
                else
                {
                    RecordAckedInputFrame(inputPeerId, command.Frame);
                    acceptedCommands.Add(result.Command);
                }
            }

            if (acceptedCommands.Count > 0)
            {
                BroadcastAuthoritativeCommands(
                    inputPeerId,
                    frame,
                    GetLastAckedInputFrame(inputPeerId),
                    acceptedCommands);
            }
        }

        private void BroadcastAuthoritativeCommands(
            string inputPeerId,
            RuntimeFrame serverFrame,
            RuntimeFrame ackedInputFrame,
            IReadOnlyList<RuntimeCommand> commands)
        {
            if (Role != NetworkSessionRole.Server || commands == null || commands.Count == 0)
                return;

            var packet = new NetworkAuthoritativeCommandPacket(
                inputPeerId,
                serverFrame,
                ackedInputFrame,
                commands);
            _authoritativeCommandObserver?.Invoke(packet);

            foreach (string peerId in _connectedPeers)
            {
                Send(
                    peerId,
                    RuntimeCommandChannel,
                    NetworkMessageKind.AuthoritativeCommands,
                    serverFrame,
                    packet);
                AuthoritativeCommandSentCount += commands.Count;
            }
        }

        private void ProcessSnapshotRequest(NetworkMessageEnvelope message)
        {
            if (Role != NetworkSessionRole.Server)
                return;
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
            {
                AddDiagnostic("snapshot request ignored: stateSyncPolicy=BootstrapOnly", RuntimeLogLevel.Info);
                return;
            }

            if (!message.TryGetPayload(out NetworkSnapshotRequestPacket packet))
            {
                AddDiagnostic("snapshot request payload mismatch");
                return;
            }

            string peerId = ResolvePeerKey(packet.PeerId, message.SenderPeerId);
            _peersNeedingFullSnapshot.Add(peerId);
            SnapshotRequestReceivedCount++;
            AddDiagnostic(
                "full snapshot requested: peer="
                + peerId
                + " authoritativeFrame="
                + packet.AuthoritativeFrame.Value
                + " reason="
                + packet.Reason,
                RuntimeLogLevel.Info);
        }

        private void ProcessAuthoritativeCommandPacket(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (Role == NetworkSessionRole.Server)
                return;

            if (!message.TryGetPayload(out NetworkAuthoritativeCommandPacket packet))
            {
                AddDiagnostic("authoritative command payload mismatch");
                return;
            }

            AuthoritativeCommandReceivedCount += packet.Commands.Count;
            if (Status == NetworkSessionStatus.Resynchronizing && _authoritativeCommandReceiveHandler == null)
            {
                Status = NetworkSessionStatus.RecoveryFailed;
                RecoveryReason = "authoritative commands arrived during recovery without a retaining correction handler";
                AddDiagnostic(RecoveryReason);
                return;
            }
            if (string.Equals(packet.InputPeerId, PeerId, StringComparison.Ordinal))
            {
                LastAckedInputFrame = packet.AckedInputFrame;
                AuthoritativeCommandEchoCount += packet.Commands.Count;
            }

            if (_authoritativeCommandReceiveHandler != null
                && _authoritativeCommandReceiveHandler(packet, frame))
            {
                return;
            }
            if (Status == NetworkSessionStatus.Resynchronizing)
            {
                Status = NetworkSessionStatus.RecoveryFailed;
                RecoveryReason = "correction handler did not retain commands during recovery";
                AddDiagnostic(RecoveryReason);
                return;
            }

            if (string.Equals(packet.InputPeerId, PeerId, StringComparison.Ordinal)
                && !_applyOwnAuthoritativeCommands)
            {
                return;
            }

            if (_runtimeCommandBuffer == null)
            {
                AuthoritativeCommandDroppedCount += packet.Commands.Count;
                return;
            }

            bool requestedResync = false;
            int lateCommandCount = 0;
            RuntimeCommand lastLateCommand = default;
            for (int i = 0; i < packet.Commands.Count; i++)
            {
                RuntimeCommand command = packet.Commands[i];
                if (command.Frame < _runtimeCommandBuffer.CurrentFrame)
                {
                    AuthoritativeCommandLateCount++;
                    lateCommandCount++;
                    lastLateCommand = command;
                    continue;
                }

                RuntimeCommandValidationResult result = _runtimeCommandBuffer.Enqueue(command);
                if (!result.Success)
                {
                    AuthoritativeCommandDroppedCount++;
                    _commandErrors.Add(result.Error);
                    AddDiagnostic(
                        "authoritative command rejected: command="
                        + command.CommandId
                        + " frame="
                        + command.Frame.Value
                        + " reason="
                        + result.Error.Message);
                    continue;
                }

                AuthoritativeCommandEnqueuedCount++;
            }

            if (lateCommandCount > 0)
            {
                RequireRecovery(frame, "late authoritative commands could not be replayed");
                requestedResync = Status == NetworkSessionStatus.Resynchronizing;
                AddLateAuthoritativeCommandDiagnostic(
                    frame,
                    packet.InputPeerId,
                    lastLateCommand,
                    lateCommandCount,
                    requestedResync);
            }
        }

        private void ProcessSnapshot(
            NetworkMessageEnvelope message,
            RuntimeFrame frame,
            RuntimeFrame localSimulationFrame)
        {
            if (Role == NetworkSessionRole.Server)
                return;

            if (!message.TryGetPayload(out NetworkSnapshotPacket packet))
            {
                AddDiagnostic("snapshot payload mismatch");
                return;
            }

            if (_hasLastSnapshotIntervalFrame
                && packet.AuthoritativeFrame < _lastSnapshotIntervalFrame)
                return;
            if (packet.FullSnapshot && _hasAppliedFullSnapshot
                && (packet.AuthoritativeFrame < _lastAppliedFullSnapshotFrame
                    || IsDuplicateFullSnapshot(packet)) && !_requiresReconnectSnapshot)
                return;
            if (Status == NetworkSessionStatus.Resynchronizing
                && packet.AuthoritativeFrame < _recoveryMinimumFrame)
                return;

            RecordSnapshotTiming(packet.AuthoritativeFrame, localSimulationFrame);
            if (packet.AuthoritativeFrame > LastServerFrame)
                LastServerFrame = packet.AuthoritativeFrame;
            LastAckedInputFrame = packet.AckedInputFrame;
            LastSnapshotHash = packet.ResultHash;
            if (packet.FullSnapshot)
                FullSnapshotReceivedCount++;
            else
                DigestSnapshotReceivedCount++;

            bool mismatch = false;
            bool compared = TryResolveLocalSnapshotHash(packet.AuthoritativeFrame, out long localHash, out bool missingHistory);
            if (compared)
            {
                ComparableSnapshotHashCount++;
                mismatch = localHash != packet.ResultHash;
                if (mismatch)
                {
                    SnapshotMismatchCount++;
                    _consecutiveComparableSnapshotMismatches++;
                }
                else
                {
                    _consecutiveComparableSnapshotMismatches = 0;
                }
            }
            else if (missingHistory)
            {
                MissingSnapshotHashCount++;
                if (!packet.FullSnapshot)
                    RequireRecovery(frame, "snapshot hash history missing at frame " + packet.AuthoritativeFrame.Value);
            }

            bool digestMismatchWithoutState = mismatch
                && !packet.FullSnapshot
                && packet.ModuleSnapshots.Count == 0;
            bool needsApply = packet.FullSnapshot
                || (mismatch
                    && packet.ModuleSnapshots.Count > 0
                    && SnapshotApplyPolicy == NetworkSnapshotApplyPolicy.ApplyOnMismatch);
            if (mismatch
                && !packet.FullSnapshot
                && (SnapshotApplyPolicy == NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly
                    || digestMismatchWithoutState))
            {
                SuppressedSnapshotMismatchCount++;
                TryRequestFullSnapshotAfterMismatch(frame, packet, localHash);
            }

            if (!needsApply)
                return;
            if (packet.FullSnapshot && !_snapshotRegistry.ValidateFullSnapshot(packet, _diagnostics))
            {
                RequireRecovery(frame, "full snapshot participant coverage is incomplete");
                return;
            }

            if (_snapshotCorrectionHandler != null)
            {
                NetworkSnapshotCorrectionResult correction =
                    _snapshotCorrectionHandler(packet, frame, localSimulationFrame);
                if (correction != null && correction.Handled)
                {
                    if (correction.Applied)
                    {
                        ResyncCount++;
                        _consecutiveComparableSnapshotMismatches = 0;
                        CompleteRecovery(packet);
                    }
                    else if (!string.IsNullOrEmpty(correction.Message))
                    {
                        AddDiagnostic(correction.Message);
                    }
                    if (!correction.Applied)
                    {
                        Status = NetworkSessionStatus.RecoveryFailed;
                        RecoveryReason = string.IsNullOrEmpty(correction.Message)
                            ? "full snapshot correction failed" : correction.Message;
                    }

                    return;
                }
            }

            bool applied = ApplySnapshot(packet);
            if (applied)
            {
                ResyncCount++;
                _consecutiveComparableSnapshotMismatches = 0;
                CompleteRecovery(packet);
            }
            else
            {
                Status = NetworkSessionStatus.RecoveryFailed;
                RecoveryReason = "snapshot apply failed; authority may be partially restored";
            }
        }

        private void CompleteRecovery(NetworkSnapshotPacket packet)
        {
            if (!packet.FullSnapshot) return;
            _lastAppliedFullSnapshotFrame = packet.AuthoritativeFrame;
            _hasAppliedFullSnapshot = true;
            _lastAppliedFullSnapshot = packet;
            _requiresReconnectSnapshot = false;
            RecoveryReason = string.Empty;
            if (Status == NetworkSessionStatus.Resynchronizing)
                Status = NetworkSessionStatus.Connected;
            _lastSnapshotRequestFrame = long.MinValue;
            _deferredSnapshotMessages.Clear();
        }

        private bool IsDuplicateFullSnapshot(NetworkSnapshotPacket packet)
        {
            if (_lastAppliedFullSnapshot == null || packet.AuthoritativeFrame != _lastAppliedFullSnapshotFrame
                || packet.ResultHash != _lastAppliedFullSnapshot.ResultHash
                || packet.ModuleSnapshots.Count != _lastAppliedFullSnapshot.ModuleSnapshots.Count)
                return false;
            for (int i = 0; i < packet.ModuleSnapshots.Count; i++)
            {
                RuntimeCustomState left = packet.ModuleSnapshots[i];
                RuntimeCustomState right = _lastAppliedFullSnapshot.ModuleSnapshots[i];
                if (left == null || right == null || left.NumericTypeId != right.NumericTypeId
                    || left.SchemaVersion != right.SchemaVersion
                    || !string.Equals(left.TypeId, right.TypeId, StringComparison.Ordinal)
                    || !string.Equals(left.PayloadJson, right.PayloadJson, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private void ProcessPing(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (!message.TryGetPayload(out NetworkHeartbeatPacket packet))
            {
                AddDiagnostic("ping payload mismatch");
                return;
            }

            Send(
                message.SenderPeerId,
                HeartbeatChannel,
                NetworkMessageKind.Pong,
                frame,
                new NetworkHeartbeatPacket(PeerId, packet.SentFrame, packet.PingSequence));
        }

        private void ProcessPong(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (!message.TryGetPayload(out NetworkHeartbeatPacket packet))
            {
                AddDiagnostic("pong payload mismatch");
                return;
            }

            long rtt = frame.Value - packet.SentFrame.Value;
            _rttFrames.Add(ClampToInt(Math.Max(0L, rtt)));
            RttEstimateFrames = _rttFrames.AverageRounded;
        }

        private bool ApplySnapshot(NetworkSnapshotPacket packet)
        {
            return _snapshotRegistry.Apply(packet, _diagnostics);
        }

        private bool TryResolveLocalSnapshotHash(RuntimeFrame frame, out long hash, out bool missingHistory)
        {
            hash = 0L;
            missingHistory = false;
            if (_snapshotHashLookup != null)
            {
                NetworkSnapshotHashLookupResult result = _snapshotHashLookup(frame);
                if (!result.HasHash)
                {
                    missingHistory = true;
                    return false;
                }

                hash = result.Hash;
                return true;
            }

            if (_resultHashProvider == null)
                return false;

            hash = _resultHashProvider(frame);
            return true;
        }

        private long ResolveResultHash(RuntimeFrame frame, string receiverPeerId)
        {
            if (_peerResultHashProvider != null && !string.IsNullOrEmpty(receiverPeerId))
                return _peerResultHashProvider(frame, receiverPeerId);
            return _resultHashProvider != null ? _resultHashProvider(frame) : 0L;
        }

        private bool AuthorizeCommand(string inputPeerId, RuntimeCommand command, RuntimeFrame frame)
        {
            if (_commandAuthorizer == null)
                return true;

            NetworkCommandAuthorizationResult authorization = _commandAuthorizer(inputPeerId, command);
            if (authorization.Accepted)
                return true;

            string reason = string.IsNullOrEmpty(authorization.Reason)
                ? "command was rejected by network authority"
                : authorization.Reason;
            var error = new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                frame,
                reason);
            _commandErrors.Add(error);
            AddDiagnostic(
                "runtime command unauthorized: peer="
                + inputPeerId
                + " command="
                + command.CommandId
                + " target="
                + command.TargetId
                + " clientFrame="
                + command.Frame.Value
                + " serverFrame="
                + frame.Value
                + " reason="
                + reason);
            return false;
        }

        private void TryRequestFullSnapshotAfterMismatch(
            RuntimeFrame frame,
            NetworkSnapshotPacket packet,
            long localHash)
        {
            if (Role == NetworkSessionRole.Server || Status != NetworkSessionStatus.Connected)
                return;
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
                return;
            if (_consecutiveComparableSnapshotMismatches < _snapshotRequestMismatchThreshold)
                return;
            if (_lastSnapshotRequestFrame != long.MinValue
                && frame.Value - _lastSnapshotRequestFrame < _snapshotRequestCooldownFrames)
            {
                return;
            }

            AddDiagnostic(
                "snapshot hash mismatch request: authoritativeFrame="
                + packet.AuthoritativeFrame.Value
                + " localHash="
                + localHash
                + " remoteHash="
                + packet.ResultHash
                + " consecutive="
                + _consecutiveComparableSnapshotMismatches,
                RuntimeLogLevel.Info);
            SendSnapshotRequest(
                frame,
                packet.AuthoritativeFrame,
                localHash,
                packet.ResultHash,
                _consecutiveComparableSnapshotMismatches,
                "snapshot hash mismatch");
        }

        private void AddLateAuthoritativeCommandDiagnostic(
            RuntimeFrame frame,
            string inputPeerId,
            RuntimeCommand lastLateCommand,
            int lateCommandCount,
            bool requestedResync)
        {
            if (_lastLateAuthoritativeCommandWarningFrame != long.MinValue
                && frame.Value - _lastLateAuthoritativeCommandWarningFrame < _snapshotRequestCooldownFrames)
            {
                _suppressedLateAuthoritativeCommandWarningCount += lateCommandCount;
                return;
            }

            int suppressed = _suppressedLateAuthoritativeCommandWarningCount;
            _suppressedLateAuthoritativeCommandWarningCount = 0;
            _lastLateAuthoritativeCommandWarningFrame = frame.Value;
            AddDiagnostic(
                "late authoritative commands: count="
                + lateCommandCount
                + " suppressed="
                + suppressed
                + " inputPeer="
                + inputPeerId
                + " lastCommandFrame="
                + lastLateCommand.Frame.Value
                + " bufferFrame="
                + _runtimeCommandBuffer.CurrentFrame.Value
                + " snapshotRequest="
                + (requestedResync ? "sent" : "not-sent"));
        }

        private void SendSnapshotRequest(
            RuntimeFrame frame,
            RuntimeFrame authoritativeFrame,
            long localHash,
            long remoteHash,
            int consecutiveMismatchCount,
            string reason)
        {
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
                return;

            Send(
                ServerPeerId,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.SnapshotRequest,
                frame,
                new NetworkSnapshotRequestPacket(
                    PeerId,
                    authoritativeFrame,
                    localHash,
                    remoteHash,
                    consecutiveMismatchCount,
                    reason));
            SnapshotRequestSentCount++;
            _lastSnapshotRequestFrame = frame.Value;
        }

        private int ResolveAuthoritativeCommandLeadFrames()
        {
            const int processOrderSafetyFrames = 2;
            return Config.InputDelayFrames
                + ResolveEstimatedOneWayFrames()
                + processOrderSafetyFrames;
        }

        private int ResolveEstimatedOneWayFrames()
        {
            if (RttEstimateFrames <= 0)
                return 2;

            return Math.Max(2, (RttEstimateFrames + 1) / 2);
        }

        private void Send(
            string receiverPeerId,
            NetworkChannel channel,
            NetworkMessageKind kind,
            RuntimeFrame frame,
            object payload)
        {
            _transport.Send(new NetworkMessageEnvelope(
                SessionId,
                PeerId,
                receiverPeerId,
                _nextSequence++,
                channel,
                kind,
                frame,
                payload));
        }

        private void AddDiagnostic(string message, RuntimeLogLevel level = RuntimeLogLevel.Warning)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _diagnostics.Add(message);
            _logger.Log(level, "Network." + Role, message);
        }

        private NetworkInputScheduleState GetOrCreateInputScheduleState(string inputPeerId)
        {
            string key = ResolvePeerKey(inputPeerId, string.Empty);
            if (_inputScheduleStates.TryGetValue(key, out NetworkInputScheduleState state))
                return state;

            state = new NetworkInputScheduleState();
            _inputScheduleStates.Add(key, state);
            return state;
        }

        private void RecordAckedInputFrame(string peerId, RuntimeFrame frame)
        {
            string key = ResolvePeerKey(peerId, string.Empty);
            if (!_lastAckedInputFrameByPeer.TryGetValue(key, out RuntimeFrame peerFrame)
                || frame > peerFrame)
            {
                _lastAckedInputFrameByPeer[key] = frame;
            }

            if (frame > LastAckedInputFrame)
                LastAckedInputFrame = frame;
        }

        private void RecordSnapshotTiming(RuntimeFrame authoritativeFrame, RuntimeFrame localFrame)
        {
            if (_hasLastSnapshotIntervalFrame
                && authoritativeFrame.Value > _lastSnapshotIntervalFrame.Value)
            {
                _snapshotIntervalFrames.Add(ClampToInt(authoritativeFrame.Value - _lastSnapshotIntervalFrame.Value));
            }

            _lastSnapshotIntervalFrame = authoritativeFrame;
            _hasLastSnapshotIntervalFrame = true;

            LastFrameDriftFrames = ClampToInt(localFrame.Value - authoritativeFrame.Value);
            _frameDriftFrames.Add(LastFrameDriftFrames);
        }

        private void ObserveServerFrame(
            NetworkMessageEnvelope message,
            RuntimeFrame localSimulationFrame)
        {
            if (Role != NetworkSessionRole.Client)
                return;
            if (!string.Equals(message.SenderPeerId, ServerPeerId, StringComparison.Ordinal))
                return;
            if (message.Kind != NetworkMessageKind.AuthoritativeCommands
                && message.Kind != NetworkMessageKind.HandshakeResponse
                && message.Kind != NetworkMessageKind.Ping
                && message.Kind != NetworkMessageKind.Pong)
            {
                return;
            }

            if (message.Frame <= LastServerFrame)
                return;

            LastServerFrame = message.Frame;
            LastFrameDriftFrames = ClampToInt(localSimulationFrame.Value - message.Frame.Value);
            _frameDriftFrames.Add(LastFrameDriftFrames);
        }

        private static string ResolvePeerKey(string peerId, string fallbackPeerId)
        {
            if (!string.IsNullOrEmpty(peerId))
                return peerId;
            return string.IsNullOrEmpty(fallbackPeerId) ? "unknown" : fallbackPeerId;
        }

        private static int ClampToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private sealed class NetworkFrameSampleWindow
        {
            private readonly int[] _values;
            private long _sum;
            private int _nextIndex;
            private int _count;

            public NetworkFrameSampleWindow(int capacity)
            {
                _values = new int[capacity > 0 ? capacity : 32];
            }

            public int Count => _count;

            public int AverageRounded
            {
                get
                {
                    if (_count == 0)
                        return 0;

                    long half = _count / 2L;
                    if (_sum >= 0L)
                        return ClampToInt((_sum + half) / _count);
                    return -ClampToInt(((-_sum) + half) / _count);
                }
            }

            public int Max
            {
                get
                {
                    if (_count == 0)
                        return 0;

                    int max = _values[0];
                    for (int i = 1; i < _count; i++)
                    {
                        if (_values[i] > max)
                            max = _values[i];
                    }

                    return max;
                }
            }

            public int MaxAbsolute
            {
                get
                {
                    if (_count == 0)
                        return 0;

                    long max = 0L;
                    for (int i = 0; i < _count; i++)
                    {
                        long value = _values[i];
                        long absolute = value < 0L ? -value : value;
                        if (absolute > max)
                            max = absolute;
                    }

                    return ClampToInt(max);
                }
            }

            public void Add(int value)
            {
                if (_count < _values.Length)
                {
                    _values[_count] = value;
                    _count++;
                    _sum += value;
                    _nextIndex = _count % _values.Length;
                    return;
                }

                _sum -= _values[_nextIndex];
                _values[_nextIndex] = value;
                _sum += value;
                _nextIndex = (_nextIndex + 1) % _values.Length;
            }
        }

        private sealed class NetworkInputScheduleState
        {
            private bool _hasAnchor;
            private long _anchorClientFrame;
            private long _anchorServerFrame;

            public RuntimeCommand Schedule(
                RuntimeCommand command,
                RuntimeFrame serverFrame,
                int leadFrames)
            {
                int safeLeadFrames = Math.Max(0, leadFrames);
                long serverSafeFrame = AddFrames(serverFrame.Value, safeLeadFrames);
                long clientScheduledFrame = AddFrames(command.Frame.Value, safeLeadFrames);
                long scheduledFrame;
                if (!_hasAnchor)
                {
                    scheduledFrame = Math.Max(clientScheduledFrame, serverSafeFrame);
                    SetAnchor(command.Frame.Value, scheduledFrame);
                }
                else
                {
                    scheduledFrame = _anchorServerFrame + (command.Frame.Value - _anchorClientFrame);
                    if (scheduledFrame < serverSafeFrame)
                    {
                        scheduledFrame = serverSafeFrame;
                        SetAnchor(command.Frame.Value, scheduledFrame);
                    }
                }

                if (scheduledFrame < 0L)
                    scheduledFrame = 0L;

                return new RuntimeCommand(
                    new RuntimeFrame(scheduledFrame),
                    command.SourceId,
                    command.CommandId,
                    command.TargetId,
                    command.Payload0,
                    command.Payload1,
                    command.Payload2,
                    command.TraceId,
                    command.Sequence,
                    command.Envelope);
            }

            private void SetAnchor(long clientFrame, long serverFrame)
            {
                _hasAnchor = true;
                _anchorClientFrame = clientFrame;
                _anchorServerFrame = serverFrame;
            }

            private static long AddFrames(long frame, int offset)
            {
                long result = frame + offset;
                return result < 0L ? 0L : result;
            }
        }

        private static int CompareMessages(NetworkMessageEnvelope left, NetworkMessageEnvelope right)
        {
            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
                return sequence;
            return string.CompareOrdinal(left.SenderPeerId, right.SenderPeerId);
        }
    }
}
