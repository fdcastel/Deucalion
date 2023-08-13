using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorChangedDto(
    string N,
    DateTimeOffset At,
    MonitorState St
);
