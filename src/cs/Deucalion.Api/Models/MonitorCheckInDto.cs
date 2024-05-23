namespace Deucalion.Api.Models;

internal record MonitorCheckInDto(
    MonitorState State,

    TimeSpan? ResponseTime,
    string? ResponseText,

    string? Secret
)
{
    internal MonitorResponse? ToMonitorResponse() =>
        new(
            State: State,
            ResponseTime: ResponseTime,
            ResponseText: ResponseText
        );
}
