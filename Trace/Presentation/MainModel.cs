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
    private readonly FeedRecovery _recovery;

    public MainModel(ISyncQueue sync, INetworkStatusService network, FeedRecovery recovery)
    {
        _sync = sync;
        _network = network;
        _recovery = recovery;
    }

    public IFeed<int> PendingCount => _recovery.Watch(_sync.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);
}
