using Deucalion.Application.Yaml;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Deucalion.Application.Configuration;

public class MonitorConfiguration
{
    public static class Messages
    {
        public const string ConfigurationMustNotBeEmpty = "Configuration file must not be empty.";
        public const string ConfigurationMustHaveMonitorsSection = "Configuration file must have a 'monitors' section.";

        public const string ConfigurationMonitorCannotBeEmpty = "Monitor '{0}' cannot be empty.";
    }

    public class DefaultConfiguration
    {
        public int? IgnoreFailCount { get; set; }
        public bool? UpsideDown { get; set; }

        public TimeSpan? IntervalWhenDown { get; set; }
        public TimeSpan? IntervalWhenUp { get; set; }

        public TimeSpan? IntervalToDown { get; set; }
    }

    public string? Version { get; set; }

    public Dictionary<string, MonitorBase> Monitors { get; set; } = default!;
    public DefaultConfiguration? Defaults { get; set; } = default!;

    public static MonitorConfiguration ReadFromFile(string configurationFile)
    {
        using var reader = new StreamReader(configurationFile);
        return ReadFromStream(reader);
    }

    public static MonitorConfiguration ReadFromStream(TextReader reader)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTagMapping("!checkin", typeof(CheckInMonitor))
            .WithTagMapping("!dns", typeof(DnsMonitor))
            .WithTagMapping("!http", typeof(HttpMonitor))
            .WithTagMapping("!ping", typeof(PingMonitor))
            .WithTagMapping("!tcp", typeof(TcpMonitor))
            .WithValidation()
            .Build();

        var result = deserializer.Deserialize<MonitorConfiguration>(reader) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

        if (result.Defaults is not null)
        {
            // Set monitor defaults
            MonitorBase.DefaultIgnoreFailCount = result.Defaults.IgnoreFailCount ?? MonitorBase.DefaultIgnoreFailCount;
            MonitorBase.DefaultUpsideDown = result.Defaults.UpsideDown ?? MonitorBase.DefaultUpsideDown;

            PullMonitor.DefaultIntervalWhenDown = result.Defaults.IntervalWhenDown ?? PullMonitor.DefaultIntervalWhenDown;
            PullMonitor.DefaultIntervalWhenUp = result.Defaults.IntervalWhenUp ?? PullMonitor.DefaultIntervalWhenUp;

            PushMonitor.DefaultIntervalToDown = result.Defaults.IntervalToDown ?? PushMonitor.DefaultIntervalToDown;
        }

        foreach (var d in result.Monitors)
        {
            if (d.Value is null)
            {
                // Monitor is empty
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorCannotBeEmpty, d.Key));
            }

            d.Value.Name = d.Key;
        }

        return result;
    }
}
