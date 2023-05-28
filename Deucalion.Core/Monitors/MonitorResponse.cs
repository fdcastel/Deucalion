namespace Deucalion.Monitors;

public class MonitorResponse : EventArgs
{
    public MonitorState State { get; set; } = MonitorState.Unknown;
    public TimeSpan? ResponseTime { get; set; }
    public string? ResponseText { get; set; }

    public override string ToString()
    {
        var formattedResponseTime = ResponseTime is not null ? $", ResponseTime = {ResponseTime}" : string.Empty;
        var formattedResponseText = ResponseText is not null ? $", ResponseText = {ResponseText}" : string.Empty;
        return $"{GetType().Name} {{ State = {State}{formattedResponseTime}{formattedResponseText} }}";
    }

    public static MonitorResponse Down(TimeSpan? elapsed = null, string? text = null) => new()
    {
        State = MonitorState.Down,
        ResponseTime = elapsed,
        ResponseText = text
    };

    public static MonitorResponse Up(TimeSpan? elapsed = null, string? text = null) => new()
    {
        State = MonitorState.Up,
        ResponseTime = elapsed,
        ResponseText = text
    };
}
