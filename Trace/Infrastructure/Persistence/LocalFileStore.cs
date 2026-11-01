using System.IO;
using Trace.Services;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Writes app state under a root folder supplied at composition time. The root is passed in
/// rather than resolved here so the same store can be pointed at a temp directory in tests.
/// </summary>
public sealed class LocalFileStore : ILocalFileStore
{
    private readonly Func<ValueTask<string>> _openRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _root;

    public LocalFileStore(string root)
        : this(() => ValueTask.FromResult(root))
    {
    }

    /// <summary>
    /// A store whose root is opened on first use rather than at construction. The app opens
    /// it through StorageFolder: on WebAssembly the local folder is restored from browser
    /// storage asynchronously and only a StorageFolder call waits for that, so a plain
    /// System.IO read at startup could find the folder still empty.
    /// </summary>
    public LocalFileStore(Func<ValueTask<string>> openRoot) => _openRoot = openRoot;

    public async ValueTask<string?> ReadTextAsync(string key, CancellationToken ct)
    {
        var path = await PathForAsync(key);
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
        var path = await PathForAsync(key);
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
        var path = await PathForAsync(key);
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

    public async ValueTask DeleteAsync(string key, CancellationToken ct)
    {
        var path = await PathForAsync(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async ValueTask<string> PathForAsync(string key)
    {
        _root ??= await _openRoot();
        var full = Path.Combine(_root, key);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return full;
    }
}
