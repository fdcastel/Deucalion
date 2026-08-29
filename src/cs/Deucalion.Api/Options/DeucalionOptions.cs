using Deucalion.Application.Configuration;

namespace Deucalion.Api.Options;

public sealed class DeucalionOptions
{
    /// <summary>The configuration section these options are bound from (<c>DEUCALION__*</c> as environment variables).</summary>
    public const string SectionName = "Deucalion";

    // Server-only
    public string? ConfigurationFile { get; set; }
    public string? StoragePath { get; set; }

    public TimeSpan EventRetentionPeriod { get; set; } = TimeSpan.FromDays(30); // Default to 30 days
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(24); // Default to once a day

    /// <summary>
    /// Newest events kept per monitor; older ones are deleted by the purge even if still within
    /// <see cref="EventRetentionPeriod"/>. Bounds the database size: the UI only ever reads the
    /// last 120 events and computes stats over the last 60, so nothing is lost by capping.
    /// </summary>
    public int MaxEventsPerMonitor { get; set; } = 100_000;

    // Client-only
    public string? PageTitle { get; set; }

    /// <summary>
    /// Fails fast on values that would otherwise take the host down later with an opaque error --
    /// e.g. <c>DEUCALION__PURGEINTERVAL=00:00:00</c> made <c>PeriodicTimer</c> throw inside the
    /// purge service, and the default <c>BackgroundServiceExceptionBehavior.StopHost</c> then
    /// stopped the whole application.
    /// </summary>
    /// <exception cref="ConfigurationErrorException">A value is out of range; the message names the option.</exception>
    public void Validate()
    {
        RequirePositive(nameof(PurgeInterval), PurgeInterval);
        RequirePositive(nameof(EventRetentionPeriod), EventRetentionPeriod);

        if (MaxEventsPerMonitor <= 0)
        {
            throw Invalid(nameof(MaxEventsPerMonitor), MaxEventsPerMonitor);
        }
    }

    private static void RequirePositive(string name, TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            throw Invalid(name, value);
        }
    }

    private static ConfigurationErrorException Invalid(string name, object value) =>
        new($"Option '{SectionName}:{name}' must be a positive value, but was '{value}'.");
}
