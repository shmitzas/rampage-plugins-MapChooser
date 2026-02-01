using MapChooser.Models;
using MapChooser.Dependencies;
using MapChooser.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using MapChooser.Menu;

namespace MapChooser.Commands;

public class AdminMapsVoteCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapChooserConfig _config;

    public AdminMapsVoteCommand(ISwiftlyCore core, PluginState state, MapLister mapLister, EndOfMapVoteManager eofManager, MapChooserConfig config)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _eofManager = eofManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }

        var player = context.Sender!;
        var menu = new AdminMapsVoteMenu(_core, _mapLister);
        menu.Show(player, (p, maps) =>
        {
            _eofManager.StartCustomVote(maps, _config.EndOfMap.VoteDuration, true);
        });
    }
}
