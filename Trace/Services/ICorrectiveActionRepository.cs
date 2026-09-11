namespace Trace.Services;

public interface ICorrectiveActionRepository
{
    IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchAll(CancellationToken ct);

    IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchForAsset(string assetId, CancellationToken ct);

    ValueTask<IImmutableList<string>> GetAssigneesAsync(CancellationToken ct);
}
