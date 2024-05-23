using Deucalion.Application.Collections;
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

    public string? Version { get; set; }

    public OrderedDictionary<string, MonitorBase> Monitors { get; set; } = default!;
    public AllMonitorTypesDefaults? Defaults { get; set; } = default!;

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
            .WithTypeConverter(new HttpMethodConverter())
            .WithValidation()
            .Build();

        var result = deserializer.Deserialize<MonitorConfiguration>(reader) ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

        foreach (var m in result.Monitors)
        {
            // Check monitor is not empty
            if (m.Value is null)
            {
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorCannotBeEmpty, m.Key));
            }

            // Set monitor name
            m.Value.Name = m.Key;

            // Set user-configured defaults
            if (result.Defaults is not null)
            {
                ApplyUserDefaults(result.Defaults, m);
            }

            // Set system defaults
            if (m.Value is DnsMonitor dnsMonitor)
            {
                dnsMonitor.Timeout ??= DnsMonitor.DefaultDnsTimeout;
                dnsMonitor.WarnTimeout ??= DnsMonitor.DefaultDnsWarnTimeout;
            }

            if (m.Value is PingMonitor pingMonitor)
            {
                pingMonitor.Timeout ??= PingMonitor.DefaultPingTimeout;
                pingMonitor.WarnTimeout ??= PingMonitor.DefaultPingWarnTimeout;

            }

            if (m.Value is PullMonitor pullMonitor)
            {
                pullMonitor.IntervalWhenDown ??= PullMonitor.DefaultIntervalWhenDown;
                pullMonitor.IntervalWhenUp ??= PullMonitor.DefaultIntervalWhenUp;
                pullMonitor.Timeout ??= PullMonitor.DefaultTimeout;
                pullMonitor.WarnTimeout ??= PullMonitor.DefaultWarnTimeout;
            }

            if (m.Value is PushMonitor pushMonitor)
            {
                pushMonitor.IntervalToDown ??= PushMonitor.DefaultIntervalToDown;
            }
        }

        return result;
    }

    private static void ApplyUserDefaults(AllMonitorTypesDefaults defaults, KeyValuePair<string, MonitorBase> m)
    {
        if (defaults is not null)
        {
            if (defaults.Dns is not null && m.Value is DnsMonitor dnsMonitor)
            {
                dnsMonitor.Timeout ??= defaults.Dns.Timeout;
                dnsMonitor.WarnTimeout ??= defaults.Dns.WarnTimeout;
            }

            if (defaults.Http is not null && m.Value is HttpMonitor httpMonitor)
            {
                httpMonitor.Timeout ??= defaults.Http.Timeout;
                httpMonitor.WarnTimeout ??= defaults.Http.WarnTimeout;
            }

            if (defaults.Ping is not null && m.Value is PingMonitor pingMonitor)
            {
                pingMonitor.Timeout ??= defaults.Ping.Timeout;
                pingMonitor.WarnTimeout ??= defaults.Ping.WarnTimeout;
            }

            if (defaults.Tcp is not null && m.Value is TcpMonitor tcpMonitor)
            {
                tcpMonitor.Timeout ??= defaults.Tcp.Timeout;
                tcpMonitor.WarnTimeout ??= defaults.Tcp.WarnTimeout;
            }

            if (m.Value is PullMonitor pullMonitor)
            {
                pullMonitor.IntervalWhenDown ??= defaults.IntervalWhenDown;
                pullMonitor.IntervalWhenUp ??= defaults.IntervalWhenUp;
                pullMonitor.Timeout ??= defaults.Timeout;
                pullMonitor.WarnTimeout ??= defaults.WarnTimeout;
            }

            // ToDo: PushMonitor
        }
    }
}
