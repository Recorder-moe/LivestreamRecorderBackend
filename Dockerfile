FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

ARG DatabaseService="ApacheCouchDB"

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj --output /home/site/wwwroot --configuration ${DatabaseService}_Release

FROM mcr.microsoft.com/azure-functions/dotnet:4

COPY --from=mwader/static-ffmpeg:6.0 /ffmpeg /usr/local/bin/
COPY --from=mwader/static-ffmpeg:6.0 /ffprobe /usr/local/bin/

RUN apt-get update && apt-get install -y --no-install-recommends python3 python3-dev python3-distutils python3-pip build-essential aria2 && \
    pip install --upgrade yt-dlp mutagen pycryptodomex websockets brotli certifi && \
    apt-get remove --purge -y build-essential python3-dev python3-distutils python3-pip && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

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
ENV ConnectionStrings_Private=
ENV ConnectionStrings_Public=
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
ENV CouchDB_Endpoint=
ENV CouchDB_Username=
ENV CouchDB_Password=

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]