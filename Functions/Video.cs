using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.Video;
using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Video
{
    private readonly ILogger _logger;
    private readonly VideoService _videoService;
    private readonly UserService _userService;
    private readonly string _frontEndUri;

    public Video(
        ILogger logger,
        VideoService videoService,
        UserService userService)
    {
        _logger = logger;
        _videoService = videoService;
        _userService = userService;
        _frontEndUri = Environment.GetEnvironmentVariable("FrontEndUri") ?? "http://localhost:4200";
    }

    [FunctionName(nameof(AddVideoAsync))]
    [OpenApiOperation(operationId: nameof(AddVideoAsync), tags: new[] { nameof(Video) })]
    [OpenApiRequestBody("application/json", typeof(AddVideoRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest)]
    public async Task<IActionResult> AddVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Video")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            AddVideoRequest data = JsonSerializer.Deserialize<AddVideoRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.Url))
            {
                return new BadRequestObjectResult("Missing Url parameter.");
            }

            var video = await _videoService.AddVideoAsync(data.Url);
            var resultdata = JsonSerializer.SerializeToUtf8Bytes(video);
            return new FileContentResult(resultdata, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                _logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateVideoAsync))]
    [OpenApiOperation(operationId: nameof(UpdateVideoAsync), tags: new[] { nameof(Video) })]
    [OpenApiRequestBody("application/json", typeof(UpdateVideoRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Video), Description = "Video")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Video not found.")]
    public async Task<IActionResult> UpdateVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Video")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            UpdateVideoRequest data = JsonSerializer.Deserialize<UpdateVideoRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.id))
            {
                return new BadRequestObjectResult("Missing videoId query parameter.");
            }
            if (string.IsNullOrEmpty(data.ChannelId))
            {
                return new BadRequestObjectResult("Missing channelId query parameter.");
            }

            var video = await _videoService.GetByVideoIdAndChannelIdAsync(data.id, data.ChannelId);
            if (null == data
                || null == video)
            {
                return new BadRequestObjectResult("Video not found.");
            }

            await _videoService.UpdateVideoAsync(video, data);
            var resultdata = JsonSerializer.SerializeToUtf8Bytes(video);
            return new FileContentResult(resultdata, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                _logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            if (e is EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(RemoveVideoAsync))]
    [OpenApiOperation(operationId: nameof(RemoveVideoAsync), tags: new[] { nameof(Video) })]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> RemoveVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Video")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new StatusCodeResult(403);

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("videoId", out var videoId);
            queryDictionary.TryGetValue("channelId", out var channelId);

            if (string.IsNullOrEmpty(videoId)
                || string.IsNullOrEmpty(channelId))
                return new BadRequestObjectResult("Missing query parameter.");

            var video = await _videoService.GetByVideoIdAndChannelIdAsync(videoId, channelId);
            if (null == video)
            {
                return new BadRequestObjectResult("Video not found.");
            }

            await _videoService.RemoveVideoAsync(video);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                _logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            if (e is EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(RemoveVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(GetToken))]
    [OpenApiOperation(operationId: nameof(GetToken), tags: new[] { nameof(Video) })]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "ChannelId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "text/plain", typeof(string), Description = "The Token.")]
    // skipcq: CS-R1073
    public async Task<IActionResult> GetToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Video/Token")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();
            // Match domains starting with demo.
            if (Regex.IsMatch(_frontEndUri, @"^(http|https)://demo\.[a-zA-Z0-9\-]+(\.[a-zA-Z]{2,})+(/[^\s]*)?$")
                && !user.IsAdmin)
                return new StatusCodeResult(403);

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("videoId", out var videoId);
            queryDictionary.TryGetValue("channelId", out var channelId);

            if (string.IsNullOrEmpty(videoId)
                || string.IsNullOrEmpty(channelId))
                return new BadRequestObjectResult("Missing parameters.");

            string token = await _videoService.GetTokenAsync(videoId, channelId);
            if (string.IsNullOrEmpty(token))
            {
                _logger.Warning("The video {videoId} download by user {userId} failed when generating Token.", videoId, user.id);
                return new BadRequestObjectResult("Failed to generate Token.");
            }

            _logger.Verbose("User {userId} has generated a token for video {videoId}", user.id, videoId);

            return new OkObjectResult(token);
        }
        catch (Exception e)
        {
            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetToken), e);
            return new InternalServerErrorResult();
        }
    }
}

