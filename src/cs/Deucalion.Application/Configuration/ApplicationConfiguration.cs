using Deucalion.Application.Yaml;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
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

    public required OrderedDictionary<string, PullMonitorConfiguration> Monitors { get; set; }

    public static ApplicationConfiguration ReadFromFile(string configurationFile)
    {
        var content = File.ReadAllText(configurationFile);
        return ReadFromString(content);
    }

    public static ApplicationConfiguration ReadFromString(string content)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTagMapping("!checkin", typeof(CheckInMonitorConfiguration))
            .WithTagMapping("!dns", typeof(DnsMonitorConfiguration))
            .WithTagMapping("!http", typeof(HttpMonitorConfiguration))
            .WithTagMapping("!ping", typeof(PingMonitorConfiguration))
            .WithTagMapping("!tcp", typeof(TcpMonitorConfiguration))
            .WithTypeConverter(new InterpolatedStringConverter())
            .WithTypeConverter(new HttpMethodConverter())
            .WithValidation()
            .Build();

        var result = deserializer.Deserialize<ApplicationConfiguration>(content) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

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

    private static void ApplyDefaults(ConfigurationDefaults defaults, KeyValuePair<string, PullMonitorConfiguration> monitorConfiguration)
    {
        if (defaults.Dns is not null && monitorConfiguration.Value is DnsMonitorConfiguration dnsMonitorConfiguration)
        {
            dnsMonitorConfiguration.Timeout ??= defaults.Dns.Timeout;
            dnsMonitorConfiguration.WarnTimeout ??= defaults.Dns.WarnTimeout;

            dnsMonitorConfiguration.RecordType ??= defaults.Dns.RecordType;
            dnsMonitorConfiguration.Resolver ??= defaults.Dns.Resolver;
        }

        if (defaults.Http is not null && monitorConfiguration.Value is HttpMonitorConfiguration httpMonitorConfiguration)
        {
            httpMonitorConfiguration.Timeout ??= defaults.Http.Timeout;
            httpMonitorConfiguration.WarnTimeout ??= defaults.Http.WarnTimeout;

            httpMonitorConfiguration.ExpectedStatusCode ??= defaults.Http.ExpectedStatusCode;
            httpMonitorConfiguration.ExpectedResponseBodyPattern ??= defaults.Http.ExpectedResponseBodyPattern;
            httpMonitorConfiguration.IgnoreCertificateErrors ??= defaults.Http.IgnoreCertificateErrors;
            httpMonitorConfiguration.Method ??= defaults.Http.Method;
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
            pullMonitorConfiguration.Timeout ??= defaults.Timeout;
            pullMonitorConfiguration.WarnTimeout ??= defaults.WarnTimeout;

            pullMonitorConfiguration.IntervalWhenDown ??= defaults.IntervalWhenDown;
            pullMonitorConfiguration.IntervalWhenUp ??= defaults.IntervalWhenUp;
        }

        if (monitorConfiguration.Value is CheckInMonitorConfiguration checkInMonitorConfiguration)
        {
            checkInMonitorConfiguration.IntervalToDown ??= defaults.IntervalToDown;
        }
    }
}
