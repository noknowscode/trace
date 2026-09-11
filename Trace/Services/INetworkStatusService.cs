namespace Trace.Services;

/// <summary>
/// Connectivity as the app sees it. Field work routinely happens in basements and
/// mechanical rooms, so this is also manually switchable: the technician can declare
/// offline before walking into a dead zone.
/// </summary>
public interface INetworkStatusService
{
    bool IsOnline { get; }

    IAsyncEnumerable<bool> Watch(CancellationToken ct);

    void SetOffline(bool offline);
}
