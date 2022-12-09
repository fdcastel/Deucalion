using System.Net;
using DnsClient;

namespace Deucalion.Application.Configuration;

public class MonitorConfiguration
{
    // Monitor
    public string ClassName { get; set; } = default!;
    public string MonitorName { get; set; } = default!;

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    // PushMonitor
    public TimeSpan? IntervalToDown { get; set; }

    // PullMonitor
    public TimeSpan? IntervalWhenUp { get; set; }
    public TimeSpan? IntervalWhenDown { get; set; }
    public TimeSpan? Timeout { get; set; }

    // PingMonitor
    public string? Host { get; set; }

    // TcpMonitor
    public int? Port { get; set; }

    // DnsMonitor
    public QueryType? RecordType { get; set; }
    public IPEndPoint? Resolver { get; set; }

    // HttpMonitor
    public Uri? Url { get; set; }
    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
    public bool? IgnoreCertificateErrors { get; set; }
}
