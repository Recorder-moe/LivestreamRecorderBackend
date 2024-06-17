# syntax=docker/dockerfile:1

# From the base image
ARG UID=app
ARG APP_UID=1654

ARG VERSION=EDGE
ARG RELEASE=0
ARG BUILD_CONFIGURATION=ApacheCouchDB_Release

########################################
# Base stage
########################################
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0-slim AS base

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

WORKDIR /app

# Install under /root/.local
ENV PIP_USER="true"
ARG PIP_NO_WARN_SCRIPT_LOCATION=0
ARG PIP_ROOT_USER_ACTION="ignore"
ARG PIP_NO_COMPILE="true"
ARG PIP_DISABLE_PIP_VERSION_CHECK="true"

RUN --mount=type=cache,id=apt-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/var/cache/apt \
    --mount=type=cache,id=aptlists-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/var/lib/apt/lists \
    apt-get update && apt-get install -y --no-install-recommends \
    python3.12 \
    # Cleanup
    find "/root/.local" -name '*.pyc' -print0 | xargs -0 rm -f || true ; \
    find "/root/.local" -type d -name '__pycache__' -print0 | xargs -0 rm -rf || true ;

ARG UID
# ffmpeg
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/static-ffmpeg-upx:7.0-1 /ffmpeg /usr/bin/
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/static-ffmpeg-upx:7.0-1 /ffprobe /usr/bin/

# yt-dlp
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/yt-dlp:distroless /home/monty/.local /usr/bin/

########################################
# Build stage
########################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /source

ARG TARGETARCH
RUN --mount=source=LivestreamRecorderBackend.csproj,target=LivestreamRecorderBackend.csproj \
    --mount=source=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj,target=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj \
    dotnet restore -a $TARGETARCH "LivestreamRecorderBackend.csproj"

########################################
# Publish stage
########################################
FROM build AS publish

ARG BUILD_CONFIGURATION

ARG TARGETARCH
RUN --mount=source=.,target=.,rw \
    dotnet publish "LivestreamRecorderBackend.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app

########################################
# Final stage
########################################
FROM base as final

ARG UID
# Support arbitrary user ids (OpenShift best practice)
# https://docs.openshift.com/container-platform/4.14/openshift_images/create-images.html#use-uid_create-images
RUN chown -R $UID:0 /azure-functions-host && \
    chmod -R g=u /azure-functions-host

# Create directories with correct permissions
RUN install -d -m 775 -o $UID -g 0 /home/site/wwwroot && \
    install -d -m 775 -o $UID -g 0 /licenses

# dumb-init
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/static-ffmpeg-upx:7.0-1 /dumb-init /usr/bin/

COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/tarampampam/curl:8.8.0 /bin/curl /bin/curl
HEALTHCHECK --interval=10s --timeout=2s --retries=3 --start-period=10s CMD [ \
    "curl", "--fail", "http://127.0.0.1:8080/api/Utility/Wake/" \
    ]

# Copy licenses (OpenShift Policy)
COPY --link --chown=$UID:0 --chmod=775 LICENSE /licenses/LICENSE
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/yt-dlp:distroless /licenses/yt-dlp.LICENSE /licenses/yt-dlp.LICENSE

# Copy dist
COPY --link --chown=$UID:0 --chmod=775 --from=publish /app /home/site/wwwroot

ENV PATH="/home/site/wwwroot:/home/$UID/.local/bin:$PATH"
ENV PYTHONPATH "/home/$UID/.local/lib/python3.12/site-packages:${PYTHONPATH}"

ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# Set this to the connection string for the online storage account or the local emulator
# https://learn.microsoft.com/zh-tw/azure/storage/common/storage-use-azurite#http-connection-strings
ENV AzureWebJobsStorage=

# Issue: Azure Durable Function HttpStart failure: Webhooks are not configured
# https://stackoverflow.com/a/64404153/8706033
ENV WEBSITE_HOSTNAME=localhost:8080

ENV CORS_SUPPORT_CREDENTIALS=true
ENV CORS_ALLOWED_ORIGINS=["https://localhost:4200"]
ENV FrontEndUri=https://localhost:4200

ENV Registration_allowed=false
ENV Seq_ServerUrl=
ENV Seq_ApiKey=

ENV StorageService=
ENV Blob_ConnectionString=
ENV Blob_ContainerNamePublic=
ENV Blob_ContainerNamePrivate=
ENV S3_Endpoint=
ENV S3_AccessKey=
ENV S3_SecretKey=
ENV S3_Secure=
ENV S3_BucketNamePrivate=
ENV S3_BucketNamePublic=

ENV CosmosDB_Private_ConnectionString=
ENV CosmosDB_Public_ConnectionString=
ENV CouchDB_Endpoint=
ENV CouchDB_Username=
ENV CouchDB_Password=

ENV GITHUB_PROVIDER_AUTHENTICATION_ID=
ENV GITHUB_PROVIDER_AUTHENTICATION_SECRET=

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER $UID

STOPSIGNAL SIGINT

# Use dumb-init as PID 1 to handle signals properly
ENTRYPOINT [ "dumb-init", "--", "/opt/startup/start_nonappservice.sh" ]

ARG VERSION
ARG RELEASE
LABEL name="Recorder-moe/LivestreamRecorderBackend" \
    # Authors for LivestreamRecorderBackend
    vendor="Recorder-moe" \
    # Maintainer for this docker image
    maintainer="jim60105" \
    # Dockerfile source repository
    url="https://github.com/Recorder-moe/LivestreamRecorderBackend" \
    version=${VERSION} \
    # This should be a number, incremented with each change
    release=${RELEASE} \
    io.k8s.display-name="LivestreamRecorderBackend" \
    summary="LivestreamRecorderBackend: The backend azure function for the Recorder.moe project." \
    description="Recorder.moe is an advanced live stream recording system. We utilize containerization technology to achieve horizontal scalability, enabling us to monitor and record an unlimited number of channels simultaneously. For more information about this tool, please visit the following website: https://github.com/Recorder-moe"
