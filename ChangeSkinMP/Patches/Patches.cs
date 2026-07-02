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
            // 4.0.1: is_server/is_client_or_host are true in SP too (Steamworks
            // inits without a lobby). Require a real MP local player before
            // registering NetBody instances; RegisterConnected compares against
            // LOCAL_PLAYER.clientId and would NRE otherwise.
            if (NetPlayer.LOCAL_PLAYER != null)
            {
                NetworkRegistry.RegisterConnected(__instance);
                ConsoleScript_Patch_RegisterAllCommands.RefreshPlayerNames();
            }
        }
    }

    // NOTE: No [HarmonyPatch] attribute here. Krokosha 4.0.1 added overloads of
    // NetBody.DestroyNPC, so attribute-based lookup is ambiguous and throws
    // AmbiguousMatchException — which aborts Harmony.PatchAll() and skips every
    // later patch class (command registration, sprite replacer). Plugin.Awake
    // patches this manually after the resilient per-class loop, resolving the
    // parameterless overload via reflection.
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
                    string output;
                    try { output = ArgsParser.Execute(args); }
                    catch (Exception e)
                    {
                        output = $"[ChangeSkin] Command error: {e.Message}";
                        Plugin.Logger.LogError(e);
                    }
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
            Plugin.Logger.LogInfo("[ChangeSkin] 'skin' command registered via RegisterAllCommands patch");
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
                // Single-player has no lobby, so there is no host/admin role.
                // KrokoshaMP's CanExecuteAdminCommands() returns false in SP and
                // would lock out every skin command (load-local, enable, ...).
                // Skip the admin gate entirely when no MP local player exists.
                // (4.0.1 inits Steamworks in SP, so is_server is true even with
                //  no lobby; LOCAL_PLAYER == null is the real no-lobby signal.)
                bool isSinglePlayer = NetPlayer.LOCAL_PLAYER == null;

                if (!isSinglePlayer && !Con.CanExecuteAdminCommands())
                {
                    string subcommand = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    bool isAdminCommand = subcommand == "enable"
                        || subcommand == "disable"
                        || subcommand == "rule-set"
                        || subcommand == "rule-get"
                        || subcommand == "ban"
                        || subcommand == "unban";

                    if (isAdminCommand)
                    {
                        __instance.LogToConsole("Only the host or an admin can use that skin command.");
                        __instance.AddCommandToLogAndClearInput();
                        return false;
                    }

                    if (!ModConfig.Instance.SkinChangingEnabled)
                    {
                        __instance.LogToConsole("[ChangeSkin] Skin changing is currently disabled. An admin must enable it first.");
                        __instance.AddCommandToLogAndClearInput();
                        return false;
                    }
                }

                string output;
                try { output = ArgsParser.Execute(args); }
                catch (Exception e)
                {
                    output = $"[ChangeSkin] Command error: {e.Message}";
                    Plugin.Logger.LogError(e);
                }
                __instance.LogToConsole(output);
                __instance.AddCommandToLogAndClearInput();
                return false;
            }
            return true;
        }
    }
}