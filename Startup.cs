using Azure.Identity;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.Authentication;
using LivestreamRecorderBackend.Services.StorageService;
using LivestreamRecorderBackend.SingletonServices;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Serilog;
using System;
using System.Configuration;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(LivestreamRecorderBackend.Startup))]

namespace LivestreamRecorderBackend
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient("client", config =>
            {
                config.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(".NET", "6.0"));
                config.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Recorder.moe", "1.0"));
                config.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://recorder.moe)"));
            });
            ILogger logger = Helper.Log.MakeLogger();
            builder.Services.AddSingleton(logger);
            builder.Services.AddMemoryCache(option => option.SizeLimit = 1024);

            #region CosmosDB
            builder.Services.AddDbContext<PublicContext>(options =>
            {
                options.UseCosmos(connectionString: Environment.GetEnvironmentVariable("ConnectionStrings_Public")!,
                                  databaseName: "Public",
                                  cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });
            builder.Services.AddDbContext<PrivateContext>(options =>
            {
                options.UseCosmos(connectionString: Environment.GetEnvironmentVariable("ConnectionStrings_Private")!,
                                  databaseName: "Private",
                                  cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });

            builder.Services.AddScoped<UnitOfWork_Public>();
            builder.Services.AddScoped<UnitOfWork_Private>();
            builder.Services.AddScoped<IVideoRepository, VideoRepository>();
            builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            #endregion

            #region Storage
            if (Environment.GetEnvironmentVariable("StorageService") == "AzureBlobStorage")
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ConnectionString"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ContainerNamePrivate"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ContainerNamePublic")))
                {
                    logger.Fatal("Invalid ENV for BlobStorage. Please set Blob_ConnectionString, Blob_ContainerNamePrivate, Blob_ContainerNamePublic");
                    throw new ConfigurationErrorsException("Invalid ENV for BlobStorage. Please set Blob_ConnectionString, Blob_ContainerNamePrivate, Blob_ContainerNamePublic");
                }

                builder.Services.AddAzureClients(clientsBuilder =>
                {
                    clientsBuilder.UseCredential(new DefaultAzureCredential())
                                  .AddBlobServiceClient(Environment.GetEnvironmentVariable("Blob_ConnectionString"));
                });

                builder.Services.AddSingleton<IStorageService, ABSService>();
            }
            else if (Environment.GetEnvironmentVariable("StorageService") == "S3")
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_Endpoint"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_AccessKey"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_SecretKey"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_Secure"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_BucketNamePrivate"))
                    || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("S3_BucketNamePublic")))
                {
                    logger.Fatal("Invalid ENV for S3. Please set S3_Endpoint, S3_AccessKey, S3_SecretKey, S3_Secure, S3_BucketNamePrivate, S3_BucketNamePublic");
                    throw new ConfigurationErrorsException("Invalid ENV for S3. Please set S3_Endpoint, S3_AccessKey, S3_SecretKey, S3_Secure, S3_BucketNamePrivate, S3_BucketNamePublic");
                }

                MinioClient minio = new MinioClient()
                            .WithEndpoint(Environment.GetEnvironmentVariable("S3_Endpoint"))
                            .WithCredentials(Environment.GetEnvironmentVariable("S3_AccessKey"), Environment.GetEnvironmentVariable("S3_SecretKey"))
                            .WithSSL(bool.Parse(Environment.GetEnvironmentVariable("S3_Secure") ?? "false"))
                            .Build();

                builder.Services.AddSingleton<IMinioClient>(minio);
                builder.Services.AddSingleton<IStorageService, S3Service>();
            }
            else
            {
                logger.Fatal("Invalid ENV StorageService. Should be AzureBlobStorage or S3.");
                throw new ConfigurationErrorsException("Invalid ENV StorageService. Should be AzureBlobStorage or S3.");
            }
            #endregion

            builder.Services.AddScoped<ChannelService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<VideoService>();
            builder.Services.AddScoped<FC2Service>();

            builder.Services.AddScoped<AuthenticationService>();
            builder.Services.AddScoped<GoogleService>();
            builder.Services.AddScoped<GithubService>();
            builder.Services.AddScoped<MicrosoftService>();
            builder.Services.AddScoped<DiscordService>();
        }
    }
}