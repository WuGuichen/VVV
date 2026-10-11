using System;
using System.Collections.Generic;
using MxFramework.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        void SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request);
    }

    internal static class CharacterTestRuntimeAiPlannerSessionAuthorityWire
    {
        public const string RequestTypeId =
            "mx.character-test.runtime-ai-planner.session-deck-selection";
        public const int SchemaVersion = 2;
        public const int MaximumPayloadCharacters = 4096;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Error
        };

        /// <remarks>LowFreqAlloc: pre-session typed network request only.</remarks>
        public static NetworkApplicationControlRequestPacket CreateRequestPacket(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var document = new RequestDocument
            {
                CharacterStableId = request.CharacterStableId,
                CharacterNumericId = request.CharacterNumericId,
                ExpectedCatalogId = request.ExpectedCatalogId,
                ExpectedCatalogRevision = request.ExpectedCatalogRevision,
                ExpectedCatalogNumericId = request.ExpectedCatalogNumericId,
                ExpectedCatalogNumericIdentityHash = request.ExpectedCatalogNumericIdentityHash,
                ExpectedCatalogHash = request.ExpectedCatalogHash,
                CardNumericIds = CopyNumericIds(request.CardNumericIds),
                StableCardLabels = CopyIds(request.StableCardLabels)
            };
            return new NetworkApplicationControlRequestPacket(
                RequestTypeId,
                SchemaVersion,
                requestId,
                JsonConvert.SerializeObject(document, JsonSettings));
        }

        /// <remarks>LowFreqAlloc: authoritative pre-session request decode only.</remarks>
        public static bool TryDecodeRequest(
            string senderPeerId,
            NetworkApplicationControlRequestPacket packet,
            out CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            out string failure)
        {
            request = null;
            failure = string.Empty;
            if (!ValidatePacketIdentity(packet, out failure))
                return false;
            string normalizedPeerId = senderPeerId?.Trim() ?? string.Empty;
            if (normalizedPeerId.Length == 0)
            {
                failure = "Session deck request sender peer id is missing.";
                return false;
            }
            if (packet.PayloadJson.Length > MaximumPayloadCharacters)
            {
                failure = "Session deck request payload exceeds the fixed size limit.";
                return false;
            }

            RequestDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<RequestDocument>(packet.PayloadJson, JsonSettings);
            }
            catch (Exception exception)
            {
                failure = "Session deck request payload is invalid: " + exception.Message;
                return false;
            }
            if (document == null || document.CardNumericIds == null || document.StableCardLabels == null)
            {
                failure = "Session deck request payload is incomplete.";
                return false;
            }
            if (document.CardNumericIds.Length > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots
                || document.CardNumericIds.Length != document.StableCardLabels.Length)
            {
                failure = "Session deck request exceeds the fixed slot limit.";
                return false;
            }
            request = new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                normalizedPeerId,
                document.CharacterStableId,
                document.CharacterNumericId,
                document.ExpectedCatalogId,
                document.ExpectedCatalogRevision,
                document.ExpectedCatalogNumericId,
                document.ExpectedCatalogNumericIdentityHash,
                document.ExpectedCatalogHash,
                document.CardNumericIds,
                document.StableCardLabels);
            return true;
        }

        /// <remarks>LowFreqAlloc: pre-session typed network response only.</remarks>
        public static NetworkApplicationControlResponsePacket CreateResponsePacket(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            var document = new ResponseDocument
            {
                AuthorityHash = result.Success ? result.SessionLock.AuthorityHash : 0L,
                DeckHash = result.Success ? result.SessionLock.DeckHash : 0L,
                CatalogHash = result.Success ? result.SessionLock.CatalogHash : 0L,
                Diagnostics = CopyDiagnostics(result.Diagnostics)
            };
            string reason = result.Success || result.Diagnostics.Count == 0
                ? string.Empty
                : result.Diagnostics[0].Code + ": " + result.Diagnostics[0].Message;
            return new NetworkApplicationControlResponsePacket(
                RequestTypeId,
                SchemaVersion,
                requestId,
                result.Success,
                reason,
                JsonConvert.SerializeObject(document, JsonSettings));
        }

        public static bool IsRequest(NetworkApplicationControlRequestPacket packet)
        {
            return packet != null
                && packet.SchemaVersion == SchemaVersion
                && string.Equals(packet.TypeId, RequestTypeId, StringComparison.Ordinal);
        }

        private static bool ValidatePacketIdentity(
            NetworkApplicationControlRequestPacket packet,
            out string failure)
        {
            failure = string.Empty;
            if (packet == null)
            {
                failure = "Session deck request packet is missing.";
                return false;
            }
            if (!string.Equals(packet.TypeId, RequestTypeId, StringComparison.Ordinal))
            {
                failure = "Session deck request type id is not supported.";
                return false;
            }
            if (packet.SchemaVersion != SchemaVersion)
            {
                failure = "Session deck request schema is not supported.";
                return false;
            }
            return true;
        }

        private static string[] CopyIds(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        private static int[] CopyNumericIds(IReadOnlyList<int> source)
        {
            var result = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        private static DiagnosticDocument[] CopyDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> source)
        {
            var result = new DiagnosticDocument[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic diagnostic = source[i];
                result[i] = new DiagnosticDocument
                {
                    Severity = (int)diagnostic.Severity,
                    Code = diagnostic.Code,
                    Path = diagnostic.Path,
                    Message = diagnostic.Message
                };
            }
            return result;
        }

        private sealed class RequestDocument
        {
            public string CharacterStableId { get; set; }
            public int CharacterNumericId { get; set; }
            public string ExpectedCatalogId { get; set; }
            public int ExpectedCatalogRevision { get; set; }
            public int ExpectedCatalogNumericId { get; set; }
            public long ExpectedCatalogNumericIdentityHash { get; set; }
            public long ExpectedCatalogHash { get; set; }
            public int[] CardNumericIds { get; set; }
            public string[] StableCardLabels { get; set; }
        }

        private sealed class ResponseDocument
        {
            public long AuthorityHash { get; set; }
            public long DeckHash { get; set; }
            public long CatalogHash { get; set; }
            public DiagnosticDocument[] Diagnostics { get; set; }
        }

        private sealed class DiagnosticDocument
        {
            public int Severity { get; set; }
            public string Code { get; set; }
            public string Path { get; set; }
            public string Message { get; set; }
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler
    {
        private readonly CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _participant;
        private readonly Func<string, CharacterTestRuntimeAiPlannerSessionAuthorityContext> _contextProvider;

        public CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler(
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant,
            Func<string, CharacterTestRuntimeAiPlannerSessionAuthorityContext> contextProvider)
        {
            _participant = participant ?? throw new ArgumentNullException(nameof(participant));
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        }

        /// <remarks>LowFreqAlloc: server-side pre-session request handling only.</remarks>
        public NetworkApplicationControlResponsePacket Handle(
            string senderPeerId,
            NetworkApplicationControlRequestPacket packet)
        {
            if (!CharacterTestRuntimeAiPlannerSessionAuthorityWire.TryDecodeRequest(
                senderPeerId,
                packet,
                out CharacterTestRuntimeAiPlannerSessionDeckRequest request,
                out string failure))
            {
                return Reject(packet, failure);
            }
            CharacterTestRuntimeAiPlannerSessionAuthorityContext context =
                _contextProvider(senderPeerId);
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result =
                _participant.TryLock(request, context);
            return CharacterTestRuntimeAiPlannerSessionAuthorityWire.CreateResponsePacket(
                packet.RequestId,
                result);
        }

        private static NetworkApplicationControlResponsePacket Reject(
            NetworkApplicationControlRequestPacket packet,
            string failure)
        {
            string typeId = packet?.TypeId;
            int schemaVersion = packet?.SchemaVersion ?? CharacterTestRuntimeAiPlannerSessionAuthorityWire.SchemaVersion;
            long requestId = packet?.RequestId ?? 0L;
            if (string.IsNullOrWhiteSpace(typeId))
                typeId = CharacterTestRuntimeAiPlannerSessionAuthorityWire.RequestTypeId;
            if (schemaVersion <= 0)
                schemaVersion = CharacterTestRuntimeAiPlannerSessionAuthorityWire.SchemaVersion;
            return new NetworkApplicationControlResponsePacket(
                typeId,
                schemaVersion,
                requestId,
                accepted: false,
                reason: failure);
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration
    {
        public CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            Func<string, CharacterTestRuntimeAiPlannerSessionAuthorityContext> serverContextProvider,
            Action<NetworkApplicationControlResponsePacket> clientResponseObserver = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (runtimeContractVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(runtimeContractVersion));
            RuntimeContractVersion = runtimeContractVersion;
            ServerContextProvider = serverContextProvider
                ?? throw new ArgumentNullException(nameof(serverContextProvider));
            ClientResponseObserver = clientResponseObserver;
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog { get; }
        public int RuntimeContractVersion { get; }
        public Func<string, CharacterTestRuntimeAiPlannerSessionAuthorityContext> ServerContextProvider { get; }
        public Action<NetworkApplicationControlResponsePacket> ClientResponseObserver { get; }

        public CharacterTestRuntimeAiPlannerSessionAuthorityParticipant CreateParticipant()
        {
            return new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
                Catalog,
                RuntimeContractVersion);
        }
    }

    internal static class CharacterTestRuntimeAiPlannerSessionAuthorityComposition
    {
        /// <remarks>LowFreqAlloc: network session composition-root setup only.</remarks>
        public static void Configure(
            ICharacterTestNetworkRunner runner,
            CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration configuration)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));
            if (configuration == null)
                return;

            if (runner is CharacterTestNetworkSessionRunner combined)
            {
                ConfigureCombined(combined, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportHostRunner host)
            {
                ConfigureServer(host.ServerRunner, configuration);
                ConfigureClient(host.LocalClientRunner, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportRoomHostRunner roomHost)
            {
                ConfigureServer(roomHost.ServerRunner.GameplayRunner, configuration);
                ConfigureClient(roomHost.LocalClientRunner.GameplayRunner, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportRoomServerRunner roomServer)
            {
                ConfigureServer(roomServer.GameplayRunner, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportRoomClientRunner roomClient)
            {
                ConfigureClient(roomClient.GameplayRunner, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportServerRunner server)
            {
                ConfigureServer(server, configuration);
                return;
            }
            if (runner is CharacterTestUnityTransportClientRunner client)
            {
                ConfigureClient(client, configuration);
                return;
            }

            throw new InvalidOperationException(
                "Runtime AI Planner session authority does not support runner type "
                + runner.GetType().FullName
                + ".");
        }

        private static void ConfigureCombined(
            CharacterTestNetworkSessionRunner runner,
            CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration configuration)
        {
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant server =
                configuration.CreateParticipant();
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant client =
                configuration.CreateParticipant();
            runner.ConfigureRuntimeAiPlannerSessionAuthority(
                server,
                client,
                new CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler(
                    server,
                    configuration.ServerContextProvider),
                configuration.ClientResponseObserver);
        }

        private static void ConfigureServer(
            CharacterTestUnityTransportServerRunner runner,
            CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration configuration)
        {
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant =
                configuration.CreateParticipant();
            runner.ConfigureRuntimeAiPlannerSessionAuthority(
                participant,
                new CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler(
                    participant,
                    configuration.ServerContextProvider));
        }

        private static void ConfigureClient(
            CharacterTestUnityTransportClientRunner runner,
            CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration configuration)
        {
            runner.ConfigureRuntimeAiPlannerSessionAuthority(
                configuration.CreateParticipant(),
                configuration.ClientResponseObserver);
        }
    }
}
