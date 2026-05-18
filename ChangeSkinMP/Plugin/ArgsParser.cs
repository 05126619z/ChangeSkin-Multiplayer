using System.Collections.Generic;
using System.Linq;
using CommandLine;
using CommandLine.Text;

namespace ChangeSkinMP;

public static class ArgsParser
{
    private static readonly Parser _parser = new Parser(config =>
    {
        config.HelpWriter = null; // отключаем автовывод
        config.CaseSensitive = false;
        config.IgnoreUnknownArguments = false;
    });

    public static string Execute(string[] args)
    {
        string result = null;

        args = args.Skip(1).ToArray();

        var parsed = _parser.ParseArguments<
            LoadLocalOptions,
            LoadRemoteOptions,
            RuleSetOptions,
            RuleGetOptions,
            BanOptions,
            UnbanOptions,
            EnableOptions,
            DisableOptions
        >(args);

        parsed.MapResult(
            (LoadLocalOptions o) => result = SkinManager.LoadLocal(o.SkinName),
            (LoadRemoteOptions o) => result = SkinManager.LoadRemote(o.Url),
            (RuleSetOptions o) => result = SkinManager.RuleSet(o.Rule, o.Value),
            (RuleGetOptions o) => result = SkinManager.RuleGet(o.Rule),
            (BanOptions o) => result = SkinManager.BanPlayer(o.Player),
            (UnbanOptions o) => result = SkinManager.UnbanPlayer(o.Player),
            (EnableOptions _) => result = SkinManager.EnableSkins(),
            (DisableOptions _) => result = SkinManager.DisableSkins(),
            errors => result = HandleErrors(errors)
        );

        if (result != null)
            Plugin.Logger.LogInfo(result);

        return result ?? "";
    }

    private static string HandleErrors(IEnumerable<Error> errors)
    {
        var list = errors.ToList();

        if (list.Any(e => e is HelpVerbRequestedError))
            return BuildHelp();

        if (list.Any(e => e is BadVerbSelectedError))
            return $"Unknown command. Available: load-local, load-remote, rule-set, rule-get, ban, unban, enable, disable";

        return string.Join("\n", list.Select(e => e.Tag.ToString()));
    }

    private static string BuildHelp() =>
        "skin load-local <name>\n"
        + "skin load-remote <url>\n"
        + "skin rule-set <rule> <value>\n"
        + "skin rule-get <rule>\n"
        + "skin ban <player>\n"
        + "skin unban <player>\n"
        + "skin enable\n"
        + "skin disable";
}

// ── Verbs ────────────────────────────────────────────────────────────────────

[Verb("load-local", HelpText = "Load a local skin by folder name")]
public class LoadLocalOptions
{
    [Value(0, Required = true, HelpText = "Skin folder name")]
    public string SkinName { get; set; }
}

[Verb("load-remote", HelpText = "Load a remote skin from URL")]
public class LoadRemoteOptions
{
    [Value(0, Required = true, HelpText = "URL of the skin")]
    public string Url { get; set; }
}

[Verb("rule-set", HelpText = "Set a rule value")]
public class RuleSetOptions
{
    [Value(0, Required = true, HelpText = "Rule name")]
    public RuleName Rule { get; set; }

    [Value(1, Required = true, HelpText = "Value (true/false)")]
    public bool Value { get; set; }
}

[Verb("rule-get", HelpText = "Get current value of a rule")]
public class RuleGetOptions
{
    [Value(0, Required = true, HelpText = "Rule name")]
    public RuleName Rule { get; set; }
}

[Verb("ban", HelpText = "Skinban a player")]
public class BanOptions
{
    [Value(0, Required = true, HelpText = "Player name")]
    public string Player { get; set; }
}

[Verb("unban", HelpText = "Remove skinban from a player")]
public class UnbanOptions
{
    [Value(0, Required = true, HelpText = "Player name")]
    public string Player { get; set; }
}

public enum RuleName
{
    SkinUploading,
    SkinDownloading,
}

[Verb("enable", HelpText = "Enable skin replacement")]
public class EnableOptions { }

[Verb("disable", HelpText = "Disable skin replacement")]
public class DisableOptions { }
