# Deucalion - Native AOT Migration

## What Was Done

### 1. Build Infrastructure (`Directory.Build.props`, `.csproj` files)

| Setting | Purpose |
|---------|---------|
| `IsAotCompatible=true` | Enables AOT compatibility analyzers across all projects |
| `EnableTrimAnalyzer=true` | Enables IL trimming analyzers (detects reflection usage) |
| `EnableConfigurationBindingGenerator=true` | Replaces reflection-based `IConfiguration.Bind()` with source-generated code |
| `EnableRequestDelegateGenerator=true` | Replaces reflection-based `MapGet`/`MapPost` with source-generated request delegates |
| `PublishAot=true` | Enables Native AOT compilation on `dotnet publish --self-contained` |
| `IsAotCompatible=false` + `EnableTrimAnalyzer=false` on test project | Tests use reflection freely; no AOT there |

### 2. System.Text.Json Source Generator (`DeucalionJsonContext.cs`)

Replaced runtime JSON reflection with a compile-time source generator. Registered all DTO types (`MonitorCheckedDto`, `MonitorStateChangedDto`, `MonitorDto`, `MonitorDto[]`, `PageConfigurationDto`, etc.) in a single `JsonSerializerContext` class. Wired into both HTTP JSON options and SignalR JSON protocol options.

### 3. Anonymous Type Replacement (`PageConfigurationDto.cs`)

The `/api/configuration` endpoint returned `new { options.PageTitle, options.PageDescription }`. Anonymous types cannot be registered in a `JsonSerializerContext`. Replaced with a named record `PageConfigurationDto`.

### 4. YamlDotNet Static Serialization

Migrated from `DeserializerBuilder` (reflection-based) to `StaticDeserializerBuilder` (source-generated). This required:

- Adding `[YamlSerializable]` attribute to all 11 YAML model types across 3 projects
- Creating `DeucalionYamlContext` static context class registering all types
- Adding `YamlDotNet.Analyzers.StaticGenerator` as a project-reference analyzer (not on NuGet)
- Adding `YamlDotNet` package references to `Deucalion.Core` and `Deucalion.Network` (for the attribute)

### 5. Custom Type Converters (`TimeSpanConverter.cs`, `IPEndPointConverter.cs`)

The reflection-based `DeserializerBuilder` auto-converts YAML scalars to `TimeSpan` and `IPEndPoint` via .NET's `TypeConverter` infrastructure. The `StaticDeserializerBuilder` lacks this capability. Two explicit `IYamlTypeConverter` implementations were created.

### 6. SignalR Hub Downgrade (`MonitorHub.cs`, `EngineBackgroundService.cs`)

`Hub<IMonitorHubClient>` uses `TypedClientBuilder<T>` which generates client proxies at runtime via `System.Reflection.Emit`. This is fundamentally impossible in AOT. Replaced with untyped `Hub` + `SendAsync()` string-based dispatch. The `IMonitorHubClient` interface is retained for `nameof()` references.

### 7. `required` Keyword Removal (5 configuration records)

The YamlDotNet source generator emits `new T()` in its object factory. The C# `required` keyword makes this a compile error (`CS9035`). Removed `required` from all YAML model properties (`Host`, `Url`, `Port`). The `[Required]` DataAnnotation attribute still provides runtime validation.

### 8. `OrderedDictionary` → `Dictionary` (`ApplicationConfiguration.cs`)

`OrderedDictionary<string, PullMonitorConfiguration>` (.NET 9) is not supported by the YamlDotNet source generator. Replaced with `Dictionary<string, PullMonitorConfiguration>`.

---

## Problems That Needed Solving

| # | Problem | Root Cause | Solution |
|---|---------|-----------|----------|
| 1 | `PlatformNotSupportedException: Dynamic code generation is not supported` | `Hub<T>` uses `Reflection.Emit` for client proxy generation | Replaced with untyped `Hub` + `SendAsync()` |
| 2 | `CS9035: Required member must be set` in generated code | YamlDotNet generator emits bare `new T()` | Removed `required` keyword; kept `[Required]` attribute |
| 3 | `InvalidCastException: String → TimeSpan` | Static deserializer doesn't use .NET `TypeConverter` infrastructure | Created `TimeSpanConverter : IYamlTypeConverter` |
| 4 | `InvalidCastException: String → IPEndPoint` | Same as above | Created `IPEndPointConverter : IYamlTypeConverter` |
| 5 | `CS0305: OrderedDictionary requires type arguments` in generated code | Source generator doesn't handle .NET 9 generic types | Changed to `Dictionary<K,V>` |
| 6 | `IL2026`/`IL3050` on `MapGet`/`MapPost` | Missing Request Delegate Generator | Added `EnableRequestDelegateGenerator=true` |
| 7 | `IL2026` on `Validator.ValidateObject()` | DataAnnotations uses reflection | Suppressed with `[UnconditionalSuppressMessage]` (types preserved by YAML source gen) |
| 8 | Anonymous type in `/api/configuration` | Cannot register anonymous types in `JsonSerializerContext` | Created `PageConfigurationDto` named record |
| 9 | Source generator not on NuGet | `YamlDotNet.Analyzers.StaticGenerator` is not published | Used local project reference from `tmp/YamlDotNet` |

---

## What Could Have Been Done Better

### 1. Start with the SignalR `Hub<T>` Problem First

The `Hub<IMonitorHubClient>` issue was discovered only at runtime (`invoke-Build prod`), not by the AOT analyzer. The IL3050 warnings were initially suppressed with `[UnconditionalSuppressMessage]` trusting that "the framework handles it" — but it doesn't in AOT. **Suppressing AOT warnings without testing the actual native binary is dangerous.** The correct approach was to test the AOT binary immediately after the first successful publish, before suppressing any warnings.

### 2. Avoid the `required` Keyword from the Start

Had the YamlDotNet source generator's `new T()` limitation been known upfront, the `required` → `= null!` migration could have been planned in advance rather than discovered through compile errors. A quick test build with the source generator before annotating all types would have revealed this earlier.

### 3. Bundle the YamlDotNet Source Generator Properly

The local `<ProjectReference>` to `tmp/YamlDotNet/YamlDotNet.Analyzers.StaticGenerator` is fragile. It depends on having the YamlDotNet source tree checked into `tmp/`. A better approach would be to:
- Build the analyzer locally and publish it as a local NuGet package
- Or wait for the official NuGet package and use a `<PackageReference>` instead

### 4. Single Commit is Too Large

The 23-file commit bundles infrastructure, JSON, YAML, SignalR, and model changes together. Ideally this should have been 4-5 smaller commits:
1. Build infrastructure (Directory.Build.props, csproj settings)
2. JSON source generator + PageConfigurationDto
3. YamlDotNet static migration + type converters
4. SignalR Hub<T> → Hub downgrade

---

## What We Gained

### Deployment

| Metric | Before (FDD) | After (AOT) |
|--------|-------------|-------------|
| **Backend files** | 22 files (1.4 MB .NET DLLs + 30.7 MB native SQLite across platforms) | 7 files (21.2 MB single exe + 1.7 MB SQLite) |
| **Runtime dependency** | Requires .NET 10 runtime installed on host | None — fully self-contained |
| **Startup time** | JIT compilation on first request | Near-instant — all code is pre-compiled |
| **Memory footprint** | Higher (JIT compiler + IL metadata in memory) | Lower (no JIT, no IL metadata) |
| **Container image** | Needs `mcr.microsoft.com/dotnet/aspnet:10.0` base (~220 MB) | Can use `mcr.microsoft.com/dotnet/runtime-deps:10.0` (~13 MB) or even `scratch` |

### Code Quality

- **Zero AOT/trim warnings** in production code — all potential reflection issues resolved at compile time
- **Compile-time JSON serialization** — no runtime reflection for HTTP responses or SignalR messages
- **Compile-time YAML deserialization** — configuration parsing is fully static
- **Compile-time request delegate generation** — `MapGet`/`MapPost` endpoints pre-compiled
- **Compile-time configuration binding** — `IConfiguration.Bind()` source-generated

### Operational

- **Single-file deployment** — one `.exe` + one native SQLite DLL + config files
- **No .NET SDK or runtime on target machine**
- **Predictable performance** — no JIT warmup, no tiered compilation variance

---

## What We Lost

### Type Safety

- **SignalR:** `Hub<IMonitorHubClient>` provided compile-time safety for hub method names and parameter types. `SendAsync("MonitorChecked", e)` is stringly-typed — a typo in the method name becomes a silent runtime bug. Mitigated by using `nameof(IMonitorHubClient.MonitorChecked)`.

- **`required` keyword:** Removed from 4 properties (`Host`, `Url`, `Port`). The compiler no longer enforces initialization. Mitigated by `[Required]` attribute providing runtime validation during YAML deserialization.

- **`OrderedDictionary` contract:** `Dictionary<K,V>` preserves insertion order in .NET's current implementation, but this is an implementation detail, not a specification guarantee. `OrderedDictionary<K,V>` provided that guarantee explicitly.

### Coupling

- **YamlDotNet dependency spread:** `YamlDotNet` package added to `Deucalion.Core` and `Deucalion.Network` projects (previously only in `Deucalion.Application`). The `[YamlSerializable]` attribute requires the package at the declaration site, pushing a serialization concern into domain/infrastructure layers.

- **Local source generator dependency:** `Deucalion.Application.csproj` now references `tmp/YamlDotNet/YamlDotNet.Analyzers.StaticGenerator` as a local project. This is a build-time dependency on an external project's source tree.

### Flexibility

- **Anonymous type convenience:** The inline `new { options.PageTitle, options.PageDescription }` was terser than a dedicated `PageConfigurationDto` record. Minor, but adds a file.

- **Build time:** AOT publish is significantly slower than framework-dependent publish (native compilation step).

- **Debugging:** Native AOT binaries have limited debugging support compared to JIT-compiled assemblies. Stack traces may show `+0x...` offsets instead of source lines.

### Maintenance

- **Type converters:** Two new boilerplate files (`TimeSpanConverter.cs`, `IPEndPointConverter.cs`) that wouldn't exist with the reflection-based deserializer. Any new non-primitive YAML type will need another converter.

- **Static context registration:** Every new YAML model type must be added to `DeucalionYamlContext` and every new DTO must be added to `DeucalionJsonContext`. Forgetting this results in runtime errors, not compile-time errors.

---

## Verdict

The migration is net positive for deployment and operational simplicity. The main costs are reduced type safety in SignalR messaging (mitigated by `nameof()`) and increased YamlDotNet boilerplate (a limitation of the library's AOT support maturity). The build produces a ~21 MB self-contained native executable with zero AOT warnings and all 68 tests passing.

---

## Next Steps: YamlDotNet Pre-Release Improvements

YamlDotNet `17.0.0-pre.5` ([fdcastel/YamlDotNet@pre-release](https://github.com/fdcastel/YamlDotNet/tree/pre-release)) includes fixes from 7 PRs submitted upstream. The pre-release packages are configured via [nuget.config](../nuget.config) pointing to GitHub Packages. Several of these fixes allow reverting workarounds that were needed for the AOT migration.

### Plan

#### 1. ~~Remove custom `TimeSpanConverter`~~ — SKIPPED

PR [#1092](https://github.com/aaubry/YamlDotNet/pull/1092) adds a built-in `TimeSpanConverter` registered by default in both `StaticDeserializerBuilder` and `StaticSerializerBuilder`. However, the built-in converter only handles `TimeSpan`, not `TimeSpan?` (nullable). Since all TimeSpan properties in the configuration model are `TimeSpan?`, the custom converter is still required.

- Custom `TimeSpanConverter.cs` — still required
- `.WithTypeConverter(new TimeSpanConverter())` — still required

#### 2. Restore `required` keyword on YAML configuration types (revert workaround from §7)

PR [#1092](https://github.com/aaubry/YamlDotNet/pull/1092) makes the source generator emit object initializer syntax (`new T() { Prop = default! }`) for types with `required` members, preventing the `CS9035` compile error.

- Restore `required` on `ApplicationConfiguration.Monitors`
- Restore `required` on configuration record properties that had it removed (e.g., `PullMonitorConfiguration`, `TcpMonitorConfiguration.Host`, `TcpMonitorConfiguration.Port`)
- The `[Required]` DataAnnotation attribute can stay for runtime validation; the `required` keyword adds compile-time enforcement

#### 3. Restore `OrderedDictionary<string, PullMonitorConfiguration>` (revert workaround from §8)

PR [#1092](https://github.com/aaubry/YamlDotNet/pull/1092) adds `OrderedDictionary<TKey, TValue>` (.NET 9+) recognition in the source generator.

- Change `Dictionary<string, PullMonitorConfiguration>` back to `OrderedDictionary<string, PullMonitorConfiguration>` in `ApplicationConfiguration.cs`
- This restores the explicit insertion-order guarantee that YAML documents expect

#### 4. Keep custom `IPEndPointConverter` and `HttpMethodConverter`

PR #1092 adds built-in converters for `TimeSpan` and `Uri` only. `IPEndPoint` and `HttpMethod` are not yet covered upstream.

- `IPEndPointConverter.cs` — still required
- `HttpMethodConverter.cs` — still required
- File as follow-up suggestion to add more built-in converters (already tracked in [YAML_DOTNET_SUGGESTIONS.md §4](../tmp/YAML_DOTNET_SUGGESTIONS.md))

#### 5. Remove local YamlDotNet source tree

The `<ProjectReference>` to `tmp/YamlDotNet/YamlDotNet.Analyzers.StaticGenerator` has been replaced with a `<PackageReference>` to the NuGet package `YamlDotNet.Analyzers.StaticGenerator` version `17.0.0-pre.5`.

- The `tmp/YamlDotNet/` directory can be deleted once the pre-release packages are verified stable
- When the upstream packages are officially released on nuget.org, remove the `github-fdcastel` source from `nuget.config`

### Expected Outcome

| Workaround | Status |
|------------|--------|
| Custom `TimeSpanConverter` | Can remove — built-in now |
| `required` keyword removed | Can restore |
| `OrderedDictionary` → `Dictionary` | Can restore |
| Custom `IPEndPointConverter` | Still needed |
| Custom `HttpMethodConverter` | Still needed |
| Local source generator `ProjectReference` | Removed — using NuGet package |
