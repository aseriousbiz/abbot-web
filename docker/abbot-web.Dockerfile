FROM mcr.microsoft.com/dotnet/sdk:7.0.103 as build
ARG BUILD_BRANCH=""
ARG BUILD_SHA=""
ARG BUILD_HEAD_REF=""
ARG BUILD_PR=""

# We need node to build
COPY ./docker/files/99-apt-preferences /etc/apt/preferences.d/99-apt-preferences
RUN apt-cache policy
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        gnupg \
        build-essential \
    && curl -sL https://deb.nodesource.com/setup_16.x | bash - \
    && apt-get install -y nodejs

# Install .NET runtime analysis tools
RUN mkdir /tools && \
    curl -sSL -o /tools/dotnet-dump https://aka.ms/dotnet-dump/linux-x64 && \
    curl -sSL -o /tools/dotnet-counters https://aka.ms/dotnet-counters/linux-x64 && \
    curl -sSL -o /tools/dotnet-gcdump https://aka.ms/dotnet-gcdump/linux-x64 && \
    curl -sSL -o /tools/dotnet-trace https://aka.ms/dotnet-trace/linux-x64 && \
    curl -sSL -o /tools/dotnet-stack https://aka.ms/dotnet-stack/linux-x64 && \
    curl -sSL -o /tools/perfcollect https://aka.ms/perfcollect && \
    chmod +x /tools/*

WORKDIR /src
COPY . .
RUN dotnet restore Abbot.sln --locked-mode

ENV BUILD_BRANCH=${BUILD_BRANCH}
ENV BUILD_SHA=${BUILD_SHA}
ENV BUILD_HEAD_REF=${BUILD_HEAD_REF}
ENV BUILD_PR=${BUILD_PR}

RUN dotnet publish src/product/Abbot.Web -c Release --no-restore --output /output
RUN echo "${BUILD_BRANCH}\n${BUILD_SHA}" > "/output/build_info.txt"

FROM mcr.microsoft.com/dotnet/aspnet:7.0 as run

WORKDIR /app

COPY ./docker/files/abbot-web-entrypoint.sh .
COPY ./docker/files/sshd_config /etc/ssh/sshd_config
COPY ./docker/files/dump-abbot /tools/dump-abbot

# Install and configure SSH
# Don't be scared by the root password set here. It's what Azure App Service uses to connect to SSH in the container.
# The SSH port is NOT exposed to the outside world _EXCEPT_ through App Service's authenticated tunnel
RUN apt-get update \
    && apt-get install -y --no-install-recommends dialog \
    && apt-get install -y --no-install-recommends openssh-server \
    && echo "root:Docker!" | chpasswd \
    && chmod u+x ./abbot-web-entrypoint.sh \
    && chmod u+x /tools/dump-abbot

COPY --from=build ./output /app
COPY --from=build /tools /tools
COPY --from=mcr.microsoft.com/dotnet/monitor /app/ /tools/dotnet-monitor/

EXPOSE 80 2222

# We use the exec form of ENTRYPOINT so that we can pass in arguments when we 'docker run'
ENTRYPOINT [ "./abbot-web-entrypoint.sh" ]
