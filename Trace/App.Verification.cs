#if DEBUG
using Microsoft.UI.Xaml.Automation;
using Trace.Infrastructure;
using Trace.Infrastructure.Persistence;
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
/// <c>Action</c> fails that point with a note and raises the corrective action sheet;
/// <c>Again</c> completes the seal housing as Pass, enters a new reading and completes it again,
/// and prints how many events the point gained; <c>Discard</c> enters the reading and asks to
/// discard it, for the confirmation; <c>Photo</c> attaches a photo and prints its URI.</item>
/// <item><c>--trace-route=Switch</c> opens four assets in one session, the ways a technician does:
/// AHU-12 from Today, C-07 from Assets, P-204 from Today, and C-07 by its code on Scan, pressing
/// Back after each. It prints the asset each one asked for, the asset the workspace shows and where
/// Back landed, and records a Pass at C-07's first point and prints the asset the stored event
/// names.</item>
/// <item><c>--trace-from=Assets|Scan|Sync|Findings</c> opens that destination first and the asset
/// from there; <c>--trace-back=1</c> then presses the asset's Back and prints where it landed.</item>
/// <item><c>--trace-seed=empty</c> clears the round, the corrective actions and the queue after the
/// store loads, for the empty states.</item>
/// <item><c>--trace-focus=log</c> prints every element keyboard focus lands on.</item>
/// <item><c>--trace-fault=load</c> makes every store read fail for the first 20 seconds, for the
/// error states and their Retry.</item>
/// <item><c>--trace-crash=launch|task</c> throws where nothing catches it: in the launch itself,
/// or in a task nobody awaits, and the task case prints whether <see cref="CrashReporter"/> wrote
/// it to <c>crash.log</c>. The launch case leaves no window: the failure is in <c>crash.log</c>
/// and on the console.</item>
/// </list>
/// Each step prints a <c>VERIFY</c> line to stderr so a run can be checked without the picture.
/// </remarks>
public partial class App
{
    private static string? VerificationArg(string name) => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith($"--trace-{name}=", StringComparison.OrdinalIgnoreCase))?
        .Split('=', 2)[1];

    /// <summary>A launch that fails, for the launch guard and the crash file.</summary>
    private static void ThrowWhenAskedAtLaunch()
    {
        if (VerificationArg("crash") is "launch")
        {
            throw new InvalidOperationException("Verification crash during launch (--trace-crash=launch).");
        }
    }

    /// <summary>An exception nothing catches, off the launch path, and whether it reached the crash file.</summary>
    private async Task CrashWhenAskedAsync()
    {
        if (VerificationArg("crash") is not "task")
        {
            return;
        }

        // Faulted and dropped: the finalizer raises UnobservedTaskException once it is collected.
        var marker = $"Verification crash (task) {Guid.NewGuid():N}";
        _ = Task.Run(new Action(() => throw new InvalidOperationException(marker)));
        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(1500);
        var path = CrashReporter.LogPath;
        var written = path is not null && File.Exists(path) && (await File.ReadAllTextAsync(path)).Contains(marker, StringComparison.Ordinal);
        Console.Error.WriteLine($"VERIFY crash=task reported={written} log={path}");
    }

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

            await CrashWhenAskedAsync();

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

            if (route is "Switch")
            {
                await VerifyAssetSwitchingAsync(root, today, navigator);
                return;
            }

            AssetSummary? pump = null;
            try
            {
                await foreach (var summaries in Host.Services.GetRequiredService<IAssetRepository>().WatchSummaries(CancellationToken.None))
                {
                    pump = summaries.FirstOrDefault(s => s.AssetId == "P-204");
                    break;
                }
            }
            catch (IOException)
            {
                // --trace-fault=load: the store cannot be read yet, so open the pump from its
                // identity alone, as a card drawn before the failure would, for the error state.
                pump = new AssetSummary(
                    "P-204", "P-204", "Centrifugal Pump", "Pump", "Water Treatment", "Area B",
                    string.Empty, string.Empty, null, 0, 0, AssetCondition.Unknown, 0);
            }

            // The asset opens from the destination named by --trace-from, as its own list would.
            FrameworkElement opener = today;
            if (VerificationArg("from") is { } from && from != "Today")
            {
                await navigator.NavigateRouteAsync(today, $"../{from}");
                await Task.Delay(800);
                opener = from switch
                {
                    "Assets" => Find<AssetsPage>(root),
                    "Scan" => Find<ScanPage>(root),
                    "Sync" => Find<SyncPage>(root),
                    "Findings" => Find<SupervisorFindingsPage>(root),
                    _ => (FrameworkElement?)null,
                } ?? today;
            }

            var response = await (opener.Navigator() ?? navigator).NavigateRouteAsync(opener, "../Asset", data: pump);
            Console.Error.WriteLine($"VERIFY route={spec} asset={pump?.AssetId} from={opener.GetType().Name} success={response?.Success}");

            await Task.Delay(1500);
            var page = Find<AssetPage>(root);
            if (page is null)
            {
                Console.Error.WriteLine("VERIFY asset page not found");
                return;
            }

            if (VerificationArg("back") is "1")
            {
                Invoke(page, "Back");
                await Task.Delay(1200);
                var shown = new FrameworkElement?[]
                    {
                        today, Find<AssetsPage>(root), Find<ScanPage>(root), Find<SyncPage>(root),
                        Find<SupervisorFindingsPage>(root), page,
                    }
                    .Where(p => p is { Parent: FrameworkElement { Visibility: Visibility.Visible } })
                    .Select(p => p!.GetType().Name);
                Console.Error.WriteLine(
                    $"VERIFY back origin={Host.Services.GetRequiredService<NavigationOrigin>().Route} shown={string.Join(",", shown)}");
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

            if (route is not ("Point" or "Action" or "Again" or "Discard" or "Photo"))
            {
                return;
            }

            // The point strip under the machine: the same button a technician would press.
            var chip = All<Button>(page).FirstOrDefault(b =>
                b.DataContext is PointMarkerView marker && marker.Point.Label.Contains("Seal", StringComparison.OrdinalIgnoreCase)
                && b is not InspectionPointMarker);
            chip?.Command?.Execute(chip.CommandParameter);
            await Task.Delay(1500);

            if (route is "Again")
            {
                // Complete, edit, complete again: two events, not one and a duplicate.
                var store = Host.Services.GetRequiredService<ITraceStore>();
                int SealEvents(TraceSnapshot s) => s.Events.Count(e => e.PointId == "P-204-seal");
                var before = SealEvents(await store.GetAsync(CancellationToken.None));

                SetText(page, "New reading", "1.6");
                await Task.Delay(800);
                Invoke(page, "Record pass");
                await Task.Delay(800);
                Invoke(page, "Complete point");
                await Task.Delay(1500);
                SetText(page, "New reading", "2.1");
                await Task.Delay(800);
                Invoke(page, "Record watch");
                await Task.Delay(800);
                Invoke(page, "Complete point");
                await Task.Delay(1500);

                var after = SealEvents(await store.GetAsync(CancellationToken.None));
                Console.Error.WriteLine($"VERIFY again seal events before={before} after={after}");
                return;
            }

            SetText(page, "New reading", "7.4");
            await Task.Delay(800);
            Console.Error.WriteLine($"VERIFY point opened={chip is not null}");

            if (route is "Photo")
            {
                Invoke(page, "Add photo evidence");
                await Task.Delay(2000);
                var draft = (await Host.Services.GetRequiredService<ITraceStore>().GetAsync(CancellationToken.None)).FindDraft("P-204-seal");
                Console.Error.WriteLine($"VERIFY photo uri={draft?.Evidence.LastOrDefault()?.LocalUri}");
                return;
            }

            if (route is "Discard")
            {
                Invoke(page, "Discard what was entered at this point");
                await Task.Delay(800);
                Console.Error.WriteLine("VERIFY discard asked");
                return;
            }

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

    /// <summary>
    /// Opens one asset after another in a single session, from each kind of entry point, and
    /// checks that every one shows the asset it was opened for and that Back returns to where
    /// it was opened from. Then records a reading on the second and checks the store files it
    /// under that asset. The checklist's other hooks open one asset per launch, which is how a
    /// workspace that kept showing the first asset went unnoticed.
    /// </summary>
    private async Task VerifyAssetSwitchingAsync(FrameworkElement root, TodayPage today, INavigator navigator)
    {
        var services = Host!.Services;
        IImmutableList<AssetSummary> summaries = ImmutableList<AssetSummary>.Empty;
        await foreach (var value in services.GetRequiredService<IAssetRepository>().WatchSummaries(CancellationToken.None))
        {
            summaries = value;
            break;
        }

        var visits = new (string AssetId, string From)[] { ("AHU-12", "Today"), ("C-07", "Assets"), ("P-204", "Today"), ("C-07", "Scan") };
        var passed = 0;
        string? recordedOn = null;

        for (var i = 0; i < visits.Length; i++)
        {
            var (assetId, from) = visits[i];
            await navigator.NavigateRouteAsync(today, $"../{from}");
            await Task.Delay(800);

            if (from is "Scan")
            {
                // The code typed off the nameplate, through Scan's own Open asset.
                var scan = Find<ScanPage>(root);
                SetText(scan!, "Asset code", assetId);
                await Task.Delay(500);
                All<Button>(scan!).FirstOrDefault(b => b.Content as string == "Open asset")?.Command?.Execute(null);
            }
            else
            {
                // A card on that destination: the request its list makes, with the row it carries.
                FrameworkElement opener = from is "Assets" ? Find<AssetsPage>(root)! : today;
                var summary = summaries.First(s => s.AssetId == assetId);
                await (opener.Navigator() ?? navigator).NavigateRouteAsync(opener, "../Asset", data: summary);
            }

            await Task.Delay(1500);
            var page = Find<AssetPage>(root);
            var shown = (page?.DataContext as AssetViewModel)?.AssetId;
            var headers = page is null
                ? string.Empty
                : string.Join("|", All<PageHeader>(page).Select(h => h.Eyebrow).Where(e => !string.IsNullOrEmpty(e)).Distinct());
            var opened = shown == assetId && headers.Length > 0 && headers.Split('|').All(h => h.StartsWith(assetId, StringComparison.Ordinal));

            if (i == 1 && page is not null && opened)
            {
                recordedOn = await RecordPassAtFirstPointAsync(page);
            }

            Invoke(page!, "Back");
            await Task.Delay(1200);
            var back = new FrameworkElement?[]
                {
                    today, Find<AssetsPage>(root), Find<ScanPage>(root), Find<SyncPage>(root), Find<SupervisorFindingsPage>(root), page,
                }
                .Where(p => p is { Parent: FrameworkElement { Visibility: Visibility.Visible } })
                .Select(p => p!.GetType().Name)
                .FirstOrDefault();
            var returned = back == $"{from}Page";

            Console.Error.WriteLine(
                $"VERIFY switch asked={assetId} from={from} shown={shown} header=\"{headers}\" back={back} ok={opened && returned}");
            passed += opened && returned ? 1 : 0;
        }

        var result = passed == visits.Length && recordedOn == visits[1].AssetId ? "pass" : "fail";
        Console.Error.WriteLine($"VERIFY switch result={result} opened={passed}/{visits.Length} recordedOn={recordedOn}");
    }

    /// <summary>Completes the open asset's first point as Pass, mid-range, and returns the asset the stored event names.</summary>
    private async Task<string?> RecordPassAtFirstPointAsync(AssetPage page)
    {
        var store = Host!.Services.GetRequiredService<ITraceStore>();
        var before = (await store.GetAsync(CancellationToken.None)).Events.Select(e => e.Id).ToHashSet();

        var chip = All<Button>(page).FirstOrDefault(b => b.DataContext is PointMarkerView && b is not InspectionPointMarker);
        if (chip?.DataContext is not PointMarkerView { Point: var point })
        {
            Console.Error.WriteLine("VERIFY switch recorded none: no point to open");
            return null;
        }

        chip.Command?.Execute(chip.CommandParameter);
        await Task.Delay(1500);
        SetText(page, "New reading", ((point.NormalMin + point.NormalMax) / 2).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture));
        await Task.Delay(800);
        Invoke(page, "Record pass");
        await Task.Delay(800);
        Invoke(page, "Complete point");
        await Task.Delay(1500);

        var added = (await store.GetAsync(CancellationToken.None)).Events.Where(e => !before.Contains(e.Id)).ToList();
        foreach (var recorded in added)
        {
            Console.Error.WriteLine(
                $"VERIFY switch recorded event={recorded.Id} asset={recorded.AssetId} point={recorded.PointId} reading={recorded.ReadingText} disposition={recorded.Disposition}");
        }

        return added.Count == 1 ? added[0].AssetId : null;
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

        public ValueTask MoveAsync(string key, string newKey, CancellationToken ct) => inner.MoveAsync(key, newKey, ct);
    }
}
#endif
