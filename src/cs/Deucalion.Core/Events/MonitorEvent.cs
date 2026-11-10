namespace Deucalion.Events;

/// <summary>
/// An event raised by a running monitor. Closed: every kind is declared in this assembly, so a
/// switch expression over it is exhaustive without a discard arm, and adding a kind fails the
/// build (CS8509) at each consumer until it is handled.
/// </summary>
public closed record MonitorEvent(string Name, DateTimeOffset At);
