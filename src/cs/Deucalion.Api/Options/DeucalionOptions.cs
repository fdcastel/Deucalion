namespace Deucalion.Api.Options;

public sealed class DeucalionOptions
{
    // Server-only
    public string? ConfigurationFile { get; set; }
    public string? StoragePath { get; set; }
    public TimeSpan? CommitInterval { get; set; }

    // Client-only
    public string? PageTitle { get; set; }
}
