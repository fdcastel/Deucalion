namespace Deucalion.Monitors;

public class MonitorResponse : EventArgs
{
    public static MonitorResponse DefaultDown { get; } = new() { State = MonitorState.Down };
    public static MonitorResponse DefaultUp { get; } = new() { State = MonitorState.Up };

    public MonitorState State { get; set; } = MonitorState.Unknown;
    public TimeSpan? ResponseTime { get; set; }
}
