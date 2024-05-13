namespace Deucalion.Api.Models;

internal record MonitorDto(
    string Name,
    MonitorConfigurationDto Config,
    MonitorStatsDto? Stats,
    IEnumerable<MonitorEventDto> Events
);
