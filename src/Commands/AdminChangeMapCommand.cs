using MapChooser.Dependencies;
using MapChooser.Helpers;
using SwiftlyS2.Shared.Commands;

namespace MapChooser.Commands;

public class AdminChangeMapCommand
{
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly ChangeMapManager _changeMapManager;

    public AdminChangeMapCommand(PluginState state, MapLister mapLister, ChangeMapManager changeMapManager)
    {
        _state = state;
        _mapLister = mapLister;
        _changeMapManager = changeMapManager;
    }

    public void Execute(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }

        var player = context.Sender!;
        var map = context.Args.Length > 0 ? context.Args[0] : null;
        if (string.IsNullOrEmpty(map))
        {
            context.Reply("Please specify a map name.");
            return;
        }

        var mapInfo = _mapLister.Maps.FirstOrDefault(m => m.Name.Contains(map, StringComparison.OrdinalIgnoreCase) || (m.Id != null && m.Id.Equals(map, StringComparison.OrdinalIgnoreCase)));
        if (mapInfo == null)
        {
            context.Reply($"Map \"{map}\" not found.");
            return;
        }

        _state.NextMap = mapInfo.Name;
        _changeMapManager.ChangeMap();
    }
}
