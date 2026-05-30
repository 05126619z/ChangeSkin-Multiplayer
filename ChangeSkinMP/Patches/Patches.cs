using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BepInEx;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Video;
using static UnityEngine.UIElements.UIR.GradientSettingsAtlas;

namespace ChangeSkinMP
{
    //     [HarmonyPatch(typeof(PlayerBody))]
    //     internal class PlayerBody_Patch1
    //     {
    //         [HarmonyPatch(nameof(PlayerBody.OnFoundScavClientInstanceInitFinish))]
    //         public static void Postfix(PlayerBody __instance)
    //         {
    //             if (!ChangeSkinMonoBehaviour.playerBodies.Contains(__instance))
    //                 ChangeSkinMonoBehaviour.playerBodies.Add(__instance);
    //             ChangeBody changeBody = __instance.body.gameObject.GetComponent<ChangeBody>();
    //             if (changeBody == null)
    //             {
    //                 changeBody = __instance.body.gameObject.AddComponent<ChangeBody>();
    //             }
    //             ChangeSkinMonoBehaviour.replacers[__instance.clientId] = changeBody;
    //             if (__instance.sci == ScavClientInstance.local_scavclientinstance)
    //             {
    //                 changeBody.isLocalChangeBody = true;
    //                 ChangeSkinMonoBehaviour.localChangeBody = changeBody;
    //                 ChangeSkinMonoBehaviour.localPlayerBody = __instance;
    //                 ChangeSkinMonoBehaviour.localBody = __instance.body;
    //             }
    //         }
    //     }

    [HarmonyPatch(typeof(NetBody), nameof(NetBody.Start))]
    internal class NetBody_Patch_Start
    {
        public static void Postfix(NetBody __instance)
        {
            if (Net.is_server || Net.is_client_or_host)
            {
                NetworkRegistry.RegisterConnected(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(NetBody), nameof(NetBody.DestroyNPC))]
    internal class NetBody_Patch_DestroyNPC
    {
        public static void Prefix(NetBody __instance)
        {
            NetworkRegistry.RegisterDisconnected(__instance);
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
                        string output = ArgsParser.Execute(args);
                        ConsoleScript.instance.LogToConsole(output);
                        // Plugin.Logger.LogInfo(output);
                    },
                    null,
                    []
                )
            );

            Con.localonly_commands.Add("skin");
        }
    }

// [HarmonyPatch(typeof(KrokoshaScavMultiplayer), nameof(KrokoshaScavMultiplayer._FIRST_INIT))]
    // internal class KrokoshaScavMultiplayer_Patch__FIRST_INIT
    // {
    //     public static void Postfix()
    //     {
    // 
    //     }
    // }

    [HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.TryExecuteCommand))]
    [HarmonyPriority(700)]
    internal class ConsoleScript_Patch_TryExecuteCommand
    {
        public static bool Prefix(ConsoleScript __instance, string[] args)
        {
            if (args[0] == "skin")
            {
                if (!Con.CanExecuteAdminCommands())
                {
                    __instance.LogToConsole("Only the host or an admin can use skin commands.");
                    __instance.AddCommandToLogAndClearInput();
                    return false;
                }
                string output = ArgsParser.Execute(args);
                __instance.LogToConsole(output);
                __instance.AddCommandToLogAndClearInput();
                return false;
            }
            return true;
        }
    }
}
