using System.Text.Json;
using System.Text.Json.Serialization;
using Deucalion.Application.Yaml;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using SharpYaml;
using SharpYaml.Serialization;

namespace Deucalion.Application.Configuration;

// SharpYaml 3.7.0 (official): both blockers are resolved:
//   CS9035 (required keyword) — fixed in PR #139.
//   SHARPYAML002 — suppressed for member types handled by registered converters (PR #140).
// All 10 model types are registered in the source-gen context.
//
// Polymorphism is configured here, not on PullMonitorConfiguration: the base type lives in
// Deucalion.Core, which must not depend on SharpYaml. The monitor type is chosen by YAML tag;
// an unknown or missing tag falls back to the base type, which ApplicationConfiguration.Parse
// rejects with ConfigurationUnknownMonitorType.
//
// SHARPYAML021 warns that the base type carries no [YamlPolymorphic]; that is the point --
// the serializer-level settings above are the configuration.
#pragma warning disable SHARPYAML021
[YamlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
    UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase,
    Converters = [typeof(IPEndPointConverter), typeof(HttpMethodConverter)])]
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
[YamlDerivedTypeMapping(typeof(PullMonitorConfiguration), typeof(CheckInMonitorConfiguration), "checkin", Tag = "!checkin")]
[YamlDerivedTypeMapping(typeof(PullMonitorConfiguration), typeof(DnsMonitorConfiguration), "dns", Tag = "!dns")]
[YamlDerivedTypeMapping(typeof(PullMonitorConfiguration), typeof(HttpMonitorConfiguration), "http", Tag = "!http")]
[YamlDerivedTypeMapping(typeof(PullMonitorConfiguration), typeof(PingMonitorConfiguration), "ping", Tag = "!ping")]
[YamlDerivedTypeMapping(typeof(PullMonitorConfiguration), typeof(TcpMonitorConfiguration), "tcp", Tag = "!tcp")]
internal partial class DeucalionYamlContext : YamlSerializerContext;
#pragma warning restore SHARPYAML021
