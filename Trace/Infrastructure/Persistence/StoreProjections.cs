using System.Runtime.CompilerServices;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Turns the store's snapshot stream into the narrow streams each screen actually binds to.
/// Projections drop unchanged results, so a sync-queue write does not rebuild the Today
/// list and a reading change does not re-render the findings view.
/// </summary>
internal static class StoreProjections
{
    public static async IAsyncEnumerable<T> Project<TIn, T>(
        IAsyncEnumerable<TIn> source,
        Func<TIn, T> selector,
        IEqualityComparer<T> comparer,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var hasPrevious = false;
        T previous = default!;

        await foreach (var input in source.WithCancellation(ct))
        {
            var value = selector(input);

            if (hasPrevious && comparer.Equals(previous, value))
            {
                continue;
            }

            previous = value;
            hasPrevious = true;
            yield return value;
        }
    }

    public static IAsyncEnumerable<T> Project<TIn, T>(
        IAsyncEnumerable<TIn> source,
        Func<TIn, T> selector,
        CancellationToken ct) =>
        Project(source, selector, EqualityComparer<T>.Default, ct);

    public static IAsyncEnumerable<IImmutableList<T>> ProjectList<TIn, T>(
        IAsyncEnumerable<TIn> source,
        Func<TIn, IImmutableList<T>> selector,
        CancellationToken ct) =>
        Project(source, selector, SequenceComparer<T>.Instance, ct);

    /// <summary>
    /// Compares lists by their contents. Records give structural equality per item, so an
    /// identical rebuild of a list compares equal and is not pushed to the view.
    /// </summary>
    private sealed class SequenceComparer<T> : IEqualityComparer<IImmutableList<T>>
    {
        public static SequenceComparer<T> Instance { get; } = new();

        public bool Equals(IImmutableList<T>? x, IImmutableList<T>? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.Count != y.Count)
            {
                return false;
            }

            return x.SequenceEqual(y);
        }

        public int GetHashCode(IImmutableList<T> obj) => obj.Count;
    }
}
