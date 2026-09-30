namespace OttoStash.Storing;

/// Logging through a method call rather than the plugin's static field. Code
/// that only logs on an error path can then run in the unit tests, where the
/// plugin's static initializer cannot.
internal static class StashLog
{
    internal static void Debug(string message)
    {
        LogSource.LogDebug(message);
    }

    internal static void Info(string message)
    {
        LogSource.LogInfo(message);
    }

    internal static void Warning(string message)
    {
        LogSource.LogWarning(message);
    }

    internal static void Error(string message)
    {
        LogSource.LogError(message);
    }

    /// Written only by a Debug build.
    internal static void BuildDebug(string message)
    {
#if DEBUG
        LogSource.LogDebug(message);
#endif
    }
}
