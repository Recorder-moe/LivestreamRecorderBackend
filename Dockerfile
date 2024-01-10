# syntax=docker/dockerfile:1
ARG UID=1001
ARG DatabaseService="ApacheCouchDB"

### Build Python
FROM debian:11 as build-python

RUN apt-get update && apt-get install -y --no-install-recommends python3=3.9.2-3 python3-pip && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

WORKDIR /app

# Install under /root/.local
ENV PIP_USER="true"
ARG PIP_NO_WARN_SCRIPT_LOCATION=0
ARG PIP_ROOT_USER_ACTION="ignore"

RUN --mount=type=cache,id=pip-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/root/.cache/pip \
    pip3 install yt-dlp && \
    # Cleanup
    find "/root/.local" -name '*.pyc' -print0 | xargs -0 rm -f || true ; \
    find "/root/.local" -type d -name '__pycache__' -print0 | xargs -0 rm -rf || true ;

### Build .NET
FROM  mcr.microsoft.com/dotnet/sdk:6.0 AS build-dotnet

WORKDIR /src

RUN --mount=source=LivestreamRecorderBackend.csproj,target=LivestreamRecorderBackend.csproj \
    --mount=source=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj,target=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj \
    dotnet restore "LivestreamRecorderBackend.csproj"

ARG DatabaseService
ARG BUILD_CONFIGURATION=${DatabaseService}_Release

RUN --mount=source=.,target=.,rw \
    dotnet publish "LivestreamRecorderBackend.csproj" -c $BUILD_CONFIGURATION -o /app/publish

### Final image
FROM mcr.microsoft.com/azure-functions/dotnet:4

RUN apt-get update && apt-get install -y --no-install-recommends python3=3.9.2-3 dumb-init=1.2.5-1 && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ARG UID

EXPOSE 80

ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
ENV CORS_SUPPORT_CREDENTIALS=true
ENV FUNCTIONS_WORKER_RUNTIME=dotnet
ENV CORS_ALLOWED_ORIGINS=["https://localhost:4200"]
ENV FrontEndUri=https://localhost:4200
ENV AzureWebJobsStorage=
ENV Seq_ServerUrl=
ENV Seq_ApiKey=
ENV Registration_allowed=true
ENV GITHUB_PROVIDER_AUTHENTICATION_ID=
ENV GITHUB_PROVIDER_AUTHENTICATION_SECRET=
ENV StorageService=
ENV Blob_ConnectionString=
ENV Blob_ContainerName=
ENV Blob_ContainerNamePublic=
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

# Support arbitrary user ids (OpenShift best practice)
# https://docs.openshift.com/container-platform/4.14/openshift_images/create-images.html#use-uid_create-images
RUN chgrp -R 0 /azure-functions-host && \
    chmod -R g=u /azure-functions-host

# Create user
RUN groupadd -g $UID $UID && \
    useradd -l -g $UID -u $UID -G $UID $UID

COPY --chown=$UID:0 --chmod=774 \
    --from=build-python /root/.local /home/$UID/.local
ENV PATH="/home/$UID/.local/bin:$PATH"
ENV PYTHONPATH "/home/$UID/.local/lib/python3.9/site-packages" 

COPY --chown=$UID:0 --chmod=774 \
    --from=build-dotnet /app/publish /home/site/wwwroot

USER $UID

ENTRYPOINT [ "dumb-init", "--", "/opt/startup/start_nonappservice.sh" ]