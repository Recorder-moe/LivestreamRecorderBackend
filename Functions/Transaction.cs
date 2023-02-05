using LivestreamRecorderBackend.DB.Exceptions;
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

namespace LivestreamRecorderBackend.Functions
{
    public class Transaction
    {
        private static ILogger Logger => Helper.Log.Logger;

        [FunctionName(nameof(SupportChannel))]
        [OpenApiOperation(operationId: nameof(SupportChannel), tags: new[] { nameof(Transaction) })]
        [OpenApiRequestBody("application/json", typeof(SupportChannelRequest), Required = true)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
        public IActionResult SupportChannel(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SupportChannel")] HttpRequest req, ClaimsPrincipal principal)
        {
            try
            {
                using var userService = new UserService();
                using var transactionService = new TransactionService();

#if DEBUG
                Helper.Log.LogClaimsPrincipal(principal);
                DB.Models.User user =
                    req.Host.Host == "localhost"
                        ? userService.GetUserById(Environment.GetEnvironmentVariable("ADMIN_USER_ID")!)
                        : userService.GetUserFromClaimsPrincipal(principal);
#else
                if (null == principal
                    || null == principal.Identity
                    || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

                DB.Models.User user = userService.GetUserFromClaimsPrincipal(principal);
#endif

                string requestBody = string.Empty;
                using (StreamReader streamReader = new(req.Body))
                {
                    requestBody = streamReader.ReadToEnd();
                }
                SupportChannelRequest data = JsonConvert.DeserializeObject<SupportChannelRequest>(requestBody)
                    ?? throw new InvalidOperationException("Invalid request body!!");

                if (user.id != data.UserId) return new ForbidResult();

                var transactionId = transactionService.SupportChannel(data.UserId, data.ChannelId, data.Amount);
                var transaction = transactionService.GetTransactionById(transactionId);
                return transaction.TransactionState == DB.Enum.TransactionState.Success
                    ? new OkObjectResult(transaction.id)
                    : new BadRequestObjectResult(transaction.id);
            }
            catch (Exception e)
            {
                if (e is NotSupportedException or EntityNotFoundException)
                {
                    Logger.Error(e, "User not found!!");
                    Helper.Log.LogClaimsPrincipal(principal);
                    return new BadRequestObjectResult(e.Message);
                }

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
                using var userService = new UserService();
                using var transactionService = new TransactionService();

#if DEBUG
                Helper.Log.LogClaimsPrincipal(principal);
                DB.Models.User user =
                    req.Host.Host == "localhost"
                        ? userService.GetUserById(Environment.GetEnvironmentVariable("ADMIN_USER_ID")!)
                        : userService.GetUserFromClaimsPrincipal(principal);
#else
                if (null == principal
                    || null == principal.Identity
                    || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

                DB.Models.User user = userService.GetUserFromClaimsPrincipal(principal);
#endif

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
                if (e is NotSupportedException or EntityNotFoundException)
                {
                    return new BadRequestObjectResult(e.Message);
                }

                Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetTransaction), e);
                return new InternalServerErrorResult();
            }
        }

        [FunctionName(nameof(ClaimSupportTokens))]
        [OpenApiOperation(operationId: nameof(ClaimSupportTokens), tags: new[] { nameof(Transaction) })]
        [OpenApiRequestBody("application/json", typeof(ClaimSupportTokensRequest), Required = true)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
        public IActionResult ClaimSupportTokens(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ClaimSupportTokens")] HttpRequest req, ClaimsPrincipal principal)
        {
            try
            {
                using var userService = new UserService();
                using var transactionService = new TransactionService();

#if DEBUG
                Helper.Log.LogClaimsPrincipal(principal);
                DB.Models.User user =
                    req.Host.Host == "localhost"
                        ? userService.GetUserById(Environment.GetEnvironmentVariable("ADMIN_USER_ID")!)
                        : userService.GetUserFromClaimsPrincipal(principal);
#else
                if (null == principal
                    || null == principal.Identity
                    || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

                DB.Models.User user = userService.GetUserFromClaimsPrincipal(principal);
#endif

                string requestBody = string.Empty;
                using (StreamReader streamReader = new(req.Body))
                {
                    requestBody = streamReader.ReadToEnd();
                }
                ClaimSupportTokensRequest data = JsonConvert.DeserializeObject<ClaimSupportTokensRequest>(requestBody)
                    ?? throw new InvalidOperationException("Invalid request body!!");

                if (user.id != data.UserId) return new ForbidResult();

                var transactionId = transactionService.ClaimSupportTokens(data.UserId,data.Amount);
                var transaction = transactionService.GetTransactionById(transactionId);
                return transaction.TransactionState == DB.Enum.TransactionState.Success
                    ? new OkObjectResult(transaction.id)
                    : new BadRequestObjectResult(transaction.id);
            }
            catch (Exception e)
            {
                if (e is NotSupportedException or EntityNotFoundException)
                {
                    Logger.Error(e, "User not found!!");
                    Helper.Log.LogClaimsPrincipal(principal);
                    return new BadRequestObjectResult(e.Message);
                }

                Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(SupportChannel), e);
                return new InternalServerErrorResult();
            }
        }
    }
}

