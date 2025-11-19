# Reference: https://github.com/dotnet/dotnet-docker/blob/main/samples/aspnetapp/Dockerfile

# build image for .net
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build-net
ARG TARGETARCH
ARG VERSION=0.0.0-dev
ARG INFORMATIONAL_VERSION=0.0.0-dev
WORKDIR /source

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
ARG VERSION=0.0.0-dev
ARG INFORMATIONAL_VERSION=0.0.0-dev
WORKDIR /source

COPY ts/deucalion-ui/package*.json .
RUN npm ci

COPY ts/deucalion-ui/ .

ENV VITE_BUILD_VERSION=$VERSION
ENV VITE_INFORMATIONAL_VERSION=$INFORMATIONAL_VERSION

RUN npm run build

# final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG VERSION=0.0.0-dev
ARG INFORMATIONAL_VERSION=0.0.0-dev
WORKDIR /app

ENV DEUCALION__CONFIGURATIONFILE="/app/deucalion.yaml" \
    DEUCALION__STORAGEPATH="/storage" \
    DEUCALION__PAGETITLE="Deucalion Status"

COPY --from=build-net /app ./
COPY --from=build-node /source/dist ./wwwroot/

USER $APP_UID
ENTRYPOINT ["dotnet", "Deucalion.Service.dll"]
