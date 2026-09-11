namespace Trace.Services;

/// <summary>
/// Dark is the working default — a technician's eyes are adapted to a mechanical room, and
/// a white screen at 2 a.m. in a basement is hostile. Light exists for office review.
/// </summary>
public interface IAppThemeService
{
    bool IsDark { get; }

    IAsyncEnumerable<bool> Watch(CancellationToken ct);

    void Toggle();
}
