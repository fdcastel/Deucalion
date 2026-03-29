using System.Text.Json;
using System.Text.Json.Serialization;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using SharpYaml.Serialization;

namespace Deucalion.Application.Configuration;

// Source-gen limitations (SharpYaml 4.0.0-pre.4):
//   CS9035: Types with 'required' members excluded — source generator emits bare 'new T()'
//           (ApplicationConfiguration, DnsMonitorConfiguration, HttpMonitorConfiguration,
//            PingMonitorConfiguration, TcpMonitorConfiguration)
//   SHARPYAML002: Types containing Uri/IPEndPoint/HttpMethod members excluded until
//                 converters are registered (Phase 4)
//           (DnsMonitorOptionalConfiguration, HttpMonitorOptionalConfiguration,
//            ApplicationConfiguration.ConfigurationDefaults)
// All excluded types use reflection fallback at runtime.
[YamlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[YamlSerializable(typeof(PullMonitorConfiguration))]
[YamlSerializable(typeof(CheckInMonitorConfiguration))]
internal partial class DeucalionYamlContext : YamlSerializerContext;
