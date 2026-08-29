# Notes for contributors and agents

User-facing docs live in [README.md](README.md); this file only covers what is
not obvious from looking around. Project guidelines are in the README under
*Development notes* — read those first.

## Styling: hand-written CSS, no utility framework

All styling is semantic CSS in `src/ts/deucalion-ui/src/styles/`:
`tokens.css` (custom properties + a minimal reset), `layout.css`, `tweaks.css`.

There is deliberately **no Tailwind or other utility framework**. It was removed
because nothing used it: it contributed a third of the CSS bundle, generated
phantom utilities by scanning source text for bare identifiers, and shadowed our
own `--font-mono` / `--radius-lg` tokens. Extend the token set instead of adding
utility classes, and do not reintroduce a framework preflight — `tokens.css`
carries the handful of resets the layout actually needs.

## The wire contract is mirrored by hand

The SSE and REST payloads use short keys (`n`, `at`, `st`, `ms`, `ns`) to
keep frames small. They are declared twice, with nothing generating one from the
other:

- `src/cs/Deucalion.Api/Models/*.cs` (`[JsonPropertyName]`)
- `src/ts/deucalion-ui/src/services/deucalion-types.ts`

Change both or neither. The `Wire contract` tests in
`src/ts/deucalion-ui/tests/e2e/dashboard.spec.ts` fail if they drift.

`MonitorState` is likewise declared in both `src/cs/Deucalion.Core/MonitorState.cs`
and `deucalion-types.ts` — the numeric values must match.

## Project layout

`Deucalion.Core` is the domain and has no package references; keep it that
way (the YAML polymorphism settings live in `Deucalion.Application`'s
`DeucalionYamlContext`, not on the base record). The dependency graph and the
list of places a new monitor type touches are in the README under *Projects
overview*; update that list when you add or remove one.

The discovery payloads (`/api/status`, `/api/version`; `Models/StatusDto.cs`,
`Models/VersionDto.cs`, served from `Endpoints/DiscoveryEndpoints.cs`) are the
exception: long keys, string states, ISO-8601 timestamps. They exist for agents
and humans on a one-shot fetch, the UI never reads them, and they are
deliberately **not** mirrored in `deucalion-types.ts`. Their shape is pinned by
the `Discovery` tests in `ApiIntegrationTests.cs`, and `public/llms.txt` must
keep naming every endpoint (`DiscoveryHeadTests.cs` fails otherwise). `MonitorRun`
(`Deucalion.Core/Storage/MonitorRun.cs`, `IStorage.GetCurrentRunAsync`) is the
storage-side sibling of those DTOs: it backs `since`/`sinceIsLowerBound` and is
likewise not mirrored in TS.

## Engine invariants

Only `EngineBackgroundService` writes a monitor's auto-WARN baseline (via
`WarnThresholdPolicy.Refresh`); request handlers such as `GET /api/monitors` and
`/api/status` must use the read-only `WarnThresholdPolicy.Compute`. A GET that
mutated the live monitor from concurrent request threads tore the 16-byte
`TimeSpan?` (issue #15, 2026-08-29). Pinned by `WarnThresholdPolicyTests` and
`PullMonitorAutoWarnConcurrencyTests`.

## Tests

`dotnet test` needs the `global.json` opt-in to Microsoft.Testing.Platform;
without it the xunit.v3 project is invisible to VSTest and the command silently
passes while running nothing. Don't remove it.

Monitor tests that need the public internet (DNS, ICMP) are skipped unless
`DEUCALION_TESTS_NETWORK=1`. HTTP and TCP are covered hermetically against a
local listener — keep it that way.

## Commands

```powershell
Invoke-Build                 # build everything into ./publish
Invoke-Build Test            # dotnet test + vitest + frontend lint
Invoke-Build Dev             # watch mode: Deucalion.Api + vite

npm --prefix ./src/ts/deucalion-ui run test:e2e   # Playwright; boots both servers itself
```
