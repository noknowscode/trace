namespace Trace.Infrastructure;

/// <summary>
/// Last-resort reporting for exceptions nothing else caught: on the UI thread
/// (<see cref="Application.UnhandledException"/>), on any thread
/// (<see cref="AppDomain.UnhandledException"/>), in faulted tasks nobody awaited
/// (<see cref="TaskScheduler.UnobservedTaskException"/>), and in work the app starts and does not
/// wait for. Each goes to the app log once the host exists and to <c>crash.log</c> in the app's
/// local folder: a desktop Uno app is a windowed process, so its console output is lost.
/// </summary>
/// <remarks>
/// Static because the handlers are: they attach before the host and its container exist, and
/// must keep working if building the host is what failed. Reporting changes no behaviour: an
/// exception that ended the app before still ends it.
/// </remarks>
public static class CrashReporter
{
    private const string ReportedKey = "Trace.CrashReporter.Reported";

    /// <summary>The app log; null until the host is built, when reports go to the console as well as the file.</summary>
    public static ILogger? Logger { get; set; }

    /// <summary>The crash file, appended to; null writes no file.</summary>
    public static string? LogPath { get; set; }

    /// <summary>Attaches the handlers for the application, the process and unobserved tasks.</summary>
    public static void Attach(Application app)
    {
        app.UnhandledException += (_, e) => Report("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("Unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Logs <paramref name="exception"/> and appends it to the crash file, once per exception instance.</summary>
    public static void Report(string source, Exception? exception)
    {
        if (exception is not null)
        {
            // OnLaunched reports and rethrows, and the rethrown exception reaches the handlers above as well.
            if (exception.Data.Contains(ReportedKey))
            {
                return;
            }

            exception.Data[ReportedKey] = true;
        }

        if (Logger is { } logger)
        {
            logger.LogCritical(exception, "Unhandled exception ({Source}).", source);
        }
        else
        {
            Console.Error.WriteLine($"Unhandled exception ({source}): {exception}");
        }

        if (LogPath is not { } path)
        {
            return;
        }

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The report is already in the log; a crash path must not throw a second exception over the first.
        }
    }
}
