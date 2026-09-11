using Trace.Domain.Enums;
using Trace.Domain.Models;
using Trace.Services;

namespace Trace.Infrastructure.Platform;

/// <summary>
/// Writes evidence into app-local storage and hands back a record that references it by
/// URI. The bytes stay on the device; only the small record travels through the sync queue.
/// </summary>
public sealed class LocalEvidenceStore : ILocalEvidenceStore
{
    private readonly ILocalFileStore _files;

    public LocalEvidenceStore(ILocalFileStore files) => _files = files;

    public async ValueTask<Evidence> SavePhotoAsync(
        string pointId,
        byte[] content,
        string extension,
        string caption,
        CancellationToken ct)
    {
        var id = $"ev-{Guid.NewGuid():N}";
        var uri = await _files.WriteBytesAsync($"evidence/{pointId}/{id}{extension}", content, ct);

        return new Evidence(id, EvidenceType.Photo, uri, caption, DateTimeOffset.Now);
    }

    public Evidence CreateNote(string caption) =>
        new($"ev-{Guid.NewGuid():N}", EvidenceType.Note, string.Empty, caption, DateTimeOffset.Now);
}
