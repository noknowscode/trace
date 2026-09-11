using Trace.Domain.Models;
using Trace.Infrastructure.Persistence;
using Trace.Services;

namespace Trace.Infrastructure.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly ITraceStore _store;

    public AssignmentRepository(ITraceStore store) => _store = store;

    public IAsyncEnumerable<IImmutableList<AssetSummary>> WatchToday(CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), SummaryProjector.Today, ct);
}
