namespace Deucalion
{
    public record MonitorResponse(string Name, bool IsUp, TimeSpan ResponseTime);
}