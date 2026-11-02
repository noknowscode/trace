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
    private readonly string? _appDataUri;
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
    /// <param name="openRoot">Opens the root folder.</param>
    /// <param name="appDataUri">
    /// The root as an <c>ms-appdata:///</c> URI, for the URIs <see cref="WriteBytesAsync"/>
    /// hands back. A file:// URI holds the absolute path, which a browser cannot load and
    /// which iOS invalidates when an update moves the app's container; an app-data URI names
    /// the file relative to the app and resolves on every head. Without one (tests over a
    /// temp folder), file URIs are returned.
    /// </param>
    public LocalFileStore(Func<ValueTask<string>> openRoot, string? appDataUri = null)
    {
        _openRoot = openRoot;
        _appDataUri = appDataUri;
    }

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

        return _appDataUri is null ? new Uri(path).AbsoluteUri : _appDataUri + key;
    }

    public async ValueTask DeleteAsync(string key, CancellationToken ct)
    {
        var path = await PathForAsync(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async ValueTask MoveAsync(string key, string newKey, CancellationToken ct)
    {
        var from = await PathForAsync(key);
        var to = await PathForAsync(newKey);
        await _gate.WaitAsync(ct);
        try
        {
            if (File.Exists(from))
            {
                File.Move(from, to, overwrite: true);
            }
        }
        finally
        {
            _gate.Release();
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
