using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Repositories;

public sealed class InspectionRepository : IInspectionRepository
{
    private readonly ITraceStore _store;

    public InspectionRepository(ITraceStore store) => _store = store;

    public IAsyncEnumerable<IImmutableList<InspectionEvent>> WatchHistory(string assetId, CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), s => Newest(s.Events, e => e.AssetId == assetId), ct);

    private static IImmutableList<InspectionEvent> Newest(
        IImmutableList<InspectionEvent> events,
        Func<InspectionEvent, bool> predicate) =>
        events.Where(predicate)
            .OrderByDescending(e => e.Timestamp)
            .ToImmutableList();
}
