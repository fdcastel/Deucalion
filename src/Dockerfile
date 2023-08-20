# build image for .net
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-net
WORKDIR /source

COPY cs/Deucalion.Api/*.csproj ./Deucalion.Api/
COPY cs/Deucalion.Application/*.csproj ./Deucalion.Application/
COPY cs/Deucalion.Core/*.csproj ./Deucalion.Core/
COPY cs/Deucalion.Network/*.csproj ./Deucalion.Network/
COPY cs/Deucalion.Storage/*.csproj ./Deucalion.Storage/
COPY cs/Deucalion.Service/*.csproj ./Deucalion.Service/

RUN dotnet restore Deucalion.Service

COPY cs/ .
WORKDIR /source/Deucalion.Service
RUN dotnet publish -c release -o /app -p:DebugType=None --no-restore

# build image for NodeJs
FROM node:lts-alpine AS build-node
WORKDIR /source

RUN npm install -g vite

COPY ts/deucalion-ui/package*.json .
RUN npm ci

COPY ts/deucalion-ui/ .
RUN npm run build

# final image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

ENV DEUCALION__CONFIGURATIONFILE="/app/deucalion.yaml" \
    DEUCALION__STORAGEPATH="/storage" \
    DEUCALION_PAGE_TITLE="Deucalion Status" \
    DEUCALION_API_URL=""

COPY --from=build-net /app ./
COPY --from=build-node /source/dist ./wwwroot/

ENTRYPOINT ["dotnet", "Deucalion.Service.dll"]
