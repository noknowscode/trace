using Uno.UI.Hosting;

namespace Trace;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            // Uno 6.7 test build: X11HostBuilder.ForceRenderingBackend (the TRACE_X11_RENDERER=software switch the
            // Xvfb screenshot scripts use) is Uno 7 only, so the switch is off on this branch.
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
