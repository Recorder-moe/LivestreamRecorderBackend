using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace LivestreamRecorderBackend.Functions;

public class Utility
{
    private readonly ILogger _logger;

    public Utility(
        ILogger logger)
    {
        _logger = logger;
    }

    [Function(nameof(Wake))]
    [OpenApiOperation(operationId: nameof(Wake), tags: new[] { nameof(Utility) })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Waked.")]
    public IActionResult Wake(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Utility/Wake")]
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
#pragma warning disable IDE0022 // 使用方法的運算式主體
        _logger.Verbose("Wake executed at: {time}", System.DateTime.Now);
#pragma warning restore IDE0022 // 使用方法的運算式主體
#endif
    }
}
