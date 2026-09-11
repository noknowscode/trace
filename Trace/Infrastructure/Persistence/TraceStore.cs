using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Trace.Domain.Models;
using Trace.Infrastructure.Seed;
using Trace.Services;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Loads state from disk once, holds it in memory, and persists only the sections a
/// mutation actually touched. Section changes are detected by reference: an immutable list
/// that a transform left alone is still the same object, so it is not rewritten.
/// </summary>
public sealed class TraceStore : ITraceStore
{
    private const string AssetsKey = "assets.json";
    private const string AssignmentsKey = "assignments.json";
    private const string EventsKey = "events.json";
    private const string ActionsKey = "actions.json";
    private const string QueueKey = "sync-queue.json";
    private const string DraftsKey = "drafts.json";

    private readonly ILocalFileStore _files;
    private readonly TimeProvider _time;
    private readonly ILogger<TraceStore> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<Channel<TraceSnapshot>> _watchers = [];

    private TraceSnapshot? _current;

    public TraceStore(ILocalFileStore files, TimeProvider time, ILogger<TraceStore> log)
    {
        _files = files;
        _time = time;
        _log = log;
    }

    public async ValueTask<TraceSnapshot> GetAsync(CancellationToken ct)
    {
        if (_current is { } loaded)
        {
            return loaded;
        }

        await _gate.WaitAsync(ct);
        try
        {
            return _current ??= await LoadAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<TraceSnapshot> Watch([EnumeratorCancellation] CancellationToken ct)
    {
        // Bounded at one with DropOldest: a screen that falls behind should catch up to the
        // latest state, not replay a backlog of intermediate ones.
        var channel = Channel.CreateBounded<TraceSnapshot>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_watchers)
        {
            _watchers.Add(channel);
        }

        try
        {
            yield return await GetAsync(ct);

            await foreach (var snapshot in channel.Reader.ReadAllAsync(ct))
            {
                yield return snapshot;
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

    public async ValueTask<TraceSnapshot> MutateAsync(
        Func<TraceSnapshot, TraceSnapshot> mutate,
        CancellationToken ct)
    {
        TraceSnapshot next;

        await _gate.WaitAsync(ct);
        try
        {
            var previous = _current ??= await LoadAsync(ct);
            next = mutate(previous);

            if (ReferenceEquals(next, previous))
            {
                return previous;
            }

            _current = next;
            await PersistChangedSectionsAsync(previous, next, ct);
        }
        finally
        {
            _gate.Release();
        }

        Channel<TraceSnapshot>[] watchers;
        lock (_watchers)
        {
            watchers = [.. _watchers];
        }

        foreach (var watcher in watchers)
        {
            watcher.Writer.TryWrite(next);
        }

        return next;
    }

    private async Task<TraceSnapshot> LoadAsync(CancellationToken ct)
    {
        var assets = await ReadAsync(AssetsKey, TraceJsonContext.Default.ImmutableListAsset, ct);

        if (assets is null || assets.Count == 0)
        {
            // First run on this device: lay down the seed round so the app is never empty
            // for the wrong reason, then persist it as ordinary state.
            var seed = SeedData.Create(_time.GetLocalNow());
            await PersistChangedSectionsAsync(TraceSnapshot.Empty, seed, ct);
            return seed;
        }

        return new TraceSnapshot(
            assets,
            await ReadAsync(AssignmentsKey, TraceJsonContext.Default.ImmutableListAssignment, ct)
                ?? ImmutableList<Assignment>.Empty,
            await ReadAsync(EventsKey, TraceJsonContext.Default.ImmutableListInspectionEvent, ct)
                ?? ImmutableList<InspectionEvent>.Empty,
            await ReadAsync(ActionsKey, TraceJsonContext.Default.ImmutableListCorrectiveAction, ct)
                ?? ImmutableList<CorrectiveAction>.Empty,
            await ReadAsync(QueueKey, TraceJsonContext.Default.ImmutableListSyncOperation, ct)
                ?? ImmutableList<SyncOperation>.Empty,
            await ReadAsync(DraftsKey, TraceJsonContext.Default.ImmutableListInspectionDraft, ct)
                ?? ImmutableList<InspectionDraft>.Empty);
    }

    private async Task<ImmutableList<T>?> ReadAsync<T>(
        string key,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<ImmutableList<T>> typeInfo,
        CancellationToken ct)
    {
        var json = await _files.ReadTextAsync(key, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException ex)
        {
            // A corrupt document must not brick the app in the field. Drop it and carry on;
            // the sync queue will still hold anything that had not reached the server.
            _log.LogError(ex, "Could not read {Key}; continuing without it.", key);
            return null;
        }
    }

    private async Task PersistChangedSectionsAsync(
        TraceSnapshot previous,
        TraceSnapshot next,
        CancellationToken ct)
    {
        await WriteIfChangedAsync(AssetsKey, previous.Assets, next.Assets,
            TraceJsonContext.Default.ImmutableListAsset, ct);
        await WriteIfChangedAsync(AssignmentsKey, previous.Assignments, next.Assignments,
            TraceJsonContext.Default.ImmutableListAssignment, ct);
        await WriteIfChangedAsync(EventsKey, previous.Events, next.Events,
            TraceJsonContext.Default.ImmutableListInspectionEvent, ct);
        await WriteIfChangedAsync(ActionsKey, previous.Actions, next.Actions,
            TraceJsonContext.Default.ImmutableListCorrectiveAction, ct);
        await WriteIfChangedAsync(QueueKey, previous.Queue, next.Queue,
            TraceJsonContext.Default.ImmutableListSyncOperation, ct);
        await WriteIfChangedAsync(DraftsKey, previous.Drafts, next.Drafts,
            TraceJsonContext.Default.ImmutableListInspectionDraft, ct);
    }

    private async Task WriteIfChangedAsync<T>(
        string key,
        IImmutableList<T> previous,
        IImmutableList<T> next,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<ImmutableList<T>> typeInfo,
        CancellationToken ct)
    {
        if (ReferenceEquals(previous, next))
        {
            return;
        }

        var json = JsonSerializer.Serialize(next.ToImmutableList(), typeInfo);
        await _files.WriteTextAsync(key, json, ct);
    }
}
