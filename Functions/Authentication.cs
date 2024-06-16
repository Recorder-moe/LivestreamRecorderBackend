using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using Serilog;
using Log = LivestreamRecorderBackend.Helper.Log;

namespace LivestreamRecorderBackend.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class Authentication(GitHubService githubService)
{
    private readonly string _frontEndUri = Environment.GetEnvironmentVariable("FrontEndUri") ?? "http://localhost:4200";
    private readonly IAuthenticationCodeHandlerService _githubService = githubService;

    private static ILogger Logger => Log.Logger;

    [Function(nameof(GitHubSignin))]
    [OpenApiOperation(operationId: nameof(GitHubSignin), tags: ["Authentication"])]
    [OpenApiParameter(name: "code", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "state", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "error", In = ParameterLocation.Query, Required = false, Type = typeof(string))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Exception")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "BadRequest")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Redirect, Description = "Success")]
    // skipcq: CS-R1073
    public async Task<IActionResult> GitHubSignin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "signin-github")]
        HttpRequest req)
    {
        Logger.Debug($"{nameof(GitHubSignin)} triggered");

        string code = req.Query["code"]!;
        string state = req.Query["state"]!;
        string? error = req.Query["error"];

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return new BadRequestObjectResult("code must be set.");

        if (!string.IsNullOrEmpty(error))
        {
            Logger.Error(error);
            throw new InvalidOperationException(error);
        }

        req.Headers.TryGetValue("Referer", out StringValues referer);
        string backend = referer.Count != 0 ? referer.First() ?? "" : req.GetDisplayUrl();

        string idToken = await _githubService.GetIdTokenAsync(
            authorizationCode: code,
            redirectUri: AuthenticationService.GetRedirectUri(backend, "api/signin-github"));

        // Treat it as an implicit flow-style URL so that my front-end can easily handle it with packages (angular-oauth2-oidc).
        return new RedirectResult(
            $"{_frontEndUri}/pages/login-redirect#state={HttpUtility.UrlEncode(state)}&access_token={idToken}&token_type=Bearer&expires_in=3599&scope=email%20profile&authuser=0&prompt=none");
    }
}
