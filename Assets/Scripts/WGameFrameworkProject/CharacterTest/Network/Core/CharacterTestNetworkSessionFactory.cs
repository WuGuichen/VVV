using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Audio;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Network;
using MxFramework.Network.UnityTransport;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestNetworkSessionFactoryOptions
    {
        public CharacterTestNetworkMode NetworkMode { get; set; }
        public int TickRate { get; set; } = 60;
        public int InputDelayFrames { get; set; } = 2;
        public int SnapshotIntervalFrames { get; set; } = 2;
        public NetworkStateSyncPolicy StateSyncPolicy { get; set; } = NetworkStateSyncPolicy.BootstrapOnly;
        public int HeartbeatIntervalFrames { get; set; } = NetworkSessionConfig.DefaultHeartbeatIntervalFrames;
        public int LoopbackLatencyFrames { get; set; }
        public int LoopbackJitterFrames { get; set; }
        public float LoopbackDropRate { get; set; }
        public bool LoopbackReorderMessages { get; set; }
        public string UnityTransportBindAddress { get; set; } = "0.0.0.0";
        public string UnityTransportRemoteAddress { get; set; } = "127.0.0.1";
        public ushort UnityTransportPort { get; set; } = 7777;
        public int UnityTransportMaxConnections { get; set; } = 16;
        public int UnityTransportMaxPayloadBytes { get; set; } = 65536;
        public int UnityTransportReliableWindowSize { get; set; } = 64;
        public int UnityTransportReceiveQueueCapacity { get; set; } = 2048;
        public int UnityTransportSendQueueCapacity { get; set; } = 2048;
        public string UnityTransportServerPeerId { get; set; } = "server";
        public string UnityTransportClientPeerId { get; set; } = "client-0";
        public string NetworkRoomId { get; set; } = "character-test-room";
        public string NetworkRoomName { get; set; } = "CharacterTest LAN Room";
        public int NetworkRoomMaxMembers { get; set; } = 4;
        public int NetworkRoomMinReadyMembers { get; set; } = 1;
        public bool NetworkRoomClientAutoReady { get; set; } = true;
        public bool NetworkRoomServerAutoStartWhenReady { get; set; } = true;
        public string NetworkRoomClientDisplayName { get; set; } = "Player";
        public IAudioService AudioService { get; set; }
        public ICharacterTestAudioCueCatalog AudioCueCatalog { get; set; }
        public bool DisposeAudioService { get; set; }
        public CharacterTestRuntimeAiPlannerProfile RuntimeAiPlannerProfile { get; set; }
        public CharacterTestRuntimeAiPlannerValidationReport RuntimeAiPlannerProfileReport { get; set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> RuntimeAiPlannerProfileBindings { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime RuntimeAiBehaviorLoadouts { get; set; }
        public IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> MovableNaturalObjectSpawnSpecs { get; set; }
        public IReadOnlyList<CharacterTestCharacterSpawnSpec> StartupCharacterSpawnSpecs { get; set; }
        public CharacterTestNetworkPeerControlMode PeerControlMode { get; set; } =
            CharacterTestNetworkPeerControlMode.SingleCharacterPlayer;
        public bool SuppressMissingStartupSpawnWarning { get; set; }
        public int PresentationAllyTeamId { get; set; }
        public CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration RuntimeAiPlannerSessionAuthority { get; set; }
        public CharacterTestCharacterActionCatalog ActionCatalog { get; set; }
        internal Action IncomingOwnershipAccepted { get; set; }
    }

    internal static class CharacterTestNetworkSessionFactory
    {
        private const string PlayableCharacterCatalogIdentityVersion =
            "mx.character-test.playable-character-catalog.v1";

        public static ICharacterTestNetworkRunner Create(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            using var startupProfile = CharacterTestStartupProfiler.CreateNetworkSession.Auto();
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ICharacterTestNetworkRunner runner = null;
            try
            {
            switch (options.NetworkMode)
            {
                case CharacterTestNetworkMode.Loopback:
                    runner = CreateObjectLoopbackRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportLoopback:
                    runner = CreateUnityTransportLoopbackRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportServer:
                    runner = CreateUnityTransportServerRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportHost:
                    runner = CreateUnityTransportHostRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportClient:
                    runner = CreateUnityTransportClientRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportRoomServer:
                    runner = CreateUnityTransportRoomServerRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportRoomHost:
                    runner = CreateUnityTransportRoomHostRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                case CharacterTestNetworkMode.UnityTransportRoomClient:
                    runner = CreateUnityTransportRoomClientRunner(
                        options,
                        logger,
                        storyContent,
                        resources,
                        baseResourcePreloadGroupId,
                        cameraMode,
                        physicsRuntime);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported CharacterTest network mode: " + options.NetworkMode);
            }
            CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(
                runner,
                options.RuntimeAiPlannerSessionAuthority);
            return runner;
            }
            catch (Exception failure)
            {
                var errors = new List<Exception>();
                if (runner != null) CharacterTestCleanupScope.Attempt(errors, "UnpublishedNetworkRunner", runner.Dispose);
                if (errors.Count != 0)
                {
                    errors.Insert(0, failure);
                    throw new AggregateException("CharacterTest network factory and rollback failed.", errors);
                }
                throw;
            }
        }

        private static CharacterTestUnityTransportHostRunner CreateUnityTransportHostRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var serverTransport = new UnityTransportNetworkTransport(transportOptions);
            var localClientTransport = new UnityTransportNetworkTransport(transportOptions);
            bool handedOffTransports = false;
            try
            {
                string bindAddress = string.IsNullOrEmpty(options.UnityTransportBindAddress)
                    ? "0.0.0.0"
                    : options.UnityTransportBindAddress;
                if (!serverTransport.StartServer(options.UnityTransportPort, bindAddress))
                    throw CreateTransportStartException(
                        serverTransport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport host server failed to start");

                string serverPeerId = GetServerPeerId(options);
                string localConnectAddress = ResolveLocalClientAddress(bindAddress);
                if (!localClientTransport.StartClient(localConnectAddress, options.UnityTransportPort, serverPeerId))
                    throw CreateTransportStartException(
                        localClientTransport,
                        serverPeerId,
                        "CharacterTest Unity Transport host local client failed to start");

                handedOffTransports = true;
                return new CharacterTestUnityTransportHostRunner(
                    serverTransport,
                    localClientTransport,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    config: CreateNetworkSessionConfig(options),
                    sessionId: CharacterTestUnityTransportSessionIds.DirectLan,
                    serverPeerId: serverPeerId,
                    localClientPeerId: GetClientPeerId(options),
                    transportMode: "unity-transport-host",
                    connectTargetLabel: localConnectAddress + ":" + options.UnityTransportPort,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                if (!handedOffTransports)
                {
                    localClientTransport.Dispose();
                    serverTransport.Dispose();
                }
                throw;
            }
        }

        private static CharacterTestNetworkSessionRunner CreateObjectLoopbackRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            LoopbackNetworkTransport loopback = new LoopbackNetworkTransport(CreateLoopbackTransportOptions(options));
            var startup = new CharacterTestCleanupScope();
            CharacterTestCleanupScope.Entry transportOwnership = startup.Own("LoopbackTransport", loopback.Clear);
            try
            {
            return new CharacterTestNetworkSessionRunner(
                loopback,
                loopback,
                useExpandedReplayContract: true,
                logger: logger,
                storyContent: storyContent,
                serverResources: resources,
                baseResourcePreloadGroupId: baseResourcePreloadGroupId,
                initialCameraMode: cameraMode,
                physicsRuntime: physicsRuntime,
                config: CreateNetworkSessionConfig(options),
                sessionId: "character-test-loopback",
                transportMode: "loopback",
                createServerViews: true,
                audioService: options.AudioService,
                audioCueCatalog: options.AudioCueCatalog,
                disposeAudioService: options.DisposeAudioService,
                runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                runtimeAiPlannerProfileBindings: options.RuntimeAiPlannerProfileBindings,
                runtimeAiBehaviorLoadouts: options.RuntimeAiBehaviorLoadouts,
                movableNaturalObjectSpawnSpecs: options.MovableNaturalObjectSpawnSpecs,
                startupCharacterSpawnSpecs: options.StartupCharacterSpawnSpecs,
                suppressMissingStartupSpawnWarning: options.SuppressMissingStartupSpawnWarning,
                presentationAllyTeamId: options.PresentationAllyTeamId,
                peerControlMode: options.PeerControlMode,
                actionCatalog: options.ActionCatalog,
                incomingOwnershipAccepted: () => { transportOwnership.Transfer(); options.IncomingOwnershipAccepted?.Invoke(); });
            }
            catch (Exception failure)
            {
                RethrowWithFactoryRollback(startup, failure);
                throw;
            }
        }

        private static CharacterTestNetworkSessionRunner CreateUnityTransportLoopbackRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var startup = new CharacterTestCleanupScope();
            try
            {
            var serverTransport = new UnityTransportNetworkTransport(transportOptions);
            CharacterTestCleanupScope.Entry serverOwnership = startup.Own("ServerTransport", serverTransport.Dispose);
            var clientTransport = new UnityTransportNetworkTransport(transportOptions);
            CharacterTestCleanupScope.Entry clientOwnership = startup.Own("ClientTransport", clientTransport.Dispose);
                string bindAddress = string.IsNullOrEmpty(options.UnityTransportBindAddress)
                    ? "127.0.0.1"
                    : options.UnityTransportBindAddress;
                if (!serverTransport.StartServer(options.UnityTransportPort, bindAddress))
                    throw CreateTransportStartException(
                        serverTransport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport server failed to start");

                string remoteAddress = string.IsNullOrEmpty(options.UnityTransportRemoteAddress)
                    ? "127.0.0.1"
                    : options.UnityTransportRemoteAddress;
                if (!clientTransport.StartClient(remoteAddress, options.UnityTransportPort, GetServerPeerId(options)))
                    throw CreateTransportStartException(
                        clientTransport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport client failed to start");

                WaitForUnityTransportLoopbackConnection(
                    serverTransport,
                    clientTransport,
                    GetServerPeerId(options),
                    GetClientPeerId(options));

                return new CharacterTestNetworkSessionRunner(
                    serverTransport,
                    clientTransport,
                    useExpandedReplayContract: true,
                    logger: logger,
                    storyContent: storyContent,
                    serverResources: resources,
                    baseResourcePreloadGroupId: baseResourcePreloadGroupId,
                    initialCameraMode: cameraMode,
                    physicsRuntime: physicsRuntime,
                    config: CreateNetworkSessionConfig(options),
                    sessionId: "character-test-utp-loopback",
                    serverPeerId: GetServerPeerId(options),
                    clientPeerId: GetClientPeerId(options),
                    transportMode: "unity-transport-loopback",
                    createServerViews: true,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    runtimeAiPlannerProfileBindings: options.RuntimeAiPlannerProfileBindings,
                    runtimeAiBehaviorLoadouts: options.RuntimeAiBehaviorLoadouts,
                    movableNaturalObjectSpawnSpecs: options.MovableNaturalObjectSpawnSpecs,
                    startupCharacterSpawnSpecs: options.StartupCharacterSpawnSpecs,
                    suppressMissingStartupSpawnWarning: options.SuppressMissingStartupSpawnWarning,
                    presentationAllyTeamId: options.PresentationAllyTeamId,
                    peerControlMode: options.PeerControlMode,
                    actionCatalog: options.ActionCatalog,
                    incomingOwnershipAccepted: () =>
                    {
                        serverOwnership.Transfer(); clientOwnership.Transfer();
                        options.IncomingOwnershipAccepted?.Invoke();
                    });
            }
            catch (Exception failure)
            {
                RethrowWithFactoryRollback(startup, failure);
                throw;
            }
        }

        private static void RethrowWithFactoryRollback(CharacterTestCleanupScope startup, Exception failure)
        {
            var errors = new List<Exception>();
            startup.Cleanup(errors);
            if (errors.Count != 0)
            {
                errors.Insert(0, failure);
                throw new AggregateException("CharacterTest transport factory and rollback failed.", errors);
            }
        }

        private static CharacterTestUnityTransportServerRunner CreateUnityTransportServerRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var transport = new UnityTransportNetworkTransport(transportOptions);
            try
            {
                string bindAddress = string.IsNullOrEmpty(options.UnityTransportBindAddress)
                    ? "0.0.0.0"
                    : options.UnityTransportBindAddress;
                if (!transport.StartServer(options.UnityTransportPort, bindAddress))
                    throw CreateTransportStartException(
                        transport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport remote server failed to start");

                return new CharacterTestUnityTransportServerRunner(
                    transport,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    config: CreateNetworkSessionConfig(options),
                    sessionId: CharacterTestUnityTransportSessionIds.DirectLan,
                    serverPeerId: GetServerPeerId(options),
                    transportMode: "unity-transport-server",
                    createServerViews: true,
                    enableLocalInputCommands: false,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        private static CharacterTestUnityTransportClientRunner CreateUnityTransportClientRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var transport = new UnityTransportNetworkTransport(transportOptions);
            try
            {
                string remoteAddress = string.IsNullOrEmpty(options.UnityTransportRemoteAddress)
                    ? "127.0.0.1"
                    : options.UnityTransportRemoteAddress;
                string serverPeerId = GetServerPeerId(options);
                if (!transport.StartClient(remoteAddress, options.UnityTransportPort, serverPeerId))
                    throw CreateTransportStartException(
                        transport,
                        serverPeerId,
                        "CharacterTest Unity Transport remote client failed to start");

                return new CharacterTestUnityTransportClientRunner(
                    transport,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    config: CreateNetworkSessionConfig(options),
                    sessionId: CharacterTestUnityTransportSessionIds.DirectLan,
                    serverPeerId: serverPeerId,
                    clientPeerId: GetClientPeerId(options),
                    transportMode: "unity-transport-client",
                    createClientViews: true,
                    connectTargetLabel: remoteAddress + ":" + options.UnityTransportPort,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        private static CharacterTestUnityTransportRoomServerRunner CreateUnityTransportRoomServerRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var transport = new UnityTransportNetworkTransport(transportOptions);
            try
            {
                string bindAddress = string.IsNullOrEmpty(options.UnityTransportBindAddress)
                    ? "0.0.0.0"
                    : options.UnityTransportBindAddress;
                if (!transport.StartServer(options.UnityTransportPort, bindAddress))
                    throw CreateTransportStartException(
                        transport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport room server failed to start");

                return new CharacterTestUnityTransportRoomServerRunner(
                    transport,
                    CreateNetworkRoomConfig(options),
                    options.NetworkRoomServerAutoStartWhenReady,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    serverPeerId: GetServerPeerId(options),
                    transportMode: "unity-transport-room-server",
                    createServerViews: true,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        private static CharacterTestUnityTransportRoomHostRunner CreateUnityTransportRoomHostRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var serverTransport = new UnityTransportNetworkTransport(transportOptions);
            var localClientTransport = new UnityTransportNetworkTransport(transportOptions);
            bool handedOffTransports = false;
            try
            {
                string bindAddress = string.IsNullOrEmpty(options.UnityTransportBindAddress)
                    ? "0.0.0.0"
                    : options.UnityTransportBindAddress;
                if (!serverTransport.StartServer(options.UnityTransportPort, bindAddress))
                    throw CreateTransportStartException(
                        serverTransport,
                        GetServerPeerId(options),
                        "CharacterTest Unity Transport room host server failed to start");

                string serverPeerId = GetServerPeerId(options);
                string localConnectAddress = ResolveLocalClientAddress(bindAddress);
                if (!localClientTransport.StartClient(localConnectAddress, options.UnityTransportPort, serverPeerId))
                    throw CreateTransportStartException(
                        localClientTransport,
                        serverPeerId,
                        "CharacterTest Unity Transport room host local client failed to start");

                handedOffTransports = true;
                return new CharacterTestUnityTransportRoomHostRunner(
                    serverTransport,
                    localClientTransport,
                    CreateNetworkRoomConfig(options),
                    options.NetworkRoomClientDisplayName,
                    options.NetworkRoomClientAutoReady,
                    options.NetworkRoomServerAutoStartWhenReady,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    serverPeerId: serverPeerId,
                    localClientPeerId: GetClientPeerId(options),
                    transportMode: "unity-transport-room-host",
                    connectTargetLabel: localConnectAddress + ":" + options.UnityTransportPort,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                if (!handedOffTransports)
                {
                    localClientTransport.Dispose();
                    serverTransport.Dispose();
                }
                throw;
            }
        }

        private static CharacterTestUnityTransportRoomClientRunner CreateUnityTransportRoomClientRunner(
            CharacterTestNetworkSessionFactoryOptions options,
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode cameraMode,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UnityTransportNetworkOptions transportOptions = CreateUnityTransportOptions(options);
            var transport = new UnityTransportNetworkTransport(transportOptions);
            try
            {
                string remoteAddress = string.IsNullOrEmpty(options.UnityTransportRemoteAddress)
                    ? "127.0.0.1"
                    : options.UnityTransportRemoteAddress;
                string serverPeerId = GetServerPeerId(options);
                if (!transport.StartClient(remoteAddress, options.UnityTransportPort, serverPeerId))
                    throw CreateTransportStartException(
                        transport,
                        serverPeerId,
                        "CharacterTest Unity Transport room client failed to start");

                return new CharacterTestUnityTransportRoomClientRunner(
                    transport,
                    CreateNetworkRoomConfig(options),
                    options.NetworkRoomClientDisplayName,
                    options.NetworkRoomClientAutoReady,
                    logger,
                    storyContent,
                    resources,
                    baseResourcePreloadGroupId,
                    cameraMode,
                    physicsRuntime: physicsRuntime,
                    serverPeerId: serverPeerId,
                    clientPeerId: GetClientPeerId(options),
                    transportMode: "unity-transport-room-client",
                    createClientViews: true,
                    connectTargetLabel: remoteAddress + ":" + options.UnityTransportPort,
                    audioService: options.AudioService,
                    audioCueCatalog: options.AudioCueCatalog,
                    disposeAudioService: options.DisposeAudioService,
                    runtimeAiPlannerProfile: options.RuntimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: options.RuntimeAiPlannerProfileReport,
                    actionCatalog: options.ActionCatalog);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        private static InvalidOperationException CreateTransportStartException(
            INetworkTransport transport,
            string peerId,
            string message)
        {
            string reason = ReadTransportStartFailure(transport, peerId);
            return new InvalidOperationException(
                string.IsNullOrWhiteSpace(reason)
                    ? message + "."
                    : message + ": " + reason);
        }

        private static string ReadTransportStartFailure(INetworkTransport transport, string peerId)
        {
            if (transport == null)
                return string.Empty;

            var events = new List<NetworkTransportEvent>();
            transport.PollEvents(peerId ?? string.Empty, RuntimeFrame.Zero, events);
            for (int i = events.Count - 1; i >= 0; i--)
            {
                NetworkTransportEvent evt = events[i];
                if (evt.Kind == NetworkTransportEventKind.Error
                    && !string.IsNullOrWhiteSpace(evt.Message))
                {
                    return evt.Message.Trim();
                }
            }

            return string.Empty;
        }

        internal static NetworkSessionConfig CreateNetworkSessionConfig(
            CharacterTestNetworkSessionFactoryOptions options)
        {
            return CharacterTestSessionHashContract.Normalize(new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: Mathf.Max(1, options.TickRate),
                inputDelayFrames: Mathf.Max(0, options.InputDelayFrames),
                snapshotIntervalFrames: Mathf.Max(1, options.SnapshotIntervalFrames),
                configHash: string.Empty,
                resourceCatalogHash: ComputeCurrentPlayableCharacterResourceCatalogHash(),
                stateSyncPolicy: options.StateSyncPolicy,
                heartbeatIntervalFrames: Mathf.Max(1, options.HeartbeatIntervalFrames)));
        }

        /// <remarks>LowFreqAlloc: shared session/replay check against current production packages.</remarks>
        internal static string ComputeCurrentPlayableCharacterResourceCatalogHash()
        {
            return ComputePlayableCharacterResourceCatalogHash(
                CharacterTestPlayableCharacterCatalog.Discover(),
                CharacterTestGeneratedPackageSource.LoadPackage);
        }

        /// <remarks>LowFreqAlloc: session bootstrap authority identity.</remarks>
        internal static string ComputePlayableCharacterResourceCatalogHash(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            Func<string, CharacterImportedPackage> packageLoader)
        {
            return ComputePlayableCharacterResourceCatalogHash(
                definitions,
                packageLoader,
                CharacterTestAuthoredContentIds.Revision,
                CharacterTestAuthoredContentIds.CatalogHash);
        }

        internal static string ComputePlayableCharacterResourceCatalogHash(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            Func<string, CharacterImportedPackage> packageLoader,
            long authoredDefinitionCatalogRevision,
            string authoredDefinitionCatalogHash)
        {
            if (definitions == null)
                throw new InvalidOperationException("Playable character catalog is missing.");
            if (packageLoader == null)
                throw new ArgumentNullException(nameof(packageLoader));
            if (authoredDefinitionCatalogRevision <= 0 || string.IsNullOrWhiteSpace(authoredDefinitionCatalogHash))
                throw new ArgumentException("Authored definition identity is required.");

            var ordered = new CharacterTestCharacterDefinition[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
                ordered[i] = definitions[i];
            Array.Sort(ordered, ComparePlayableCharacterAuthorityDefinitions);

            var canonical = new StringBuilder(256 + ordered.Length * 512);
            AppendAuthorityField(canonical, "version", PlayableCharacterCatalogIdentityVersion);
            AppendAuthorityField(
                canonical,
                "authoredDefinitionCatalogRevision",
                authoredDefinitionCatalogRevision.ToString(CultureInfo.InvariantCulture));
            AppendAuthorityField(canonical, "authoredDefinitionCatalogHash", authoredDefinitionCatalogHash);
            AppendAuthorityField(canonical, "count", ordered.Length.ToString(CultureInfo.InvariantCulture));
            int previousCharacterId = 0;
            string previousRoot = string.Empty;
            for (int i = 0; i < ordered.Length; i++)
            {
                CharacterTestCharacterDefinition definition = ordered[i];
                string root = NormalizeGeneratedPackageRoot(definition.GeneratedPackageRoot);
                if (definition.CharacterId <= 0 || string.IsNullOrEmpty(root))
                {
                    throw new InvalidOperationException(
                        "Playable character package authority identity is incomplete. characterId="
                        + definition.CharacterId.ToString(CultureInfo.InvariantCulture));
                }
                if (i > 0 && (definition.CharacterId == previousCharacterId
                    || string.Equals(root, previousRoot, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "Playable character package authority identities are not unique.");
                }

                CharacterImportedPackage package = packageLoader(root);
                ValidatePlayableCharacterAuthorityPackage(definition.CharacterId, root, package);
                CharacterUnityImportRuntimeReport report = package.ImportReport;
                CharacterImportedGeometryBinding geometry = package.Geometry;
                bool authoredPresence =
                    geometry.BodyPresenceMode == CharacterBodyPresenceMode.Authored;
                bool authoredBodyPartDamage =
                    geometry.BodyPartDamageMode == CharacterBodyPartDamageMode.Authored;

                AppendAuthorityField(canonical, "characterId", definition.CharacterId.ToString(CultureInfo.InvariantCulture));
                AppendAuthorityField(canonical, "generatedPackageRoot", root);
                AppendAuthorityField(canonical, "sourcePackageHash", report.SourcePackageHash);
                AppendAuthorityField(canonical, "geometryBindingHash", report.GeometryBindingHash);
                AppendAuthorityField(canonical, "geometryFormat", geometry.Format);
                AppendAuthorityField(canonical, "geometrySchemaVersion", geometry.SchemaVersion);
                AppendAuthorityField(
                    canonical,
                    "bodyPresenceMode",
                    authoredPresence ? "Authored" : "CompatibilityHardRoot");
                AppendAuthorityField(
                    canonical,
                    "bodyPresenceRevision",
                    authoredPresence
                        ? geometry.BodyPresenceRevision.ToString(CultureInfo.InvariantCulture)
                        : "0");
                AppendAuthorityField(
                    canonical,
                    "bodyPresenceContentHash",
                    authoredPresence
                        ? geometry.BodyPresenceContentHash
                        : "compatibility-hard-root");
                AppendAuthorityField(
                    canonical,
                    "bodyPartDamageMode",
                    authoredBodyPartDamage ? "Authored" : "Missing");
                AppendAuthorityField(
                    canonical,
                    "bodyPartDamageFormat",
                    authoredBodyPartDamage
                        ? geometry.BodyPartDamageFormat
                        : "missing");
                AppendAuthorityField(
                    canonical,
                    "bodyPartDamageSchemaVersion",
                    authoredBodyPartDamage
                        ? geometry.BodyPartDamageSchemaVersion
                        : "missing");
                AppendAuthorityField(
                    canonical,
                    "bodyPartDamageRevision",
                    authoredBodyPartDamage
                        ? geometry.BodyPartDamageRevision.ToString(CultureInfo.InvariantCulture)
                        : "0");
                AppendAuthorityField(
                    canonical,
                    "bodyPartDamageContentHash",
                    authoredBodyPartDamage
                        ? geometry.BodyPartDamageContentHash
                        : "missing");
                CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig =
                    geometry.AuthorityPoseRig ?? CharacterAuthorityPoseRigRuntimeBinding.Missing;
                AppendAuthorityField(
                    canonical,
                    "authorityPoseRigFormat",
                    authorityPoseRig.Format);
                AppendAuthorityField(
                    canonical,
                    "authorityPoseRigSchemaVersion",
                    authorityPoseRig.SchemaVersion);
                AppendAuthorityField(
                    canonical,
                    "authorityPoseRigRevision",
                    authorityPoseRig.Revision.ToString(CultureInfo.InvariantCulture));
                AppendAuthorityField(
                    canonical,
                    "authorityPoseRigContentHash",
                    authorityPoseRig.ContentHash);
                previousCharacterId = definition.CharacterId;
                previousRoot = root;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
                digest = sha256.ComputeHash(bytes);
            return PlayableCharacterCatalogIdentityVersion + ":sha256:" + ToLowerHex(digest);
        }

        private static void ValidatePlayableCharacterAuthorityPackage(
            int characterId,
            string packageRoot,
            CharacterImportedPackage package)
        {
            if (package == null)
                throw InvalidPlayableCharacterAuthorityPackage(characterId, packageRoot, "package load failed");
            CharacterUnityImportRuntimeReport report = package.ImportReport;
            CharacterImportedGeometryBinding geometry = package.Geometry;
            if (report == null || geometry == null)
                throw InvalidPlayableCharacterAuthorityPackage(characterId, packageRoot, "report or geometry is missing");
            if (!IsSha256(report.SourcePackageHash) || !IsSha256(report.GeometryBindingHash))
                throw InvalidPlayableCharacterAuthorityPackage(characterId, packageRoot, "import identity hash is invalid");

            bool isV8 = IsGeometryVersion(geometry, "mx.characterGeometryBinding.v8", "8.0");
            if (!isV8)
                throw InvalidPlayableCharacterAuthorityPackage(characterId, packageRoot, "geometry format/schema is invalid");

            bool authoredBodyPresence = geometry.BodyPresenceMode == CharacterBodyPresenceMode.Authored;
            if (authoredBodyPresence)
            {
                if (geometry.BodyPresenceMode != CharacterBodyPresenceMode.Authored
                    || geometry.BodyPresenceRevision <= 0
                    || !IsSha256(geometry.BodyPresenceContentHash))
                {
                    throw InvalidPlayableCharacterAuthorityPackage(
                        characterId,
                        packageRoot,
                        "authored Body Presence identity is invalid");
                }
            }
            else if (geometry.BodyPresenceMode != CharacterBodyPresenceMode.CompatibilityHardRoot
                || geometry.BodyPresenceRevision != 0
                || !string.IsNullOrEmpty(geometry.BodyPresenceContentHash)
                || (geometry.BodyPresenceVolumes != null && geometry.BodyPresenceVolumes.Length != 0))
            {
                throw InvalidPlayableCharacterAuthorityPackage(
                    characterId,
                    packageRoot,
                    "v8 compatibility Body Presence identity is invalid");
            }

            bool authoredBodyPartDamage = geometry.BodyPartDamageMode == CharacterBodyPartDamageMode.Authored;
            if (authoredBodyPartDamage)
            {
                if (geometry.BodyPartDamageMode != CharacterBodyPartDamageMode.Authored
                    || !string.Equals(
                        geometry.BodyPartDamageFormat,
                        "mx.characterBodyPartDamage.v3",
                        StringComparison.Ordinal)
                    || !string.Equals(
                        geometry.BodyPartDamageSchemaVersion,
                        "3.0",
                        StringComparison.Ordinal)
                    || geometry.BodyPartDamageRevision <= 0
                    || !IsSha256(geometry.BodyPartDamageContentHash))
                {
                    throw InvalidPlayableCharacterAuthorityPackage(
                        characterId,
                        packageRoot,
                        "v8 authored Body-Part Damage identity is invalid");
                }
            }
            else if (geometry.BodyPartDamageMode != CharacterBodyPartDamageMode.Missing
                || !string.IsNullOrEmpty(geometry.BodyPartDamageFormat)
                || !string.IsNullOrEmpty(geometry.BodyPartDamageSchemaVersion)
                || geometry.BodyPartDamageRevision != 0
                || !string.IsNullOrEmpty(geometry.BodyPartDamageContentHash)
                || (geometry.BodyPartDamageDefinitions != null
                    && geometry.BodyPartDamageDefinitions.Length != 0)
                || (geometry.BodyPartDamagePartLookup != null
                    && geometry.BodyPartDamagePartLookup.Length != 0))
            {
                throw InvalidPlayableCharacterAuthorityPackage(
                    characterId,
                    packageRoot,
                    "v8 Body-Part Damage identity is invalid");
            }

            CharacterAuthorityPoseRigRuntimeBinding rig = geometry.AuthorityPoseRig;
            if (rig != null && rig.IsAuthored
                && (rig.Revision <= 0 || !IsSha256(rig.ContentHash)))
            {
                throw InvalidPlayableCharacterAuthorityPackage(
                    characterId,
                    packageRoot,
                    "v8 authored Authority Pose Rig identity is invalid");
            }
        }

        private static InvalidOperationException InvalidPlayableCharacterAuthorityPackage(
            int characterId,
            string packageRoot,
            string reason)
        {
            return new InvalidOperationException(
                "Playable character package authority identity rejected. characterId="
                + characterId.ToString(CultureInfo.InvariantCulture)
                + " packageRoot="
                + packageRoot
                + " reason="
                + reason);
        }

        private static int ComparePlayableCharacterAuthorityDefinitions(
            CharacterTestCharacterDefinition left,
            CharacterTestCharacterDefinition right)
        {
            int idComparison = left.CharacterId.CompareTo(right.CharacterId);
            return idComparison != 0
                ? idComparison
                : string.Compare(
                    NormalizeGeneratedPackageRoot(left.GeneratedPackageRoot),
                    NormalizeGeneratedPackageRoot(right.GeneratedPackageRoot),
                    StringComparison.Ordinal);
        }

        private static string NormalizeGeneratedPackageRoot(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsGeometryVersion(
            CharacterImportedGeometryBinding geometry,
            string format,
            string schemaVersion)
        {
            return string.Equals(geometry.Format, format, StringComparison.Ordinal)
                && string.Equals(geometry.SchemaVersion, schemaVersion, StringComparison.Ordinal);
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length != 71
                || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }
            for (int i = 7; i < value.Length; i++)
            {
                char character = value[i];
                bool isHex = (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
                if (!isHex)
                    return false;
            }
            return true;
        }

        private static void AppendAuthorityField(StringBuilder builder, string name, string value)
        {
            string normalized = value ?? string.Empty;
            builder.Append(name)
                .Append('=')
                .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(normalized)
                .Append('\n');
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                characters[i * 2] = digits[bytes[i] >> 4];
                characters[i * 2 + 1] = digits[bytes[i] & 0x0f];
            }
            return new string(characters);
        }

        private static NetworkRoomConfig CreateNetworkRoomConfig(CharacterTestNetworkSessionFactoryOptions options)
        {
            int maxMembers = Mathf.Max(1, options.NetworkRoomMaxMembers);
            int minReadyMembers = Mathf.Clamp(options.NetworkRoomMinReadyMembers, 1, maxMembers);
            return new NetworkRoomConfig(
                string.IsNullOrWhiteSpace(options.NetworkRoomId) ? "character-test-room" : options.NetworkRoomId,
                options.NetworkRoomName,
                maxMembers,
                minReadyMembers,
                requireAllReady: true,
                allowLateJoin: false,
                gameplaySessionConfig: CreateNetworkSessionConfig(options));
        }

        private static LoopbackNetworkTransportOptions CreateLoopbackTransportOptions(
            CharacterTestNetworkSessionFactoryOptions options)
        {
            return new LoopbackNetworkTransportOptions(
                Mathf.Max(0, options.LoopbackLatencyFrames),
                Mathf.Max(0, options.LoopbackJitterFrames),
                Mathf.Clamp01(options.LoopbackDropRate),
                options.LoopbackReorderMessages);
        }

        private static UnityTransportNetworkOptions CreateUnityTransportOptions(
            CharacterTestNetworkSessionFactoryOptions options)
        {
            return new UnityTransportNetworkOptions(
                maxConnections: Mathf.Max(1, options.UnityTransportMaxConnections),
                maxPayloadBytes: Mathf.Max(256, options.UnityTransportMaxPayloadBytes),
                reliableWindowSize: Mathf.Clamp(options.UnityTransportReliableWindowSize, 1, 64),
                receiveQueueCapacity: ResolveUnityTransportQueueCapacity(options.UnityTransportReceiveQueueCapacity),
                sendQueueCapacity: ResolveUnityTransportQueueCapacity(options.UnityTransportSendQueueCapacity),
                serverPeerId: GetServerPeerId(options));
        }

        private static int ResolveUnityTransportQueueCapacity(int capacity)
        {
            return capacity > 0
                ? capacity
                : UnityTransportNetworkOptions.Default.ReceiveQueueCapacity;
        }

        private static string GetServerPeerId(CharacterTestNetworkSessionFactoryOptions options)
        {
            return string.IsNullOrEmpty(options.UnityTransportServerPeerId)
                ? "server"
                : options.UnityTransportServerPeerId;
        }

        private static string GetClientPeerId(CharacterTestNetworkSessionFactoryOptions options)
        {
            return string.IsNullOrEmpty(options.UnityTransportClientPeerId)
                ? "client-0"
                : options.UnityTransportClientPeerId;
        }

        private static string ResolveLocalClientAddress(string bindAddress)
        {
            if (string.IsNullOrWhiteSpace(bindAddress))
                return "127.0.0.1";

            string address = bindAddress.Trim();
            if (string.Equals(address, "0.0.0.0", StringComparison.Ordinal)
                || string.Equals(address, "::", StringComparison.Ordinal)
                || string.Equals(address, "*", StringComparison.Ordinal))
            {
                return "127.0.0.1";
            }

            return address;
        }

        private static void WaitForUnityTransportLoopbackConnection(
            UnityTransportNetworkTransport serverTransport,
            UnityTransportNetworkTransport clientTransport,
            string serverPeerId,
            string clientPeerId)
        {
            var events = new List<NetworkTransportEvent>();
            for (int i = 0; i < 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                events.Clear();
                serverTransport.PollEvents(serverPeerId, frame, events);
                clientTransport.PollEvents(clientPeerId, frame, events);
                for (int j = 0; j < events.Count; j++)
                {
                    NetworkTransportEvent evt = events[j];
                    if (evt.Kind == NetworkTransportEventKind.Connected
                        && string.Equals(evt.PeerId, serverPeerId, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException("CharacterTest Unity Transport loopback client did not connect to server.");
        }
    }
}
