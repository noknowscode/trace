using System.Runtime.CompilerServices;

namespace Trace.Presentation;

/// <summary>
/// Lets a screen that failed to read the device's records try again.
/// </summary>
/// <remarks>
/// Every list and record in TRACE is a live watch over the local store, exposed through
/// <c>Feed.AsyncEnumerable</c>. When such a watch fails (the store could not be read), MVUX
/// shows the error template and the watch is over: an async-enumerable feed does not restart
/// on a FeedView refresh, so a Retry bound to it does nothing. Feeds built here instead report
/// the failure, wait for <see cref="Retry"/>, and subscribe to their source again.
///
/// There is one store behind every screen, so there is one retry for the whole app: pressing
/// Try again anywhere restarts every watch that failed, including the shell's pending-sync badge,
/// which has no error template of its own.
/// </remarks>
public sealed class FeedRecovery
{
    private TaskCompletionSource _retry = NewSignal();

    /// <summary>Restarts every watch that is currently showing an error.</summary>
    public void Retry() => Interlocked.Exchange(ref _retry, NewSignal()).TrySetResult();

    /// <summary>A feed over a live watch, restarted by <see cref="Retry"/> after a failure.</summary>
    public IFeed<T> Watch<T>(Func<CancellationToken, IAsyncEnumerable<T>> source) =>
        Feed<T>.Create(ct => Messages(source, ct));

    /// <summary>A list feed over a live watch, restarted by <see cref="Retry"/> after a failure.</summary>
    public IListFeed<T> WatchList<T>(Func<CancellationToken, IAsyncEnumerable<IImmutableList<T>>> source) =>
        Watch(source).AsListFeed();

    /// <summary>
    /// The messages a recoverable feed publishes: data while the source yields, an error when it
    /// throws, then progress and fresh data once <see cref="Retry"/> is called.
    /// </summary>
    public async IAsyncEnumerable<Message<T>> Messages<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> source,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var current = Message<T>.Initial;

        while (!ct.IsCancellationRequested)
        {
            // Taken before subscribing, so a Retry pressed while this attempt is failing is
            // not missed.
            var retry = Volatile.Read(ref _retry).Task;
            Exception? failure = null;

            var values = source(ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    bool hasValue;
                    try
                    {
                        hasValue = await values.MoveNextAsync();
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        failure = ex;
                        break;
                    }

                    if (!hasValue)
                    {
                        yield break;
                    }

                    // A null value is "nothing there" (an asset that no longer resolves), the
                    // same as Feed.AsyncEnumerable reports it: the None state, not an error.
                    current = current.With().Data(Option.SomeOrNone(values.Current)).Error(null).IsTransient(false);
                    yield return current;
                }
            }
            finally
            {
                await values.DisposeAsync();
            }

            current = current.With().Error(failure).IsTransient(false);
            yield return current;

            await retry.WaitAsync(ct);

            // Reading again: the view shows progress until the source answers.
            current = current.With().IsTransient(true);
            yield return current;
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
