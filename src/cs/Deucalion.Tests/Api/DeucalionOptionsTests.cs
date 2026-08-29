using Deucalion.Api;
using Deucalion.Api.Options;
using Deucalion.Application.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Deucalion.Tests.Api;

/// <summary>
/// Regression for #23 (1): <c>DEUCALION__PURGEINTERVAL=00:00:00</c> used to bind silently and
/// then kill the host from inside <c>PurgeBackgroundService</c> (<c>PeriodicTimer</c> rejects a
/// non-positive period) with an opaque message. The options are now validated when bound.
/// </summary>
public class DeucalionOptionsTests
{
    [Theory]
    [InlineData("PurgeInterval", "00:00:00")]
    [InlineData("PurgeInterval", "-00:00:01")]
    [InlineData("EventRetentionPeriod", "00:00:00")]
    [InlineData("MaxEventsPerMonitor", "0")]
    [InlineData("MaxEventsPerMonitor", "-1")]
    public void Issue23_ConfigureApplicationBuilder_InvalidOption_FailsFastNamingTheOption(string option, string value)
    {
        var builder = WebApplication.CreateBuilder();

        // In-memory values are added last, so they win over any Deucalion__* environment variable.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DeucalionOptions.SectionName}:{option}"] = value,
        });

        var exception = Assert.Throws<ConfigurationErrorException>(() => builder.ConfigureApplicationBuilder());

        Assert.Contains(option, exception.Message, StringComparison.Ordinal);
        Assert.Contains(value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DefaultsArePositive()
    {
        new DeucalionOptions().Validate();
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-01:00:00")]
    public void Validate_NonPositivePurgeInterval_Throws(string purgeInterval)
    {
        var options = new DeucalionOptions { PurgeInterval = TimeSpan.Parse(purgeInterval) };

        var exception = Assert.Throws<ConfigurationErrorException>(options.Validate);

        Assert.Contains("Deucalion:PurgeInterval", exception.Message, StringComparison.Ordinal);
    }
}
