namespace Trace.Services;

/// <summary>
/// App-local durable file access. Behind an interface because the file system is a platform
/// capability: the app talks to this, never to a platform path directly.
/// </summary>
public interface ILocalFileStore
{
    ValueTask<string?> ReadTextAsync(string key, CancellationToken ct);

    ValueTask WriteTextAsync(string key, string content, CancellationToken ct);

    /// <summary>Writes binary content and returns a URI that can be resolved by an Image source.</summary>
    ValueTask<string> WriteBytesAsync(string key, byte[] content, CancellationToken ct);

    ValueTask DeleteAsync(string key, CancellationToken ct);

    /// <summary>Renames a file, replacing whatever <paramref name="newKey"/> held. Nothing happens when there is no file.</summary>
    ValueTask MoveAsync(string key, string newKey, CancellationToken ct);
}
