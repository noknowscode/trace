using Uno.UI.Hosting;

namespace Trace;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
#if DEBUG
            // Screenshot runs under Xvfb (tools/matrix.sh): TRACE_X11_RENDERER=software forces Skia's CPU
            // backend instead of Mesa's llvmpipe OpenGL, which leaves parts of the window unpainted after a
            // launch or a resize: whole cards missing from the round, the rail absent at a new width.
            .UseX11(x11 =>
            {
                if (Environment.GetEnvironmentVariable("TRACE_X11_RENDERER") == "software")
                {
                    x11.ForceRenderingBackend(X11RenderingBackend.Software);
                }
            })
#else
            .UseX11()
#endif
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
