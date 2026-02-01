namespace MapChooser.Models;

public class RtvConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnabledInWarmup { get; set; } = false;
    public bool NominationEnabled { get; set; } = true;
    public int MinPlayers { get; set; } = 0;
    public int MinRounds { get; set; } = 0;
    public bool ChangeMapImmediately { get; set; } = true;
    public int MapsToShow { get; set; } = 6;
    public int VoteDuration { get; set; } = 30;
    public int VotePercentage { get; set; } = 60;
    public int VoteCooldownTime { get; set; } = 300;
}

public class VotemapConfig
{
    public bool Enabled { get; set; } = true;
    public int VotePercentage { get; set; } = 60;
    public bool ChangeMapImmediately { get; set; } = true;
    public int MinPlayers { get; set; } = 0;
}

public class EndOfMapConfig
{
    public bool Enabled { get; set; } = true;
    public int MapsToShow { get; set; } = 6;
    public int VoteDuration { get; set; } = 30;
    public int TriggerSecondsBeforeEnd { get; set; } = 120;
    public int TriggerRoundsBeforeEnd { get; set; } = 4;
    public bool AllowExtend { get; set; } = true;
    public int ExtendTimeStep { get; set; } = 15;
    public int ExtendRoundStep { get; set; } = 5;
    public int ExtendLimit { get; set; } = 3;
}

public class ExtendMapConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnabledInWarmup { get; set; } = false;
    public int MinPlayers { get; set; } = 0;
    public int MinRounds { get; set; } = 0;
    public int VotePercentage { get; set; } = 60;
}

public class MapChooserConfig
{
    public RtvConfig Rtv { get; set; } = new();
    public VotemapConfig Votemap { get; set; } = new();
    public EndOfMapConfig EndOfMap { get; set; } = new();
    public ExtendMapConfig ExtendMap { get; set; } = new();
    public int MapsInCooldown { get; set; } = 3;
    public bool AllowSpectatorsToVote { get; set; } = false;
    public List<Map> Maps { get; set; } = new();
    public string SetNextMapPermission { get; set; } = "admin.changemap";
    public string MapsVotePermission { get; set; } = "admin.mapsvote";
}
