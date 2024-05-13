namespace Deucalion.Cli.Models;

internal record MonitorCheckedDto(
    string? N,
    long At,
    MonitorState St,
    int? Ms,
    string? Te
);
