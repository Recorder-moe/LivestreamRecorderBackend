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
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Channel
{
    private readonly ILogger _logger;
    private readonly ChannelService _channelService;
    private readonly UserService _userService;

    public Channel(
        ILogger logger,
        ChannelService channelService,
        UserService userService)
    {
        _logger = logger;
        _channelService = channelService;
        _userService = userService;
    }

    [FunctionName(nameof(AddChannelAsync))]
    [OpenApiOperation(operationId: nameof(AddChannelAsync), tags: new[] { nameof(Channel) })]
    [OpenApiRequestBody("application/json", typeof(AddChannelRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> AddChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel")] HttpRequest req,
        [DurableClient] IDurableClient starter)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
                // Workaround for issue: https://github.com/Azure/azure-functions-durable-extension/issues/1138#issuecomment-585868647
                req.Body = new MemoryStream(Encoding.ASCII.GetBytes(requestBody));
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
                _logger.Warning("Unsupported platform for {url}", url);
                throw new InvalidOperationException($"Unsupported platform for {url}.");
            }

            switch (Platform)
            {
                case "Youtube":
                    var info = await YoutubeDL.GetInfoByYtdlpAsync(data.Url);
                    if (null == info)
                    {
                        _logger.Warning("Failed to get channel info for {url}", data.Url);
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

            channel = _channelService.ChannelExists(channelId)
                ? await _channelService.GetByChannelIdAndSource(channelId, Platform) ?? throw new EntityNotFoundException(channelId)
                : await _channelService.AddChannelAsync(id: channelId,
                                                        source: Platform,
                                                        channelName: channelName);

            _logger.Information("Finish adding channel {channelName}:{channelId}", channelName, channelId);

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

            _logger.Information("Started orchestration with ID {instanceId}.", instanceId);
            // Wait for the instance to start executing
            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(15));
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateChannel_Http))]
    [OpenApiOperation(operationId: nameof(UpdateChannel_Http), tags: new[] { nameof(Channel) })]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "Response")]
    public async Task<IActionResult> UpdateChannel_Http(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Channel")] HttpRequest req,
            [DurableClient] IDurableClient starter)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
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
            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateChannel_Http), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateChannel_Durable))]
    public bool UpdateChannel_Durable(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        UpdateChannelRequest data = context.GetInput<UpdateChannelRequest>();
        _ = Task.Run(async () =>
        {
            _logger.Information("Start updating channel {channelId}", data.id);
            var channel = await _channelService.GetChannelByIdAsync(data.id);
            if (null == channel)
            {
                _logger.Warning("Channel {channelId} not found when updating", data.id);
                throw new EntityNotFoundException(data.id);
            }

            if (null != data.Avatar)
            {
                data.Avatar = data.Avatar.Replace("_bigger", "")        // Twitcasting
                                         .Replace("70x70", "300x300");  // Twitch
            }
            await _channelService.UpdateChannelData(channel, data.AutoUpdateInfo, data.ChannelName, data.Avatar, data.Banner);

            await _channelService.EnableMonitoringAsync(data.id);
            _logger.Information("Finish updating channel {channelId}", data.id);
        });
        return true;
    }

    [FunctionName(nameof(EnableChannelAsync))]
    [OpenApiOperation(operationId: nameof(EnableChannelAsync), tags: new[] { nameof(Channel) })]
    [OpenApiRequestBody("application/json", typeof(AddChannelRequest), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Response")]
    public async Task<IActionResult> EnableChannelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel/EnableChannel")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<EnableChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (data.Monitoring)
                await _channelService.EnableMonitoringAsync(data.id);
            else
                await _channelService.DisableMonitoringAsync(data.id);

            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(EnableChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }
}

