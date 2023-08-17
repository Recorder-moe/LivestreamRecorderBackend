using Azure.Identity;
#if COUCHDB
using CouchDB.Driver.DependencyInjection;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.CouchDB;
#endif
#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.Authentication;
using LivestreamRecorderBackend.Services.StorageService;
using LivestreamRecorderBackend.SingletonServices;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Newtonsoft.Json.Serialization;
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
            builder.Services.AddMvc()
                .AddNewtonsoftJson(o => o.SerializerSettings.ContractResolver = new DefaultContractResolver());

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
#if COSMOSDB
            builder.Services.AddDbContext<PublicContext>((options) =>
            {
                options
                    //.EnableSensitiveDataLogging()
                    .UseCosmos(connectionString: Environment.GetEnvironmentVariable("CosmosDB_Public_ConnectionString")!,
                               databaseName: "Public",
                               cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);
            builder.Services.AddDbContext<PrivateContext>((options) =>
            {
                options
                    //.EnableSensitiveDataLogging()
                    .UseCosmos(connectionString: Environment.GetEnvironmentVariable("CosmosDB_Private_ConnectionString")!,
                               databaseName: "Private",
                               cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);

            builder.Services.AddSingleton<UnitOfWork_Public>();
            builder.Services.AddSingleton<UnitOfWork_Private>();
            builder.Services.AddSingleton<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            builder.Services.AddSingleton<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            builder.Services.AddSingleton<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif
            #endregion
            #region CouchDB
#if COUCHDB
            builder.Services.AddCouchContext<CouchDBContext>((options) =>
            {
                options
                    .UseEndpoint(Environment.GetEnvironmentVariable("CouchDB_Endpoint")!)
                    .UseCookieAuthentication(username: Environment.GetEnvironmentVariable("CouchDB_Username")!, password: Environment.GetEnvironmentVariable("CouchDB_Password")!)
#if !RELEASE
                    .ConfigureFlurlClient(setting
                        => setting.BeforeCall = call
                            => Log.Debug("Sending request to couch: {request} {body}", call, call.RequestBody))
#endif
                    .SetPropertyCase(PropertyCaseType.None);
            });

            builder.Services.AddSingleton<UnitOfWork_Public>();
            builder.Services.AddSingleton<UnitOfWork_Private>();
            builder.Services.AddSingleton<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            builder.Services.AddSingleton<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            builder.Services.AddSingleton<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif
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

            builder.Services.AddSingleton<ChannelService>();
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddSingleton<VideoService>();
            builder.Services.AddSingleton<FC2Service>();

            builder.Services.AddSingleton<AuthenticationService>();
            builder.Services.AddSingleton<GoogleService>();
            builder.Services.AddSingleton<GithubService>();
            builder.Services.AddSingleton<MicrosoftService>();
            builder.Services.AddSingleton<DiscordService>();
        }
    }
}