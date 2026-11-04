using Trace.Infrastructure;

namespace Trace.Tests;

/// <summary>The last-resort report: once per exception, and never a second exception of its own.</summary>
[NonParallelizable]
public class CrashReporterTests
{
    [TearDown]
    public void TearDown() => CrashReporter.LogPath = null;

    [Test]
    public void An_exception_is_written_to_the_crash_file_once()
    {
        var folder = Path.Combine(Path.GetTempPath(), "trace-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "crash.log");
        try
        {
            CrashReporter.LogPath = path;
            var crash = new InvalidOperationException("The round could not be built.");

            CrashReporter.Report("OnLaunched", crash);
            CrashReporter.Report("UI thread", crash);

            var text = File.ReadAllText(path);
            text.Should().Contain("OnLaunched").And.Contain("The round could not be built.");
            text.Should().NotContain("UI thread", "the rethrown launch failure reaches the global handlers too, and is reported once");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Test]
    public void A_crash_file_that_cannot_be_written_does_not_throw()
    {
        var file = Path.GetTempFileName();
        try
        {
            // The crash file's folder is a file: the write fails, and the report must not throw over the crash.
            CrashReporter.LogPath = Path.Combine(file, "crash.log");

            var report = () => CrashReporter.Report("AppDomain", new InvalidOperationException("boom"));

            report.Should().NotThrow();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
