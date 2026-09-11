namespace Trace.Domain.Enums;

/// <summary>
/// Explicit synchronization state. TRACE never infers sync status from connectivity —
/// a record is only <see cref="Synced"/> once the queue confirms the upload.
/// </summary>
public enum SyncState
{
    /// <summary>Committed to local storage, not yet queued.</summary>
    Local = 0,

    /// <summary>Queued and waiting for a sync pass.</summary>
    Pending = 1,

    /// <summary>Confirmed by the remote source.</summary>
    Synced = 2,

    /// <summary>A sync pass rejected it. Retryable.</summary>
    Failed = 3,
}
