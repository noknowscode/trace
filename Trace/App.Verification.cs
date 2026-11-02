#if DEBUG
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Trace.Presentation.Controls;

namespace Trace;

/// <summary>
/// Hooks for the screenshot matrix (<c>tools/matrix.sh</c>). Debug builds only: Release never
/// reads these arguments.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>--trace-theme=light|dark</c> presses the theme toggle on the round when the app is not
/// already in that theme, so the toggle's label and the saved preference agree with the screen.</item>
/// <item><c>--trace-route=Today|Assets|Scan|Sync|Findings</c> opens a destination the way its tab
/// does; <c>Asset[:Overview|Inspect|History|Documents]</c> opens P-204 the way a card on the round
/// does; <c>Point</c> also opens its seal housing point and enters an out-of-range reading;
/// <c>Action</c> fails that point with a note and raises the corrective action sheet.</item>
/// <item><c>--trace-seed=empty</c> clears the round, the corrective actions and the queue after the
/// store loads, for the empty states.</item>
/// <item><c>--trace-focus=log</c> prints every element keyboard focus lands on.</item>
/// <item><c>--trace-fault=load</c> makes every store read fail for the first 20 seconds, for the
/// error states and their Retry.</item>
/// </list>
/// Each step prints a <c>VERIFY</c> line to stderr so a run can be checked without the picture.
/// </remarks>
public partial class App
{
    private static string? VerificationArg(string name) => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith($"--trace-{name}=", StringComparison.OrdinalIgnoreCase))?
        .Split('=', 2)[1];

    /// <summary>Wraps the file store so reads fail for a while after launch, when asked to.</summary>
    private static ILocalFileStore WithVerificationFaults(ILocalFileStore store) =>
        string.Equals(VerificationArg("fault"), "load", StringComparison.OrdinalIgnoreCase)
            ? new FaultingFileStore(store, TimeSpan.FromSeconds(20))
            : store;

    private async Task ApplyVerificationAsync()
    {
        try
        {
            if (Host is null || MainWindow?.Content is not FrameworkElement root)
            {
                Console.Error.WriteLine($"VERIFY failed: no host or window content ({MainWindow?.Content?.GetType().Name})");
                return;
            }

            if (VerificationArg("focus") is "log")
            {
                // Names every element keyboard focus lands on, to check the tab order.
                Microsoft.UI.Xaml.Input.FocusManager.GotFocus += (_, e) => Console.Error.WriteLine(
                    $"VERIFY focus={e.NewFocusedElement?.GetType().Name} name={(e.NewFocusedElement is DependencyObject d ? AutomationProperties.GetName(d) : string.Empty)}");
            }

            if (VerificationArg("seed") is "empty")
            {
                await Host.Services.GetRequiredService<ITraceStore>().MutateAsync(
                    s => s with
                    {
                        Assignments = ImmutableList<Assignment>.Empty,
                        Actions = ImmutableList<CorrectiveAction>.Empty,
                        Queue = ImmutableList<SyncOperation>.Empty,
                    },
                    CancellationToken.None);
                Console.Error.WriteLine("VERIFY seed=empty");
            }

            // Everything below goes through the round, exactly as a technician would.
            TodayPage? today = null;
            for (var i = 0; i < 100 && today is null; i++)
            {
                await Task.Delay(100);
                today = Find<TodayPage>(root);
            }

            if (today is null)
            {
                Console.Error.WriteLine("VERIFY failed: the round never loaded");
                return;
            }

            if (VerificationArg("theme") is { } theme)
            {
                // The round's own toggle, so its label and the saved preference agree with the screen.
                var dark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase);
                await Task.Delay(500);
                if ((today.ActualTheme == ElementTheme.Dark) != dark
                    && today.FindName("ThemeToggle") is Button { Command: { } toggle })
                {
                    toggle.Execute(null);
                    await Task.Delay(500);
                }

                Console.Error.WriteLine($"VERIFY theme={(dark ? "Dark" : "Light")} actual={today.ActualTheme}");
            }

            if (VerificationArg("route") is not { } spec)
            {
                return;
            }

            if (today.Navigator() is not { } navigator)
            {
                Console.Error.WriteLine($"VERIFY route={spec} failed: no navigator");
                return;
            }

            var parts = spec.Split(':', 2);
            var route = parts[0];
            if (route is "Today" or "Assets" or "Scan" or "Sync" or "Findings")
            {
                var tab = await navigator.NavigateRouteAsync(today, $"../{route}");
                Console.Error.WriteLine($"VERIFY route={spec} success={tab?.Success}");
                return;
            }

            AssetSummary? pump = null;
            await foreach (var summaries in Host.Services.GetRequiredService<IAssetRepository>().WatchSummaries(CancellationToken.None))
            {
                pump = summaries.FirstOrDefault(s => s.AssetId == "P-204");
                break;
            }

            var response = await navigator.NavigateRouteAsync(today, "../Asset", data: pump);
            Console.Error.WriteLine($"VERIFY route={spec} asset={pump?.AssetId} success={response?.Success}");

            await Task.Delay(1500);
            var page = Find<AssetPage>(root);
            if (page is null)
            {
                Console.Error.WriteLine("VERIFY asset page not found");
                return;
            }

            if (parts.Length == 2 && Enum.TryParse<AssetTab>(parts[1], out var assetTab))
            {
                var bar = All<Uno.Toolkit.UI.TabBar>(page).FirstOrDefault();
                if (bar is not null)
                {
                    bar.SelectedIndex = (int)assetTab;
                }

                Console.Error.WriteLine($"VERIFY tab={assetTab} bar={bar is not null}");
                return;
            }

            if (route is not ("Point" or "Action"))
            {
                return;
            }

            // The point strip under the machine: the same button a technician would press.
            var chip = All<Button>(page).FirstOrDefault(b =>
                b.DataContext is PointMarkerView marker && marker.Point.Label.Contains("Seal", StringComparison.OrdinalIgnoreCase)
                && b is not InspectionPointMarker);
            chip?.Command?.Execute(chip.CommandParameter);
            await Task.Delay(1500);

            SetText(page, "New reading", "7.4");
            await Task.Delay(800);
            Console.Error.WriteLine($"VERIFY point opened={chip is not null}");

            if (route is not "Action")
            {
                return;
            }

            SetText(page, "Inspection notes", "Seal weeping at the gland, vibration up on the drive end.");
            Invoke(page, "Record fail");
            await Task.Delay(800);
            Invoke(page, "Complete point");
            await Task.Delay(1500);
            Invoke(page, "Create corrective action");
            await Task.Delay(800);
            Console.Error.WriteLine("VERIFY action sheet requested");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"VERIFY failed: {ex}");
        }
    }

    private static void SetText(DependencyObject root, string automationName, string text)
    {
        if (All<TextBox>(root).FirstOrDefault(t => AutomationProperties.GetName(t) == automationName) is { } box)
        {
            box.Text = text;
        }
    }

    private static void Invoke(DependencyObject root, string automationName)
    {
        if (All<Button>(root).FirstOrDefault(b => AutomationProperties.GetName(b) == automationName) is { Command: { } command } button)
        {
            command.Execute(button.CommandParameter);
        }
    }

    private static T? Find<T>(DependencyObject root)
        where T : DependencyObject => All<T>(root).FirstOrDefault();

    private static IEnumerable<T> All<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in All<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Fails every read until the window has passed, then defers to the real store.</summary>
    private sealed class FaultingFileStore(ILocalFileStore inner, TimeSpan window) : ILocalFileStore
    {
        private readonly DateTimeOffset _until = DateTimeOffset.UtcNow + window;

        public ValueTask<string?> ReadTextAsync(string key, CancellationToken ct) =>
            DateTimeOffset.UtcNow < _until
                ? throw new IOException($"Simulated read failure for {key}.")
                : inner.ReadTextAsync(key, ct);

        public ValueTask WriteTextAsync(string key, string content, CancellationToken ct) =>
            inner.WriteTextAsync(key, content, ct);

        public ValueTask<string> WriteBytesAsync(string key, byte[] content, CancellationToken ct) =>
            inner.WriteBytesAsync(key, content, ct);

        public ValueTask DeleteAsync(string key, CancellationToken ct) => inner.DeleteAsync(key, ct);
    }
}
#endif
