using System;
using System.Configuration;
using System.Net.Http.Headers;
using Azure.Identity;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.Authentication;
using LivestreamRecorderBackend.Services.PlatformService;
using LivestreamRecorderBackend.Services.StorageService;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Interfaces;
using Log = LivestreamRecorderBackend.Helper.Log;
#if COUCHDB
using CouchDB.Driver.DependencyInjection;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.CouchDB;
#endif

#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#endif

IHostBuilder builder = new HostBuilder()
                       .ConfigureFunctionsWebApplication()
                       .ConfigureServices((_, services) =>
                       {
                           services.AddHttpClient("client",
                                                  config =>
                                                  {
                                                      config.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(".NET", "8.0"));
                                                      config.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Recorder.moe", "1.0"));
                                                      config.DefaultRequestHeaders.UserAgent.Add(
                                                          new ProductInfoHeaderValue("(+https://recorder.moe)"));
                                                  });

                           services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
                           {
                               var options = new OpenApiConfigurationOptions
                               {
                                   Servers = DefaultOpenApiConfigurationOptions.GetHostNames(),
                                   OpenApiVersion = OpenApiVersionType.V3,
                                   IncludeRequestingHostName = true,
                                   ForceHttps = false,
                                   ForceHttp = false
                               };

                               return options;
                           });

                           ILogger logger = Log.MakeLogger();
                           services.AddSingleton(logger);
                           services.AddMemoryCache(option => option.SizeLimit = 1024);

                           #region CosmosDB

#if COSMOSDB
                           services.AddDbContext<PublicContext>((options) =>
                                                                {
                                                                    options
                                                                        //.EnableSensitiveDataLogging()
                                                                        .UseCosmos(
                                                                            connectionString: Environment.GetEnvironmentVariable(
                                                                                "CosmosDB_Public_ConnectionString")!,
                                                                            databaseName: "Public",
                                                                            cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
                                                                },
                                                                ServiceLifetime.Singleton,
                                                                ServiceLifetime.Singleton);

                           services.AddDbContext<PrivateContext>((options) =>
                                                                 {
                                                                     options
                                                                         //.EnableSensitiveDataLogging()
                                                                         .UseCosmos(
                                                                             connectionString: Environment.GetEnvironmentVariable(
                                                                                 "CosmosDB_Private_ConnectionString")!,
                                                                             databaseName: "Private",
                                                                             cosmosOptionsAction: option
                                                                                 => option.GatewayModeMaxConnectionLimit(380));
                                                                 },
                                                                 ServiceLifetime.Singleton,
                                                                 ServiceLifetime.Singleton);

                           services.AddSingleton<UnitOfWork_Public>();
                           services.AddSingleton<UnitOfWork_Private>();
                           services.AddSingleton<IVideoRepository>(
                               (s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));

                           services.AddSingleton<IChannelRepository>(
                               (s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));

                           services.AddSingleton<IUserRepository>(
                               (s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif

                           #endregion

                           #region CouchDB

#if COUCHDB
                           services.AddCouchContext<CouchDBContext>(options =>
                           {
                               options
                                   .UseEndpoint(Environment.GetEnvironmentVariable("CouchDB_Endpoint")!)
                                   .UseCookieAuthentication(username: Environment.GetEnvironmentVariable("CouchDB_Username")!,
                                                            password: Environment.GetEnvironmentVariable("CouchDB_Password")!)
#if !RELEASE
                                   .ConfigureFlurlClient(setting
                                                             => setting.BeforeCall = call
                                                                 => Serilog.Log.Debug("Sending request to couch: {request} {body}",
                                                                                      call,
                                                                                      call.RequestBody))
#endif
                                   .SetPropertyCase(PropertyCaseType.None);
                           });

                           services.AddSingleton<UnitOfWork_Public>();
                           services.AddSingleton<UnitOfWork_Private>();
                           services.AddSingleton<IVideoRepository>(
                               s => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));

                           services.AddSingleton<IChannelRepository>(
                               s => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));

                           services.AddSingleton<IUserRepository>(
                               s => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif

                           #endregion

                           #region Storage

                           if (Environment.GetEnvironmentVariable("StorageService") == "AzureBlobStorage")
                           {
                               if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ConnectionString"))
                                   || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ContainerNamePrivate"))
                                   || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Blob_ContainerNamePublic")))
                               {
                                   logger.Fatal(
                                       "Invalid ENV for BlobStorage. Please set Blob_ConnectionString, Blob_ContainerNamePrivate, Blob_ContainerNamePublic");

                                   throw new ConfigurationErrorsException(
                                       "Invalid ENV for BlobStorage. Please set Blob_ConnectionString, Blob_ContainerNamePrivate, Blob_ContainerNamePublic");
                               }

                               services.AddAzureClients(clientsBuilder =>
                               {
                                   clientsBuilder.UseCredential(new DefaultAzureCredential())
                                                 .AddBlobServiceClient(Environment.GetEnvironmentVariable("Blob_ConnectionString"));
                               });

                               services.AddSingleton<IStorageService, AbsService>();
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
                                   logger.Fatal(
                                       "Invalid ENV for S3. Please set S3_Endpoint, S3_AccessKey, S3_SecretKey, S3_Secure, S3_BucketNamePrivate, S3_BucketNamePublic");

                                   throw new ConfigurationErrorsException(
                                       "Invalid ENV for S3. Please set S3_Endpoint, S3_AccessKey, S3_SecretKey, S3_Secure, S3_BucketNamePrivate, S3_BucketNamePublic");
                               }

                               IMinioClient? minio = new MinioClient()
                                                     .WithEndpoint(Environment.GetEnvironmentVariable("S3_Endpoint"))
                                                     .WithCredentials(Environment.GetEnvironmentVariable("S3_AccessKey"),
                                                                      Environment.GetEnvironmentVariable("S3_SecretKey"))
                                                     .WithSSL(bool.Parse(Environment.GetEnvironmentVariable("S3_Secure") ?? "false"))
                                                     .Build();

                               services.AddSingleton(minio);
                               services.AddSingleton<IStorageService, S3Service>();
                           }
                           else
                           {
                               logger.Fatal("Invalid ENV StorageService. Should be AzureBlobStorage or S3.");
                               throw new ConfigurationErrorsException("Invalid ENV StorageService. Should be AzureBlobStorage or S3.");
                           }

                           #endregion

                           services.AddSingleton<ChannelService>();
                           services.AddSingleton<UserService>();
                           services.AddSingleton<VideoService>();

                           services.AddSingleton<ITwitchAPI, TwitchAPI>(_ =>
                           {
                               var api = new TwitchAPI(
                                   settings: new ApiSettings
                                   {
                                       ClientId = Environment.GetEnvironmentVariable("Twitch_ClientId"),
                                       Secret = Environment.GetEnvironmentVariable("Twitch_ClientSecret")
                                   });

                               return api;
                           });

                           services.AddSingleton<Fc2Service>();
                           services.AddSingleton<TwitcastingService>();
                           services.AddSingleton<TwitchService>();
                           services.AddSingleton<YoutubeService>();

                           services.AddSingleton<AuthenticationService>();
                           services.AddSingleton<GoogleService>();
                           services.AddSingleton<GitHubService>();
                           services.AddSingleton<MicrosoftService>();
                           services.AddSingleton<DiscordService>();
                       });

IHost host = builder.Build();

host.Run();
