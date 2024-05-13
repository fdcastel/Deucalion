namespace Deucalion.Cli.Models;

internal record MonitorStateChangedDto(
    string N,
    DateTimeOffset At,
    MonitorState St
);
