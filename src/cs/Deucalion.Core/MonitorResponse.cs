﻿namespace Deucalion;

public record MonitorResponse(
    MonitorState State = MonitorState.Unknown,
    TimeSpan? ResponseTime = null,
    string? ResponseText = null
)
{
    public override string ToString()
    {
        var formattedResponseTime = ResponseTime is not null ? $", ResponseTime = {ResponseTime}" : string.Empty;
        var formattedResponseText = ResponseText is not null ? $", ResponseText = {ResponseText}" : string.Empty;
        return $"{GetType().Name} {{ State = {State}{formattedResponseTime}{formattedResponseText} }}";
    }

    public static MonitorResponse Down(TimeSpan? elapsed = null, string? text = null) =>
        new(
            State: MonitorState.Down,
            ResponseTime: elapsed,
            ResponseText: text
        );

    public static MonitorResponse Up(TimeSpan? elapsed = null, string? text = null, TimeSpan? warnElapsed = null) =>
        new(
            State: warnElapsed is null
                ? MonitorState.Up
                : elapsed > warnElapsed
                    ? MonitorState.Warn
                    : MonitorState.Up,
            ResponseTime: elapsed,
            ResponseText: text
        );

    public static MonitorResponse Warn(TimeSpan? elapsed = null, string? text = null) =>
        new(
            State: MonitorState.Warn,
            ResponseTime: elapsed,
            ResponseText: text
        );
}

public class MonitorResponseEventArgs(MonitorResponse? response) : EventArgs
{
    public MonitorResponse? Response { get; set; } = response;
}
