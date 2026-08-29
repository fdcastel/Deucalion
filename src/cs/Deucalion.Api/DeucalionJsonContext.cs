using System.Text.Json.Serialization;
using Deucalion.Api.Models;

namespace Deucalion.Api;

// JsonIgnoreCondition is NOT a [Flags] enum (Never=0, Always=1, WhenWritingDefault=2,
// WhenWritingNull=3). The previous `WhenWritingDefault | WhenWritingNull` evaluated to 2|3 == 3
// == WhenWritingNull, so it only ever meant this. Stated plainly here.
//
// WhenWritingNull is also the *correct* behaviour: WhenWritingDefault would drop `lastState: 0`
// (Unknown), `availability: 0` and `fr: 0`, all of which the UI reads.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(MonitorCheckedDto))]
[JsonSerializable(typeof(MonitorStateChangedDto))]
[JsonSerializable(typeof(MonitorEventDto))]
[JsonSerializable(typeof(MonitorStatsDto))]
[JsonSerializable(typeof(MonitorConfigurationDto))]
[JsonSerializable(typeof(MonitorDto))]
[JsonSerializable(typeof(MonitorDto[]))]
[JsonSerializable(typeof(PageConfigurationDto))]
[JsonSerializable(typeof(StatusDto))]
[JsonSerializable(typeof(VersionDto))]
internal partial class DeucalionJsonContext : JsonSerializerContext;
