using Deucalion.Application.Yaml;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Deucalion.Application.Configuration;

public class ApplicationConfiguration
{
    public static class Messages
    {
        public const string ConfigurationMustNotBeEmpty = "Configuration file must not be empty.";
        public const string ConfigurationMustHaveMonitorsSection = "Configuration file must have a 'monitors' section.";

        public const string ConfigurationMonitorCannotBeEmpty = "Monitor '{0}' cannot be empty.";
    }

    public string? Version { get; set; }

    public DatabaseConfiguration Database { get; set; } = default!;

    public Dictionary<string, MonitorBase> Monitors { get; set; } = default!;

    public static ApplicationConfiguration ReadFromFile(string configurationFile)
    {
        using var reader = new StreamReader(configurationFile);
        return ReadFromStream(reader);
    }

    public static ApplicationConfiguration ReadFromStream(TextReader reader)
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

        var result = deserializer.Deserialize<ApplicationConfiguration>(reader) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

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
