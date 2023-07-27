using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Serilog;
using System.Net;

namespace LivestreamRecorderBackend.Functions;

public class Utility
{
    private readonly ILogger _logger;

    public Utility(
        ILogger logger)
    {
        _logger = logger;
    }

    [FunctionName(nameof(Wake))]
    [OpenApiOperation(operationId: nameof(Wake), tags: new[] { nameof(Utility) })]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Waked.")]
    public IActionResult Wake([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Utility/Wake")] HttpRequest req)
    {
        Wake();
        return new OkResult();
    }

#if !DEBUG && Windows
    [FunctionName(nameof(WakeByTimer))]
    public void WakeByTimer([TimerTrigger("0 * * * * *")] TimerInfo timerInfo)
        => Wake();
#endif

    private void Wake()
    {
#if DEBUG
#pragma warning disable IDE0022 // 使用方法的運算式主體
        _logger.Verbose("Wake executed at: {time}", System.DateTime.Now);
#pragma warning restore IDE0022 // 使用方法的運算式主體
#endif
    }
}
