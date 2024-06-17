using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.Video;
using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Serilog;

namespace LivestreamRecorderBackend.Functions;

public class Video(ILogger logger,
                   VideoService videoService,
                   UserService userService)
{
    private readonly string _frontEndUri = Environment.GetEnvironmentVariable("FrontEndUri") ?? "http://localhost:4200";

    [Function(nameof(AddVideoAsync))]
    [OpenApiOperation(operationId: nameof(AddVideoAsync), tags: [nameof(Video)])]
    [OpenApiRequestBody("application/json", typeof(AddVideoRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest)]
    public async Task<IActionResult> AddVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Video")]
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

            AddVideoRequest data = JsonSerializer.Deserialize<AddVideoRequest>(requestBody)
                                   ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.Url)) return new BadRequestObjectResult("Missing Url parameter.");

            LivestreamRecorder.DB.Models.Video? video = await videoService.AddVideoAsync(data.Url);
            byte[] resultData = JsonSerializer.SerializeToUtf8Bytes(video);
            return new FileContentResult(resultData, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(UpdateVideoAsync))]
    [OpenApiOperation(operationId: nameof(UpdateVideoAsync), tags: [nameof(Video)])]
    [OpenApiRequestBody("application/json", typeof(UpdateVideoRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Video), Description = "Video")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Video not found.")]
    public async Task<IActionResult> UpdateVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Video")]
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

            UpdateVideoRequest data = JsonSerializer.Deserialize<UpdateVideoRequest>(requestBody)
                                      ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.id)) return new BadRequestObjectResult("Missing videoId query parameter.");

            if (string.IsNullOrEmpty(data.ChannelId)) return new BadRequestObjectResult("Missing channelId query parameter.");

            LivestreamRecorder.DB.Models.Video? video = await videoService.GetByVideoIdAndChannelIdAsync(data.id, data.ChannelId);
            if (null == video) return new BadRequestObjectResult("Video not found.");

            await videoService.UpdateVideoAsync(video, data);
            byte[] resultData = JsonSerializer.SerializeToUtf8Bytes(video);
            return new FileContentResult(resultData, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            if (e is EntityNotFoundException) return new BadRequestObjectResult(e.Message);

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(RemoveVideoAsync))]
    [OpenApiOperation(operationId: nameof(RemoveVideoAsync), tags: [nameof(Video)])]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> RemoveVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Video")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            IDictionary<string, string?> queryDictionary = req.Query.ToDictionary(p => p.Key, p => p.Value.Last());
            queryDictionary.TryGetValue("videoId", out string? videoId);
            queryDictionary.TryGetValue("channelId", out string? channelId);

            if (string.IsNullOrEmpty(videoId)
                || string.IsNullOrEmpty(channelId))
                return new BadRequestObjectResult("Missing query parameter.");

            LivestreamRecorder.DB.Models.Video? video = await videoService.GetByVideoIdAndChannelIdAsync(videoId, channelId);
            if (null == video) return new BadRequestObjectResult("Video not found.");

            await videoService.RemoveVideoAsync(video);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            if (e is EntityNotFoundException) return new BadRequestObjectResult(e.Message);

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(RemoveVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(GetToken))]
    [OpenApiOperation(operationId: nameof(GetToken), tags: [nameof(Video)])]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "text/plain", typeof(string), Description = "The Token.")]
    // skipcq: CS-R1073
    public async Task<IActionResult> GetToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Video/Token")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            // Match domains starting with demo.
            if (Regex.IsMatch(_frontEndUri, @"^(http|https)://demo\.[a-zA-Z0-9\-]+(\.[a-zA-Z]{2,})+(/[^\s]*)?$")
                && !user.IsAdmin)
                return new StatusCodeResult(403);

            IDictionary<string, string?> queryDictionary = req.Query.ToDictionary(p => p.Key, p => p.Value.Last());
            queryDictionary.TryGetValue("videoId", out string? videoId);
            queryDictionary.TryGetValue("channelId", out string? channelId);

            if (string.IsNullOrEmpty(videoId)
                || string.IsNullOrEmpty(channelId))
                return new BadRequestObjectResult("Missing parameters.");

            string token = await videoService.GetTokenAsync(videoId, channelId);
            if (string.IsNullOrEmpty(token))
            {
                logger.Warning("The video {videoId} download by user {userId} failed when generating Token.", videoId, user.id);
                return new BadRequestObjectResult("Failed to generate Token.");
            }

            logger.Verbose("User {userId} has generated a token for video {videoId}", user.id, videoId);

            return new OkObjectResult(token);
        }
        catch (Exception e)
        {
            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetToken), e);
            return new InternalServerErrorResult();
        }
    }
}
