using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.Video;
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Video
{
    private static ILogger Logger => Helper.Log.Logger;

    [FunctionName(nameof(AddVideoAsync))]
    [OpenApiOperation(operationId: nameof(AddVideoAsync), tags: new[] { nameof(Video) })]
    [OpenApiRequestBody("application/json", typeof(AddVideoRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest)]
    public async Task<IActionResult> AddVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Video")] HttpRequest req,
        ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var videoService = new VideoService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            AddVideoRequest data = JsonConvert.DeserializeObject<AddVideoRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.Url))
            {
                return new BadRequestObjectResult("Missing Url parameter.");
            }

            var videoId = await videoService.AddVideoAsync(data.Url);
            var video = videoService.GetVideoById(videoId);
            return new OkObjectResult(video);
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddVideoAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateVideo))]
    [OpenApiOperation(operationId: nameof(UpdateVideo), tags: new[] { nameof(Video) })]
    [OpenApiRequestBody("application/json", typeof(UpdateVideoRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Video not found.")]
    public IActionResult UpdateVideo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "Video")] HttpRequest req,
        ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var videoService = new VideoService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            UpdateVideoRequest data = JsonConvert.DeserializeObject<UpdateVideoRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (string.IsNullOrEmpty(data.id))
            {
                return new BadRequestObjectResult("Missing videoId query parameter.");
            }

            var video = videoService.GetVideoById(data.id);
            if (null == data)
            {
                return new BadRequestObjectResult("Video not found.");
            }

            videoService.UpdateVideo(video, data);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }
            else if (e is EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateVideo), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(RemoveVideo))]
    [OpenApiOperation(operationId: nameof(RemoveVideo), tags: new[] { nameof(Video) })]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Ok")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public IActionResult RemoveVideo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Video")] HttpRequest req,
        ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var videoService = new VideoService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("videoId", out var videoId);

            if (null == videoId)
            {
                return new BadRequestObjectResult("Missing videoId query parameter.");
            }

            var video = videoService.GetVideoById(videoId);
            if (null == video)
            {
                return new BadRequestObjectResult("Video not found.");
            }

            videoService.RemoveVideo(video);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }
            else if (e is EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(RemoveVideo), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(GetSASToken))]
    [OpenApiOperation(operationId: nameof(GetSASToken), tags: new[] { nameof(Video) })]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "text/plain", typeof(string), Description = "The SAS Token.")]
    public async Task<IActionResult> GetSASToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Video/SASToken")] HttpRequest req,
            ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();
            if (!user.IsAdmin) return new ForbidResult();

            using var videoService = new VideoService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("videoId", out var videoId);

            if (string.IsNullOrEmpty(videoId))
                return new BadRequestObjectResult("Missing parameters.");

            string? sASToken = await videoService.GetSASTokenAsync(videoId);
            if (string.IsNullOrEmpty(sASToken))
            {
                Logger.Warning("The video {videoId} download by user {userId} failed when generating SASToken.", videoId, user.id);
                return new BadRequestObjectResult("Failed to generate SASToken.");
            }

            Logger.Verbose("User {userId} has generated a SAS token for video {videoId}", user.id, videoId);

            return new OkObjectResult(sASToken);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetSASToken), e);
            return new InternalServerErrorResult();
        }
    }
}

