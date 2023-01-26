using LivestreamRecorderBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System;
using System.Net;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend;

public class Auth
{
    private static ILogger? _logger;
    private readonly GoogleOIDCService _googleOIDCService;
    private readonly string _frontEndUri;

    public static ILogger Logger
    {
        get
        {
            if (null == _logger
                || _logger.GetType() != typeof(Serilog.Core.Logger))
            {
                _logger = MakeLogger();
            }
            return _logger;
        }
        set => _logger = value;
    }

    public Auth()
    {
#if DEBUG
        Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));
#endif

        _frontEndUri = Environment.GetEnvironmentVariable("FrontEndUri") ?? "http://localhost:4200";
        _googleOIDCService = new GoogleOIDCService();

        Logger.Verbose("Starting up...");
    }

    public static ILogger MakeLogger()
    {
        Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));

        var logger = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Fatal)
                        .MinimumLevel.Override("System", LogEventLevel.Fatal)
                        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}",
                                         restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .WriteTo.Seq(serverUrl: Environment.GetEnvironmentVariable("Seq_ServerUrl")!,
                                     apiKey: Environment.GetEnvironmentVariable("Seq_ApiKey"),
                                     restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .Enrich.FromLogContext()
                        .CreateLogger();
        return logger;
    }

    [FunctionName(nameof(GoogleOIDCSignin))]
    [OpenApiOperation(operationId: nameof(GoogleOIDCSignin), tags: new[] { "Auth" })]
    [OpenApiParameter(name: "code", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "state", In = ParameterLocation.Query, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "error", In = ParameterLocation.Query, Required = false, Type = typeof(string))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Exception")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "BadRequest")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Redirect, Description = "Success")]
    public async Task<IActionResult> GoogleOIDCSignin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "Auth/oidc/signin")] HttpRequest req)
    {
        Logger.Debug($"{nameof(GoogleOIDCSignin)} triggered");

        Logger.Debug("Get display url: {url}", req.GetDisplayUrl());
        _googleOIDCService.SetupRedirectUri(req.GetDisplayUrl());

        string code = req.Query["code"]!;
        string state = req.Query["state"]!;
        string? error = req.Query["error"];

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return new BadRequestObjectResult("code must be set.");

#if DEBUG
        Logger.Debug("Code: {code}", code);
        Logger.Debug("State: {state}", state);
        Logger.Debug("error: {error}", error);
#endif

        if (state != "12345GG")
        {
            Logger.Warning("Invalid state: {state}", state);
            return new BadRequestResult();
        }

        if (!string.IsNullOrEmpty(error))
        {
            Logger.Error(error);
            throw new Exception(error);
        }

        string idToken = await _googleOIDCService.GetIdTokenAsync(code);

#if DEBUG
        Logger.Debug("idToken: {idToken}", idToken);
#endif

        return new RedirectResult($"{_frontEndUri}/pages/login?idToken={idToken}");
    }
}

