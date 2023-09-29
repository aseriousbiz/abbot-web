FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# The value for this is provided automatically by 'docker build'
ARG TARGETPLATFORM

# Install .NET 6.0 Runtime manually, since we need it to run the Azure Functions tools.
# This logic is based on the logic in https://github.com/dotnet/dotnet-docker/blob/7a8368250a32baf4038bd809ebc7381e8db61bc8/src/runtime/6.0/bullseye-slim/amd64/Dockerfile
# But we need to support building on arm64 (for our MacBooks).
RUN if [ "${TARGETPLATFORM}" = "linux/arm64" ]; then \
        curl -fSL --output dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Runtime/6.0.9/dotnet-runtime-6.0.9-linux-arm64.tar.gz; \
    else \
        curl -fSL --output dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Runtime/6.0.9/dotnet-runtime-6.0.9-linux-x64.tar.gz; \
    fi
RUN tar -oxzf dotnet.tar.gz -C /usr/share/dotnet

ARG BUILD_BRANCH=""
ARG BUILD_SHA=""
ARG BUILD_HEAD_REF=""
ARG BUILD_PR=""

WORKDIR /src
COPY . .
RUN dotnet restore Abbot.sln --locked-mode

ENV BUILD_BRANCH=${BUILD_BRANCH}
ENV BUILD_SHA=${BUILD_SHA}
ENV BUILD_HEAD_REF=${BUILD_HEAD_REF}
ENV BUILD_PR=${BUILD_PR}

RUN dotnet publish src/functions/Abbot.Functions.DotNet -c Release --no-restore --output /output
RUN echo "${BUILD_BRANCH}\n${BUILD_SHA}" > "/output/build_info.txt"

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated7.0-appservice
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated7.0-slim

# Upgrade any OS packages with outstanding upgrades, to ensure we've got security fixes.
RUN apt-get update && apt-get upgrade -qyy && rm -rf /var/lib/apt/lists/*

ENV \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=true \
    FUNCTIONS_EXTENSION_VERSION=~4 \
    ASPNETCORE_URLS=http://+:8080 \
    AbbotApiBaseUrl=https://ab.bot/api

EXPOSE 8080
COPY --from=build /output /home/site/wwwroot