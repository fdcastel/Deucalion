# Deucalion

Minimal project for systems monitoring. When Grafana + Prometheus is overkill.

This is not a "Status Page" project. There is no intention to add alerts, incident histories, push notifications, or CRUD UIs to configure everything.

Just put up a simple configuration file, start the service, and you are done.

![Deucalion UI example](deucalion-ui.png)



# Usage

For a quick start:

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
  - `Deucalion.Api`: Server-side Asp.Net Web Api application.
  - `Deucalion.Service`: Service Host for `Deucalion.Api`. Can run as a Service on Windows.
  - `deucalion-ui`: Client-side React application.
  - `Deucalion.Tests`: xUnit tests.
  - `Deucalion.Cli`: Sample command-line SignalR client.



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

`Invoke-Build`  or `Invoke-Build build` will put all artifacts in `./publish` folder.

`Invoke-Build test` will build and start a production version using `./deucalion-sample.yaml` configuration.
