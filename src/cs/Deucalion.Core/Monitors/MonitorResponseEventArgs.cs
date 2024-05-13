namespace Deucalion.Monitors;

public class MonitorResponseEventArgs(MonitorResponse? response) : EventArgs
{
    public MonitorResponse? Response { get; set; } = response;
}
