using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Serilog;

namespace LivestreamRecorderBackend.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class Utility(ILogger logger)
{
    [Function(nameof(Wake))]
    [OpenApiOperation(operationId: nameof(Wake), tags: [nameof(Utility)])]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Waked.")]
    public IActionResult Wake(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthz")]
        HttpRequest req)
    {
        Wake();
        return new OkResult();
    }

#if RELEASE && Windows
    [Function(nameof(WakeByTimer))]
    public void WakeByTimer([TimerTrigger("0 * * * * *")] TimerInfo timerInfo)
        => Wake();
#endif

    private void Wake()
    {
#if !RELEASE
        logger.Verbose("Wake executed at: {time}", DateTime.Now);
#endif
    }
}
