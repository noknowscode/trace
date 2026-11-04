namespace Trace.Presentation;

/// <summary>The failures a command reports on screen rather than letting escape.</summary>
internal static class StoreFailures
{
    /// <summary>The store or the evidence folder could not be read or written.</summary>
    public static bool IsStorageFailure(Exception ex) => ex is IOException or UnauthorizedAccessException;
}
