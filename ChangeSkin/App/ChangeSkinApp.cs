using ChangeSkin.Core;
using ChangeSkin.Network;
using ChangeSkin.Skin;

namespace ChangeSkin.App;

/// <summary>
/// Composition root that wires together all ChangeSkin services.
/// </summary>
internal sealed class ChangeSkinApp
{
    public ChangeSkinApp(
        PlayerRegistry registry,
        ISkinNetwork network,
        ISkinLoader skinLoader,
        ChangeSkinService service,
        SkinCommandHandler commandHandler
    )
    {
        Registry = registry;
        Network = network; // null in single-player
        SkinLoader = skinLoader;
        Service = service;
        CommandHandler = commandHandler;
    }

    public PlayerRegistry Registry { get; }
    public ISkinNetwork Network { get; }
    public ISkinLoader SkinLoader { get; }
    public ChangeSkinService Service { get; }
    public SkinCommandHandler CommandHandler { get; }
}
