using System.Diagnostics.CodeAnalysis;
using System.IO;
using Trace.Infrastructure.Persistence;
using Trace.Infrastructure.Platform;
using Trace.Infrastructure.Repositories;
using Uno.Resizetizer;
using Windows.Storage;

namespace Trace;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
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
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .UseHttp((context, services) => {
#if DEBUG
                // DelegatingHandler will be automatically injected
                services.AddTransient<DelegatingHandler, DebugHttpHandler>();
#endif

})
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(TimeProvider.System);

                    // Persistence. The store root is resolved here, at composition time, so
                    // nothing below this line has to know about platform storage paths.
                    services.AddSingleton<ILocalFileStore>(_ => new LocalFileStore(
                        Path.Combine(ApplicationData.Current.LocalFolder.Path, "trace")));
                    services.AddSingleton<ITraceStore, TraceStore>();

                    // Repositories over the shared snapshot stream.
                    services.AddSingleton<IAssetRepository, AssetRepository>();
                    services.AddSingleton<IAssignmentRepository, AssignmentRepository>();
                    services.AddSingleton<IInspectionRepository, InspectionRepository>();
                    services.AddSingleton<ICorrectiveActionRepository, CorrectiveActionRepository>();
                    services.AddSingleton<ISyncQueue, SyncQueue>();
                    services.AddSingleton<IInspectionWorkflow, InspectionWorkflow>();

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

        // Restores the theme the technician last chose. The service resolves the root itself,
        // so this has to run after the navigation root exists.
        await Host.Services.GetRequiredService<IThemeService>().InitializeAsync();
    }

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
