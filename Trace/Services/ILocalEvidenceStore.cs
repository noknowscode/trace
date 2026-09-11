namespace Trace.Services;

/// <summary>
/// Keeps evidence files in app-local storage and hands back a URI the record can reference.
/// Files stay on the device until the sync queue confirms their event.
/// </summary>
public interface ILocalEvidenceStore
{
    ValueTask<Evidence> SavePhotoAsync(string pointId, byte[] content, string extension, string caption, CancellationToken ct);

    Evidence CreateNote(string caption);
}
