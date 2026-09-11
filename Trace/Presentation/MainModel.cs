using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// The navigation shell. It owns nothing but the two things every destination needs to see:
/// how much work has not left the device, and whether the device can currently send it.
/// </summary>
public partial record MainModel
{
    private readonly ISyncQueue _sync;
    private readonly INetworkStatusService _network;

    public MainModel(ISyncQueue sync, INetworkStatusService network)
    {
        _sync = sync;
        _network = network;
    }

    public IFeed<int> PendingCount => Feed.AsyncEnumerable(_sync.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);
}
