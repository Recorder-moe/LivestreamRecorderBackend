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
using System.Net;
using System.Security.Claims;
using System.Web.Http;

namespace LivestreamRecorderBackend
{
    public class User
    {
        private static ILogger Logger => Helper.Log.Logger;

        public User()
        {
            Logger.Verbose("Starting up...");
        }

        [FunctionName(nameof(GetUser))]
        [OpenApiOperation(operationId: nameof(GetUser), tags: new[] { "User" })]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetUserResponse), Description = "User")]
        public IActionResult GetUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
        {
            try
            {
                using var userService = new UserService();

                if (null == principal) return new UnauthorizedResult();

#if DEBUG
                Helper.Log.LogClaimsPrincipal(principal);
#endif

                var uid = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                var issuer = principal.FindFirst("iss")?.Value;
                DB.Models.User? user = null;
                switch (issuer)
                {
                    case "https://accounts.google.com":
                        UserService.CheckUID(principal);

                        user = userService.GetUserByGoogleUID(uid!);
                        break;
                    default:
                        throw new NotSupportedException($"Issuer {issuer} is not support!!");
                }

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
        [OpenApiOperation(operationId: nameof(CreateOrUpdateUserFromEasyAuth), tags: new[] { "User" })]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "The OK response")]
        public IActionResult CreateOrUpdateUserFromEasyAuth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "User")] HttpRequest req, ClaimsPrincipal principal)
        {
            try
            {
                using var userService = new UserService();

                if (null == principal) return new UnauthorizedResult();

#if DEBUG
                Helper.Log.LogClaimsPrincipal(principal);
#endif
                userService.CreateOrUpdateUser(principal);
                return new OkResult();
            }
            catch (Exception e)
            {
                Logger.Error("Unhandled exception in {apiname}: {exception}", nameof(CreateOrUpdateUserFromEasyAuth), e);
                return new InternalServerErrorResult();
            }
        }

    }
}

