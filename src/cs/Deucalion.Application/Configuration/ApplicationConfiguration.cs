using Deucalion.Application.Collections;
using Deucalion.Application.Yaml;
using Deucalion.Monitors.Configuration;
using Deucalion.Network.Monitors;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Deucalion.Application.Configuration;

public record ApplicationConfiguration
{
    public record ConfigurationDefaults : PullMonitorConfiguration
    {
        public TimeSpan? IntervalToDown { get; set; }

        public DnsMonitorOptionalConfiguration? Dns { get; set; }
        public HttpMonitorOptionalConfiguration? Http { get; set; }
        public PullMonitorConfiguration? Ping { get; set; }
        public PullMonitorConfiguration? Tcp { get; set; }
    }

    public static class Messages
    {
        public const string ConfigurationMustNotBeEmpty = "Configuration file must not be empty.";
        public const string ConfigurationMustHaveMonitorsSection = "Configuration file must have a 'monitors' section.";

        public const string ConfigurationMonitorCannotBeEmpty = "Monitor '{0}' cannot be empty.";
    }

    public ConfigurationDefaults? Defaults { get; set; }

    public required OrderedDictionary<string, MonitorConfiguration> Monitors { get; set; }

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
            .WithTagMapping("!checkin", typeof(CheckInMonitorConfiguration))
            .WithTagMapping("!dns", typeof(DnsMonitorConfiguration))
            .WithTagMapping("!http", typeof(HttpMonitorConfiguration))
            .WithTagMapping("!ping", typeof(PingMonitorConfiguration))
            .WithTagMapping("!tcp", typeof(TcpMonitorConfiguration))
            .WithTypeConverter(new HttpMethodConverter())
            .WithValidation()
            .Build();

        var result = deserializer.Deserialize<ApplicationConfiguration>(reader) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

        foreach (var monitor in result.Monitors)
        {
            // Check monitor is not empty
            if (monitor.Value is null)
            {
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorCannotBeEmpty, monitor.Key));
            }

            // Set monitor name
            monitor.Value.Name = monitor.Key;

            // Apply user-configured defaults
            if (result.Defaults is not null)
            {
                ApplyDefaults(result.Defaults, monitor);
            }
        }

        return result;
    }

    private static void ApplyDefaults(ConfigurationDefaults defaults, KeyValuePair<string, MonitorConfiguration> monitorConfiguration)
    {
        if (defaults.Dns is not null && monitorConfiguration.Value is DnsMonitorConfiguration dnsMonitorConfiguration)
        {
            dnsMonitorConfiguration.Timeout ??= defaults.Dns.Timeout;
            dnsMonitorConfiguration.WarnTimeout ??= defaults.Dns.WarnTimeout;
        }

        if (defaults.Http is not null && monitorConfiguration.Value is HttpMonitorConfiguration httpMonitorConfiguration)
        {
            httpMonitorConfiguration.Timeout ??= defaults.Http.Timeout;
            httpMonitorConfiguration.WarnTimeout ??= defaults.Http.WarnTimeout;
        }

        if (defaults.Ping is not null && monitorConfiguration.Value is PingMonitorConfiguration pingMonitorConfiguration)
        {
            pingMonitorConfiguration.Timeout ??= defaults.Ping.Timeout;
            pingMonitorConfiguration.WarnTimeout ??= defaults.Ping.WarnTimeout;
        }

        if (defaults.Tcp is not null && monitorConfiguration.Value is TcpMonitorConfiguration tcpMonitorConfiguration)
        {
            tcpMonitorConfiguration.Timeout ??= defaults.Tcp.Timeout;
            tcpMonitorConfiguration.WarnTimeout ??= defaults.Tcp.WarnTimeout;
        }

        if (monitorConfiguration.Value is PullMonitorConfiguration pullMonitorConfiguration)
        {
            pullMonitorConfiguration.IntervalWhenDown ??= defaults.IntervalWhenDown;
            pullMonitorConfiguration.IntervalWhenUp ??= defaults.IntervalWhenUp;
            pullMonitorConfiguration.Timeout ??= defaults.Timeout;
            pullMonitorConfiguration.WarnTimeout ??= defaults.WarnTimeout;
        }

        if (monitorConfiguration.Value is PushMonitorConfiguration pushMonitorConfiguration)
        {
            pushMonitorConfiguration.IntervalToDown ??= defaults.IntervalToDown;
        }
    }
}
