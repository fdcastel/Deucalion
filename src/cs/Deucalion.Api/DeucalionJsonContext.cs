using System.Text.Json.Serialization;
using Deucalion.Api.Models;

namespace Deucalion.Api;

[JsonSourceGenerationOptions(
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
internal partial class DeucalionJsonContext : JsonSerializerContext;
