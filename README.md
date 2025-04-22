# Deucalion

Minimal project for systems monitoring. When Grafana + Prometheus is overkill.

This is not a "Status Page" project. There is no intention to add alerts, incident histories, push notifications, or CRUD UIs to configure everything.

Just create a configuration file, start the service, and you are done.

![Deucalion UI example](deucalion-ui.png)



# Usage

## Quick start

```yaml
# docker-compose.yaml
version: "3.9"

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
      - ./example.yaml:/app/deucalion.yaml
      - ./data/:/storage/
```

```yaml
# example.yaml
monitors:
  ping-example:
    !ping
    host: cloudflare.com
    intervalWhenUp: 00:00:03
    group: Cloudflare

  tcp-example:
    !tcp
    host: cloudflare.com
    port: 443
    intervalWhenUp: 00:00:03
    group: Cloudflare

  dns-example:
    !dns
    host: google.com
    recordType: A
    resolver: 1.1.1.1:53
    intervalWhenUp: 00:00:03
    group: Google

  http-example:
    !http
    url: https://google.com
    expectedStatusCode: 200
    expectedResponseBodyPattern: .*
    ignoreCertificateErrors: true
    intervalWhenUp: 00:00:03
    group: Google
```



## Configuration

The monitoring behavior is defined in a YAML configuration file (e.g., `deucalion.yaml`).

### `defaults` Section

This optional section allows you to define default values that apply to all monitors unless overridden in a specific monitor's configuration.

```yaml
defaults:
  intervalWhenUp: 00:00:03   # Default check interval when the monitor is UP
  intervalWhenDown: 00:00:03 # Default check interval when the monitor is DOWN
```

### `monitors` Section

This section defines the individual monitors. Each monitor has a unique name (e.g., `ping-example`) and a type indicated by a YAML tag (e.g., `!ping`).

The following parameters are available for most monitors:
- `group`: A string to group monitors together in the UI.
- `intervalWhenUp`: Check interval when the monitor is UP. Overrides the default. Format: `HH:MM:SS` or `HH:MM:SS.fff`.
- `intervalWhenDown`: Check interval when the monitor is DOWN. Overrides the default. Format: `HH:MM:SS` or `HH:MM:SS.fff`.
- `image`: URL for a custom icon to display for the monitor.
- `href`: URL to link to when the monitor name is clicked. Set to `""` to disable the link.

#### `!ping` Monitor

```yaml
ping-example:
  !ping
  host: cloudflare.com             # Required: The hostname or IP address to ping.
```

#### `!tcp` Monitor

```yaml
tcp-example:
  !tcp
  host: cloudflare.com             # Required: The hostname or IP address to connect to.
  port: 443                        # Required: The TCP port to connect to.
```

#### `!dns` Monitor

```yaml
dns-example:
  !dns
  host: google.com                 # Required: The hostname to query.
  recordType: A                    # Required: The DNS record type (e.g., A, AAAA, MX, CNAME).
  resolver: 1.1.1.1:53             # Required: The DNS resolver IP address and port.
```

#### `!http` Monitor

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

#### `!checkin` Monitor

A passive monitor that waits for an external system to report ("check-in") via a specific URL.

```yaml
checkin-example:
  !checkin
  secret: your-secret-key          # Required: A secret key that must be provided in the check-in request.
  # Common parameters (intervalWhenUp defines the expected check-in frequency)...
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

> Do not use `Deucalion.Service` for debugging. It uses a static (pre-built) version of UI (you need to run `Invoke-Build Build` first).



### Using Visual Studio Code

Run

```powershell
Invoke-Build Dev
```

This will start both `Deucalion.Api` and `deucalion-ui` projects in development mode. Any source file changes will be detected and reloaded.



## Logging

In **Development** environment the log level for `Deucalion.Api` namespace is `Debug`. This generates a log entry for each message received from `EngineBackgroundService`.

For **Production** environments the log level is `Information` (the default). To change this, you can run the application with

`--Logging:LogLevel:Deucalion=Debug`

in command-line. Or change the appropriate value in `appsettings.json`.



## How to build

Install [`Invoke-Build`](https://github.com/nightroman/Invoke-Build).

`Invoke-Build` or `Invoke-Build build` will put all artifacts in the `./publish` folder.

`Invoke-Build test` will run the unit tests using `dotnet test`.
