using Deucalion.Monitors;
using Deucalion.Monitors.Configuration;
using Deucalion.Network.Monitors;

namespace Deucalion.Application.Configuration;

public static class ConfigurationExtensions
{
    internal static CheckInMonitor Build(this CheckInMonitorConfiguration configuration) =>
        new CheckInMonitor()
            .ConfigureCheckInMonitor(configuration);

    internal static DnsMonitor Build(this DnsMonitorConfiguration configuration) =>
        new DnsMonitor()
        {
            Host = configuration.Host
        }.ConfigureDnsMonitor(configuration);

    internal static HttpMonitor Build(this HttpMonitorConfiguration configuration) =>
        new HttpMonitor()
        {
            Url = configuration.Url
        }.ConfigureHttpMonitor(configuration);

    internal static PingMonitor Build(this PingMonitorConfiguration configuration) =>
        new PingMonitor()
        {
            Host = configuration.Host
        }.ConfigurePingMonitor(configuration);

    internal static TcpMonitor Build(this TcpMonitorConfiguration configuration) =>
        new TcpMonitor()
        {
            Host = configuration.Host,
            Port = configuration.Port
        }.ConfigureTcpMonitor(configuration);

    private static CheckInMonitor ConfigureCheckInMonitor(this CheckInMonitor monitor, CheckInMonitorConfiguration configuration)
    {
        monitor.Secret = configuration.Secret ?? monitor.Secret;
        monitor.ConfigurePushMonitor(configuration);
        return monitor;
    }

    private static DnsMonitor ConfigureDnsMonitor(this DnsMonitor monitor, DnsMonitorConfiguration configuration)
    {
        monitor.RecordType = configuration.RecordType ?? monitor.RecordType;
        monitor.Resolver = configuration.Resolver ?? monitor.Resolver;
        monitor.ConfigurePullMonitor(configuration);
        return monitor;
    }

    private static HttpMonitor ConfigureHttpMonitor(this HttpMonitor monitor, HttpMonitorConfiguration configuration)
    {
        monitor.Url = configuration.Url;
        monitor.ExpectedStatusCode = configuration.ExpectedStatusCode ?? monitor.ExpectedStatusCode;
        monitor.ExpectedResponseBodyPattern = configuration.ExpectedResponseBodyPattern ?? monitor.ExpectedResponseBodyPattern;
        monitor.IgnoreCertificateErrors = configuration.IgnoreCertificateErrors ?? monitor.IgnoreCertificateErrors;
        monitor.Method = configuration.Method ?? monitor.Method;
        monitor.ConfigurePullMonitor(configuration);
        return monitor;
    }

    private static PingMonitor ConfigurePingMonitor(this PingMonitor monitor, PingMonitorConfiguration configuration)
    {
        monitor.Host = configuration.Host;
        monitor.ConfigurePullMonitor(configuration);
        return monitor;
    }

    private static TcpMonitor ConfigureTcpMonitor(this TcpMonitor monitor, TcpMonitorConfiguration configuration)
    {
        monitor.Host = configuration.Host;
        monitor.Port = configuration.Port;
        monitor.ConfigurePullMonitor(configuration);
        return monitor;
    }

    private static PullMonitor ConfigurePullMonitor(this PullMonitor monitor, PullMonitorConfiguration configuration)
    {
        monitor.IntervalWhenUp = configuration.IntervalWhenUp ?? monitor.IntervalWhenUp;
        monitor.IntervalWhenDown = configuration.IntervalWhenDown ?? monitor.IntervalWhenDown;
        monitor.Timeout = configuration.Timeout ?? monitor.Timeout;
        monitor.WarnTimeout = configuration.WarnTimeout ?? monitor.WarnTimeout;
        monitor.ConfigureMonitor(configuration);
        return monitor;
    }

    private static PushMonitor ConfigurePushMonitor(this PushMonitor monitor, PushMonitorConfiguration configuration)
    {
        monitor.IntervalToDown = configuration.IntervalToDown ?? monitor.IntervalToDown;
        monitor.ConfigureMonitor(configuration);
        return monitor;
    }

    private static MonitorBase ConfigureMonitor(this MonitorBase monitor, MonitorConfiguration configuration)
    {
        monitor.Name = configuration.Name ?? monitor.Name;
        monitor.IgnoreFailCount = configuration.IgnoreFailCount ?? monitor.IgnoreFailCount;
        monitor.UpsideDown = configuration.UpsideDown ?? monitor.UpsideDown;
        return monitor;
    }
}
