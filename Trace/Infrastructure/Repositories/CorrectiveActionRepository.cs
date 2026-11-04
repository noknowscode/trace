using Trace.Infrastructure.Persistence;
using Trace.Infrastructure.Seed;

namespace Trace.Infrastructure.Repositories;

public sealed class CorrectiveActionRepository : ICorrectiveActionRepository
{
    private readonly ITraceStore _store;

    public CorrectiveActionRepository(ITraceStore store) => _store = store;

    public IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchAll(CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), s => Newest(s.Actions, _ => true), ct);

    public IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchOpenForAsset(string assetId, CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), s => Newest(s.Actions, a => a.AssetId == assetId && a.IsOpenWork), ct);

    public ValueTask<IImmutableList<string>> GetAssigneesAsync(CancellationToken ct) =>
        ValueTask.FromResult(SeedData.Assignees);

    private static IImmutableList<CorrectiveAction> Newest(
        IImmutableList<CorrectiveAction> actions,
        Func<CorrectiveAction, bool> predicate) =>
        actions.Where(predicate)
            .OrderBy(a => a.IsOpenWork ? 0 : 1)
            .ThenByDescending(a => a.Priority)
            .ThenByDescending(a => a.CreatedAt)
            .ToImmutableList();
}
