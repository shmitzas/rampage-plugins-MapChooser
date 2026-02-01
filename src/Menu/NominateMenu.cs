using MapChooser.Models;
using MapChooser.Dependencies;
using MapChooser.Helpers;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using System.Threading.Tasks;

namespace MapChooser.Menu;

public class NominateMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;

    public NominateMenu(ISwiftlyCore core, MapLister mapLister)
    {
        _core = core;
        _mapLister = mapLister;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onNominate)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var currentMapName = _core.ConVar.FindAsString("mapname")?.ValueAsString;
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.nominate.title"] ?? "Nominate a map:");
        foreach (var map in _mapLister.Maps)
        {
            if (!string.IsNullOrEmpty(currentMapName) && map.Name.Equals(currentMapName, StringComparison.OrdinalIgnoreCase)) continue;

            var option = new ButtonMenuOption($"<font color='lightgreen'>{map.Name}</font>");
            option.Click += (sender, args) =>
            {
                _core.Scheduler.NextTick(() => {
                    onNominate(args.Player, map.Name);
                    var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                    if (currentMenu != null)
                    {
                        _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                    }
                });
                return ValueTask.CompletedTask;
            };

            builder.AddOption(option);
        }

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
