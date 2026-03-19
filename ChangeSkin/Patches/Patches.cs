using ChangeSkin.App;
using ChangeSkin.Core;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine.SceneManagement;

namespace ChangeSkin.Patches;

[HarmonyPatch(
    typeof(KrokoshaScavMultiplayer),
    nameof(KrokoshaScavMultiplayer.KrokoshaOnSceneLoaded)
)]
internal class KrokoshaScavMultiplayer_Patch_KrokoshaOnSceneLoaded
{
    public static void Postfix(object[] __args)
    {
        if (
            __args[0] is Scene scene
            && scene.name == "SampleScene"
            && !ChangeSkinMain.initialized
        )
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    "[Patch] KrokoshaOnSceneLoaded: SampleScene detected — triggering Init()"
                );

            ChangeSkinMain.Init();
        }
    }
}

[HarmonyPatch(typeof(NetBody), nameof(NetBody.OnFoundNetPlayerInitFinish))]
internal class NetBody_Patch_OnFoundNetPlayerInitFinish
{
    public static void Postfix(NetBody __instance)
    {
        if (!ChangeSkinMain.initialized)
            return;

        ulong clientId = __instance.player.clientId;

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[Patch] OnFoundNetPlayerInitFinish: clientId={clientId} name={__instance.name}"
            );

        // Skip if already registered
        if (ChangeSkinMain.replacers.ContainsKey(clientId))
            return;

        // Find the matching NetPlayer and register
        foreach (NetPlayer scav in NetPlayer.ClientIdToPlayerDict.Values)
        {
            if (scav.clientId == clientId)
            {
                ChangeSkinMain.RegisterPlayer(scav);
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(NetBody), nameof(NetBody.OnDestroy))]
internal class NetBody_Patch_OnDestroy
{
    public static void Prefix(NetBody __instance)
    {
        if (!ChangeSkinMain.initialized)
            return;

        ulong clientId = __instance.player.clientId;

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[Patch] NetBody.OnDestroy: clientId={clientId} name={__instance.name}");

        ChangeSkinMain.UnregisterPlayer(clientId);
    }
}

[HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.RegisterAllCommands))]
internal class ConsoleScript_Patch_RegisterAllCommands
{
    public static void Postfix()
    {
        ConsoleScript.Commands.Add(
            new Command(
                "skin",
                "Control command for ChangeSkin",
                delegate(string[] args)
                {
                    string output = ChangeSkinMain.ToggleReplacement(args);
                    ConsoleScript.instance.LogToConsole(output);
                },
                null,
                []
            )
        );
    }
}

[HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.TryExecuteCommand))]
[HarmonyPriority(300)]
internal class ConsoleScript_Patch_TryExecuteCommand
{
    public static bool Prefix(ConsoleScript __instance, string[] args, bool addToLog)
    {
        if (args.Length > 0 && args[0] == "skin")
        {
            string output = ChangeSkinMain.ToggleReplacement(args);
            __instance.LogToConsole(output);
            __instance.AddCommandToLogAndClearInput();
            return false;
        }
        return true;
    }
}
