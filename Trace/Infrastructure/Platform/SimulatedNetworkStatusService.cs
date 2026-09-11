using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Trace.Services;

namespace Trace.Infrastructure.Platform;

/// <summary>
/// Connectivity the technician can also assert manually.
/// </summary>
/// <remarks>
/// TRACE treats "offline" as a working mode, not only as a detected condition: a technician
/// walking into a mechanical room declares it before losing signal. This implementation
/// carries only the manual switch — real link detection would layer underneath it without
/// changing anything above this interface.
/// </remarks>
public sealed class SimulatedNetworkStatusService : INetworkStatusService
{
    private readonly List<Channel<bool>> _watchers = [];

    public bool IsOnline { get; private set; } = true;

    public async IAsyncEnumerable<bool> Watch([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_watchers)
        {
            _watchers.Add(channel);
        }

        try
        {
            yield return IsOnline;

            await foreach (var state in channel.Reader.ReadAllAsync(ct))
            {
                yield return state;
            }
        }
        finally
        {
            lock (_watchers)
            {
                _watchers.Remove(channel);
            }
        }
    }

    public void SetOffline(bool offline)
    {
        if (IsOnline == !offline)
        {
            return;
        }

        IsOnline = !offline;

        Channel<bool>[] watchers;
        lock (_watchers)
        {
            watchers = [.. _watchers];
        }

        foreach (var watcher in watchers)
        {
            watcher.Writer.TryWrite(IsOnline);
        }
    }
}
