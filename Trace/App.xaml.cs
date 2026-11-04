using System.Diagnostics.CodeAnalysis;
using Trace.Infrastructure.Persistence;
using Trace.Infrastructure.Platform;
using Trace.Infrastructure.Repositories;
using Uno.Resizetizer;
using Windows.Storage;

namespace Trace;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning),
                    enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .UseLocalization()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(TimeProvider.System);

                    // Persistence. The store root is chosen here, at composition time (and opened
                    // on first use), so nothing below this line has to know about platform storage
                    // paths.
#if DEBUG
                    services.AddSingleton(_ => WithVerificationFaults(new LocalFileStore(OpenStoreRootAsync, StoreAppDataUri)));
#else
                    services.AddSingleton<ILocalFileStore>(_ => new LocalFileStore(OpenStoreRootAsync, StoreAppDataUri));
#endif
                    services.AddSingleton<ITraceStore, TraceStore>();

                    // Repositories over the shared snapshot stream.
                    services.AddSingleton<IAssetRepository, AssetRepository>();
                    services.AddSingleton<IAssignmentRepository, AssignmentRepository>();
                    services.AddSingleton<IInspectionRepository, InspectionRepository>();
                    services.AddSingleton<ICorrectiveActionRepository, CorrectiveActionRepository>();
                    services.AddSingleton<ISyncQueue, SyncQueue>();
                    services.AddSingleton<IInspectionWorkflow, InspectionWorkflow>();
                    services.AddSingleton<IActionDispatch, ActionDispatch>();

                    // One retry for every screen that failed to read the store.
                    services.AddSingleton<FeedRecovery>();

                    // Where an asset was opened from, for its Back.
                    services.AddSingleton<NavigationOrigin>();

                    // Platform capabilities. Each is a deterministic stand-in for hardware
                    // TRACE does not own; swapping in a device implementation is a one-line
                    // change here.
                    services.AddSingleton<INetworkStatusService, SimulatedNetworkStatusService>();
                    services.AddSingleton<ICameraService, SimulatedCameraService>();
                    services.AddSingleton<IAssetScanService, SimulatedAssetScanService>();
                    services.AddSingleton<ILocalEvidenceStore, LocalEvidenceStore>();
                })
                // Dark is the working default (declared on the Application); this lets a
                // technician override it for office review and remembers the choice.
                .UseThemeSwitching()
                .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
            );
        MainWindow = builder.Window;

#if DEBUG && !NO_UNO_STUDIO
        // UseStudio blocks the window from ever being created when no DevServer is
        // reachable, which makes headless verification runs hang with no visible error.
        // TRACE_NO_HOTDESIGN=1 opts a run out of it.
        if (Environment.GetEnvironmentVariable("TRACE_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();

        Host = await MainWindow.InitializeNavigationAsync(
            () => Task.FromResult(builder.Build()),
            initialRoute: "Main"
        );

        // The destinations share one region, which keeps no back stack: record each one as it
        // is shown, so an asset's Back returns to the destination it was opened from. The
        // route after a navigation reads Main/<destination>, so the segment under Main is it.
        var origin = Host.Services.GetRequiredService<NavigationOrigin>();
        Host.Services.GetRequiredService<IRouteNotifier>().RouteChanged +=
            (_, e) => origin.Shown(e.Route?.Next()?.Base);

        // The theme the technician last chose is restored by the theme service itself: it is
        // scoped to the navigation, and the instance the round's model takes applies the saved
        // choice as soon as it is created. Asking the root provider for one here created a second
        // instance on a root that is never loaded, and awaiting it stopped OnLaunched for good.
#if DEBUG
        await ApplyVerificationAsync();
#endif
    }

    /// <summary>The store's folder under the app's local data.</summary>
    private const string StoreFolder = "trace";

    /// <summary>The same folder as an app-data URI, which evidence photos are referenced by.</summary>
    private const string StoreAppDataUri = $"ms-appdata:///local/{StoreFolder}/";

    /// <summary>
    /// The store's folder, opened through StorageFolder before any file access: on WebAssembly
    /// that call is what waits for the browser-backed local folder to be restored.
    /// </summary>
    private static async ValueTask<string> OpenStoreRootAsync() =>
        (await ApplicationData.Current.LocalFolder.CreateFolderAsync(StoreFolder, CreationCollisionOption.OpenIfExists)).Path;

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<TodayPage, TodayModel>(),
            new ViewMap<AssetsPage, AssetsModel>(),
            new ViewMap<ScanPage, ScanModel>(),
            new ViewMap<SyncPage, SyncModel>(),
            new ViewMap<SupervisorFindingsPage, SupervisorFindingsModel>(),

            // The asset workspace is entered with the row that was tapped, so it opens on
            // the machine rather than on a lookup.
            new DataViewMap<AssetPage, AssetModel, AssetSummary>()
        );

        routes.Register(
            new RouteMap(
                "Main",
                View: views.FindByViewModel<MainModel>(),
                IsDefault: true,
                Nested:
                [
                    new RouteMap("Today", View: views.FindByViewModel<TodayModel>(), IsDefault: true),
                    new RouteMap("Assets", View: views.FindByViewModel<AssetsModel>()),
                    new RouteMap("Scan", View: views.FindByViewModel<ScanModel>()),
                    new RouteMap("Sync", View: views.FindByViewModel<SyncModel>()),
                    new RouteMap("Findings", View: views.FindByViewModel<SupervisorFindingsModel>()),
                    new RouteMap("Asset", View: views.FindByViewModel<AssetModel>()),
                ])
        );
    }
}
