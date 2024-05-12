namespace Deucalion.Storage;

public record MonitorSummary(
    DateTimeOffset? LastDown,
    DateTimeOffset? LastUp
);
