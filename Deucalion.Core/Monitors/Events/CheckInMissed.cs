namespace Deucalion.Monitors.Events
{
    public record CheckInMissed(string Name, TimeSpan At) : MonitorEvent(Name, At);
}
