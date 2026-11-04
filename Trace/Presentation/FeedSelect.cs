namespace Trace.Presentation;

/// <summary>Feed operators the models share.</summary>
internal static class FeedSelect
{
    /// <summary>
    /// Projects each value onto something that may not be there. MVUX's own Select already reads
    /// a null result as None (the state a binding shows as nothing), but its signature does not
    /// admit one; this does, and leaves loading and None as Select would.
    /// </summary>
    public static IFeed<TResult> SelectOrNone<TSource, TResult>(this IFeed<TSource> source, Func<TSource, TResult?> selector)
        where TResult : class =>
        source.SelectData(data =>
            data.IsSome(out var value) ? Option.SomeOrNone(selector(value))
            : data.IsNone() ? Option<TResult>.None()
            : Option<TResult>.Undefined());
}
