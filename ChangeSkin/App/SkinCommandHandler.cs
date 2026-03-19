using ChangeSkin.Core;

namespace ChangeSkin.App;

internal sealed class SkinCommandHandler
{
    private readonly ChangeSkinService _service;

    public SkinCommandHandler(ChangeSkinService service)
    {
        _service = service;
    }

    public string Execute(string[] args)
    {
        string helpMessage =
            " skin load local {skinName}\n skin load remote {skinURL}\n skin rule set/get skinuploading true/false\n skin rule set/get skindownloading true/false\n skin ban/unban {playername}\n skin enable/disable\n skin reload\n skin clearcache\n skin verbose true/false\n skin unload";

        if (args == null || args.Length <= 1)
            return helpMessage;

        string command = args[1];

        try
        {
            if (command == "load" && args.Length == 4)
            {
                if (args[2] == "local")
                {
                    _service.LoadLocalForSelf(args[3]);
                    return $"Local skin {args[3]} loaded";
                }
                if (args[2] == "remote")
                {
                    return _service.LoadRemoteForSelf(args[3]);
                }
            }

            if (command == "rule")
            {
                if (args.Length == 5 && args[2] == "set")
                    return _service.SetRule(args[3], args[4]);

                if (args.Length == 4 && args[2] == "get")
                    return _service.GetRule(args[3]);
            }

            if (command == "ban" && args.Length == 3)
                return _service.BanByPlayerName(args[2]);

            if (command == "unban" && args.Length == 3)
                return _service.UnbanByPlayerName(args[2]);

            if (command == "enable")
            {
                _service.EnableAll();
                if (Core.Plugin.ModConfig.LastSelectedSkin == null)
                    return "Skin for self not selected \notherwise everything is ok";
                return "ChangeSkin enabled";
            }

            if (command == "disable")
            {
                _service.DisableAll();
                return "ChangeSkin disabled";
            }

            if (command == "reload")
            {
                _service.ReloadAll();
                return "ChangeSkin reloaded";
            }

            if (command == "unload")
            {
                _service.UnloadSelf();
                return "Self skin unloaded";
            }

            if (command == "clearcache")
                return _service.ClearCache();

            if (command == "verbose" && args.Length == 3)
                return _service.SetVerbose(args[2]);

            if (command == "init")
            {
                ChangeSkinMain.Destructor();
                ChangeSkinMain.Init();
                return "ChangeSkin initialized";
            }
        }
        finally
        {
            Core.Plugin.Instance.SaveConfig();
        }

        return helpMessage;
    }
}
