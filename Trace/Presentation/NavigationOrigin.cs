namespace Trace.Presentation;

/// <summary>
/// The destination an asset was opened from, so the asset's Back returns there.
/// </summary>
/// <remarks>
/// Today, Assets, Scan and Findings can all open an asset, and back has to restore the exact
/// context it came from (UNO-BUILD-SPEC, navigation contract). The destinations and the asset
/// share one Visibility region, which keeps no back stack, so going back means naming a
/// route: the app records each destination as it is shown (App.xaml.cs, from the navigation's
/// route notifications) and the asset navigates back to the last one.
/// </remarks>
public sealed class NavigationOrigin
{
    private static readonly string[] Destinations = ["Today", "Assets", "Scan", "Sync", "Findings"];

    private string _route = "Today";

    /// <summary>The last destination shown; Today until another one is.</summary>
    public string Route => Volatile.Read(ref _route);

    /// <summary>Records a route that was shown. Anything but a destination, such as the asset itself, is ignored.</summary>
    public void Shown(string? route)
    {
        if (route is not null && Destinations.Contains(route))
        {
            Volatile.Write(ref _route, route);
        }
    }
}
