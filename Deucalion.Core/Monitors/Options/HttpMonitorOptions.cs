using System.Net;

namespace Deucalion.Monitors.Options
{
    public class HttpMonitorOptions : PullMonitorOptions
    {
        public Uri Url { get; set; } = default!;

        public HttpStatusCode? ExpectedStatusCode { get; set; }
        public string? ExpectedResponseBodyPattern { get; set; }
        public bool? IgnoreCertificateErrors { get; set; }
    }
}
