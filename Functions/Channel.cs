using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.Channel;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Channel
{
    private static ILogger Logger => Helper.Log.Logger;

    [FunctionName(nameof(AddChannelAsync))]
    [OpenApiOperation(operationId: nameof(AddChannelAsync), tags: new[] { nameof(Channel) })]
    [OpenApiRequestBody("application/json", typeof(AddChannelRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> AddChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel")] HttpRequest req,
        [DurableClient] IDurableClient starter,
        ClaimsPrincipal principal)
    {
        try
        {
            var user = Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var channelService = new ChannelService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<AddChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            LivestreamRecorder.DB.Models.Channel channel;
            var channelId = "";
            var channelName = "";
            var url = data.Url.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd(new[] { '/' });

            string Platform;
            if (url.Contains("youtube"))
            {
                Platform = "Youtube";
            }
            else if (url.Contains("twitcasting"))
            {
                Platform = "Twitcasting";
            }
            else if (url.Contains("twitch"))
            {
                Platform = "Twitch";
            }
            else if (url.Contains("fc2"))
            {
                Platform = "FC2";
            }
            else
            {
                Logger.Warning("Unsupported platform for {url}", url);
                throw new InvalidOperationException($"Unsupported platform for {url}.");
            }

            switch (Platform)
            {
                case "Youtube":
                    var info = await YoutubeDL.GetInfoByYtdlpAsync(data.Url);
                    if (null == info)
                    {
                        Logger.Warning("Failed to get channel info for {url}", data.Url);
                        return new OkObjectResult("Failed");
                    }

                    channelId = info.ChannelId;
                    channelName = info.Uploader;
                    break;
                case "Twitcasting":
                case "Twitch":
                case "FC2":
                default:
                    channelId = url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                    channelName = data.Name ?? channelId;
                    break;
            }

            channel = channelService.ChannelExists(channelId)
                ? channelService.GetChannelById(channelId)
                : channelService.AddChannel(id: channelId,
                                            source: Platform,
                                            channelName: channelName);

            Logger.Information("Finish adding channel {channelName}:{channelId}", channelName, channelId);

            var instanceId = await starter.StartNewAsync(
                orchestratorFunctionName: nameof(UpdateChannel_Durable),
                input: new UpdateChannelRequest()
                {
                    id = channelId,
                    AutoUpdateInfo = channel.Source == "Youtube"
                                     || channel.Source == "FC2",
                    ChannelName = data.Name ?? channelName ?? channelId,
                    Avatar = data.Avatar,
                    Banner = data.Banner,
                });

            return new OkObjectResult(channelId);
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateChannel_Http))]
    [OpenApiOperation(operationId: nameof(UpdateChannel_Http), tags: new[] { nameof(Channel) })]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> UpdateChannel_Http(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Channel")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ClaimsPrincipal principal)
    {
        try
        {
            var user = Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<UpdateChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            var instanceId = await starter.StartNewAsync(
                orchestratorFunctionName: nameof(UpdateChannel_Durable),
                input: data);

            return new OkResult();
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateChannel_Http), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateChannel_Durable))]
    public static bool UpdateChannel_Durable(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        UpdateChannelRequest data = context.GetInput<UpdateChannelRequest>();
        _ = Task.Run(async () =>
        {
            Logger.Information("Start updating channel {channelId}", data.id);
            using var channelService = new ChannelService();
            var channel = channelService.GetChannelById(data.id);

            if (null != data.Avatar)
            {
                data.Avatar = data.Avatar.Replace("_bigger", "")        // Twitcasting
                                         .Replace("70x70", "300x300");  // Twitch
            }
            await channelService.UpdateChannelData(channel, data.AutoUpdateInfo, data.ChannelName, data.Avatar, data.Banner);

            channelService.EnableMonitoring(data.id);
            Logger.Information("Finish updating channel {channelId}", data.id);
        });
        return true;
    }

    [FunctionName(nameof(EnableChannelAsync))]
    [OpenApiOperation(operationId: nameof(EnableChannelAsync), tags: new[] { nameof(Channel) })]
    [OpenApiRequestBody("application/json", typeof(AddChannelRequest), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Response")]
    public async Task<IActionResult> EnableChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel/EnableChannel")] HttpRequest req,
        ClaimsPrincipal principal)
    {
        try
        {
            var user = Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var channelService = new ChannelService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<EnableChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (data.Monitoring)
                channelService.EnableMonitoring(data.id);
            else
                channelService.DisableMonitoring(data.id);

            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is EntityNotFoundException)
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(EnableChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }
}

