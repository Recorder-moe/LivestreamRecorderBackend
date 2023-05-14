using LivestreamRecorderBackend.DTO;
using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Channel")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var channelService = new ChannelService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            AddChannelRequest data = JsonConvert.DeserializeObject<AddChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            // TODO Update channel images
            var channelId = "";
            var url = data.Url.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd(new[] { '/' });
            // Youtube
            if (data.Url.Contains("youtube"))
            {
                var info = await Helper.YoutubeDL.GetInfoByYtdlpAsync(data.Url);
                if (null == info)
                {
                    Logger.Warning("Failed to get channel info for {url}", data.Url);
                    return new OkObjectResult("Failed");
                }

                channelId = info.ChannelId;
            }
            // Twitch, Twitcasting
            else
            {
                channelId = url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            }

            if (channelService.ChannelExists(channelId))
            {
                return new OkObjectResult(channelId);
            }

            return new OkObjectResult("OK");
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddChannelAsync), e);
            return new InternalServerErrorResult();
        }
    }
}

