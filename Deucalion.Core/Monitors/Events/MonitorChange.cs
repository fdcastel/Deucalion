namespace Deucalion.Monitors.Events
{
    public record MonitorChange(string Name, TimeSpan At, MonitorState NewState) : MonitorEvent(Name, At);
}
