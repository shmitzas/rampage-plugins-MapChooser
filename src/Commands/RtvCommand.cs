using MapChooser.Models;
using MapChooser.Dependencies;
using MapChooser.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MapChooser.Commands;

public class RtvCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly VoteManager _voteManager;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapChooserConfig _config;

    public RtvCommand(ISwiftlyCore core, PluginState state, VoteManager voteManager, EndOfMapVoteManager eofManager, MapChooserConfig config)
    {
        _core = core;
        _state = state;
        _voteManager = voteManager;
        _eofManager = eofManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!_config.Rtv.Enabled) return;
        if (!context.IsSentByPlayer) return;

        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        
        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum <= 1)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.spectator"]);
            return;
        }

        if (_state.EofVoteHappening)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.already_voted"]);
            return;
        }

        if (_state.RtvCooldownEndTime.HasValue)
        {
            var remaining = (_state.RtvCooldownEndTime.Value - DateTime.Now).TotalSeconds;
            if (remaining > 0)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.cooldown", (int)remaining]);
                return;
            }
            else
            {
                _state.RtvCooldownEndTime = null;
            }
        }

        if (_voteManager.AddVote(player.Slot))
        {
            var allPlayers = _core.PlayerManager.GetAllPlayers()
                .Where(p => p.IsValid && !p.IsFakeClient && (_config.AllowSpectatorsToVote || p.Controller?.TeamNum > 1))
                .ToList();
            int totalPlayers = allPlayers.Count;
            int needed = _voteManager.GetRequiredVotes(totalPlayers);
            
            _core.PlayerManager.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.voted", player.Controller?.PlayerName ?? "Unknown", _voteManager.VoteCount, needed]);

            if (_voteManager.HasReached(totalPlayers))
            {
                // _voteManager.Clear(); // Don't clear votes immediately to allow for retraction
                _eofManager.StartVote(_config.Rtv.VoteDuration, _config.Rtv.MapsToShow, _config.Rtv.ChangeMapImmediately, isRtv: true);
            }
        }
        else
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.already_voted"]);
        }
    }
}
