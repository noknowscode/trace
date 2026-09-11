using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Trace.Services;

namespace Trace.Infrastructure.Platform;

/// <summary>
/// Switches the theme on the navigation root rather than on the Application, which can only
/// be assigned before the first window content is set.
/// </summary>
public sealed class AppThemeService : IAppThemeService
{
    private readonly List<Channel<bool>> _watchers = [];
    private FrameworkElement? _root;

    public bool IsDark { get; private set; } = true;

    /// <summary>Called once the navigation root exists.</summary>
    public void Attach(FrameworkElement root)
    {
        _root = root;
        Apply();
    }

    public async IAsyncEnumerable<bool> Watch([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_watchers)
        {
            _watchers.Add(channel);
        }

        try
        {
            yield return IsDark;

            await foreach (var value in channel.Reader.ReadAllAsync(ct))
            {
                yield return value;
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

    public void Toggle()
    {
        IsDark = !IsDark;
        Apply();

        Channel<bool>[] watchers;
        lock (_watchers)
        {
            watchers = [.. _watchers];
        }

        foreach (var watcher in watchers)
        {
            watcher.Writer.TryWrite(IsDark);
        }
    }

    private void Apply()
    {
        if (_root is not null)
        {
            _root.RequestedTheme = IsDark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }
}
