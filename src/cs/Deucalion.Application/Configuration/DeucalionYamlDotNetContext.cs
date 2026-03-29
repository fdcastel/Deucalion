using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Configuration;

[YamlStaticContext]
[YamlSerializable(typeof(ApplicationConfiguration))]
[YamlSerializable(typeof(ApplicationConfiguration.ConfigurationDefaults))]
[YamlSerializable(typeof(PullMonitorConfiguration))]
[YamlSerializable(typeof(CheckInMonitorConfiguration))]
[YamlSerializable(typeof(DnsMonitorConfiguration))]
[YamlSerializable(typeof(DnsMonitorOptionalConfiguration))]
[YamlSerializable(typeof(HttpMonitorConfiguration))]
[YamlSerializable(typeof(HttpMonitorOptionalConfiguration))]
[YamlSerializable(typeof(PingMonitorConfiguration))]
[YamlSerializable(typeof(TcpMonitorConfiguration))]
public partial class DeucalionYamlDotNetContext : YamlDotNet.Serialization.StaticContext;
