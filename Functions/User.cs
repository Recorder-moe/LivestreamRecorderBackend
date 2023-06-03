using LivestreamRecorder.DB.Exceptions;
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
    private readonly ILogger _logger;
    private readonly UserService _userService;

    public User(
        ILogger logger,
        UserService userService)
    {
        _logger = logger;
        _userService = userService;
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
            var user = _userService.AuthAndGetUser(principal, req.Host.Host == "localhost");
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
            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetUser), e);
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

            _userService.CreateOrUpdateUserWithOAuthClaims(principal);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUserFromEasyAuth), e);
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
            var user = _userService.AuthAndGetUser(principal, req.Host.Host == "localhost");
            if (null == user) return new UnauthorizedResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = streamReader.ReadToEnd();
            }
            UpdateUserRequest data = JsonConvert.DeserializeObject<UpdateUserRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            user = _userService.UpdateUser(data, user);
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
                _logger.Warning(e, e.Message);
                Helper.Log.LogClaimsPrincipal(principal);
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateUser), e);
            return new InternalServerErrorResult();
        }
    }
}

