using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace LivestreamRecorderBackend.Functions;

public class Authentication
{
    private static ILogger Logger => Helper.Log.Logger;
    private readonly string _frontEndUri;
    private readonly IAuthenticationCodeHandlerService _githubService;

    public Authentication(GithubService githubService)
    {
        _frontEndUri = Environment.GetEnvironmentVariable("FrontEndUri") ?? "http://localhost:4200";
        _githubService = githubService;
    }

    [FunctionName(nameof(GithubSignin))]
    [OpenApiOperation(operationId: nameof(GithubSignin), tags: new[] { "Authentication" })]
    [OpenApiParameter(name: "code", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "state", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "error", In = ParameterLocation.Query, Required = false, Type = typeof(string))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Exception")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "BadRequest")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Redirect, Description = "Success")]
    public async Task<IActionResult> GithubSignin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "signin-github")] HttpRequest req)
    {
        Logger.Debug($"{nameof(GithubSignin)} triggered");

        string code = req.Query["code"]!;
        string state = req.Query["state"]!;
        string? error = req.Query["error"];

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return new BadRequestObjectResult("code must be set.");

        if (!string.IsNullOrEmpty(error))
        {
            Logger.Error(error);
            throw new Exception(error);
        }

        var backEnd = req.Headers.Referer.FirstOrDefault() ?? req.GetDisplayUrl();

        string idToken = await _githubService.GetIdTokenAsync(
            authorization_code: code,
            redirectUri: AuthenticationService.GetRedirectUri(backEnd, "api/signin-github"));

        // Treat it as an implicit flow-style URL so that my front-end can easily handle it with packages (angular-oauth2-oidc).
        return new RedirectResult($"{_frontEndUri}/pages/login-redirect#state={HttpUtility.UrlEncode(state)}&access_token={idToken}&token_type=Bearer&expires_in=3599&scope=email%20profile&authuser=0&prompt=none");
    }
}

