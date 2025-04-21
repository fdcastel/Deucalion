namespace Deucalion.Api.Options;

public sealed class DeucalionOptions
{
    // Server-only
    public string? ConfigurationFile { get; set; }
    public string? StoragePath { get; set; }

    public TimeSpan EventRetentionPeriod { get; set; } = TimeSpan.FromDays(30); // Default to 30 days
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(24); // Default to once a day

    // Client-only
    public string? PageTitle { get; set; }
    public string? PageDescription { get; set; }
}
