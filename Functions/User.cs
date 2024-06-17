using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorderBackend.DTO.User;
using LivestreamRecorderBackend.Services;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Primitives;
using Omu.ValueInjecter;
using Serilog;

namespace LivestreamRecorderBackend.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class User(ILogger logger,
                  UserService userService,
                  AuthenticationService authenticationService)
{
    [Function(nameof(GetUserAsync))]
    [OpenApiOperation(operationId: nameof(GetUserAsync), tags: [nameof(User)])]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> GetUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();

            var result = new GetUserResponse();
            result.InjectFrom(user);
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(result);
            return new FileContentResult(data, "application/json");
        }
        catch (Exception e)
        {
            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(GetUserAsync), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(CreateOrUpdateUser))]
    [OpenApiOperation(operationId: nameof(CreateOrUpdateUser), tags: [nameof(User)])]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Issuer not supported")]
    // skipcq: CS-R1073
    public async Task<IActionResult> CreateOrUpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "User")]
        HttpRequest req)
    {
        try
        {
            if (!req.Headers.TryGetValue("Authorization", out StringValues authHeader)
                || authHeader.Count == 0) return new UnauthorizedResult();

            string? token = authHeader.First()?.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last();
            if (null == token) return new UnauthorizedResult();
            ClaimsPrincipal principal = await authenticationService.GetUserInfoFromTokenAsync(token);

            if (null == principal.Identity
                || !principal.Identity.IsAuthenticated) return new UnauthorizedResult();

            await userService.CreateOrUpdateUserWithOAuthClaimsAsync(principal);
            return new OkResult();
        }
        catch (Exception e)
        {
            if (e is NotSupportedException or EntityNotFoundException) return new BadRequestObjectResult(e.Message);

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUser), e);
            return new InternalServerErrorResult();
        }
    }

    [Function(nameof(UpdateUserAsync))]
    [OpenApiOperation(operationId: nameof(UpdateUserAsync), tags: [nameof(User)])]
    [OpenApiRequestBody("application/json", typeof(UpdateUserRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "User not found.")]
    public async Task<IActionResult> UpdateUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "User")]
        HttpRequest req)
    {
        try
        {
            LivestreamRecorder.DB.Models.User? user = await userService.AuthAndGetUserAsync(req.Headers);
            if (null == user) return new UnauthorizedResult();

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            UpdateUserRequest data = JsonSerializer.Deserialize<UpdateUserRequest>(requestBody)
                                     ?? throw new InvalidOperationException("Invalid request body!!");

            user = await userService.UpdateUserAsync(data, user);
            var result = new GetUserResponse();
            result.InjectFrom(user);
            byte[] resultData = JsonSerializer.SerializeToUtf8Bytes(result);
            return new FileContentResult(resultData, "application/json");
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                logger.Warning(e, e.Message);
                return new BadRequestObjectResult(e.Message);
            }

            logger.Error("Unhandled exception in {apiname}: {exception}", nameof(UpdateUserAsync), e);
            return new InternalServerErrorResult();
        }
    }
}
