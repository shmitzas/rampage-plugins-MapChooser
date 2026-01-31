using MapChooser.Models;
using MapChooser.Dependencies;
using MapChooser.Helpers;
using MapChooser.Commands;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace MapChooser;

[PluginMetadata(Id = "MapChooser", Version = "0.0.1-beta", Name = "Map Chooser", Author = "abnerfs, Oz-Lin (Ported by Cascade)", Description = "Port of cs2-rockthevote to SwiftlyS2")]
public sealed class MapChooser : BasePlugin {
    private MapChooserConfig _config = new();
    private PluginState _state = new();
    private MapLister _mapLister = new();
    private MapCooldown _mapCooldown = null!;
    private ChangeMapManager _changeMapManager = null!;
    private VoteManager _rtvVoteManager = null!;
    private EndOfMapVoteManager _eofManager = null!;
    
    private RtvCommand _rtvCmd = null!;
    private UnRtvCommand _unRtvCmd = null!;
    private NominateCommand _nominateCmd = null!;
    private TimeleftCommand _timeleftCmd = null!;
    private NextmapCommand _nextmapCmd = null!;
    private VotemapCommand _votemapCmd = null!;
    private RevoteCommand _revoteCmd = null!;
    private SetNextMapCommand _setNextMapCmd = null!;

    public MapChooser(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void Load(bool hotReload) {
        Core.Configuration
            .InitializeJsonWithModel<MapChooserConfig>("config.jsonc", "MapChooser")
            .Configure(builder => {
                builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
            });

        _config = Core.Configuration.Manager.GetSection("MapChooser").Get<MapChooserConfig>() ?? new MapChooserConfig();
        _mapLister.UpdateMaps(_config.Maps);
        
        _mapCooldown = new MapCooldown(_config);
        _changeMapManager = new ChangeMapManager(Core, _state, _mapLister);
        _rtvVoteManager = new VoteManager();
        _eofManager = new EndOfMapVoteManager(Core, _state, _rtvVoteManager, _mapLister, _mapCooldown, _changeMapManager, _config);

        _rtvCmd = new RtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _unRtvCmd = new UnRtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _nominateCmd = new NominateCommand(Core, _state, _mapLister, _config);
        _timeleftCmd = new TimeleftCommand(Core, _state, _config);
        _nextmapCmd = new NextmapCommand(Core, _state);
        _votemapCmd = new VotemapCommand(Core, _state, _mapLister, _mapCooldown, _changeMapManager, _config);
        _revoteCmd = new RevoteCommand(Core, _state, _eofManager);
        _setNextMapCmd = new SetNextMapCommand(Core, _state, _mapLister, _changeMapManager);

        Core.Command.RegisterCommand("rtv", _rtvCmd.Execute);
        Core.Command.RegisterCommand("unrtv", _unRtvCmd.Execute);
        Core.Command.RegisterCommand("nominate", _nominateCmd.Execute);
        Core.Command.RegisterCommand("timeleft", _timeleftCmd.Execute);
        Core.Command.RegisterCommand("nextmap", _nextmapCmd.Execute);
        Core.Command.RegisterCommand("votemap", _votemapCmd.Execute);
        Core.Command.RegisterCommand("revote", _revoteCmd.Execute);
        Core.Command.RegisterCommand("setnextmap", _setNextMapCmd.Execute, permission: "@css/changemap");

        Core.GameEvent.HookPost<EventRoundEnd>(OnRoundEnd);
        Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPost<EventRoundAnnounceWarmup>(OnAnnounceWarmup);
        Core.GameEvent.HookPost<EventWarmupEnd>(OnWarmupEnd);
        Core.GameEvent.HookPost<EventCsWinPanelMatch>(OnWinPanelMatch);
        Core.GameEvent.HookPost<EventRoundAnnounceMatchStart>(OnMatchStart);
        Core.Event.OnMapLoad += OnMapLoad;

        Core.Scheduler.DelayAndRepeat(1000, 1000, () =>
        {
            if (_state.WarmupRunning || _state.EofVoteHappening || _state.MapChangeScheduled) return;

            var timelimitConVar = Core.ConVar.Find<float>("mp_timelimit");
            float timelimit = timelimitConVar?.Value ?? 0;

            if (timelimit > 0 && Core.Engine?.GlobalVars != null)
            {
                float timePlayed = Core.Engine.GlobalVars.CurrentTime - _state.MapStartTime;
                float timeRemaining = (timelimit * 60) - timePlayed;
                if (timeRemaining <= _config.EndOfMap.TriggerSecondsBeforeEnd)
                {
                    _eofManager.StartVote(_config.EndOfMap.VoteDuration, _config.EndOfMap.MapsToShow);
                }
            }
        });
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _state.MapChangeScheduled = false;
        _state.EofVoteHappening = false;
        _state.NextMap = null;
        _state.RoundsPlayed = 0;
        _state.MapStartTime = 0; // Initialize to 0, will be updated on MatchStart/WarmupEnd
        _state.RtvCooldownEndTime = null;
        
        _rtvVoteManager?.Clear();
        _nominateCmd?.Clear();
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        var warmupConVar = Core.ConVar.Find<int>("mp_warmup_period");
        _state.WarmupRunning = warmupConVar?.Value == 1;
        CheckAutomatedVote();
        return HookResult.Continue;
    }

    private HookResult OnAnnounceWarmup(EventRoundAnnounceWarmup @event)
    {
        _state.WarmupRunning = true;
        return HookResult.Continue;
    }

    private HookResult OnWarmupEnd(EventWarmupEnd @event)
    {
        _state.WarmupRunning = false;
        _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
        return HookResult.Continue;
    }

    private HookResult OnMatchStart(EventRoundAnnounceMatchStart @event)
    {
        _state.RoundsPlayed = 0;
        _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
        _state.WarmupRunning = false;
        return HookResult.Continue;
    }

    private HookResult OnWinPanelMatch(EventCsWinPanelMatch @event)
    {
        if (_state.EofVoteHappening)
        {
            // If vote is still going when match ends, we should probably force end it
            // or ensure the winner is set.
        }
        else if (_state.MapChangeScheduled)
        {
            _changeMapManager.ChangeMap();
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _state.RoundsPlayed++;
        if (_state.MapChangeScheduled && !_state.EofVoteHappening)
        {
           // Do nothing, wait for match end or specific trigger
        }
        else
        {
            CheckAutomatedVote();
        }

        return HookResult.Continue;
    }

    private void CheckAutomatedVote()
    {
        if (!_config.EndOfMap.Enabled || _state.EofVoteHappening || _state.MapChangeScheduled || _state.WarmupRunning) return;

        var timelimitConVar = Core.ConVar.Find<float>("mp_timelimit");
        var maxroundsConVar = Core.ConVar.Find<int>("mp_maxrounds");
        var winlimitConVar = Core.ConVar.Find<int>("mp_winlimit");
        
        float timelimit = timelimitConVar?.Value ?? 0;
        int maxrounds = maxroundsConVar?.Value ?? 0;
        int winlimit = winlimitConVar?.Value ?? 0;

        bool trigger = false;

        if (timelimit > 0 && Core.Engine?.GlobalVars != null)
        {
            float timePlayed = Core.Engine.GlobalVars.CurrentTime - _state.MapStartTime;
            float timeRemaining = (timelimit * 60) - timePlayed;
            if (timeRemaining <= _config.EndOfMap.TriggerSecondsBeforeEnd)
            {
                trigger = true;
            }
        }

        if (!trigger && maxrounds > 0)
        {
            int roundsRemaining = maxrounds - _state.RoundsPlayed;
            if (roundsRemaining <= _config.EndOfMap.TriggerRoundsBeforeEnd)
            {
                trigger = true;
            }
        }

        // New Logic: Check score proximity to winning (Match Point)
        if (!trigger)
        {
            int winningScore = 0;
            if (winlimit > 0)
            {
                winningScore = winlimit;
            }
            else if (maxrounds > 0)
            {
                // In MR12 (24 rounds), winning score is 13.
                // In MR15 (30 rounds), winning score is 16.
                winningScore = (maxrounds / 2) + 1;
            }

            if (winningScore > 0)
            {
                var teams = Core.EntitySystem.GetAllEntitiesByClass<CCSTeam>();
                int maxTeamScore = 0;
                foreach (var team in teams)
                {
                   // Calculate total score from halves and overtime
                   int score = team.ScoreFirstHalf + team.ScoreSecondHalf + team.ScoreOvertime;
                   if (score > maxTeamScore) maxTeamScore = score;
                }
                
                if (winningScore - maxTeamScore <= _config.EndOfMap.TriggerRoundsBeforeEnd)
                {
                    trigger = true;
                }
            }
        }

        if (trigger)
        {
            _eofManager.StartVote(_config.EndOfMap.VoteDuration, _config.EndOfMap.MapsToShow);
        }
    }

    public override void Unload() {
    }
}