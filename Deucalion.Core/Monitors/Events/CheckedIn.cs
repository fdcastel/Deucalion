namespace Deucalion.Monitors.Events
{
    public record CheckedIn(string Name, TimeSpan At) : MonitorEvent(Name, At);
}
