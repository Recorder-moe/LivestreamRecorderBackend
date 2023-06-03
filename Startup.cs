using Azure.Identity;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderBackend.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(LivestreamRecorderBackend.Startup))]

namespace LivestreamRecorderBackend
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton(Helper.Log.MakeLogger());

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

            builder.Services.AddAzureClients(clientsBuilder =>
            {
                clientsBuilder.UseCredential(new DefaultAzureCredential())
                              .AddBlobServiceClient(Environment.GetEnvironmentVariable("Blob_ConnectionString"));
            });

            builder.Services.AddSingleton<ABSService>();

            builder.Services.AddScoped<ChannelService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<VideoService>();
            builder.Services.AddScoped<FC2Service>();
        }
    }
}