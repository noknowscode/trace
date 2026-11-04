using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Repositories;

public sealed class InspectionRepository : IInspectionRepository
{
    private readonly ITraceStore _store;

    public InspectionRepository(ITraceStore store) => _store = store;

    public IAsyncEnumerable<IImmutableList<InspectionEvent>> WatchHistory(string assetId, CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), s => Newest(s.Events, e => e.AssetId == assetId), ct);

    public async ValueTask<IImmutableList<InspectionEvent>> GetHistoryAsync(string assetId, CancellationToken ct) =>
        Newest((await _store.GetAsync(ct)).Events, e => e.AssetId == assetId);

    private static IImmutableList<InspectionEvent> Newest(
        IImmutableList<InspectionEvent> events,
        Func<InspectionEvent, bool> predicate) =>
        events.Where(predicate)
            .OrderByDescending(e => e.Timestamp)
            .ToImmutableList();
}
