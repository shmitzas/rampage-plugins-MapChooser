using MapChooser.Models;
using MapChooser.Dependencies;
using MapChooser.Helpers;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using System.Threading.Tasks;

using MapChooser.Menu;
namespace MapChooser.Commands;

public class NominateCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly MapChooserConfig _config;


    public NominateCommand(ISwiftlyCore core, PluginState state, MapLister mapLister, MapChooserConfig config)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!_config.Rtv.NominationEnabled) return;
        if (!context.IsSentByPlayer) return;
        
        var player = context.Sender!;

        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum <= 1)
        {
            var localizer = _core.Translation.GetPlayerLocalizer(player);
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.spectator"]);
            return;
        }
        string? mapName = context.Args.Length > 0 ? context.Args[0] : null;

        if (string.IsNullOrEmpty(mapName))
        {
            OpenNominationMenu(player);
        }
        else
        {
            HandleNomination(player, mapName);
        }
    }

    private void OpenNominationMenu(IPlayer player)
    {
        var menu = new NominateMenu(_core, _mapLister);
        menu.Show(player, HandleNomination);
    }

    private void HandleNomination(IPlayer player, string mapName)
    {
        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum <= 1)
        {
            var local = _core.Translation.GetPlayerLocalizer(player);
            player.SendChat(local["map_chooser.prefix"] + " " + local["map_chooser.general.validation.spectator"]);
            return;
        }
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var currentMapName = _core.ConVar.FindAsString("mapname")?.ValueAsString;
        var map = _mapLister.Maps.FirstOrDefault(m => m.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.nominate.not_found", mapName]);
            return;
        }

        if (!string.IsNullOrEmpty(currentMapName) && map.Name.Equals(currentMapName, StringComparison.OrdinalIgnoreCase))
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.nominate.current_map"]);
            return;
        }

        _state.Nominations[player.Slot] = map.Name;
        _core.PlayerManager.SendChat(localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.nominate.success", player.Controller?.PlayerName ?? "Unknown", map.Name]);
    }

    public List<string> GetNominations()
    {
        return _state.Nominations.Values.Distinct().ToList();
    }

    public void Clear()
    {
        _state.Nominations.Clear();
    }
}
