namespace Deucalion.Api.Models;

internal record InitDto(
    PageConfigurationDto Configuration,
    MonitorDto[] Monitors
);
