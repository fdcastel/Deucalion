namespace Deucalion.Cli.Models;

public record MonitorStateChangedDto(
    string N,
    DateTimeOffset At,
    MonitorState St
);
