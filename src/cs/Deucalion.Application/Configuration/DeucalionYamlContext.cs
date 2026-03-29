using System.Text.Json;
using System.Text.Json.Serialization;
using Deucalion.Application.Yaml;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using SharpYaml.Serialization;

namespace Deucalion.Application.Configuration;

// SharpYaml 3.7.0 (official): both blockers are resolved:
//   CS9035 (required keyword) — fixed in PR #139.
//   SHARPYAML002 — suppressed for member types handled by registered converters (PR #140).
// All 10 model types are registered in the source-gen context.
[YamlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    Converters = [typeof(UriConverter), typeof(IPEndPointConverter), typeof(HttpMethodConverter)])]
[YamlSerializable(typeof(ApplicationConfiguration))]
[YamlSerializable(typeof(ApplicationConfiguration.ConfigurationDefaults))]
[YamlSerializable(typeof(PullMonitorConfiguration))]
[YamlSerializable(typeof(CheckInMonitorConfiguration))]
[YamlSerializable(typeof(DnsMonitorOptionalConfiguration))]
[YamlSerializable(typeof(DnsMonitorConfiguration))]
[YamlSerializable(typeof(HttpMonitorOptionalConfiguration))]
[YamlSerializable(typeof(HttpMonitorConfiguration))]
[YamlSerializable(typeof(PingMonitorConfiguration))]
[YamlSerializable(typeof(TcpMonitorConfiguration))]
internal partial class DeucalionYamlContext : YamlSerializerContext;
