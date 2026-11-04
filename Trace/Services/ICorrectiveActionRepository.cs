namespace Trace.Services;

public interface ICorrectiveActionRepository
{
    /// <summary>Every action on the device: open work first, worst first, then the closed ones.</summary>
    IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchAll(CancellationToken ct);

    /// <summary>The asset's open and dispatched actions, worst first.</summary>
    IAsyncEnumerable<IImmutableList<CorrectiveAction>> WatchOpenForAsset(string assetId, CancellationToken ct);

    ValueTask<IImmutableList<string>> GetAssigneesAsync(CancellationToken ct);
}
