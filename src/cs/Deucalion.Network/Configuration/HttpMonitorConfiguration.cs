using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record HttpMonitorOptionalConfiguration : PullMonitorConfiguration
{
    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
    public bool? IgnoreCertificateErrors { get; set; }
    public HttpMethod? Method { get; set; }
}

[YamlSerializable]
public record HttpMonitorConfiguration : HttpMonitorOptionalConfiguration
{
    [Required]
    public Uri Url { get; set; } = null!;
}
