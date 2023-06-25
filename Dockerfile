FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj --output /home/site/wwwroot --configuration Release

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet:4-appservice
FROM mcr.microsoft.com/azure-functions/dotnet:4
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]

EXPOSE 80

ENV CORS_SUPPORT_CREDENTIALS=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet \
    CORS_ALLOWED_ORIGINS=["https://localhost:4200"] \
    FrontEndUri=https://localhost:4200 \
    AzureWebJobsStorage= \
    Seq_ServerUrl= \
    Seq_ApiKey= \
    ConnectionStrings_Private= \
    ConnectionStrings_Public= \
    Blob_ConnectionString= \
    Blob_ContainerName= \
    Blob_ContainerNamePublic= \
    Registration_allowed=true \
    GITHUB_PROVIDER_AUTHENTICATION_ID= \
    GITHUB_PROVIDER_AUTHENTICATION_SECRET=