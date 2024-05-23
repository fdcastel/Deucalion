using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Monitors.Configuration;

namespace Deucalion.Network.Monitors;

public record HttpMonitorOptionalConfiguration : PullMonitorConfiguration
{
    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
    public bool? IgnoreCertificateErrors { get; set; }
    public HttpMethod? Method { get; set; }
}

public record HttpMonitorConfiguration : HttpMonitorOptionalConfiguration
{
    [Required]
    public required Uri Url { get; set; }
}
