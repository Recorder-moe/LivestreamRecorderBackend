using LivestreamRecorderBackend.DTO.Transaction;
using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Omu.ValueInjecter;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class Transaction
{
    private static ILogger Logger => Helper.Log.Logger;

    [FunctionName(nameof(SupportChannel))]
    [OpenApiOperation(operationId: nameof(SupportChannel), tags: new[] { nameof(Transaction) })]
    [OpenApiRequestBody("application/json", typeof(SupportChannelRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "The transaction id.")]
    public IActionResult SupportChannel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Transaction/SupportChannel")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            SupportChannelRequest data = JsonConvert.DeserializeObject<SupportChannelRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            var transactionId = transactionService.NewSupportChannelTransaction(data.UserId, data.ChannelId, data.Amount);
            var transaction = transactionService.GetTransactionById(transactionId);
            return transaction.TransactionState == DB.Enum.TransactionState.Success
                ? new OkObjectResult(transaction.id)
                : new BadRequestObjectResult(transaction.id);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(SupportChannel), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(GetTransaction))]
    [OpenApiOperation(operationId: nameof(GetTransaction), tags: new[] { nameof(Transaction) })]
    [OpenApiParameter(name: "transactionId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Get Transaction By TransactionId")]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Get Transactions By UserId")]
    [OpenApiParameter(name: "channelId", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Get Transactions By ChannelId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "application/json", typeof(List<DTO.Transaction.Transaction>), Description = "The OK response")]
    public IActionResult GetTransaction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Transaction")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("transactionId", out var transactionId);
            queryDictionary.TryGetValue("userId", out var userId);
            queryDictionary.TryGetValue("channelId", out var channelId);

            var result = new GetTransactionResponse();

            if (!string.IsNullOrEmpty(transactionId))
            {
                var transaction = Mapper.Map<DTO.Transaction.Transaction>(transactionService.GetTransactionById(transactionId));
                result.Add(transaction);

                if (transaction.UserId != user.id)
                {
                    Logger.Warning("User {userId} tried to get transaction {transactionId} but is not the owner of the transaction", user.id, transactionId);
                    return new ForbidResult();
                }
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                if (userId != user.id)
                {
                    Logger.Warning("User {userId} tried to get transactions of user {userIdToGet} but is not the owner of the user", user.id, userId);
                    return new ForbidResult();
                }

                result = new GetTransactionResponse(
                                transactionService.GetTransactionsByUser(userId)
                                                  .Select(p => Mapper.Map<DTO.Transaction.Transaction>(p)));
            }
            else if (!string.IsNullOrEmpty(channelId))
            {
                result = new GetTransactionResponse(
                                transactionService.GetTransactionsByChannel(channelId)
                                                  .Select(p => Mapper.Map<DTO.Transaction.Transaction>(p)));
            }
            return new JsonResult(result, new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
            });
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetTransaction), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(ClaimSupportTokens))]
    [OpenApiOperation(operationId: nameof(ClaimSupportTokens), tags: new[] { nameof(Transaction) })]
    [OpenApiRequestBody("application/json", typeof(ClaimSupportTokensRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "The transaction id.")]
    public IActionResult ClaimSupportTokens(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Transaction/ClaimSupportTokens")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            ClaimSupportTokensRequest data = JsonConvert.DeserializeObject<ClaimSupportTokensRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            var transactionId = transactionService.ClaimSupportTokens(data.UserId, data.Amount);
            var transaction = transactionService.GetTransactionById(transactionId);
            return transaction.TransactionState == DB.Enum.TransactionState.Success
                ? new OkObjectResult(transaction.id)
                : new BadRequestObjectResult(transaction.id);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(ClaimSupportTokens), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(DownloadVideo))]
    [OpenApiOperation(operationId: nameof(DownloadVideo), tags: new[] { nameof(Transaction) })]
    [OpenApiRequestBody("application/json", typeof(DownloadVideoRequest), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "The transaction id.")]
    public IActionResult DownloadVideo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Transaction/DownloadVideo")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();
            using var videoService = new VideoService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            DownloadVideoRequest data = JsonConvert.DeserializeObject<DownloadVideoRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (user.id != data.UserId) return new ForbidResult();

            if (!videoService.IsVideoArchived(data.VideoId))
            {
                Logger.Warning("User {userId} requested to download a video {videoId} that is not archived.", data.UserId, data.VideoId);
                return new BadRequestObjectResult($"Video {data.VideoId} is not archived.");
            }

            var transactionId = transactionService.NewDownloadVideoTransaction(data.UserId, data.VideoId);
            var transaction = transactionService.GetTransactionById(transactionId);
            return transaction.TransactionState == DB.Enum.TransactionState.Success
                ? new OkObjectResult(transaction.id)
                : new BadRequestObjectResult(transaction.id);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(DownloadVideo), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(IsVideoDownloaded))]
    [OpenApiOperation(operationId: nameof(IsVideoDownloaded), tags: new[] { nameof(Transaction) })]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "UserId")]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "VideoId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "application/json", typeof(bool), Description = "The OK response")]
    public IActionResult IsVideoDownloaded(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Transaction/IsVideoDownloaded")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("userId", out var userId);
            queryDictionary.TryGetValue("videoId", out var videoId);

            return user.id != userId
                ? new ForbidResult()
                : string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(userId)
                    ? new BadRequestObjectResult("Missing parameters.")
                    : new OkObjectResult(transactionService.IsVideoDownloaded(videoId, userId));
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(IsVideoDownloaded), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(IsChannelSupportedBeforeVideoArchived))]
    [OpenApiOperation(operationId: nameof(IsChannelSupportedBeforeVideoArchived), tags: new[] { nameof(Transaction) })]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Search with UserId")]
    [OpenApiParameter(name: "videoId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Search with VideoId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "application/json", typeof(bool), Description = "The OK response")]
    public IActionResult IsChannelSupportedBeforeVideoArchived(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Transaction/IsChannelSupportedBeforeVideoArchived")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();
            using var videoService = new VideoService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("userId", out var userId);
            queryDictionary.TryGetValue("videoId", out var videoId);

            if (!string.IsNullOrEmpty(userId))
            {
                if (userId != user.id)
                {
                    Logger.Warning("User {userId} tried to get transactions of user {userIdToGet} but is not the owner of the user", user.id, userId);
                    return new ForbidResult();
                }
            }
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(videoId))
            {
                return new BadRequestObjectResult("Missing parameters.");
            }

            var video = videoService.GetVideoById(videoId);
            var transaction = transactionService.GetFirstSupportTransaction(video.ChannelId, userId);
            return new OkObjectResult(null != transaction && video.ArchivedTime >= transaction.Timestamp);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(IsChannelSupportedBeforeVideoArchived), e);
            return new InternalServerErrorResult();
        }
    }

}

