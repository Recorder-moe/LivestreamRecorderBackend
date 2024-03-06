using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.User;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Omu.ValueInjecter;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;

namespace LivestreamRecorderBackend.Functions;

public class User
{
    private readonly ILogger _logger;
    private readonly UserService _userService;
    private readonly AuthenticationService _authenticationService;

    public User(
        ILogger logger,
        UserService userService,
        AuthenticationService authenticationService)
    {
        _logger = logger;
        _userService = userService;
        _authenticationService = authenticationService;
    }

    [FunctionName(nameof(GetUserAsync))]
    [OpenApiOperation(operationId: nameof(GetUserAsync), tags: new[] { nameof(User) })]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> GetUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();

            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            var data = JsonSerializer.SerializeToUtf8Bytes(result);
            return new FileContentResult(data, "application/json");
        }
        catch (Exception e)
        {
            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetUserAsync), e);
            return new InternalServerErrorResult();
        }
    }


    [FunctionName(nameof(CreateOrUpdateUser))]
    [OpenApiOperation(operationId: nameof(CreateOrUpdateUser), tags: new[] { nameof(User) })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Issuer not supported")]
    // skipcq: CS-R1073
    public async Task<IActionResult> CreateOrUpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "User")] HttpRequest req)
    {
        try
        {
            if (!req.Headers.TryGetValue("Authorization", out var authHeader)
                || authHeader.Count == 0) return new UnauthorizedResult();
            var token = authHeader.First()?.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
            if (null == token) return new UnauthorizedResult();
            var principal = await _authenticationService.GetUserInfoFromTokenAsync(token);

            if (null == principal
                || null == principal.Identity
                || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

            await _userService.CreateOrUpdateUserWithOAuthClaimsAsync(principal);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException)
            {
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUser), e);
            return new InternalServerErrorResult();
        }
    }

    [FunctionName(nameof(UpdateUserAsync))]
    [OpenApiOperation(operationId: nameof(UpdateUserAsync), tags: new[] { nameof(User) })]
    [OpenApiRequestBody("application/json", typeof(UpdateUserRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> UpdateUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "User")] HttpRequest req)
    {
        try
        {
            var user = await _userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();

            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            UpdateUserRequest data = JsonSerializer.Deserialize<UpdateUserRequest>(requestBody)
                ?? throw new InvalidOperationException("Invalid request body!!");

            user = await _userService.UpdateUserAsync(data, user);
            var result = new GetUserResponse();
            if (null != user) result.InjectFrom(user);
            var resultdata = JsonSerializer.SerializeToUtf8Bytes(result);
            return new FileContentResult(resultdata, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                _logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            _logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateUserAsync), e);
            return new InternalServerErrorResult();
        }
    }
}

