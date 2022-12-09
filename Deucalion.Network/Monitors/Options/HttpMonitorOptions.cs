using System.Net;
using Deucalion.Monitors.Options;

namespace Deucalion.Network.Monitors.Options;

public class HttpMonitorOptions : PullMonitorOptions
{
    public Uri Url { get; set; } = default!;

    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
    public bool? IgnoreCertificateErrors { get; set; }
}
