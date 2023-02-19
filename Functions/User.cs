using LivestreamRecorderBackend.DB.Exceptions;
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
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class User
{
    private static ILogger Logger => Helper.Log.Logger;

    public User()
    {
        Logger.Verbose("Starting up...");
    }

    [FunctionName(nameof(GetUser))]
    [OpenApiOperation(operationId: nameof(GetUser), tags: new[] { nameof(User) })]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public IActionResult GetUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
    {
        try
        {
            using var userService = new UserService();

#if DEBUG
            Helper.Log.LogClaimsPrincipal(principal);
            DB.Models.User? user =
                req.Host.Host == "localhost"
                    ? userService.GetUserById(Environment.GetEnvironmentVariable("ADMIN_USER_ID")!)
                    : userService.GetUserFromClaimsPrincipal(principal);
#else
            if (null == principal
                || null == principal.Identity
                || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();
            DB.Models.User user = userService.GetUserFromClaimsPrincipal(principal);
#endif
            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            return new JsonResult(result, new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
            });
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                Logger.Error(e, "User not found!!");
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

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
            using var userService = new UserService();

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
            if (e is NotSupportedException or EntityNotFoundException)
            {
                Logger.Error(e, "User not found!!");
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUserFromEasyAuth), e);
            return new InternalServerErrorResult();
        }
    }

}

