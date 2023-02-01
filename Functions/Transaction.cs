using LivestreamRecorderBackend.DB.Exceptions;
using LivestreamRecorderBackend.DB.Models;
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
    }
}

