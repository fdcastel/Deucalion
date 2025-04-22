namespace Deucalion.Cli.Models;

// Duplicated from Deucalion.Core to avoid dependency. Keep in sync with Deucalion.Core.MonitorState.
public enum MonitorState
{
    Unknown = 0,
    Up = 1,
    Down = 2,
    Warn = 3,
    Degraded = 4,
}
