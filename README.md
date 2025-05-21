# Deucalion

A minimal project for systems monitoring, designed for cases where Grafana and Prometheus are overkill.

This is not a typical "Status Page" project. There are no alerts, incident histories, push notifications, or CRUD UIs for configuration.

Simply create a configuration file, start the service, and you're done.

![Deucalion UI example](deucalion-ui.apng)

# Table of Contents

- [Quick start](#quick-start)
- [Configuration](#configuration)
  - [Defaults Section](#defaults-section)
  - [Monitors Section](#monitors-section)
  - [Monitor Name Interpolation](#monitor-name-interpolation)
- [Monitor Types](#monitor-types)
  - [`ping` Monitor](#ping-monitor)
  - [`tcp` Monitor](#tcp-monitor)
  - [`dns` Monitor](#dns-monitor)
  - [`http` Monitor](#http-monitor)
  - [`checkin` Monitor](#checkin-monitor)
- [Development notes](#development-notes)
  - [How to debug](#how-to-debug)
  - [Logging](#logging)
  - [How to build](#how-to-build)

# Prerequisites

- Docker (for containerized usage)
- .NET 9 SDK (for development and building)
- PowerShell (for build scripts)
- [Invoke-Build](https://github.com/nightroman/Invoke-Build) (for build automation)

# Usage

## Quick start

```yaml
# docker-compose.yaml
services:
  deucalion:
    user: root
    container_name: deucalion
    image: ghcr.io/fdcastel/deucalion:latest
    ports:
      - 80:8080
    environment:
      - DEUCALION__PAGETITLE=Deucalion status
    volumes:
      - ./example.yaml:/app/example.yaml  # Rename or copy your configuration file as needed.
      - ./data/:/storage/
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

### Monitors Section

This section defines the individual monitors. Each monitor has a unique name (e.g., `ping-example`) and a type indicated by a YAML tag (e.g., `!ping`).

The following optional parameters are available for all monitors:
- `group`: A string to group monitors together in the UI.
- `image`: URL for a custom icon to display for the monitor.
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
| ping     | `host`                           | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `image`, `href` |
| tcp      | `host`, `port`                   | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `image`, `href` |
| dns      | `host`, `recordType`, `resolver` | `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `image`, `href` |
| http     | `url`                            | `expectedStatusCode`, `expectedResponseBodyPattern`, `ignoreCertificateErrors`, `timeout`, `warnTimeout`, `intervalWhenUp`, `intervalWhenDown`, `group`, `image`, `href`, `method` |
| checkin  | `secret`                         | `intervalWhenUp`, `group`, `image`, `href`                                      |

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
  warnTimeout: 00:00:00.250        # (Optional) Time threshold after which the monitor shows a 'Warning' state. Format: HH:MM:SS.fff.
  timeout: 00:00:02                # (Optional) Time after which the request is considered failed. Format: HH:MM:SS or HH:MM:SS.fff. Defaults to 00:00:05.
```


### `checkin` Monitor

A passive monitor that waits for an external system to report ("check in") via a specific URL.

```yaml
checkin-example:
  !checkin
  secret: your-secret-key          # Required: A secret key that must be provided in the check-in request.
```

- The check-in URL is `/api/checkin/{monitorName}/{secret}`.
  - For the example above, it would be `/api/checkin/checkin-example/your-secret-key`.
- A `GET` or `POST` request to this URL marks the monitor as UP.
- If a check-in is not received within the expected interval, the monitor is marked as DOWN.

# Development notes

## Project guidelines:
  - Configuration files over CRUD forms
  - [Hexagonal Architecture](https://en.wikipedia.org/wiki/Hexagonal_architecture_(software))
  - [K.I.S.S.](https://en.wikipedia.org/wiki/KISS_principle)
  - [Do One Thing And Do It Well](https://en.wikipedia.org/wiki/Unix_philosophy): Not a "Status Page" (with incidents, justifications, etc)

## Projects overview:

  - `Deucalion.Core`: Base types and events shared between servers and clients.
  - `Deucalion.Application`: Core engine and configuration.
  - `Deucalion.Network`: Base network monitors.
  - `Deucalion.Storage`: Persistence and statistics.
  - `Deucalion.Api`: Server-side ASP.NET Web API application.
  - `Deucalion.Service`: Service Host for `Deucalion.Api`. Can run as a Windows Service.
  - `Deucalion.Tests`: xUnit tests.
  - `Deucalion.Cli`: Sample command-line SignalR client.
  - `deucalion-ui`: Client-side React application.

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

## Logging

In the **Development** environment, the log level for the `Deucalion.Api` namespace is set to `Debug`. This generates a log entry for each message received from `EngineBackgroundService`.

For **Production** environments, the log level is `Information` (the default). To change this, you can run the application with

`--Logging:LogLevel:Deucalion=Debug`

in the command line, or change the appropriate value in `appsettings.json`.

## How to build

Install [`Invoke-Build`](https://github.com/nightroman/Invoke-Build).

`Invoke-Build` or `Invoke-Build build` will put all artifacts in the `./publish` folder.

`Invoke-Build test` will run the unit tests using `dotnet test`.
