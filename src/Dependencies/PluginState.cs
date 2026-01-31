using MapChooser.Models;

namespace MapChooser.Dependencies;

public class PluginState
{
    public bool MapChangeScheduled { get; set; }
    public bool EofVoteHappening { get; set; }
    public bool CommandsDisabled { get; set; }
    public string? NextMap { get; set; }
    public bool ChangeMapImmediately { get; set; }
    public float MapStartTime { get; set; }
    public int RoundsPlayed { get; set; }
    public bool WarmupRunning { get; set; }
    public DateTime? RtvCooldownEndTime { get; set; }
}
