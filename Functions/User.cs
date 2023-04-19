using LivestreamRecorderBackend.DTO.User;
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
using System.Net;
using System.Security.Claims;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class User
{
    private static ILogger Logger => Helper.Log.Logger;

    [FunctionName(nameof(GetUser))]
    [OpenApiOperation(operationId: nameof(GetUser), tags: new[] { nameof(User) })]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public IActionResult GetUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            return new JsonResult(result, new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
            });
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetUser), e);
            return new InternalServerErrorResult();
        }
    }


    [FunctionName(nameof(CreateOrUpdateUserFromEasyAuth))]
    [OpenApiOperation(operationId: nameof(CreateOrUpdateUserFromEasyAuth), tags: new[] { nameof(User) })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Issuer not supported")]
    public IActionResult CreateOrUpdateUserFromEasyAuth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            using var userService = new UserService();

#if DEBUG
            if (req.Host.Host == "localhost")
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return new OkResult();
            }
#endif

            if (null == principal
                || null == principal.Identity
                || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

            userService.CreateOrUpdateUserWithOAuthClaims(principal);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is NotSupportedException)
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUserFromEasyAuth), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateUser))]
    [OpenApiOperation(operationId: nameof(UpdateUser), tags: new[] { nameof(User) })]
    [OpenApiRequestBody("application/json", typeof(UpdateUserRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public IActionResult UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var userService = new UserService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            UpdateUserRequest data = JsonConvert.DeserializeObject<UpdateUserRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            user = userService.UpdateUser(data, user);
            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            return new JsonResult(result, new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
            });
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateUser), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(AddUserReferralCode))]
    [OpenApiOperation(operationId: nameof(AddUserReferralCode), tags: new[] { nameof(User) })]
    [OpenApiRequestBody("application/json", typeof(AddUserReferralCodeRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public IActionResult AddUserReferralCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "User/ReferralCode")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var userService = new UserService();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            AddUserReferralCodeRequest data = JsonConvert.DeserializeObject<AddUserReferralCodeRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            if (!string.IsNullOrEmpty(data.id))
            {
                if (data.id != user.id)
                {
                    Logger.Warning("User {userId} tried to add referralCode of user {userIdToModify} but is not the owner of the user", user.id, data.id);
                    return new ForbidResult();
                }
            }
            if (string.IsNullOrEmpty(data.id) || string.IsNullOrEmpty(data.ReferralCode))
            {
                return new BadRequestObjectResult("Missing parameters.");
            }

            user = userService.AddUserReferralCode(data, user);
            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            return new JsonResult(result, new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
            });
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                Logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(AddUserReferralCode), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(GetSupportedChannels))]
    [OpenApiOperation(operationId: nameof(GetSupportedChannels), tags: new[] { nameof(User) })]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Search with UserId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, "application/json", typeof(List<string>), Description = "Channel id array")]
    public IActionResult GetSupportedChannels(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User/SupportedChannels")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            var user = Helper.Auth.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            using var transactionService = new TransactionService();

            IDictionary<string, string> queryDictionary = req.GetQueryParameterDictionary();
            queryDictionary.TryGetValue("userId", out var userId);

            if (!string.IsNullOrEmpty(userId))
            {
                if (userId != user.id)
                {
                    Logger.Warning("User {userId} tried to get transactions of user {userIdToGet} but is not the owner of the user", user.id, userId);
                    return new ForbidResult();
                }
            }
            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Missing parameters.");
            }

            var channelIds = transactionService.GetSupportedChannelsByUser(userId);
            return new OkObjectResult(channelIds);
        }
        catch (Exception e)
        {
            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetSupportedChannels), e);
            return new InternalServerErrorResult();
        }
    }

}

