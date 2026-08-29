namespace Deucalion.Api.Models;

/// <param name="Events">Recent events, columnar (see <see cref="MonitorEventsDto"/>); omitted when there are none.</param>
internal record MonitorDto(
    string Name,
    MonitorConfigurationDto Config,
    MonitorStatsDto? Stats,
    MonitorEventsDto? Events
);
