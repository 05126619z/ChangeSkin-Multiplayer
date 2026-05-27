using ChangeSkinMP;

internal class Log
{
    public static void Info(string message)
    {
        string msg = $"[ChangeSkin] {message}";
        Plugin.Logger.LogInfo(msg);
        if (ModConfig.Instance.Verbose)
            ConsoleScript.instance.LogToConsole(msg);
    }

    public static void Warn(string message)
    {
        string msg = $"[ChangeSkin] WARNING: {message}";
        Plugin.Logger.LogWarning(msg);
        if (ModConfig.Instance.Verbose)
            ConsoleScript.instance.LogToConsole(msg);
    }

    public static void Err(string message)
    {
        string msg = $"[ChangeSkin] ERROR: {message}";
        Plugin.Logger.LogError(msg);
        if (ModConfig.Instance.Verbose)
            ConsoleScript.instance.LogToConsole(msg);
    }
}
