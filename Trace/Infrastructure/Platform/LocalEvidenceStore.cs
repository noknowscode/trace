namespace Trace.Infrastructure.Platform;

/// <summary>
/// Writes evidence into app-local storage and hands back a record that references it by
/// URI. The bytes stay on the device; only the small record travels through the sync queue.
/// </summary>
public sealed class LocalEvidenceStore : ILocalEvidenceStore
{
    private readonly ILocalFileStore _files;
    private readonly TimeProvider _time;

    public LocalEvidenceStore(ILocalFileStore files, TimeProvider time)
    {
        _files = files;
        _time = time;
    }

    public async ValueTask<Evidence> SavePhotoAsync(
        string pointId,
        byte[] content,
        string extension,
        string caption,
        CancellationToken ct)
    {
        var id = $"ev-{Guid.NewGuid():N}";
        var uri = await _files.WriteBytesAsync($"evidence/{pointId}/{id}{extension}", content, ct);

        return new Evidence(id, EvidenceType.Photo, uri, caption, _time.GetLocalNow());
    }
}
