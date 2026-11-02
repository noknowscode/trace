using System.Runtime.CompilerServices;
using Trace.Presentation;
using Uno.Extensions.Reactive;

namespace Trace.Tests;

/// <summary>
/// Every screen reads the store through a live watch. When the read fails, the screen shows an
/// error with Try again; these pin down that Try again really reads again, which MVUX's own
/// refresh does not do for a watch.
/// </summary>
[TestFixture]
public class FeedRecoveryTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Test]
    public async Task A_watch_that_works_publishes_its_values()
    {
        var recovery = new FeedRecovery();
        var messages = recovery.Messages(_ => Values(1, 2), CancellationToken.None).GetAsyncEnumerator();

        (await Next(messages)).Current.Data.SomeOrDefault().Should().Be(1);
        (await Next(messages)).Current.Data.SomeOrDefault().Should().Be(2);
        (await messages.MoveNextAsync()).Should().BeFalse("the watch ended, so the feed does too");
    }

    [Test]
    public async Task A_failed_read_is_reported_and_read_again_on_retry()
    {
        var recovery = new FeedRecovery();
        var attempts = 0;
        IAsyncEnumerable<int> Source(CancellationToken ct) =>
            ++attempts == 1 ? Failing<int>(new IOException("Store unreadable")) : Values(7);

        var messages = recovery.Messages(Source, CancellationToken.None).GetAsyncEnumerator();

        var failed = await Next(messages);
        failed.Current.Error.Should().BeOfType<IOException>();

        // Nothing more until someone presses Try again.
        var waiting = messages.MoveNextAsync().AsTask();
        (await Task.WhenAny(waiting, Task.Delay(200))).Should().NotBe(waiting);

        recovery.Retry();
        (await waiting.WaitAsync(Patience)).Should().BeTrue();
        messages.Current.Current.IsTransient.Should().BeTrue("the view shows progress while it reads again");

        var recovered = await Next(messages);
        recovered.Current.Error.Should().BeNull();
        recovered.Current.Data.SomeOrDefault().Should().Be(7);
        attempts.Should().Be(2);
    }

    [Test]
    public async Task A_null_value_is_nothing_there_not_an_error()
    {
        var recovery = new FeedRecovery();
        var messages = recovery.Messages(_ => Values(new string?[] { null }), CancellationToken.None).GetAsyncEnumerator();

        var message = await Next(messages);
        message.Current.Data.IsNone().Should().BeTrue();
        message.Current.Error.Should().BeNull();
    }

    private static async Task<Message<T>> Next<T>(IAsyncEnumerator<Message<T>> messages)
    {
        (await messages.MoveNextAsync().AsTask().WaitAsync(Patience)).Should().BeTrue();
        return messages.Current;
    }

    private static async IAsyncEnumerable<T> Values<T>(params T[] values)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }

    private static async IAsyncEnumerable<T> Failing<T>(Exception error, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        throw error;
#pragma warning disable CS0162 // An iterator needs a yield to be one.
        yield break;
#pragma warning restore CS0162
    }
}
