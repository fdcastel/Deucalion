namespace Deucalion.Monitors
{
    public interface IMonitor
    {
        Task<bool> IsUpAsync();
    }
}
