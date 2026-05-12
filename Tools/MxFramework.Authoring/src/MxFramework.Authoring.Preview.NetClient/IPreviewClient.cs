using System.Threading;
using System.Threading.Tasks;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Preview;

public interface IPreviewClient
{
    bool IsHandshaked { get; }

    Task<HandshakeResult> HandshakeAsync(string clientName, string clientVersion, CancellationToken cancellationToken = default);

    Task<LoadPatchResult> LoadPatchAsync(LoadPatchParams parameters, CancellationToken cancellationToken = default);

    Task<RuntimePreviewResult> ApplyBuffAsync(ApplyBuffParams parameters, CancellationToken cancellationToken = default);

    Task<ResetResult> ResetAsync(ResetParams parameters, CancellationToken cancellationToken = default);

    Task<RuntimePreviewResult> GetSnapshotAsync(GetSnapshotParams parameters, CancellationToken cancellationToken = default);

    Task<GetLogsResult> GetLogsAsync(GetLogsParams parameters, CancellationToken cancellationToken = default);
}
