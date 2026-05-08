using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using Clap.Net;

namespace ChangeSkinMP;

using Clap.Net;

#nullable enable
// Корневая команда "skin"
[Command(Name = "skin", About = "Manage player skins")]
public partial class SkinCommand
{
    [Command]
    public SkinSubCommands? Command { get; init; }
}

// ── Первый уровень подкоманд ─────────────────────────────────────────────────

[SubCommand]
public partial class SkinSubCommands
{
    [Command(About = "Load a skin (local or remote)")]
    public partial class Load : SkinSubCommands
    {
        [Command]
        public LoadSubCommands? Command { get; init; }
    }

    [Command(About = "Manage server skin rules")]
    public partial class Rule : SkinSubCommands
    {
        [Command]
        public RuleSubCommands? Command { get; init; }
    }

    [Command(About = "Skinban a player")]
    public partial class Ban : SkinSubCommands
    {
        [Arg(Help = "Player name")]
        public required string Player { get; init; }
    }

    [Command(About = "Remove skinban from a player")]
    public partial class Unban : SkinSubCommands
    {
        [Arg(Help = "Player name")]
        public required string Player { get; init; }
    }

    [Command(About = "Enable skin replacement")]
    public partial class Enable : SkinSubCommands { }

    [Command(About = "Disable skin replacement")]
    public partial class Disable : SkinSubCommands { }

    [Command(About = "Reload all skins")]
    public partial class Reload : SkinSubCommands { }

    [Command(About = "Unload your own skin")]
    public partial class Unload : SkinSubCommands { }

    [Command(Name = "clearcache", About = "Clear the skin download cache")]
    public partial class ClearCache : SkinSubCommands { }

    [Command(About = "Toggle verbose logging")]
    public partial class Verbose : SkinSubCommands
    {
        [Arg(Help = "true or false")]
        public required bool Enabled { get; init; }
    }
}

// ── skin load local|remote ───────────────────────────────────────────────────

[SubCommand]
public partial class LoadSubCommands
{
    [Command(About = "Load a local skin by folder name")]
    public partial class Local : LoadSubCommands
    {
        [Arg(Help = "Skin folder name")]
        public required string SkinName { get; init; }
    }

    [Command(About = "Load a remote skin from URL")]
    public partial class Remote : LoadSubCommands
    {
        [Arg(Help = "URL of the skin")]
        public required string Url { get; init; }
    }
}

// ── skin rule set|get ────────────────────────────────────────────────────────

public enum RuleName
{
    SkinUploading,
    SkinDownloading,
}

[SubCommand]
public partial class RuleSubCommands
{
    [Command(About = "Set a rule value")]
    public partial class Set : RuleSubCommands
    {
        [Arg(Help = "Rule name")]
        public required RuleName Rule { get; init; }

        [Arg(Help = "Value (true/false)")]
        public required bool Value { get; init; }
    }

    [Command(About = "Get current value of a rule")]
    public partial class Get : RuleSubCommands
    {
        [Arg(Help = "Rule name")]
        public required RuleName Rule { get; init; }
    }
}
#nullable disable
