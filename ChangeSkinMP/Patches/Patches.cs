using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using TMPro;
using UnityEngine;

namespace ChangeSkinMP
{
    [HarmonyPatch(typeof(NetBody), nameof(NetBody.Start))]
    internal class NetBody_Patch_Start
    {
        public static void Postfix(NetBody __instance)
        {
            if (Net.is_server || Net.is_client_or_host)
            {
                NetworkRegistry.RegisterConnected(__instance);
                ConsoleScript_Patch_RegisterAllCommands.RefreshPlayerNames();
            }
        }
    }

    [HarmonyPatch(typeof(NetBody), nameof(NetBody.DestroyNPC))]
    internal class NetBody_Patch_DestroyNPC
    {
        public static void Prefix(NetBody __instance)
        {
            NetworkRegistry.RegisterDisconnected(__instance);
            ConsoleScript_Patch_RegisterAllCommands.RefreshPlayerNames();
        }
    }

    [HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.RegisterAllCommands))]
    internal class ConsoleScript_Patch_RegisterAllCommands
    {
        internal static Command skinCommand;
        internal static List<string> localSkinNames;

        public static void Postfix()
        {
            localSkinNames = GetLocalSkinNames();

            skinCommand = new Command(
                "skin",
                "Control command for ChangeSkin",
                delegate(string[] args)
                {
                    string output = ArgsParser.Execute(args);
                    ConsoleScript.instance.LogToConsole(output);
                },
                new Dictionary<int, List<string>>
                {
                    [0] = new List<string>
                    {
                        "load-local", "load-remote", "rule-set", "rule-get",
                        "ban", "unban", "enable", "disable"
                    },
                    [1] = new List<string>(),
                    [2] = new List<string> { "true", "false" },
                },
                []
            );

            RefreshArg1();
            ConsoleScript.Commands.Add(skinCommand);
            Con.localonly_commands.Add("skin");
        }

        internal static void RefreshArg1()
        {
            if (skinCommand == null)
                return;

            var arg1 = skinCommand.argAutofill[1];
            arg1.Clear();

            if (localSkinNames != null)
                arg1.AddRange(localSkinNames);
            arg1.Add("[remote skin url]");
            arg1.Add("SkinUploading");
            arg1.Add("SkinDownloading");
            arg1.AddRange(GetPlayerNames());

            ConsoleScript_Patch_Update.InvalidateCache();
        }

        internal static void RefreshPlayerNames()
        {
            if (skinCommand == null)
                return;

            var arg1 = skinCommand.argAutofill[1];
            for (int i = arg1.Count - 1; i >= 0; i--)
            {
                if (!IsStaticSuggestion(arg1[i]))
                    arg1.RemoveAt(i);
            }

            arg1.AddRange(GetPlayerNames());

            ConsoleScript_Patch_Update.InvalidateCache();
        }

        static bool IsStaticSuggestion(string s) =>
            s == "[remote skin url]"
            || s == "SkinUploading"
            || s == "SkinDownloading"
            || (localSkinNames != null && localSkinNames.Contains(s));

        static List<string> GetLocalSkinNames()
        {
            string resourcesDir = Path.Combine(Paths.PluginPath, "ChangeSkinMP", "resources");
            var names = new List<string>();

            if (Directory.Exists(resourcesDir))
            {
                foreach (var dir in Directory.GetDirectories(resourcesDir))
                    names.Add(Path.GetFileName(dir));

                foreach (var zip in Directory.GetFiles(resourcesDir, "*.zip"))
                    names.Add(Path.GetFileNameWithoutExtension(zip));
            }

            return names;
        }

        internal static List<string> GetPlayerNames()
        {
            var names = new List<string>();
            foreach (var entry in NetworkRegistry.Players)
            {
                if (entry.PlayerInfo?.Nickname != null)
                    names.Add(entry.PlayerInfo.Nickname);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(ConsoleScript), "Update")]
    internal class ConsoleScript_Patch_Update
    {
        static string _lastInput = "";

        static void Postfix(ConsoleScript __instance)
        {
            var cmd = ConsoleScript_Patch_RegisterAllCommands.skinCommand;
            if (cmd == null) return;

            if (!__instance.active) return;

            var inputField = Traverse.Create(__instance).Field("input").GetValue<TMP_InputField>();
            if (inputField == null) return;

            string text = inputField.text;
            if (text == _lastInput) return;
            _lastInput = text;

            string trimmed = text.TrimStart();
            if (trimmed != "skin" && !trimmed.StartsWith("skin ")) return;

            string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string subcommand = parts.Length > 1 ? parts[1] : "";

            var arg1 = cmd.argAutofill[1];
            var arg2 = cmd.argAutofill[2];

            arg1.Clear();
            arg2.Clear();

            switch (subcommand)
            {
                case "load-local":
                    if (ConsoleScript_Patch_RegisterAllCommands.localSkinNames != null)
                        arg1.AddRange(ConsoleScript_Patch_RegisterAllCommands.localSkinNames);
                    break;
                case "load-remote":
                    arg1.Add("[remote skin url]");
                    break;
                case "rule-set":
                    arg1.Add("SkinUploading");
                    arg1.Add("SkinDownloading");
                    arg2.Add("true");
                    arg2.Add("false");
                    break;
                case "rule-get":
                    arg1.Add("SkinUploading");
                    arg1.Add("SkinDownloading");
                    break;
                case "ban":
                case "unban":
                    arg1.AddRange(ConsoleScript_Patch_RegisterAllCommands.GetPlayerNames());
                    break;
                case "enable":
                case "disable":
                    break;
                default:
                    if (ConsoleScript_Patch_RegisterAllCommands.localSkinNames != null)
                        arg1.AddRange(ConsoleScript_Patch_RegisterAllCommands.localSkinNames);
                    arg1.Add("[remote skin url]");
                    arg1.Add("SkinUploading");
                    arg1.Add("SkinDownloading");
                    arg1.AddRange(ConsoleScript_Patch_RegisterAllCommands.GetPlayerNames());
                    arg2.Add("true");
                    arg2.Add("false");
                    break;
            }
        }

        internal static void InvalidateCache() => _lastInput = "";
    }

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