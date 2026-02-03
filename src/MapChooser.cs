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

[PluginMetadata(Id = "MapChooser", Version = "0.0.7-beta", Name = "Map Chooser", Author = "aga", Description = "Map chooser plugin for SwiftlyS2")]
public sealed class MapChooser : BasePlugin {
    private MapChooserConfig _config = new();
    private PluginState _state = new();
    private MapLister _mapLister = new();
    private MapCooldown _mapCooldown = null!;
    private ChangeMapManager _changeMapManager = null!;
    private VoteManager _rtvVoteManager = null!;
    private VoteManager _extVoteManager = null!;
    private EndOfMapVoteManager _eofManager = null!;
    private ExtendManager _extendManager = null!;
    
    private RtvCommand _rtvCmd = null!;
    private UnRtvCommand _unRtvCmd = null!;
    private NominateCommand _nominateCmd = null!;
    private TimeleftCommand _timeleftCmd = null!;
    private NextmapCommand _nextmapCmd = null!;
    private VotemapCommand _votemapCmd = null!;
    private RevoteCommand _revoteCmd = null!;
    private SetNextMapCommand _setNextMapCmd = null!;
    private ExtendCommand _extendCmd = null!;
    private AdminMapsVoteCommand _adminMapsVoteCmd = null!;

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
        
        _mapCooldown = new MapCooldown(Core, _config);
        _changeMapManager = new ChangeMapManager(Core, _state, _mapLister, _config);
        _rtvVoteManager = new VoteManager();
        _extVoteManager = new VoteManager();
        _extendManager = new ExtendManager(Core, _state, _config);
        _eofManager = new EndOfMapVoteManager(Core, _state, _rtvVoteManager, _mapLister, _mapCooldown, _changeMapManager, _extendManager, _config);

        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.RoundsPlayed = 0;
        var warmupConVar = Core.ConVar.Find<int>("mp_warmup_period");
        _state.WarmupRunning = warmupConVar?.Value == 1;

        _rtvCmd = new RtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _unRtvCmd = new UnRtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _nominateCmd = new NominateCommand(Core, _state, _mapLister, _mapCooldown, _config);
        _timeleftCmd = new TimeleftCommand(Core, _state, _config);
        _nextmapCmd = new NextmapCommand(Core, _state);
        _votemapCmd = new VotemapCommand(Core, _state, _mapLister, _mapCooldown, _changeMapManager, _config);
        _revoteCmd = new RevoteCommand(Core, _state, _eofManager, _config);
        _setNextMapCmd = new SetNextMapCommand(Core, _state, _mapLister, _changeMapManager);
        _extendCmd = new ExtendCommand(Core, _state, _extVoteManager, _extendManager, _config);
        _adminMapsVoteCmd = new AdminMapsVoteCommand(Core, _state, _mapLister, _eofManager, _config);

        Core.Command.RegisterCommand("rtv", _rtvCmd.Execute);
        Core.Command.RegisterCommand("unrtv", _unRtvCmd.Execute);
        Core.Command.RegisterCommand("nominate", _nominateCmd.Execute);
        Core.Command.RegisterCommand("timeleft", _timeleftCmd.Execute);
        Core.Command.RegisterCommand("nextmap", _nextmapCmd.Execute);
        Core.Command.RegisterCommand("votemap", _votemapCmd.Execute);
        Core.Command.RegisterCommand("revote", _revoteCmd.Execute);
        Core.Command.RegisterCommand("setnextmap", _setNextMapCmd.Execute, permission: _config.SetNextMapPermission);
        Core.Command.RegisterCommand("ext", _extendCmd.Execute);
        Core.Command.RegisterCommand("extendmap", _extendCmd.Execute);
        Core.Command.RegisterCommand("mapsvote", _adminMapsVoteCmd.Execute, permission: _config.MapsVotePermission);

        Core.GameEvent.HookPost<EventRoundEnd>(OnRoundEnd);
        Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPost<EventRoundAnnounceWarmup>(OnAnnounceWarmup);
        Core.GameEvent.HookPost<EventWarmupEnd>(OnWarmupEnd);
        Core.GameEvent.HookPost<EventCsWinPanelMatch>(OnWinPanelMatch);
        Core.GameEvent.HookPost<EventRoundAnnounceMatchStart>(OnMatchStart);
        Core.GameEvent.HookPost<EventRoundAnnounceMatchPoint>(OnMatchPoint);
        Core.Event.OnMapLoad += OnMapLoad;

        Core.Scheduler.DelayAndRepeat(1000, 1000, () =>
        {
            CheckAutomatedVote();
        });
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _eofManager?.ResetVote();
        _state.MapChangeScheduled = false;
        _state.EofVoteHappening = false;
        _state.NextMap = null;
        _state.RoundsPlayed = 0;
        try {
            _state.MapStartTime = Core.Engine is { } e ? e.GlobalVars.CurrentTime : 0;
        } catch {
            _state.MapStartTime = 0;
        }
        
        _state.RtvCooldownEndTime = null;
        
        _rtvVoteManager?.Clear();
        _extVoteManager?.Clear();
        _nominateCmd?.Clear();
        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.MatchEnded = false;
        
        _mapCooldown.OnMapStart(@event.MapName, Core.Engine.WorkshopId);
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
        try {
            _state.MapStartTime = Core.Engine is { } e ? e.GlobalVars.CurrentTime : 0;
        } catch {
            _state.MapStartTime = 0;
        }
        return HookResult.Continue;
    }

    private HookResult OnMatchStart(EventRoundAnnounceMatchStart @event)
    {
        _eofManager?.ResetVote();
        _state.RoundsPlayed = 0;
        try {
            _state.MapStartTime = Core.Engine is { } e ? e.GlobalVars.CurrentTime : 0;
        } catch {
            _state.MapStartTime = 0;
        }
        _state.WarmupRunning = false;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.MapChangeScheduled = false;
        _state.EofVoteHappening = false;
        _state.NextMap = null;
        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        
        _rtvVoteManager?.Clear();
        _extVoteManager?.Clear();
        _nominateCmd?.Clear();
        return HookResult.Continue;
    }

    private HookResult OnMatchPoint(EventRoundAnnounceMatchPoint @event)
    {
        CheckAutomatedVote(true);
        return HookResult.Continue;
    }

    private HookResult OnWinPanelMatch(EventCsWinPanelMatch @event)
    {
        _state.MatchEnded = true;
        if (_state.EofVoteHappening)
        {
            // Vote is still going when match ends.
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
        if (_state.MapChangeScheduled && !_state.EofVoteHappening && !_state.ChangeMapImmediately && _state.IsRtv)
        {
            _changeMapManager.ChangeMap();
        }
        else
        {
            CheckAutomatedVote();
        }

        return HookResult.Continue;
    }

    private void CheckAutomatedVote(bool force = false)
    {
        if (!_config.EndOfMap.Enabled || _state.EofVoteHappening || _state.MapChangeScheduled || _state.WarmupRunning) return;

        // Sync rounds played with engine for better accuracy
        var gameRules = Core.EntitySystem.GetGameRules();
        if (gameRules != null)
        {
            _state.RoundsPlayed = gameRules.TotalRoundsPlayed;
        }

        if (!force)
        {
            if (_state.RoundsPlayed < _state.NextEofVotePossibleRound) return;
            if (Core.Engine != null && Core.Engine.GlobalVars.CurrentTime < _state.NextEofVotePossibleTime) return;
        }

        var timelimitConVar = Core.ConVar.Find<float>("mp_timelimit");
        var maxroundsConVar = Core.ConVar.Find<int>("mp_maxrounds");
        var winlimitConVar = Core.ConVar.Find<int>("mp_winlimit");
        
        float timelimit = timelimitConVar?.Value ?? 0;
        int maxrounds = maxroundsConVar?.Value ?? 0;
        int winlimit = winlimitConVar?.Value ?? 0;

        bool trigger = false;

        if (timelimit > 0 && Core.Engine != null)
        {
            if (_state.MapStartTime <= 0)
            {
                _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
            }
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
            _state.NextEofVotePossibleRound = _state.RoundsPlayed + 1;
            if (Core.Engine != null)
                _state.NextEofVotePossibleTime = Core.Engine.GlobalVars.CurrentTime + _config.EndOfMap.VoteDuration + 1;
            _eofManager.StartVote(_config.EndOfMap.VoteDuration, _config.EndOfMap.MapsToShow);
        }
    }

    public override void Unload() {
    }
}