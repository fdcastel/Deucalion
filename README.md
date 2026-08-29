# Deucalion

A minimal project for systems monitoring, designed for cases where Grafana and Prometheus are overkill.

This is not a typical "Status Page" project. There are no alerts, incident histories, push notifications, or CRUD UIs for configuration.

Simply create a configuration file, start the service, and you're done.

![Deucalion UI example](deucalion-ui.apng)

# Table of Contents

- [Prerequisites](#prerequisites)
- [Usage](#usage)
  - [Quick start](#quick-start)
- [Configuration](#configuration)
  - [Defaults Section](#defaults-section)
  - [WARN timeout auto-baseline](#warn-timeout-auto-baseline)
  - [Monitors Section](#monitors-section)
  - [Monitor Name Interpolation](#monitor-name-interpolation)
- [Monitor Types](#monitor-types)
  - [`ping` Monitor](#ping-monitor)
  - [`tcp` Monitor](#tcp-monitor)
  - [`dns` Monitor](#dns-monitor)
  - [`http` Monitor](#http-monitor)
  - [`checkin` Monitor](#checkin-monitor)
- [API](#api)
- [Development notes](#development-notes)
  - [How to debug](#how-to-debug)
  - [Logging](#logging)
  - [How to build](#how-to-build)
- [License](#license)

# Prerequisites

- Docker (for containerized usage)
- .NET 10 SDK (for development and building)
- PowerShell (for build scripts)
- [Invoke-Build](https://github.com/nightroman/Invoke-Build) (for build automation)

[GitVersion](https://gitversion.net/) is pinned in `.config/dotnet-tools.json` and restored by the build script (`dotnet tool restore`); it does not need to be installed globally.

# Usage

## Quick start

```yaml
# docker-compose.yaml
services:
  deucalion:
    container_name: deucalion
    image: ghcr.io/fdcastel/deucalion:latest
    ports:
      - 80:8080
    environment:
      - DEUCALION__PAGETITLE=Deucalion status
    volumes:
      - ./example.yaml:/app/deucalion.yaml  # The image reads /app/deucalion.yaml; mount your configuration file there.
      - ./data/:/storage/                    # Must be writable by UID 1654 (the image runs as a non-root user).
```

```yaml
# example.yaml
defaults:
  intervalWhenUp: 00:00:03   # Default check interval when the monitor is UP

monitors:
  ping-example:
    !ping
    host: cloudflare.com
    group: Cloudflare

  tcp-example:
    !tcp
    host: cloudflare.com
    port: 443
    group: Cloudflare

  dns-example:
    !dns
    host: google.com
    recordType: A
    resolver: 1.1.1.1:53
    group: Google

  http-example:
    !http
    url: https://google.com
    expectedStatusCode: 200
    expectedResponseBodyPattern: .*
    ignoreCertificateErrors: true
    group: Google
```

# Configuration

Monitoring behavior is defined in a YAML configuration file (e.g., `deucalion.yaml`).

### Server options

Server settings come from the `Deucalion` configuration section -- as environment variables, `DEUCALION__<NAME>` (see the *Quick start* above). Every option has a default; an invalid value (e.g. a zero interval) stops the server at startup with a `Configuration error` naming the option.

| Option                 | Default            | Description |
|------------------------|--------------------|-------------|
| `CONFIGURATIONFILE`    | `deucalion.yaml`   | Path of the YAML configuration file. |
| `STORAGEPATH`          | `<temp>/Deucalion` | Directory of the SQLite database. |
| `PAGETITLE`            | `Deucalion status` | Title of the web page. |
| `EVENTRETENTIONPERIOD` | `30.00:00:00`      | Events older than this are deleted by the purge. |
| `MAXEVENTSPERMONITOR`  | `100000`           | Newest events kept per monitor; the purge deletes the rest, even if still within the retention period. |
| `PURGEINTERVAL`        | `1.00:00:00`       | How often the purge runs (it also runs once at startup). |

The purge deletes in chunks of 10,000 rows, so the engine keeps recording events while a large backlog is removed, and then hands the freed pages back to the file system: the database file shrinks. The UI only ever reads the last 120 events per monitor, so `MAXEVENTSPERMONITOR` bounds disk usage without losing anything the dashboard shows.

### Defaults Section

This optional section allows you to define default values that apply to all monitors, or to all monitors of a specific type, unless overridden in a monitor's configuration. Example:

```yaml
defaults:
  intervalWhenUp: 00:01:00    # Check interval when the monitor is UP
  intervalWhenDown: 00:01:00  # Check interval when the monitor is DOWN
  timeout: 00:00:05
  warnTimeout: 00:00:01

  http:
    timeout: 00:00:10
    warnTimeout: 00:00:02
    expectedStatusCode: 202
    ignoreCertificateErrors: true

  dns:
    recordType: AAAA
    resolver: 8.8.8.8

  ping:
    timeout: 00:00:05
    warnTimeout: 00:00:01
```

You can set defaults for each monitor type as follows:

- `intervalWhenUp`, `intervalWhenDown`, `timeout`, `warnTimeout` (global or for each monitor type)
- `expectedStatusCode`, `expectedResponseBodyPattern`, `ignoreCertificateErrors`, `method` (for http only)
- `recordType`, `resolver` (for dns only)

### WARN timeout auto-baseline

`warnTimeout` is optional. When neither a monitor-level value nor a `defaults` value is set, Deucalion derives one continuously from the monitor's recent response-time history:

- Auto-WARN = `P95 × 3`, with a 5 ms floor and a per-monitor-type ceiling (1 s base, 500 ms for `dns` and `ping`).
- The rolling window is the last 60 successful probes; until at least 20 samples are collected, the per-type ceiling is used as a sane fallback.
- An explicit `warnTimeout` (in the monitor or in the `defaults` block) always wins — auto only kicks in when both are unset.

The same threshold drives the sparkline scale in the UI: the chart's Y-axis is anchored at `[0, WARN]`, so a steady probe reads as a flat line near the baseline and a slow one approaches the top.

A `WARN` probe means "up, but slow". It counts as available: it does not advance `ignoreFailCount`, and the monitor keeps polling at `intervalWhenUp` rather than dropping to `intervalWhenDown`.

### Monitors Section

This section defines the individual monitors. Each monitor has a unique name (e.g., `ping-example`) and a type indicated by a YAML tag (e.g., `!ping`).

Monitor names appear in URLs (`/api/monitors/{monitorName}`). The name `events` (in any letter case) is reserved for the event stream endpoint and is rejected at startup.

The following optional parameters are available for all monitors:
- `group`: A string to group monitors together in the UI.
- `href`: URL to link to when the monitor name is clicked.
- `intervalWhenUp`: Check interval when the monitor is UP (except for `checkin`).
- `intervalWhenDown`: Check interval when the monitor is DOWN (except for `checkin`).

#### Monitor Name Interpolation

You can use `${MONITOR_NAME}` in monitor fields to insert the monitor's name dynamically. Example:
```yaml
monitors:
  google: !http
    url: https://${MONITOR_NAME}.com
```
This will set the URL to `https://google.com`.

### Monitor Types

| Type     | Required Fields                  | Optional Fields                                                                |
|----------|----------------------------------|--------------------------------------------------------------------------------|
| ping     | `host`                           | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `href` |
| tcp      | `host`, `port`                   | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `href` |
| dns      | `host`, `recordType`, `resolver` | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `href` |
| http     | `url`                            | `expectedStatusCode`, `expectedResponseBodyPattern`, `ignoreCertificateErrors`, `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `href`, `method` |
| checkin  | *(none)*                         | `secret`, `intervalToDown`, `group`, `href`                                    |

### `ping` Monitor

```yaml
ping-example:
  !ping
  host: cloudflare.com             # Required: The hostname or IP address to ping.
```


### `tcp` Monitor

```yaml
tcp-example:
  !tcp
  host: cloudflare.com             # Required: The hostname or IP address to connect to.
  port: 443                        # Required: The TCP port to connect to.
```


### `dns` Monitor

```yaml
dns-example:
  !dns
  host: google.com                 # Required: The hostname to query.
  recordType: A                    # Required: The DNS record type (e.g., A, AAAA, MX, CNAME).
  resolver: 1.1.1.1:53             # Required: The DNS resolver IP address and port.
```


### `http` Monitor

```yaml
http-example:
  !http
  url: https://google.com          # Required: The URL to request.
  expectedStatusCode: 200          # (Optional) Expected HTTP status code. Defaults to 200-299.
  expectedResponseBodyPattern: .*  # (Optional) Regex pattern to match against the response body.
  ignoreCertificateErrors: true    # (Optional) Set to true to ignore SSL/TLS certificate errors. Defaults to false.
  warnTimeout: 00:00:00.250        # (Optional) Time threshold after which the monitor shows a 'Warning' state. If omitted, derived from history. Format: HH:MM:SS.fff.
  timeout: 00:00:02                # (Optional) Time after which the request is considered failed. Format: HH:MM:SS or HH:MM:SS.fff. Defaults to 00:00:05.
```


### `checkin` Monitor

A passive monitor that waits for an external system to report ("check in") over HTTP.

```yaml
checkin-example:
  !checkin
  secret: your-secret-key          # (Optional) If set, must be sent in the `deucalion-checkin-secret` header.
  intervalToDown: 00:05:00         # (Optional) Time without a check-in before the monitor goes DOWN. Defaults to 00:01:00.
```

Check in with a `POST` to `/api/monitors/{monitorName}/checkin`:

```bash
curl -X POST \
     -H 'deucalion-checkin-secret: your-secret-key' \
     http://localhost:5000/api/monitors/checkin-example/checkin
```

- Only `POST` is accepted -- there is no `GET` form.
- The secret travels in the `deucalion-checkin-secret` **header**, never in the URL.
- `secret` is **optional**. If you omit it no authentication is performed: anyone who can reach
  the endpoint can mark the monitor UP.
- Each check-in marks the monitor UP. If none arrives within `intervalToDown`, it goes DOWN.
- Check-ins are limited to 60 per minute per client address; further ones get `429 Too Many Requests`
  until the minute is over.

# API

Everything the page shows is available as JSON, unauthenticated, with open CORS for reads. An agent (or a human with `curl`) given only the page URL can find it without reading the JavaScript: the served HTML advertises it (`<link rel="alternate" type="application/json" href="/api/status">`, a `<meta name="description">` naming the endpoint, and a `<noscript>` pointer), `GET /` with `Accept: application/json` returns the summary directly, and `/llms.txt` is a one-screen description of the whole API.

| Endpoint | Returns |
|----------|---------|
| `GET /api/status` | Self-describing summary: overall `status` (`operational` -- nothing down; `degraded` -- some monitors down; `outage` -- every monitor down), `updatedAt`, overall `availability` (%), one entry per monitor (`name`, `group`, `type`, `state` as `up` / `warn` / `down` / `degraded` / `unknown`, `since`, `availability`, `latencyMs`), and `links` to the other endpoints. Timestamps are ISO-8601 UTC. Start here. |
| `GET /` with `Accept: application/json` | The same document as `/api/status` (responses carry `Vary: Accept`; a browser's Accept header still gets the HTML). |
| `GET /api/version` | `name`, `version` (build number and git SHA), `runtime`, `startedAt` -- tells you which build a deployment is actually running. |
| `GET /api/monitors` | Full detail per monitor as the UI consumes it: `config`, rolling `stats` (last 60 probes), and the recent `events` in compact form (`at` unix seconds, `st` numeric state, `ms` latency). |
| `GET /api/monitors/{name}` | One monitor in the same shape. Unknown names return `404` `application/problem+json`. |
| `GET /api/monitors/events` | Server-Sent Events stream: `MonitorChecked` (`n`, `at`, `fr`, `st`, `ms`, `ns`) on every probe and `MonitorStateChanged` (`n`, `at`, `fr`, `st`) on transitions. |
| `POST /api/monitors/{name}/checkin` | Heartbeat for `checkin` monitors -- see [`checkin` Monitor](#checkin-monitor). |
| `GET /llms.txt` | Plain-Markdown description of the above, for agents that look for it. |

Numeric states in the compact payloads: `0` unknown, `1` down, `2` up, `3` warn, `4` degraded.

```bash
curl -s http://localhost:5000/api/status
curl -s -H 'Accept: application/json' http://localhost:5000/
curl -s http://localhost:5000/api/version
```

The Docker image declares a `HEALTHCHECK` that runs `Deucalion.Service --healthcheck`, which probes `/api/version` on the container's own port (`ASPNETCORE_HTTP_PORTS`, default `8080`) and exits `0` on a 2xx.

# Development notes

## Project guidelines:
  - Configuration files over CRUD forms
  - Layered architecture with an acyclic dependency graph (see below). This is
    *not* Hexagonal Architecture: the engine and the API switch on the concrete
    monitor types rather than talking to them through an abstraction.
  - [K.I.S.S.](https://en.wikipedia.org/wiki/KISS_principle)
  - [Do One Thing And Do It Well](https://en.wikipedia.org/wiki/Unix_philosophy): Not a "Status Page" (with incidents, justifications, etc)

## Projects overview:

Six .NET projects, one SPA. Dependencies only point downwards:

```
Deucalion.Service
  └─ Deucalion.Api ──────────┬─ Deucalion.Application ─ Deucalion.Network ─ Deucalion.Core
                             └─ Deucalion.Storage ───────────────────────── Deucalion.Core
```

  - `Deucalion.Core`: The domain, with no package references. `PullMonitor`,
    `MonitorState`, `MonitorResponse`, the monitor events, the base
    `PullMonitorConfiguration` record, and the storage port (`IStorage`,
    `MonitorStats`, `StoredEvent`, in the `Deucalion.Storage` namespace).
  - `Deucalion.Network`: The five monitor implementations (`ping`, `tcp`, `dns`,
    `http`, `checkin`) and their configuration records.
  - `Deucalion.Application`: YAML parsing and validation of `deucalion.yaml`,
    building live monitors from it, the polling engine (`RunAllAsync`) and the
    auto-WARN policy. Knows every concrete monitor type.
  - `Deucalion.Storage`: `SqliteStorage`, the one `IStorage` implementation.
  - `Deucalion.Api`: ASP.NET Core endpoints, wire DTOs, the SSE broadcaster and
    the background services. Also the composition root: it wires
    `SqliteStorage` to `IStorage` and registers the built monitors.
  - `Deucalion.Service`: Host for `Deucalion.Api`, Native AOT published. Can run
    as a Windows Service.
  - `Deucalion.Tests`: xUnit tests.
  - `deucalion-ui`: Client-side SolidJS single-page application.

### Adding a monitor type

The monitor types are enumerated by hand in a few well-known places. A new type
(`foo`) touches all of them:

  1. `Deucalion.Network/Configuration/FooMonitorConfiguration.cs` -- the YAML
     record, derived from `PullMonitorConfiguration`.
  2. `Deucalion.Network/Monitors/FooMonitor.cs` -- the `PullMonitor`.
  3. `Deucalion.Application/Configuration/DeucalionYamlContext.cs` --
     `[YamlSerializable]` and the `!foo` `[YamlDerivedTypeMapping]`.
  4. `Deucalion.Application/Configuration/ConfigurationExtensions.cs` -- a
     `Build()` overload and its arm in `MonitorFromConfiguration`.
  5. `Deucalion.Application/Configuration/ApplicationConfiguration.cs` -- the
     `ConfigurationDefaults.Foo` block, `ApplyDefaults`, `InterpolateMonitorName`
     and the tag list in `Messages.ConfigurationUnknownMonitorType`.
  6. `Deucalion.Api/Models/MonitorConfigurationDto.cs` -- `ExtractType`.
  7. `deucalion-ui/src/services/deucalion-types.ts` (`MonitorType`) and the
     `.type-badge.t-foo` colour in `styles/layout.css`.
  8. This README: the monitor's section and `deucalion-sample.yaml`.

## How to debug

### Using Visual Studio 2022

Open `Deucalion.sln` with Visual Studio 2022.

Start both `Deucalion.Api` and `deucalion-ui` projects. You may [set multiple startup projects](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects) for this.

> Do not use `Deucalion.Service` for debugging. It uses a static (pre-built) version of the UI (you need to run `Invoke-Build Build` first).

### Using Visual Studio Code

Run

```powershell
Invoke-Build Dev
```

This will start both `Deucalion.Api` and `deucalion-ui` projects in development mode. Any changes to source files will be detected and reloaded automatically.

### Watching the event stream

The server publishes every monitor event over Server-Sent Events. To tail it from a terminal:

```bash
curl -N http://localhost:5000/api/monitors/events
```

## Logging

In the **Development** environment, the log level for the `Deucalion.Api` namespace is set to `Debug`. This generates a log entry for each message received from `EngineBackgroundService`.

For **Production** environments, the log level is `Information` (the default). To change this, you can run the application with

`--Logging:LogLevel:Deucalion=Debug`

in the command line, or change the appropriate value in `appsettings.json`.

## How to build

Install [`Invoke-Build`](https://github.com/nightroman/Invoke-Build).

`Invoke-Build` or `Invoke-Build build` will put all artifacts in the `./publish` folder.

The version is computed by GitVersion, restored from the repo-local tool manifest. When it cannot run (no git history, a shallow clone, a source tarball) the build warns and uses `0.0.0-dev` instead of failing.

`Invoke-Build test` runs the .NET unit tests (`dotnet test -c Release`, with a minimum-expected-tests floor so a silent zero-test run fails) and the frontend unit tests (Vitest). Set `DEUCALION_TESTS_NETWORK=1` to include the DNS/ICMP tests that need the public internet; CI runs them weekly.

End-to-end tests are separate -- they boot both servers themselves:

```powershell
npm --prefix ./src/ts/deucalion-ui run test:e2e
```

# License

[MIT](LICENSE).
