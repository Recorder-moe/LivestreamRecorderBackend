using LivestreamRecorderBackend.DTO;
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

            using var channelService = new ChannelService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<AddChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            LivestreamRecorder.DB.Models.Channel channel;
            var channelId = "";
            var channelName = "";
            var url = data.Url.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd(new[] { '/' });
            // Youtube
            if (data.Url.Contains("youtube"))
            {
                var info = await YoutubeDL.GetInfoByYtdlpAsync(data.Url);
                if (null == info)
                {
                    Logger.Warning("Failed to get channel info for {url}", data.Url);
                    return new OkObjectResult("Failed");
                }

                channelId = info.ChannelId;
                channelName = info.Uploader;
                channel = channelService.ChannelExists(channelId)
                    ? channelService.GetChannelById(channelId)
                    : channelService.AddChannel(channelId, "Youtube", channelName);
            }
            // Twitch, Twitcasting
            else
            {
                channelId = url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                channelName = data.Name ?? "";
                channel = channelService.ChannelExists(channelId)
                    ? channelService.GetChannelById(channelId)
                    : channelService.AddChannel(channelId, url.Contains("twitch") ? "Twitch" : "Twitcasting", channelName);
            }
            Logger.Information("Finish adding channel {channelId}", channelId);

            var instanceId = await starter.StartNewAsync(
                orchestratorFunctionName: nameof(UpdateChannel_Durable),
                input: new UpdateChannelRequest()
                {
                    UserId = data.UserId,
                    ChannelId = channelId,
                    Name = data.Name,
                    Avatar = data.Avatar,
                    Banner = data.Banner,
                });

            return new OkObjectResult(channelId);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateChannel_Http))]
    [OpenApiOperation(operationId: nameof(UpdateChannel_Http), tags: new[] { nameof(Channel) })]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> UpdateChannel_Http(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "UpdateChannel")] HttpRequest req,
            ClaimsPrincipal principal)
    {
        try
        {
            var user = Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<UpdateChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            await UpdateChannel_Inner(data);

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
        _ = Task.Run(() => UpdateChannel_Inner(data));
        return true;
    }

    private static async Task UpdateChannel_Inner(UpdateChannelRequest data)
    {
        Logger.Information("Start updating channel {channelId}", data.ChannelId);
        using var channelService = new ChannelService();
        var channel = channelService.GetChannelById(data.ChannelId);

        // Youtube
        if (channel.Source == "Youtube")
        {
            await channelService.UpdateChannelData(channel);
        }
        // Twitch, Twitcasting
        else
        {
            if(null != data.Avatar)
            {
                data.Avatar = data.Avatar.Replace("_bigger", "")        // Twitcasting
                                         .Replace("70x70", "300x300");  // Twitch
            }
            await channelService.UpdateChannelData(channel, data.Name, data.Avatar, data.Banner);
        }

        channelService.EnableMonitoring(data.ChannelId);
        Logger.Information("Finish updating channel {channelId}", data.ChannelId);
    }
}

