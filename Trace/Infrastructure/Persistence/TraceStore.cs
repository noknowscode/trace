using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Trace.Infrastructure.Seed;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Loads state from disk once, holds it in memory, and persists only the sections a
/// mutation actually touched. Section changes are detected by reference: an immutable list
/// that a transform left alone is still the same object, so it is not rewritten.
/// </summary>
/// <remarks>
/// A mutation that touches several sections is committed through a journal
/// (<see cref="StoreCommit"/>), so it lands whole or not at all. A section that cannot be read
/// at launch is set aside under a new name rather than overwritten, and the snapshot says so.
/// </remarks>
public sealed class TraceStore : ITraceStore
{
    private const string AssetsKey = "assets.json";
    private const string AssignmentsKey = "assignments.json";
    private const string EventsKey = "events.json";
    private const string ActionsKey = "actions.json";
    private const string QueueKey = "sync-queue.json";
    private const string DraftsKey = "drafts.json";
    private const string CommitKey = "commit.json";

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
            try
            {
                await PersistAsync(StoreCommit.Between(previous, next), ct);
            }
            catch
            {
                // Disk may now hold part of this commit (a journal, some sections). The next
                // read loads from disk again, which finishes the commit from its journal,
                // instead of writing the next one on top of half of this one.
                _current = null;
                throw;
            }

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
        var setAside = new List<string>();

        // A commit interrupted after its journal landed is finished first: the journal holds
        // the newer copy of every section it touched, so those sections are taken from it.
        var pending = await ReadSectionAsync(CommitKey, TraceJsonContext.Default.StoreCommit, setAside, ct);

        // Every section is read before anything is decided. Reading the catalogue first and
        // returning early on a bad read would lose the technician's outbox: an unreadable
        // assets.json looks exactly like a first run.
        var assets = await ReadAsync(AssetsKey, pending.Value?.Assets, TraceJsonContext.Default.ImmutableListAsset, setAside, ct);
        var assignments = await ReadAsync(AssignmentsKey, pending.Value?.Assignments, TraceJsonContext.Default.ImmutableListAssignment, setAside, ct);
        var events = await ReadAsync(EventsKey, pending.Value?.Events, TraceJsonContext.Default.ImmutableListInspectionEvent, setAside, ct);
        var actions = await ReadAsync(ActionsKey, pending.Value?.Actions, TraceJsonContext.Default.ImmutableListCorrectiveAction, setAside, ct);
        var queue = await ReadAsync(QueueKey, pending.Value?.Queue, TraceJsonContext.Default.ImmutableListSyncOperation, setAside, ct);
        var drafts = await ReadAsync(DraftsKey, pending.Value?.Drafts, TraceJsonContext.Default.ImmutableListInspectionDraft, setAside, ct);

        if (assets.Value is { Count: > 0 })
        {
            if (pending.Value is { } commit)
            {
                _log.LogWarning("Finishing an interrupted commit of {Sections} sections.", commit.SectionCount);
                await PersistAsync(commit, ct);
            }

            return new TraceSnapshot(
                assets.Value,
                assignments.Value ?? ImmutableList<Assignment>.Empty,
                events.Value ?? ImmutableList<InspectionEvent>.Empty,
                actions.Value ?? ImmutableList<CorrectiveAction>.Empty,
                queue.Value ?? ImmutableList<SyncOperation>.Empty,
                drafts.Value ?? ImmutableList<InspectionDraft>.Empty)
            {
                SetAside = setAside.ToImmutableList(),
            };
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

        // Writes every section through a fresh journal, which also retires a pending one.
        await PersistAsync(StoreCommit.Between(TraceSnapshot.Empty, seed), ct);
        return seed with { SetAside = setAside.ToImmutableList() };
    }

    /// <summary>
    /// A section read. <c>Unreadable</c> separates "this file is damaged" from "this file is
    /// not there yet", which are the same value but very different situations: the first is a
    /// recovery, the second is a first run.
    /// </summary>
    private readonly record struct Section<T>(T? Value, bool Unreadable)
        where T : class;

    private async Task<Section<ImmutableList<T>>> ReadAsync<T>(
        string key,
        ImmutableList<T>? committed,
        JsonTypeInfo<ImmutableList<T>> typeInfo,
        List<string> setAside,
        CancellationToken ct) =>
        committed is not null
            ? new Section<ImmutableList<T>>(committed, Unreadable: false)
            : await ReadSectionAsync(key, typeInfo, setAside, ct);

    private async Task<Section<T>> ReadSectionAsync<T>(
        string key,
        JsonTypeInfo<T> typeInfo,
        List<string> setAside,
        CancellationToken ct)
        where T : class
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
            // A corrupt document must not brick the app in the field, and must not be
            // overwritten either: it may hold work that never reached the server. It is moved
            // aside for support to recover, and the caller carries on with every section it
            // could read.
            var aside = $"{key}.unreadable-{_time.GetUtcNow():yyyyMMdd-HHmmss}";
            await _files.MoveAsync(key, aside, ct);
            setAside.Add(aside);

            _log.LogError(ex, "Could not read {Key}; set it aside as {Aside} and continued without it.", key, aside);
            return new Section<T>(null, Unreadable: true);
        }
    }

    private async Task PersistAsync(StoreCommit commit, CancellationToken ct)
    {
        // One section is one file, and a file write is atomic on its own.
        var journaled = commit.SectionCount > 1;
        if (journaled)
        {
            await _files.WriteTextAsync(CommitKey, JsonSerializer.Serialize(commit, TraceJsonContext.Default.StoreCommit), ct);
        }

        await WriteAsync(AssetsKey, commit.Assets, TraceJsonContext.Default.ImmutableListAsset, ct);
        await WriteAsync(AssignmentsKey, commit.Assignments, TraceJsonContext.Default.ImmutableListAssignment, ct);
        await WriteAsync(EventsKey, commit.Events, TraceJsonContext.Default.ImmutableListInspectionEvent, ct);
        await WriteAsync(ActionsKey, commit.Actions, TraceJsonContext.Default.ImmutableListCorrectiveAction, ct);
        await WriteAsync(QueueKey, commit.Queue, TraceJsonContext.Default.ImmutableListSyncOperation, ct);
        await WriteAsync(DraftsKey, commit.Drafts, TraceJsonContext.Default.ImmutableListInspectionDraft, ct);

        if (journaled)
        {
            await _files.DeleteAsync(CommitKey, ct);
        }
    }

    private async Task WriteAsync<T>(
        string key,
        ImmutableList<T>? section,
        JsonTypeInfo<ImmutableList<T>> typeInfo,
        CancellationToken ct)
    {
        if (section is not null)
        {
            await _files.WriteTextAsync(key, JsonSerializer.Serialize(section, typeInfo), ct);
        }
    }
}
