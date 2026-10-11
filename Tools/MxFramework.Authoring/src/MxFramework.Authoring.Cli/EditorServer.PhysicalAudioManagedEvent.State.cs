using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;


internal static partial class EditorServer
{
    private static bool IsAudioApplicationRunningUnsafe() =>
        !string.IsNullOrEmpty(ActiveAudioApplicationId)
        && AudioApplications.TryGetValue(ActiveAudioApplicationId, out AudioApplicationStatus running)
        && !running.Terminal;

    private static bool IsManagedPhysicalEventOperationActiveUnsafe() =>
        !string.IsNullOrEmpty(ActiveManagedPhysicalEventOperationId)
        && ManagedPhysicalEventOperations.TryGetValue(
            ActiveManagedPhysicalEventOperationId,
            out ManagedPhysicalEventStatus running)
        && !running.terminal;

    private static void UpdateManagedPhysicalEventStatus(
        string rootPath,
        ManagedPhysicalEventStatus status,
        string stage,
        int progress,
        string message,
        JsonSerializerOptions jsonOptions)
    {
        status.stage = stage;
        status.progress = progress;
        status.message = message;
        status.updatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        SaveManagedPhysicalEventStatus(rootPath, status, jsonOptions);
    }

    private static void SaveManagedPhysicalEventStatus(
        string rootPath,
        ManagedPhysicalEventStatus status,
        JsonSerializerOptions jsonOptions) =>
        WriteJsonFileAtomic(
            ResolveAudioBridgePath(
                rootPath,
                ManagedPhysicalEventStatusDirectory + "/" + status.operationId + ".json"),
            status,
            jsonOptions);

    internal sealed class ManagedPhysicalEventApiRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedSemanticHash { get; set; } = string.Empty;
        public string expectedPhysicalHash { get; set; } = string.Empty;
        public string familyId { get; set; } = string.Empty;
        public string materialPairId { get; set; } = string.Empty;
        public string variantKind { get; set; } = string.Empty;
        public string basisId { get; set; } = string.Empty;
        public bool createBasis { get; set; }
        public string templateEventDefinitionId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
    }

    internal sealed class ManagedPhysicalEventStatus
    {
        public string operationId { get; set; } = string.Empty;
        public string basisId { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public int runtimeCueId { get; set; }
        public string eventDefinitionId { get; set; } = string.Empty;
        public int runtimeEventId { get; set; }
        public string eventPath { get; set; } = string.Empty;
        public string eventGuid { get; set; } = string.Empty;
        public bool created { get; set; }
        public string stage { get; set; } = string.Empty;
        public int progress { get; set; }
        public string message { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string detail { get; set; } = string.Empty;
        public string bankHash { get; set; } = string.Empty;
        public string snapshotHash { get; set; } = string.Empty;
        public string semanticManifestHash { get; set; } = string.Empty;
        public string physicalManifestHash { get; set; } = string.Empty;
        public bool terminal { get; set; }
        public int httpStatus { get; set; } = 200;
        public string updatedAtUtc { get; set; } = string.Empty;

        public static ManagedPhysicalEventStatus Failed(
            string basisId,
            string code,
            string message,
            int httpStatus,
            string operationId = null) =>
            new ManagedPhysicalEventStatus
            {
                operationId = operationId ?? Guid.NewGuid().ToString("N"),
                basisId = basisId ?? string.Empty,
                stage = "Failed",
                message = message ?? string.Empty,
                code = code ?? string.Empty,
                terminal = true,
                httpStatus = httpStatus,
                updatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
    }

    internal sealed class ManagedPhysicalEventOperation
    {
        public string SemanticPath { get; set; } = string.Empty;
        public string PhysicalPath { get; set; } = string.Empty;
        public string SnapshotPath { get; set; } = string.Empty;
        public string ExpectedSemanticHash { get; set; } = string.Empty;
        public string ExpectedPhysicalHash { get; set; } = string.Empty;
        public AudioSemanticManifestDocument SemanticManifest { get; set; }
        public PhysicalAudioAuthoringManifestDocument PhysicalManifest { get; set; }
        public AudioCueAuthoringDefinition Cue { get; set; }
        public AudioEventAuthoringDefinition AudioEvent { get; set; }
        public PhysicalAudioBasisSoundAuthoring Basis { get; set; }
        public FmodManagedPhysicalEventRequestV1 FmodRequest { get; set; }
    }

    internal sealed class FmodManagedPhysicalEventRequestV1
    {
        public string Format { get; set; } = "mx.fmodManagedPhysicalEventRequest.v1";
        public string RequestId { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string BasisId { get; set; } = string.Empty;
        public string EventPath { get; set; } = string.Empty;
        public string ExpectedEventGuid { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BankPath { get; set; } = string.Empty;
        public string BusPath { get; set; } = string.Empty;
        public bool Is3D { get; set; } = true;
        public float MinDistance { get; set; } = 1f;
        public float MaxDistance { get; set; } = 20f;
        public string CreatedUtc { get; set; } = string.Empty;
    }

    private sealed class FmodManagedPhysicalEventStatus
    {
        public string requestId { get; set; } = string.Empty;
        public string requestHash { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string eventPath { get; set; } = string.Empty;
        public string eventGuid { get; set; } = string.Empty;
        public string bankPath { get; set; } = string.Empty;
        public string busPath { get; set; } = string.Empty;
        public bool created { get; set; }
        public JsonElement eventBackup { get; set; }
        public bool mutationRolledBack { get; set; }
        public bool buildCompleted { get; set; }
        public bool buildSucceeded { get; set; }
    }

    private sealed class ManagedPhysicalEventException : Exception
    {
        public ManagedPhysicalEventException(string code, string message, int httpStatus)
            : base(message)
        {
            Code = code;
            HttpStatus = httpStatus;
        }

        public string Code { get; }
        public int HttpStatus { get; }

        public static ManagedPhysicalEventException Invalid(string code, string message) =>
            new ManagedPhysicalEventException(code, message, 400);

        public static ManagedPhysicalEventException Conflict(string code, string message, int status) =>
            new ManagedPhysicalEventException(code, message, status);
    }
}
