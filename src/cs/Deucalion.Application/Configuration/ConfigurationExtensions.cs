using Deucalion.Configuration;
using Deucalion.Monitors;
using Deucalion.Network.Configuration;
using Deucalion.Network.Monitors;

namespace Deucalion.Application.Configuration;

public static class ConfigurationExtensions
{
    internal static CheckInMonitor Build(this CheckInMonitorConfiguration configuration)
    {
        var monitor = new CheckInMonitor();
        monitor.Secret = configuration.Secret ?? monitor.Secret;
        monitor.IntervalToDown = configuration.IntervalToDown ?? monitor.IntervalToDown;
        monitor.IntervalWhenUp = monitor.IntervalToDown;
        monitor.IntervalWhenDown = monitor.IntervalToDown;
        monitor.Name = configuration.Name ?? monitor.Name;
        monitor.IgnoreFailCount = configuration.IgnoreFailCount ?? monitor.IgnoreFailCount;
        monitor.UpsideDown = configuration.UpsideDown ?? monitor.UpsideDown;
        return monitor;
    }

    internal static DnsMonitor Build(this DnsMonitorConfiguration configuration)
    {
        var monitor = new DnsMonitor() { Host = configuration.Host };
        monitor.RecordType = configuration.RecordType ?? monitor.RecordType;
        monitor.Resolver = configuration.Resolver ?? monitor.Resolver;
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static HttpMonitor Build(this HttpMonitorConfiguration configuration)
    {
        var monitor = new HttpMonitor() { Url = configuration.Url };
        monitor.ExpectedStatusCode = configuration.ExpectedStatusCode ?? monitor.ExpectedStatusCode;
        monitor.ExpectedResponseBodyPattern = configuration.ExpectedResponseBodyPattern ?? monitor.ExpectedResponseBodyPattern;
        monitor.IgnoreCertificateErrors = configuration.IgnoreCertificateErrors ?? monitor.IgnoreCertificateErrors;
        monitor.Method = configuration.Method ?? monitor.Method;
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static PingMonitor Build(this PingMonitorConfiguration configuration)
    {
        var monitor = new PingMonitor() { Host = configuration.Host };
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static TcpMonitor Build(this TcpMonitorConfiguration configuration)
    {
        var monitor = new TcpMonitor() { Host = configuration.Host, Port = configuration.Port };
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    private static void ConfigurePullMonitor(PullMonitor monitor, PullMonitorConfiguration configuration)
    {
        monitor.IntervalWhenUp = configuration.IntervalWhenUp ?? monitor.IntervalWhenUp;
        monitor.IntervalWhenDown = configuration.IntervalWhenDown ?? monitor.IntervalWhenDown;
        monitor.Timeout = configuration.Timeout ?? monitor.Timeout;
        monitor.WarnTimeout = configuration.WarnTimeout ?? monitor.WarnTimeout;
        monitor.Name = configuration.Name ?? monitor.Name;
        monitor.IgnoreFailCount = configuration.IgnoreFailCount ?? monitor.IgnoreFailCount;
        monitor.UpsideDown = configuration.UpsideDown ?? monitor.UpsideDown;
    }
}
