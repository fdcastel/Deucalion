# Reference: https://github.com/dotnet/dotnet-docker/blob/main/samples/aspnetapp/Dockerfile

# build image for .net
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-net
ARG TARGETARCH
ARG VERSION=0.0.0-dev
ARG INFORMATIONAL_VERSION=0.0.0-dev
WORKDIR /source

# Install native AOT prerequisites
RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY cs/Directory.Build.props ./
COPY cs/Deucalion.Api/*.csproj ./Deucalion.Api/
COPY cs/Deucalion.Application/*.csproj ./Deucalion.Application/
COPY cs/Deucalion.Core/*.csproj ./Deucalion.Core/
COPY cs/Deucalion.Network/*.csproj ./Deucalion.Network/
COPY cs/Deucalion.Storage/*.csproj ./Deucalion.Storage/
COPY cs/Deucalion.Service/*.csproj ./Deucalion.Service/

RUN dotnet restore Deucalion.Service -a $TARGETARCH

COPY cs/ .
WORKDIR /source/Deucalion.Service
RUN dotnet publish -c release -o /app \
    -p:Version=${VERSION} \
    -p:InformationalVersion=${INFORMATIONAL_VERSION} \
    -p:DebugType=None --no-restore -a $TARGETARCH

# build image for NodeJs
FROM --platform=$BUILDPLATFORM node:lts-alpine AS build-node
WORKDIR /source

COPY ts/deucalion-ui/package*.json .
RUN npm ci

COPY ts/deucalion-ui/ .

RUN npm run build

# final image -- AOT produces a native binary, no .NET runtime needed
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

ENV DEUCALION__CONFIGURATIONFILE="/app/deucalion.yaml" \
    DEUCALION__STORAGEPATH="/storage" \
    DEUCALION__PAGETITLE="Deucalion Status"

COPY --from=build-net /app ./
COPY --from=build-node /source/dist ./wwwroot/

# SqliteStorage creates its database under /storage at startup; the directory
# must exist and be writable by the non-root user before USER switches to it.
RUN mkdir -p /storage && chown $APP_UID:$APP_UID /storage
VOLUME /storage

EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s CMD ["./Deucalion.Service", "--healthcheck"]
ENTRYPOINT ["./Deucalion.Service"]
