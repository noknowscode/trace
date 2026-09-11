using System.IO;
using Trace.Services;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Writes app state under a root folder supplied at composition time. The root is passed in
/// rather than resolved here so the same store can be pointed at a temp directory in tests.
/// </summary>
public sealed class LocalFileStore : ILocalFileStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalFileStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public async ValueTask<string?> ReadTextAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        await _gate.WaitAsync(ct);
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask WriteTextAsync(string key, string content, CancellationToken ct)
    {
        var path = PathFor(key);
        await _gate.WaitAsync(ct);
        try
        {
            // Write to a sibling temp file and move into place, so a process kill mid-write
            // cannot leave a half-serialized document behind.
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, content, ct);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<string> WriteBytesAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = PathFor(key);
        await _gate.WaitAsync(ct);
        try
        {
            await File.WriteAllBytesAsync(path, content, ct);
        }
        finally
        {
            _gate.Release();
        }

        return new Uri(path).AbsoluteUri;
    }

    public ValueTask DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }

    private string PathFor(string key)
    {
        var full = Path.Combine(_root, key);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return full;
    }
}
