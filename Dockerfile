FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj --output /home/site/wwwroot --configuration Release

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet:4-appservice
FROM mcr.microsoft.com/azure-functions/dotnet:4

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
ENV Blob_ConnectionString=
ENV Blob_ContainerName=
ENV Blob_ContainerNamePublic=
ENV Registration_allowed=true
ENV GITHUB_PROVIDER_AUTHENTICATION_ID=
ENV GITHUB_PROVIDER_AUTHENTICATION_SECRET=

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]