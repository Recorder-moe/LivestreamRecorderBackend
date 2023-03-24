using Azure.Storage.Blobs;
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
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Video
{
    private static ILogger Logger => Helper.Log.Logger;

    [FunctionName(nameof(GetSASToken))]
    [OpenApiOperation(operationId: nameof(GetSASToken), tags: new[] { nameof(Video) })]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "UserId")]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "text/plain", typeof(string), Description = "The SAS Token.")]
    public async Task<IActionResult> GetSASToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Video/SASToken")] HttpRequest req,
            [Blob("livestream-recorder")] BlobContainerClient blobContainerClient,
            ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var videoService = new VideoService();
            using var transactionService = new TransactionService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("userId", out var userId);
            queryDictionary.TryGetValue("videoId", out var videoId);

            if (user.id != userId) return new ForbidResult();

            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(userId))
                return new BadRequestObjectResult("Missing parameters.");

            if (!transactionService.IsVideoDownloaded(videoId, userId))
            {
                Logger.Warning("The video {videoId} download by user {userId} failed because the download transaction was not completed. Please ensure that the transaction for downloading the video is successfully processed at /api/Transaction/DownloadVideo.", videoId, userId);
                return new BadRequestObjectResult("The video download failed because the download transaction was not completed. Please ensure that the transaction for downloading the video is successfully processed at /api/Transaction/DownloadVideo.");
            }

            string? sASToken = await videoService.GetSASTokenAsync(videoId, blobContainerClient);
            if (string.IsNullOrEmpty(sASToken))
            {
                Logger.Warning("The video {videoId} download by user {userId} failed when generating SASToken.", videoId, userId);
                return new BadRequestObjectResult("Failed to generate SASToken.");
            }

            Logger.Verbose("User {userId} has generated a SAS token for video {videoId}", userId, videoId);

            return new OkObjectResult(sASToken);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetSASToken), e);
            return new InternalServerErrorResult();
        }
    }
}

