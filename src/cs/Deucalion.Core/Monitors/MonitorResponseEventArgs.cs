namespace Deucalion.Monitors;

public class MonitorResponseEventArgs : EventArgs
{
    public MonitorResponse? Response { get; set; }

    public MonitorResponseEventArgs(MonitorResponse? response)
    {
        Response = response;
    }
}
