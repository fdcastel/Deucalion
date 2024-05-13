using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorCheckInDto(
    MonitorState State,

    TimeSpan? ResponseTime,
    string? ResponseText,

    string? Secret
)
{
    internal MonitorResponse? ToMonitorResponse() =>
        new()
        {
            State = State,
            ResponseTime = ResponseTime,
            ResponseText = ResponseText
        };
}
