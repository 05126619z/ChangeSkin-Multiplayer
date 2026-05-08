using ChangeSkinMP;

internal class Log
{
    public static void Info(string message)
    {
        Plugin.Logger.LogInfo($"[ChangeSkin] {message}");
    }

    public static void Warn(string message)
    {
        Plugin.Logger.LogWarning($"[ChangeSkin] WARNING: {message}");
    }

    public static void Err(string message)
    {
        Plugin.Logger.LogError($"[ChangeSkin] ERROR: {message}");
    }
}
