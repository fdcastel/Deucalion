using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Deucalion.Application.Configuration;

public class ApplicationConfiguration
{
    public static class Messages
    {
        public const string ConfigurationMustNotBeEmpty = "Configuration file must not be empty.";
        public const string ConfigurationMustHaveMonitorsSection = "Configuration file must have a 'Monitors' section.";

        public const string ConfigurationMonitorMustHaveClass = "Monitor '{0}' must have a 'class' key.";
        public const string ConfigurationMonitorClassNotFound = "Monitor '{0}' references class '{1}' which is unknown.";

        public const string ConfigurationMonitorMustHaveHost = "Monitor '{0}' must have a 'host' key.";
        public const string ConfigurationMonitorMustHaveUrl = "Monitor '{0}' must have a 'url' key.";
        public const string ConfigurationMonitorMustHavePort = "Monitor '{0}' must have a 'port' key.";
    }

    public string? Version { get; set; }

    public Dictionary<string, MonitorConfiguration> Monitors { get; set; } = default!;

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
            .WithAttributeOverride<MonitorConfiguration>(
                d => d.ClassName!,
                new YamlMemberAttribute { Alias = "class" }
            )
            .Build();

        var result = deserializer.Deserialize<ApplicationConfiguration>(reader) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

        foreach (var d in result.Monitors)
        {
            if (d.Value is null)
            {
                // Monitor is empty
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveClass, d.Key));
            }

            d.Value.MonitorName = d.Key;
            d.Value.ClassName = d.Value.ClassName ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveClass, d.Key));

            switch (d.Value.ClassName)
            {
                case "dns":
                    d.Value.Host = d.Value.Host ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveHost, d.Key));
                    break;

                case "http":
                    d.Value.Url = d.Value.Url ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveUrl, d.Key));
                    break;

                case "ping":
                    d.Value.Host = d.Value.Host ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveHost, d.Key));
                    break;

                case "tcp":
                    d.Value.Host = d.Value.Host ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHaveHost, d.Key));
                    d.Value.Port = d.Value.Port ?? throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorMustHavePort, d.Key));
                    break;

                default:
                    throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorClassNotFound, d.Key, d.Value.ClassName));
            }
        }

        return result;
    }
}
