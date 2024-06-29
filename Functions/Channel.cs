using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.Channel;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Models;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.PlatformService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.OpenApi.Models;
using Serilog;

namespace LivestreamRecorderBackend.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class Channel(ILogger logger,
                     ChannelService channelService,
                     TwitchService twitchService,
                     UserService userService)
{
    [Function(nameof(AddChannelAsync))]
    [OpenApiOperation(operationId: nameof(AddChannelAsync), tags: [nameof(Channel)])]
    [OpenApiRequestBody("application/json", typeof(AddChannelRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> AddChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel")]
        HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
                // Workaround for issue: https://github.com/Azure/azure-functions-durable-extension/issues/1138#issuecomment-585868647
                req.Body = new MemoryStream(Encoding.ASCII.GetBytes(requestBody));
            }

            AddChannelRequest data = JsonSerializer.Deserialize<AddChannelRequest>(requestBody)
                                     ?? throw new InvalidOperationException("Invalid request body!!");

            string channelId;
            string channelName;
            string url = data.Url.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd(['/']);

            string platform;
            if (url.Contains("youtube"))
            {
                platform = "Youtube";
            }
            else if (url.Contains("twitcasting"))
            {
                platform = "Twitcasting";
            }
            else if (url.Contains("twitch"))
            {
                platform = "Twitch";
            }
            else if (url.Contains("fc2"))
            {
                platform = "FC2";
            }
            else
            {
                logger.Warning("Unsupported platform for {url}", url);
                throw new InvalidOperationException($"Unsupported platform for {url}.");
            }

            // skipcq: CS-R1116
            switch (platform)
            {
                case "Youtube":
                    YtdlpVideoData? info = await YoutubeDL.GetInfoByYtdlpAsync(data.Url);
                    if (null == info)
                    {
                        logger.Warning("Failed to get channel info for {url}", data.Url);
                        return new OkObjectResult("Failed");
                    }

                    channelId = info.ChannelId;
                    channelName = info.Uploader;
                    break;
                case "Twitch":
                    twitchService.EnsureTwitchSetup();
                    goto default;
                //case "Twitcasting":
                //case "FC2":
                default:
                    channelId = url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                    channelName = channelId;
                    break;
            }

            channelId = NameHelper.ChangeId.ChannelId.DatabaseType(channelId, platform);

            LivestreamRecorder.DB.Models.Channel channel =
                await channelService.GetByChannelIdAndSourceAsync(channelId, platform)
                ?? await channelService.AddChannelAsync(id: channelId,
                                                        source: platform,
                                                        channelName: channelName);

            logger.Information("Finish adding channel {channelName}:{channelId}", channelName, channelId);

            string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(
                orchestratorName: nameof(UpdateChannel_Durable),
                input: new UpdateChannelRequest
                {
                    id = channelId,
                    Source = channel.Source
                });

            logger.Information("Started orchestration with ID {instanceId}.", instanceId);
            // Wait for the instance to start executing
            await starter.WaitForInstanceStartAsync(instanceId);
            return new OkResult();
        }
        catch (Exception e)
        {
            switch (e)
            {
                case InvalidOperationException:
                    return new BadRequestObjectResult(e.Message);
                case ConfigurationErrorsException:
                    return new UnprocessableEntityObjectResult(e.Message);
                default:
                    logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddChannelAsync), e);
                    return new InternalServerErrorResult();
            }
        }
    }

    [Function(nameof(UpdateChannel_Http))]
    [OpenApiOperation(operationId: nameof(UpdateChannel_Http), tags: [nameof(Channel)])]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    // skipcq: CS-R1073
    public async Task<IActionResult> UpdateChannel_Http(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Channel")]
        HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            UpdateChannelRequest data = JsonSerializer.Deserialize<UpdateChannelRequest>(requestBody)
                                        ?? throw new InvalidOperationException("Invalid request body!!");

            await starter.ScheduleNewOrchestrationInstanceAsync(
                orchestratorName: nameof(UpdateChannel_Durable),
                input: data);

            return new OkResult();
        }
        catch (Exception e)
        {
            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateChannel_Http), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(UpdateChannel_Durable))]
    public bool UpdateChannel_Durable(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        UpdateChannelRequest? data = context.GetInput<UpdateChannelRequest>();
        if (null == data) throw new InvalidOperationException("Invalid request body!!");
        _ = Task.Run(async () =>
        {
            logger.Information("Start updating channel {channelId}", data.id);
            LivestreamRecorder.DB.Models.Channel? channel =
                await channelService.GetByChannelIdAndSourceAsync(data.id, data.Source);

            if (null == channel)
            {
                logger.Warning("Channel {channelId} not found when updating", data.id);
                throw new EntityNotFoundException(data.id);
            }

            await channelService.UpdateChannelDataAsync(channel);

            await channelService.EditMonitoringAsync(data.id, data.Source, true);
            logger.Information("Finish updating channel {channelId}", data.id);
        });

        return true;
    }

    [Function(nameof(EnableChannelAsync))]
    [OpenApiOperation(operationId: nameof(EnableChannelAsync), tags: [nameof(Channel)])]
    [OpenApiRequestBody("application/json", typeof(EnableChannelRequest), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Response")]
    public async Task<IActionResult> EnableChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel/EnableChannel")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            EnableChannelRequest data = JsonSerializer.Deserialize<EnableChannelRequest>(requestBody)
                                        ?? throw new InvalidOperationException("Invalid request body!!");

            await channelService.EditMonitoringAsync(data.id, data.Source, data.Monitoring);

            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is EntityNotFoundException) return new BadRequestObjectResult(e.Message);

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(EnableChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(HideChannelAsync))]
    [OpenApiOperation(operationId: nameof(HideChannelAsync), tags: [nameof(Channel)])]
    [OpenApiRequestBody("application/json", typeof(HideChannelRequest), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Response")]
    public async Task<IActionResult> HideChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel/HideChannel")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            HideChannelRequest data = JsonSerializer.Deserialize<HideChannelRequest>(requestBody)
                                      ?? throw new InvalidOperationException("Invalid request body!!");

            await channelService.EditHidingAsync(data.id, data.Source, data.Hide);

            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is EntityNotFoundException) return new BadRequestObjectResult(e.Message);

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(HideChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }
}
