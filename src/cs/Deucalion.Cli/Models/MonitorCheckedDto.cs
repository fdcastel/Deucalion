namespace Deucalion.Cli.Models;

public record MonitorCheckedDto(
    string? N,
    long At,
    MonitorState St,
    int? Ms,
    string? Te
);
