namespace Trace.Domain.Models;

/// <summary>
/// One queued write waiting for the remote source. The queue is durable, so it survives
/// process termination and the technician can see exactly what has not left the device.
/// </summary>
public partial record SyncOperation(
    string Id,
    string EntityType,
    string EntityId,
    string Operation,
    string Description,
    DateTimeOffset CreatedAt,
    SyncState Status,
    int AttemptCount,
    bool HasAttachments,
    string? Error)
{
    public bool IsRetryable => Status == SyncState.Failed;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
}
