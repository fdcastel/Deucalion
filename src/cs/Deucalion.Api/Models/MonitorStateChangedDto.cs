using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorStateChangedDto(
    string N,
    DateTimeOffset At,
    MonitorState St
);
