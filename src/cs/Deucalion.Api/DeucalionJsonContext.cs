using System.Text.Json.Serialization;
using Deucalion.Api.Models;

namespace Deucalion.Api;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(MonitorCheckedDto))]
[JsonSerializable(typeof(MonitorStateChangedDto))]
[JsonSerializable(typeof(MonitorEventDto))]
[JsonSerializable(typeof(MonitorStatsDto))]
[JsonSerializable(typeof(MonitorConfigurationDto))]
[JsonSerializable(typeof(MonitorDto))]
[JsonSerializable(typeof(MonitorDto[]))]
[JsonSerializable(typeof(PageConfigurationDto))]
[JsonSerializable(typeof(InitDto))]
internal partial class DeucalionJsonContext : JsonSerializerContext;
