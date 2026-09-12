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

            // Disk first. Publishing the snapshot before the write means a failed write
            // leaves memory ahead of disk permanently: the section is already committed, so
            // the next mutation compares against it, finds it unchanged, and never retries.
            await PersistChangedSectionsAsync(previous, next, ct);
            _current = next;
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
        // Every section is read before anything is decided. Reading the catalogue first and
        // returning early on a bad read is how the technician's outbox used to disappear:
        // an unreadable assets.json looked exactly like a first run.
        var assets = await ReadAsync(AssetsKey, TraceJsonContext.Default.ImmutableListAsset, ct);
        var assignments = await ReadAsync(AssignmentsKey, TraceJsonContext.Default.ImmutableListAssignment, ct);
        var events = await ReadAsync(EventsKey, TraceJsonContext.Default.ImmutableListInspectionEvent, ct);
        var actions = await ReadAsync(ActionsKey, TraceJsonContext.Default.ImmutableListCorrectiveAction, ct);
        var queue = await ReadAsync(QueueKey, TraceJsonContext.Default.ImmutableListSyncOperation, ct);
        var drafts = await ReadAsync(DraftsKey, TraceJsonContext.Default.ImmutableListInspectionDraft, ct);

        if (assets.Value is { Count: > 0 })
        {
            return new TraceSnapshot(
                assets.Value,
                assignments.Value ?? ImmutableList<Assignment>.Empty,
                events.Value ?? ImmutableList<InspectionEvent>.Empty,
                actions.Value ?? ImmutableList<CorrectiveAction>.Empty,
                queue.Value ?? ImmutableList<SyncOperation>.Empty,
                drafts.Value ?? ImmutableList<InspectionDraft>.Empty);
        }

        // No usable catalogue. The app needs one to be navigable at all, so lay down the
        // seed round — but only the catalogue. Anything the technician already recorded is
        // carried across, the queue above all: those operations still have to reach the
        // server, and a damaged catalogue is no reason to drop them.
        var seed = SeedData.Create(_time.GetLocalNow());
        var recovered = assets.Unreadable || events.Value is { Count: > 0 } || queue.Value is { Count: > 0 };

        if (recovered)
        {
            _log.LogError(
                "The asset catalogue could not be read. Reseeding it and keeping {Events} recorded events, "
                + "{Queue} queued operations and {Drafts} drafts.",
                events.Value?.Count ?? 0,
                queue.Value?.Count ?? 0,
                drafts.Value?.Count ?? 0);

            seed = seed with
            {
                Events = events.Value ?? seed.Events,
                Actions = actions.Value ?? seed.Actions,
                Queue = queue.Value ?? seed.Queue,
                Drafts = drafts.Value ?? seed.Drafts,
            };
        }

        await PersistChangedSectionsAsync(TraceSnapshot.Empty, seed, ct);
        return seed;
    }

    /// <summary>
    /// A section read. <c>Unreadable</c> separates "this file is damaged" from "this file is
    /// not there yet", which are the same value but very different situations: the first is a
    /// recovery, the second is a first run.
    /// </summary>
    private readonly record struct Section<T>(ImmutableList<T>? Value, bool Unreadable);

    private async Task<Section<T>> ReadAsync<T>(
        string key,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<ImmutableList<T>> typeInfo,
        CancellationToken ct)
    {
        var json = await _files.ReadTextAsync(key, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Section<T>(null, Unreadable: false);
        }

        try
        {
            return new Section<T>(JsonSerializer.Deserialize(json, typeInfo), Unreadable: false);
        }
        catch (JsonException ex)
        {
            // A corrupt document must not brick the app in the field. Drop this section and
            // carry on — the caller keeps every section it could read.
            _log.LogError(ex, "Could not read {Key}; continuing without it.", key);
            return new Section<T>(null, Unreadable: true);
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
